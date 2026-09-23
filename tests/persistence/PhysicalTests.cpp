// Test-only transparent VFS observation. The production Backend and stock VFS
// still perform every operation. This is measured envelope evidence, not a proof
// that all NTFS/ext4 allocation geometries have the same physical overhead.
//
// Source proof map, sqlite3.c SHA256
// b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189:
// - 65567-65570: begin journal at offset zero; 65770-65775: journal only an
//   unjournaled original page; 65711-65714: one page record is pageSize+8 bytes.
// - 64317-64321: cache_spill=OFF suppresses the path that would call
//   syncJournal(...,1) at 64345; commit uses (...,0) at 66293. Thus one header,
//   not one header per cache flush. Header sector <=65536 (60050,62392-62404).
// - 64015-64019: stale next-header invalidation cannot extend the old file,
//   since its one-byte write requires an existing successful eight-byte read.
// - 61034-61064,61736-61740: PERSIST/-1 invalidates the first 28 bytes, resets
//   journalOff and retains existing length. Repeated transactions do not append.
// - 62586-62594: only first recovery header sets dbSize/mxPgno; 61987-61989:
//   every replay page above that dbSize is skipped. Later headers cannot enlarge
//   the DB even though their original-size fields are parsed. Backend preflight
//   must bound the first header before any SQL statement triggers recovery.
// - 44247,52419: stock Unix/Windows SIZE_HINT preallocation requires szChunk>0;
//   production sets no chunk size. 65272-65278 enforces normal page growth cap.
//
// Scope caveats: normal no-auto-vacuum workload, no external DB editing,
// verified stock VFS, fixed SQL/no application savepoints/ATTACH. Existing files'
// auto_vacuum setting is inherited from their headers (75956-75957), not fixed by
// page_size or schema validation. Do not generalize this proof to changed
// settings: page movement's fault path can clear pInJournal (66946-66960).
// AllocationSize/st_blocks are separate observations from SQLite byte offsets;
// qualify filesystem allocation geometry and overhead independently. Ext4
// statvfs block size alone does not establish its bigalloc cluster size.
#define main HistoricalModeProbeMain
#include "ModeProbe.cpp"
#undef main
#include "Backend.hpp"
#include <fstream>
#include <iostream>
#ifndef _WIN32
#include <sys/stat.h>
#include <sys/statvfs.h>
#endif
namespace P = CarbonLuau::Persistence;
namespace {
constexpr uint64_t DatabaseLimit = 131072ull * 4096;
constexpr uint64_t JournalLimit = 131072ull * (4096 + 8) + 65536;
constexpr uint64_t OperationalBudget = 1280ull * 1024 * 1024;
std::filesystem::path ActiveDirectory;
sqlite3_io_methods PhysicalMethods{};
uint64_t PeakDatabaseExtent = 0, PeakJournalExtent = 0, PeakAllocation = 0;
uint64_t Writes = 0, Checks = 0, OtherOpens = 0;
bool ObservationFailed = false;

uint64_t Allocation(const std::filesystem::path& Path)
{
#ifdef _WIN32
    HANDLE Handle = CreateFileW(Path.c_str(), FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
        OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr);
    Check(Handle != INVALID_HANDLE_VALUE, "allocation open");
    FILE_STANDARD_INFO Info{};
    const bool Good = GetFileInformationByHandleEx(Handle, FileStandardInfo, &Info, sizeof(Info)) != 0;
    CloseHandle(Handle);
    Check(Good && Info.AllocationSize.QuadPart >= 0, "allocation query");
    return uint64_t(Info.AllocationSize.QuadPart);
#else
    struct stat Info{};
    Check(lstat(Path.c_str(), &Info) == 0 && Info.st_blocks >= 0, "allocation query");
    return uint64_t(Info.st_blocks) * 512;
#endif
}
void Observe() noexcept
{
    // Never throw through SQLite's C callbacks; record an observation failure
    // and let the untouched production operation return before asserting it.
    try {
        uint64_t Total = 0;
        for (const auto& Entry : std::filesystem::directory_iterator(ActiveDirectory)) {
            Check(Entry.is_regular_file(), "unexpected fixture directory entry");
            Total += Allocation(Entry.path());
            const auto Name = Entry.path().filename().string();
            Check(Name == "store.sqlite3" || Name == "store.sqlite3-journal", "unexpected SQLite side file");
            Check(Entry.file_size() <= (Name == "store.sqlite3" ? DatabaseLimit : JournalLimit), "file length ceiling");
        }
        PeakAllocation = std::max(PeakAllocation, Total); ++Checks;
        Check(Total <= OperationalBudget, "observed operational allocation budget");
    } catch (...) { ObservationFailed = true; }
}
int PhysicalWrite(sqlite3_file* Handle, const void* Data, int Count, sqlite3_int64 Offset)
{
    auto* Item = Cast(Handle);
    if (Count < 0 || Offset < 0 || uint64_t(Offset) > UINT64_MAX - uint64_t(Count)) ObservationFailed = true;
    else {
        const auto Extent = uint64_t(Offset) + uint64_t(Count);
        if (Item->Kind[0] == 'D') {
            PeakDatabaseExtent = std::max(PeakDatabaseExtent, Extent);
            if (Extent > DatabaseLimit) ObservationFailed = true;
        } else if (Item->Kind[0] == 'J') {
            PeakJournalExtent = std::max(PeakJournalExtent, Extent);
            if (Extent > JournalLimit) ObservationFailed = true;
        }
    }
    ++Writes;
    const int Result = Write(Handle, Data, Count, Offset); Observe(); return Result;
}
int PhysicalTruncate(sqlite3_file* Handle, sqlite3_int64 Count)
{ const int Result = Truncate(Handle, Count); Observe(); return Result; }
int PhysicalSync(sqlite3_file* Handle, int Flags)
{ const int Result = Sync(Handle, Flags); Observe(); return Result; }
int PhysicalOpen(sqlite3_vfs* Vfs, const char* Name, sqlite3_file* Handle, int Flags, int* Actual)
{
    const int Result = Open(Vfs, Name, Handle, Flags, Actual);
    if (Result == SQLITE_OK) {
        Handle->pMethods = &PhysicalMethods;
        if (Cast(Handle)->Kind[0] != 'D' && Cast(Handle)->Kind[0] != 'J') ++OtherOpens;
    }
    return Result;
}
P::Deadline Until() { return P::Clock::now() + std::chrono::seconds(5); }
P::Identity Key(unsigned Index) { return {false, "", "Physical", std::to_string(Index)}; }
P::Bytes Payload(const P::Identity& Id, unsigned Round)
{
    P::Value Value; Value.Type = P::Kind::Array;
    for (unsigned Index = 0; Index < 4; ++Index) {
        auto Part = std::make_shared<P::Value>(); Part->Type = P::Kind::String;
        Part->String.assign(Round % 2 ? 8192 : 16300, char('a' + Round % 26));
        Value.Array.push_back(Part);
    }
    return P::Encode(Id, Value, Until());
}
void Good(const P::Result& Result)
{ Check(Result.Code == P::Error::None, "production backend operation"); Check(!ObservationFailed, "physical trace violation"); }
void PutBig32(P::Bytes& Data, size_t Offset, uint32_t Value)
{ for (unsigned Index = 0; Index < 4; ++Index) Data.at(Offset + Index) = uint8_t(Value >> (24 - 8 * Index)); }
void Header(P::Bytes& Data, size_t Offset, uint32_t Count, uint32_t Original)
{
    const uint8_t Magic[] = {0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7};
    std::copy(std::begin(Magic), std::end(Magic), Data.begin() + Offset);
    PutBig32(Data, Offset + 8, Count); PutBig32(Data, Offset + 16, Original);
    PutBig32(Data, Offset + 20, 512); PutBig32(Data, Offset + 24, 4096);
}
void RecoveryPageBounds(bool SecondHeader)
{
    const auto DatabasePath = ActiveDirectory / "store.sqlite3";
    const uint64_t Before = std::filesystem::file_size(DatabasePath);
    Check(Before % 4096 == 0 && Before <= DatabaseLimit, "fixture database pages");
    // Synthetic offline journal, not a production-created crash state. The
    // first header is valid. An oversized record page must not extend the DB;
    // a later header's original-size field must not replace the first header.
    const size_t RecordOffset = SecondHeader ? 1024 : 512;
    P::Bytes Journal(RecordOffset + 4096 + 8, 0);
    Header(Journal, 0, SecondHeader ? 0 : 1, uint32_t(Before / 4096));
    if (SecondHeader) Header(Journal, 512, 1, 0xffffffffu);
    PutBig32(Journal, RecordOffset, 131073);
    {
        std::ofstream File(ActiveDirectory / "store.sqlite3-journal", std::ios::binary | std::ios::trunc);
        File.write(reinterpret_cast<const char*>(Journal.data()), std::streamsize(Journal.size()));
        Check(bool(File), "synthetic journal write");
    }
    { P::Backend Store(ActiveDirectory, Until()); Good(Store.Execute(P::Operation::Get, Key(0), {}, Until())); }
    Check(std::filesystem::file_size(DatabasePath) == Before, "recovery grew database from ignored page/header");
    Check(!ObservationFailed, "recovery physical trace");
}
}
int main()
{
    try {
        // Unique retained evidence directory; no broad deletion or modification
        // of any user/production database. Main chooses the qualified volume.
        ActiveDirectory = std::filesystem::current_path() /
            ("physical-" + std::to_string(P::Clock::now().time_since_epoch().count()));
        Check(std::filesystem::create_directory(ActiveDirectory), "fixture directory");
        Initialize(); PhysicalMethods = Methods;
        PhysicalMethods.xWrite = PhysicalWrite; PhysicalMethods.xTruncate = PhysicalTruncate;
        PhysicalMethods.xSync = PhysicalSync; Tracer.xOpen = PhysicalOpen;
        Check(sqlite3_vfs_register(&Tracer, 1) == SQLITE_OK, "default observation VFS");
#ifdef _WIN32
        wchar_t Volume[MAX_PATH]{}; DWORD Sectors = 0, Bytes = 0, Free = 0, Total = 0;
        Check(GetVolumePathNameW(ActiveDirectory.c_str(), Volume, MAX_PATH) &&
            GetDiskFreeSpaceW(Volume, &Sectors, &Bytes, &Free, &Total), "volume allocation geometry");
        std::cout << "[CarbonLuau:Persistence] Physical geometry cluster=" << uint64_t(Sectors) * Bytes << " bytes\n";
#else
        struct statvfs Geometry{}; Check(statvfs(ActiveDirectory.c_str(), &Geometry) == 0, "volume geometry");
        std::cout << "[CarbonLuau:Persistence] Physical geometry block=" << uint64_t(Geometry.f_bsize)
            << " fragment=" << uint64_t(Geometry.f_frsize) << " (not a bigalloc-cluster proof)\n";
#endif
        for (unsigned Round = 0; Round < 4; ++Round) {
            P::Backend Store(ActiveDirectory, P::Clock::now() + std::chrono::seconds(30));
            for (unsigned Index = 0; Index < 192; ++Index) {
                const auto Id = Key(Index);
                if (Round) Good(Store.Execute(P::Operation::Remove, Id, {}, Until()));
                Good(Store.Execute(P::Operation::Set, Id, Payload(Id, Round), Until()));
            }
            Observe(); Check(!ObservationFailed, "round physical envelope");
        }
        RecoveryPageBounds(false); RecoveryPageBounds(true);
        Check(OtherOpens == 0 && Writes > 0 && Checks > 0 && PeakJournalExtent > 0, "observed fixed-file workload");
        std::cout << "[CarbonLuau:Persistence] Physical fixture PASS writes=" << Writes << " checks=" << Checks
            << " db-extent=" << PeakDatabaseExtent << " journal-extent=" << PeakJournalExtent
            << " allocation-peak=" << PeakAllocation << "; limits=" << DatabaseLimit << '/' << JournalLimit
            << "; operational-budget=" << OperationalBudget << "; retained=" << ActiveDirectory.u8string() << '\n';
        return 0;
    } catch (const P::Failure& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Physical fixture failure code=%u\n", unsigned(Problem.Code));
    } catch (const std::exception& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Physical fixture failure: %s\n", Problem.what());
    }
    return 1;
}
