#pragma once
#include "../../native/src/runtime/RuntimeInternal.hpp"
#include "GameplayPublicationTests.hpp"
#include <set>

// Actual embedded facade, native event codec, callback roots and scheduler.
// Only the managed host is mocked; no second dispatcher or production export.
namespace CarbonLuau::InventoryChangedTests {
using namespace CarbonLuau::Runtime;
static ClHandle CurrentVm;
static Vm* CurrentRuntime;
static uint64_t NextListener;
static unsigned SignalChecks, Gates;
static unsigned Gives, Takes;
static int64_t InventoryAmount;
static bool Stale, RejectGate;
static std::set<std::string> Listeners;
static std::set<uint64_t> Releases;
static void Check(bool Good, const char* Message)
{
    if (!Good) { std::fprintf(stderr, "[CarbonLuau:InventoryChangedNative] %s\n", Message); std::exit(1); }
}
static std::string Pack(const std::vector<std::string>& Fields)
{
    std::string Result;
    for (const auto& Field : Fields) { Result += Field; Result += '\0'; }
    return Result;
}
static std::vector<std::string> Unpack(const char* Bytes, uint32_t Length)
{
    std::vector<std::string> Fields;
    size_t Start = 0;
    for (size_t End = 0; End < Length; ++End) if (!Bytes[End]) {
        Fields.emplace_back(Bytes + Start, End - Start); Start = End + 1;
    }
    Check(Start == Length, "terminated private host fields"); return Fields;
}
static std::vector<std::string> Frame(uint64_t Reservation = 1)
{
    return {"itemchanged", "1", "17", "76561190000999888", "scrap", std::to_string(Reservation),
        "0", "5", "5", "91", "73", ""};
}
static uint32_t Host(uint64_t OwnerId, uint32_t Operation, const char* Bytes, uint32_t Length,
    char* Output, uint32_t Capacity, uint32_t* Written)
{
    *Written = 0;
    auto Reply = [&](const std::vector<std::string>& Fields) {
        std::string Value = Pack(Fields); Check(Value.size() <= Capacity, "bounded mock response");
        std::memcpy(Output, Value.data(), Value.size()); *Written = uint32_t(Value.size()); return 0u;
    };
    if (Operation == 1) return Reply({"17", "76561190000999888", "Fixture"});
    if (Operation == 3) return Reply({"1", "Fixture"});
    if (Operation == 40) {
        Check(Unpack(Bytes, Length) == std::vector<std::string>({"17", "76561190000999888"}),
            "inventory acquisition uses original connection");
        return Reply({"73"});
    }
    if (Operation == 41) {
        auto Fields = Unpack(Bytes, Length);
        Check(Fields.size() >= 4 && Fields[0] == "17" && Fields[1] == "76561190000999888" && Fields[2] == "73",
            "Inventory41 preserves original connection and publication");
        if (Stale) return 1;
        if (Fields[3] == "signal") {
            Check(Fields.size() == 4, "signal acquisition and Connect exact field count");
            ++SignalChecks; return 0;
        }
        uint32_t Backend = Fields[3] == "count" ? 26 : Fields[3] == "has" ? 27 :
            Fields[3] == "take" ? 29 : Fields[3] == "give" ? 30 : 0;
        Check(Backend && Fields.size() == (Backend == 26 ? 5u : Backend == 30 ? 7u : 6u),
            "Inventory41 delegates only qualified backend shapes");
        std::vector<std::string> Request{Fields[0], Fields[1]};
        Request.insert(Request.end(), Fields.begin() + 4, Fields.end());
        auto Payload = Pack(Request);
        return Host(OwnerId, Backend, Payload.data(), uint32_t(Payload.size()), Output, Capacity, Written);
    }
    if (Operation == 26 || Operation == 27 || Operation == 29 || Operation == 30) {
        auto Fields = Unpack(Bytes, Length);
        Check(Fields.size() == (Operation == 26 ? 3u : Operation == 30 ? 5u : 4u) &&
            Fields[0] == "17" && Fields[1] == "76561190000999888" && Fields[2] == "scrap",
            "qualified backend receives original token/UserId/short name");
        if (Operation == 26) return Reply({std::to_string(InventoryAmount)});
        int64_t Amount = std::stoll(Fields[3]); Check(Amount > 0, "positive exact backend amount");
        if (Operation == 27) return Reply({InventoryAmount >= Amount ? "1" : "0"});
        if (Operation == 30) {
            Check(Fields[4] == "InventoryOnly", "optional/default typed behavior retains backend spelling");
            ++Gives; InventoryAmount += Amount; return Reply({"1"});
        }
        ++Takes; if (InventoryAmount < Amount) return Reply({"0"});
        InventoryAmount -= Amount; return Reply({"1"});
    }
    if (Operation == 6) {
        auto Fields = Unpack(Bytes, Length);
        if (Fields[0] == "itemchanged")
            Check(Fields == std::vector<std::string>({"itemchanged", "17", "76561190000999888", "73"}),
                "targeted Host6 carries original token/UserId/publication");
        else Check(Fields.size() == 1, "existing generic Signals keep one-field Host6");
        auto Id = std::to_string(++NextListener); Listeners.insert(Id); return Reply({Id});
    }
    if (Operation == 7) {
        auto Fields = Unpack(Bytes, Length); Check(Fields.size() == 1, "disconnect only listener identity");
        Listeners.erase(Fields[0]); return 0;
    }
    if (Operation == 9) {
        auto Fields = Unpack(Bytes, Length);
        if (Fields[0] != "itemchanged") return 0;
        ++Gates;
        Check(CurrentRuntime->GameplayInFlightOwner && CurrentRuntime->GameplayInFlightOwner->Id == OwnerId &&
            CurrentRuntime->GameplayInFlightReservation == std::stoull(Fields[5]) &&
            CurrentRuntime->GameplayInFlightPayload && CurrentRuntime->GameplayInFlightPayload->size() == Length,
            "fresh gate holds exact original payload and D23 reservation");
        Check(cl_domain_event(CurrentVm, OwnerId, Bytes, Length) == CL_INVALID_ARGUMENT,
            "gate cannot recursively enqueue original reservation");
        return RejectGate || Stale || !Listeners.count(Fields[1]) || Fields[2] != "17" ||
            Fields[3] != "76561190000999888" || Fields[9] != "91" || Fields[10] != "73" ? 1u : 0u;
    }
    if (Operation == 39) {
        auto Fields = Unpack(Bytes, Length); Check(Fields.size() == 1, "existing reservation transfer39 shape");
        uint64_t Reservation = std::stoull(Fields[0]);
        Check(Releases.insert(Reservation).second && CurrentRuntime->GameplayInFlightReservation != Reservation,
            "accepted reservation acknowledged once after in-flight payload detaches");
        for (const auto& Slot : CurrentRuntime->GameplayReservations)
            Check(Slot.Nonce != Reservation, "native slot released before acknowledgement");
    }
    return 0;
}
struct Fixture {
    ClHandle VmId = 0, Root = 0;
    Fixture(const char* Body = "State.Contexts[#State.Contexts+1]=Context", const char* Cold = nullptr) {
        NextListener = SignalChecks = Gates = 0; Stale = RejectGate = false;
        Gives = Takes = 0; InventoryAmount = 0;
        Listeners.clear(); Releases.clear(); TestAllocationFailureAfter = -1; TestGameplayCopyFailure = false;
        ClVmConfig Config{16 * MiB}; Check(cl_vm_create(&Config, &VmId) == CL_OK, "real VM");
        CurrentVm = VmId; CurrentRuntime = GetVm(VmId);
        Check(cl_domain_create(VmId, 4096, &Root) == CL_OK && cl_domain_facade(VmId, Root, Host) == CL_OK,
            "domain-bound script Host mock with actual bootstrap");
        std::string Module = std::string("local Player=game:GetService('Players'):GetPlayers()[1]; ") +
            "local State={Player=Player,Inventory=Player:GetInventory(),Contexts={}}; State.Signal=State.Inventory.ItemChanged; " +
            "State.Connection=State.Signal:Connect(function(Context) " + Body + " end); return State";
        Check(cl_domain_module(VmId, Root, "state", Module.data(), uint32_t(Module.size())) == CL_OK, "state module");
        if (Cold) Check(cl_domain_module(VmId, Root, "c4cold", Cold, uint32_t(std::strlen(Cold))) == CL_OK, "C4 cold module before commit");
        Run("require('state')"); Check(cl_domain_commit(VmId, Root) == CL_OK, "committed listener owner");
    }
    ~Fixture() {
        TestAllocationFailureAfter = -1; TestGameplayCopyFailure = false;
        Check(cl_vm_destroy(VmId) == CL_OK && !TestLiveBytes, "fixture teardown has zero Lua allocator bytes");
        CurrentRuntime = nullptr;
    }
    Vm& Runtime() { return *GetVm(VmId); }
    Domain& Owner() { return *GetDomain(Runtime(), Root); }
    void Run(const std::string& Source) {
        ClHandle Thread = 0; ClResult Result{};
        Check(cl_domain_load_source(VmId, Root, "inventory.changed.fixture", Source.data(), uint32_t(Source.size()),
            &Thread, &Result) == CL_OK, "compile actual public API fixture");
        auto Status = cl_thread_resume(Thread, 100000000, &Result);
        if (Status != CL_OK) std::fprintf(stderr, "[CarbonLuau:InventoryChangedNative] %s\n", Result.Error);
        Check(Status == CL_OK && cl_thread_destroy(Thread) == CL_OK, "execute actual public API fixture");
    }
    ClStatus Submit(const std::vector<std::string>& Fields) {
        std::string Payload = Pack(Fields);
        return cl_domain_event(VmId, Root, Payload.data(), uint32_t(Payload.size()));
    }
    ClStatus Step(uint64_t Budget = 100000000) {
        ClSchedulerInfo Info{}; Check(cl_vm_scheduler(VmId, &Info) == CL_OK, "scheduler snapshot");
        uint32_t Ran = 0; ClResult Result{};
        auto Status = cl_vm_callback(VmId, Info.NowNs, Info.Sequence, Budget, &Ran, &Result);
        Check(Ran == 1, "one bounded callback attempted"); return Status;
    }
};
static void Codec()
{
    Fixture F;
    uint64_t Used = F.Runtime().Used, Sequence = F.Runtime().Sequence;
    TestAllocationFailureAfter = 0;
    for (const auto& Change : std::vector<std::pair<unsigned, std::string>>{
        {0,"itemadded"},{0,"itemremoved"},{1,"0"},{1,"01"},{2,"0"},{2,"+17"},{2,"18446744073709551616"},
        {3,""},{3,"765x"},{3,std::string(21,'1')},{4,""},{4,"Scrap"},{4," scrap"},{4,"scrap "},
        {4,"\tscrap"},{4,"scrap\r"},{4,std::string(129,'a')},{4,std::string("\xc2\xa2",2)},
        {4,std::string("sc\0rap",6)},{5,"0"},{5,"01"},{5,"18446744073709551616"},
        {6,"00"},{6,"01"},{6,"-1"},{6,"+0"},{6,"-0"},{6,"1.0"},{6,"1e0"},{6," 0"},
        {6,"9007199254740992"},{6,"18446744073709551616"},{7,"05"},{7,"-5"},{7,"5.0"},
        {7,"NaN"},{7,"Infinity"},{7,"9007199254740992"},{8,"0"},{8,"-0"},{8,"+5"},{8,"05"},
        {8,"-05"},{8,"--5"},{8,"5e0"},{8,"5.0"},{8,"5 "},{8,"9007199254740992"},
        {8,"-9007199254740992"},{8,"-5"},{8,"4"},{9,"0"},{9,"01"},{9,"18446744073709551616"},
        {10,"0"},{10,"01"},{10,"+73"},{10,"18446744073709551616"},{11,"private"}}) {
        auto Bad = Frame(); Bad[Change.first] = Change.second;
        Check(F.Submit(Bad) == CL_INVALID_ARGUMENT && F.Runtime().Used == Used &&
            F.Runtime().Sequence == Sequence && F.Owner().Queue.empty() && !F.Runtime().GameplayReserved && Releases.empty(),
            "malformed codec rejected before allocation, sequence or reservation");
    }
    auto Missing = Frame(); Missing.pop_back(); auto Extra = Frame(); Extra.push_back("");
    auto Zero = Frame(); Zero[6] = Zero[7] = Zero[8] = "0";
    auto Oversized = Frame(); Oversized[4] = std::string(2048,'a');
    Check(F.Submit(Missing) == CL_INVALID_ARGUMENT && F.Submit(Extra) == CL_INVALID_ARGUMENT &&
        F.Submit(Zero) == CL_INVALID_ARGUMENT && F.Submit(Oversized) == CL_INVALID_ARGUMENT, "exact12 and nonzero delta");
    std::string Unterminated = Pack(Frame()); Unterminated.pop_back();
    Check(cl_domain_event(F.VmId,F.Root,Unterminated.data(),uint32_t(Unterminated.size())) == CL_INVALID_ARGUMENT,
        "missing final NUL rejected");
    TestAllocationFailureAfter = -1;
    for (int Mode = 0; Mode < 3; ++Mode) {
        auto Good = Frame(uint64_t(Mode + 1));
        if (Mode == 0) { Good[7] = Good[8] = "9007199254740991"; Good[4] = std::string(128,'a'); }
        if (Mode == 1) { Good[6] = "9007199254740991"; Good[7] = "0"; Good[8] = "-9007199254740991"; }
        if (Mode == 2) { Good[6] = "9007199254740991"; Good[7] = "9007199254740990"; Good[8] = "-1"; }
        Check(F.Submit(Good) == CL_OK && F.Step() == CL_OK && Releases.count(uint64_t(Mode + 1)),
            "maximum exact quantities and positive/negative delta boundaries");
    }
    F.Run("local C=require('state').Contexts; assert(#C==3); "
        "assert(C[1].Before==0 and C[1].After==9007199254740991 and C[1].Delta==9007199254740991); "
        "assert(C[2].Before==9007199254740991 and C[2].After==0 and C[2].Delta==-9007199254740991); "
        "assert(C[3].Before==9007199254740991 and C[3].After==9007199254740990 and C[3].Delta==-1)");
    auto Boundary = Frame(4); Boundary[1] = Boundary[2] = Boundary[5] = Boundary[9] = Boundary[10] = "18446744073709551615";
    Boundary[3] = "00000000000000000000"; Boundary[4] = std::string("a\tb\x7f",4);
    Check(F.Submit(Boundary) == CL_OK && F.Step() == CL_OK, "uint64 identities and existing UserId/item validator parity");
}
static void PublicContext()
{
    Fixture F;
    Check(SignalChecks == 2 && NextListener == 1 && F.Owner().Queue.empty(), "member and Connect check without synthetic replay");
    F.Run("local S=require('state'); assert(table.isfrozen(S.Signal) and table.isfrozen(S.Connection)); "
        "assert(S.Signal==S.Inventory.ItemChanged); "
        "assert(not pcall(function() S.Inventory.ItemChanged={} end)); "
        "assert(not pcall(function() S.Signal.Connect(function() end) end)); "
        "assert(not pcall(function() return S.Inventory.ItemAdded end)); "
        "assert(not pcall(function() return S.Inventory.ItemRemoved end))");
    Check(F.Submit(Frame()) == CL_OK && F.Submit(Frame()) == CL_INVALID_ARGUMENT && F.Step() == CL_OK, "existing reserved admission");
    F.Run("local C=require('state').Contexts[1]; assert(type(C)=='table' and table.isfrozen(C) and getmetatable(C)==nil); "
        "assert(C.Item=='scrap' and C.Before==0 and C.After==5 and C.Delta==5); local N=0; "
        "for K in C do assert(K=='Item' or K=='Before' or K=='After' or K=='Delta'); N+=1 end; assert(N==4); "
        "assert(C.Player==nil and C.ListenerId==nil and C.Token==nil and C.UserId==nil and C.Reservation==nil "
        "and C.DemandId==nil and C.Publication==nil); assert(not pcall(function() C.Delta=99 end))");
    Stale = true; uint64_t BeforeListeners = NextListener;
    F.Run("local S=require('state'); assert(not pcall(function() return S.Inventory.ItemChanged end)); "
        "assert(not pcall(function() S.Signal:Connect(function() end) end))");
    Check(NextListener == BeforeListeners, "cached stale signal cannot register another listener");
    Check(F.Submit(Frame(2)) == CL_OK && F.Step() == CL_OK && Releases.count(2), "stale exact connection rejected by fresh gate");
    Stale = false;
    F.Run("assert(#require('state').Contexts==1); require('state').Connection:Disconnect()");
    Check(F.Submit(Frame(3)) == CL_OK && F.Step() == CL_OK && Releases.count(3), "disconnected callback drops and refunds");
    F.Run("assert(#require('state').Contexts==1); game:GetService('Players').PlayerAdded:Connect(function(P) assert(P.UserId) end)");
    Check(NextListener == 2, "generic Signal retains existing request shape");
}
static void Ownership()
{
    for (int Mode = 0; Mode < 7; ++Mode) {
        const char* Body = Mode == 1 ? "error('ordinary')" : Mode == 2 ? "coroutine.yield()" :
            Mode == 3 ? "while true do end" : Mode == 4 ? "buffer.create(16777217)" : "assert(table.isfrozen(Context))";
        Fixture F(Body); Check(F.Submit(Frame()) == CL_OK, "accepted reservation");
        if (Mode == 6) {
            Check(cl_domain_destroy(F.VmId,F.Root) == CL_OK && Releases.count(1) && !F.Runtime().GameplayReserved,
                "domain retirement discards original reservation"); continue;
        }
        RejectGate = Mode == 5;
        auto Status = F.Step(Mode == 3 ? 3000000 : 100000000);
        Check(Status == (Mode == 1 || Mode == 2 ? CL_RUNTIME_ERROR : Mode == 3 ? CL_TIMEOUT :
            Mode == 4 ? CL_MEMORY_LIMIT : CL_OK) && Releases.size() == 1 && Releases.count(1) &&
            !F.Runtime().GameplayReserved && !F.Runtime().GameplayInFlightReservation && !F.Runtime().GameplayInFlightPayload,
            "normal/error/yield/fatal/gate cancellation consumes original reservation once");
    }
    for (unsigned Field : {2u,3u,9u,10u}) {
        Fixture F("error('stale target must not dispatch')"); auto Wrong = Frame(); Wrong[Field] = "92";
        Check(F.Submit(Wrong) == CL_OK && F.Step() == CL_OK && Releases.count(1),
            "fresh host gate suppresses mismatched player/demand/publication authority");
    }
    Fixture F;
    Check(ControlPublication(F.Runtime(), F.Owner(), 10), "snapshot existing roots");
    F.Run("require('state').Signal:Connect(function() error('rolled back') end)");
    Check(CarbonLuau::GameplayPublicationTests::Roots(F.Runtime().State,F.Owner().FacadeCallbackRoots) == 2,
        "targeted signal shares existing callback roots");
    Check(ControlPublication(F.Runtime(), F.Owner(), 12) &&
        CarbonLuau::GameplayPublicationTests::Roots(F.Runtime().State,F.Owner().FacadeCallbackRoots) == 1,
        "D7 rollback restores exact prior callback roots");
    auto RolledBack = Frame(); RolledBack[1] = "2";
    Check(F.Submit(RolledBack) == CL_OK && F.Step() == CL_OK, "missing rolled-back callback never dispatches");
    F.Run("assert(#require('state').Contexts==0)");
    Check(ControlPublication(F.Runtime(), F.Owner(), 10), "snapshot for successful publication");
    F.Run("require('state').Signal:Connect(function(Context) assert(table.isfrozen(Context)) end)");
    Check(ControlPublication(F.Runtime(), F.Owner(), 11) &&
        CarbonLuau::GameplayPublicationTests::Roots(F.Runtime().State,F.Owner().FacadeCallbackRoots) == 2,
        "D7 commit retains new targeted callback and clears snapshot");
    auto Committed = Frame(2); Committed[1] = "3";
    Check(F.Submit(Committed) == CL_OK && F.Step() == CL_OK && Releases.count(2), "committed targeted root dispatches");
}
static void Bounds()
{
    Fixture F;
    for (uint64_t Index = 1; Index <= 256; ++Index) {
        auto Fields = Frame(Index);
        if (Index % 2 == 0)
            Fields = {"entitydestroyed", "1", "41", "9", "assets/fixture.prefab", std::to_string(Index), "", "", "", "", "", ""};
        Check(F.Submit(Fields) == CL_OK, "inventory and Entity snapshots share D23 owner quota");
    }
    Check(F.Submit(Frame(257)) == CL_INVALID_ARGUMENT && F.Owner().GameplayReserved == 256 &&
        F.Runtime().GameplayReserved == 256, "no independent inventory reservation queue");
    Check(cl_domain_destroy(F.VmId,F.Root) == CL_OK && !F.Runtime().GameplayReserved && Releases.size() == 256,
        "mixed gameplay cancellation returns all existing reservations");
}
static void Allocation()
{
    unsigned Failed = 0, Accepted = 0;
    for (int Position = 0; Position < 96; ++Position) {
        Fixture F;
        // Unlike B2's 512-byte prefab, even a fresh maximum 128-byte item
        // occupies a small-object page. Retain unique valid payloads up to the
        // existing 256-reservation owner cap so interned strings/free slots
        // cannot hide every backing allocation from the fault injector.
        constexpr unsigned Envelope = 256;
        unsigned Queued = 0;
        ClStatus Status = CL_OK;
        TestAllocationFailureAfter = Position;
        for (unsigned Index = 1; Index <= Envelope; ++Index) {
            auto Fields = Frame(Index);
            std::string Prefix = "inventoryallocation_" + std::to_string(Position) + "_" + std::to_string(Index) + "_";
            Fields[4] = Prefix + std::string(128 - Prefix.size(), 'z');
            Fields[6] = "9007199254740990"; Fields[7] = "9007199254740991"; Fields[8] = "1";
            Status = F.Submit(Fields);
            if (Status != CL_OK) break;
            ++Queued;
        }
        TestAllocationFailureAfter = -1;
        if (Status == CL_OK) {
            ++Accepted;
            Check(Queued == Envelope && F.Runtime().GameplayReserved == Envelope && Releases.empty(),
                "accepted allocation retains every bounded reservation");
            Check(cl_domain_destroy(F.VmId,F.Root) == CL_OK && !F.Runtime().GameplayReserved,
                "successful allocation trial releases queued owner reservations");
        } else {
            ++Failed;
            Check(Status == CL_MEMORY_LIMIT && !F.Runtime().State && !F.Runtime().GameplayReserved &&
                !Releases.count(uint64_t(Queued + 1)), "failed native enqueue leaves rejected admission refund to managed caller");
        }
        Check(Releases.size() == Queued, "allocation trial releases each prior accepted reservation once");
        for (unsigned Index = 1; Index <= Queued; ++Index)
            Check(Releases.count(Index) == 1, "allocation cleanup preserves exact accepted reservation identities");
    }
    Check(Failed && Accepted, "enqueue allocation boundary exercises refusal and success");
    {
        Fixture F; Check(F.Submit(Frame()) == CL_OK, "existing queued reservation"); TestGameplayCopyFailure = true;
        Check(F.Submit(Frame(2)) == CL_MEMORY_LIMIT && !F.Runtime().State && !F.Runtime().GameplayReserved &&
            Releases.size() == 1 && Releases.count(1) && !Releases.count(2), "native copy failure retires accepted work; caller refunds rejection");
        TestGameplayCopyFailure = false;
    }
    for (int Position = 0; Position < 64; ++Position) {
        Fixture F("assert(table.isfrozen(Context) and Context.Delta==5)");
        Check(F.Submit(Frame()) == CL_OK, "context allocation queue");
        TestAllocationFailureAfter = Position; auto Status = F.Step(); TestAllocationFailureAfter = -1;
        Check((Status == CL_OK || Status == CL_MEMORY_LIMIT || Status == CL_RUNTIME_ERROR) &&
            !F.Runtime().GameplayReserved && Releases.size() == 1 && Releases.count(1),
            "frozen context allocation faults release original reservation exactly once");
    }
}
static void Migration()
{
    const char* Cold = "local I=require('state').Inventory; for _,M in {'GiveItem','TakeItem'} do local Ok,Error=pcall(function() I[M](I,'scrap',1) end); "
        "assert(not Ok and string.find(Error,'requires a committed domain',1,true)) end; return true";
    Fixture F("State.Contexts[#State.Contexts+1]=Context", Cold);
    F.Run("local S=require('state'); local I=S.Inventory; assert(game.ApiVersion=='0.7.0-experimental'); "
        "for _,K in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(S.Player[K]==nil) end; "
        "assert(I:GiveItem('scrap',2)); assert(I:GiveItem('scrap',3,GiveItemBehavior.InventoryOnly)); "
        "assert(I:GiveItem('scrap',1,nil)); assert(I:CountItem('scrap')==6 and I:HasItem('scrap') and I:HasItem('scrap',6)); "
        "assert(I:TakeItem('scrap',2)); assert(I:CountItem('scrap')==4 and not I:TakeItem('scrap',5)); "
        "for _,V in {true,false,'InventoryOnly',Enum.DropMode.Replace,{},1} do assert(not pcall(function() I:GiveItem('scrap',1,V) end)) end; "
        "assert(not pcall(function() I.GiveItem({},'scrap',1) end)); assert(not pcall(function() I.TakeItem({},'scrap',1) end))");
    Check(Gives == 3 && Takes == 2 && InventoryAmount == 4, "raw embedded bootstrap delegates41 to backend29/30 without duplicate calls");
    F.Run("assert(require('c4cold'))");
    Check(Gives == 3 && Takes == 2, "native first-load provisional rejection before managed mutation delegation");
    Stale = true;
    F.Run("local I=require('state').Inventory; for _,M in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(not pcall(function() I[M](I,'scrap',1) end)) end");
    Check(Gives == 3 && Takes == 2, "stale captured Inventory never reaches backend mutation");
}
inline void RunFaults()
{
    Migration(); Codec(); PublicContext(); Ownership(); Bounds(); Allocation();
    std::puts("[CarbonLuau:InventoryChangedNative] PASS exact codec, bound Signal, frozen context, publication and allocation cleanup");
}
}
