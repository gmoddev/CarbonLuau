// Offline architecture research. Source database is always opened READONLY.
// No runtime schema migration or production compatibility claim.
#include "sqlite3.h"
#include "Derived.hpp"
#include <algorithm>
#include <cstring>
#include <filesystem>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#endif
namespace {
namespace P=CarbonLuau::Persistence;
// Research matrix controls, never runtime/author configuration.
size_t ChunkSize=768;
size_t ShrinkSize=1536;
int64_t RecordOffset=0;
void Check(int Rc) { if(Rc!=SQLITE_OK && Rc!=SQLITE_DONE)throw std::runtime_error("SQLite code="+std::to_string(Rc)); }
struct Connection {
    sqlite3* Db=nullptr;
    Connection(const std::string& Path,bool Write=false){Check(sqlite3_open_v2(Path.c_str(),&Db,Write?SQLITE_OPEN_READWRITE|SQLITE_OPEN_CREATE:SQLITE_OPEN_READONLY,nullptr));}
    ~Connection(){sqlite3_close(Db);}
    void Sql(const std::string& Text){char* Error=nullptr;int Rc=sqlite3_exec(Db,Text.c_str(),nullptr,nullptr,&Error);if(Rc!=SQLITE_OK){std::string Message=Error?Error:"";sqlite3_free(Error);throw std::runtime_error("SQLite code="+std::to_string(Rc)+" "+Message);}}
};
struct Statement {
    sqlite3_stmt* St=nullptr;
    Statement(Connection& Db,const std::string& Text){Check(sqlite3_prepare_v2(Db.Db,Text.c_str(),-1,&St,nullptr));}
    ~Statement(){sqlite3_finalize(St);}
    bool Next(){int Rc=sqlite3_step(St);if(Rc==SQLITE_ROW)return true;Check(Rc);return false;}
    void Done(){Check(sqlite3_step(St));Check(sqlite3_reset(St));Check(sqlite3_clear_bindings(St));}
    void Number(int At,int64_t Value){Check(sqlite3_bind_int64(St,At,Value));}
    void Blob(int At,const std::string& Value){Check(sqlite3_bind_blob(St,At,Value.data(),int(Value.size()),SQLITE_TRANSIENT));}
    std::string Text(int At){auto P=sqlite3_column_text(St,At);return P?reinterpret_cast<const char*>(P):"";}
    std::string Bytes(int At){auto P=sqlite3_column_blob(St,At);int N=sqlite3_column_bytes(St,At);return N?std::string(static_cast<const char*>(P),size_t(N)):std::string();}
    int64_t Number(int At){return sqlite3_column_int64(St,At);}
};
int64_t Scalar(Connection& Db,const std::string& Sql){Statement S(Db,Sql);if(!S.Next())throw std::runtime_error("missing scalar");return S.Number(0);}
void Stats(Connection& Db,const std::string& Stage){
    std::cout<<"[CarbonLuau:StorageFit] stage="<<Stage<<" pages="<<Scalar(Db,"PRAGMA page_count")<<" freelist="<<Scalar(Db,"PRAGMA freelist_count")<<" page_size="<<Scalar(Db,"PRAGMA page_size")<<std::endl;
    Statement S(Db,"SELECT name,pagetype,count(*),sum(ncell),sum(payload),sum(unused),sum(pgsize),max(mx_payload) FROM dbstat GROUP BY name,pagetype ORDER BY name,pagetype");
    while(S.Next()){std::cout<<"tree="<<S.Text(0)<<" kind="<<S.Text(1)<<" pages="<<S.Number(2)<<" cells="<<S.Number(3)<<" payload="<<S.Number(4)<<" unused="<<S.Number(5)<<" physical="<<S.Number(6)<<" max_cell="<<S.Number(7)<<'\n';}
    std::cout.flush();
}
void Integrity(Connection& Db){Statement S(Db,"PRAGMA integrity_check");if(!S.Next()||S.Text(0)!="ok"||S.Next())throw std::runtime_error("integrity check failed");}
void VerifyDerived(Connection& Db,P::Derived& Engine,P::Deadline& End){
    // The offline fit probe expects an unchanged, coherent derived graph.
    // Exercise the production bounded verifier but reject any withdrawal that
    // the former read-only forensic validation would have reported as invalid.
    Engine.InvalidateVerification();
    // Offline 100,000-record corpora can contain hundreds of thousands of
    // distinct prefixes, each requiring its own bounded proof step.
    End=P::Clock::now()+std::chrono::minutes(30);
    for(unsigned Step=0;Step<2000000;++Step){
        Db.Sql("BEGIN IMMEDIATE");
        try{
            const auto Changes=sqlite3_total_changes64(Db.Db);
            const auto Result=Engine.VerifyStep();
            if(Result==P::Derived::VerificationResult::DerivedInvalid||
                sqlite3_total_changes64(Db.Db)!=Changes)
                throw std::runtime_error("research derived representation mismatch");
            Db.Sql("COMMIT");
            if(Result==P::Derived::VerificationResult::Complete){
                std::cout<<"[CarbonLuau:StorageFit] derived_verification_steps="<<Step+1<<std::endl;
                return;
            }
        }catch(...){sqlite3_exec(Db.Db,"ROLLBACK",nullptr,nullptr,nullptr);throw;}
    }
    throw std::runtime_error("research derived verification step bound");
}
std::string Padded(const std::string& Ns,const std::string& Store,const std::string& Key,const std::string& Original,int64_t Charge){
    P::Identity Id{Ns[0]==1,Ns.substr(1),Store,Key};auto End=P::Clock::now()+std::chrono::seconds(5);
    auto Root=P::Decode(Id,P::Bytes(Original.begin(),Original.end()),End);
    const int64_t Extra=Charge-int64_t(Store.size()+Key.size()+Original.size())-24;
    if(Extra<0||Extra>16384||Root->Type!=P::Kind::Map)throw std::runtime_error("padding shape");
    auto Padding=std::make_shared<P::Value>();Padding->Type=P::Kind::String;Padding->String=std::string(size_t(Extra),'z');
    Root->Map.emplace_back("ResearchPadding",Padding);
    auto Encoded=P::Encode(Id,*Root,End);
    if(Encoded.size()+Store.size()+Key.size()!=size_t(Charge))throw std::runtime_error("padding accounting");
    P::Decode(Id,Encoded,End);return std::string(Encoded.begin(),Encoded.end());
}
void ReadEnvelope(sqlite3_context* Context,int Count,sqlite3_value** Values){
    // Prototype-only compatibility view for the unchanged derived engine.
    // Not shipped SQL/API, not a candidate production storage adapter.
    sqlite3_stmt* Read=nullptr;
    try{
        if(Count!=1)throw std::runtime_error("arity");
        Check(sqlite3_prepare_v2(sqlite3_context_db_handle(Context),"SELECT Payload FROM Chunks WHERE RecordId=?1 ORDER BY Part",-1,&Read,nullptr));
        Check(sqlite3_bind_int64(Read,1,sqlite3_value_int64(Values[0])));
        std::string Bytes;int Rc;
        while((Rc=sqlite3_step(Read))==SQLITE_ROW){
            int Size=sqlite3_column_bytes(Read,0);if(Size<0||Bytes.size()+size_t(Size)>65536)throw std::runtime_error("envelope bound");
            Bytes.append(static_cast<const char*>(sqlite3_column_blob(Read,0)),size_t(Size));
        }
        Check(Rc);sqlite3_finalize(Read);Read=nullptr;
        sqlite3_result_blob(Context,Bytes.data(),int(Bytes.size()),SQLITE_TRANSIENT);
    }catch(...){sqlite3_finalize(Read);sqlite3_result_error(Context,"bounded research envelope lookup failed",-1);}
}
void Churn(Connection& Db){
    // Offline, bounded-per-transaction shrink/refill. Not a runtime adapter.
    // Retain F0..F4; future preparation still indexes unchanged F0..F2.
    Statement Rows(Db,"SELECT R.Id,S.Namespace,S.Store,R.Key,ResearchEnvelope(R.Id),R.Charge FROM RecordIdentity R JOIN Stores S ON S.Id=R.StoreId ORDER BY R.Id");
    Statement Part(Db,"INSERT OR REPLACE INTO Chunks VALUES(?1,?2,?3)"),Remove(Db,"DELETE FROM Chunks WHERE RecordId=?1 AND Part>=?2"),Charge(Db,"UPDATE RecordIdentity SET Charge=?2 WHERE Id=?1"),Quota(Db,"UPDATE Quotas SET Bytes=Bytes+?2 WHERE Namespace=?1"),Total(Db,"UPDATE Totals SET Bytes=Bytes+?1");
    unsigned Count=0;Db.Sql("BEGIN IMMEDIATE");
    while(Rows.Next()){
        auto Ns=Rows.Bytes(1),Store=Rows.Bytes(2),Key=Rows.Bytes(3),Old=Rows.Bytes(4);
        P::Identity Id{Ns[0]==1,Ns.substr(1),Store,Key};auto End=P::Clock::now()+std::chrono::seconds(5);
        auto Root=P::Decode(Id,P::Bytes(Old.begin(),Old.end()),End);
        Root->Map.erase(std::remove_if(Root->Map.begin(),Root->Map.end(),[](const auto& Item){return Item.first=="ResearchPadding"||Item.first=="F5"||Item.first=="F6"||Item.first=="F7"||(ShrinkSize==1024&&(Item.first=="F3"||Item.first=="F4"));}),Root->Map.end());
        auto Small=P::Encode(Id,*Root,End);int64_t NewCharge=ShrinkSize+Store.size()+Key.size();
        auto Value=Padded(Ns,Store,Key,std::string(Small.begin(),Small.end()),NewCharge);
        for(size_t At=0;At<Value.size();At+=ChunkSize){Part.Number(1,Rows.Number(0));Part.Number(2,At/ChunkSize);Part.Blob(3,Value.substr(At,ChunkSize));Part.Done();}
        Remove.Number(1,Rows.Number(0));Remove.Number(2,(Value.size()+ChunkSize-1)/ChunkSize);Remove.Done();Charge.Number(1,Rows.Number(0));Charge.Number(2,NewCharge);Charge.Done();
        auto Delta=NewCharge-Rows.Number(5);Quota.Blob(1,Ns);Quota.Number(2,Delta);Quota.Done();Total.Number(1,Delta);Total.Done();
        if(++Count%32==0){Db.Sql("COMMIT");Db.Sql("BEGIN IMMEDIATE");}
    }Db.Sql("COMMIT");Stats(Db,"churn-shrunk");
    struct StoreRow{int64_t Id;std::string Ns,Name;};std::vector<StoreRow> Stores;
    {Statement S(Db,"SELECT Id,Namespace,Store FROM Stores ORDER BY Id");while(S.Next())Stores.push_back({S.Number(0),S.Bytes(1),S.Bytes(2)});}
    const auto Remaining=268435456-Scalar(Db,"SELECT Bytes FROM Totals");
    Statement Record(Db,"INSERT INTO RecordIdentity VALUES(?1,?2,?3,?4)"),Chunk(Db,"INSERT INTO Chunks VALUES(?1,?2,?3)"),AddQuota(Db,"UPDATE Quotas SET Bytes=Bytes+?2,Keys=Keys+1 WHERE Namespace=?1"),AddTotal(Db,"UPDATE Totals SET Bytes=Bytes+?1,Keys=Keys+1");
    try{
        Db.Sql("BEGIN IMMEDIATE");
        for(unsigned I=0;I<12500;++I){
            const auto& S=Stores[I%Stores.size()];auto Key="refill-"+std::to_string(I);P::Identity Id{S.Ns[0]==1,S.Ns.substr(1),S.Name,Key};auto End=P::Clock::now()+std::chrono::seconds(5);
            P::Value Root;
            for(unsigned F=0;F<3;++F){auto V=std::make_shared<P::Value>();V->Type=P::Kind::String;V->String=std::to_string(I)+":"+std::to_string(F);V->String.resize(256,'x');Root.Map.emplace_back("F"+std::to_string(F),V);}
            auto Encoded=P::Encode(Id,Root,End);auto NewCharge=Remaining/12500+(I<Remaining%12500?1:0);
            auto Value=Padded(S.Ns,S.Name,Key,std::string(Encoded.begin(),Encoded.end()),NewCharge);
            const auto PhysicalId=RecordOffset+90000+I;
            Record.Number(1,PhysicalId);Record.Number(2,S.Id);Record.Blob(3,Key);Record.Number(4,NewCharge);Record.Done();
            for(size_t At=0;At<Value.size();At+=ChunkSize){Chunk.Number(1,PhysicalId);Chunk.Number(2,At/ChunkSize);Chunk.Blob(3,Value.substr(At,ChunkSize));Chunk.Done();}
            AddQuota.Blob(1,S.Ns);AddQuota.Number(2,NewCharge);AddQuota.Done();AddTotal.Number(1,NewCharge);AddTotal.Done();
            if((I+1)%32==0){Db.Sql("COMMIT");Db.Sql("BEGIN IMMEDIATE");}
        }Db.Sql("COMMIT");
    }catch(...){sqlite3_exec(Db.Db,"ROLLBACK",nullptr,nullptr,nullptr);std::cout<<"[CarbonLuau:StorageFit] refill_failed primary_logical="<<Scalar(Db,"SELECT Bytes FROM Totals")<<" database="<<Scalar(Db,"PRAGMA page_count")*4096<<std::endl;Integrity(Db);throw;}
    if(Scalar(Db,"SELECT Bytes FROM Totals")!=268435456||Scalar(Db,"SELECT max(Bytes) FROM Quotas")>16777216||Scalar(Db,"SELECT max(Keys) FROM Quotas")>10000)throw std::runtime_error("churn quotas");
    Statement Verify(Db,"SELECT Namespace,Store,Key,Envelope,Charge FROM Records ORDER BY Namespace,Store,Key");Count=0;
    while(Verify.Next()){
        auto Ns=Verify.Bytes(0),Value=Verify.Bytes(3);P::Identity Id{Ns[0]==1,Ns.substr(1),Verify.Bytes(1),Verify.Bytes(2)};
        P::Decode(Id,P::Bytes(Value.begin(),Value.end()),P::Clock::now()+std::chrono::seconds(5));
        if(Value.size()+Id.Store.size()+Id.Key.size()!=size_t(Verify.Number(4)))throw std::runtime_error("churn envelope charge");++Count;
    }
    if(Count!=100000)throw std::runtime_error("churn record count");Integrity(Db);Stats(Db,"churn-refilled");
    std::cout<<"[CarbonLuau:StorageFit] churn_verified_records="<<Count<<" primary_logical=268435456"<<std::endl;
}
void ContinueBuild(Connection& Db,bool PrepareEmpty=false,bool RunChurn=false){
    Db.Sql("ALTER TABLE Records RENAME TO RecordIdentity;CREATE VIEW Records AS SELECT S.Namespace,S.Store,R.Key,ResearchEnvelope(R.Id) AS Envelope,R.Charge FROM RecordIdentity R JOIN Stores S ON S.Id=R.StoreId");
    Check(sqlite3_create_function(Db.Db,"ResearchEnvelope",1,SQLITE_UTF8,nullptr,ReadEnvelope,nullptr,nullptr));
    if(RunChurn)Churn(Db);
    auto End=P::Clock::now()+std::chrono::seconds(60);P::Derived Engine(Db.Db,End);
    VerifyDerived(Db,Engine,End);
    if(PrepareEmpty){Statement Stores(Db,"SELECT Namespace,Store FROM Stores ORDER BY Id");
        while(Stores.Next())for(unsigned Field=0;Field<3;++Field){
            auto Ns=Stores.Bytes(0);P::Identity Id{Ns[0]==1,Ns.substr(1),Stores.Bytes(1),"Key"};
            End=P::Clock::now()+std::chrono::seconds(5);Db.Sql("BEGIN IMMEDIATE");
            if(!Engine.Prepare(Id,"F"+std::to_string(Field)))throw std::runtime_error("research preparation rejected");Db.Sql("COMMIT");
        }
    }
    int64_t Peak=Scalar(Db,"SELECT Bytes FROM DerivedTotals"),PeakPages=Scalar(Db,"PRAGMA page_count");unsigned Batches=0;
    for(;;){
        End=P::Clock::now()+std::chrono::seconds(5);Db.Sql("BEGIN IMMEDIATE");
        if(!Engine.HasWork()){Db.Sql("COMMIT");break;}
        if(++Batches>30000)throw std::runtime_error("research maintenance bound");
        try{Engine.Maintain();Db.Sql("COMMIT");}catch(...){
            sqlite3_exec(Db.Db,"ROLLBACK",nullptr,nullptr,nullptr);
            std::cout<<"[CarbonLuau:StorageFit] maintenance_failed batches="<<Batches<<" primary_logical="<<Scalar(Db,"SELECT Bytes FROM Totals")<<" derived_logical="<<Scalar(Db,"SELECT Bytes FROM DerivedTotals")<<" database="<<Scalar(Db,"PRAGMA page_count")*4096<<std::endl;
            Integrity(Db);throw;
        }
        auto Bytes=Scalar(Db,"SELECT Bytes FROM DerivedTotals"),Pages=Scalar(Db,"PRAGMA page_count");
        if(Bytes>Peak){Peak=Bytes;if(Peak/1048576%8==0)std::cout<<"[CarbonLuau:StorageFit] near_peak_derived="<<Peak<<" pages="<<Pages<<std::endl;}
        PeakPages=std::max(PeakPages,Pages);
    }
    VerifyDerived(Db,Engine,End);Integrity(Db);
    std::cout<<"[CarbonLuau:StorageFit] continued_batches="<<Batches<<" peak_derived_logical="<<Peak<<" peak_database="<<PeakPages*4096<<" primary_logical="<<Scalar(Db,"SELECT Bytes FROM Totals")<<std::endl;
    Stats(Db,"completed-prototype-only");
}
void CopyTable(Connection& Source,Connection& Dest,const std::string& Name){
    // Only names read from the fixed trusted fixture schema, not author input.
    if(Name.find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_")!=std::string::npos)throw std::runtime_error("schema name");
    Statement Read(Source,"SELECT * FROM "+Name);int Count=sqlite3_column_count(Read.St);
    std::string Sql="INSERT INTO "+Name+" VALUES(";for(int I=0;I<Count;++I){if(I)Sql+=",";Sql+="?"+std::to_string(I+1);}Sql+=")";
    Statement Write(Dest,Sql);unsigned Rows=0;Dest.Sql("BEGIN IMMEDIATE");
    while(Read.Next()){for(int I=0;I<Count;++I)Check(sqlite3_bind_value(Write.St,I+1,sqlite3_column_value(Read.St,I)));Write.Done();if(++Rows%32==0){Dest.Sql("COMMIT");Dest.Sql("BEGIN IMMEDIATE");}}
    Dest.Sql("COMMIT");
}
void Prototype(Connection& Source,const std::string& Path,const std::string& Mode){
    if(std::filesystem::exists(Path))throw std::runtime_error("destination exists; refusing overwrite");
    std::filesystem::create_directories(std::filesystem::path(Path).parent_path());Connection Dest(Path,true);
    Dest.Sql("PRAGMA page_size=4096;PRAGMA synchronous=EXTRA;PRAGMA journal_mode=PERSIST;PRAGMA temp_store=MEMORY;PRAGMA cache_size=-4096;PRAGMA cache_spill=OFF;PRAGMA mmap_size=0;PRAGMA journal_size_limit=-1;PRAGMA max_page_count=131072");
    bool RunChurn=Mode=="chunks-churn"||Mode=="chunks-churn-wide";
    RecordOffset=Mode=="chunks-churn-wide"?int64_t(1)<<24:0;
    std::cout<<"[CarbonLuau:StorageFit] record_id_offset="<<RecordOffset<<std::endl;
    bool Square=Mode=="chunks-square"||RunChurn;
    bool Random=Mode=="chunks-full-random"||Square;
    bool Full=Mode=="chunks-full"||Random;
    bool Compact=Mode=="chunks-compact";
    bool Chunks=Mode=="chunks"||Compact||Full;bool Rowid=Mode=="rowid";
    auto SourceRows=Square?87500:Scalar(Source,"SELECT count(*) FROM Records");
    const int64_t Target=268435456;
    if(!Chunks&&!Rowid&&Mode!="without")throw std::runtime_error("layout");
    std::vector<std::string> Tables,Indexes;
    {Statement Schema(Source,"SELECT type,name,sql FROM sqlite_schema WHERE sql IS NOT NULL ORDER BY type DESC,name");
    while(Schema.Next()){
        std::string Type=Schema.Text(0),Name=Schema.Text(1),Sql=Schema.Text(2);
        if(Square&&Name.rfind("Derived",0)==0)continue;
        if(Type=="table"){
            if(Name=="Records"){
                if(Chunks)continue;
                if(Rowid){auto At=Sql.find(" WITHOUT ROWID");if(At==std::string::npos)throw std::runtime_error("source layout");Sql.erase(At);}
            }
            if(Compact && Name=="DerivedPrefixes")Sql="CREATE TABLE DerivedPrefixes(Id INTEGER NOT NULL,Generation INTEGER NOT NULL,Prefix BLOB NOT NULL,Refs INTEGER NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Generation,Prefix)) WITHOUT ROWID";
            if(Compact && Name=="DerivedEntries")Sql="CREATE TABLE DerivedEntries(Id INTEGER NOT NULL,PrefixId INTEGER NOT NULL,Suffix BLOB NOT NULL,RecordKey BLOB NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(PrefixId,Suffix,RecordKey)) WITHOUT ROWID";
            Dest.Sql(Sql);Tables.push_back(Name);
        }else if(Type=="index"){
            if(Compact && Name=="DerivedPrefixOrder")Sql="CREATE UNIQUE INDEX DerivedPrefixIds ON DerivedPrefixes(Id)";
            if(Compact && Name=="DerivedEntryOrder")Sql="CREATE UNIQUE INDEX DerivedEntryIds ON DerivedEntries(Id)";
            Indexes.push_back(Sql);
        }else throw std::runtime_error("unexpected source schema object");
    }}
    for(const auto& Sql:Indexes)Dest.Sql(Sql);
    if(Chunks){
        Dest.Sql("CREATE TABLE Stores(Id INTEGER PRIMARY KEY,Namespace BLOB NOT NULL,Store BLOB NOT NULL,UNIQUE(Namespace,Store));CREATE TABLE Records(Id INTEGER PRIMARY KEY,StoreId INTEGER NOT NULL,Key BLOB NOT NULL,Charge INTEGER NOT NULL,UNIQUE(StoreId,Key));CREATE TABLE Chunks(RecordId INTEGER NOT NULL,Part INTEGER NOT NULL,Payload BLOB NOT NULL,PRIMARY KEY(RecordId,Part)) WITHOUT ROWID");
        Statement Read(Source,"SELECT Namespace,Store,Key,Envelope,Charge FROM Records ORDER BY Namespace,Store,Key");
        Statement Store(Dest,"INSERT INTO Stores VALUES(?1,?2,?3)"),Record(Dest,"INSERT INTO Records VALUES(?1,?2,?3,?4)"),Chunk(Dest,"INSERT INTO Chunks VALUES(?1,?2,?3)");
        std::string PreviousNs,PreviousStore;int64_t StoreId=0,RecordId=0;Dest.Sql("BEGIN IMMEDIATE");
        while(RecordId<SourceRows && Read.Next()){
            auto Ns=Read.Bytes(0),Name=Read.Bytes(1),Envelope=Read.Bytes(3);
            int64_t Charge=Read.Number(4);
            if(Full){Charge=Target/SourceRows+(RecordId<Target%SourceRows?1:0);Envelope=Padded(Ns,Name,Read.Bytes(2),Envelope,Charge);}
            if(Ns!=PreviousNs||Name!=PreviousStore){Store.Number(1,++StoreId);Store.Blob(2,Ns);Store.Blob(3,Name);Store.Done();PreviousNs=Ns;PreviousStore=Name;}
            ++RecordId;
            // Exact permutation for the 100,000-row adversarial corpus. Stable
            // IDs are internal; namespace/key/envelope remain unchanged.
            if(Random&&SourceRows!=100000&&SourceRows!=87500)throw std::runtime_error("permutation corpus size");
            const auto PhysicalId=RecordOffset+(Random?((RecordId-1)*65537)%SourceRows+1:RecordId);
            Record.Number(1,PhysicalId);Record.Number(2,StoreId);Record.Blob(3,Read.Bytes(2));Record.Number(4,Charge);Record.Done();
            for(size_t At=0;At<Envelope.size();At+=ChunkSize){Chunk.Number(1,PhysicalId);Chunk.Number(2,At/ChunkSize);Chunk.Blob(3,Envelope.substr(At,ChunkSize));Chunk.Done();}
            if(RecordId%32==0){Dest.Sql("COMMIT");Dest.Sql("BEGIN IMMEDIATE");}
        }Dest.Sql("COMMIT");
    }
    for(const auto& Name:Tables)CopyTable(Source,Dest,Name);
    if(Full){
        Dest.Sql("BEGIN IMMEDIATE;DELETE FROM Quotas WHERE Namespace NOT IN(SELECT Namespace FROM Stores);UPDATE Quotas SET Bytes=(SELECT sum(R.Charge) FROM Records R JOIN Stores S ON S.Id=R.StoreId WHERE S.Namespace=Quotas.Namespace),Keys=(SELECT count(*) FROM Records R JOIN Stores S ON S.Id=R.StoreId WHERE S.Namespace=Quotas.Namespace),Stores=(SELECT count(*) FROM Stores S WHERE S.Namespace=Quotas.Namespace);UPDATE Totals SET Bytes=(SELECT sum(Charge) FROM Records),Keys=(SELECT count(*) FROM Records),Namespaces=(SELECT count(*) FROM Quotas);COMMIT");
        if(Scalar(Dest,"SELECT max(Bytes) FROM Quotas")>16777216||Scalar(Dest,"SELECT Bytes FROM Totals")!=Target)throw std::runtime_error("full corpus primary quotas");
    }
    if(Square){auto End=P::Clock::now()+std::chrono::seconds(5);P::Derived Engine(Dest.Db,End);Dest.Sql("BEGIN IMMEDIATE");Engine.Create();Dest.Sql("COMMIT");}
    Stats(Dest,Mode+"-fresh");Integrity(Dest);
    // Byte-for-byte envelope and exact identity/charge verification, without
    // pretending the research layout is accepted by the production backend.
    Statement Original(Source,"SELECT Namespace,Store,Key,Envelope,Charge FROM Records ORDER BY Namespace,Store,Key LIMIT "+std::to_string(SourceRows));
    Statement Rebuilt(Dest,Chunks?"SELECT S.Namespace,S.Store,R.Key,R.Charge,R.Id FROM Records R JOIN Stores S ON S.Id=R.StoreId ORDER BY S.Namespace,S.Store,R.Key":"SELECT Namespace,Store,Key,Charge,Envelope FROM Records ORDER BY Namespace,Store,Key");
    Statement Parts(Dest,Chunks?"SELECT Payload FROM Chunks WHERE RecordId=?1 ORDER BY Part":"SELECT 1 WHERE 0");
    unsigned Checked=0;
    while(Original.Next()){
        if(!Rebuilt.Next())throw std::runtime_error("missing rebuilt row");
        for(int I=0;I<3;++I)if(Original.Bytes(I)!=Rebuilt.Bytes(I))throw std::runtime_error("identity mismatch");
        std::string Envelope;
        if(Chunks){Parts.Number(1,Rebuilt.Number(4));while(Parts.Next())Envelope+=Parts.Bytes(0);Check(sqlite3_reset(Parts.St));Check(sqlite3_clear_bindings(Parts.St));}
        else Envelope=Rebuilt.Bytes(4);
        auto Expected=Original.Bytes(3);auto Charge=Original.Number(4);
        if(Full){Charge=Target/SourceRows+(Checked<Target%SourceRows?1:0);Expected=Padded(Original.Bytes(0),Original.Bytes(1),Original.Bytes(2),Expected,Charge);}
        if(Envelope!=Expected||Charge!=Rebuilt.Number(3))throw std::runtime_error("envelope or primary logical charge mismatch");++Checked;
    }
    if(Rebuilt.Next())throw std::runtime_error("extra rebuilt row");
    std::cout<<"[CarbonLuau:StorageFit] exact_primary_rows_verified="<<Checked<<" derived_charge="<<Scalar(Dest,"SELECT Bytes FROM DerivedTotals")<<" file_bytes="<<std::filesystem::file_size(Path)<<std::endl;
    if(!Chunks){auto End=P::Clock::now()+std::chrono::seconds(60);P::Derived Engine(Dest.Db,End);VerifyDerived(Dest,Engine,End);}
    if(Full)ContinueBuild(Dest,Square,RunChurn);
}
}
int main(int Count,char** Args){
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try{
        if(Count!=2&&Count!=4&&Count!=6)throw std::runtime_error("source.sqlite3 [new-destination.sqlite3 mode [chunk-size shrink-size]]");
        if(Count==6){ChunkSize=std::stoul(Args[4]);ShrinkSize=std::stoul(Args[5]);
            if((ChunkSize!=512&&ChunkSize!=640&&ChunkSize!=768&&ChunkSize!=896&&ChunkSize!=960)||(ShrinkSize!=1024&&ShrinkSize!=1536&&ShrinkSize!=1537&&ShrinkSize!=1792&&ShrinkSize!=2048))throw std::runtime_error("research matrix bounds");}
        std::cout<<"[CarbonLuau:StorageFit] chunk_size="<<ChunkSize<<" shrink_size="<<ShrinkSize<<std::endl;
        Connection Source(Args[1]);std::cout<<"[CarbonLuau:StorageFit] sqlite="<<sqlite3_libversion()<<" source_id="<<sqlite3_sourceid()<<" diagnostic_dbstat=true"<<std::endl;
        if(Count==2){Stats(Source,"original-readonly");Integrity(Source);
            Statement S(Source,"SELECT count(*),sum(length(Namespace)),sum(length(Store)),sum(length(Key)),sum(length(Envelope)),sum(Charge),min(length(Envelope)),max(length(Envelope)) FROM Records");S.Next();
            std::cout<<"records="<<S.Number(0)<<" namespace_bytes="<<S.Number(1)<<" store_bytes="<<S.Number(2)<<" key_bytes="<<S.Number(3)<<" envelope_bytes="<<S.Number(4)<<" logical="<<S.Number(5)<<" min_envelope="<<S.Number(6)<<" max_envelope="<<S.Number(7)<<std::endl;
        }else Prototype(Source,Args[2],Args[3]);return 0;
    }catch(const P::Failure& Error){std::cerr<<"[CarbonLuau:StorageFit] private engine failure="<<unsigned(Error.Code)<<std::endl;return 1;}
    catch(const std::exception& Error){std::cerr<<"[CarbonLuau:StorageFit] FAILED "<<Error.what()<<std::endl;return 1;}
}
