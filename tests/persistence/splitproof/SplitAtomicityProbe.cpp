// Standalone research probe for pinned SQLite multi-file PERSIST/EXTRA behavior.
// Never linked into the CarbonLuau worker or shipped in a release package.
#include "sqlite3.h"
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <iostream>
#include <stdexcept>
#include <string>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <sys/stat.h>
#endif

namespace {
sqlite3_vfs* BaseVfs = nullptr;
sqlite3_vfs WrappedVfs{};
enum class CrashPoint { None, BeforeSuperDelete, AfterSuperDelete };
CrashPoint Crash = CrashPoint::None;

[[noreturn]] void Die(const char* Message) { throw std::runtime_error(Message); }
void Check(int Code, sqlite3* Db = nullptr) {
    if (Code == SQLITE_OK || Code == SQLITE_ROW || Code == SQLITE_DONE) return;
    std::string Message = "SQLite code=" + std::to_string(Code);
    if (Db) Message += " detail=" + std::string(sqlite3_errmsg(Db));
    throw std::runtime_error(Message);
}
void Sql(sqlite3* Db, const char* Text) { Check(sqlite3_exec(Db, Text, nullptr, nullptr, nullptr), Db); }
int Delete(sqlite3_vfs*, const char* Path, int SyncDir) {
    const bool Super = Path && std::strstr(Path, "-mj") != nullptr;
    if (Super && SyncDir && Crash == CrashPoint::BeforeSuperDelete) std::_Exit(77);
    const int Result = BaseVfs->xDelete(BaseVfs, Path, SyncDir);
    if (Super && SyncDir && Crash == CrashPoint::AfterSuperDelete && Result == SQLITE_OK) std::_Exit(78);
    return Result;
}
void InstallVfs() {
    BaseVfs = sqlite3_vfs_find(nullptr);
    if (!BaseVfs) Die("default VFS missing");
    WrappedVfs = *BaseVfs;
    WrappedVfs.zName = "CarbonLuauSplitProofVfs";
    WrappedVfs.xDelete = Delete;
    Check(sqlite3_vfs_register(&WrappedVfs, 1));
}
int64_t Scalar(sqlite3* Db, const char* Text) {
    sqlite3_stmt* Statement = nullptr;
    Check(sqlite3_prepare_v2(Db, Text, -1, &Statement, nullptr), Db);
    const int Result = sqlite3_step(Statement);
    if (Result != SQLITE_ROW) { sqlite3_finalize(Statement); Die("scalar row missing"); }
    const int64_t Value = sqlite3_column_int64(Statement, 0);
    Check(sqlite3_finalize(Statement), Db);
    return Value;
}
std::string TextScalar(sqlite3* Db, const char* Text) {
    sqlite3_stmt* Statement = nullptr;
    Check(sqlite3_prepare_v2(Db, Text, -1, &Statement, nullptr), Db);
    if (sqlite3_step(Statement) != SQLITE_ROW) { sqlite3_finalize(Statement); Die("text row missing"); }
    const unsigned char* Data = sqlite3_column_text(Statement, 0);
    const std::string Value = Data ? reinterpret_cast<const char*>(Data) : "";
    Check(sqlite3_finalize(Statement), Db);
    return Value;
}
struct Connection {
    sqlite3* Db = nullptr;
    explicit Connection(const std::filesystem::path& Folder) {
        const auto Primary = (Folder / "primary.sqlite").string();
        Check(sqlite3_open_v2(Primary.c_str(), &Db, SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE, WrappedVfs.zName), Db);
        sqlite3_limit(Db, SQLITE_LIMIT_ATTACHED, 1);
        sqlite3_stmt* Statement = nullptr;
        Check(sqlite3_prepare_v2(Db, "ATTACH DATABASE ?1 AS derived", -1, &Statement, nullptr), Db);
        const auto Derived = (Folder / "derived.sqlite").string();
        Check(sqlite3_bind_text(Statement, 1, Derived.c_str(), int(Derived.size()), SQLITE_TRANSIENT), Db);
        Check(sqlite3_step(Statement), Db);
        Check(sqlite3_finalize(Statement), Db);
        Sql(Db, "PRAGMA main.page_size=4096; PRAGMA derived.page_size=4096;"
                "PRAGMA main.journal_mode=PERSIST; PRAGMA derived.journal_mode=PERSIST;"
                "PRAGMA main.synchronous=EXTRA; PRAGMA derived.synchronous=EXTRA;"
                "PRAGMA temp_store=MEMORY; PRAGMA cache_spill=OFF;"
                "PRAGMA main.cache_size=-4096; PRAGMA derived.cache_size=-4096;"
                "PRAGMA main.mmap_size=0; PRAGMA derived.mmap_size=0;"
                "PRAGMA main.auto_vacuum=NONE; PRAGMA derived.auto_vacuum=NONE;"
                "PRAGMA main.journal_size_limit=-1; PRAGMA derived.journal_size_limit=-1;");
        if (Scalar(Db, "PRAGMA main.page_size") != 4096 || Scalar(Db, "PRAGMA derived.page_size") != 4096 ||
            Scalar(Db, "PRAGMA main.synchronous") != 3 || Scalar(Db, "PRAGMA derived.synchronous") != 3 ||
            Scalar(Db, "PRAGMA main.cache_spill") != 0 || Scalar(Db, "PRAGMA derived.cache_spill") != 0 ||
            Scalar(Db, "PRAGMA main.auto_vacuum") != 0 || Scalar(Db, "PRAGMA derived.auto_vacuum") != 0 ||
            Scalar(Db, "PRAGMA main.journal_size_limit") != -1 || Scalar(Db, "PRAGMA derived.journal_size_limit") != -1 ||
            Scalar(Db, "PRAGMA main.max_page_count=131072") != 131072)
            Die("unqualified pager geometry or synchronous mode");
        if (TextScalar(Db, "PRAGMA main.journal_mode") != "persist" ||
            TextScalar(Db, "PRAGMA derived.journal_mode") != "persist")
            Die("unqualified journal mode");
    }
    ~Connection() { if (Db) sqlite3_close(Db); }
};
void Schema(sqlite3* Db) {
    // Primary definitions are the exact Foundation 1 table shapes. The derived
    // definitions are the current 2A candidate, merely in an attached schema.
    Sql(Db, "CREATE TABLE main.Records(Namespace BLOB NOT NULL,Store BLOB NOT NULL,Key BLOB NOT NULL,Envelope BLOB NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Namespace,Store,Key)) WITHOUT ROWID");
    Sql(Db, "CREATE TABLE main.Quotas(Namespace BLOB PRIMARY KEY,Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Stores INTEGER NOT NULL) WITHOUT ROWID");
    Sql(Db, "CREATE TABLE main.Totals(Id INTEGER PRIMARY KEY CHECK(Id=1),Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Namespaces INTEGER NOT NULL)");
    Sql(Db, "CREATE TABLE derived.DerivedTotals(Id INTEGER PRIMARY KEY CHECK(Id=1),NextField INTEGER NOT NULL,NextGeneration INTEGER NOT NULL,NextPrefix INTEGER NOT NULL,NextEntry INTEGER NOT NULL,WorkGeneration INTEGER NOT NULL,Bytes INTEGER NOT NULL,Charge INTEGER NOT NULL)");
    Sql(Db, "CREATE TABLE derived.DerivedNamespaces(Namespace BLOB PRIMARY KEY,Bytes INTEGER NOT NULL,Charge INTEGER NOT NULL) WITHOUT ROWID");
    Sql(Db, "CREATE TABLE derived.DerivedFields(Id INTEGER PRIMARY KEY,Namespace BLOB NOT NULL,Store BLOB NOT NULL,Name BLOB NOT NULL,Charge INTEGER NOT NULL)");
    Sql(Db, "CREATE UNIQUE INDEX derived.DerivedFieldNames ON DerivedFields(Namespace,Store,Name)");
    Sql(Db, "CREATE TABLE derived.DerivedGenerations(Id INTEGER PRIMARY KEY,FieldId INTEGER NOT NULL,State INTEGER NOT NULL,Checkpoint BLOB NOT NULL,Members INTEGER NOT NULL,Booleans INTEGER NOT NULL,Numbers INTEGER NOT NULL,Strings INTEGER NOT NULL,Oversized INTEGER NOT NULL,Charge INTEGER NOT NULL)");
    Sql(Db, "CREATE INDEX derived.DerivedGenerationFields ON DerivedGenerations(FieldId,Id)");
    Sql(Db, "CREATE INDEX derived.DerivedGenerationWork ON DerivedGenerations(Id) WHERE State IN (1,3)");
    Sql(Db, "CREATE TABLE derived.DerivedPrefixes(Id INTEGER PRIMARY KEY,Generation INTEGER NOT NULL,Prefix BLOB NOT NULL,Refs INTEGER NOT NULL,Charge INTEGER NOT NULL)");
    Sql(Db, "CREATE UNIQUE INDEX derived.DerivedPrefixOrder ON DerivedPrefixes(Generation,Prefix)");
    Sql(Db, "CREATE TABLE derived.DerivedEntries(Id INTEGER PRIMARY KEY,PrefixId INTEGER NOT NULL,Suffix BLOB NOT NULL,RecordKey BLOB NOT NULL,Charge INTEGER NOT NULL)");
    Sql(Db, "CREATE UNIQUE INDEX derived.DerivedEntryOrder ON DerivedEntries(PrefixId,Suffix,RecordKey)");
    Sql(Db, "CREATE TABLE derived.DerivedMembers(Generation INTEGER NOT NULL,RecordKey BLOB NOT NULL,Type INTEGER NOT NULL,EntryId INTEGER NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Generation,RecordKey)) WITHOUT ROWID");
    Sql(Db, "CREATE UNIQUE INDEX derived.DerivedMemberEntries ON DerivedMembers(EntryId) WHERE EntryId<>0");
    Sql(Db, "INSERT INTO main.Totals VALUES(1,0,0,0)");
    Sql(Db, "INSERT INTO derived.DerivedTotals VALUES(1,1,1,1,1,0,65,65)");
}
void PrintFiles(const std::filesystem::path& Folder) {
    for (const auto& File : std::filesystem::directory_iterator(Folder)) {
        if (!File.is_regular_file()) continue;
        uint64_t Allocated = 0;
#ifdef _WIN32
        HANDLE Handle = CreateFileW(File.path().c_str(), FILE_READ_ATTRIBUTES,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
        FILE_STANDARD_INFO Info{};
        if (Handle == INVALID_HANDLE_VALUE ||
            !GetFileInformationByHandleEx(Handle, FileStandardInfo, &Info, sizeof(Info))) {
            if (Handle != INVALID_HANDLE_VALUE) CloseHandle(Handle);
            Die("allocation query failed");
        }
        Allocated = uint64_t(Info.AllocationSize.QuadPart);
        CloseHandle(Handle);
#else
        struct stat Info{};
        if (stat(File.path().c_str(), &Info) != 0 || Info.st_blocks < 0) Die("allocation query failed");
        Allocated = uint64_t(Info.st_blocks) * 512;
#endif
        std::cout << "[CarbonLuau:SplitProof] file=" << File.path().filename().string()
                  << " eof=" << File.file_size() << " allocated=" << Allocated << '\n';
    }
}
}

int main(int Count, char** Args) {
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
    try {
        if (Count != 3) Die("usage: SplitAtomicityProbe <new|before|after|normal|derivedonlybefore|inspect|check0|check1> <folder>");
        const std::string Mode = Args[1];
        const auto Folder = std::filesystem::absolute(Args[2]);
        if (Mode == "inspect") { PrintFiles(Folder); return 0; }
        if (Mode == "new") {
            if (std::filesystem::exists(Folder)) Die("fixture already exists; no overwrite");
            std::filesystem::create_directories(Folder);
        } else if (!std::filesystem::exists(Folder)) Die("fixture missing");
        InstallVfs();
        Connection C(Folder);
        if (Mode == "new") {
            Schema(C.Db);
            std::cout << "[CarbonLuau:SplitProof] sqlite=" << sqlite3_libversion()
                      << " source=" << sqlite3_sourceid() << " primary_pages=" << Scalar(C.Db, "PRAGMA main.page_count")
                      << " derived_pages=" << Scalar(C.Db, "PRAGMA derived.page_count") << '\n';
        } else if (Mode == "before" || Mode == "after" || Mode == "normal" || Mode == "derivedonlybefore") {
            Sql(C.Db, "BEGIN IMMEDIATE");
            if (Mode != "derivedonlybefore") Sql(C.Db, "UPDATE main.Totals SET Bytes=Bytes+1 WHERE Id=1");
            Sql(C.Db, "UPDATE derived.DerivedTotals SET Bytes=Bytes+1 WHERE Id=1");
            Crash = Mode == "before" || Mode == "derivedonlybefore" ? CrashPoint::BeforeSuperDelete :
                    Mode == "after" ? CrashPoint::AfterSuperDelete : CrashPoint::None;
            Sql(C.Db, "COMMIT");
            Crash = CrashPoint::None;
            std::cout << "[CarbonLuau:SplitProof] committed main=" << Scalar(C.Db, "SELECT Bytes FROM main.Totals")
                      << " derived=" << Scalar(C.Db, "SELECT Bytes-65 FROM derived.DerivedTotals") << '\n';
        } else if (Mode == "check0" || Mode == "check1") {
            const int64_t Expected = Mode == "check0" ? 0 : 1;
            const auto Main = Scalar(C.Db, "SELECT Bytes FROM main.Totals");
            const auto Derived = Scalar(C.Db, "SELECT Bytes-65 FROM derived.DerivedTotals");
            std::cout << "[CarbonLuau:SplitProof] recovered main=" << Main << " derived=" << Derived
                      << " expected=" << Expected << '\n';
            if (Main != Expected || Derived != Expected) Die("cross-database atomicity violation");
        } else Die("unknown mode");
        return 0;
    } catch (const std::exception& Error) {
        std::cerr << "[CarbonLuau:SplitProof] " << Error.what() << '\n';
        return 1;
    }
}
