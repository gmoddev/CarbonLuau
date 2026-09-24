// Research fixture: trace the stock SQLite VFS, not a production/custom VFS.
// A passing observation is NOT a persistence durability qualification.
#include "sqlite3.h"
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <stdexcept>
#include <string>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <sys/stat.h>
#include <unistd.h>
#endif

namespace {
bool Recording = false;
bool Deleted = false;
int DeleteCalls = 0, DirectorySyncRequested = 0, FileSyncs = 0;
int DirectorySyncs = 0, SyncsAfterDelete = 0;
sqlite3_vfs* DefaultVfs = nullptr;

void Sync(bool Directory)
{
    if (!Recording) return;
    if (Directory) ++DirectorySyncs; else ++FileSyncs;
    if (Deleted) ++SyncsAfterDelete;
}

int TraceDelete(sqlite3_vfs*, const char* Name, int SyncDirectory)
{
    if (Recording) DirectorySyncRequested += SyncDirectory;
    return DefaultVfs->xDelete(DefaultVfs, Name, SyncDirectory);
}

#ifdef _WIN32
using FlushFunction = BOOL (WINAPI*)(HANDLE);
using DeleteFunction = BOOL (WINAPI*)(LPCWSTR);
FlushFunction OriginalFlush = nullptr;
DeleteFunction OriginalDelete = nullptr;
BOOL WINAPI TraceFlush(HANDLE File)
{
    BY_HANDLE_FILE_INFORMATION Info{};
    const bool Directory = GetFileInformationByHandle(File, &Info) &&
        (Info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY);
    Sync(Directory);
    return OriginalFlush(File);
}
BOOL WINAPI TraceDeleteFile(LPCWSTR Name)
{
    const BOOL Result = OriginalDelete(Name);
    if (Recording && Result) { ++DeleteCalls; Deleted = true; }
    return Result;
}
#endif

void Check(bool Condition, const char* Message)
{
    if (!Condition) throw std::runtime_error(Message);
}

void Exec(sqlite3* Database, const char* Sql)
{
    const int Status = sqlite3_exec(Database, Sql, nullptr, nullptr, nullptr);
    Check(Status == SQLITE_OK, sqlite3_errmsg(Database));
}

int Scalar(sqlite3* Database, const char* Sql)
{
    sqlite3_stmt* Statement = nullptr;
    Check(sqlite3_prepare_v2(Database, Sql, -1, &Statement, nullptr) == SQLITE_OK, "prepare");
    const int Status = sqlite3_step(Statement);
    const int Result = Status == SQLITE_ROW ? sqlite3_column_int(Statement, 0) : -1;
    sqlite3_finalize(Statement);
    Check(Status == SQLITE_ROW, "scalar");
    return Result;
}

void Run(const std::filesystem::path& Folder, const char* Mode, int Expected)
{
    const auto Path = Folder / (std::string(Mode) + ".sqlite3");
    Check(!std::filesystem::exists(Path), "probe must never open existing data");
    sqlite3* Database = nullptr;
    Check(sqlite3_open_v2(Path.string().c_str(), &Database,
        SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE, "carbonluau-durability-probe") == SQLITE_OK, "open");
    try {
        Exec(Database, "PRAGMA page_size=4096; PRAGMA journal_mode=DELETE; PRAGMA synchronous=EXTRA;"
            "CREATE TABLE Record(Key INTEGER PRIMARY KEY, Value BLOB);"
            "INSERT INTO Record VALUES(1, zeroblob(1000));");
        Check(Scalar(Database, "SELECT journal_mode='delete' FROM pragma_journal_mode") == 1, "journal mode");
        Exec(Database, Expected == 3 ? "PRAGMA synchronous=EXTRA" : "PRAGMA synchronous=FULL");
        Check(Scalar(Database, "PRAGMA synchronous") == Expected, "synchronous pragma");
        Exec(Database, "BEGIN IMMEDIATE; UPDATE Record SET Value=zeroblob(2000) WHERE Key=1;");
        DeleteCalls = DirectorySyncRequested = FileSyncs = DirectorySyncs = SyncsAfterDelete = 0;
        Deleted = false;
        Recording = true;
        const int Commit = sqlite3_exec(Database, "COMMIT", nullptr, nullptr, nullptr);
        Recording = false;
        std::printf("{\"Mode\":\"%s\",\"Pragma\":%d,\"Commit\":%d,\"Deletes\":%d,"
            "\"DirectorySyncRequested\":%d,\"FileSyncs\":%d,\"DirectorySyncs\":%d,"
            "\"SyncsAfterDelete\":%d}\n", Mode, Expected, Commit, DeleteCalls,
            DirectorySyncRequested, FileSyncs, DirectorySyncs, SyncsAfterDelete);
        Check(Commit == SQLITE_OK, "commit");
        Check(DeleteCalls == 1 && FileSyncs >= 2, "stock delete/file-sync path not observed");
        Check(DirectorySyncRequested == (Expected == 3 ? 1 : 0), "pager EXTRA request");
#ifdef _WIN32
        Check(DirectorySyncs == 0 && SyncsAfterDelete == 0, "Windows VFS observation changed; re-investigate");
#else
        // The first journal-file sync can also sync its creation directory.
        // The distinction under investigation is the sync AFTER deletion.
        Check(DirectorySyncs >= (Expected == 3 ? 1 : 0), "Linux directory sync observation changed");
        Check(SyncsAfterDelete == (Expected == 3 ? 1 : 0), "Linux post-delete sync observation changed");
#endif
        Check(Scalar(Database, "SELECT length(Value) FROM Record WHERE Key=1") == 2000, "value");
    } catch (...) { Recording = false; sqlite3_close(Database); throw; }
    Check(sqlite3_close(Database) == SQLITE_OK, "close");
    Check(sqlite3_open_v2(Path.string().c_str(), &Database, SQLITE_OPEN_READONLY, nullptr) == SQLITE_OK, "reopen");
    const int Length = Scalar(Database, "SELECT length(Value) FROM Record WHERE Key=1");
    const int Close = sqlite3_close(Database);
    Check(Length == 2000 && Close == SQLITE_OK, "ordinary reopen failed");
}
} // namespace

#ifndef _WIN32
extern "C" int __real_fsync(int);
extern "C" int __real_fdatasync(int);
extern "C" int __real_unlink(const char*);
extern "C" int __wrap_fsync(int File)
{
    struct stat Info{};
    Sync(fstat(File, &Info) == 0 && S_ISDIR(Info.st_mode));
    return __real_fsync(File);
}
extern "C" int __wrap_fdatasync(int File)
{
    struct stat Info{};
    Sync(fstat(File, &Info) == 0 && S_ISDIR(Info.st_mode));
    return __real_fdatasync(File);
}
extern "C" int __wrap_unlink(const char* Name)
{
    const int Result = __real_unlink(Name);
    if (Recording && Result == 0) { ++DeleteCalls; Deleted = true; }
    return Result;
}
#endif

int main()
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    const auto Process = GetCurrentProcessId();
#else
    const auto Process = getpid();
#endif
    try {
        Check(std::strcmp(sqlite3_libversion(), "3.53.4") == 0, "unexpected SQLite version");
        Check(sqlite3_initialize() == SQLITE_OK, "initialize");
        DefaultVfs = sqlite3_vfs_find(nullptr);
        Check(DefaultVfs && DefaultVfs->iVersion >= 3, "VFS version");
        std::printf("{\"SQLite\":\"%s\",\"SourceId\":\"%s\",\"Vfs\":\"%s\"}\n",
            sqlite3_libversion(), sqlite3_sourceid(), DefaultVfs->zName);
#ifdef _WIN32
        OriginalFlush = reinterpret_cast<FlushFunction>(DefaultVfs->xGetSystemCall(DefaultVfs, "FlushFileBuffers"));
        OriginalDelete = reinterpret_cast<DeleteFunction>(DefaultVfs->xGetSystemCall(DefaultVfs, "DeleteFileW"));
        Check(OriginalFlush && OriginalDelete, "Windows syscall seam");
        Check(DefaultVfs->xSetSystemCall(DefaultVfs, "FlushFileBuffers",
            reinterpret_cast<sqlite3_syscall_ptr>(TraceFlush)) == SQLITE_OK, "flush trace");
        Check(DefaultVfs->xSetSystemCall(DefaultVfs, "DeleteFileW",
            reinterpret_cast<sqlite3_syscall_ptr>(TraceDeleteFile)) == SQLITE_OK, "delete trace");
#endif
        sqlite3_vfs Trace = *DefaultVfs;
        Trace.zName = "carbonluau-durability-probe";
        Trace.pNext = nullptr;
        Trace.xDelete = TraceDelete;
        Check(sqlite3_vfs_register(&Trace, 0) == SQLITE_OK, "trace VFS");
        const auto Stamp = std::chrono::steady_clock::now().time_since_epoch().count();
        const auto Folder = std::filesystem::current_path() /
            ("probe-" + std::to_string(Process) + "-" + std::to_string(Stamp));
        Check(std::filesystem::create_directory(Folder), "fresh fixture directory required");
        Run(Folder, "FULL", 2);
        Run(Folder, "EXTRA", 3);
        sqlite3_vfs_unregister(&Trace);
        std::puts("[CarbonLuau:Persistence] Observation PASS; not a power-loss or backend qualification.");
        return 0;
    } catch (const std::exception& Error) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Probe failed: %s\n", Error.what());
        return 1;
    }
}
