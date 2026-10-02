// Research-only physical-fit gate. Never linked into the worker or shipped.
// Uses the qualified F1 codec/schema and exact pinned SQLite, not fake blobs.
#include "Backend.hpp"
#include "Derived.hpp"
#include "sqlite3.h"
#include <cstring>
#include <filesystem>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <vector>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <sys/stat.h>
#endif
using namespace CarbonLuau::Persistence;
namespace {
void Check(int Rc) { if (Rc!=SQLITE_OK && Rc!=SQLITE_DONE) throw std::runtime_error("SQLite probe failure code="+std::to_string(Rc)); }
void Sql(sqlite3* Db,const char* Text) { Check(sqlite3_exec(Db,Text,nullptr,nullptr,nullptr)); }
int64_t Scalar(sqlite3* Db,const char* Text) {
    sqlite3_stmt* S=nullptr; Check(sqlite3_prepare_v2(Db,Text,-1,&S,nullptr));
    if(sqlite3_step(S)!=SQLITE_ROW) throw std::runtime_error("missing scalar");
    auto V=sqlite3_column_int64(S,0); sqlite3_finalize(S); return V;
}
struct Statement {
    sqlite3_stmt* S=nullptr;
    Statement(sqlite3* Db,const char* Text) { Check(sqlite3_prepare_v2(Db,Text,-1,&S,nullptr)); }
    ~Statement(){sqlite3_finalize(S);}
    void Blob(int I,const std::string& V){Check(sqlite3_bind_blob(S,I,V.data(),int(V.size()),SQLITE_TRANSIENT));}
    void Blob(int I,const Bytes& V){Check(sqlite3_bind_blob(S,I,V.data(),int(V.size()),SQLITE_TRANSIENT));}
    void Integer(int I,int64_t V){Check(sqlite3_bind_int64(S,I,V));}
    int Step(){int R=sqlite3_step(S); sqlite3_reset(S); sqlite3_clear_bindings(S); return R;}
};
struct Row { Identity Id; };
std::string StringValue(unsigned Field,size_t Row,bool Unique,size_t Width=1024){
    if(!Unique)return std::string(Width,char('a'+Field));
    uint64_t State=(uint64_t(Row)+1)*6364136223846793005ull+Field;
    std::string Result=std::to_string(Row)+":"+std::to_string(Field)+":";
    while(Result.size()<Width){State=State*6364136223846793005ull+1442695040888963407ull;Result.push_back(char(' '+((State>>32)%95)));}
    return Result;
}
uint64_t Allocation(const std::filesystem::path& Path){
    if(!std::filesystem::exists(Path))return 0;
#ifdef _WIN32
    HANDLE H=CreateFileW(Path.c_str(),FILE_READ_ATTRIBUTES,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,0,nullptr);
    FILE_STANDARD_INFO Info{}; bool Good=H!=INVALID_HANDLE_VALUE && GetFileInformationByHandleEx(H,FileStandardInfo,&Info,sizeof(Info));
    if(H!=INVALID_HANDLE_VALUE)CloseHandle(H);
    if(!Good)throw std::runtime_error("allocation measurement");return uint64_t(Info.AllocationSize.QuadPart);
#else
    struct stat Info{};if(stat(Path.c_str(),&Info)!=0 || Info.st_blocks<0)throw std::runtime_error("allocation measurement");return uint64_t(Info.st_blocks)*512;
#endif
}
void Observe(sqlite3* Db,const std::filesystem::path& Dir,const char* Stage,uint64_t Primary,uint64_t Derived,uint64_t Count){
    std::cout<<"[CarbonLuau:DerivedCapacity] stage="<<Stage<<" primary_logical="<<Primary
        <<" derived_logical_upper="<<Derived<<" entries="<<Count<<" pages="<<Scalar(Db,"PRAGMA page_count")
        <<" db_eof="<<std::filesystem::file_size(Dir/"store.sqlite3")
        <<" journal_eof="<<(std::filesystem::exists(Dir/"store.sqlite3-journal")?std::filesystem::file_size(Dir/"store.sqlite3-journal"):0)<<std::endl;
    std::cout<<"[CarbonLuau:DerivedCapacity] observed_allocated="<<Allocation(Dir/"store.sqlite3")+Allocation(Dir/"store.sqlite3-journal")<<std::endl;
}
}
int main(int Count,char** Args){
    sqlite3* Db=nullptr;
    try {
        if(Count!=3 && Count!=4) throw std::runtime_error("expected absolute fixture directory and wide|compact|split|production|production-expanded|production-expanded-fragmented|production-local|production-fragmented|verify [unique]");
        bool Unique=Count==4 && std::string(Args[3])=="unique";
        if(Count==4 && !Unique)throw std::runtime_error("unknown scalar corpus");
        auto Dir=std::filesystem::absolute(Args[1]);
        // Postfailure evidence only: normal production startup proves integrity,
        // every F1 envelope/quota and the complete derived graph. Do not resume
        // maintenance or issue an author mutation in this diagnostic mode.
        if(std::string(Args[2])=="verify"){
            if(Count!=3 || !std::filesystem::exists(Dir/"store.sqlite3"))throw std::runtime_error("existing fixture required");
            {Backend Verify(Dir,Clock::now()+std::chrono::seconds(30));if(!Verify.Available())throw std::runtime_error("postfailure backend unavailable");}
            Check(sqlite3_open_v2((Dir/"store.sqlite3").string().c_str(),&Db,SQLITE_OPEN_READONLY,nullptr));
            Observe(Db,Dir,"postfailure-startup-verified",Scalar(Db,"SELECT Bytes FROM Totals"),Scalar(Db,"SELECT Bytes FROM DerivedTotals"),Scalar(Db,"SELECT count(*) FROM DerivedEntries"));
            std::cout<<"[CarbonLuau:DerivedCapacity] primary_records="<<Scalar(Db,"SELECT count(*) FROM Records")<<" primary_codec_quota_and_derived_graph_startup_validation=true no_maintenance_resumed=true"<<std::endl;
            sqlite3_close(Db);return 0;
        }
        bool Compact=std::string(Args[2])=="compact";
        bool Split=std::string(Args[2])=="split";
        bool Local=std::string(Args[2])=="production-local";
        bool Fragmented=std::string(Args[2])=="production-fragmented" ||
            std::string(Args[2])=="production-expanded-fragmented";
        bool Expanded=std::string(Args[2])=="production-expanded" ||
            std::string(Args[2])=="production-expanded-fragmented";
        bool Production=std::string(Args[2])=="production" || Expanded || Local || Fragmented;
        if(!Compact && !Split && !Production && std::string(Args[2])!="wide")throw std::runtime_error("layout");
        if(std::filesystem::exists(Dir))throw std::runtime_error("fixture already exists; never overwrite");
        std::filesystem::create_directories(Dir);
        {Backend Initial(Dir,Clock::now()+std::chrono::seconds(30));}
        Check(sqlite3_open_v2((Dir/"store.sqlite3").string().c_str(),&Db,SQLITE_OPEN_READWRITE,nullptr));
        std::cout<<"[CarbonLuau:DerivedCapacity] sqlite="<<sqlite3_libversion()<<" source="<<sqlite3_sourceid()<<" layout="<<Args[2]<<" unique="<<Unique<<std::endl;
        Sql(Db,"PRAGMA synchronous=EXTRA; PRAGMA journal_mode=PERSIST; PRAGMA cache_size=-4096; PRAGMA cache_spill=OFF; PRAGMA temp_store=MEMORY; PRAGMA mmap_size=0; PRAGMA journal_size_limit=-1;");
        const auto PageLimit=Expanded?262144:131072;
        const auto PageCommand="PRAGMA max_page_count="+std::to_string(PageLimit);
        if(Scalar(Db,PageCommand.c_str())!=PageLimit || Scalar(Db,"PRAGMA page_size")!=4096 || Scalar(Db,"PRAGMA synchronous")!=3)throw std::runtime_error("profile");
        Value Root; Root.Type=Kind::Map;
        // All eight legal queryable fields are represented in every primary row.
        for(int F=0;F<8;++F){auto V=std::make_shared<Value>(); V->Type=Kind::String; V->String=std::string(1024,char('a'+F)); Root.Map.emplace_back("F"+std::to_string(F),V);}
        std::vector<Row> Rows;
        uint64_t Primary=0,Derived=0,Entries=0;
        const uint64_t Target=Local?16ull*1024*1024-200000:255ull*1024*1024;
        const uint64_t PerNamespace=Local?Target:8ull*1024*1024;
        {
            Statement Insert(Db,"INSERT INTO Records VALUES(?1,?2,?3,?4,?5)");
            Statement Quota(Db,"INSERT INTO Quotas VALUES(?1,?2,?3,1)");
            unsigned N=0;
            while(Primary<Target && Rows.size()<100000){
                uint64_t NsBytes=0,NsKeys=0; std::string Package="capacity"+std::to_string(N++), Ns=std::string(1,'\1')+Package;
                // One namespace < 16MiB; one store; < 10,000 records.
                while(NsBytes<PerNamespace && Primary<Target && Rows.size()<100000){
                    Sql(Db,"BEGIN IMMEDIATE");
                    for(unsigned B=0;B<32 && NsBytes<PerNamespace && Primary<Target && Rows.size()<100000;++B){
                        Identity Id{true,Package,"S","K"+std::to_string(NsKeys)};
                        for(unsigned F=0;F<8;++F)Root.Map[F].second->String=StringValue(F,Rows.size(),Unique,Fragmented?256:1024);
                        auto Envelope=Encode(Id,Root,Clock::now()+std::chrono::seconds(5));
                        const auto Charge=Id.Store.size()+Id.Key.size()+Envelope.size();
                        Insert.Blob(1,Ns); Insert.Blob(2,Id.Store); Insert.Blob(3,Id.Key); Insert.Blob(4,Envelope); Insert.Integer(5,Charge); Check(Insert.Step());
                        Rows.push_back({Id}); NsBytes+=Charge; Primary+=Charge; ++NsKeys;
                    }
                    Sql(Db,"COMMIT");
                }
                Sql(Db,"BEGIN IMMEDIATE"); Quota.Blob(1,Ns); Quota.Integer(2,NsBytes); Quota.Integer(3,NsKeys); Check(Quota.Step()); Sql(Db,"COMMIT");
            }
            Statement Total(Db,"UPDATE Totals SET Bytes=?1,Keys=?2,Namespaces=?3 WHERE Id=1");
            Total.Integer(1,Primary); Total.Integer(2,Rows.size()); Total.Integer(3,N); Check(Total.Step());
        }
        Observe(Db,Dir,"primary-filled",Primary,Derived,Entries);
        sqlite3_close(Db); Db=nullptr;
        // Real F1 startup validates every checksum/identity and quota counter.
        {Backend Verify(Dir,Clock::now()+std::chrono::seconds(Expanded?5:30));}
        if(Production){
            Check(sqlite3_open_v2((Dir/"store.sqlite3").string().c_str(),&Db,SQLITE_OPEN_READONLY,nullptr));
            uint64_t Maximum=0,MaximumAllocation=0,Batches=0;
            bool Completed=false;
            {
                Backend Store(Dir,Clock::now()+std::chrono::seconds(30));
                for(unsigned Cycle=0;Cycle<(Expanded?1u:3u);++Cycle){
                std::string Previous;
                for(const auto& Row:Rows)if(Row.Id.Package!=Previous){
                    Previous=Row.Id.Package;
                    for(unsigned F=0;F<(Local?8u:2u);++F)
                        if(!Store.PrepareDerived(Row.Id,"F"+std::to_string(F),Clock::now()+std::chrono::seconds(5),true))throw std::runtime_error("production prepare rejected");
                }
                while(Store.HasDerivedWork() && Batches<30000){
                    if(!Store.MaintainDerived(Clock::now()+std::chrono::seconds(5))){
                        Observe(Db,Dir,"production-maintenance-failed",Primary,Scalar(Db,"SELECT Bytes FROM DerivedTotals"),Scalar(Db,"SELECT count(*) FROM DerivedEntries"));
                        throw std::runtime_error("production maintenance failed before bounded convergence");
                    }
                    ++Batches;
                    const auto Bytes=uint64_t(Scalar(Db,"SELECT Bytes FROM DerivedTotals"));
                    Maximum=std::max(Maximum,Bytes);
                    MaximumAllocation=std::max(MaximumAllocation,Allocation(Dir/"store.sqlite3")+Allocation(Dir/"store.sqlite3-journal"));
                    if(Batches%256==0)Observe(Db,Dir,"production-batch",Primary,Bytes,Scalar(Db,"SELECT count(*) FROM DerivedEntries"));
                }
                Completed=Store.Available() && !Store.HasDerivedWork();
                if(!Completed)throw std::runtime_error("production cycle did not converge");
                std::cout<<"[CarbonLuau:DerivedCapacity] completed_rebuild_cycle="<<Cycle+1<<std::endl;
                }
            }
            Observe(Db,Dir,"production-final",Primary,Scalar(Db,"SELECT Bytes FROM DerivedTotals"),Scalar(Db,"SELECT count(*) FROM DerivedEntries"));
            std::cout<<"[CarbonLuau:DerivedCapacity] production_batches="<<Batches<<" maximum_exact_derived_charge="<<Maximum<<" maximum_observed_allocation="<<MaximumAllocation<<" converged="<<Completed<<std::endl;
            if(!Completed || Maximum<(Local?15ull:63ull)*1024*1024 || Maximum>(Local?16ull:64ull)*1024*1024)throw std::runtime_error("production capacity target unproven");
            sqlite3_close(Db);Db=nullptr;
            const auto Start=Clock::now();
            {Backend Verify(Dir,Start+std::chrono::seconds(Expanded?5:30));}
            std::cout<<"[CarbonLuau:DerivedCapacity] production_full_startup_ms="<<std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now()-Start).count()<<" physical_fit=true"<<std::endl;
            return 0;
        }
        Check(sqlite3_open_v2((Dir/"store.sqlite3").string().c_str(),&Db,SQLITE_OPEN_READWRITE,nullptr));
        Sql(Db,"PRAGMA synchronous=EXTRA; PRAGMA journal_mode=PERSIST; PRAGMA cache_size=-4096; PRAGMA cache_spill=OFF; PRAGMA temp_store=MEMORY; PRAGMA mmap_size=0; PRAGMA journal_size_limit=-1; PRAGMA max_page_count=131072;");
        Sql(Db,Split ?
            "CREATE TABLE IndexEntries(PrefixId INTEGER NOT NULL,Suffix BLOB NOT NULL,RecordKey BLOB NOT NULL,PRIMARY KEY(PrefixId,Suffix,RecordKey)) WITHOUT ROWID" : Compact ?
            "CREATE TABLE IndexEntries(FieldId INTEGER NOT NULL,Generation INTEGER NOT NULL,SortKey BLOB NOT NULL,RecordKey BLOB NOT NULL,PRIMARY KEY(FieldId,Generation,SortKey,RecordKey)) WITHOUT ROWID" :
            "CREATE TABLE IndexEntries(Namespace BLOB NOT NULL,Store BLOB NOT NULL,FieldId INTEGER NOT NULL,Generation INTEGER NOT NULL,SortKey BLOB NOT NULL,RecordKey BLOB NOT NULL,PRIMARY KEY(Namespace,Store,FieldId,Generation,SortKey,RecordKey)) WITHOUT ROWID");
        if(Split)Sql(Db,"CREATE TABLE IndexPrefixes(FieldId INTEGER NOT NULL,Generation INTEGER NOT NULL,Prefix BLOB NOT NULL,PrefixId INTEGER NOT NULL UNIQUE,Entries INTEGER NOT NULL,PRIMARY KEY(FieldId,Generation,Prefix)) WITHOUT ROWID");
        // Generous conservative metadata charge, reserved BEFORE entries, not
        // free/unaccounted metadata. This probe stores no production metadata.
        Derived=1024*1024;
        bool Full=false;
        {
            Statement Insert(Db,Split ? "INSERT INTO IndexEntries VALUES(?1,?2,?3)" : Compact ? "INSERT INTO IndexEntries VALUES(?1,?2,?3,?4)" : "INSERT INTO IndexEntries VALUES(?1,?2,?3,?4,?5,?6)");
            uint64_t BatchBytes=0,BatchCount=0; Sql(Db,"BEGIN IMMEDIATE");
            for(unsigned F=0;F<8 && !Full && Derived<63ull*1024*1024;++F){
                size_t RowIndex=0;
                for(const auto& R:Rows){
                    std::string Ns=std::string(1,'\1')+R.Id.Package;
                    auto Text=StringValue(F,RowIndex++,Unique);
                    Bytes Key{uint8_t(Kind::String)}; Key.insert(Key.end(),Text.begin(),Text.end());
                    // Canonical logical upper charge: 4-byte lengths for each
                    // variable value, 8-byte field/generation IDs, plus 32 bytes
                    // row bookkeeping. Includes full authority even compact mode.
                    uint64_t Charge=4+Ns.size()+4+R.Id.Store.size()+8+8+4+Key.size()+4+R.Id.Key.size()+32;
                    if(Split)Charge+=64; // Conservative extra directory/counter/unique-ID charge.
                    if(Derived+BatchBytes+Charge>63ull*1024*1024)break;
                    uint64_t Field=uint64_t(std::stoul(R.Id.Package.substr(8)))*8+F+1;
                    if(Split){
                        if(!Unique)throw std::runtime_error("split capacity fixture requires distinct prefixes");
                        Bytes Prefix(Key.begin(),Key.begin()+512),Suffix(Key.begin()+512,Key.end());
                        Statement Directory(Db,"INSERT INTO IndexPrefixes VALUES(?1,1,?2,?3,1)");
                        Directory.Integer(1,Field);Directory.Blob(2,Prefix);Directory.Integer(3,Entries+BatchCount+1);Check(Directory.Step());
                        Insert.Integer(1,Entries+BatchCount+1);Insert.Blob(2,Suffix);Insert.Blob(3,R.Id.Key);
                    }else{
                        int I=1;if(!Compact){Insert.Blob(I++,Ns);Insert.Blob(I++,R.Id.Store);}
                        Insert.Integer(I++,Field);Insert.Integer(I++,1);Insert.Blob(I++,Key);Insert.Blob(I++,R.Id.Key);
                    }
                    int Rc=Insert.Step();
                    if(Rc==SQLITE_FULL){Full=true;sqlite3_exec(Db,"ROLLBACK",nullptr,nullptr,nullptr);break;}
                    Check(Rc); BatchBytes+=Charge; ++BatchCount;
                    if(BatchCount==32){Sql(Db,"COMMIT");Derived+=BatchBytes;Entries+=BatchCount;BatchBytes=BatchCount=0;Sql(Db,"BEGIN IMMEDIATE");}
                }
                if(Derived+BatchBytes>=63ull*1024*1024-1200)break;
            }
            if(!Full){Sql(Db,"COMMIT");Derived+=BatchBytes;Entries+=BatchCount;}
        }
        Observe(Db,Dir,Full?"SQLITE_FULL":"derived-target",Primary,Derived,Entries);
        if(Scalar(Db,"SELECT count(*) FROM IndexEntries")!=int64_t(Entries))throw std::runtime_error("rollback count");
        {
            Statement Verify(Db,"SELECT Namespace,Store,Key,Envelope FROM Records ORDER BY Namespace,Store,Key");
            size_t Checked=0; int Rc;
            while((Rc=sqlite3_step(Verify.S))==SQLITE_ROW){
                auto Text=[&](int I){return std::string(static_cast<const char*>(sqlite3_column_blob(Verify.S,I)),size_t(sqlite3_column_bytes(Verify.S,I)));};
                auto Ns=Text(0),Envelope=Text(3);Identity Id{Ns[0]==1,Ns.substr(1),Text(1),Text(2)};
                Decode(Id,Bytes(Envelope.begin(),Envelope.end()),Clock::now()+std::chrono::seconds(5));++Checked;
            }
            Check(Rc);if(Checked!=Rows.size())throw std::runtime_error("primary changed");
            std::cout<<"[CarbonLuau:DerivedCapacity] post_failure_primary_envelopes_verified="<<Checked<<std::endl;
        }
        std::cout<<"[CarbonLuau:DerivedCapacity] primary_unchanged="<<(Scalar(Db,"SELECT sum(Charge) FROM Records")==int64_t(Primary))
            <<" primary_verified_before_derived=true derived_target="<<63ull*1024*1024<<" physical_fit="<<(!Full)<<std::endl;
        sqlite3_close(Db); return Full?10:0;
    }catch(const std::exception& E){std::cerr<<"[CarbonLuau:DerivedCapacity] "<<E.what()<<std::endl;if(Db)sqlite3_close(Db);return 1;}
}
