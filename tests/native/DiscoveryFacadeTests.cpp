#include "../../native/src/runtime/RuntimeInternal.hpp"
#include "../../native/src/scripts/Compiler.hpp"
#include <set>

using namespace CarbonLuau::Runtime;
namespace {
void Check(bool Good, const char* Message)
{
    if (!Good) { std::fprintf(stderr, "[CarbonLuau:DiscoveryTest] %s\n", Message); std::exit(1); }
}
struct Request { uint64_t Owner, Route; std::vector<std::string> Fields; };
std::vector<Request> Requests;
std::set<std::pair<uint64_t,uint64_t>> Released;
ClHandle CurrentVm;
unsigned Fetches, Lookups;
bool Reject, Stale, ExpireFetch, Reenter, Malformed, ReleaseFailure;
unsigned Results = 2;

std::vector<std::string> Fields(const char* Bytes, size_t Length)
{
    std::vector<std::string> Result;
    size_t Start = 0;
    for (size_t End = 0; End < Length; ++End) if (!Bytes[End]) {
        Result.emplace_back(Bytes + Start, End - Start); Start = End + 1;
    }
    Check(Start == Length, "exact textual frame"); return Result;
}
void Respond(const std::vector<std::string>& Values, char* Output, uint32_t Capacity, uint32_t* Written)
{
    for (const auto& Value : Values) {
        Check(*Written + Value.size() + 1 <= Capacity, "response capacity");
        std::memcpy(Output + *Written, Value.c_str(), Value.size() + 1); *Written += uint32_t(Value.size() + 1);
    }
}
uint32_t Host(uint64_t OwnerId, uint32_t Operation, const char* Bytes, uint32_t Length,
    char* Output, uint32_t Capacity, uint32_t* Written)
{
    *Written = 0;
    if (Operation != 36 && Operation != 37 && Operation != 38 && Operation != 34 && Operation != 35) return 0;
    auto Input = Fields(Bytes, Length);
    if (Operation == 36) {
        Check(Input.size() == 7 && Length < 1024, "bounded seven-field snapshot");
        Vm* Runtime = GetVm(CurrentVm); Domain* Owner = GetDomain(*Runtime, OwnerId, true);
        Check(Owner && CanDispatchDiscovery(*Runtime, *Owner) && Runtime->DiscoveryReserved != 0,
            "native owner admission and reservation precede host Submit");
        uint64_t Route = std::stoull(Input[0]);
        Requests.push_back(Request{OwnerId, Route, Input});
        if (Reenter) {
            std::string Payload = std::string("discovery\0", 10) + Input[0] + '\0';
            Check(cl_domain_event(CurrentVm, OwnerId, Payload.data(), uint32_t(Payload.size())) == CL_INVALID_ARGUMENT,
                "ready rejects recursive host-driven admission");
        }
        return Reject ? 1 : 0;
    }
    if (Operation == 37) {
        Check(Input.size() == 1 && Length <= 21, "bounded allocation-free route release");
        Check(Released.emplace(OwnerId, std::stoull(Input[0])).second, "release at most once");
        return ReleaseFailure ? 1 : 0;
    }
    if (Operation == 38) {
        Check(Input.size() == 1, "private fetch route only");
        Vm* Runtime = GetVm(CurrentVm); Domain* Owner = GetDomain(*Runtime, OwnerId, true);
        Check(Owner && CanDispatchDiscovery(*Runtime, *Owner) && Runtime->Admission &&
            Runtime->Admission->Owner == Owner && std::chrono::steady_clock::now() < Runtime->Deadline &&
            !Released.count({OwnerId, std::stoull(Input[0])}), "actual callback admission precedes fetch and release");
        ++Fetches;
        if (ExpireFetch) Runtime->Deadline = std::chrono::steady_clock::now() - std::chrono::seconds(1);
        if (Stale) { Respond({"DiscoveryStaleResult"}, Output, Capacity, Written); return 0; }
        std::vector<std::string> OutputFields{""};
        for (unsigned Index = 0; Index < Results; ++Index)
            for (const auto& Field : {std::string("77"), std::to_string(41 + Index), std::to_string(1 + Index), std::string("1")})
                OutputFields.push_back(Field);
        if (Malformed) OutputFields.push_back("extra");
        Respond(OutputFields, Output, Capacity, Written); return 0;
    }
    if (Operation == 34) { ++Lookups; Respond({"77", "41", "1", "1"}, Output, Capacity, Written); }
    if (Operation == 35) {
        Check(Input.size() == 3, "exact Entity property witness");
        if (Input[2] == "Id") Respond({std::to_string(std::stoull(Input[0]) - 40)}, Output, Capacity, Written);
        else if (Input[2] == "Prefab") Respond({"assets/fixture.prefab"}, Output, Capacity, Written);
        else Respond({"1", "2", "3"}, Output, Capacity, Written);
    }
    return 0;
}

struct Fixture {
    ClHandle Vm = 0, Root = 0;
    Fixture() {
        Requests.clear(); Released.clear(); Fetches = Lookups = 0;
        Reject = Stale = ExpireFetch = Reenter = Malformed = ReleaseFailure = false; Results = 2;
        ClVmConfig Config{16 * MiB};
        Check(cl_vm_create(&Config, &Vm) == CL_OK && cl_vm_scripts(Vm, 2) == CL_OK, "create real VM");
        CurrentVm = Vm;
        Check(cl_domain_create(Vm, 2, &Root) == CL_OK && cl_domain_facade(Vm, Root, Host) == CL_OK, "install trusted facade");
        Module(Root, "state", "return {}");
        Module(Root, "cold", "local W=game:GetService('Workspace'); assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end)); return W");
        Module(Root, "failed", "local S=require('state'); local W=game:GetService('Workspace'); S.Leaked=W; assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end)); error('fail')");
        Run(Root, "require('state')");
        Check(cl_domain_commit(Vm, Root) == CL_OK, "commit root");
    }
    ~Fixture() {
        TestAllocationFailureAfter = -1; ReleaseFailure = false;
        Check(cl_vm_destroy(Vm) == CL_OK && !TestLiveBytes, "no retained VM callback roots");
        Check(Released.size() == Requests.size(), "all submitted routes released");
    }
    Domain& Owner(ClHandle Id) { return *GetDomain(*GetVm(Vm), Id); }
    void Module(ClHandle Id, const char* Name, const std::string& Source) {
        Check(cl_domain_module(Vm, Id, Name, Source.data(), uint32_t(Source.size())) == CL_OK, "fixture module");
    }
    ClHandle Addon(const char* Name) {
        ClHandle Id = 0;
        Check(cl_domain_create(Vm, 2, &Id) == CL_OK && cl_domain_addon(Vm, Id, Name, "1.0.0", "") == CL_OK &&
            cl_domain_facade(Vm, Id, Host) == CL_OK, "fixture addon");
        Module(Id, "state", "return {}"); return Id;
    }
    void Run(ClHandle Id, const std::string& Source, ClStatus Expected = CL_OK) {
        ClHandle Thread = 0; ClResult Result{};
        Check(cl_domain_load_source(Vm, Id, "discovery.fixture", Source.data(), uint32_t(Source.size()), &Thread, &Result) == CL_OK, "load fixture source");
        auto Status = cl_thread_resume(Thread, 100000000, &Result);
        if (Status != Expected) std::fprintf(stderr, "[CarbonLuau:DiscoveryTest] expected=%u got=%u %s\n", Expected, Status, Result.Error);
        Check(Status == Expected, "execute fixture source");
        Check(cl_thread_destroy(Thread) == ((Result.Flags & 1) ? CL_INVALID_ARGUMENT : CL_OK), "release source thread");
    }
    void Run(const std::string& Source, ClStatus Expected = CL_OK) { Run(Root, Source, Expected); }
    ClStatus Ready(size_t Index) {
        const auto& Request = Requests.at(Index);
        std::string Payload = std::string("discovery\0", 10) + std::to_string(Request.Route) + '\0';
        return cl_domain_event(Vm, Request.Owner, Payload.data(), uint32_t(Payload.size()));
    }
    ClSchedulerInfo Info() { ClSchedulerInfo Result{}; Check(cl_vm_scheduler(Vm, &Result) == CL_OK, "ready scheduler snapshot"); return Result; }
    void Step(const ClSchedulerInfo& Cutoff, bool ExpectedRan = true, ClStatus Expected = CL_OK, uint64_t Budget = 100000000) {
        uint32_t Ran = 0; ClResult Result{};
        auto Status = cl_vm_callback(Vm, Cutoff.NowNs, Cutoff.Sequence, Budget, &Ran, &Result);
        if (Status != Expected) std::fprintf(stderr, "[CarbonLuau:DiscoveryTest] callback expected=%u got=%u %s\n", Expected, Status, Result.Error);
        Check(Status == Expected && bool(Ran) == ExpectedRan, "callback result");
    }
};
const std::string Prefix = "local S=require('state'); local W=game:GetService('Workspace'); ";
const std::string Submit = "W:GetEntitiesInRadiusAsync(Vector3.new(1.25,-2.5,3.75),4,function(E,Error) assert(E and not Error and #E==2); S.Done=(S.Done or 0)+1 end)";

void ArgumentsAndSnapshot()
{
    Fixture F; Reenter = true;
    F.Run(Prefix + R"(
        local Hits=0
        local Meta={__index=function() Hits+=1; return 2 end,__iter=function() Hits+=1; return next,{},nil end}
        for _,Options in {setmetatable({},Meta),setmetatable({Limit=1},{__metatable=false}),
            {Limit=0},{Limit=257},{Limit=1.5},{Limit='2'},{Limit=0/0},{Limit=math.huge},
            {Prefab=''},{Prefab='a\0b'},{Prefab=string.rep('a',513)},{Prefab=string.char(255)},
            {Prefab=string.char(192,128)},{Prefab=string.char(237,160,128)},{Unknown=1},
            {Prefab='x',Limit=1,Third=3},{[1]=1}} do
            assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end,Options) end))
        end
        for _,Position in {{X=0,Y=0,Z=0},setmetatable({},{__tostring=function() Hits+=1; return 'Vector3' end}),Vector2.new(0,0),'Vector3'} do
            assert(not pcall(function() W:GetEntitiesInRadiusAsync(Position,1,function() end) end))
        end
        for _,Radius in {-1,0/0,math.huge,-math.huge,'1'} do
            assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),Radius,function() end) end))
        end
        assert(not pcall(function() W.GetEntitiesInRadiusAsync({},Vector3.new(0,0,0),1,function() end) end))
        assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,nil) end))
        assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end,nil,1) end))
        assert(Hits==0)
        local O={Prefab='assets/é.prefab',Limit=256}
        assert(select('#',W:GetEntitiesInRadiusAsync(Vector3.new(1.25,-2.5,3.75),-0.0,function(E,Err)
            assert(E and not Err and #E==2 and E[1].Id=='1' and E[2].Id=='2')
            assert(E[1]==W:GetEntityById('1') and E[1].Position==Vector3.new(1,2,3)); S.Done=true
        end,O))==0)
        O.Prefab='changed'; O.Limit=1
        assert(S.Done==nil)
    )");
    Check(Requests.size() == 1 && Requests[0].Fields[1] == "1.25" && Requests[0].Fields[2] == "-2.5" &&
        Requests[0].Fields[3] == "3.75" && Requests[0].Fields[4] == "0" && Requests[0].Fields[5] == "assets/é.prefab" &&
        Requests[0].Fields[6] == "256", "immutable exact prefab/numeric/option snapshot");
    Check(F.Ready(0) == CL_OK && F.Ready(0) == CL_INVALID_ARGUMENT, "duplicate ready rejected");
    Check(Fetches == 0 && GetVm(F.Vm)->DiscoveryReserved == 1, "ready does not fetch or release");
    F.Step(F.Info());
    Check(Fetches == 1 && Lookups == 1 && GetVm(F.Vm)->DiscoveryReserved == 0, "actual fetch, factory; no retargeting lookup");
    F.Run(Prefix + "assert(S.Done)");
    Check(F.Ready(0) == CL_INVALID_ARGUMENT, "completion cannot replay");
}

void ReservationsAndReadyRaces()
{
    Fixture F;
    F.Run(Prefix + Submit + "; " + Submit + "; assert(not pcall(function() " + Submit + " end)); " +
        "task.defer(function() end); task.defer(function() end)");
    Check(Requests.size() == 2 && GetVm(F.Vm)->DiscoveryReserved == 2, "fixed domain two-slot bound");
    Check(F.Ready(1) == CL_OK, "ready survives normal queue saturation");
    auto Before = F.Info(); Check(Before.Queued == 4 && Before.NextDueNs, "ready and traversal reservations contribute to Info/due");
    Check(F.Ready(0) == CL_OK, "first request can become ready later");
    F.Step(Before); F.Step(Before); F.Step(Before); F.Step(Before, false);
    Check(Fetches == 1 && GetVm(F.Vm)->DiscoveryReserved == 1, "sequence cutoff holds later-ready request");
    F.Step(F.Info()); Check(Fetches == 2 && GetVm(F.Vm)->DiscoveryReserved == 0, "both independently admitted");
    F.Run(Prefix + "assert(S.Done==2)");

    for (unsigned I = 0; I < 4; ++I) {
        std::string Name = "owner" + std::to_string(I); auto Id = F.Addon(Name.c_str());
        F.Run(Id, "require('state')"); Check(cl_domain_commit(F.Vm, Id) == CL_OK, "commit capacity owner");
        F.Run(Id, Prefix + Submit + ";" + Submit);
    }
    Check(GetVm(F.Vm)->DiscoveryReserved == 8, "global eight reservations");
    F.Run(Prefix + "assert(not pcall(function() " + Submit + " end))");
    auto& Slot = F.Owner(F.Root).DiscoveryCallbacks[0];
    Check(!Slot.Route, "global rejection leaves no root slot");
    std::string Unknown("discovery\0" "99999\0", 16);
    Check(cl_domain_event(F.Vm, F.Root, Unknown.data(), uint32_t(Unknown.size())) == CL_INVALID_ARGUMENT, "unknown route cannot create reservation");
    for (const auto& Payload : {std::string("discovery\0" "01\0", 13), std::string("discovery\0" "1\0" "extra\0", 18),
        std::string("discovery\0" "18446744073709551616\0", 31)})
        Check(cl_domain_event(F.Vm, F.Root, Payload.data(), uint32_t(Payload.size())) == CL_INVALID_ARGUMENT, "strict ready payload");
    auto Request = Requests.back();
    Check(cl_domain_destroy(F.Vm, Request.Owner) == CL_OK && GetVm(F.Vm)->DiscoveryReserved == 6, "owner retirement frees both roots");
    Check(F.Ready(Requests.size() - 1) == CL_INVALID_ARGUMENT, "late retired ingress rejected");
}

void PublicationAndLaundering()
{
    Fixture F;
    F.Run("assert(not pcall(require,'failed')); require('cold')");
    Check(Requests.empty(), "cold/failed module publication cannot submit");
    F.Run(Prefix + "S.Leaked:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end)");
    Check(Requests.size() == 1, "committed owner can use its existing facade after module failure");
    auto Provider = F.Addon("provider"), Consumer = F.Addon("consumer");
    F.Module(Provider, "api", "local W=game:GetService('Workspace'); return function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end");
    Check(cl_domain_public_module(F.Vm, Provider, "api") == CL_OK && cl_domain_commit(F.Vm, Provider) == CL_OK &&
        cl_domain_dependency(F.Vm, Consumer, "provider", Provider) == CL_OK, "public borrowed facade binding");
    F.Module(Consumer, "cold", "local A=require('@provider/api'); assert(not pcall(A)); return A");
    F.Run(Consumer, std::string("local A=require('cold'); assert(not pcall(A)); local W=game:GetService('Workspace'); ") +
        "assert(not pcall(function() W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() end) end)); " +
        "task.defer(function() assert(not pcall(A)); " + Prefix + Submit + " end)");
    Check(Requests.size() == 1, "provisional/public module laundering prevented");
    Check(cl_domain_commit(F.Vm, Consumer) == CL_OK, "commit consumer"); F.Step(F.Info());
    Check(Requests.size() == 2 && Requests.back().Owner == Consumer, "cached foreign facade fails, own admitted owner succeeds");
    auto Candidate = F.Addon("candidate");
    F.Module(Candidate, "failed", "error('publication rollback')");
    F.Run(Candidate, Prefix + "assert(not pcall(function() " + Submit + " end)); error('candidate fails')", CL_RUNTIME_ERROR);
    Check(Requests.size() == 2 && cl_domain_destroy(F.Vm, Candidate) == CL_OK, "failed candidate leaves no callback root");
}

void CompletionFailureAndRetirement()
{
    {
        Fixture F; Reject = true;
        F.Run(Prefix + "assert(not pcall(function() " + Submit + " end))");
        Check(GetVm(F.Vm)->DiscoveryReserved == 0 && Released.size() == 1, "host rejection cleans reserved callback and route");
    }
    {
        Fixture F; F.Run(Prefix + Submit); Check(F.Ready(0) == CL_OK, "queued stale result");
        Stale = true; // Lifetime changes after readiness but before admission.
        F.Run(Prefix + "S.Done=nil");
        // Change the callback by submitting a second request with the controlled error signature.
        F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,Err) assert(E==nil and Err=='DiscoveryStaleResult'); S.Error=true end)");
        Check(F.Ready(1) == CL_OK, "error ready");
        F.Step(F.Info(), true, CL_RUNTIME_ERROR); F.Step(F.Info());
        F.Run(Prefix + "assert(S.Done==nil and S.Error)"); Check(Fetches == 2, "stale whole result fails at admission, no replay");
    }
    {
        Fixture F; F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,Err) assert(E==nil and Err=='DiscoveryDeadline'); S.Expired=(S.Expired or 0)+1 end)");
        auto Pending = F.Info(); Check(Pending.Queued == 1 && Pending.NextDueNs > Pending.NowNs,
            "pending capture exposes absolute next due deadline");
        F.Owner(F.Root).DiscoveryCallbacks[0].Expires = NowNs() - 1;
        auto Info = F.Info(); Check(Info.Queued == 1 && GetVm(F.Vm)->DiscoveryReserved == 1 && Released.empty(),
            "absolute traversal expiry retains ready deadline callback");
        Check(F.Ready(0) == CL_OK && F.Ready(0) == CL_INVALID_ARGUMENT,
            "expiry before first notification accepts exactly one genuine notification");
        F.Step(Info); F.Step(F.Info(), false);
        Check(!Fetches && GetVm(F.Vm)->DiscoveryReserved == 0 && Released.size() == 1,
            "expired notified capture admits error once and releases managed traversal");
        F.Run(Prefix + "assert(S.Expired==1)");
    }
    {
        Fixture F; F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,Err) assert(E==nil and Err=='DiscoveryDeadline'); S.Expired=true end)");
        F.Owner(F.Root).DiscoveryCallbacks[0].Expires = NowNs() - 1;
        F.Step(F.Info()); Check(!Fetches && Released.size() == 1 && !GetVm(F.Vm)->DiscoveryReserved,
            "expiry callback before notification cancels managed route");
        Check(F.Ready(0) == CL_INVALID_ARGUMENT, "release prevents later notification authority");
        F.Run(Prefix + "assert(S.Expired)");
    }
    {
        Fixture F; F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function(E,Err) assert(E==nil and Err=='DiscoveryDeadline'); S.Expired=true end)");
        Check(F.Ready(0) == CL_OK, "expiry queued ready");
        auto Cutoff = F.Info(); F.Owner(F.Root).DiscoveryCallbacks[0].Expires = NowNs() - 1;
        F.Step(Cutoff); Check(!Fetches && GetVm(F.Vm)->DiscoveryReserved == 0, "expiry rechecked at actual callback admission");
        F.Run(Prefix + "assert(S.Expired)");
    }
    {
        Fixture F; F.Run(Prefix + Submit); Check(F.Ready(0) == CL_OK, "ready before domain replacement");
        F.Owner(F.Root).Discarded = UINT64_MAX; GetVm(F.Vm)->RetiredDiscarded = UINT64_MAX;
        Check(cl_domain_destroy(F.Vm, F.Root) == CL_OK && !GetVm(F.Vm)->DiscoveryReserved && !Fetches, "domain retirement suppresses queued callbacks");
        Check(GetVm(F.Vm)->RetiredDiscarded == UINT64_MAX, "discovery discard diagnostics saturate without wrap");
    }
    {
        Fixture F; F.Run(Prefix + Submit); Check(F.Ready(0) == CL_OK, "ready before fatal VM retirement");
        F.Run("while true do end", CL_TIMEOUT);
        Check(!GetVm(F.Vm)->State && !GetVm(F.Vm)->DiscoveryReserved && !Fetches, "fatal retirement releases ready and traversal roots");
    }
    {
        Fixture F; F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() error('callback fails') end)");
        Check(F.Ready(0) == CL_OK, "throwing callback ready"); F.Step(F.Info(), true, CL_RUNTIME_ERROR);
        Check(!GetVm(F.Vm)->DiscoveryReserved && Released.size() == 1, "callback failure releases exactly once"); F.Step(F.Info(), false);
    }
    {
        Fixture F; F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() coroutine.yield() end)");
        Check(F.Ready(0) == CL_OK, "yielding callback ready"); F.Step(F.Info(), true, CL_RUNTIME_ERROR);
        Check(GetVm(F.Vm)->State && !GetVm(F.Vm)->DiscoveryReserved && Released.size() == 1,
            "callback cannot yield or retain its reservation");
    }
    {
        Fixture F; F.Run(Prefix + Submit); Check(F.Ready(0) == CL_OK, "release failure ready"); ReleaseFailure = true;
        F.Step(F.Info(), true, CL_INTERNAL_ERROR); ReleaseFailure = false;
        Check(!GetVm(F.Vm)->State && !GetVm(F.Vm)->DiscoveryReserved && Released.size() == 1,
            "failed release fails closed without replay or retained native roots");
        Check(F.Info().Queued == 0, "retired integrity failure remains inspectable for managed teardown");
    }
    {
        Fixture F; F.Run(Prefix + Submit); Check(F.Ready(0) == CL_OK, "malformed callback ready"); Malformed = true;
        F.Step(F.Info(), true, CL_RUNTIME_ERROR); Check(!GetVm(F.Vm)->DiscoveryReserved, "materialization rejection releases callback");
        F.Run(Prefix + "assert(S.Done==nil)");
    }
    {
        Fixture F; F.Run(Prefix + Submit); Check(F.Ready(0) == CL_OK, "deadline fetch ready"); ExpireFetch = true;
        F.Step(F.Info(), true, CL_TIMEOUT); Check(!GetVm(F.Vm)->State && !GetVm(F.Vm)->DiscoveryReserved,
            "fetch deadline retires without user callback or replay");
    }
    {
        Fixture F; F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() while true do end end)");
        Check(F.Ready(0) == CL_OK, "user deadline ready"); F.Step(F.Info(), true, CL_TIMEOUT, 1000000);
        Check(!GetVm(F.Vm)->State && Released.size() == 1, "user callback deadline releases and retires");
    }
}

void FairnessNestedAndBounds()
{
    Fixture F; Results = 256;
    F.Run(Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1e308,function(E,Err) assert(E and not Err and #E==256 and E[256].Id=='256'); " +
        Submit + " end)");
    Check(Requests[0].Fields[4] == "1e+308" && Requests[0].Fields[5].empty() && Requests[0].Fields[6] == "256", "finite maximum radius and default snapshots");
    Check(F.Ready(0) == CL_OK, "maximum response ready"); auto Cutoff = F.Info(); F.Step(Cutoff);
    Check(Requests.size() == 2 && GetVm(F.Vm)->DiscoveryReserved == 1, "nested submission gets a distinct later reservation");
    Results = 2; Check(F.Ready(1) == CL_OK, "nested later completion"); F.Step(Cutoff, false); F.Step(F.Info());
    auto Other = F.Addon("fair"); F.Run(Other, "require('state')"); Check(cl_domain_commit(F.Vm, Other) == CL_OK, "fair owner commit");
    F.Run(Prefix + Submit + "; task.defer(function() end)");
    F.Run(Other, Prefix + Submit); Check(F.Ready(2) == CL_OK && F.Ready(3) == CL_OK, "two fair owners ready");
    Cutoff = F.Info(); auto Before = Fetches; GetVm(F.Vm)->SchedulerCursor = Other; F.Step(Cutoff);
    Check(Fetches == Before, "oldest ordinary task wins within selected domain");
    F.Step(Cutoff); Check(Fetches == Before + 1, "round robin admits other domain ahead of second root work");
    F.Step(Cutoff); Check(Fetches == Before + 2, "root ready runs next round");
}

void AllocationFaults()
{
    for (int FailAfter = 0; FailAfter < 32; ++FailAfter) {
        Fixture F;
        ClHandle Thread = 0; ClResult Result{};
        std::string Source = Prefix + "W:GetEntitiesInRadiusAsync(Vector3.new(0,0,0),1,function() buffer.create(65536) end)";
        Check(cl_domain_load_source(F.Vm, F.Root, "allocation.discovery", Source.data(), uint32_t(Source.size()), &Thread, &Result) == CL_OK,
            "fault fixture load");
        TestAllocationFailureAfter = FailAfter;
        auto Status = cl_thread_resume(Thread, 100000000, &Result);
        TestAllocationFailureAfter = -1;
        Check(Status == CL_OK || Status == CL_MEMORY_LIMIT, "allocation fault contained");
        Check(cl_thread_destroy(Thread) == CL_OK, "fault thread cleanup");
        if (!Requests.empty()) {
            Check(F.Ready(0) == CL_OK, "allocation callback ready");
            auto Cutoff = F.Info(); TestAllocationFailureAfter = 0;
            F.Step(Cutoff, true, CL_MEMORY_LIMIT); TestAllocationFailureAfter = -1;
            Check(!GetVm(F.Vm)->DiscoveryReserved && !GetVm(F.Vm)->State, "completion allocation fault releases and retires");
        } else Check(!GetVm(F.Vm)->DiscoveryReserved, "submission allocation failure releases reservation");
    }
}
} // namespace

int main(int Count, char** Args)
{
    Check(Count == 2, "compiler worker argument"); SetCompilerExecutableForTesting(Args[1]);
    ArgumentsAndSnapshot(); ReservationsAndReadyRaces(); PublicationAndLaundering();
    CompletionFailureAndRetirement(); FairnessNestedAndBounds(); AllocationFaults();
    ResetCompilerForTesting();
    std::puts("[CarbonLuau:DiscoveryTest] PASS real VM arguments, fixed reservations, queued admission, lifetimes, publication, laundering, deadlines and allocation faults");
}
