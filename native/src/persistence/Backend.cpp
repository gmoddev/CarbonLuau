#include "Backend.hpp"
#include "Derived.hpp"
#include "Query.hpp"
#include "sqlite3.h"
#include <algorithm>
#include <array>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <map>
#include <set>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <cerrno>
#include <sys/random.h>
#include <sys/stat.h>
#endif

namespace CarbonLuau::Persistence {
#ifdef CARBONLUAU_PERSISTENCE_BUDGET_TESTING
uint32_t TestInstructionThreshold(uint32_t Requested);
void TestStatementInstructions(sqlite3_stmt* Statement) noexcept;
#endif
#ifdef CARBONLUAU_PERSISTENCE_TESTING
void TestCheckpoint(const char* Stage);
#define STORAGE_POINT(Stage) TestCheckpoint(Stage)
#else
#define STORAGE_POINT(Stage) ((void)0)
#endif
namespace {
void FillSecret(std::array<uint8_t,32>& Secret) {
#ifdef _WIN32
    HMODULE Library=LoadLibraryW(L"advapi32.dll");
    Require(Library!=nullptr,Error::StorageUnavailable);
    using RandomFunction=BOOLEAN (WINAPI*)(void*,ULONG);
    auto Random=reinterpret_cast<RandomFunction>(GetProcAddress(Library,"SystemFunction036"));
    bool Good=Random && Random(Secret.data(),ULONG(Secret.size()))!=0;
    FreeLibrary(Library);Require(Good,Error::StorageUnavailable);
#else
    size_t Position=0;
    while(Position<Secret.size()){
        ssize_t Count=getrandom(Secret.data()+Position,Secret.size()-Position,0);
        if(Count<0 && errno==EINTR)continue;
        Require(Count>0,Error::StorageUnavailable);Position+=size_t(Count);
    }
#endif
}
// Persistence-2A private capacity candidate. Page/file extents are hard;
// filesystem allocated blocks remain an operational, measured budget.
constexpr uint32_t DatabasePages = 262144;
constexpr uint64_t DatabaseBytes = uint64_t(DatabasePages) * 4096;
constexpr uint64_t JournalBytes = uint64_t(DatabasePages) * (4096 + 8) + 65536;
constexpr uint64_t OperationalBytes = 2560ull * 1024 * 1024;
constexpr uint64_t NamespaceBytes = 16ull * 1024 * 1024;
constexpr uint64_t GlobalBytes = 256ull * 1024 * 1024;
constexpr const char* Schema[] = {
    "CREATE TABLE Records(Namespace BLOB NOT NULL,Store BLOB NOT NULL,Key BLOB NOT NULL,Envelope BLOB NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Namespace,Store,Key)) WITHOUT ROWID",
    "CREATE TABLE Quotas(Namespace BLOB PRIMARY KEY,Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Stores INTEGER NOT NULL) WITHOUT ROWID",
    "CREATE TABLE Totals(Id INTEGER PRIMARY KEY CHECK(Id=1),Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Namespaces INTEGER NOT NULL)"
};
Error SqlError(int Rc)
{
    switch (Rc & 255) {
    case SQLITE_FULL: return Error::StorageFull;
    case SQLITE_BUSY: case SQLITE_LOCKED: return Error::StorageBusy;
    case SQLITE_CORRUPT: case SQLITE_NOTADB: case SQLITE_SCHEMA: return Error::StorageCorrupt;
    case SQLITE_INTERRUPT: return Error::DeadlineExceeded;
    default: return Error::StorageError;
    }
}
void SqlCheck(int Rc) { if (Rc != SQLITE_OK) throw Failure(SqlError(Rc)); }
void Sql(sqlite3* Db, const char* Text) { SqlCheck(sqlite3_exec(Db, Text, nullptr, nullptr, nullptr)); }
class Statement {
public:
    sqlite3_stmt* Handle = nullptr;
    Statement(sqlite3* Db, const char* Text) { SqlCheck(sqlite3_prepare_v2(Db, Text, -1, &Handle, nullptr)); }
    ~Statement() { sqlite3_finalize(Handle); }
    void Blob(int Index, const std::string& Value) { SqlCheck(sqlite3_bind_blob(Handle, Index, Value.data(), int(Value.size()), SQLITE_TRANSIENT)); }
    void Blob(int Index, const Bytes& Value) { SqlCheck(sqlite3_bind_blob(Handle, Index, Value.data(), int(Value.size()), SQLITE_TRANSIENT)); }
    void Integer(int Index, int64_t Value) { SqlCheck(sqlite3_bind_int64(Handle, Index, Value)); }
    bool Next() { int Rc = sqlite3_step(Handle); if (Rc == SQLITE_ROW) return true; if (Rc == SQLITE_DONE) return false; throw Failure(SqlError(Rc)); }
    int64_t Number(int Index) { Require(sqlite3_column_type(Handle, Index) == SQLITE_INTEGER, Error::StorageCorrupt); return sqlite3_column_int64(Handle, Index); }
    std::string Data(int Index, size_t Maximum, bool Text = false) {
        Require(sqlite3_column_type(Handle, Index) == (Text ? SQLITE_TEXT : SQLITE_BLOB), Error::StorageCorrupt);
        int Count = sqlite3_column_bytes(Handle, Index); Require(Count >= 0 && size_t(Count) <= Maximum, Error::StorageCorrupt);
        const auto* Ptr = static_cast<const char*>(sqlite3_column_blob(Handle, Index));
        Require(Ptr || !Count, Error::StorageError); return Count ? std::string(Ptr, size_t(Count)) : std::string();
    }
};
int64_t Scalar(sqlite3* Db, const char* Text)
{ Statement Query(Db, Text); Require(Query.Next(), Error::StorageCorrupt); auto Value = Query.Number(0); Require(!Query.Next(), Error::StorageCorrupt); return Value; }
std::string ScalarText(sqlite3* Db, const char* Text)
{ Statement Query(Db, Text); Require(Query.Next(), Error::StorageCorrupt); auto Value = Query.Data(0, 1024, true); Require(!Query.Next(), Error::StorageCorrupt); return Value; }
std::string Namespace(const Identity& Id) { return std::string(1, Id.Addon ? '\1' : '\0') + Id.Package; }
Identity RecordIdentity(const std::string& Ns, std::string Store, std::string Key)
{
    Require(!Ns.empty() && uint8_t(Ns[0]) <= 1, Error::StorageCorrupt);
    Identity Id{Ns[0] == 1, Ns.substr(1), std::move(Store), std::move(Key)};
    try { Validate(Id); } catch (...) { throw Failure(Error::StorageCorrupt); } return Id;
}
void BindKey(Statement& Query, const Identity& Id)
{ Query.Blob(1, Namespace(Id)); Query.Blob(2, Id.Store); Query.Blob(3, Id.Key); }
struct Counts { int64_t Bytes = 0, Keys = 0, Stores = 0; };
Counts NamespaceCounts(sqlite3* Db, const std::string& Ns)
{
    Statement Query(Db, "SELECT Bytes,Keys,Stores FROM Quotas WHERE Namespace=?1"); Query.Blob(1, Ns);
    if (!Query.Next()) return {};
    Counts Value{Query.Number(0),Query.Number(1),Query.Number(2)};
    Require(Value.Bytes>0 && Value.Bytes<=int64_t(NamespaceBytes) && Value.Keys>0 && Value.Keys<=10000 && Value.Stores>0 && Value.Stores<=64,Error::StorageCorrupt);
    Require(!Query.Next(), Error::StorageCorrupt); return Value;
}
uint32_t Big32(const uint8_t* Data)
{ return uint32_t(Data[0]) << 24 | uint32_t(Data[1]) << 16 | uint32_t(Data[2]) << 8 | Data[3]; }
uint64_t Allocated(const std::filesystem::path& Path)
{
#ifdef _WIN32
    HANDLE File=CreateFileW(Path.c_str(),FILE_READ_ATTRIBUTES,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,
        nullptr,OPEN_EXISTING,FILE_FLAG_OPEN_REPARSE_POINT,nullptr);
    Require(File!=INVALID_HANDLE_VALUE,Error::StorageUnavailable);
    FILE_STANDARD_INFO Size{}; BY_HANDLE_FILE_INFORMATION Info{};
    bool Good=GetFileInformationByHandleEx(File,FileStandardInfo,&Size,sizeof(Size))!=0 &&
        GetFileInformationByHandle(File,&Info)!=0;
    CloseHandle(File);
    Require(Good && !(Info.dwFileAttributes&FILE_ATTRIBUTE_REPARSE_POINT) && Info.nNumberOfLinks==1 &&
        Size.AllocationSize.QuadPart>=0,Error::StorageUnavailable);
    return uint64_t(Size.AllocationSize.QuadPart);
#else
    struct stat Info{};
    Require(lstat(Path.c_str(),&Info)==0 && S_ISREG(Info.st_mode) && Info.st_nlink==1 && Info.st_blocks>=0,Error::StorageUnavailable);
    Require(uint64_t(Info.st_blocks)<=UINT64_MAX/512,Error::StorageFull);
    return uint64_t(Info.st_blocks)*512;
#endif
}
}

Backend::Backend(const std::filesystem::path& Path, Deadline StartupEnd) : Directory(Path), End(StartupEnd)
{
    try { FillSecret(QuerySecret); Open(); Healthy = true; }
    catch (...) { if (Database) sqlite3_close(Database); Database = nullptr; throw; }
}
Backend::~Backend() { Indexes.reset(); if (Database) sqlite3_close(Database); QuerySecret.fill(0); }
#ifdef CARBONLUAU_PERSISTENCE_TESTING
uint32_t Backend::TestCapAtCurrentPages()
{
    Require(Healthy,Error::StorageUnavailable);
    const auto Pages=Scalar(Database,"PRAGMA page_count");
    Require(Pages>0 && Pages<=DatabasePages,Error::StorageError);
    const auto Command="PRAGMA max_page_count="+std::to_string(Pages);
    Require(Scalar(Database,Command.c_str())==Pages,Error::StorageError);
    return uint32_t(Pages);
}
void Backend::TestInjectEmptyPrefixes(const Identity& Id,uint32_t Count)
{
    Require(Healthy && Count<=105,Error::InvalidArgument);
    End=Clock::now()+std::chrono::seconds(5);Progress(1000000);
    Sql(Database,"BEGIN IMMEDIATE");
    try {
        auto View=Indexes->Inspect(Id,Id.Key);
        Require(View.Admitted && View.Status==Derived::State::Active,Error::InvalidArgument);
        Statement Insert(Database,"INSERT INTO DerivedPrefixes(Id,Generation,Prefix,Refs,Charge) VALUES(?1,?2,?3,1,38)");
        for(uint32_t Index=0;Index<Count;++Index) {
            std::string Prefix(1,char(uint8_t(Kind::Number)));Prefix.push_back(char(Index+1));
            Insert.Integer(1,100000+Index);Insert.Integer(2,View.Generation);Insert.Blob(3,Prefix);
            Require(!Insert.Next(),Error::StorageError);
            sqlite3_reset(Insert.Handle);sqlite3_clear_bindings(Insert.Handle);
        }
        Sql(Database,"COMMIT");Progress();
    } catch(...) {sqlite3_progress_handler(Database,0,nullptr,nullptr);sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr);Progress();throw;}
}
void Backend::TestRemoveEmptyPrefixes()
{
    Require(Healthy,Error::StorageUnavailable);End=Clock::now()+std::chrono::seconds(5);Progress();
    Sql(Database,"BEGIN IMMEDIATE");
    try {Sql(Database,"DELETE FROM DerivedPrefixes WHERE Id>=100000 AND Id<100105");Sql(Database,"COMMIT");}
    catch(...) {sqlite3_progress_handler(Database,0,nullptr,nullptr);sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr);Progress();throw;}
}
#endif
void Backend::Progress(uint32_t Instructions)
{
    // The pinned fixed-program certificate bounds unchecked progress-callback
    // spans and the outer rollback. Reserve their cost before the hard ceiling.
    // See DerivedBudgetTests; this is not a bound for arbitrary caller SQL.
    InstructionBudget=Instructions ? Instructions-65536 : 0;
#ifdef CARBONLUAU_PERSISTENCE_BUDGET_TESTING
    if(Instructions)InstructionBudget=TestInstructionThreshold(InstructionBudget);
#endif
    // SQLite's progress interval belongs to each prepared VM; many short
    // statements can each finish below a coarse interval. Count every VDBE
    // instruction for the aggregate maintenance budget, not 1,000 per callback.
    sqlite3_progress_handler(Database,Instructions?1:1000,[](void* Context) {
        auto* Self=static_cast<Backend*>(Context);
        if(Clock::now()>=Self->End)return 1;
        if(Self->InstructionBudget){
            if(Self->InstructionBudget<=1)return 1;
            --Self->InstructionBudget;
        }
        return 0;
    },this);
}
void Backend::CheckFiles()
{
    Require(Directory.is_absolute() && std::filesystem::is_directory(Directory), Error::StorageUnavailable);
    for (auto Part = Directory; !Part.empty(); Part = Part.parent_path()) {
        Require(!std::filesystem::is_symlink(std::filesystem::symlink_status(Part)), Error::StorageUnavailable);
        if (Part == Part.parent_path()) break;
    }
    uint64_t Total = 0;
    for (const auto& File : std::filesystem::directory_iterator(Directory)) {
        Require(Clock::now() < End, Error::DeadlineExceeded);
        const auto Name = File.path().filename().string();
        Require((Name == "store.sqlite3" || Name == "store.sqlite3-journal" || Name == "owner.lock" || Name == "supervisor.lock") &&
            File.is_regular_file() && !File.is_symlink(), Error::StorageUnavailable);
        const uint64_t Length = File.file_size();
        Require(Length <= (Name == "store.sqlite3" ? DatabaseBytes : Name == "store.sqlite3-journal" ? JournalBytes : 4096), Error::StorageFull);
        uint64_t Allocation=Allocated(File.path());
        // Operational pre/postflight budget checks, not an in-flight physical
        // allocation guarantee. EOF/page limits are separate hard bounds.
        Require(Allocation <= (Name=="store.sqlite3" ? DatabaseBytes+64ull*1024*1024 :
            Name=="store.sqlite3-journal" ? JournalBytes+64ull*1024*1024 : 65536),Error::StorageFull);
        Require(Allocation<=OperationalBytes-Total,Error::StorageFull); Total+=Allocation;
    }
    Require(Total <= OperationalBytes, Error::StorageFull);
    // Recovery may raise SQLite's page limit from the first journal header.
    // Reject an out-of-contract original size before SQLite can extend the DB.
    const auto Journal = Directory / "store.sqlite3-journal";
    if (std::filesystem::exists(Journal) && std::filesystem::file_size(Journal) >= 28) {
        std::ifstream Input(Journal, std::ios::binary); std::array<uint8_t,28> Header{};
        Require(bool(Input.read(reinterpret_cast<char*>(Header.data()), Header.size())), Error::StorageError);
        const uint8_t Magic[] = {0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7};
        if (!std::memcmp(Header.data(), Magic, 8)) {
            const auto Sector = Big32(Header.data()+20);
            Require(Big32(Header.data()+16) <= DatabasePages && Big32(Header.data()+24) == 4096 &&
                Sector >= 512 && Sector <= 65536 && !(Sector & (Sector-1)), Error::StorageCorrupt);
        }
        // This single-database workload never writes a super-journal pointer.
        // Reject its sentinel before SQLite could follow a persisted path. This
        // is preflight only: SQLite still owns all actual hot-journal recovery.
        Input.seekg(-16,std::ios::end); std::array<uint8_t,16> Tail{};
        Require(bool(Input.read(reinterpret_cast<char*>(Tail.data()),Tail.size())),Error::StorageError);
        const auto Length=Big32(Tail.data()); const auto JournalSize=std::filesystem::file_size(Journal);
        auto* Vfs=sqlite3_vfs_find(nullptr); Require(Vfs!=nullptr,Error::StorageUnavailable);
        if (!std::memcmp(Tail.data()+8,Magic,8) && Length && Length<=uint32_t(Vfs->mxPathname) && Length<=JournalSize-16) {
            Require(Length<=32768,Error::StorageCorrupt);
            std::string Name(Length,'\0'); Input.seekg(-16-std::streamoff(Length),std::ios::end);
            Require(bool(Input.read(&Name[0],Length)),Error::StorageError);
            uint32_t Checksum=Big32(Tail.data()+4);
            for (char Byte : Name) Checksum-=int8_t(Byte);
            Require(Checksum!=0 || Name[0]==0,Error::StorageCorrupt);
        }
    }
}
void Backend::Open()
{
#ifdef CARBONLUAU_PERSISTENCE_TIMING_TESTING
    auto PhaseStart=Clock::now();
    auto MarkPhase=[&](const char* Name) {
        const auto Now=Clock::now();
        std::fprintf(stderr,"[CarbonLuau:Persistence] startup-phase=%s elapsed_ms=%lld\n",Name,
            static_cast<long long>(std::chrono::duration_cast<std::chrono::milliseconds>(Now-PhaseStart).count()));
        PhaseStart=Now;
    };
#endif
    CheckFiles(); const auto Path = Directory / "store.sqlite3";
    const bool New = !std::filesystem::exists(Path);
    Require(New || std::filesystem::file_size(Path) >= 100, Error::StorageCorrupt);
    Require(std::string(sqlite3_libversion()) == "3.53.4" &&
        std::string(sqlite3_sourceid())=="2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc" && !sqlite3_compileoption_used("NO_SYNC") &&
        !sqlite3_compileoption_used("DISABLE_DIRSYNC") && sqlite3_compileoption_used("OMIT_WAL"), Error::FormatUnsupported);
    SqlCheck(sqlite3_open_v2(Path.u8string().c_str(), &Database, SQLITE_OPEN_READWRITE | (New ? SQLITE_OPEN_CREATE : 0) | SQLITE_OPEN_NOFOLLOW, nullptr));
#ifdef CARBONLUAU_PERSISTENCE_BUDGET_TESTING
    sqlite3_trace_v2(Database,SQLITE_TRACE_PROFILE,[](unsigned,void*,void* Statement,void*)->int {
        TestStatementInstructions(static_cast<sqlite3_stmt*>(Statement));return 0;
    },nullptr);
#endif
    Progress();
    sqlite3_limit(Database, SQLITE_LIMIT_LENGTH, 70*1024);
    sqlite3_limit(Database, SQLITE_LIMIT_SQL_LENGTH, 4096);
    sqlite3_limit(Database, SQLITE_LIMIT_COLUMN, 16);
    sqlite3_limit(Database, SQLITE_LIMIT_ATTACHED, 0);
    sqlite3_limit(Database, SQLITE_LIMIT_VARIABLE_NUMBER, 16);
    sqlite3_limit(Database, SQLITE_LIMIT_EXPR_DEPTH, 32);
    sqlite3_limit(Database, SQLITE_LIMIT_TRIGGER_DEPTH, 0);
    int Effective = 0;
    SqlCheck(sqlite3_db_config(Database, SQLITE_DBCONFIG_DEFENSIVE, 1, &Effective)); Require(Effective == 1, Error::StorageUnavailable);
    SqlCheck(sqlite3_db_config(Database, SQLITE_DBCONFIG_TRUSTED_SCHEMA, 0, &Effective)); Require(Effective == 0, Error::StorageUnavailable);
    Sql(Database, "PRAGMA synchronous=EXTRA; PRAGMA page_size=4096; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-4096; PRAGMA cache_spill=OFF; PRAGMA mmap_size=0; PRAGMA journal_size_limit=-1;");
    Require(ScalarText(Database, "PRAGMA journal_mode=PERSIST") == "persist", Error::StorageUnavailable);
    Require(Scalar(Database,"PRAGMA synchronous") == 3 && Scalar(Database,"PRAGMA page_size") == 4096 &&
        Scalar(Database,"PRAGMA auto_vacuum") == 0 &&
        Scalar(Database,"PRAGMA cache_spill") == 0 && Scalar(Database,"PRAGMA journal_size_limit") == -1 &&
        Scalar(Database,"PRAGMA mmap_size") == 0 && Scalar(Database,"PRAGMA temp_store") == 2 &&
        Scalar(Database,"PRAGMA max_page_count=262144") == DatabasePages, Error::StorageUnavailable);
    // OMIT_WAL rejects WAL read formats inside SQLite, including page 1 restored
    // by hot-journal recovery. A newer write-only format can instead become
    // read-only: never publish a Ready backend for that case either.
    Require(sqlite3_db_readonly(Database, "main") == 0, Error::FormatUnsupported);
    CheckSchema(New);
#ifdef CARBONLUAU_PERSISTENCE_TIMING_TESTING
    MarkPhase("sqlite-schema-integrity");
#endif
    Indexes=std::make_unique<Derived>(Database,End);
    if(LegacySchema)STORAGE_POINT("derived-upgrade-before-begin");
    Sql(Database,"BEGIN IMMEDIATE");
    try{
        // Ready depends on authoritative D21 state. Retained derived data is
        // process-locally unadmitted until separate bounded verification.
        CheckRecords();
#ifdef CARBONLUAU_PERSISTENCE_TIMING_TESTING
        MarkPhase("primary-record-proof");
#endif
        if(LegacySchema){
            Indexes->Create(); STORAGE_POINT("derived-upgrade-after-tables");
            Sql(Database,"PRAGMA user_version=2");
        }
        if(LegacySchema)STORAGE_POINT("derived-upgrade-before-commit");
        Sql(Database,"COMMIT");
        if(LegacySchema)STORAGE_POINT("derived-upgrade-after-commit");
    }catch(...){sqlite3_progress_handler(Database,0,nullptr,nullptr);sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr);throw;}
    CheckFiles();
#ifdef CARBONLUAU_PERSISTENCE_TIMING_TESTING
    MarkPhase("primary-ready-final-check");
#endif
}
void Backend::CheckSchema(bool New)
{
    if (New) {
        STORAGE_POINT("schema-before-begin");
        Sql(Database, "BEGIN IMMEDIATE");
        for (const auto* Text : Schema) Sql(Database, Text);
        STORAGE_POINT("schema-after-tables");
        STORAGE_POINT("schema-before-commit");
        Sql(Database, "INSERT INTO Totals VALUES(1,0,0,0); PRAGMA application_id=1129074756; PRAGMA user_version=1; COMMIT");
        STORAGE_POINT("schema-after-commit");
    }
    const auto Version=Scalar(Database,"PRAGMA user_version");
    Require(Scalar(Database,"PRAGMA application_id") == 1129074756 && (Version==1 || Version==2), Error::FormatUnsupported);
    LegacySchema=Version==1;
    Require(ScalarText(Database,"PRAGMA integrity_check") == "ok", Error::StorageCorrupt);
    std::multiset<std::string> Expected;
    for(const auto* Text:Schema)Expected.insert(Text);
    if(!LegacySchema)for(const auto& Text:Derived::Schema())Expected.insert(Text);
    Statement Query(Database,"SELECT sql FROM sqlite_schema WHERE sql IS NOT NULL");
    while(Query.Next()){
        const auto Text=Query.Data(0,4096,true);const auto It=Expected.find(Text);
        Require(It!=Expected.end(),Error::StorageCorrupt);Expected.erase(It);
    }
    Require(Expected.empty(),Error::StorageCorrupt);
}
void Backend::CheckRecords()
{
    std::map<std::string,Counts> CountsByNamespace;
    std::string PreviousNamespace, PreviousStore; int64_t Bytes = 0, Keys = 0;
    Statement Rows(Database,"SELECT Namespace,Store,Key,Envelope,Charge FROM Records ORDER BY Namespace,Store,Key");
    while (Rows.Next()) {
        Require(++Keys <= 100000, Error::StorageCorrupt);
        const auto Ns = Rows.Data(0,66); const auto Store = Rows.Data(1,64); const auto Key = Rows.Data(2,128);
        auto Id = RecordIdentity(Ns,Store,Key); const auto Blob = Rows.Data(3,MaximumEnvelope);
        ValidateEnvelope(Id, CarbonLuau::Persistence::Bytes(Blob.begin(),Blob.end()),End);
        int64_t Charge = int64_t(Store.size()+Key.size()+Blob.size()); Require(Rows.Number(4) == Charge, Error::StorageCorrupt);
        auto& Count = CountsByNamespace[Ns]; Count.Bytes += Charge; ++Count.Keys;
        if (Ns != PreviousNamespace || Store != PreviousStore) ++Count.Stores;
        PreviousNamespace=Ns; PreviousStore=Store; Bytes += Charge;
        Require(Count.Bytes <= int64_t(NamespaceBytes) && Count.Keys <= 10000 && Count.Stores <= 64 &&
            CountsByNamespace.size() <= 256 && Bytes <= int64_t(GlobalBytes), Error::StorageCorrupt);
    }
    Statement Quotas(Database,"SELECT Namespace,Bytes,Keys,Stores FROM Quotas"); size_t Seen = 0;
    while (Quotas.Next()) {
        const auto It = CountsByNamespace.find(Quotas.Data(0,66)); Require(It != CountsByNamespace.end(),Error::StorageCorrupt);
        const auto& Count=It->second;
        Require(Quotas.Number(1)==Count.Bytes && Quotas.Number(2)==Count.Keys && Quotas.Number(3)==Count.Stores,Error::StorageCorrupt); ++Seen;
    }
    Require(Seen == CountsByNamespace.size(),Error::StorageCorrupt);
    Statement Total(Database,"SELECT Id,Bytes,Keys,Namespaces FROM Totals");
    Require(Total.Next() && Total.Number(0)==1 && Total.Number(1)==Bytes && Total.Number(2)==Keys && Total.Number(3)==int64_t(Seen) && !Total.Next(),Error::StorageCorrupt);
}
Result Backend::Execute(Operation Op, const Identity& Id, const Bytes& Envelope, Deadline RequestEnd)
{
    if (!Healthy) return {Error::StorageUnavailable}; End = RequestEnd; Progress();
    bool Transaction = false, CommitStarted = false;
    try {
        Require(Clock::now() < End, Error::DeadlineExceeded); Validate(Id);
        Require(Op == Operation::Get || Op == Operation::Set || Op == Operation::Remove);
        std::shared_ptr<Value> NewValue;
        if (Op == Operation::Set) {
            // A malformed incoming frame is not evidence that durable storage is
            // corrupt. Reject it before BEGIN without disabling healthy data.
            try { NewValue=Decode(Id,Envelope,End); }
            catch (const Failure& Problem) {
                return {Problem.Code==Error::DeadlineExceeded ? Problem.Code : Error::InvalidArgument};
            }
        } else Require(Envelope.empty());
        try { CheckFiles(); }
        catch (const Failure& Problem) {
            if (Problem.Code==Error::DeadlineExceeded) throw;
            Healthy=false;
            // An observed budget/shape violation disables this backend. Distinguish
            // it from SQLite FULL inside a transaction, which may roll back safely.
            return {Problem.Code==Error::StorageFull ? Error::StorageUnavailable : Problem.Code};
        }
        STORAGE_POINT("before-transaction");
        Sql(Database,Op == Operation::Get ? "BEGIN" : "BEGIN IMMEDIATE"); Transaction = true;
        STORAGE_POINT("after-begin");
        Bytes Old; std::shared_ptr<Value> OldValue; int64_t OldCharge = 0; bool Found;
        {
            Statement Query(Database,"SELECT Envelope,Charge FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3"); BindKey(Query,Id);
            Found = Query.Next();
            if (Found) { auto Blob=Query.Data(0,MaximumEnvelope); Old.assign(Blob.begin(),Blob.end()); OldCharge=Query.Number(1);
                Require(OldCharge == int64_t(Id.Store.size()+Id.Key.size()+Old.size()),Error::StorageCorrupt);
                if (Op==Operation::Get) ValidateEnvelope(Id,Old,End);
                else OldValue=Decode(Id,Old,End); }
        }
        if (Op == Operation::Get) { bool Present=NamespaceCounts(Database,Namespace(Id)).Keys>0; Sql(Database,"COMMIT"); Transaction=false;
            try { CheckFiles(); } catch (...) { Healthy=false; return {Error::StorageUnavailable}; }
            return {Error::None,Found,std::move(Old),Present}; }
        const auto Ns=Namespace(Id); const auto Before=NamespaceCounts(Database,Ns);
        Statement Total(Database,"SELECT Bytes,Keys,Namespaces FROM Totals WHERE Id=1"); Require(Total.Next(),Error::StorageCorrupt);
        auto Global=Counts{Total.Number(0),Total.Number(1),Total.Number(2)};
        Require(Global.Bytes>=0 && Global.Bytes<=int64_t(GlobalBytes) && Global.Keys>=0 && Global.Keys<=100000 && Global.Stores>=0 && Global.Stores<=256,Error::StorageCorrupt);
        // Release the read statement before COMMIT.
        Require(!Total.Next(),Error::StorageCorrupt);
        Statement Exists(Database,"SELECT 1 FROM Records WHERE Namespace=?1 AND Store=?2 LIMIT 1"); Exists.Blob(1,Ns); Exists.Blob(2,Id.Store);
        bool StoreExists=Exists.Next(); if (StoreExists) Require(!Exists.Next(),Error::StorageCorrupt);
        int64_t NewCharge = Op == Operation::Set ? int64_t(Id.Store.size()+Id.Key.size()+Envelope.size()) : 0;
        int64_t Delta=NewCharge-OldCharge, KeyDelta=Op==Operation::Set ? (Found ? 0 : 1) : (Found ? -1 : 0);
        Counts After{Before.Bytes+Delta,Before.Keys+KeyDelta,Before.Stores+(Op==Operation::Set && !StoreExists ? 1 : 0)};
        int64_t NamespaceDelta=Before.Keys==0 && After.Keys>0 ? 1 : Before.Keys>0 && After.Keys==0 ? -1 : 0;
        Require(After.Bytes<=int64_t(NamespaceBytes) && After.Keys<=10000 && After.Stores<=64 &&
            Global.Bytes+Delta<=int64_t(GlobalBytes) && Global.Keys+KeyDelta<=100000 && Global.Stores+NamespaceDelta<=256,Error::QuotaExceeded);
        if (Op==Operation::Set) {
            Statement Update(Database,"INSERT INTO Records VALUES(?1,?2,?3,?4,?5) ON CONFLICT(Namespace,Store,Key) DO UPDATE SET Envelope=excluded.Envelope,Charge=excluded.Charge");
            BindKey(Update,Id); Update.Blob(4,Envelope); Update.Integer(5,NewCharge); Require(!Update.Next(),Error::StorageError);
        } else {
            Statement Remove(Database,"DELETE FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3"); BindKey(Remove,Id); Require(!Remove.Next(),Error::StorageError);
            Statement Remaining(Database,"SELECT 1 FROM Records WHERE Namespace=?1 AND Store=?2 LIMIT 1"); Remaining.Blob(1,Ns); Remaining.Blob(2,Id.Store);
            if (StoreExists && !Remaining.Next()) --After.Stores;
        }
        STORAGE_POINT("after-key");
        if (After.Keys) {
            Statement Update(Database,"INSERT INTO Quotas VALUES(?1,?2,?3,?4) ON CONFLICT(Namespace) DO UPDATE SET Bytes=excluded.Bytes,Keys=excluded.Keys,Stores=excluded.Stores");
            Update.Blob(1,Ns); Update.Integer(2,After.Bytes); Update.Integer(3,After.Keys); Update.Integer(4,After.Stores); Require(!Update.Next(),Error::StorageError);
        } else { Statement Remove(Database,"DELETE FROM Quotas WHERE Namespace=?1"); Remove.Blob(1,Ns); Require(!Remove.Next(),Error::StorageError); }
        {
            Statement Update(Database,"UPDATE Totals SET Bytes=Bytes+?1,Keys=Keys+?2,Namespaces=Namespaces+?3 WHERE Id=1");
            Update.Integer(1,Delta); Update.Integer(2,KeyDelta); Update.Integer(3,NamespaceDelta); Require(!Update.Next(),Error::StorageError);
        }
        STORAGE_POINT("after-quota");
        if (Indexes->VerificationComplete()) {
            Indexes->Mutate(Id,OldValue.get(),NewValue.get());
        } else {
            // While retained derived state is unproved, no generation is
            // admitted for Query. Invalidate any in-progress proof before
            // committing a primary mutation; the later proof must compare
            // against the new authoritative snapshot.
            Indexes->InvalidateVerification();
        }
        Require(Clock::now() < End,Error::DeadlineExceeded); CommitStarted=true;
        STORAGE_POINT("before-commit");
        Sql(Database,"COMMIT"); Transaction=false;
        STORAGE_POINT("after-commit");
        Require(Clock::now() < End,Error::Indeterminate); CheckFiles();
        return {Error::None,Op==Operation::Set || Found,{},After.Keys>0};
    } catch (const Failure& Problem) {
        bool RolledBack = !Transaction;
        if (Transaction) { sqlite3_progress_handler(Database,0,nullptr,nullptr); RolledBack=sqlite3_get_autocommit(Database)!=0 || sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr)==SQLITE_OK;
            Progress(); }
        if (CommitStarted || !RolledBack) { Healthy=false; return {Op==Operation::Get ? Error::StorageError : Error::Indeterminate}; }
        if (Problem.Code==Error::StorageCorrupt || Problem.Code==Error::FormatUnsupported || Problem.Code==Error::StorageError) Healthy=false;
        return {Problem.Code};
    } catch (...) { Healthy=false; return {Op==Operation::Get ? Error::StorageError : Error::Indeterminate}; }
}
bool Backend::HasDerivedWork()
{
    if(!Healthy || DerivedPaused)return false;
    End=Clock::now()+std::chrono::seconds(5);Progress(1000000);
    try{
        Sql(Database,"BEGIN");
        const bool Work=!Indexes->VerificationFailed() &&
            (!Indexes->VerificationComplete() || Indexes->HasWork());
        Sql(Database,"COMMIT"); Progress(); return Work;
    }catch(...){
        sqlite3_progress_handler(Database,0,nullptr,nullptr);
        sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr);
        Healthy=false;return false;
    }
}
bool Backend::PrepareDerived(const Identity& Id,const std::string& Field,Deadline OperationEnd,bool Force)
{
    if(!Indexes || DerivedPaused || !Indexes->VerificationComplete()) return false;
    return DerivedOperation(&Id,&Field,OperationEnd,Force);
}
DemandResult Backend::DemandDerived(const Identity& Id,Deadline OperationEnd)
{
    if(!Healthy || !Indexes)return {Error::StorageUnavailable,0};
    // Validate the complete worker identity and the narrower literal field
    // bound before touching SQLite. A rejected frame cannot poison the backend.
    try {
        Identity StoreId=Id; StoreId.Key="K";
        Validate(StoreId); Require(ValidField(Id.Key));
    }
    catch(const Failure& Problem) { return {Problem.Code,0}; }
    if(Clock::now()>=OperationEnd)return {Error::DeadlineExceeded,0};
    if(DerivedPaused || Indexes->VerificationFailed())return {Error::None,2};
    if(!Indexes->VerificationComplete())return {Error::None,0};

    End=std::min(OperationEnd,Clock::now()+std::chrono::seconds(5));Progress(1000000);
    bool Transaction=false,CommitStarted=false;
    try {
        Require(Clock::now()<End,Error::DeadlineExceeded);
        try { CheckFiles(); }
        catch(const Failure& Problem) { if(Problem.Code!=Error::DeadlineExceeded)Healthy=false; throw; }
        Sql(Database,"BEGIN IMMEDIATE");Transaction=true;
        auto View=Indexes->Inspect(Id,Id.Key);
        uint32_t Flags=0;
        if(View.Admitted && View.Status==Derived::State::Active)Flags=1;
        else if(View.Status==Derived::State::Building)Flags=0;
        else if(!Indexes->Prepare(Id,Id.Key))Flags=2;
        // A newly allocated BUILDING generation is never reported ready. The
        // next poll observes ACTIVE only after the maintenance COMMIT and proof.
        Require(Clock::now()<End,Error::DeadlineExceeded);
        STORAGE_POINT("derived-before-commit");CommitStarted=true;
        Sql(Database,"COMMIT");Transaction=false;
        STORAGE_POINT("derived-after-commit");CheckFiles();
        Require(Clock::now()<End,Error::DeadlineExceeded);
        Progress();
        return {Error::None,Flags};
    } catch(const Failure& Problem) {
        sqlite3_progress_handler(Database,0,nullptr,nullptr);
        bool RolledBack=!Transaction || sqlite3_get_autocommit(Database)!=0 ||
            sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr)==SQLITE_OK;
        Progress();
        if(!CommitStarted && RolledBack)Indexes->InvalidateVerification();
        if(CommitStarted || !RolledBack || Problem.Code==Error::StorageCorrupt ||
            Problem.Code==Error::FormatUnsupported || Problem.Code==Error::StorageError)Healthy=false;
        else if(Problem.Code!=Error::DeadlineExceeded)DerivedPaused=true;
        return {CommitStarted || !RolledBack ? Error::Indeterminate : Problem.Code,0};
    } catch(...) {
        Indexes->InvalidateVerification();Healthy=false;
        return {Error::StorageError,0};
    }
}
Result Backend::QueryDerived(const Identity& Id,const Bytes& Descriptor,Deadline OperationEnd)
{
    if(!Healthy || !Indexes)return {Error::StorageUnavailable};
    QueryWire::Request Request;
    try {
        Identity StoreId=Id;StoreId.Key="K";Validate(StoreId);
        QueryWire::Check(ValidField(Id.Key));
        Request=QueryWire::Parse(Descriptor);
    } catch(const Failure& Problem) {
        return {Problem.Code==Error::DeadlineExceeded ? Problem.Code : QueryWire::InvalidQuery};
    }
    End=OperationEnd;Progress(1000000);
    bool Transaction=false;
    try {
        Require(Clock::now()<End,Error::DeadlineExceeded);
        try {CheckFiles();}catch(const Failure& Problem){if(Problem.Code!=Error::DeadlineExceeded)Healthy=false;throw;}
        Sql(Database,"BEGIN");Transaction=true;
        auto View=Indexes->Inspect(Id,Id.Key);
        if(!View.Admitted || View.Status!=Derived::State::Active){
            if(Request.Flags&8)throw Failure(QueryWire::InvalidCursor);
            throw Failure(View.Status==Derived::State::Building ? QueryWire::IndexPreparing : QueryWire::QueryUnavailable);
        }
        QueryWire::Boundary Boundary;
        if(Request.Flags&8) {
            Boundary=QueryWire::ReadCursor(QuerySecret,Id,Request,Request.Cursor);
            QueryWire::Check(Boundary.Generation==uint64_t(View.Generation),QueryWire::InvalidCursor);
        }
        Kind Type=Request.ExplicitType?Request.Type:Kind::Map;
        if(Request.Flags&8)Type=Boundary.Type;
        else if(!Request.ExplicitType) {
            unsigned Categories=unsigned(View.Booleans>0)+unsigned(View.Numbers>0)+unsigned(View.Strings>0);
            if(Categories>1)throw Failure(QueryWire::AmbiguousFieldType);
            if(Categories==1)Type=View.Booleans?Kind::Boolean:View.Numbers?Kind::Number:Kind::String;
            else Type=Kind::Number; // Empty complete field: no representation is selected publicly.
        }
        if(Type==Kind::Boolean && !(Request.Flags&1))throw Failure(QueryWire::InvalidQuery);
        if(!View.Queryable(Type))throw Failure(QueryWire::QueryUnavailable);
        if((Request.Flags&8) && Request.ExplicitType)QueryWire::Check(Request.Type==Type,QueryWire::InvalidCursor);

        std::string Low(1,char(uint8_t(Type)));
        std::string High(1,char(uint8_t(Type)));High.append(511,char(0xff));
        bool HighInclusive=false;
        if(Request.Flags&1){Low=Request.Equals;High=Request.Equals;HighInclusive=true;}
        else {
            if(Request.Flags&2)Low=Request.Min;
            if(Request.Flags&4){High=Request.Max;HighInclusive=true;}
        }
        auto LowerPrefix=Low.substr(0,512), UpperPrefix=High.substr(0,512);
        if(Request.Flags&8){
            auto CursorPrefix=Boundary.Scalar.substr(0,512);
            if(Request.Descending)UpperPrefix=std::min(UpperPrefix,CursorPrefix);
            else LowerPrefix=std::max(LowerPrefix,CursorPrefix);
        }
        const char* PrefixSql=Request.Descending ? QueryWire::PrefixDescending : QueryWire::PrefixAscending;
        Statement Prefixes(Database,PrefixSql);Prefixes.Integer(1,View.Generation);Prefixes.Blob(2,LowerPrefix);Prefixes.Blob(3,UpperPrefix);
        Bytes Page{'C','L','Q','R'};Put32(Page,1);Put32(Page,0);Put32(Page,0);
        uint32_t Count=0,Candidates=0,PrefixRows=0,Expanded=0;bool More=false;
        std::string LastScalar,LastKey;
        while(Prefixes.Next()) {
            // Every interior prefix in the selected range has at least one
            // retained entry. Only the two predicate endpoints and a cursor
            // endpoint can be empty after suffix/keyset filtering. Never
            // accept SQL LIMIT truncation as proof that a page is complete.
            Require(++PrefixRows<=104,QueryWire::QueryUnavailable);
            int64_t PrefixId=Prefixes.Number(0);std::string Prefix=Prefixes.Data(1,512);
            Require(!Prefix.empty() && Prefix[0]==char(uint8_t(Type)),Error::StorageCorrupt);
            std::string SuffixLow, SuffixHigh(513,char(0xff));
            if(Prefix==LowerPrefix && Low.size()>Prefix.size())SuffixLow=Low.substr(Prefix.size());
            if(HighInclusive && Prefix==UpperPrefix)SuffixHigh=High.substr(Prefix.size());
            if(SuffixLow>SuffixHigh)continue;
            std::string SeekSuffix=Request.Descending?std::string(513,char(0xff)):std::string();
            std::string SeekKey=Request.Descending?std::string(129,char(0xff)):std::string();
            if((Request.Flags&8) && Prefix==Boundary.Scalar.substr(0,512)){
                SeekSuffix=Boundary.Scalar.substr(Prefix.size());SeekKey=Boundary.Key;
            }
            const char* EntriesSql=Request.Descending ? QueryWire::EntryDescending : QueryWire::EntryAscending;
            Statement Entries(Database,EntriesSql);Entries.Integer(1,PrefixId);Entries.Blob(2,SuffixLow);Entries.Blob(3,SuffixHigh);
            Entries.Blob(4,SeekSuffix);Entries.Blob(5,SeekKey);
            while(Entries.Next()) {
                Require(++Candidates<=101,QueryWire::QueryUnavailable);
                int64_t EntryId=Entries.Number(0);auto Suffix=Entries.Data(1,513),Key=Entries.Data(2,128);
                Require(ValidText(Key,128,true),Error::StorageCorrupt);
                auto Scalar=Prefix+Suffix;
                // The prefix bound can include a trailing shared prefix whose
                // suffix lies beyond Max; never admit that row.
                if(Scalar<Low || (HighInclusive && Scalar>High) ||
                    ((Request.Flags&8) && (Request.Descending ?
                        std::pair{Scalar,Key}>=std::pair{Boundary.Scalar,Boundary.Key} :
                        std::pair{Scalar,Key}<=std::pair{Boundary.Scalar,Boundary.Key})))continue;
                if(Count>=Request.Limit){More=true;break;}
                Statement Member(Database,QueryWire::MemberPoint);
                Member.Integer(1,View.Generation);Member.Blob(2,Key);
                Require(Member.Next() && Member.Number(0)==int64_t(Type) && Member.Number(1)==EntryId && !Member.Next(),Error::StorageCorrupt);
                Identity RecordId=Id;RecordId.Key=Key;
                Statement Primary(Database,QueryWire::PrimaryPoint);BindKey(Primary,RecordId);
                Require(Primary.Next(),Error::StorageCorrupt);auto Blob=Primary.Data(0,MaximumEnvelope);
                Bytes Envelope(Blob.begin(),Blob.end());Require(!Primary.Next(),Error::StorageCorrupt);
                auto Root=Decode(RecordId,Envelope,End);auto Extracted=ExtractScalar(*Root,Id.Key);
                Require(Extracted.Represented && Extracted.Usable && Extracted.Type==Type &&
                    std::string(Extracted.SortKey.begin(),Extracted.SortKey.end())==Scalar,Error::StorageCorrupt);
                // Each item contributes at most 4,096 expanded entries under
                // the qualified F1 codec. Count the actual graph, not bytes.
                auto EntriesIn=[](const Value& Value,auto&& Self)->uint32_t {
                    uint32_t Total=Value.Type==Kind::Array?uint32_t(Value.Array.size()):
                        Value.Type==Kind::Map?uint32_t(Value.Map.size()):0;
                    if(Value.Type==Kind::Array)for(const auto& Child:Value.Array)Total+=Self(*Child,Self);
                    if(Value.Type==Kind::Map)for(const auto& Child:Value.Map)Total+=Self(*Child.second,Self);
                    return Total;
                };
                uint32_t ItemEntries=EntriesIn(*Root,EntriesIn);
                size_t ItemBytes=4+Key.size()+4+Envelope.size();
                // Reserve a maximal cursor even before knowing whether a
                // lookahead exists. One maximum legal item always fits.
                if(Expanded+ItemEntries+3>8192 || Page.size()+ItemBytes+4+QueryWire::MaximumCursorOutput>QueryWire::MaximumPage){
                    Require(Count>0,Error::StorageError);More=true;break;
                }
                Expanded+=ItemEntries+3;QueryWire::Append(Page,Key);QueryWire::Append(Page,Blob);
                LastScalar=std::move(Scalar);LastKey=std::move(Key);++Count;
            }
            if(More || Candidates>=101)break;
        }
        // If examined rows were filtered at the hard ceiling, the worker
        // cannot prove completion without violating the 101-row bound.
        if(Candidates>=101 && !More)throw Failure(QueryWire::QueryUnavailable);
        std::string Cursor=More?QueryWire::MakeCursor(QuerySecret,Id,Request,Type,uint64_t(View.Generation),LastScalar,LastKey):"";
        // CLQR + version + count + cursor length + cursor + complete items.
        Bytes Output{'C','L','Q','R'};Put32(Output,1);Put32(Output,Count);QueryWire::Append(Output,Cursor);
        Output.insert(Output.end(),Page.begin()+16,Page.end());
        Require(Output.size()<=QueryWire::MaximumPage,Error::StorageError);
        Sql(Database,"COMMIT");Transaction=false;
        try {CheckFiles();} catch(const Failure& Problem) {
            if(Problem.Code!=Error::DeadlineExceeded)Healthy=false;
            throw;
        }
        Progress();
        return {Error::None,false,std::move(Output),false};
    } catch(const Failure& Problem) {
        sqlite3_progress_handler(Database,0,nullptr,nullptr);
        bool RolledBack=!Transaction || sqlite3_get_autocommit(Database)!=0 || sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr)==SQLITE_OK;
        Progress();
        if(!RolledBack || Problem.Code==Error::StorageCorrupt || Problem.Code==Error::FormatUnsupported || Problem.Code==Error::StorageError)Healthy=false;
        return {RolledBack?Problem.Code:Error::StorageError};
    } catch(...) {
        sqlite3_progress_handler(Database,0,nullptr,nullptr);if(Transaction)sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr);
        Healthy=false;return {Error::StorageError};
    }
}
bool Backend::MaintainDerived(Deadline OperationEnd)
{ return DerivedOperation(nullptr,nullptr,OperationEnd); }
bool Backend::DerivedOperation(const Identity* Id,const std::string* Field,Deadline OperationEnd,bool Force)
{
    if(!Healthy || DerivedPaused)return false;
    End=std::min(OperationEnd,Clock::now()+std::chrono::seconds(5));Progress(1000000);
    bool Transaction=false,CommitStarted=false;
    try{
        Require(Clock::now()<End,Error::DeadlineExceeded);
        try{CheckFiles();}catch(const Failure& Problem){if(Problem.Code!=Error::DeadlineExceeded)Healthy=false;throw;}
        Sql(Database,"BEGIN IMMEDIATE");Transaction=true;
        bool Accepted=true;
        if(Id)Accepted=Indexes->Prepare(*Id,*Field,Force);
        else if(!Indexes->VerificationComplete()) {
            // A derived-only mismatch closes Query admission for this worker
            // lifetime, but must not disable authoritative D21 operations.
            // VerificationFailed also stops the idle maintenance loop.
            Indexes->VerifyStep();
        }
        else Indexes->Maintain();
        Require(Clock::now()<End,Error::DeadlineExceeded);
        STORAGE_POINT("derived-before-commit");CommitStarted=true;
        Sql(Database,"COMMIT");Transaction=false;
        STORAGE_POINT("derived-after-commit");CheckFiles();Progress();return Accepted;
    }catch(const Failure& Problem){
        sqlite3_progress_handler(Database,0,nullptr,nullptr);
        bool RolledBack=!Transaction || sqlite3_get_autocommit(Database)!=0 || sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr)==SQLITE_OK;
        Progress();
        if(!CommitStarted && RolledBack && Indexes) Indexes->InvalidateVerification();
        if(CommitStarted || !RolledBack || Problem.Code==Error::StorageCorrupt || Problem.Code==Error::FormatUnsupported || Problem.Code==Error::StorageError)Healthy=false;
        else DerivedPaused=true;
        return false;
    }catch(...){if(Indexes)Indexes->InvalidateVerification();Healthy=false;return false;}
}
}
