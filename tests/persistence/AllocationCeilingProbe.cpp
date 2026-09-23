// Research only: stock SQLite/VFS with observation, not a production quota VFS.
#define main HistoricalModeMain
#include "ModeProbe.cpp"
#undef main
#include "Backend.hpp"
#include <iomanip>
#include <iostream>
#include <sstream>
#ifndef _WIN32
#include <sys/stat.h>
#endif
namespace P = CarbonLuau::Persistence;
namespace {
std::filesystem::path Observed;
sqlite3_io_methods ObservingMethods{};
uint64_t PeakAllocated = 0, PeakDb = 0, PeakJournal = 0, Observations = 0, AuxiliaryOpens = 0;
bool BadObservation = false;
constexpr uint64_t DbBound = 131072ull * 4096, JournalBound = 65536ull + 131072ull * 4104;
uint64_t Allocated(const std::filesystem::path& Path)
{
#ifdef _WIN32
    HANDLE Item = CreateFileW(Path.c_str(), FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
    Check(Item != INVALID_HANDLE_VALUE, "allocation handle"); FILE_STANDARD_INFO Info{};
    const bool Good = GetFileInformationByHandleEx(Item, FileStandardInfo, &Info, sizeof(Info)) != 0;
    CloseHandle(Item); Check(Good && Info.AllocationSize.QuadPart >= 0, "allocation info");
    return uint64_t(Info.AllocationSize.QuadPart);
#else
    struct stat Info{}; Check(lstat(Path.c_str(), &Info) == 0 && Info.st_blocks >= 0, "allocation stat");
    return uint64_t(Info.st_blocks) * 512;
#endif
}
void Observe() noexcept
{
    try {
        uint64_t Total = 0;
        for (const auto& Item : std::filesystem::directory_iterator(Observed)) {
            Check(Item.is_regular_file(), "unexpected directory entry");
            const auto Name = Item.path().filename().string();
            Check(Name == "store.sqlite3" || Name == "store.sqlite3-journal", "unexpected side file");
            const auto Length = Item.file_size(); Total += Allocated(Item.path());
            if (Name == "store.sqlite3") { PeakDb = std::max(PeakDb, Length); Check(Length <= DbBound, "DB EOF bound"); }
            else { PeakJournal = std::max(PeakJournal, Length); Check(Length <= JournalBound, "journal EOF bound"); }
        }
        PeakAllocated = std::max(PeakAllocated, Total); ++Observations;
        Check(Total <= 1280ull * 1024 * 1024, "observed physical budget");
    } catch (...) { BadObservation = true; }
}
int ObserveWrite(sqlite3_file* Item, const void* Data, int Count, sqlite3_int64 Offset)
{ const int Rc = Write(Item, Data, Count, Offset); Observe(); return Rc; }
int ObserveSync(sqlite3_file* Item, int Flags)
{ const int Rc = Sync(Item, Flags); Observe(); return Rc; }
int ObserveTruncate(sqlite3_file* Item, sqlite3_int64 Length)
{ const int Rc = Truncate(Item, Length); Observe(); return Rc; }
int ObserveOpen(sqlite3_vfs* Vfs, const char* Name, sqlite3_file* Item, int Flags, int* Actual)
{
    const int Rc = Open(Vfs, Name, Item, Flags, Actual);
    if (Rc == SQLITE_OK) {
        Item->pMethods = &ObservingMethods;
        if (!(Flags & (SQLITE_OPEN_MAIN_DB | SQLITE_OPEN_MAIN_JOURNAL))) ++AuxiliaryOpens;
    }
    return Rc;
}
void Select(const std::filesystem::path& Path)
{ Observed = Path; PeakAllocated = PeakDb = PeakJournal = Observations = AuxiliaryOpens = 0; BadObservation = false; }
void Report(const char* Label)
{
    Observe(); Check(!BadObservation && AuxiliaryOpens == 0, "allocation observation/auxiliary check");
    std::cout << "[CarbonLuau:Allocation] " << Label << " db-eof=" << PeakDb << " journal-eof=" << PeakJournal
        << " allocated-peak=" << PeakAllocated << " observations=" << Observations << " auxiliary-opens=" << AuxiliaryOpens << '\n';
}
struct CapDatabase {
    sqlite3* Handle = nullptr;
    CapDatabase(const std::filesystem::path& Path, unsigned Pages, bool Create)
    {
        Check(sqlite3_open_v2(Path.string().c_str(), &Handle, SQLITE_OPEN_READWRITE |
            (Create ? SQLITE_OPEN_CREATE : 0), Tracer.zName) == SQLITE_OK, "cap open");
        Exec(Handle, "PRAGMA page_size=4096; PRAGMA auto_vacuum=NONE; PRAGMA cache_size=-4096;"
            "PRAGMA cache_spill=OFF; PRAGMA temp_store=MEMORY; PRAGMA mmap_size=0; PRAGMA journal_size_limit=-1");
        Check(Scalar(Handle, "PRAGMA journal_mode=PERSIST") == "persist", "PERSIST");
        Exec(Handle, "PRAGMA synchronous=EXTRA");
        Check(Scalar(Handle, ("PRAGMA max_page_count=" + std::to_string(Pages)).c_str()) == std::to_string(Pages), "page cap");
        Check(Scalar(Handle, "PRAGMA page_size") == "4096" && Scalar(Handle, "PRAGMA auto_vacuum") == "0", "page geometry");
        if (Create) Exec(Handle, "CREATE TABLE R(K INTEGER PRIMARY KEY,V BLOB); CREATE TABLE Q(N INTEGER); INSERT INTO Q VALUES(0)");
    }
    ~CapDatabase() { if (Handle) sqlite3_close(Handle); }
};
void VerifyCap(sqlite3* Db)
{
    Check(Scalar(Db, "PRAGMA integrity_check") == "ok", "cap integrity");
    Check(Scalar(Db, "SELECT N=(SELECT count(*) FROM R) FROM Q") == "1", "cap atomic accounting");
}
// Deliberately toy schema/data: exact backend page-cap mechanics, NOT logical-quota
// or production workload reachability. Inserts require another 4 KiB page.
void FillCap(const std::filesystem::path& Path, unsigned Pages)
{
    Select(Path); CapDatabase Db(Path / "store.sqlite3", Pages, true); Report("empty-cap-db");
    unsigned Batch = 64, Fulls = 0;
    for (;;) {
        const auto Before = Scalar(Db.Handle, "SELECT N FROM Q");
        Exec(Db.Handle, "BEGIN IMMEDIATE; UPDATE Q SET N=N+1"); // rollback must include accounting
        int Rc = SQLITE_OK;
        for (unsigned Index = 0; Index < Batch && Rc == SQLITE_OK; ++Index)
            Rc = sqlite3_exec(Db.Handle, "INSERT INTO R(V) VALUES(zeroblob(4000))", nullptr, nullptr, nullptr);
        if (Rc == SQLITE_FULL) {
            ++Fulls;
            if (!sqlite3_get_autocommit(Db.Handle)) Exec(Db.Handle, "ROLLBACK");
            Check(Scalar(Db.Handle, "SELECT N FROM Q") == Before, "FULL changed accounting");
            VerifyCap(Db.Handle);
            if (Batch == 1) break;
            Batch = 1; continue;
        }
        Check(Rc == SQLITE_OK, "cap insert unexpected failure");
        Exec(Db.Handle, "UPDATE Q SET N=N+" + std::to_string(Batch - 1)); Check(Commit(Db.Handle) == SQLITE_OK, "cap commit");
    }
    Check(Scalar(Db.Handle, "PRAGMA page_count") == std::to_string(Pages), "did not reach exact page cap");
    Check(std::filesystem::file_size(Path / "store.sqlite3") == uint64_t(Pages) * 4096, "exact cap EOF");
    VerifyCap(Db.Handle);
    // At cap, an allocation-free update still works; FULL is not corruption.
    Exec(Db.Handle, "BEGIN IMMEDIATE; UPDATE R SET V=zeroblob(4000) WHERE K=1");
    Check(Commit(Db.Handle) == SQLITE_OK, "healthy after FULL");
    std::cout << "[CarbonLuau:Allocation] exact-pages=" << Pages << " full-rejections=" << Fulls
        << " rows=" << Scalar(Db.Handle, "SELECT N FROM Q") << " one-more-page/block rejected\n";
    Report("exact-cap-and-FULL");
}
int CapChild(int Count, char** Args)
{
    Check(Count == 5, "cap child args"); Select(Args[2]);
    CapDatabase Db(Observed / "store.sqlite3", unsigned(std::stoul(Args[3])), false);
    StopAt = Args[4];
    Exec(Db.Handle, "BEGIN IMMEDIATE; DELETE FROM R WHERE K=1; UPDATE Q SET N=N-1");
    Point("before-commit"); Check(Commit(Db.Handle) == SQLITE_OK, "cap child commit"); Point("after-commit");
    throw std::runtime_error("cap kill point not reached");
}
void CapCrashes(const std::filesystem::path& Source, const std::filesystem::path& Root, const std::filesystem::path& Exe)
{
    const char* Points[] = {"before-commit", "during-commit", "database-sync", "marker-write", "marker-sync", "after-commit"};
    for (const auto* Name : Points) {
        const auto Path = Root / Name; Check(std::filesystem::create_directory(Path), "crash folder");
        std::filesystem::copy_file(Source / "store.sqlite3", Path / "store.sqlite3");
        std::filesystem::copy_file(Source / "store.sqlite3-journal", Path / "store.sqlite3-journal");
        Select(Path); std::string Before;
        { CapDatabase Db(Path / "store.sqlite3", 128, false); Before = Scalar(Db.Handle, "SELECT N FROM Q"); }
        Check(Spawn(Exe, {"cap-child", Path.string(), "128", Name}) == 77, "expected cap crash");
        { CapDatabase Db(Path / "store.sqlite3", 128, false); VerifyCap(Db.Handle);
          const auto After = Scalar(Db.Handle, "SELECT N FROM Q");
          const bool Old = After == Before, New = std::stoll(After) == std::stoll(Before) - 1;
          Check(Old || New, "torn cap crash");
          if (std::string(Name) == "before-commit" || std::string(Name) == "database-sync") Check(Old, "pre-marker state");
          if (std::string(Name) == "marker-sync" || std::string(Name) == "after-commit") Check(New, "post-marker state");
        }
        Report(Name);
    }
}
void ActualCapCrashes(const std::filesystem::path& Path, const std::filesystem::path& Exe)
{
    Select(Path);
    for (const char* Name : {"database-sync", "marker-sync"}) {
        std::string Before;
        { CapDatabase Db(Path / "store.sqlite3", 131072, false); Before = Scalar(Db.Handle, "SELECT N FROM Q"); }
        Check(Spawn(Exe, {"cap-child", Path.string(), "131072", Name}) == 77, "actual-cap crash");
        { CapDatabase Db(Path / "store.sqlite3", 131072, false); VerifyCap(Db.Handle);
          const auto After = Scalar(Db.Handle, "SELECT N FROM Q");
          Check(std::string(Name) == "database-sync" ? After == Before : std::stoll(After) == std::stoll(Before) - 1,
              "actual-cap recovery state"); }
        Report(Name);
    }
}
P::Deadline End() { return P::Clock::now() + std::chrono::seconds(5); }
P::Identity Identity(unsigned Ns, unsigned Index)
{
    std::ostringstream Key; Key << std::setw(4) << std::setfill('0') << Index;
    return {Ns != 0, Ns ? "addon" + std::to_string(Ns) : "", "S", Key.str()};
}
P::Bytes Sized(const P::Identity& Id, size_t Length)
{
    P::Value Value; Value.Type = P::Kind::Array; size_t Remaining = Length - 69;
    for (unsigned Index = 0; Index < 4; ++Index) {
        auto Part = std::make_shared<P::Value>(); Part->Type = P::Kind::String;
        const size_t Count = std::min<size_t>(16384, Remaining); Part->String.assign(Count, 'x'); Remaining -= Count; Value.Array.push_back(Part);
    }
    Check(Remaining == 0, "payload range"); auto Bytes = P::Encode(Id, Value, End()); Check(Bytes.size() == Length, "payload size"); return Bytes;
}
void Put(P::Backend& Db, unsigned Ns, unsigned Index, size_t Length, P::Error Expected = P::Error::None)
{
    const auto Id = Identity(Ns, Index); Check(Db.Execute(P::Operation::Set, Id, Sized(Id, Length), End()).Code == Expected, "production set result");
    Check(!BadObservation, "production allocation observation");
}
void ProductionQuota(const std::filesystem::path& Path)
{
    Select(Path);
    {
        P::Backend Db(Path, End()); Report("production-empty");
        for (unsigned Ns = 0; Ns < 16; ++Ns) {
            for (unsigned Index = 0; Index < 256; ++Index) Put(Db, Ns, Index, 65531);
            if (Ns == 14) Report("production-near-global-quota");
        }
        Report("production-exact-256MiB");
        Put(Db, 0, 0, 65532, P::Error::QuotaExceeded); Put(Db, 16, 0, 95, P::Error::QuotaExceeded);
        Put(Db, 0, 0, 65531); Put(Db, 0, 0, 32768); Put(Db, 0, 0, 65531);
        // Bounded 512-key shrink/grow/delete/reinsert cycles keep logical <= cap.
        for (unsigned Round = 0; Round < 2; ++Round) {
            for (unsigned Ns = 0; Ns < 2; ++Ns) for (unsigned Index = 0; Index < 256; ++Index) Put(Db, Ns, Index, 80 + Index);
            for (unsigned Ns = 0; Ns < 2; ++Ns) for (unsigned Index = 0; Index < 256; ++Index) {
                Check(Db.Execute(P::Operation::Remove, Identity(Ns, Index), {}, End()).Code == P::Error::None, "production remove");
                Put(Db, Ns, Index, 65531);
            }
            Report("production-churn-exact-quota");
        }
    }
    { P::Backend Db(Path, P::Clock::now() + std::chrono::seconds(30));
      Check(Db.Execute(P::Operation::Get, Identity(0, 0), {}, End()).Code == P::Error::None, "production quota restart"); }
    Report("production-restart");
}
void HighWater(const std::filesystem::path& Path)
{
    Select(Path); CapDatabase Db(Path / "store.sqlite3", 128, false);
    // Research-only all-page update magnifies journal, not a production operation.
    Exec(Db.Handle, "BEGIN IMMEDIATE; UPDATE R SET V=CAST(replace(hex(zeroblob(2000)),'0','x') AS BLOB)"); Check(Commit(Db.Handle) == SQLITE_OK, "high water commit");
    const auto High = std::filesystem::file_size(Path / "store.sqlite3-journal");
    for (unsigned Index = 0; Index < 8; ++Index) {
        Exec(Db.Handle, "BEGIN IMMEDIATE; UPDATE R SET V=zeroblob(4000) WHERE K=1"); Check(Commit(Db.Handle) == SQLITE_OK, "retention commit");
        Check(std::filesystem::file_size(Path / "store.sqlite3-journal") == High, "journal did not retain high water");
    }
    Exec(Db.Handle, "BEGIN IMMEDIATE; DELETE FROM R WHERE K%2=0; UPDATE Q SET N=(SELECT count(*) FROM R)");
    Check(Commit(Db.Handle) == SQLITE_OK, "freelist commit"); VerifyCap(Db.Handle);
    Check(Scalar(Db.Handle, "PRAGMA page_count") == "128" && std::stoi(Scalar(Db.Handle, "PRAGMA freelist_count")) > 0, "freelist retention");
    Exec(Db.Handle, "BEGIN IMMEDIATE; INSERT INTO R(V) VALUES(zeroblob(4000)); UPDATE Q SET N=N+1");
    Check(Commit(Db.Handle) == SQLITE_OK, "freelist reuse"); VerifyCap(Db.Handle);
    Report("journal-high-water-and-page-reuse");
}
}
int main(int Count, char** Args)
{
    try {
        std::cout << std::unitbuf;
#ifdef _WIN32
        SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
        Initialize(); ObservingMethods = Methods; ObservingMethods.xWrite = ObserveWrite;
        ObservingMethods.xSync = ObserveSync; ObservingMethods.xTruncate = ObserveTruncate; Tracer.xOpen = ObserveOpen;
        Check(sqlite3_vfs_register(&Tracer, 1) == SQLITE_OK, "register observing VFS");
        if (Count > 1 && std::string(Args[1]) == "cap-child") return CapChild(Count, Args);
        const auto Root = std::filesystem::current_path() / ("allocation-ceiling-" + std::to_string(Clock::now().time_since_epoch().count()));
        Check(std::filesystem::create_directory(Root), "research root");
        for (const char* Name : {"small", "actual", "production"}) Check(std::filesystem::create_directory(Root / Name), "research folder");
        FillCap(Root / "small", 128);
        { CapDatabase Db(Root / "small" / "store.sqlite3", 128, false); VerifyCap(Db.Handle); }
        CapCrashes(Root / "small", Root, std::filesystem::absolute(Args[0])); HighWater(Root / "small");
        FillCap(Root / "actual", 131072);
        { CapDatabase Db(Root / "actual" / "store.sqlite3", 131072, false); VerifyCap(Db.Handle); }
        ActualCapCrashes(Root / "actual", std::filesystem::absolute(Args[0]));
        ProductionQuota(Root / "production");
        std::cout << "[CarbonLuau:Allocation] Probe PASS; retained=" << Root.string()
            << "; observation is not hard physical enforcement\n";
        return 0;
    } catch (const P::Failure& Problem) { std::fprintf(stderr, "[CarbonLuau:Allocation] backend failure=%u\n", unsigned(Problem.Code)); }
      catch (const std::exception& Problem) { std::fprintf(stderr, "[CarbonLuau:Allocation] failure=%s\n", Problem.what()); }
    return 1;
}
