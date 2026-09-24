#include "Backend.hpp"
#include "sqlite3.h"
#include <algorithm>
#include <array>
#include <cstring>
#include <fstream>
#include <map>
#include <set>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <sys/stat.h>
#endif

namespace CarbonLuau::Persistence {
#ifdef CARBONLUAU_PERSISTENCE_TESTING
void TestCheckpoint(const char* Stage);
#define STORAGE_POINT(Stage) TestCheckpoint(Stage)
#else
#define STORAGE_POINT(Stage) ((void)0)
#endif
namespace {
constexpr uint64_t DatabaseBytes = 512ull * 1024 * 1024;
constexpr uint64_t JournalBytes = 131072ull * (4096 + 8) + 65536;
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
    try { Open(); Healthy = true; }
    catch (...) { if (Database) sqlite3_close(Database); Database = nullptr; throw; }
}
Backend::~Backend() { if (Database) sqlite3_close(Database); }
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
        Require(Allocation<=1280ull*1024*1024-Total,Error::StorageFull); Total+=Allocation;
    }
    Require(Total <= 1280ull * 1024 * 1024, Error::StorageFull);
    // Recovery may raise SQLite's page limit from the first journal header.
    // Reject an out-of-contract original size before SQLite can extend the DB.
    const auto Journal = Directory / "store.sqlite3-journal";
    if (std::filesystem::exists(Journal) && std::filesystem::file_size(Journal) >= 28) {
        std::ifstream Input(Journal, std::ios::binary); std::array<uint8_t,28> Header{};
        Require(bool(Input.read(reinterpret_cast<char*>(Header.data()), Header.size())), Error::StorageError);
        const uint8_t Magic[] = {0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7};
        if (!std::memcmp(Header.data(), Magic, 8)) {
            const auto Sector = Big32(Header.data()+20);
            Require(Big32(Header.data()+16) <= 131072 && Big32(Header.data()+24) == 4096 &&
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
    CheckFiles(); const auto Path = Directory / "store.sqlite3";
    const bool New = !std::filesystem::exists(Path);
    Require(New || std::filesystem::file_size(Path) >= 100, Error::StorageCorrupt);
    Require(std::string(sqlite3_libversion()) == "3.53.4" &&
        std::string(sqlite3_sourceid())=="2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc" && !sqlite3_compileoption_used("NO_SYNC") &&
        !sqlite3_compileoption_used("DISABLE_DIRSYNC") && sqlite3_compileoption_used("OMIT_WAL"), Error::FormatUnsupported);
    SqlCheck(sqlite3_open_v2(Path.u8string().c_str(), &Database, SQLITE_OPEN_READWRITE | (New ? SQLITE_OPEN_CREATE : 0) | SQLITE_OPEN_NOFOLLOW, nullptr));
    sqlite3_progress_handler(Database, 1000, [](void* Context) { return Clock::now() >= *static_cast<Deadline*>(Context) ? 1 : 0; }, &End);
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
        Scalar(Database,"PRAGMA max_page_count=131072") == 131072, Error::StorageUnavailable);
    // OMIT_WAL rejects WAL read formats inside SQLite, including page 1 restored
    // by hot-journal recovery. A newer write-only format can instead become
    // read-only: never publish a Ready backend for that case either.
    Require(sqlite3_db_readonly(Database, "main") == 0, Error::FormatUnsupported);
    CheckSchema(New); CheckRecords(); CheckFiles();
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
    Require(Scalar(Database,"PRAGMA application_id") == 1129074756 && Scalar(Database,"PRAGMA user_version") == 1, Error::FormatUnsupported);
    Require(ScalarText(Database,"PRAGMA integrity_check") == "ok", Error::StorageCorrupt);
    Statement Query(Database,"SELECT sql FROM sqlite_schema ORDER BY name");
    for (unsigned Index : {1u,0u,2u}) Require(Query.Next() && Query.Data(0,1024,true) == Schema[Index], Error::StorageCorrupt);
    Require(!Query.Next(), Error::StorageCorrupt);
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
        Decode(Id, CarbonLuau::Persistence::Bytes(Blob.begin(),Blob.end()),End);
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
    if (!Healthy) return {Error::StorageUnavailable}; End = RequestEnd;
    bool Transaction = false, CommitStarted = false;
    try {
        Require(Clock::now() < End, Error::DeadlineExceeded); Validate(Id);
        Require(Op == Operation::Get || Op == Operation::Set || Op == Operation::Remove);
        if (Op == Operation::Set) {
            // A malformed incoming frame is not evidence that durable storage is
            // corrupt. Reject it before BEGIN without disabling healthy data.
            try { Decode(Id,Envelope,End); }
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
        Bytes Old; int64_t OldCharge = 0; bool Found;
        {
            Statement Query(Database,"SELECT Envelope,Charge FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3"); BindKey(Query,Id);
            Found = Query.Next();
            if (Found) { auto Blob=Query.Data(0,MaximumEnvelope); Old.assign(Blob.begin(),Blob.end()); OldCharge=Query.Number(1);
                Require(OldCharge == int64_t(Id.Store.size()+Id.Key.size()+Old.size()),Error::StorageCorrupt); Decode(Id,Old,End); }
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
        Require(Clock::now() < End,Error::DeadlineExceeded); CommitStarted=true;
        STORAGE_POINT("before-commit");
        Sql(Database,"COMMIT"); Transaction=false;
        STORAGE_POINT("after-commit");
        Require(Clock::now() < End,Error::Indeterminate); CheckFiles();
        return {Error::None,Op==Operation::Set || Found,{},After.Keys>0};
    } catch (const Failure& Problem) {
        bool RolledBack = !Transaction;
        if (Transaction) { sqlite3_progress_handler(Database,0,nullptr,nullptr); RolledBack=sqlite3_get_autocommit(Database)!=0 || sqlite3_exec(Database,"ROLLBACK",nullptr,nullptr,nullptr)==SQLITE_OK;
            sqlite3_progress_handler(Database,1000,[](void* Context) { return Clock::now()>=*static_cast<Deadline*>(Context) ? 1 : 0; },&End); }
        if (CommitStarted || !RolledBack) { Healthy=false; return {Op==Operation::Get ? Error::StorageError : Error::Indeterminate}; }
        if (Problem.Code==Error::StorageCorrupt || Problem.Code==Error::FormatUnsupported || Problem.Code==Error::StorageError) Healthy=false;
        return {Problem.Code};
    } catch (...) { Healthy=false; return {Op==Operation::Get ? Error::StorageError : Error::Indeterminate}; }
}
}
