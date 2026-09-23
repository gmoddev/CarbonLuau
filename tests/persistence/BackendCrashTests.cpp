// Reuse the historical transparent tracing harness without changing its original
// source or production SQLite VFS. Only this executable enables checkpoints.
#define main HistoricalModeProbeMain
#include "ModeProbe.cpp"
#undef main
#include "Backend.hpp"
namespace P = CarbonLuau::Persistence;
namespace {
sqlite3_mem_methods OriginalMemory{};
int AllocationAfter=-1;
bool AllocationFails()
{ if (AllocationAfter<0) return false; if (AllocationAfter--!=0) return false; FaultHit=true; return true; }
void* FaultMalloc(int Size) { return AllocationFails() ? nullptr : OriginalMemory.xMalloc(Size); }
void* FaultRealloc(void* Pointer,int Size) { return AllocationFails() ? nullptr : OriginalMemory.xRealloc(Pointer,Size); }
}
namespace CarbonLuau::Persistence {
void TestCheckpoint(const char* Stage)
{
    if (!std::strcmp(Stage,"after-begin") && StopAt.rfind("oom-",0)==0) AllocationAfter=std::stoi(StopAt.substr(4));
    if (!std::strcmp(Stage,"before-commit")) { Marker=false; InCommit=true; }
    if (!std::strcmp(Stage,"after-commit")) InCommit=false;
    Point(Stage);
}
}
namespace {
sqlite3_io_methods FaultMethods{};
int FaultWrite(sqlite3_file* File,const void* Data,int Size,sqlite3_int64 Offset)
{
    auto* Item=Cast(File);
    if (!FaultHit && Item->Kind[0]=='J' && (StopAt=="fault-full" || StopAt=="fault-short-write")) {
        FaultHit=true;
        if (StopAt=="fault-full") return SQLITE_FULL;
        const int Result=Item->Real->pMethods->xWrite(Item->Real,Data,Size/2,Offset);
        return Result==SQLITE_OK ? SQLITE_IOERR_WRITE : Result;
    }
    return Write(File,Data,Size,Offset);
}
int FaultSync(sqlite3_file* File,int Flags)
{
    if (!FaultHit && StopAt=="fault-database-sync" && Cast(File)->Kind[0]=='D') {
        FaultHit=true; return SQLITE_IOERR_FSYNC;
    }
    return Sync(File,Flags);
}
int FaultOpen(sqlite3_vfs* Vfs,const char* Name,sqlite3_file* File,int Flags,int* Actual)
{
    const int Result=Open(Vfs,Name,File,Flags,Actual);
    if (Result==SQLITE_OK) File->pMethods=&FaultMethods;
    return Result;
}
P::Deadline Until() { return Clock::now()+std::chrono::seconds(5); }
P::Identity Key() { return {false,"","Store","Key"}; }
P::Bytes Value(size_t Size)
{
    P::Value Item; Item.Type=P::Kind::String; Item.String.assign(Size,'x');
    return P::Encode(Key(),Item,Until());
}
int CrashChild(char** Args)
{
    P::Backend Store(Args[2],Until()); StopAt=Args[3];
    const bool Remove=std::string(Args[4])=="remove";
    auto Result=Store.Execute(Remove ? P::Operation::Remove : P::Operation::Set,Key(),Remove ? P::Bytes{} : Value(16384),Until());
    if (StopAt.rfind("oom-",0)==0) {
        // Lookaside and statement reuse mean the normal path has a finite,
        // operation-specific number of allocator calls. Report exhaustion
        // separately; it is never counted as an injected-failure case.
        if (!FaultHit) { Check(Result.Code==P::Error::None,"unfaulted allocation probe"); return 52; }
        // SQLite may recover an optional allocation failure; a success still
        // requires the production COMMIT path and full value/quota verification.
        return Result.Code==P::Error::None ? 51 : 50;
    }
    if (StopAt.rfind("fault-",0)==0) {
        Check(FaultHit && Result.Code!=P::Error::None,"fault must not report success");
        if (StopAt=="fault-marker-sync" || StopAt=="fault-database-sync")
            Check(Result.Code==P::Error::Indeterminate && !Store.Available(),"commit sync failure classification");
        return 50;
    }
    Check(Result.Code==P::Error::None,"child transaction");
    Point("before-completion");
    throw std::runtime_error("checkpoint not reached");
}
}
int main(int Count,char** Args)
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try {
        Check(sqlite3_config(SQLITE_CONFIG_GETMALLOC,&OriginalMemory)==SQLITE_OK,"read SQLite allocator");
        auto Memory=OriginalMemory; Memory.xMalloc=FaultMalloc; Memory.xRealloc=FaultRealloc;
        Check(sqlite3_config(SQLITE_CONFIG_MALLOC,&Memory)==SQLITE_OK,"install test allocation shim");
        Initialize(); FaultMethods=Methods; FaultMethods.xWrite=FaultWrite; FaultMethods.xSync=FaultSync; Tracer.xOpen=FaultOpen;
        Check(sqlite3_vfs_register(&Tracer,1)==SQLITE_OK,"default test trace VFS");
        if (Count==5 && std::string(Args[1])=="backend-child") return CrashChild(Args);
        if (Count==4 && std::string(Args[1])=="backend-create") {
            StopAt=Args[3]; P::Backend Store(Args[2],Until());
            throw std::runtime_error("initial schema checkpoint not reached");
        }
        Check(Count==1,"no production path accepted");
        auto Root=std::filesystem::current_path()/("backend-crash-"+std::to_string(Clock::now().time_since_epoch().count()));
        Check(std::filesystem::create_directory(Root),"fresh fixture root");
        unsigned Cases=0;
        for (const std::string Stage : {"schema-before-begin","schema-after-tables","schema-before-commit","schema-after-commit"}) {
            auto Folder=Root/Stage; Check(std::filesystem::create_directory(Folder),"initial schema fixture");
            Check(Spawn(std::filesystem::absolute(Args[0]),{"backend-create",Folder.string(),Stage})==77,"initial schema crash exercised");
            bool Ready=false;
            try { P::Backend Store(Folder,Until()); Ready=true; }
            catch (const P::Failure& Error) {
                Check(Error.Code==P::Error::StorageCorrupt || Error.Code==P::Error::FormatUnsupported,"controlled incomplete schema");
                Check(std::filesystem::exists(Folder/"store.sqlite3"),"incomplete database preserved, never silently reset");
            }
            Check(Stage!="schema-after-commit" || Ready,"committed initial schema recovered");
            std::printf("[CarbonLuau:Persistence] Initial schema %s => %s\n",Stage.c_str(),Ready ? "ready" : "preserved/unavailable");
            ++Cases;
        }
        std::vector<std::string> Stages{
            "before-transaction","after-begin","after-key","after-quota","before-commit",
            "during-commit","database-sync","marker-write","marker-sync","after-commit","before-completion","fault-marker-sync",
            "fault-full","fault-short-write","fault-database-sync"};
        for (unsigned Position=0; Position<256; ++Position) Stages.push_back("oom-"+std::to_string(Position));
        for (bool Remove : {false,true}) {
          bool Exhausted=false;
          unsigned AllocationCases=0;
          for (const std::string& Stage : Stages) {
            auto Folder=Root/(Stage+(Remove ? "-remove" : "-set"));
            Check(std::filesystem::create_directory(Folder),"fresh fixture");
            { P::Backend Store(Folder,Until()); Check(Store.Execute(P::Operation::Set,Key(),Value(100),Until()).Code==P::Error::None,"baseline"); }
            int Status=Spawn(std::filesystem::absolute(Args[0]),{"backend-child",Folder.string(),Stage,Remove ? "remove" : "set"});
            const bool Allocation=Stage.rfind("oom-",0)==0;
            if (!(Allocation ? (Status==50 || Status==51 || Status==52) : Status==(Stage.rfind("fault-",0)==0 ? 50 : 77)))
                throw std::runtime_error(Stage+(Remove ? " remove" : " set")+" checkpoint/fault status="+std::to_string(Status));
            // Startup independently validates every row/envelope and all namespace,
            // store, key and byte counters, not merely this requested value.
            P::Backend Reopened(Folder,Until()); auto Result=Reopened.Execute(P::Operation::Get,Key(),{},Until());
            Check(Result.Code==P::Error::None,"recovered startup and read");
            const bool Old=Result.Found && Result.Envelope==Value(100);
            const bool New=Remove ? !Result.Found : Result.Found && Result.Envelope==Value(16384);
            Check(Old || New,"only complete old/new state");
            if (Allocation && Status!=50) Check(New,"successful allocation recovery committed full state");
            if (Stage=="marker-sync" || Stage=="after-commit" || Stage=="before-completion") Check(New,"durable transaction survived lost completion");
            if (Stage=="before-transaction" || Stage=="after-begin" || Stage=="after-key" || Stage=="after-quota" || Stage=="before-commit" || Stage=="database-sync") Check(Old,"uncommitted transaction rolled back");
            if (Allocation && Status==52) { Exhausted=true; break; }
            if (Allocation) ++AllocationCases;
            ++Cases;
          }
          Check(Exhausted && AllocationCases>0,"all reachable normal-path allocation positions enumerated within bound");
          std::printf("[CarbonLuau:Persistence] %s allocation faults exercised=%u; next position not reached (not counted)\n",Remove ? "Remove" : "Set",AllocationCases);
        }
        auto Folder=Root/"trace"; Check(std::filesystem::create_directory(Folder),"trace directory");
        { P::Backend Store(Folder,Until());
          Check(Store.Execute(P::Operation::Set,Key(),Value(100),Until()).Code==P::Error::None,"trace baseline");
          Events.clear(); Record=true; DatabaseSyncs=JournalMarkerSyncs=0;
          Check(Store.Execute(P::Operation::Set,Key(),Value(16384),Until()).Code==P::Error::None,"trace overwrite"); Record=false;
          Check(DatabaseSyncs==1 && JournalMarkerSyncs==1,"actual backend synchronized DB and retained journal marker"); }
        std::printf("[CarbonLuau:Persistence] Backend crash/fault PASS cases=%u; trace=%s\n",Cases,Events.c_str());
        std::puts("[CarbonLuau:Persistence] Process-kill evidence only; completion checkpoint is backend boundary, not IPC delivery.");
        return 0;
    } catch (const std::exception& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Backend crash test failed: %s\n",Problem.what()); return 1; }
}
