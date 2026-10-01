// Standalone test executable; compile Backend.cpp/Derived.cpp with
// CARBONLUAU_PERSISTENCE_TESTING. Reuse fixtures, never production databases.
#define main DerivedFunctionalMain
#include "DerivedTests.cpp"
#undef main
#include <cstdlib>
#include <cstring>
#include <thread>
#ifndef _WIN32
#include <cerrno>
#include <csignal>
#include <sys/wait.h>
#include <unistd.h>
#endif

namespace {
std::string CrashAt;
std::string FailureAt;
Error InjectedError = Error::StorageError;
bool FailureHit = false;
bool CapacityArmed = false, CapacityHit = false;
void KillHere()
{
#ifdef _WIN32
    TerminateProcess(GetCurrentProcess(), 77);
#else
    kill(getpid(), SIGKILL);
#endif
    std::_Exit(77);
}
int Spawn(const std::filesystem::path& Exe, const std::vector<std::string>& Arguments)
{
#ifdef _WIN32
    std::wstring Command = L"\"" + Exe.wstring() + L"\"";
    for (const auto& Argument : Arguments) {
        Check(Argument.find('"') == std::string::npos && (Argument.empty() || Argument.back() != '\\'),
            "safe child argument");
        Command += L" \"" + std::filesystem::path(Argument).wstring() + L"\"";
    }
    STARTUPINFOW Start{}; Start.cb = sizeof(Start); PROCESS_INFORMATION Process{};
    Check(CreateProcessW(Exe.c_str(), Command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW,
        nullptr, nullptr, &Start, &Process) != 0, "launch hidden crash child");
    const DWORD Wait = WaitForSingleObject(Process.hProcess, 30000);
    if (Wait != WAIT_OBJECT_0) {
        TerminateProcess(Process.hProcess, 99);
        WaitForSingleObject(Process.hProcess, 5000);
    }
    DWORD Status = 99; GetExitCodeProcess(Process.hProcess, &Status);
    CloseHandle(Process.hThread); CloseHandle(Process.hProcess);
    Check(Wait == WAIT_OBJECT_0, "crash child timeout"); return int(Status);
#else
    std::vector<std::string> Storage{Exe.string()};
    Storage.insert(Storage.end(), Arguments.begin(), Arguments.end());
    std::vector<char*> Pointers;
    for (auto& Argument : Storage) Pointers.push_back(Argument.data());
    Pointers.push_back(nullptr);
    const pid_t Pid = fork(); Check(Pid >= 0, "fork crash child");
    if (Pid == 0) { execv(Exe.c_str(), Pointers.data()); _exit(99); }
    const auto End = Clock::now() + std::chrono::seconds(30);
    int Status = 0;
    for (;;) {
        const auto Wait = waitpid(Pid, &Status, WNOHANG);
        if (Wait == Pid) break;
        if (Wait < 0 && errno == EINTR) continue;
        Check(Wait >= 0, "wait for crash child");
        if (Clock::now() >= End) {
            kill(Pid, SIGKILL);
            while (waitpid(Pid, &Status, 0) < 0 && errno == EINTR) {}
            throw std::runtime_error("crash child timeout");
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(2));
    }
    return WIFSIGNALED(Status) && WTERMSIG(Status) == SIGKILL ? 77 :
        WIFEXITED(Status) ? WEXITSTATUS(Status) : 99;
#endif
}
}
namespace CarbonLuau::Persistence {
void TestCheckpoint(const char* Stage)
{
    if (CapacityArmed && std::strcmp(Stage, "derived-capacity-after-change") == 0) {
        CapacityArmed = false; CapacityHit = true;
        throw Failure(Error::QuotaExceeded);
    }
    if (FailureAt == Stage) {
        FailureHit = true; FailureAt.clear();
        throw Failure(InjectedError);
    }
    if (CrashAt == Stage) KillHere();
}
}
namespace {
const Identity CrashId{false, "", "Store", "Key"};
void VerifyCrash(Backend& Store)
{
    for (unsigned Step = 0; Step < 4096; ++Step) {
        if (!Store.HasDerivedWork()) { Check(Store.Available(), "crash verification health"); return; }
        Check(Store.MaintainDerived(Until()) && Store.Available(), "crash verification step");
    }
    throw std::runtime_error("crash verification did not converge");
}
int CrashChild(const std::filesystem::path& Folder, const std::string& Stage, const std::string& Mode)
{
    if (Mode == "upgrade") {
        CrashAt = Stage; Backend Store(Folder, Until());
    } else if (Mode == "verify") {
        Backend Store(Folder, Until());
        Connection Db(Folder);
        unsigned Steps = 0;
        while (Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=3") < 32)
            Check(++Steps < 4096 && Store.MaintainDerived(Until()), "bounded pre-crash derived verification");
        CrashAt = Stage;
        while (Store.HasDerivedWork())
            Check(++Steps < 4096 && Store.MaintainDerived(Until()), "bounded crash verification");
    } else {
        Backend Store(Folder, Until());
        if (Mode == "maintain" || Mode == "cleanup") {
            // Reopen invalidates process-local derived admission. Complete only
            // that proof, preserving the intentionally unfinished BUILDING batch.
            unsigned Steps = 0;
            while (!Store.PrepareDerived(CrashId, Mode == "cleanup" ? "Control" : "Field", Until()))
                Check(++Steps < 4096 && Store.MaintainDerived(Until()), "child verification before build fault");
        } else VerifyCrash(Store);
        CrashAt = Stage;
        CapacityArmed = Mode == "capacity-set";
        if (Mode == "prepare") Check(Store.PrepareDerived(CrashId, "Field", Until()), "child preparation");
        else if (Mode == "maintain" || Mode == "cleanup") Check(Store.MaintainDerived(Until()), "child maintenance");
        else if (Mode == "remove")
            Check(Store.Execute(Operation::Remove, CrashId, {}, Until()).Code == Error::None, "child removal");
        else {
            Check(Mode == "set" || Mode == "capacity-set", "known child mode");
            const auto Envelope = Encode(CrashId, Record(Text(std::string(1024, 'z'))), Until());
            Check(Store.Execute(Operation::Set, CrashId, Envelope, Until()).Code == Error::None, "child mutation");
        }
    }
    throw std::runtime_error("requested crash checkpoint was not reached");
}
void UpgradeCrashes(const std::filesystem::path& Parent, const std::filesystem::path& Exe)
{
    for (const char* Stage : {"derived-upgrade-before-begin", "derived-upgrade-after-tables",
        "derived-upgrade-before-commit", "derived-upgrade-after-commit"}) {
        const auto Folder = Fixture(Parent, Stage);
        Legacy(Folder, {{CrashId, Record(Numeric(7))}});
        Check(Spawn(Exe, {"derived-child", Folder.string(), Stage, "upgrade"}) == 77, "upgrade checkpoint hit");
        {
            Backend Store(Folder, Until());
            const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
            Check(Store.Available() && Read.Code == Error::None && Read.Found &&
                Read.Envelope == Encode(CrashId, Record(Numeric(7)), Until()), "upgrade crash primary intact");
            VerifyCrash(Store);
            Check(!Store.HasDerivedWork(), "upgrade crash creates no field demand");
        }
        Connection Db(Folder);
        Check(Scalar(Db, "PRAGMA user_version") == 2 &&
            Scalar(Db, "SELECT count(*) FROM DerivedFields") == 0, "upgrade recovers complete schema");
    }
}
void TransactionCrashes(const std::filesystem::path& Parent, const std::filesystem::path& Exe)
{
    for (const std::string Mode : {"set", "remove"})
        for (const std::string Stage : {"after-key", "after-quota", "before-commit", "after-commit"}) {
            const auto Folder = Fixture(Parent, (Mode + "-" + Stage).c_str());
            {
                Backend Store(Folder, Until()); Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
                Check(Store.PrepareDerived(CrashId, "Field", Until()), "prepare active crash fixture");
                Drain(Store);
            }
            Check(Spawn(Exe, {"derived-child", Folder.string(), Stage, Mode}) == 77, "primary crash checkpoint hit");
            {
                Backend Store(Folder, Until());
                const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
                Check(Read.Code == Error::None && Store.Available(), "primary crash recovery");
                if (Stage != "after-commit")
                    Check(Read.Found && Read.Envelope == Encode(CrashId, Record(Numeric(7)), Until()), "uncommitted primary rolled back");
                else if (Mode == "remove") Check(!Read.Found, "committed remove durable");
                else Check(Read.Found && Read.Envelope == Encode(CrashId, Record(Text(std::string(1024, 'z'))), Until()),
                    "committed new primary durable");
            }
            CheckProjection(Folder, CrashId);
        }
}

void SavepointFailures(const std::filesystem::path& Parent)
{
    const std::vector<std::pair<std::string, bool>> Cases{
        {"derived-before-savepoint", false}, {"derived-after-savepoint", false},
        {"derived-before-release", false}, {"derived-after-release", false},
        {"derived-before-rollback-to", true}, {"derived-after-rollback-to", true},
        {"derived-before-rollback-release", true}, {"derived-after-rollback-release", true},
        {"derived-before-withdraw", true}, {"derived-after-withdraw", true}};
    for (const auto& Case : Cases) {
        const auto Folder = Fixture(Parent, ("failure-" + Case.first).c_str());
        {
            Backend Store(Folder, Until()); Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
            Check(Store.PrepareDerived(CrashId, "Field", Until()), "fault fixture preparation"); Drain(Store);
            FailureAt = Case.first; FailureHit = false;
            InjectedError = Error::StorageError; CapacityArmed = Case.second; CapacityHit = false;
            const auto Result = Store.Execute(Operation::Set, CrashId,
                Encode(CrashId, Record(Text(std::string(1024, 'z'))), Until()), Until());
            FailureAt.clear(); CapacityArmed = false;
            Check(FailureHit && CapacityHit == Case.second, "savepoint failure hook reached");
            Check(Result.Code == Error::StorageError && !Store.Available(), "failed savepoint operation is not safe withdrawal");
        }
        {
            Backend Store(Folder, Until());
            const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
            Check(Read.Code == Error::None && Read.Found &&
                Read.Envelope == Encode(CrashId, Record(Numeric(7)), Until()), "outer rollback preserves old primary");
        }
        CheckProjection(Folder, CrashId);
    }
    // An error categorized as physical FULL at the same partially changed
    // point must escape capacity recovery. This is a controlled error-seam
    // test, not a physical disk-full/VFS qualification.
    const auto Folder = Fixture(Parent, "full-not-capacity");
    {
        Backend Store(Folder, Until()); Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
        Check(Store.PrepareDerived(CrashId, "Field", Until()), "FULL fixture preparation"); Drain(Store);
        FailureAt = "derived-capacity-after-change"; FailureHit = false; InjectedError = Error::StorageFull;
        const auto Result = Store.Execute(Operation::Set, CrashId,
            Encode(CrashId, Record(Numeric(8)), Until()), Until());
        FailureAt.clear();
        Check(FailureHit && Result.Code == Error::StorageFull, "physical category not converted to withdrawal");
    }
    CheckProjection(Folder, CrashId);
    {
        Backend Store(Folder, Until());
        Check(Store.Execute(Operation::Get, CrashId, {}, Until()).Envelope ==
            Encode(CrashId, Record(Numeric(7)), Until()), "FULL old primary");
    }
}

void RealPageFull(const std::filesystem::path& Parent)
{
    const auto Folder=Fixture(Parent,"real-page-full");
    const auto NewId=Key(CrashId,"CapacityOverflow");
    const auto OldEnvelope=Encode(CrashId,Record(Numeric(7)),Until());
    Derived::View BeforeIndex;
    {
        Backend Store(Folder,Until());
        Set(Store,CrashId,Record(Numeric(7))); VerifyCrash(Store);
        Check(Store.PrepareDerived(CrashId,"Field",Until()),"FULL fixture preparation");
        Drain(Store);
        BeforeIndex = Inspect(Folder, CrashId).View;
        Check(Store.TestCapAtCurrentPages()>0,"test-only page cap installed");
        auto NewValue=Record(Numeric(8));
        for(unsigned Part=0;Part<4;++Part)
            NewValue.Map.emplace_back("Padding"+std::to_string(Part),
                std::make_shared<Value>(Text(std::string(15000,'x'))));
        const auto NewEnvelope=Encode(NewId,NewValue,Until());
        const auto Result=Store.Execute(Operation::Set,NewId,NewEnvelope,Until());
        Check(Result.Code==Error::StorageFull && Store.Available(),"real SQLite FULL is definite before COMMIT");
        const auto Old=Store.Execute(Operation::Get,CrashId,{},Until());
        const auto Missing=Store.Execute(Operation::Get,NewId,{},Until());
        Check(Old.Code==Error::None && Old.Found && Old.Envelope==OldEnvelope &&
            Missing.Code==Error::None && !Missing.Found,"FULL preserves authoritative primary state");
    }
    CheckProjection(Folder,CrashId);
    const auto AfterIndex = Inspect(Folder, CrashId).View;
    Check(BeforeIndex.Status == Derived::State::Active &&
        AfterIndex.Status == Derived::State::Active &&
        AfterIndex.Generation == BeforeIndex.Generation,
        "physical FULL does not masquerade as derived logical withdrawal");
    {
        Backend Store(Folder,Until());
        const auto Old=Store.Execute(Operation::Get,CrashId,{},Until());
        const auto Missing=Store.Execute(Operation::Get,NewId,{},Until());
        Check(Old.Code==Error::None && Old.Envelope==OldEnvelope &&
            Missing.Code==Error::None && !Missing.Found,"FULL restart preserves primary and ACTIVE index");
    }
}

void CommittedAcknowledgementLost(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "committed-acknowledgement-lost");
    const auto NewEnvelope = Encode(CrashId, Record(Numeric(8)), Until());
    {
        Backend Store(Folder, Until());
        Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
        Check(Store.PrepareDerived(CrashId, "Field", Until()), "lost acknowledgement preparation");
        Drain(Store);
        FailureAt = "after-commit"; FailureHit = false; InjectedError = Error::StorageError;
        const auto Result = Store.Execute(Operation::Set, CrashId, NewEnvelope, Until());
        FailureAt.clear();
        Check(FailureHit && Result.Code == Error::Indeterminate && !Store.Available(),
            "post-COMMIT acknowledgement loss is indeterminate, never definite success or failure");
    }
    {
        Backend Store(Folder, Until());
        const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
        Check(Store.Available() && Read.Code == Error::None && Read.Found &&
            Read.Envelope == NewEnvelope, "committed mutation remains durable after lost acknowledgement");
    }
    CheckProjection(Folder, CrashId);
}

void CapacityWithdrawal(const std::filesystem::path& Parent, bool Building)
{
    const auto Folder = Fixture(Parent, Building ? "capacity-building" : "capacity-active");
    Derived::View Before;
    {
        Backend Store(Folder, Until());
        for (unsigned Index = 0; Index < 70; ++Index)
            Set(Store, Key(CrashId, RecordKey(Index)), Record(Numeric(Index)));
        Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
        Check(Store.PrepareDerived(CrashId, "Control", Until()), "capacity verification control"); Drain(Store);
        Check(Store.PrepareDerived(CrashId, "Field", Until()), "capacity fixture preparation");
        if (Building) Check(Store.MaintainDerived(Until()), "partial capacity build");
        else Drain(Store);
    }
    Before = Inspect(Folder, CrashId).View;
    {
        Backend Store(Folder, Until());
        unsigned Steps = 0;
        while (!Store.PrepareDerived(CrashId, "Field", Until()))
            Check(++Steps < 4096 && Store.MaintainDerived(Until()), "capacity fixture verification");
        CapacityArmed = true; CapacityHit = false;
        const auto Envelope = Encode(CrashId, Record(Text(std::string(1024, 'z'))), Until());
        const auto Result = Store.Execute(Operation::Set, CrashId, Envelope, Until());
        CapacityArmed = false;
        Check(CapacityHit && Result.Code == Error::None && Store.Available(), "logical capacity preserves valid primary Set");
        const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
        Check(Read.Code == Error::None && Read.Envelope == Envelope, "withdrawal commits new primary");
    }
    const auto Withdrawn = Inspect(Folder, CrashId);
    Check(Before.Generation > 0 && Withdrawn.View.FieldId == Before.FieldId &&
        Withdrawn.View.Status == Derived::State::Unavailable && Withdrawn.Stats.Cleanup == 1,
        "failed generation atomically unavailable");
    CheckProjection(Folder, CrashId, true);
    {
        Backend Store(Folder, Until());
        unsigned Steps = 0;
        while (!Store.PrepareDerived(CrashId, "Control", Until()))
            Check(++Steps < 4096 && Store.MaintainDerived(Until()), "cleanup verification");
        Check(Store.MaintainDerived(Until()), "one cleanup batch");
    }
    const auto Cleaned = Inspect(Folder, CrashId);
    Check(Withdrawn.Stats.Members - Cleaned.Stats.Members <= 32 &&
        Cleaned.Stats.Members < Withdrawn.Stats.Members, "cleanup bounded and progressing");
    {
        Backend Store(Folder, Until()); Drain(Store);
        Check(Store.PrepareDerived(CrashId, "Field", Until()), "retry withdrawn generation");
        Drain(Store);
    }
    CheckProjection(Folder, CrashId);
    Check(Inspect(Folder, CrashId).View.Generation > Before.Generation, "replacement has fresh generation");
}

void DerivedCrashes(const std::filesystem::path& Parent, const std::filesystem::path& Exe)
{
    const std::vector<std::pair<std::string, std::string>> Cases{
        {"prepare", "derived-before-commit"}, {"prepare", "derived-after-commit"},
        {"maintain", "derived-before-checkpoint"}, {"maintain", "derived-after-checkpoint"},
        {"maintain", "derived-before-publish"}, {"maintain", "derived-after-publish"},
        {"maintain", "derived-before-commit"}, {"maintain", "derived-after-commit"},
        {"set", "derived-before-savepoint"}, {"set", "derived-after-savepoint"},
        {"set", "derived-before-release"}, {"set", "derived-after-release"},
        {"capacity-set", "derived-before-rollback-to"}, {"capacity-set", "derived-after-rollback-to"},
        {"capacity-set", "derived-before-rollback-release"}, {"capacity-set", "derived-after-rollback-release"},
        {"capacity-set", "derived-before-withdraw"}, {"capacity-set", "derived-after-withdraw"},
        {"capacity-set", "before-commit"}, {"capacity-set", "after-commit"}};
    for (const auto& Case : Cases) {
        const auto& Mode = Case.first; const auto& Stage = Case.second;
        const auto Folder = Fixture(Parent, ("crash-" + Mode + "-" + Stage).c_str());
        {
            Backend Store(Folder, Until()); Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
            if (Mode != "prepare") {
                Check(Store.PrepareDerived(CrashId, "Field", Until()), "derived crash preparation");
                if (Mode != "maintain") Drain(Store);
            }
        }
        const int Status = Spawn(Exe, {"derived-child", Folder.string(), Stage, Mode});
        if (Status != 77)
            std::fprintf(stderr, "[CarbonLuau:Persistence] Missed derived crash stage %s/%s: child=%d\n",
                Mode.c_str(), Stage.c_str(), Status);
        Check(Status == 77, "derived crash checkpoint reached");
        {
            Backend Store(Folder, Until());
            const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
            const auto Expected = Mode == "capacity-set" && Stage == "after-commit" ?
                Record(Text(std::string(1024, 'z'))) : Record(Numeric(7));
            Check(Store.Available() && Read.Code == Error::None && Read.Found &&
                Read.Envelope == Encode(CrashId, Expected, Until()), "derived crash old/new primary atomicity");
        }
        const auto Recovered = Inspect(Folder, CrashId);
        if (Mode == "prepare") {
            Check((Recovered.View.FieldId != 0) == (Stage == "derived-after-commit"), "prepare commit atomicity");
        } else if (Mode == "maintain") {
            const bool Committed = Stage == "derived-after-commit";
            Check(Recovered.View.Status == (Committed ? Derived::State::Active : Derived::State::Building) &&
                Recovered.View.Members == (Committed ? 1 : 0), "publication/checkpoint atomicity");
        } else CheckProjection(Folder, CrashId, Mode == "capacity-set" && Stage == "after-commit");
        {
            Backend Store(Folder, Until()); Drain(Store);
            Check(Store.PrepareDerived(CrashId, "Field", Until()), "post-crash preparation"); Drain(Store);
        }
        CheckProjection(Folder, CrashId);
    }
}

void ProgressCrashes(const std::filesystem::path& Parent, const std::filesystem::path& Exe)
{
    for (const std::string Stage : {"derived-before-checkpoint", "derived-after-checkpoint",
        "derived-before-commit", "derived-after-commit"}) {
        const auto Folder = Fixture(Parent, ("progress-" + Stage).c_str());
        {
            Backend Store(Folder, Until());
            for (unsigned Index = 0; Index < 70; ++Index)
                Set(Store, Key(CrashId, RecordKey(Index)), Record(Numeric(Index)));
            VerifyCrash(Store);
            Check(Store.PrepareDerived(CrashId, "Field", Until()) && Store.MaintainDerived(Until()), "progress fixture");
        }
        Check(Spawn(Exe, {"derived-child", Folder.string(), Stage, "maintain"}) == 77, "progress checkpoint hit");
        { Backend Store(Folder, Until()); Check(Store.Available(), "progress recovery"); }
        const auto Recovered = Inspect(Folder, CrashId);
        const bool Committed = Stage == "derived-after-commit";
        Check(Recovered.View.Status == Derived::State::Building &&
            Recovered.View.Members == (Committed ? 64 : 32) &&
            Recovered.Checkpoint == (Committed ? "K063" : "K031"),
            "nonfinal checkpoint/entries commit atomically");
        { Backend Store(Folder, Until()); Drain(Store); }
        CheckProjection(Folder, CrashId);
    }
}

void CleanupCrashes(const std::filesystem::path& Parent, const std::filesystem::path& Exe)
{
    for (const std::string Stage : {"derived-before-cleanup", "derived-after-cleanup",
        "derived-before-commit", "derived-after-commit"}) {
        const auto Folder = Fixture(Parent, ("cleanup-" + Stage).c_str());
        {
            Backend Store(Folder, Until()); Set(Store, CrashId, Record(Numeric(7))); VerifyCrash(Store);
            Check(Store.PrepareDerived(CrashId, "Field", Until()), "cleanup fixture"); Drain(Store);
            Check(Store.PrepareDerived(CrashId, "Control", Until()), "cleanup verification control"); Drain(Store);
            CapacityArmed = true; CapacityHit = false;
            Set(Store, CrashId, Record(Numeric(8)));
            CapacityArmed = false; Check(CapacityHit, "cleanup withdrawal triggered");
        }
        const auto Before = Inspect(Folder, CrashId);
        Check(Before.Stats.Cleanup == 1 && Before.Stats.Members == 1, "retained cleanup fixture");
        Check(Spawn(Exe, {"derived-child", Folder.string(), Stage, "cleanup"}) == 77, "cleanup checkpoint hit");
        {
            Backend Store(Folder, Until());
            const auto Read = Store.Execute(Operation::Get, CrashId, {}, Until());
            Check(Read.Code == Error::None && Read.Envelope == Encode(CrashId, Record(Numeric(8)), Until()),
                "cleanup crash preserves authoritative primary");
        }
        const auto After = Inspect(Folder, CrashId);
        const bool Committed = Stage == "derived-after-commit";
        Check(After.Stats.Cleanup == (Committed ? 0 : 1) && After.Stats.Members == (Committed ? 0 : 1) &&
            (Committed ? After.Stats.Bytes < Before.Stats.Bytes : After.Stats.Bytes == Before.Stats.Bytes),
            "cleanup rows/accounting release atomically");
        {
            Backend Store(Folder, Until()); Drain(Store);
            Check(Store.PrepareDerived(CrashId, "Field", Until()), "cleanup restart retry"); Drain(Store);
        }
        CheckProjection(Folder, CrashId);
    }
}
void QuarantineBatchCrash(const std::filesystem::path& Parent, const std::filesystem::path& Exe)
{
    const auto Folder = Fixture(Parent, "quarantine-between-batches");
    auto Input = Record(Text(std::string(1024, 'z')));
    Input.Map.emplace_back("Other", std::make_shared<Value>(Text(std::string(1024, 'w'))));
    std::vector<Identity> Records;
    {
        Backend Store(Folder, Until());
        for (unsigned Index = 0; Index < 40; ++Index) {
            const unsigned StoreIndex = (Index * 17) % 40;
            Identity Id{false, "", std::string("Store") + (StoreIndex < 10 ? "0" : "") + std::to_string(StoreIndex), "Key"};
            Set(Store, Id, Input); VerifyCrash(Store);
            Check(Store.PrepareDerived(Id, "Field", Until()), "quarantine crash field preparation");
            if (StoreIndex == 31)
                Check(Store.PrepareDerived(Id, "Other", Until()), "crash at mid-store cursor boundary");
            Records.push_back(std::move(Id));
        }
        Drain(Store);
    }
    {
        Connection Db(Folder, true);
        Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
        Db.Exec("UPDATE DerivedEntries SET Suffix=CAST(X'79'||substr(Suffix,2) AS BLOB)");
        Db.Exec("COMMIT");
    }
    Check(Spawn(Exe, {"derived-child", Folder.string(), "derived-after-withdraw", "verify"}) == 77,
        "crash after first bounded quarantine commit");
    {
        Connection Db(Folder);
        const auto Cleanup = Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=3");
        const auto Active = Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2");
        // Store31 owns two fields; its one verification transaction withdraws
        // both, so the committed count crosses the threshold from 31 to 33.
        Check(Cleanup == 33 && Active == 8,
            "bounded verification commits whole-store withdrawals before crash");
    }
    {
        Backend Store(Folder, Clock::now() + std::chrono::seconds(30));
        VerifyCrash(Store);
        Check(Store.Available(), "crashed quarantine resumes safely");
        for (const auto& Id : Records) {
            auto Read = Store.Execute(Operation::Get, Id, {}, Until());
            Check(Read.Code == Error::None && Read.Envelope == Encode(Id, Input, Until()),
                "quarantine crash preserves authoritative primary");
        }
    }
    Connection Db(Folder);
    Check(Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=3") == 0 &&
        Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 0,
        "restart finishes bounded deferred quarantine and cleanup");
}
}
int main(int Count, char** Arguments)
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
    try {
        if (Count == 5 && std::string(Arguments[1]) == "derived-child")
            return CrashChild(Arguments[2], Arguments[3], Arguments[4]);
        const bool FocusedFaults = Count == 2 && std::string(Arguments[1]) == "--focused-faults";
        Check(Count == 1 || FocusedFaults, "no production path accepted");
        const auto Root = std::filesystem::absolute(std::filesystem::current_path() /
            ("derived-crash-" + std::to_string(Clock::now().time_since_epoch().count())));
        Check(std::filesystem::create_directory(Root), "fresh crash fixture root");
        const auto Exe = std::filesystem::absolute(Arguments[0]);
        if (FocusedFaults) {
            SavepointFailures(Root); RealPageFull(Root); CommittedAcknowledgementLost(Root);
            std::puts("[CarbonLuau:Persistence] Focused derived capacity/fault tests PASS; fixtures retained");
            return 0;
        }
        UpgradeCrashes(Root, Exe); TransactionCrashes(Root, Exe); DerivedCrashes(Root, Exe);
        ProgressCrashes(Root, Exe); CleanupCrashes(Root, Exe); QuarantineBatchCrash(Root, Exe);
        SavepointFailures(Root); RealPageFull(Root); CommittedAcknowledgementLost(Root);
        CapacityWithdrawal(Root, false); CapacityWithdrawal(Root, true);
        // Prove invalid structure is rejected BEFORE even attempting to
        // quarantine an earlier coherent mismatch, not merely rolled back
        // after a premature withdrawal.
        FailureAt = "derived-before-withdraw"; FailureHit = false; InjectedError = Error::StorageError;
        InvalidDerivedStructure(Root);
        FailureAt.clear();
        Check(!FailureHit, "complete graph validation precedes every quarantine attempt");
        std::puts("[CarbonLuau:Persistence] Derived crash tests PASS; process-crash evidence only; fixtures retained");
        return 0;
    } catch (const std::exception& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Derived crash tests failed: %s\n", Problem.what());
        return 1;
    }
}
