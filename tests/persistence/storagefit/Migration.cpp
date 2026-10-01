// Research only: source fixture copied once into an isolated test destination.
// Copying the fixture is test setup, NOT part of the proposed migration.
#define main StorageFitResearchMain
#include "Probe.cpp"
#undef main
#include <cstdlib>

namespace {
struct Row { std::string Ns,Store,Key,Envelope; int64_t Charge; };
void Cut(const std::string& Point) {
    const char* Requested=std::getenv("CARBONLUAU_RESEARCH_MIGRATION_CUT");
    if(Requested&&Point==Requested)std::_Exit(93);
}
void Profile(Connection& Db) {
    Db.Sql("PRAGMA synchronous=EXTRA;PRAGMA journal_mode=PERSIST;PRAGMA temp_store=MEMORY;PRAGMA cache_size=-4096;PRAGMA cache_spill=OFF;PRAGMA mmap_size=0;PRAGMA journal_size_limit=-1;PRAGMA max_page_count=131072");
    if(Scalar(Db,"PRAGMA page_size")!=4096)throw std::runtime_error("page size");
}
void VerifyMigration(Connection& Source,Connection& Db) {
    Statement Original(Source,"SELECT Namespace,Store,Key,Envelope,Charge FROM Records ORDER BY Namespace,Store,Key");
    Statement Read(Db,"SELECT R.Id,R.Charge FROM CompactRecords R JOIN CompactStores S ON S.Id=R.StoreId WHERE S.Namespace=?1 AND S.Store=?2 AND R.Key=?3");
    Statement Part(Db,"SELECT Part,Payload FROM CompactChunks WHERE RecordId=?1 ORDER BY Part");
    size_t Count=0;
    while(Original.Next()){
        for(int I=0;I<3;++I)Read.Blob(I+1,Original.Bytes(I));
        if(!Read.Next()||Read.Number(1)!=Original.Number(4))throw std::runtime_error("identity/charge");
        Part.Number(1,Read.Number(0));std::string Bytes;int64_t Sequence=0;
        while(Part.Next()){if(Part.Number(0)!=Sequence++)throw std::runtime_error("chunk sequence");Bytes+=Part.Bytes(1);if(Bytes.size()>65536)throw std::runtime_error("envelope bound");}
        if(Bytes!=Original.Bytes(3))throw std::runtime_error("byte mismatch");
        auto Ns=Original.Bytes(0);P::Identity Id{Ns[0]==1,Ns.substr(1),Original.Bytes(1),Original.Bytes(2)};
        P::Decode(Id,P::Bytes(Bytes.begin(),Bytes.end()),P::Clock::now()+std::chrono::seconds(5));
        if(Read.Next())throw std::runtime_error("duplicate");
        Check(sqlite3_reset(Read.St));Check(sqlite3_clear_bindings(Read.St));Check(sqlite3_reset(Part.St));Check(sqlite3_clear_bindings(Part.St));++Count;
    }
    if(Scalar(Db,"SELECT count(*) FROM CompactRecords")!=int64_t(Count)||Scalar(Db,"SELECT count(*) FROM Records")!=0)throw std::runtime_error("row count");
    for(const auto& Table:{"Quotas","Totals"}){
        Statement A(Source,std::string("SELECT * FROM ")+Table+" ORDER BY 1"),B(Db,std::string("SELECT * FROM ")+Table+" ORDER BY 1");
        while(A.Next()){if(!B.Next())throw std::runtime_error("quota row");for(int I=0;I<sqlite3_column_count(A.St);++I){if(sqlite3_column_type(A.St,I)!=sqlite3_column_type(B.St,I)||A.Bytes(I)!=B.Bytes(I))throw std::runtime_error("quota changed");}}
        if(B.Next())throw std::runtime_error("extra quota");
    }
    Integrity(Db);std::cout<<"[CarbonLuau:Migration] exact_verified="<<Count<<" quotas_unchanged=true pages="<<Scalar(Db,"PRAGMA page_count")<<" freelist="<<Scalar(Db,"PRAGMA freelist_count")<<std::endl;
}
void Migrate(Connection& Db) {
    bool Started=Scalar(Db,"SELECT count(*) FROM sqlite_schema WHERE name='CompactState'")!=0;
    size_t Batches=0,PeakMemory=0;
    for(;;){
        std::vector<Row> Rows;size_t Bytes=0;
        { Statement Read(Db,"SELECT Namespace,Store,Key,Envelope,Charge FROM Records ORDER BY Namespace,Store,Key LIMIT 32");
          while(Read.Next()){
              auto Envelope=Read.Bytes(3);if(Bytes+Envelope.size()>262144)break;
              auto Ns=Read.Bytes(0);P::Identity Id{Ns[0]==1,Ns.substr(1),Read.Bytes(1),Read.Bytes(2)};
              P::Decode(Id,P::Bytes(Envelope.begin(),Envelope.end()),P::Clock::now()+std::chrono::seconds(5));
              Bytes+=Envelope.size();Rows.push_back({Ns,Id.Store,Id.Key,Envelope,Read.Number(4)});
          }
        }
        if(Rows.empty())break;
        Db.Sql("BEGIN IMMEDIATE");
        try {
            // Delete first to release overflow/B-tree pages before allocating
            // destinations, all inside the SAME rollback-journal transaction.
            {Statement Delete(Db,"DELETE FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3");
             for(const auto& R:Rows){Delete.Blob(1,R.Ns);Delete.Blob(2,R.Store);Delete.Blob(3,R.Key);Delete.Done();}}
            Cut("after-delete");
            if(!Started){
                Db.Sql("CREATE TABLE CompactState(Id INTEGER PRIMARY KEY,Complete INTEGER NOT NULL);INSERT INTO CompactState VALUES(1,0);CREATE TABLE CompactStores(Id INTEGER PRIMARY KEY,Namespace BLOB NOT NULL,Store BLOB NOT NULL,UNIQUE(Namespace,Store));CREATE TABLE CompactRecords(Id INTEGER PRIMARY KEY,StoreId INTEGER NOT NULL,Key BLOB NOT NULL,Charge INTEGER NOT NULL,UNIQUE(StoreId,Key));CREATE TABLE CompactChunks(RecordId INTEGER NOT NULL,Part INTEGER NOT NULL,Payload BLOB NOT NULL,PRIMARY KEY(RecordId,Part)) WITHOUT ROWID;PRAGMA user_version=3");
                Started=true;
            }
            Cut("after-schema");
            { Statement Store(Db,"INSERT INTO CompactStores(Namespace,Store) VALUES(?1,?2) ON CONFLICT DO NOTHING"),Find(Db,"SELECT Id FROM CompactStores WHERE Namespace=?1 AND Store=?2"),Record(Db,"INSERT INTO CompactRecords(StoreId,Key,Charge) VALUES(?1,?2,?3)"),Part(Db,"INSERT INTO CompactChunks VALUES(?1,?2,?3)");
              for(const auto& R:Rows){
                  Store.Blob(1,R.Ns);Store.Blob(2,R.Store);Store.Done();Find.Blob(1,R.Ns);Find.Blob(2,R.Store);if(!Find.Next())throw std::runtime_error("store");auto StoreId=Find.Number(0);Check(sqlite3_reset(Find.St));Check(sqlite3_clear_bindings(Find.St));
                  Record.Number(1,StoreId);Record.Blob(2,R.Key);Record.Number(3,R.Charge);Record.Done();auto Id=sqlite3_last_insert_rowid(Db.Db);
                  for(size_t At=0;At<R.Envelope.size();At+=ChunkSize){Part.Number(1,Id);Part.Number(2,At/ChunkSize);Part.Blob(3,R.Envelope.substr(At,ChunkSize));Part.Done();}
              }
            }
            Cut("before-commit");Db.Sql("COMMIT");Cut("after-commit");
        }catch(...){sqlite3_exec(Db.Db,"ROLLBACK",nullptr,nullptr,nullptr);throw;}
        PeakMemory=std::max(PeakMemory,size_t(sqlite3_memory_highwater(0)));++Batches;
    }
    if(!Started)throw std::runtime_error("research fixture must be nonempty");
    Db.Sql("BEGIN IMMEDIATE;UPDATE CompactState SET Complete=1;COMMIT");
    // Empty legacy table retained in research for verification; production
    // retirement/schema checks need independent bounded qualification.
    std::cout<<"[CarbonLuau:Migration] batches="<<Batches<<" sqlite_memory_highwater="<<PeakMemory<<std::endl;
}
}
int main(int Count,char** Args){
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try{
        if(Count!=4)throw std::runtime_error("source new-or-resume-destination copy|resume|capacity");
        const std::string Mode=Args[3];
        if(Mode=="copy"||Mode=="capacity"){
            if(std::filesystem::exists(Args[2]))throw std::runtime_error("destination exists");
            std::filesystem::create_directories(std::filesystem::path(Args[2]).parent_path());
            std::filesystem::copy_file(Args[1],Args[2]);
        }else if(Mode!="resume")throw std::runtime_error("mode");
        Connection Source(Args[1]),Db(Args[2],true);Profile(Db);
        if(Mode=="capacity"){
            // Diagnostic allocation clamp, NOT a canonical 512-MiB fixture.
            Db.Sql("PRAGMA max_page_count="+std::to_string(Scalar(Db,"PRAGMA page_count")));
        }
        std::cout<<"[CarbonLuau:Migration] source_version="<<Scalar(Source,"PRAGMA user_version")<<" sqlite="<<sqlite3_sourceid()<<std::endl;
        Migrate(Db);VerifyMigration(Source,Db);return 0;
    }catch(const P::Failure& E){std::cerr<<"[CarbonLuau:Migration] codec_error="<<unsigned(E.Code)<<std::endl;return 1;}
    catch(const std::exception& E){std::cerr<<"[CarbonLuau:Migration] FAILED "<<E.what()<<std::endl;return 1;}
}
