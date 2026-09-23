// Investigation only: transparent VFS tracing/fault injection and killed child
// processes. Not a production VFS, storage schema, quota adapter or worker.
#include "sqlite3.h"
#include <algorithm>
#include <chrono>
#include <cstddef>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <new>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <csignal>
#include <sys/wait.h>
#include <unistd.h>
#endif

namespace {
using Clock = std::chrono::steady_clock;
sqlite3_vfs* Parent = nullptr;
sqlite3_vfs Tracer{};
bool InCommit = false, Record = false, Marker = false, WalMarker = false, FaultHit = false;
std::string StopAt, Events;
int DatabaseSyncs = 0, JournalMarkerSyncs = 0, WalSyncs = 0;
sqlite3_int64 PeakJournalWrite = 0, PeakWalWrite = 0;

void Check(bool Good, const char* Message)
{
    if (!Good) throw std::runtime_error(Message);
}
void Point(const char* Name)
{
    if (StopAt != Name) return;
    // Immediate OS termination, with no SQLite close, C++ destructor or exit handler.
#ifdef _WIN32
    TerminateProcess(GetCurrentProcess(), 77);
#else
    kill(getpid(), SIGKILL);
#endif
    std::abort();
}
// SQLite guarantees eight-byte alignment, not C++ max_align_t alignment.
struct File {
    sqlite3_file Base;
    sqlite3_file* Real;
    const char* Kind;
};
static_assert(alignof(File) <= 8 && sizeof(File) % 8 == 0, "SQLite file alignment");
File* Cast(sqlite3_file* Value) { return reinterpret_cast<File*>(Value); }
void Event(File* Value, const char* Operation)
{
    if (!Record || Events.size() > 8192) return;
    Events += Value->Kind; Events += ':'; Events += Operation; Events += ' ';
}
int Close(sqlite3_file* Value) { return Cast(Value)->Real->pMethods->xClose(Cast(Value)->Real); }
int Read(sqlite3_file* Value, void* Data, int Size, sqlite3_int64 Offset)
{ return Cast(Value)->Real->pMethods->xRead(Cast(Value)->Real, Data, Size, Offset); }
int Write(sqlite3_file* Value, const void* Data, int Size, sqlite3_int64 Offset)
{
    auto* Item = Cast(Value);
    const int Result = Item->Real->pMethods->xWrite(Item->Real, Data, Size, Offset);
    const bool IsMarker = InCommit && Item->Kind[0] == 'J' && Offset == 0 && Size == 28 &&
        std::all_of(static_cast<const unsigned char*>(Data), static_cast<const unsigned char*>(Data) + Size,
            [](unsigned char Byte) { return Byte == 0; });
    Event(Item, IsMarker ? "ZeroHeader" : "Write");
    if (Item->Kind[0] == 'J') PeakJournalWrite = std::max(PeakJournalWrite, Offset + Size);
    if (Item->Kind[0] == 'W') PeakWalWrite = std::max(PeakWalWrite, Offset + Size);
    if (Result == SQLITE_OK && InCommit) {
        Point("during-commit");
        if (Item->Kind[0] == 'W' && Offset >= 32 && (Offset - 32) % (4096 + 24) == 0 && Size >= 24) {
            const auto* Header = static_cast<const unsigned char*>(Data);
            if (Header[4] || Header[5] || Header[6] || Header[7]) WalMarker = true;
        }
        if (IsMarker) { Marker = true; Point("marker-write"); }
    }
    return Result;
}
int Truncate(sqlite3_file* Value, sqlite3_int64 Size)
{
    auto* Item = Cast(Value);
    const int Result = Item->Real->pMethods->xTruncate(Item->Real, Size);
    Event(Item, Size == 0 ? "TruncateZero" : "Truncate");
    if (Result == SQLITE_OK && InCommit && Item->Kind[0] == 'J' && Size == 0) {
        Marker = true; Point("marker-write");
    }
    return Result;
}
int Sync(sqlite3_file* Value, int Flags)
{
    auto* Item = Cast(Value);
    const bool FinalJournal = InCommit && Marker && Item->Kind[0] == 'J';
    if (FinalJournal && StopAt == "fault-marker-sync") {
        FaultHit = true; Event(Item, "InjectedSyncFailure"); return SQLITE_IOERR_FSYNC;
    }
    const int Result = Item->Real->pMethods->xSync(Item->Real, Flags);
    Event(Item, Result == SQLITE_OK ? "SyncOK" : "SyncError");
    if (Result == SQLITE_OK && InCommit) {
        Point("during-commit");
        if (Item->Kind[0] == 'D') { ++DatabaseSyncs; Point("database-sync"); }
        if (Item->Kind[0] == 'W') { ++WalSyncs; if (WalMarker) Point("wal-sync"); }
        if (FinalJournal) { ++JournalMarkerSyncs; Point("marker-sync"); }
    }
    return Result;
}
int Size(sqlite3_file* Value, sqlite3_int64* Out) { return Cast(Value)->Real->pMethods->xFileSize(Cast(Value)->Real, Out); }
int Lock(sqlite3_file* Value, int Kind) { return Cast(Value)->Real->pMethods->xLock(Cast(Value)->Real, Kind); }
int Unlock(sqlite3_file* Value, int Kind) { return Cast(Value)->Real->pMethods->xUnlock(Cast(Value)->Real, Kind); }
int Reserved(sqlite3_file* Value, int* Out) { return Cast(Value)->Real->pMethods->xCheckReservedLock(Cast(Value)->Real, Out); }
int Control(sqlite3_file* Value, int Op, void* Arg) { return Cast(Value)->Real->pMethods->xFileControl(Cast(Value)->Real, Op, Arg); }
int Sector(sqlite3_file* Value) { return Cast(Value)->Real->pMethods->xSectorSize(Cast(Value)->Real); }
int Device(sqlite3_file* Value) { return Cast(Value)->Real->pMethods->xDeviceCharacteristics(Cast(Value)->Real); }
int ShmMap(sqlite3_file* Value, int Page, int Bytes, int Extend, void volatile** Out)
{ return Cast(Value)->Real->pMethods->xShmMap(Cast(Value)->Real, Page, Bytes, Extend, Out); }
int ShmLock(sqlite3_file* Value, int Offset, int Count, int Flags)
{ return Cast(Value)->Real->pMethods->xShmLock(Cast(Value)->Real, Offset, Count, Flags); }
void ShmBarrier(sqlite3_file* Value) { Cast(Value)->Real->pMethods->xShmBarrier(Cast(Value)->Real); }
int ShmUnmap(sqlite3_file* Value, int Delete) { return Cast(Value)->Real->pMethods->xShmUnmap(Cast(Value)->Real, Delete); }
int Fetch(sqlite3_file* Value, sqlite3_int64 Offset, int Count, void** Out)
{ return Cast(Value)->Real->pMethods->xFetch(Cast(Value)->Real, Offset, Count, Out); }
int Unfetch(sqlite3_file* Value, sqlite3_int64 Offset, void* Data)
{ return Cast(Value)->Real->pMethods->xUnfetch(Cast(Value)->Real, Offset, Data); }
const sqlite3_io_methods Methods = {3, Close, Read, Write, Truncate, Sync, Size,
    Lock, Unlock, Reserved, Control, Sector, Device, ShmMap, ShmLock, ShmBarrier, ShmUnmap, Fetch, Unfetch};
int Open(sqlite3_vfs*, const char* Name, sqlite3_file* Out, int Flags, int* Actual)
{
    auto* Item = new (Out) File{};
    Item->Real = reinterpret_cast<sqlite3_file*>(Item + 1);
    Item->Kind = Flags & SQLITE_OPEN_MAIN_DB ? "Database" : Flags & SQLITE_OPEN_MAIN_JOURNAL ? "Journal" :
        Flags & SQLITE_OPEN_WAL ? "Wal" : "Other";
    const int Result = Parent->xOpen(Parent, Name, Item->Real, Flags, Actual);
    if (Result == SQLITE_OK) {
        if (Item->Real->pMethods->iVersion < 3) {
            Item->Real->pMethods->xClose(Item->Real); return SQLITE_ERROR;
        }
        Item->Base.pMethods = &Methods;
    }
    return Result;
}
int Delete(sqlite3_vfs*, const char* Name, int DirectorySync)
{
    if (Record) Events += DirectorySync ? "Journal:DeleteSyncRequested " : "Journal:Delete ";
    return Parent->xDelete(Parent, Name, DirectorySync);
}
void Initialize()
{
    Check(std::strcmp(sqlite3_libversion(), "3.53.4") == 0, "SQLite pin");
    Check(sqlite3_initialize() == SQLITE_OK, "initialize");
    Check(!sqlite3_compileoption_used("NO_SYNC") && !sqlite3_compileoption_used("DISABLE_DIRSYNC"), "unsafe SQLite build");
    Parent = sqlite3_vfs_find(nullptr);
    Check(Parent && Parent->iVersion >= 3, "VFS version");
    Tracer = *Parent; Tracer.zName = "carbonluau-mode-probe"; Tracer.pNext = nullptr;
    Tracer.szOsFile = sizeof(File) + Parent->szOsFile; Tracer.xOpen = Open; Tracer.xDelete = Delete;
    Check(sqlite3_vfs_register(&Tracer, 0) == SQLITE_OK, "register trace");
}
void Exec(sqlite3* Database, const std::string& Sql)
{ Check(sqlite3_exec(Database, Sql.c_str(), nullptr, nullptr, nullptr) == SQLITE_OK, sqlite3_errmsg(Database)); }
std::string Scalar(sqlite3* Database, const char* Sql)
{
    sqlite3_stmt* Statement = nullptr;
    Check(sqlite3_prepare_v2(Database, Sql, -1, &Statement, nullptr) == SQLITE_OK, "prepare");
    const int Result = sqlite3_step(Statement);
    const unsigned char* Text = Result == SQLITE_ROW ? sqlite3_column_text(Statement, 0) : nullptr;
    const std::string Value = Text ? reinterpret_cast<const char*>(Text) : "";
    sqlite3_finalize(Statement); Check(Result == SQLITE_ROW, "scalar"); return Value;
}
struct Database {
    sqlite3* Handle = nullptr;
    Database(const std::filesystem::path& Path, const std::string& Mode, int Level, bool Create)
    {
        Check(Mode == "DELETE" || Mode == "TRUNCATE" || Mode == "PERSIST" || Mode == "WAL", "mode");
        Check(Level >= 1 && Level <= 3, "sync level");
        Check(sqlite3_open_v2(Path.string().c_str(), &Handle, SQLITE_OPEN_READWRITE |
            (Create ? SQLITE_OPEN_CREATE : 0), Tracer.zName) == SQLITE_OK, "open");
        Exec(Handle, "PRAGMA page_size=4096; PRAGMA cache_size=-4096; PRAGMA mmap_size=0;"
            "PRAGMA temp_store=MEMORY; PRAGMA wal_autocheckpoint=0; PRAGMA journal_size_limit=-1;");
        std::string Lower = Mode; std::transform(Lower.begin(), Lower.end(), Lower.begin(), [](char C) { return char(C + ('a' - 'A')); });
        const auto Actual = Scalar(Handle, ("PRAGMA journal_mode=" + Mode).c_str());
        Check(Actual == Lower, "effective journal mode");
        Exec(Handle, "PRAGMA synchronous=" + std::to_string(Level));
        Check(Scalar(Handle, "PRAGMA synchronous") == std::to_string(Level), "effective sync");
        if (Create) {
            Exec(Handle, "CREATE TABLE Record(Key INTEGER PRIMARY KEY, Value BLOB);"
                "CREATE TABLE Quota(Bytes INTEGER NOT NULL); BEGIN IMMEDIATE;"
                "INSERT INTO Record VALUES(1,zeroblob(1000)); INSERT INTO Quota VALUES(1000); COMMIT;");
        }
    }
    ~Database() { if (Handle) sqlite3_close(Handle); }
};
void BeginMutation(sqlite3* Db, bool Remove, int Bytes)
{
    Point("before-transaction"); Exec(Db, "BEGIN IMMEDIATE"); Point("after-begin");
    Exec(Db, Remove ? "DELETE FROM Record WHERE Key=1" :
        "INSERT OR REPLACE INTO Record VALUES(1,zeroblob(" + std::to_string(Bytes) + "))");
    Point("after-key");
    Exec(Db, "UPDATE Quota SET Bytes=(SELECT coalesce(sum(length(Value)),0) FROM Record)");
    Point("after-quota"); Point("before-commit");
}
int Commit(sqlite3* Db)
{
    Marker = false; WalMarker = false; InCommit = true;
    const int Result = sqlite3_exec(Db, "COMMIT", nullptr, nullptr, nullptr);
    InCommit = false;
    return Result;
}
int Verify(const std::filesystem::path& Path, const std::string& Mode, int Level)
{
    Database Db(Path, Mode, Level, false);
    Check(Scalar(Db.Handle, "PRAGMA integrity_check") == "ok", "integrity");
    Check(Scalar(Db.Handle, "SELECT Bytes=(SELECT coalesce(sum(length(Value)),0) FROM Record) FROM Quota") == "1", "torn key/quota");
    return std::stoi(Scalar(Db.Handle, "SELECT Bytes FROM Quota"));
}
int Child(int Count, char** Args)
{
    Check(Count == 7, "child arguments");
    Database Db(Args[2], Args[3], std::stoi(Args[4]), false);
    StopAt = Args[5];
    BeginMutation(Db.Handle, std::string(Args[6]) == "remove", 65536);
    const int Result = Commit(Db.Handle);
    if (StopAt == "fault-marker-sync") {
        Check(FaultHit && Result != SQLITE_OK, "flush failure falsely succeeded");
        return 50;
    }
    Check(Result == SQLITE_OK, "child commit");
    Point("after-commit"); Point("before-ack");
    Point("after-ack"); // conceptual result boundary; not an implemented IPC callback
    throw std::runtime_error("requested kill point was not reached");
}
int Spawn(const std::filesystem::path& Exe, const std::vector<std::string>& Args)
{
#ifdef _WIN32
    std::string Command = "\"" + Exe.string() + "\"";
    for (const auto& Arg : Args) { Check(Arg.find('"') == std::string::npos, "quoted path"); Command += " \"" + Arg + "\""; }
    STARTUPINFOA Start{}; Start.cb = sizeof(Start); PROCESS_INFORMATION Process{};
    Check(CreateProcessA(nullptr, &Command[0], nullptr, nullptr, FALSE, CREATE_NO_WINDOW,
        nullptr, nullptr, &Start, &Process) != 0, "launch child");
    const DWORD Wait = WaitForSingleObject(Process.hProcess, 10000);
    if (Wait != WAIT_OBJECT_0) TerminateProcess(Process.hProcess, 99);
    DWORD Status = 99; GetExitCodeProcess(Process.hProcess, &Status);
    CloseHandle(Process.hThread); CloseHandle(Process.hProcess);
    Check(Wait == WAIT_OBJECT_0, "child timeout"); return int(Status);
#else
    std::vector<std::string> Storage{Exe.string()}; Storage.insert(Storage.end(), Args.begin(), Args.end());
    std::vector<char*> Pointers; for (auto& Arg : Storage) Pointers.push_back(&Arg[0]); Pointers.push_back(nullptr);
    const pid_t Pid = fork(); Check(Pid >= 0, "fork");
    if (Pid == 0) { execv(Exe.c_str(), Pointers.data()); _exit(99); }
    int Status = 0; const auto End = Clock::now() + std::chrono::seconds(10);
    while (waitpid(Pid, &Status, WNOHANG) == 0) {
        if (Clock::now() > End) { kill(Pid, SIGKILL); waitpid(Pid, &Status, 0); throw std::runtime_error("child timeout"); }
        std::this_thread::sleep_for(std::chrono::milliseconds(2));
    }
    return WIFSIGNALED(Status) && WTERMSIG(Status) == SIGKILL ? 77 : WIFEXITED(Status) ? WEXITSTATUS(Status) : 99;
#endif
}
void CrashMatrix(const std::filesystem::path& Root, const std::filesystem::path& Exe, const std::string& Mode, int Level)
{
    std::vector<std::string> Points{"before-transaction", "after-begin", "after-key", "after-quota",
        "before-commit", "during-commit", "after-commit", "before-ack", "after-ack"};
    if (Mode == "WAL") Points.push_back("wal-sync");
    else {
        Points.push_back("database-sync");
        if (Mode != "DELETE") { Points.push_back("marker-write"); Points.push_back("marker-sync"); Points.push_back("fault-marker-sync"); }
    }
    int Cases = 0;
    for (const bool Remove : {false, true}) for (const auto& Stage : Points) {
        const auto Folder = Root / (Mode + std::to_string(Level) + "-" + Stage + (Remove ? "-remove" : "-set"));
        Check(std::filesystem::create_directory(Folder), "fresh crash folder");
        const auto Path = Folder / "store.sqlite3";
        { Database Db(Path, Mode, Level, true); }
        const int Exit = Spawn(Exe, {"child", Path.string(), Mode, std::to_string(Level), Stage, Remove ? "remove" : "set"});
        Check(Exit == (Stage == "fault-marker-sync" ? 50 : 77), "child did not reach kill/fault");
        const int Bytes = Verify(Path, Mode, Level);
        const bool After = Stage == "after-commit" || Stage == "before-ack" || Stage == "after-ack" || Stage == "marker-sync" || Stage == "wal-sync";
        const bool Before = Stage == "before-transaction" || Stage == "after-begin" || Stage == "after-key" || Stage == "after-quota" || Stage == "before-commit" || Stage == "database-sync";
        const int NewBytes = Remove ? 0 : 65536;
        Check(Bytes == 1000 || Bytes == NewBytes, "neither complete old nor new state");
        if (After) Check(Bytes == NewBytes, "acknowledged/marker-synced value lost");
        if (Before) Check(Bytes == 1000, "uncommitted state recovered");
        ++Cases;
    }
    std::printf("{\"CrashMatrix\":\"%s\",\"Sync\":%d,\"Cases\":%d,\"Result\":\"PASS\"}\n", Mode.c_str(), Level, Cases);
}
unsigned long long Bytes(const std::filesystem::path& Path)
{ return std::filesystem::exists(Path) ? std::filesystem::file_size(Path) : 0; }
void Benchmark(const std::filesystem::path& Root, const std::string& Mode, int Level)
{
    const auto Folder = Root / (Mode + std::to_string(Level) + "-benchmark");
    Check(std::filesystem::create_directory(Folder), "fresh benchmark"); const auto Path = Folder / "store.sqlite3";
    Database Db(Path, Mode, Level, true);
    Events.clear(); Record = true; DatabaseSyncs = JournalMarkerSyncs = WalSyncs = 0;
    BeginMutation(Db.Handle, false, 65536); Check(Commit(Db.Handle) == SQLITE_OK, "trace commit"); Record = false;
    std::printf("{\"Trace\":\"%s\",\"Sync\":%d,\"Events\":\"%s\"}\n", Mode.c_str(), Level, Events.c_str());
    if (Mode == "PERSIST" || Mode == "TRUNCATE") Check(DatabaseSyncs == 1 && JournalMarkerSyncs == 1, "missing durable marker sync");
    if (Mode == "WAL") Check(DatabaseSyncs == 0 && WalSyncs == (Level >= 2 ? 1 : 0), "WAL commit sync mismatch");
    for (int Operation = 0; Operation < 3; ++Operation) {
        std::vector<double> Times;
        for (int Index = 0; Index < 32; ++Index) {
            if (Operation == 2) { BeginMutation(Db.Handle, false, 65536); Check(Commit(Db.Handle) == SQLITE_OK, "remove preparation"); }
            const auto Start = Clock::now();
            BeginMutation(Db.Handle, Operation == 2, Operation == 0 ? 1024 : 65536);
            Check(Commit(Db.Handle) == SQLITE_OK, "benchmark commit");
            Times.push_back(std::chrono::duration<double, std::milli>(Clock::now() - Start).count());
        }
        std::sort(Times.begin(), Times.end());
        std::printf("{\"Benchmark\":\"%s\",\"Sync\":%d,\"Operation\":\"%s\",\"Samples\":32,\"MedianMs\":%.4f,\"P95Ms\":%.4f}\n",
            Mode.c_str(), Level, Operation == 0 ? "Set1KiB" : Operation == 1 ? "Set64KiB" : "Remove64KiB", Times[16], Times[30]);
    }
    std::printf("{\"Files\":\"%s\",\"Sync\":%d,\"Database\":%llu,\"Journal\":%llu,\"Wal\":%llu,\"Shm\":%llu}\n",
        Mode.c_str(), Level, Bytes(Path), Bytes(Path.string()+"-journal"), Bytes(Path.string()+"-wal"), Bytes(Path.string()+"-shm"));
    if (Mode == "WAL") {
        Record = true; Events.clear();
        int Frames = 0, Done = 0;
        Check(sqlite3_wal_checkpoint_v2(Db.Handle, nullptr, SQLITE_CHECKPOINT_TRUNCATE, &Frames, &Done) == SQLITE_OK, "checkpoint");
        Record = false;
        std::printf("{\"Checkpoint\":\"WAL\",\"Sync\":%d,\"WalBytesAfter\":%llu,\"Events\":\"%s\"}\n", Level, Bytes(Path.string()+"-wal"), Events.c_str());
    }
    Check(Scalar(Db.Handle, "SELECT Bytes=(SELECT coalesce(sum(length(Value)),0) FROM Record) FROM Quota") == "1", "benchmark quota");
}
} // namespace

int main(int Count, char** Args)
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
    try {
        Initialize();
        if (Count > 1 && std::string(Args[1]) == "child") return Child(Count, Args);
        Check(Count == 1, "run in an empty task-owned working directory; no production path argument");
        const auto Root = std::filesystem::current_path() / ("modes-" + std::to_string(Clock::now().time_since_epoch().count()));
        Check(std::filesystem::create_directory(Root), "fresh root");
        std::printf("{\"SQLite\":\"%s\",\"Source\":\"%s\",\"Vfs\":\"%s\"}\n", sqlite3_libversion(), sqlite3_sourceid(), Parent->zName);
        for (const std::string Mode : {"DELETE", "TRUNCATE", "PERSIST", "WAL"}) for (int Level : {2, 3}) {
            Benchmark(Root, Mode, Level);
            CrashMatrix(Root, std::filesystem::absolute(Args[0]), Mode, Level);
        }
        Benchmark(Root, "WAL", 1); // Diagnostic comparator only, not a recommended durability mode.
        std::printf("[CarbonLuau:Persistence] Mode investigation PASS; peak journal write=%lld WAL write=%lld. No power-cut evidence.\n",
            static_cast<long long>(PeakJournalWrite), static_cast<long long>(PeakWalWrite));
        return 0;
    } catch (const std::exception& Error) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Mode probe failed: %s\n", Error.what()); return 1;
    }
}
