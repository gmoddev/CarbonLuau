// Research-only startup reachability, not a production VFS or WAL adoption.
#define main HistoricalModeMain
#include "ModeProbe.cpp"
#undef main
#include "Backend.hpp"
#include <fstream>
#include <iostream>
#ifndef _WIN32
#include <sys/stat.h>
#endif
namespace P = CarbonLuau::Persistence;
namespace {
sqlite3_io_methods StartupMethods{};
unsigned WalOpens = 0, ShmMaps = 0, Hints = 0;
std::filesystem::path ActivePath;
bool Recording = false, ObservationFailed = false;
uint64_t PeakAllocation = 0, PeakWal = 0, PeakShm = 0, PeakJournal = 0;
void ObserveStartup() noexcept
{
    if (!Recording) return;
    try {
        uint64_t Total = 0;
        for (const auto& Item : std::filesystem::directory_iterator(ActivePath)) {
            const auto Name = Item.path().filename().string(); const auto Length = Item.file_size();
            if (Name == "store.sqlite3-wal") PeakWal = std::max(PeakWal, Length);
            else if (Name == "store.sqlite3-shm") PeakShm = std::max(PeakShm, Length);
            else if (Name == "store.sqlite3-journal") PeakJournal = std::max(PeakJournal, Length);
            else Check(Name == "store.sqlite3", "unexpected startup file");
#ifdef _WIN32
            HANDLE Handle = CreateFileW(Item.path().c_str(), FILE_READ_ATTRIBUTES,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
            Check(Handle != INVALID_HANDLE_VALUE, "startup allocation handle"); FILE_STANDARD_INFO Info{};
            const bool Good = GetFileInformationByHandleEx(Handle, FileStandardInfo, &Info, sizeof(Info)) != 0;
            CloseHandle(Handle); Check(Good && Info.AllocationSize.QuadPart >= 0, "startup allocation");
            Total += uint64_t(Info.AllocationSize.QuadPart);
#else
            struct stat Info{}; Check(lstat(Item.path().c_str(), &Info) == 0 && Info.st_blocks >= 0, "startup stat");
            Total += uint64_t(Info.st_blocks) * 512;
#endif
        }
        PeakAllocation = std::max(PeakAllocation, Total);
    } catch (...) { ObservationFailed = true; }
}
int StartupShm(sqlite3_file* Item, int Page, int Bytes, int Extend, void volatile** Out)
{ ++ShmMaps; const int Rc = ShmMap(Item, Page, Bytes, Extend, Out); ObserveStartup(); return Rc; }
int StartupControl(sqlite3_file* Item, int Op, void* Arg)
{ if (Op == SQLITE_FCNTL_SIZE_HINT) ++Hints; const int Rc = Control(Item, Op, Arg); ObserveStartup(); return Rc; }
int StartupWrite(sqlite3_file* Item, const void* Bytes, int Count, sqlite3_int64 Offset)
{ const int Rc = Write(Item, Bytes, Count, Offset); ObserveStartup(); return Rc; }
int StartupSync(sqlite3_file* Item, int Flags)
{ const int Rc = Sync(Item, Flags); ObserveStartup(); return Rc; }
int StartupTruncate(sqlite3_file* Item, sqlite3_int64 Length)
{ const int Rc = Truncate(Item, Length); ObserveStartup(); return Rc; }
int StartupOpen(sqlite3_vfs* Vfs, const char* Name, sqlite3_file* Item, int Flags, int* Actual)
{
    const int Rc = Open(Vfs, Name, Item, Flags, Actual);
    if (Rc == SQLITE_OK) { Item->pMethods = &StartupMethods; if (Flags & SQLITE_OPEN_WAL) ++WalOpens; }
    ObserveStartup(); return Rc;
}
}
int main()
{
    try {
#ifdef _WIN32
        SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
        Initialize(); StartupMethods = Methods; StartupMethods.xShmMap = StartupShm;
        StartupMethods.xFileControl = StartupControl; Tracer.xOpen = StartupOpen;
        StartupMethods.xWrite = StartupWrite; StartupMethods.xSync = StartupSync; StartupMethods.xTruncate = StartupTruncate;
        Check(sqlite3_vfs_register(&Tracer, 1) == SQLITE_OK, "startup trace default");
        auto Path = std::filesystem::current_path() / ("allocation-startup-" + std::to_string(Clock::now().time_since_epoch().count()));
        Check(std::filesystem::create_directory(Path), "startup fixture folder");
        { P::Backend Store(Path, P::Clock::now() + std::chrono::seconds(30)); }
        // The pre-fix investigation used PRAGMA journal_mode=WAL via the stock
        // WAL-capable build. Current production excludes WAL: construct the same
        // closed, checkpointed header-only fixture offline, not a runtime path.
        {
            std::fstream File(Path / "store.sqlite3", std::ios::binary | std::ios::in | std::ios::out);
            File.seekp(18); const char Versions[] = {2, 2};
            Check(bool(File.write(Versions, 2)), "offline WAL header fixture");
        }
        Check(!std::filesystem::exists(Path / "store.sqlite3-wal") && !std::filesystem::exists(Path / "store.sqlite3-shm"), "WAL header-only fixture");
        WalOpens = ShmMaps = Hints = 0; bool Ready = false; unsigned Error = 0; ActivePath = Path; Recording = true;
        try { P::Backend Store(Path, P::Clock::now() + std::chrono::seconds(30)); Ready = Store.Available(); }
        catch (const P::Failure& Failure) { Error = unsigned(Failure.Code); }
        ObserveStartup(); Check(!ObservationFailed, "startup observations failed");
        Check(!Ready && Error && !WalOpens && !ShmMaps, "inherited WAL must fail before auxiliary use");
        std::cout << "[CarbonLuau:Allocation] inherited-WAL ready=" << Ready << " error=" << Error
            << " wal-opens=" << WalOpens << " shm-map-calls=" << ShmMaps << " size-hints=" << Hints
            << " wal-eof=" << PeakWal << " shm-eof=" << PeakShm << " journal-eof=" << PeakJournal
            << " allocated-peak=" << PeakAllocation << "; retained=" << Path.string() << '\n';
        std::cout << "[CarbonLuau:Allocation] startup observation complete; not production qualification\n";
        return 0;
    } catch (const P::Failure& Error) { std::fprintf(stderr, "[CarbonLuau:Allocation] startup backend error=%u\n", unsigned(Error.Code)); }
      catch (const std::exception& Error) { std::fprintf(stderr, "[CarbonLuau:Allocation] startup probe: %s\n", Error.what()); }
    return 1;
}
