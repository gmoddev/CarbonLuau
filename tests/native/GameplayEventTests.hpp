#pragma once
#include "../../native/src/runtime/RuntimeInternal.hpp"
#include <set>

namespace CarbonLuau::GameplayEventTests {
using namespace CarbonLuau::Runtime;
static ClHandle CurrentVm;
static Vm* CurrentRuntime;
static std::set<uint64_t> Releases;
static bool RejectGate, RejectRelease, ThrowGate;
static unsigned Gates;
static uint64_t NextListener;
static void Check(bool Good, const char* Message)
{
    if (!Good) { std::fprintf(stderr, "[CarbonLuau:GameplayEventNative] %s\n", Message); std::exit(1); }
}
static std::string Pack(const std::vector<std::string>& Fields)
{
    std::string Result;
    for (const auto& Field : Fields) { Result += Field; Result += '\0'; }
    return Result;
}
static std::vector<std::string> Frame(uint64_t Nonce = 1)
{
    return {"died", "1", "1", "76561190000999888", "Fixture", std::to_string(Nonce),
        "1.25", "-2.5", "0", "", "", ""};
}
static std::vector<std::string> DestroyedFrame(uint64_t Nonce = 1)
{
    return {"entitydestroyed", "1", "41", "9", "assets/fixture.prefab", std::to_string(Nonce),
        "1.25", "-2.5", "0", "", "", ""};
}
static uint32_t Host(uint64_t OwnerId, uint32_t Operation, const char* Bytes, uint32_t Length,
    char* Output, uint32_t Capacity, uint32_t* Written)
{
    *Written = 0;
    if (Operation == 6 || Operation == 8)
        *Written = uint32_t(std::snprintf(Output, Capacity, "%llu", (unsigned long long)++NextListener) + 1);
    else if (Operation == 9) {
        Vm& Runtime = *CurrentRuntime;
        if (Length >= 5 && (!std::memcmp(Bytes, "died\0", 5) || (Length >= 8 && !std::memcmp(Bytes, "spawned\0", 8)) ||
            (Length >= 16 && !std::memcmp(Bytes, "entitydestroyed\0", 16)))) {
            ++Gates;
            Check(Runtime.GameplayInFlightOwner && Runtime.GameplayInFlightOwner->Id == OwnerId &&
                Runtime.GameplayInFlightReservation && Runtime.GameplayInFlightPayload &&
                Runtime.GameplayInFlightPayload->size() == Length && Runtime.GameplayReserved != 0,
                "reservation and native payload survive through fresh callback gate");
            Check(cl_domain_event(CurrentVm, OwnerId, Bytes, Length) == CL_INVALID_ARGUMENT,
                "duplicate nonce cannot retarget an executing callback");
        }
        if (ThrowGate) throw std::runtime_error("controlled native gate exception");
        return RejectGate ? 1 : 0;
    } else if (Operation == 39) {
        Check(Length >= 2 && Length <= 21 && !Bytes[Length - 1], "bounded private release nonce");
        uint64_t Nonce = std::stoull(Bytes);
        Check(Releases.insert(Nonce).second, "release is attempted once per accepted item");
        Vm& Runtime = *CurrentRuntime;
        Check(Runtime.GameplayInFlightReservation != Nonce,
            "in-flight native payload is cleared before managed quota acknowledgement");
        for (const auto& Slot : Runtime.GameplayReservations)
            Check(Slot.Nonce != Nonce, "native slot is released before acknowledgement");
        return RejectRelease ? 1 : 0;
    }
    return 0;
}
struct Fixture {
    ClHandle VmId = 0, Root = 0;
    Fixture(const char* Body = "assert(Player.UserId=='76561190000999888')") {
        Releases.clear(); Gates = 0; NextListener = 0;
        RejectGate = RejectRelease = ThrowGate = false; TestAllocationFailureAfter = -1;
        TestGameplayCopyFailure = false;
        ClVmConfig Config{16 * MiB}; Check(cl_vm_create(&Config, &VmId) == CL_OK, "real VM");
        CurrentVm = VmId; CurrentRuntime = GetVm(VmId); Root = Add(Body);
    }
    ~Fixture() {
        TestAllocationFailureAfter = -1; TestGameplayCopyFailure = false; RejectRelease = ThrowGate = false;
        Check(cl_vm_destroy(VmId) == CL_OK && !TestLiveBytes, "event fixture teardown has no Lua allocator leak");
        CurrentRuntime = nullptr;
    }
    Vm& Runtime() { return *GetVm(VmId); }
    Domain& Owner(ClHandle Id = 0) { return *GetDomain(Runtime(), Id ? Id : Root); }
    void Run(ClHandle OwnerId, const std::string& Source) {
        ClHandle Thread = 0; ClResult Result{};
        Check(cl_domain_load_source(VmId, OwnerId, "gameplay.event.fixture", Source.data(),
            uint32_t(Source.size()), &Thread, &Result) == CL_OK, "real source compile");
        auto Status = cl_thread_resume(Thread, 100000000, &Result);
        if (Status != CL_OK) std::fprintf(stderr, "[CarbonLuau:GameplayEventNative] %s\n", Result.Error);
        Check(Status == CL_OK && cl_thread_destroy(Thread) == CL_OK, "real bootstrap execution");
    }
    ClHandle Add(const char* Body) {
        ClHandle Id = 0; NextListener = 0;
        Check(cl_domain_create(VmId, 4096, &Id) == CL_OK && cl_domain_facade(VmId, Id, Host) == CL_OK,
            "bounded owner facade");
        Run(Id, std::string("game:GetService('Players').PlayerAdded:Connect(function(Player) ") + Body + " end)");
        Check(cl_domain_commit(VmId, Id) == CL_OK, "committed owner"); return Id;
    }
    ClStatus Submit(const std::vector<std::string>& Fields, ClHandle Id = 0) {
        std::string Payload = Pack(Fields);
        return cl_domain_event(VmId, Id ? Id : Root, Payload.data(), uint32_t(Payload.size()));
    }
    ClStatus Step(uint64_t Budget = 100000000) {
        ClSchedulerInfo Info{}; Check(cl_vm_scheduler(VmId, &Info) == CL_OK, "scheduler snapshot");
        uint32_t Ran = 0; ClResult Result{};
        auto Status = cl_vm_callback(VmId, Info.NowNs, Info.Sequence, Budget, &Ran, &Result);
        Check(Ran == 1, "one existing scheduler item attempted"); return Status;
    }
};

static void Codec()
{
    {
    Fixture F("return");
    F.Run(F.Root, "game:GetService('Workspace').EntityDestroyed:Connect(function(Context) "
        "assert(type(Context)=='table' and table.isfrozen(Context)) "
        "assert(Context.Id=='9' and Context.Prefab=='assets/fixture.prefab') "
        "assert(not pcall(function() Context.Id='retargeted' end)) "
        "assert(Context.Entity==nil and Context.Cause==nil) "
        "if Context.Position then "
        "assert(Context.Position==Vector3.new(1.25,-2.5,0)) "
        "assert(not pcall(function() Context.Position.X=7 end)) end end)");
    auto Destroyed = DestroyedFrame(801); Destroyed[1] = "2";
    Check(F.Submit(Destroyed) == CL_OK && F.Step() == CL_OK && Gates == 1 && !F.Runtime().GameplayReserved,
        "EntityDestroyed delivers exactly one immutable snapshot without live facade resolution");
    Destroyed[5] = "802"; Destroyed[6] = Destroyed[7] = Destroyed[8] = "";
    Check(F.Submit(Destroyed) == CL_OK && F.Step() == CL_OK && Gates == 2 && Releases.size() == 2,
        "EntityDestroyed accepts absent optional position and refunds reservation after delivery");
    }
    {
    Fixture F("return");
    auto Destroyed = DestroyedFrame(803); Destroyed[4] = std::string(512,'x');
    Destroyed[6] = "3.4028234663852886e38"; Destroyed[7] = "-3.4028234663852886e38";
    Destroyed[8] = "1.401298464324817e-45";
    Check(F.Submit(Destroyed) == CL_OK && F.Step() == CL_OK, "EntityDestroyed prefab and Single boundaries");
    for (const auto& Change : std::vector<std::pair<unsigned, std::string>> {
        {0,"entitydestroyedx"},{1,"0"},{1,"01"},{2,"0"},{2,"01"},{2,"18446744073709551616"},
        {3,"0"},{3,"01"},{3,"18446744073709551616"},{4,""},{4,std::string(513,'x')},
        {4,std::string("\xc0\x80",2)},{4,std::string("\xed\xa0\x80",3)},{5,"0"},{5,"01"},
        {6,"NaN"},{6,"Infinity"},{6,"1e39"},{6,"1e-99"},{6,"3.4028235e38"},{6,"-3.4028235e38"},
        {6,"+1"},{6," 1"},{6,"01"},
        {6,"-0"},{6,"0.0"},{6,"1e2junk"},{7,""},{8,""},{9,"1"},{10,"1"},{11,"invented"}}) {
        auto Bad = DestroyedFrame(804); Bad[Change.first] = Change.second;
        Check(F.Submit(Bad) == CL_INVALID_ARGUMENT && !F.Runtime().GameplayReserved,
            "malformed EntityDestroyed leaves no retained payload or reservation");
    }
    auto Missing = DestroyedFrame(804); Missing.pop_back();
    auto Extra = DestroyedFrame(804); Extra.push_back("");
    auto Oversized = DestroyedFrame(804); Oversized[4] = std::string(2048,'x');
    Check(F.Submit(Missing) == CL_INVALID_ARGUMENT && F.Submit(Extra) == CL_INVALID_ARGUMENT &&
        F.Submit(Oversized) == CL_INVALID_ARGUMENT, "EntityDestroyed exact field count and payload ceiling");
    std::string Unterminated = Pack(DestroyedFrame(804)); Unterminated.pop_back();
    Check(cl_domain_event(F.VmId,F.Root,Unterminated.data(),uint32_t(Unterminated.size())) == CL_INVALID_ARGUMENT,
        "EntityDestroyed missing final terminator rejected");
    }
    {
    Fixture F("return");
    auto Entity = std::vector<std::string>{"entityspawned", "1", "41", "9", "assets/fixture.prefab", "901", "77", "1", "", "", "", ""};
    Check(F.Submit(Entity) == CL_OK && F.Step() == CL_OK && !F.Runtime().GameplayReserved,
        "EntitySpawned shares existing reserved queue and consumption");
    Entity[5] = "902"; Entity[4] = std::string(512, 'x');
    Check(F.Submit(Entity) == CL_OK && F.Step() == CL_OK, "maximum prefab bytes");
    for (const auto& Change : std::vector<std::pair<unsigned, std::string>> {
        {2,"0"},{2,"01"},{3,"0"},{3,"18446744073709551616"},{4,""},{4,std::string(513,'x')},
        {4,std::string("\xc0\x80",2)},{6,"0"},{7,"0"},{8,"1"},{11,"invented"}}) {
        auto Bad = Entity; Bad[4]="assets/fixture.prefab"; Bad[5]="903"; Bad[Change.first]=Change.second;
        Check(F.Submit(Bad) == CL_INVALID_ARGUMENT && !F.Runtime().GameplayReserved, "malformed EntitySpawned no retention");
    }
    }
    Fixture F;
    std::vector<std::vector<std::string>> Invalid;
    for (const auto& Change : std::vector<std::pair<unsigned,std::string>> {
        {0,"diedx"},{0,"spawnedx"},{0,"unknown"},{1,"0"},{1,"01"},{1,"18446744073709551616"},
        {2,"+1"},{2,"1.0"},{3,""},{3,"765x"},{3,std::string(21,'1')},{4,std::string(129,'a')},
        {4,std::string("\xc2",1)},{4,std::string("\xc0\x80",2)},{4,std::string("\xed\xa0\x80",3)},
        {4,std::string("\xf4\x90\x80\x80",4)},{5,"0"},{5,"01"},{5,"18446744073709551616"},
        {6,"NaN"},{6,"Infinity"},{6,"1e39"},{6,"1e-99"},{6,"+1"},{6," 1"},{6,"01"},
        {6,"0x1"},{6,"1."},{6,"1e"},{6,"-0"},{6,"0.0"},{6,"1e2junk"},{6,std::string(33,'1')},
        {7,""},{9,"2"},{10,"3"},{11,"orphan"}}) {
        auto Fields = Frame(); Fields[Change.first] = Change.second; Invalid.push_back(Fields);
    }
    auto Missing = Frame(); Missing.pop_back(); Invalid.push_back(Missing);
    auto Extra = Frame(); Extra.push_back(""); Invalid.push_back(Extra);
    auto SpawnKiller = Frame(); SpawnKiller[0] = "spawned"; SpawnKiller[9] = "2";
    SpawnKiller[10] = "76561190000999889"; SpawnKiller[11] = "Killer"; Invalid.push_back(SpawnKiller);
    auto Oversized = Frame(); Oversized[4] = std::string(2048,'a'); Invalid.push_back(Oversized);
    uint64_t Before = F.Runtime().Used, Sequence = F.Runtime().Sequence;
    TestAllocationFailureAfter = 0;
    for (const auto& Fields : Invalid) {
        Check(F.Submit(Fields) == CL_INVALID_ARGUMENT, "malformed lifecycle rejected before allocation");
        Check(F.Runtime().Used == Before && F.Runtime().Sequence == Sequence && F.Owner().Queue.empty() &&
            F.Runtime().GameplayReserved == 0 && Releases.empty(), "malformed input consumes no allocation, work or reservation");
    }
    auto Terminal = Frame(); Terminal[9] = "2"; Terminal[10] = "76561190000999889"; Terminal[11] = "Killer";
    std::string Unterminated = Pack(Terminal); Unterminated.pop_back();
    Check(cl_domain_event(F.VmId,F.Root,Unterminated.data(),uint32_t(Unterminated.size())) == CL_INVALID_ARGUMENT,
        "missing final NUL rejected without allocation");
    TestAllocationFailureAfter = -1;
    auto Valid = Frame(1); Valid[4] = ""; Valid[6] = "3.4028235E+38";
    Valid[7] = "-3.4028235e38"; Valid[8] = "1.401298E-45";
    Check(F.Submit(Valid) == CL_OK, "Single round-trip maximum/subnormal accepted");
    Valid = Frame(2); Valid[0] = "spawned"; Valid[6] = Valid[7] = Valid[8] = "";
    Check(F.Submit(Valid) == CL_OK, "empty optional position and killer accepted");
    Valid = Frame(3); Valid[9] = "18446744073709551615"; Valid[10] = "76561190000999889"; Valid[11] = "";
    Check(F.Submit(Valid) == CL_OK, "exact optional killer token with valid empty name accepted");
    Valid = Frame(4); Valid[1] = Valid[2] = Valid[5] = "18446744073709551615";
    Valid[4].clear(); for (int Index = 0; Index < 64; ++Index) Valid[4] += "\xc2\xa2";
    Check(F.Submit(Valid) == CL_OK && F.Runtime().GameplayReserved == 4, "canonical uint64 and128-byte UTF-8 boundaries accepted");
}

static void Ownership()
{
    {
        Fixture F("error('must be suppressed')"); RejectGate = true;
        Check(F.Submit(DestroyedFrame(701)) == CL_OK && F.Step() == CL_OK && Releases.count(701) &&
            !F.Runtime().GameplayReserved, "EntityDestroyed fresh gate cancellation refunds exact reservation");
    }
    {
        Fixture F("return");
        Check(F.Submit(DestroyedFrame(702)) == CL_OK && F.Submit(DestroyedFrame(703)) == CL_OK,
            "EntityDestroyed accepted queued snapshots");
        Check(cl_domain_destroy(F.VmId,F.Root) == CL_OK && Releases.size() == 2 &&
            Releases.count(702) && Releases.count(703) && !F.Runtime().GameplayReserved,
            "EntityDestroyed owner retirement cancels without replay or reservation leak");
    }
    for (int Mode = 0; Mode < 7; ++Mode) {
        const char* Body = Mode == 1 ? "error('ordinary')" : Mode == 2 ? "coroutine.yield()" :
            Mode == 3 ? "while true do end" : Mode == 4 ? "buffer.create(16777217)" : "assert(Player.UserId=='76561190000999888')";
        Fixture F(Body);
        Check(F.Submit(Frame(1)) == CL_OK && F.Submit(Frame(1)) == CL_INVALID_ARGUMENT,
            "queued duplicate nonce rejected without consuming ownership");
        RejectGate = Mode == 5; RejectRelease = Mode == 6;
        ClStatus Status = F.Step(Mode == 3 ? 3000000 : 100000000);
        ClStatus Expected = Mode == 1 || Mode == 2 ? CL_RUNTIME_ERROR : Mode == 3 ? CL_TIMEOUT :
            Mode == 4 ? CL_MEMORY_LIMIT : Mode == 6 ? CL_INTERNAL_ERROR : CL_OK;
        Check(Status == Expected && Releases.count(1) == 1 && F.Runtime().GameplayReserved == 0 &&
            F.Runtime().GameplayInFlightReservation == 0 && F.Runtime().GameplayInFlightPayload == nullptr,
            "consume/error/yield/timeout/memory/gate/release-failure return ownership once");
        if (Mode == 3 || Mode == 6) Check(!F.Runtime().State, "fatal path retires VM");
    }
    {
        Fixture F; Check(F.Submit(Frame(1)) == CL_OK, "exception queue"); ThrowGate = true;
        Check(F.Step() == CL_INTERNAL_ERROR && !F.Runtime().State && F.Runtime().GameplayReserved == 0 && Releases.count(1),
            "unexpected pre-entry exception clears in-flight payload before host teardown");
    }
    {
        Fixture F("while true do end");
        Check(F.Submit(Frame(1)) == CL_OK && F.Submit(Frame(2)) == CL_OK, "fatal two-item queue");
        Check(F.Step(3000000) == CL_TIMEOUT && Releases.size() == 2 && F.Runtime().GameplayReserved == 0,
            "fatal retirement releases popped item and undelivered queue without replay");
    }
    {
        Fixture F; Check(F.Submit(Frame(1)) == CL_OK && F.Submit(Frame(2)) == CL_OK, "retirement queue");
        Check(cl_domain_destroy(F.VmId,F.Root) == CL_OK && Releases.size() == 2 && F.Runtime().GameplayReserved == 0,
            "domain retirement releases every reservation before facade host removal");
    }
}

static void Bounds()
{
    Fixture F; ClHandle Other = F.Add("return"), Third = F.Add("return");
    for (uint64_t Index = 1; Index <= 256; ++Index)
        Check(F.Submit(Index % 2 ? Frame(Index) : DestroyedFrame(Index)) == CL_OK, "shared domain256 envelope");
    Check(F.Submit(Frame(257)) == CL_INVALID_ARGUMENT && F.Owner().GameplayReserved == 256,
        "per-domain gameplay quota cannot consume entire global budget");
    Check(F.Submit(Frame(1),Other) == CL_INVALID_ARGUMENT, "nonce is unique across all owners while retained");
    for (uint64_t Index = 257; Index <= 512; ++Index) Check(F.Submit(Frame(Index),Other) == CL_OK, "global512 slots");
    Check(F.Submit(Frame(513),Third) == CL_INVALID_ARGUMENT && F.Runtime().GameplayReserved == 512,
        "global reservation cap rejects before dispatch allocation");
    std::string Legacy = Pack({"added","1","1","76561190000999888","Fixture"});
    Check(cl_domain_event(F.VmId,Third,Legacy.data(),uint32_t(Legacy.size())) == CL_OK &&
        F.Owner(Third).Queue.back().GameplayReservation == 0 && F.Runtime().GameplayReserved == 512,
        "legacy signals remain outside gameplay reservation quota");
    Check(cl_domain_destroy(F.VmId,F.Root) == CL_OK && F.Runtime().GameplayReserved == 256 && Releases.size() == 256,
        "retiring one domain leaves other reservations owned");
    Check(F.Submit(Frame(513),Third) == CL_OK && F.Runtime().GameplayReserved == 257, "quota resumes after actual discard");
}

static void Allocation()
{
    unsigned Failed = 0, Accepted = 0;
    for (int Position = 0; Position < 96; ++Position) {
        Fixture F; TestAllocationFailureAfter = Position;
        auto Status = F.Submit(Frame()); TestAllocationFailureAfter = -1;
        if (Status == CL_OK) { ++Accepted; Check(F.Runtime().GameplayReserved == 1 && Releases.empty(), "accepted allocation retains ownership"); }
        else {
            ++Failed; Check(Status == CL_MEMORY_LIMIT && F.Runtime().GameplayReserved == 0 && Releases.empty(),
                "failed enqueue clears native slot; managed caller owns rejection refund");
        }
    }
    Check(Failed != 0 && Accepted != 0, "enqueue allocation boundary exercised");
    {
        Fixture F; Check(F.Submit(Frame(1)) == CL_OK, "native copy failure has preceding accepted item");
        TestGameplayCopyFailure = true;
        Check(F.Submit(Frame(2)) == CL_MEMORY_LIMIT && !F.Runtime().State && F.Runtime().GameplayReserved == 0 &&
            Releases.size() == 1 && Releases.count(1) && !Releases.count(2),
            "native payload copy failure discards accepted queue but caller refunds failed admission");
        TestGameplayCopyFailure = false;
    }
    for (int Position = 0; Position < 32; ++Position) {
        Fixture F; Check(F.Submit(Frame()) == CL_OK, "callback allocation queue");
        TestAllocationFailureAfter = Position; auto Status = F.Step(); TestAllocationFailureAfter = -1;
        Check((Status == CL_OK || Status == CL_MEMORY_LIMIT || Status == CL_RUNTIME_ERROR) &&
            F.Runtime().GameplayReserved == 0 && Releases.count(1) == 1, "callback allocation failures release accepted reservation");
    }
    for (int Position = 0; Position < 64; ++Position) {
        Fixture F("return");
        F.Run(F.Root, "game:GetService('Workspace').EntityDestroyed:Connect(function(Context) "
            "assert(table.isfrozen(Context) and Context.Position~=nil) end)");
        auto Snapshot = DestroyedFrame(); Snapshot[1] = "2"; Snapshot[4] = std::string(512,'x');
        Check(F.Submit(Snapshot) == CL_OK, "snapshot allocation queue");
        TestAllocationFailureAfter = Position; auto Status = F.Step(); TestAllocationFailureAfter = -1;
        Check((Status == CL_OK || Status == CL_MEMORY_LIMIT || Status == CL_RUNTIME_ERROR) &&
            F.Runtime().GameplayReserved == 0 && Releases.count(1) == 1,
            "EntityDestroyed record/Vector3 allocation failures refund exactly once");
    }
}
inline void RunFaults()
{
    Codec(); Ownership(); Bounds(); Allocation();
    std::printf("[CarbonLuau:GameplayEventNative] PASS strict private codec, quotas, callback ownership and allocation/retirement cleanup\n");
}
}
