// Production Backend admission with the production OMIT_WAL SQLite build.
// Like PhysicalTests.cpp, observe the stock VFS through ModeProbe's transparent
// wrapper. No WAL-capable SQLite, PRAGMA journal_mode=WAL, or child process is
// needed: offline bytes 18/19 = 2/2 model a checkpointed, closed WAL database.
#define main HistoricalWalAdmissionModeMain
#include "ModeProbe.cpp"
#undef main
#include "Backend.hpp"
#include <fstream>

namespace P = CarbonLuau::Persistence;
namespace {
constexpr size_t PageBytes = 4096;
constexpr size_t SectorBytes = 512;
constexpr uint64_t FixtureLimit = 1024 * 1024;
constexpr unsigned CounterLimit = 100000;
sqlite3_io_methods AdmissionMethods{};
struct Observation {
    unsigned DatabaseOpens = 0, JournalOpens = 0, OtherOpens = 0;
    unsigned WalOpens = 0, ShmOpens = 0, ShmCalls = 0;
    unsigned JournalReads = 0, PageOneWrites = 0;
    bool Overflow = false;
} Seen;
std::string ActiveCase = "initialization";

void Count(unsigned& Value) noexcept
{
    if (Value == CounterLimit) Seen.Overflow = true;
    else ++Value;
}
bool Suffix(const char* Name, const char* Ending) noexcept
{
    if (!Name) return false;
    const size_t Length = std::strlen(Name), Tail = std::strlen(Ending);
    return Length >= Tail && std::memcmp(Name + Length - Tail, Ending, Tail) == 0;
}
int AdmissionOpen(sqlite3_vfs* Vfs, const char* Name, sqlite3_file* Handle, int Flags, int* Actual)
{
    // Count attempts before delegation, including failed opens. SHM is usually
    // opened inside the stock VFS's xShmMap, not through the VFS xOpen entry.
    if (Flags & SQLITE_OPEN_MAIN_DB) Count(Seen.DatabaseOpens);
    else if (Flags & SQLITE_OPEN_MAIN_JOURNAL) Count(Seen.JournalOpens);
    else Count(Seen.OtherOpens);
    if ((Flags & SQLITE_OPEN_WAL) || Suffix(Name, "-wal")) Count(Seen.WalOpens);
    if (Suffix(Name, "-shm")) Count(Seen.ShmOpens);
    const int Result = Open(Vfs, Name, Handle, Flags, Actual);
    if (Result == SQLITE_OK) Handle->pMethods = &AdmissionMethods;
    return Result;
}
int AdmissionRead(sqlite3_file* Handle, void* Data, int Size, sqlite3_int64 Offset)
{
    if (Cast(Handle)->Kind[0] == 'J') Count(Seen.JournalReads);
    return Read(Handle, Data, Size, Offset);
}
int AdmissionWrite(sqlite3_file* Handle, const void* Data, int Size, sqlite3_int64 Offset)
{
    const int Result = Write(Handle, Data, Size, Offset);
    if (Result == SQLITE_OK && Cast(Handle)->Kind[0] == 'D' && Offset == 0 && Size == int(PageBytes))
        Count(Seen.PageOneWrites);
    return Result;
}
int AdmissionShmMap(sqlite3_file* Handle, int Page, int Bytes, int Extend, void volatile** Out)
{
    Count(Seen.ShmCalls);
    auto* Real = Cast(Handle)->Real;
    // OMIT_WAL VFS methods may be null. A regression must fail the observation
    // assertion rather than crash in a null callback before it can be reported.
    if (!Real->pMethods->xShmMap) { *Out = nullptr; return SQLITE_IOERR_SHMMAP; }
    return Real->pMethods->xShmMap(Real, Page, Bytes, Extend, Out);
}
int AdmissionShmLock(sqlite3_file* Handle, int Offset, int CountValue, int Flags)
{
    Count(Seen.ShmCalls);
    auto* Real = Cast(Handle)->Real;
    return Real->pMethods->xShmLock ? Real->pMethods->xShmLock(Real, Offset, CountValue, Flags) : SQLITE_IOERR_SHMLOCK;
}
void AdmissionShmBarrier(sqlite3_file* Handle)
{
    Count(Seen.ShmCalls);
    auto* Real = Cast(Handle)->Real;
    if (Real->pMethods->xShmBarrier) Real->pMethods->xShmBarrier(Real);
}
int AdmissionShmUnmap(sqlite3_file* Handle, int DeleteValue)
{
    Count(Seen.ShmCalls);
    auto* Real = Cast(Handle)->Real;
    return Real->pMethods->xShmUnmap ? Real->pMethods->xShmUnmap(Real, DeleteValue) : SQLITE_IOERR;
}

P::Deadline Until() { return P::Clock::now() + std::chrono::seconds(5); }
P::Bytes ReadBytes(const std::filesystem::path& Path)
{
    const auto Length = std::filesystem::file_size(Path);
    Check(Length <= FixtureLimit, "fixture exceeds bounded read");
    std::ifstream Input(Path, std::ios::binary);
    Check(bool(Input), "fixture read open");
    P::Bytes Data(static_cast<size_t>(Length));
    if (Length) Input.read(reinterpret_cast<char*>(Data.data()), std::streamsize(Length));
    Check(bool(Input), "fixture read");
    return Data;
}
void WriteBytes(const std::filesystem::path& Path, const P::Bytes& Data)
{
    Check(Data.size() <= FixtureLimit, "fixture exceeds bounded write");
    std::ofstream Output(Path, std::ios::binary | std::ios::trunc);
    Output.write(reinterpret_cast<const char*>(Data.data()), std::streamsize(Data.size()));
    Output.close();
    Check(bool(Output), "fixture write/close");
}
void PutBig32(P::Bytes& Data, size_t Offset, uint32_t Value)
{
    for (unsigned Index = 0; Index < 4; ++Index)
        Data.at(Offset + Index) = uint8_t(Value >> (24 - 8 * Index));
}
P::Bytes HotJournal(const P::Bytes& DatabaseImage)
{
    Check(DatabaseImage.size() >= PageBytes && DatabaseImage.size() % PageBytes == 0,
        "journal original database size");
    // Same header layout as PhysicalTests::Header, now with a valid page-one
    // record. SQLite's documented rollback checksum samples N-200, N-400, ...
    // and adds the header nonce: https://www.sqlite.org/fileformat2.html#the_rollback_journal
    // This is synthetic offline recovery input, not a production crash claim.
    constexpr uint32_t Nonce = 0x13579bdf;
    const uint8_t Magic[] = {0xd9, 0xd5, 0x05, 0xf9, 0x20, 0xa1, 0x63, 0xd7};
    P::Bytes Journal(SectorBytes + PageBytes + 8, 0);
    std::copy(std::begin(Magic), std::end(Magic), Journal.begin());
    PutBig32(Journal, 8, 1);
    PutBig32(Journal, 12, Nonce);
    PutBig32(Journal, 16, uint32_t(DatabaseImage.size() / PageBytes));
    PutBig32(Journal, 20, uint32_t(SectorBytes));
    PutBig32(Journal, 24, uint32_t(PageBytes));
    PutBig32(Journal, SectorBytes, 1);
    std::copy_n(DatabaseImage.begin(), PageBytes, Journal.begin() + SectorBytes + 4);
    uint32_t Checksum = Nonce;
    for (int Offset = int(PageBytes) - 200; Offset >= 0; Offset -= 200)
        Checksum += DatabaseImage[size_t(Offset)];
    PutBig32(Journal, SectorBytes + 4 + PageBytes, Checksum);
    return Journal;
}
std::filesystem::path Fixture(const std::filesystem::path& Root, const std::string& Name,
    const P::Bytes& DatabaseImage, const P::Bytes& Journal)
{
    const auto Path = Root / Name;
    Check(std::filesystem::create_directory(Path), "fresh fixture directory");
    WriteBytes(Path / "store.sqlite3", DatabaseImage);
    WriteBytes(Path / "store.sqlite3-journal", Journal);
    return Path;
}
void NoAuxiliary(const std::filesystem::path& Path)
{
    Check(!Seen.Overflow, "VFS counter overflow");
    Check(Seen.WalOpens == 0 && Seen.ShmOpens == 0 && Seen.ShmCalls == 0,
        "unsupported WAL/shared-memory path reached");
    Check(Seen.OtherOpens == 0, "unexpected auxiliary VFS open");
    for (const auto& Entry : std::filesystem::directory_iterator(Path)) {
        const auto Name = Entry.path().filename().string();
        Check(Entry.is_regular_file() && (Name == "store.sqlite3" || Name == "store.sqlite3-journal"),
            "unexpected retained auxiliary file");
    }
}
void Rejected(const std::filesystem::path& Path, P::Error ExpectedError)
{
    bool Returned = false, Ready = false, Controlled = false;
    P::Error Error = P::Error::None;
    try {
        P::Backend Store(Path, Until());
        Returned = true;
        Ready = Store.Available();
    } catch (const P::Failure& Problem) {
        Error = Problem.Code;
        // The point of rejection can move between PRAGMA evaluation and the
        // explicit read-only gate. SqlError maps SQLITE_READONLY to StorageError.
        Controlled = Error == P::Error::StorageCorrupt || Error == P::Error::StorageUnavailable ||
            Error == P::Error::FormatUnsupported || Error == P::Error::StorageError;
    }
    NoAuxiliary(Path); // Also observes constructor cleanup and destruction.
    std::printf("[CarbonLuau:Persistence] WAL admission case=%s ready=%u error=%u expectedError=%u allowed=%u/%u/%u/%u wal=%u shm=%u\n",
        ActiveCase.c_str(), unsigned(Ready), unsigned(Error), unsigned(ExpectedError),
        unsigned(P::Error::StorageCorrupt), unsigned(P::Error::StorageUnavailable),
        unsigned(P::Error::FormatUnsupported), unsigned(P::Error::StorageError), Seen.WalOpens, Seen.ShmCalls);
    Check(!Returned && !Ready && Controlled, "unsupported format did not fail construction with a controlled error");
}
void Recovered(const std::filesystem::path& Path, const P::Identity& Id, const P::Bytes& Envelope)
{
    {
        P::Backend Store(Path, Until());
        Check(Store.Available(), "supported rollback database not Ready");
        const auto Result = Store.Execute(P::Operation::Get, Id, {}, Until());
        Check(Result.Code == P::Error::None && Result.Found && Result.NamespacePresent && Result.Envelope == Envelope,
            "supported rollback recovery lost canonical data");
    }
    NoAuxiliary(Path);
    Check(Seen.DatabaseOpens > 0, "production Backend bypassed observation VFS");
}
void Replayed()
{
    Check(Seen.JournalOpens > 0 && Seen.JournalReads > 0 && Seen.PageOneWrites > 0,
        "hot journal did not replay page one through SQLite");
}
struct Versions {
    const char* Name;
    uint8_t Write, Read;
    P::Error ExpectedError = P::Error::None;
};
constexpr Versions Unsupported[] = {
    {"write2-read2", 2, 2, P::Error::StorageCorrupt},
    {"write2-read1", 2, 1, P::Error::FormatUnsupported},
    {"write1-read2", 1, 2, P::Error::StorageCorrupt}
};
}

int main()
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
    try {
        Initialize();
        Check(sqlite3_compileoption_used("OMIT_WAL") != 0, "fixture must link production OMIT_WAL SQLite");
        AdmissionMethods = Methods;
        AdmissionMethods.xRead = AdmissionRead;
        AdmissionMethods.xWrite = AdmissionWrite;
        AdmissionMethods.xShmMap = AdmissionShmMap;
        AdmissionMethods.xShmLock = AdmissionShmLock;
        AdmissionMethods.xShmBarrier = AdmissionShmBarrier;
        AdmissionMethods.xShmUnmap = AdmissionShmUnmap;
        Tracer.xOpen = AdmissionOpen;
        Check(sqlite3_vfs_register(&Tracer, 1) == SQLITE_OK, "default admission observation VFS");

        const auto Root = std::filesystem::current_path() /
            ("wal-admission-" + std::to_string(P::Clock::now().time_since_epoch().count()));
        Check(std::filesystem::create_directory(Root), "fixture root");
        const auto Base = Root / "baseline";
        Check(std::filesystem::create_directory(Base), "baseline directory");
        const P::Identity Id{false, "", "WalAdmission", "Existing"};
        P::Value Value; Value.Type = P::Kind::String; Value.String = "canonical value survives recovery";
        const auto Envelope = P::Encode(Id, Value, Until());
        {
            P::Backend Store(Base, Until());
            Check(Store.Available(), "baseline not Ready");
            Check(Store.Execute(P::Operation::Set, Id, Envelope, Until()).Code == P::Error::None,
                "production canonical seed");
        }
        const auto Canonical = ReadBytes(Base / "store.sqlite3");
        const auto ColdJournal = ReadBytes(Base / "store.sqlite3-journal");
        Check(Canonical.size() >= PageBytes && Canonical.size() % PageBytes == 0 &&
            std::memcmp(Canonical.data(), "SQLite format 3\0", 16) == 0 &&
            Canonical[16] == 0x10 && Canonical[17] == 0 && Canonical[18] == 1 && Canonical[19] == 1,
            "baseline must have canonical 4096-byte rollback header");
        Check(ColdJournal.size() >= 28 && std::all_of(ColdJournal.begin(), ColdJournal.begin() + 28,
            [](uint8_t Byte) { return Byte == 0; }), "baseline PERSIST journal must be cold");

        ActiveCase = "rollback-control";
        Seen = {};
        Recovered(Base, Id, Envelope);
        Check(ReadBytes(Base / "store.sqlite3") == Canonical, "control changed database");
        unsigned Cases = 1;
        for (const auto& Version : Unsupported) {
            P::Bytes Image = Canonical;
            Image[18] = Version.Write; Image[19] = Version.Read;
            ActiveCase = std::string("main-") + Version.Name;
            const auto Path = Fixture(Root, ActiveCase, Image, ColdJournal);
            Seen = {};
            Rejected(Path, Version.ExpectedError);
            Check(ReadBytes(Path / "store.sqlite3") == Image, "rejection modified unsupported main database");
            Check(ReadBytes(Path / "store.sqlite3-journal") == ColdJournal, "rejection modified cold journal");
            ++Cases;
        }
        for (const auto& Version : Unsupported) {
            P::Bytes Restored = Canonical;
            Restored[18] = Version.Write; Restored[19] = Version.Read;
            ActiveCase = std::string("hot-") + Version.Name;
            // A valid main header passes a superficial pre-open gate. SQLite
            // must still reject the unsupported header subsequently replayed
            // from page one, without entering WAL/SHM or normalizing it to 1/1.
            const auto Path = Fixture(Root, ActiveCase, Canonical, HotJournal(Restored));
            Seen = {};
            Rejected(Path, Version.ExpectedError);
            Replayed();
            Check(ReadBytes(Path / "store.sqlite3") == Restored, "replayed unsupported image was altered or not restored");
            // SQLite owns journal finalization during legitimate playback; do
            // not demand an unchanged hot journal after recovery has occurred.
            ++Cases;
        }
        const auto GoodJournal = HotJournal(Canonical);
        const Versions Torn[] = {{"torn-magic", 1, 1}, {"torn-write2-read2", 2, 2},
            {"torn-write2-read1", 2, 1}, {"torn-write1-read2", 1, 2}};
        for (const auto& Version : Torn) {
            ActiveCase = Version.Name;
            P::Bytes Damaged = Canonical;
            Damaged[0] ^= 1;
            Damaged[18] = Version.Write; Damaged[19] = Version.Read;
            const auto Path = Fixture(Root, ActiveCase, Damaged, GoodJournal);
            Seen = {};
            Recovered(Path, Id, Envelope);
            Replayed();
            Check(ReadBytes(Path / "store.sqlite3") == Canonical, "torn main header was not restored exactly");
            // A second production open must retain both the recovered header
            // and the canonical stored envelope after SQLite finalizes recovery.
            Seen = {};
            Recovered(Path, Id, Envelope);
            Check(ReadBytes(Path / "store.sqlite3") == Canonical, "reopen changed recovered database");
            ++Cases;
        }
        Check(sqlite3_vfs_unregister(&Tracer) == SQLITE_OK, "unregister admission VFS");
        std::printf("[CarbonLuau:Persistence] WAL admission PASS cases=%u; retained=%s\n", Cases, Root.u8string().c_str());
        return 0;
    } catch (const P::Failure& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] WAL admission case=%s backend error=%u\n", ActiveCase.c_str(), unsigned(Problem.Code));
    } catch (const std::exception& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] WAL admission case=%s failure=%s\n", ActiveCase.c_str(), Problem.what());
    }
    return 1;
}
