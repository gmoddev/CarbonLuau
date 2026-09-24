// Public native facade against a bounded in-memory HostDelegate transport.
// All envelopes use the shipped Persistence-1A codec; no substitute serializer.
#include "../../native/src/runtime/RuntimeInternal.hpp"
#include "../../native/src/scripts/Compiler.hpp"
#include <set>

using namespace CarbonLuau::Runtime;
namespace P = CarbonLuau::Persistence;
namespace {
void Check(bool Good, const char* Message)
{
    if (!Good) { std::fprintf(stderr, "[CarbonLuau:PersistenceTest] %s\n", Message); std::exit(1); }
}
struct Request {
    uint64_t VmGeneration, Domain, Route;
    uint32_t Operation;
    P::Identity Identity;
    P::Bytes Envelope;
};
std::vector<Request> Requests;
std::set<std::pair<uint64_t,uint64_t>> Released;
ClHandle CurrentVm = 0;
uint64_t CurrentGeneration = 0;
uint32_t Reject = 0;
bool Reenter = false;
bool ExpireSubmission = false;
bool ExpireCompletion = false;
uint32_t Host(uint64_t DomainId, uint32_t Operation, const char* Bytes, uint32_t Length,
    char*, uint32_t, uint32_t* Written)
{
    *Written = 0;
    const auto* Frame = reinterpret_cast<const uint8_t*>(Bytes);
    if (Operation == 31) {
        Check(Length >= 56 && Length <= P::MaximumFrame && !std::memcmp(Frame, "CLPB", 4) && P::Read32(Frame + 4) == 1, "submission header");
        Request Value{P::Read64(Frame + 12), P::Read64(Frame + 20), P::Read64(Frame + 28), P::Read32(Frame + 8)};
        Check(Value.Domain == DomainId && Value.VmGeneration == GetVm(CurrentVm)->GenerationId, "exact submitted lifetime");
        Value.Identity.Addon = P::Read32(Frame + 36) != 0;
        size_t Offset = 56;
        std::string* Strings[] = {&Value.Identity.Package, &Value.Identity.Store, &Value.Identity.Key};
        for (size_t I = 0; I < 3; ++I) {
            size_t Size = P::Read32(Frame + 40 + I * 4); Check(Size <= Length - Offset, "bounded submission string");
            Strings[I]->assign(Bytes + Offset, Size); Offset += Size;
        }
        size_t Size = P::Read32(Frame + 52); Check(Size == Length - Offset, "exact envelope length");
        Value.Envelope.assign(Frame + Offset, Frame + Length);
        P::Validate(Value.Identity);
        Check(Value.Identity.Package == GetDomain(*GetVm(CurrentVm), DomainId)->PackageId, "host selected namespace");
        Check(GetVm(CurrentVm)->Admission && GetVm(CurrentVm)->Admission->Owner->Id == DomainId && CanMutateHost(*GetVm(CurrentVm)), "committed exact owner at host");
        if (Reject) return Reject;
        if (Reenter) Check(cl_domain_storage_completion(CurrentVm, DomainId, Value.VmGeneration, Value.Route, 0, 0, nullptr, 0) == CL_INVALID_ARGUMENT,
            "completion cannot recursively enter active admission");
        Requests.push_back(std::move(Value));
        if (ExpireSubmission) GetVm(CurrentVm)->Deadline = P::Clock::now() - std::chrono::seconds(1);
    } else if (Operation == 32) {
        Check(Length == 32 && !std::memcmp(Frame, "CLPR", 4) && P::Read32(Frame + 4) == 1 && P::Read64(Frame + 16) == DomainId, "release frame");
        // Registry removal precedes Vm::~Vm. Teardown's release callback must
        // validate against the host binding captured at installation, not look
        // up a VM handle which is already intentionally unavailable.
        Check(P::Read64(Frame + 8) == CurrentGeneration, "release generation");
        Check(Released.emplace(DomainId, P::Read64(Frame + 24)).second, "route released once");
        if (auto* Runtime=GetVm(CurrentVm); Runtime && Runtime->Admission) {
            Check(Runtime->Admission->Owner->Id == DomainId && !Runtime->Admission->Provisional &&
                P::Clock::now() < Runtime->Deadline, "fresh callback admission precedes release/materialization");
            if (ExpireCompletion) Runtime->Deadline = P::Clock::now() - std::chrono::seconds(1);
        }
    }
    return 0;
}
struct Fixture {
    ClHandle Vm = 0, Root = 0;
    explicit Fixture(bool PublicationTests = false) {
        Requests.clear(); Released.clear(); Reject = 0; Reenter = false; ExpireSubmission = false; ExpireCompletion = false;
        ClVmConfig Config{16 * MiB};
        Check(cl_vm_create(&Config, &Vm) == CL_OK && cl_vm_scripts(Vm, 8) == CL_OK, "create fixture");
        CurrentVm = Vm; CurrentGeneration=GetVm(Vm)->GenerationId;
        Check(cl_domain_create(Vm, 8, &Root) == CL_OK, "create root facade domain");
        Check(cl_domain_facade(Vm, Root, Host) == CL_OK, "install native public facade");
        Module(Root, "state", "return {}");
        if (PublicationTests) {
            Module(Root,"failed",R"(local State=require('state'); State.Service=game:GetService('DataStoreService'); State.Leaked=State.Service:GetDataStore('Failed'); error('failed module'))");
            Module(Root,"child",R"(local State=require('state'); State.Child=game:GetService('DataStoreService'):GetDataStore('Child'); return State.Child)");
            Module(Root,"outer",R"(require('child'); error('failed outer'))");
            Module(Root,"cold",R"(local Store=game:GetService('DataStoreService'):GetDataStore('Cold'); assert(not pcall(function() Store:GetAsync('Key',function() end) end)); return Store)");
        }
        Run(Root, "require('state')"); Check(cl_domain_commit(Vm, Root) == CL_OK, "commit fixture root");
    }
    ~Fixture() {
        TestAllocationFailureAfter = -1; TestStorageCopyFailure = false;
        Check(cl_vm_destroy(Vm) == CL_OK, "destroy fixture"); Check(TestLiveBytes == 0, "all VM callback references released");
    }
    void Module(ClHandle Domain, const char* Name, const std::string& Source) {
        Check(cl_domain_module(Vm, Domain, Name, Source.data(), uint32_t(Source.size())) == CL_OK, "register fixture module");
    }
    ClHandle Domain(const char* Package) {
        ClHandle Id = 0; Check(cl_domain_create(Vm, 8, &Id) == CL_OK, "create addon domain");
        Check(cl_domain_addon(Vm, Id, Package, "1.0.0", "") == CL_OK && cl_domain_facade(Vm, Id, Host) == CL_OK, "bind addon facade");
        Module(Id, "state", "return {}"); return Id;
    }
    ClResult Run(ClHandle Domain, const std::string& Source, ClStatus Expected = CL_OK, uint64_t Budget = 100000000) {
        ClHandle Thread = 0; ClResult Result{};
        Check(cl_domain_load_source(Vm, Domain, "persistence.fixture", Source.data(), uint32_t(Source.size()), &Thread, &Result) == CL_OK, "load fixture");
        ClStatus Status = cl_thread_resume(Thread, Budget, &Result);
        if (Status != Expected) std::fprintf(stderr, "[CarbonLuau:PersistenceTest] expected=%u actual=%u %s\n%s\n", Expected, Status, Result.Error, Source.c_str());
        Check(Status == Expected, "script fixture status");
        Check(cl_thread_destroy(Thread) == ((Result.Flags & 1) ? CL_INVALID_ARGUMENT : CL_OK), "release fixture thread");
        return Result;
    }
    ClResult Run(const std::string& Source, ClStatus Expected = CL_OK) { return Run(Root, Source, Expected); }
    ClStatus Complete(size_t Index, uint32_t Error = 0, bool Found = false, const P::Bytes& Envelope = {}) {
        const auto& Request = Requests.at(Index);
        return cl_domain_storage_completion(Vm, Request.Domain, Request.VmGeneration, Request.Route, Error, Found ? 1u : 0u,
            Envelope.empty() ? nullptr : Envelope.data(), uint32_t(Envelope.size()));
    }
    ClSchedulerInfo Cutoff() { ClSchedulerInfo Info{}; Check(cl_vm_scheduler(Vm, &Info) == CL_OK, "scheduler cutoff"); return Info; }
    ClResult Step(const ClSchedulerInfo& Cutoff, bool ExpectedRan = true, ClStatus Expected = CL_OK, uint64_t Budget = 100000000) {
        ClResult Result{}; uint32_t Ran = 0;
        ClStatus Status = cl_vm_callback(Vm, Cutoff.NowNs, Cutoff.Sequence, Budget, &Ran, &Result);
        if (Status != Expected) std::fprintf(stderr, "[CarbonLuau:PersistenceTest] callback expected=%u actual=%u %s\n", Expected, Status, Result.Error);
        Check(Status == Expected && (Ran != 0) == ExpectedRan, "callback fixture status"); return Result;
    }
    void Drain(size_t Count) { auto Info = Cutoff(); for (size_t I = 0; I < Count; ++I) Step(Info); Step(Info, false); }
};
const std::string Prefix = "local State=require('state'); local Service=game:GetService('DataStoreService'); local Store=Service:GetDataStore('Values'); ";
std::shared_ptr<P::Value> Decode(size_t Index) {
    const auto& Value = Requests.at(Index); return P::Decode(Value.Identity, Value.Envelope, P::Clock::now() + std::chrono::seconds(1));
}
void ValuesAndResults()
{
    Fixture F; Reenter = true;
    F.Run(Prefix + R"(
        local Shared={Flag=false,Text='hello',Small=2^-1074,Zero=-0.0}
        local Input={A=Shared,B=Shared,Array={true,3,'x'},Empty={}}
        assert(select('#',Store:SetAsync('Snapshot',Input,function(Saved,Err) assert(Saved==true and Err==nil); State.Saved=true end))==0)
        Shared.Text='changed'; Input.Array[1]=false
        assert(State.Saved==nil)
    )");
    Check(Requests.size() == 1, "single immutable snapshot");
    auto Value = Decode(0); Check(Value->Type == P::Kind::Map, "map snapshot");
    P::Bytes Stored = Requests[0].Envelope;
    Check(F.Complete(0, 0, true) == CL_OK, "set completion"); F.Drain(1);
    F.Run(Prefix + R"(
        assert(State.Saved)
        Store:GetAsync('Snapshot',function(Value,Err)
            assert(Err==nil and Value.A~=Value.B and Value.A.Text=='hello' and Value.Array[1]==true)
            assert(Value.A.Flag==false and Value.A.Small==2^-1074 and 1/Value.A.Zero==-math.huge)
            assert(next(Value.Empty)==nil and getmetatable(Value)==nil)
            Value.A.Text='edited'; State.First=Value
        end)
        Store:GetAsync('Snapshot',function(Value,Err) assert(Err==nil and Value~=State.First and Value.A.Text=='hello'); State.Fresh=true end)
    )");
    Check(F.Complete(1, 0, true, Stored) == CL_OK && F.Complete(2, 0, true, Stored) == CL_OK, "get copy completions"); F.Drain(2);
    F.Run(Prefix + R"(
        assert(State.Fresh)
        Store:GetAsync('Absent',function(V,E) assert(V==nil and E==nil) end)
        Store:RemoveAsync('Absent',function(V,E) assert(V==false and E==nil) end)
        Store:RemoveAsync('Present',function(V,E) assert(V==true and E==nil) end)
        Store:GetAsync('Failure',function(V,E) assert(V==nil and E=='StorageUnavailable') end)
        Store:SetAsync('Unknown',true,function(V,E) assert(V==nil and E=='Indeterminate') end)
    )");
    Check(F.Complete(3) == CL_OK && F.Complete(4) == CL_OK && F.Complete(5,0,true) == CL_OK &&
        F.Complete(6,3) == CL_OK && F.Complete(7,10) == CL_OK, "canonical result shapes"); F.Drain(5);
    Check(Released.size() == Requests.size() && GetVm(F.Vm)->StorageReserved == 0, "all delivered reservations released");
}
void RejectValuesAndBounds()
{
    Fixture F;
    F.Run(Prefix + R"(
        local function Bad(Value) assert(not pcall(function() Store:SetAsync('Key',Value,function() error('rejected callback') end) end)) end
        Bad(nil); Bad(0/0); Bad(math.huge); Bad(-math.huge); Bad(function() end); Bad(coroutine.create(function() end))
        Bad(buffer.create(1)); Bad(vector.create(1,2,3)); Bad(Store); Bad(Service); Bad(game)
        Bad(setmetatable({},{__pairs=function() error('metamethod') end})); Bad({[true]=1}); Bad({[2]=1})
        Bad({[1]=1,Name=2}); Bad({[1.5]=1}); Bad({[0]=1}); Bad({[-1]=1}); Bad({[1e100]=1})
        local Cycle={}; Cycle.Self=Cycle; Bad(Cycle)
        Bad(string.char(0)); Bad(string.char(255)); Bad(string.char(0xc0,0x80)); Bad(string.rep('x',16385))
        Bad({[string.rep('x',129)]=1}); Bad({[string.char(0)]=1})
        local Deep={}; local Tail=Deep; for I=1,16 do Tail[1]={}; Tail=Tail[1] end; Bad(Deep)
        local Wide={}; for I=1,1025 do Wide[I]=false end; Bad(Wide)
        local Row={}; for I=1,1024 do Row[I]=true end; Bad({Row,Row,Row,Row})
        Bad({string.rep('x',16384),string.rep('x',16384),string.rep('x',16384),string.rep('x',16316)})
        for _,Name in {'','.', '..','a/b','a\\b','a:b',string.char(1),string.char(127),string.char(255),string.rep('x',65)} do
            assert(not pcall(function() Service:GetDataStore(Name) end))
        end
        for _,Key in {'','.', '..','a/b','a\\b','a:b',string.char(0),string.char(255),string.rep('x',129)} do
            assert(not pcall(function() Store:GetAsync(Key,function() end) end))
        end
        assert(not pcall(function() Store:GetAsync(1,function() end) end))
        assert(not pcall(function() Store:GetAsync('Key') end))
        assert(not pcall(function() Store:SetAsync('Key',1,true) end))
        assert(not pcall(function() Store:SetAsync('Key',1,function() end,1) end))
        assert(not pcall(function() Store.Name='x' end))
        assert(Store.Name==nil and Store.Path==nil and Store.Close==nil and Store.Query==nil)
        assert(not pcall(function() Store.GetAsync({},'Key',function() end) end))
    )");
    Check(Requests.empty() && GetVm(F.Vm)->StorageReserved == 0, "invalid input rejects before acceptance");
    F.Run(Prefix + R"(
        local function Save(Key,Value) Store:SetAsync(Key,Value,function(V,E) assert(V and E==nil) end) end
        local Deep={}; local Tail=Deep; for I=1,15 do Tail[1]={}; Tail=Tail[1] end; Save('Depth',Deep)
        local Wide={}; for I=1,1024 do Wide[I]=false end; Save('Width',Wide)
        local Row={}; for I=1,1023 do Row[I]=true end; Save('Expanded',{Row,Row,Row,Row})
        Save('Envelope',{string.rep('x',16384),string.rep('x',16384),string.rep('x',16384),string.rep('x',16315)})
        Save('MapKeys',{['']=true,[string.rep('x',128)]=true,['a/b:']=false})
        Service:GetDataStore(string.rep('x',64)):SetAsync(string.rep('k',128),false,function(V,E) assert(V and E==nil) end)
    )");
    Check(Requests.size() == 6 && Requests[3].Envelope.size() == 65536, "inclusive conversion bounds");
    for (size_t I = 0; I < Requests.size(); ++I) { Decode(I); Check(F.Complete(I,0,true) == CL_OK, "bounded set completion"); }
    F.Drain(6);
    F.Run(Prefix + "for I=1,62 do Service:GetDataStore('Store'..I) end; Service:GetDataStore('Store1'); assert(not pcall(function() Service:GetDataStore('Overflow') end))");
    Check(GetDomain(*GetVm(F.Vm), F.Root)->StorageNames.size() == 64, "64 distinct acquired names per domain");
}
void PublicationAndPrivacy()
{
    Fixture F(true);
    F.Run(Prefix + R"(
        assert(not pcall(require,'failed'))
        assert(not pcall(function() State.Leaked:GetAsync('Key',function() end) end))
        assert(not pcall(function() State.Service:GetDataStore('Other') end))
        assert(not pcall(require,'outer'))
        assert(not pcall(function() State.Child:GetAsync('Key',function() end) end))
        local Cold=require('cold'); Cold:GetAsync('Key',function(V,E) assert(V==nil and E==nil) end)
    )");
    Check(Requests.size() == 1, "cold/publication no dispatch until scope commits"); Check(F.Complete(0) == CL_OK,"cold completion"); F.Drain(1);
    ClHandle A = F.Domain("provider");
    F.Module(A,"api",R"(local State=require('state'); return State)");
    Check(cl_domain_public_module(F.Vm,A,"api") == CL_OK, "public shared state");
    F.Run(A,R"(local State=require('state'); State.Store=game:GetService('DataStoreService'):GetDataStore('Private'); State.Service=game:GetService('DataStoreService'); State.Call=function() State.Store:GetAsync('Key',function() end) end; State.Acquire=function() return game:GetService('DataStoreService') end; require('api'))");
    Check(cl_domain_commit(F.Vm,A) == CL_OK,"provider commit");
    ClHandle B = F.Domain("consumer");
    Check(cl_domain_dependency(F.Vm,B,"provider",A) == CL_OK,"consumer dependency");
    F.Run(B,R"(
        local Foreign=require('@provider/api')
        assert(not pcall(Foreign.Call)); assert(not pcall(Foreign.Acquire))
        assert(not pcall(function() Foreign.Service:GetDataStore('Private') end))
        assert(not pcall(function() Foreign.Store:GetAsync('Key',function() end) end))
        local Own=game:GetService('DataStoreService'):GetDataStore('Private')
        assert(not pcall(function() Own:GetAsync('Key',function() end) end))
        task.defer(function() Own:GetAsync('Key',function() end) end)
    )");
    Check(Requests.size() == 1,"foreign and provisional rejected");
    Check(cl_domain_commit(F.Vm,B) == CL_OK,"consumer commit"); F.Drain(1);
    Check(Requests.size() == 2 && Requests[1].Identity.Package == "consumer", "deferred namespace derives resource owner");
    Check(F.Complete(1) == CL_OK,"consumer completion"); F.Drain(1);
    F.Run(B,"local F=require('@provider/api'); assert(not pcall(F.Call)); assert(not pcall(F.Acquire))");
    Check(cl_domain_destroy(F.Vm,A) == CL_OK,"retire foreign resource owner");
    F.Run(B,"local F=require('@provider/api'); assert(not pcall(F.Call))", CL_RUNTIME_ERROR); // exact binding itself is stale
    ClHandle Failed = F.Domain("failed");
    F.Run(Failed,"local S=game:GetService('DataStoreService'):GetDataStore('X'); task.defer(function() S:SetAsync('Key',true,function() end) end); error('candidate')",CL_RUNTIME_ERROR);
    Check(cl_domain_destroy(F.Vm,Failed) == CL_OK,"failed candidate teardown"); F.Drain(0);
    Check(Requests.size() == 2,"failed candidate never dispatches");
}
void SchedulingAndRaces()
{
    Fixture F;
    F.Run(Prefix + R"(
        State.Order={}
        for I=1,8 do Store:GetAsync('Key'..I,function() State.Order[#State.Order+1]=I; task.defer(function() State.Later=true end) end) end
        assert(not pcall(function() Store:GetAsync('Overflow',function() end) end))
        for I=1,8 do task.defer(function() State.Tasks=(State.Tasks or 0)+1 end) end
    )");
    Check(Requests.size() == 8 && GetVm(F.Vm)->StorageReserved == 8, "separate reserved slots survive task saturation");
    const auto First = Requests[0];
    Check(cl_domain_storage_completion(F.Vm,F.Root,First.VmGeneration+1,First.Route,0,0,nullptr,0)==CL_INVALID_ARGUMENT,"wrong epoch");
    Check(cl_domain_storage_completion(F.Vm,F.Root,First.VmGeneration,First.Route+100,0,0,nullptr,0)==CL_INVALID_ARGUMENT,"unknown route");
    Check(cl_domain_storage_completion(F.Vm,F.Root,First.VmGeneration,First.Route,0,2,nullptr,0)==CL_INVALID_ARGUMENT,"invalid found flag");
    Check(cl_domain_storage_completion(F.Vm,F.Root,First.VmGeneration,First.Route,1,0,nullptr,0)==CL_INVALID_ARGUMENT,"accepted programming error rejected");
    Check(cl_domain_storage_completion(F.Vm,F.Root,First.VmGeneration,First.Route,3,1,nullptr,0)==CL_INVALID_ARGUMENT,"failure shape");
    Check(cl_domain_storage_completion(F.Vm,F.Root,First.VmGeneration,First.Route,0,1,nullptr,65537)==CL_INVALID_ARGUMENT,"oversized response");
    ClStatus OffThread = CL_OK;
    std::thread Other([&] { OffThread=F.Complete(0); }); Other.join(); Check(OffThread==CL_INVALID_ARGUMENT,"foreign owner thread");
    auto Before = F.Cutoff();
    // Out-of-order delivery cannot overtake the first accepted route.
    Check(F.Complete(1)==CL_OK,"out-of-order completion");
    F.Drain(8); F.Run(Prefix+"assert(State.Order[1]==nil and State.Tasks==8)");
    Check(F.Complete(0)==CL_OK && F.Complete(0)==CL_INVALID_ARGUMENT,"duplicate response");
    for(size_t I=2;I<8;++I) Check(F.Complete(I)==CL_OK,"completion intake");
    F.Step(Before,false); // sequence fence excludes all later completion work
    auto Cutoff=F.Cutoff();
    for(size_t I=0;I<8;++I) F.Step(Cutoff);
    F.Step(Cutoff,false);
    F.Run(Prefix+"for I=1,8 do assert(State.Order[I]==I) end; assert(State.Later==nil)");
    F.Drain(8); F.Run(Prefix+"assert(State.Later)");
    Check(Released.size()==8 && GetVm(F.Vm)->StorageReserved==0,"exactly one callback and release");
    // A callback may submit new work, but the old scheduler cutoff cannot run it.
    F.Run(Prefix+"Store:GetAsync('First',function() Store:GetAsync('Second',function() State.Second=true end) end)");
    Check(F.Complete(8)==CL_OK,"first recursive submission completion"); Cutoff=F.Cutoff(); F.Step(Cutoff);
    Check(Requests.size()==10 && F.Complete(9)==CL_OK,"callback new request accepted"); F.Step(Cutoff,false); F.Drain(1);
    F.Run(Prefix+"assert(State.Second)");
    F.Run(Prefix+"Store:GetAsync('Error',function() error('ordinary callback error') end)");
    Check(F.Complete(10)==CL_OK,"error callback completion"); F.Step(F.Cutoff(),true,CL_RUNTIME_ERROR);
    F.Run("return 1");
    F.Run(Prefix+"Store:GetAsync('Accepted',function() State.AfterError=true end); error('caller error after acceptance')",CL_RUNTIME_ERROR);
    Check(F.Complete(11)==CL_OK,"ordinary caller error preserves accepted callback"); F.Drain(1);
    F.Run(Prefix+"assert(State.AfterError)");
    F.Run(Prefix+"Store:GetAsync('Retire',function() error('stale callback') end)");
    Check(cl_domain_destroy(F.Vm,F.Root)==CL_OK && F.Complete(12)==CL_INVALID_ARGUMENT,"retirement rejects late completion");
    Check(GetVm(F.Vm)->StorageReserved==0 && Released.size()==13,"teardown releases pending references");
}
void FairDomains()
{
    Fixture F;
    ClHandle A=F.Domain("alpha"), B=F.Domain("beta");
    Check(cl_domain_commit(F.Vm,A)==CL_OK && cl_domain_commit(F.Vm,B)==CL_OK,"fairness domains commit");
    F.Run(A,Prefix+"for I=1,3 do Store:GetAsync('Key'..I,function() end) end");
    F.Run(B,Prefix+"for I=1,3 do Store:GetAsync('Key'..I,function() end) end");
    for(size_t I=0;I<6;++I) Check(F.Complete(I)==CL_OK,"fair completion");
    auto Cutoff=F.Cutoff();
    for(unsigned I=0;I<6;++I) { F.Step(Cutoff); Check(GetVm(F.Vm)->SchedulerCursor==(I%2 ? B : A),"round-robin completion fairness"); }
    F.Step(Cutoff,false);
}
void FailuresAndDeadlines()
{
    {
        Fixture F;
        for(uint32_t Code=1;Code<=6;++Code) {
            Reject=Code; F.Run(Prefix+"assert(not pcall(function() Store:GetAsync('Rejected',function() end) end))");
            Check(GetVm(F.Vm)->StorageReserved==0 && Requests.empty(),"host rejection releases callback slot");
        }
        Reject=0; F.Run(Prefix+"Store:GetAsync('CopyFailure',function() error('must not run') end)");
        TestStorageCopyFailure=true;
        Check(F.Complete(0)==CL_MEMORY_LIMIT && !GetVm(F.Vm)->State && GetVm(F.Vm)->StorageReserved==0,"completion allocation failure retires entire VM");
        Check(Released.size()==1,"fatal copy failure releases callback");
    }
    {
        Fixture F; ExpireSubmission=true;
        F.Run(Prefix+"pcall(function() Store:SetAsync('Deadline',true,function() end) end)",CL_TIMEOUT);
        Check(Requests.size()==1 && !GetVm(F.Vm)->State && Released.size()==1,"original admission expiry never rolls back/replays accepted write");
    }
    {
        Fixture F; F.Run(Prefix+"Store:GetAsync('Timeout',function() while true do end end)");
        Check(F.Complete(0)==CL_OK,"timeout callback completion");
        F.Step(F.Cutoff(),true,CL_TIMEOUT,1000000);
        Check(!GetVm(F.Vm)->State && Released.size()==1,"fresh callback timeout is VM fatal");
    }
    {
        Fixture F; F.Run(Prefix+"Store:GetAsync('Decode',function() error('expired materialization must not enter callback') end)");
        P::Value Value; Value.Type=P::Kind::Boolean; Value.Boolean=false;
        auto Envelope=P::Encode(Requests[0].Identity,Value,P::Clock::now()+std::chrono::seconds(1));
        Check(F.Complete(0,0,true,Envelope)==CL_OK,"decode deadline completion");
        ExpireCompletion=true; F.Step(F.Cutoff(),true,CL_TIMEOUT);
        Check(!GetVm(F.Vm)->State,"materialization shares fresh callback deadline");
    }
    {
        Fixture F; F.Run(Prefix+"Store:GetAsync('Memory',function() end)");
        P::Value Value; Value.Type=P::Kind::String; Value.String=std::string(16384,'x');
        auto Envelope=P::Encode(Requests[0].Identity,Value,P::Clock::now()+std::chrono::seconds(1));
        Check(F.Complete(0,0,true,Envelope)==CL_OK,"materialization fault completion");
        TestAllocationFailureAfter=0;
        F.Step(F.Cutoff(),true,CL_MEMORY_LIMIT);
        TestAllocationFailureAfter=-1;
        Check(!GetVm(F.Vm)->State && Released.size()==1,"materialization allocation failure retires VM");
    }
}
void CorruptionAndFreshBudget()
{
    Fixture F;
    F.Run(Prefix+R"(
        Store:GetAsync('False',function(V,E) assert(V==false and E==nil) end)
        Store:GetAsync('Corrupt',function(V,E) assert(V==nil and E=='StorageCorrupt') end)
        Store:GetAsync('Format',function(V,E) assert(V==nil and E=='FormatUnsupported') end)
    )");
    P::Value Value; Value.Type=P::Kind::Boolean; Value.Boolean=false;
    auto End=P::Clock::now()+std::chrono::seconds(1);
    auto False=P::Encode(Requests[0].Identity,Value,End);
    auto Corrupt=P::Encode(Requests[1].Identity,Value,End); Corrupt.back()^=1;
    auto Format=P::Encode(Requests[2].Identity,Value,End); Format[4]=2;
    // Expiration of the finished caller must not govern the later callback.
    GetVm(F.Vm)->Deadline=P::Clock::now()-std::chrono::seconds(1);
    Check(F.Complete(0,0,true,False)==CL_OK && F.Complete(1,0,true,Corrupt)==CL_OK && F.Complete(2,0,true,Format)==CL_OK,"bounded decode inputs admitted");
    F.Drain(3);
}
void CompletionCollection()
{
    // Synthetic transport only: a real VM and production codec, but the mock
    // HostDelegate echoes the accepted envelope instead of using a disk worker.
    Fixture F;
    F.Run(Prefix+R"(
        State.Weak=setmetatable({}, {__mode='v'})
        State.Cycle=0; State.Saved=0; State.Read=0
        State.SubmitSet=function()
            State.Cycle+=1
            local Byte=string.char(64+State.Cycle)
            local Input={string.rep(Byte,16384),string.rep('b',16384),string.rep('c',16384),string.rep('d',16315)}
            local Capture={Input=Input}
            local Callback=function(Saved,Err)
                assert(Saved==true and Err==nil and #Capture.Input[1]==16384)
                State.Saved+=1
            end
            State.Weak[1]=Input; State.Weak[2]=Capture; State.Weak[3]=Callback
            Store:SetAsync('Retention',Input,Callback)
        end
        State.SubmitGet=function()
            local Capture={Payload=string.rep('q',16384)}
            local Callback=function(Value,Err)
                assert(Err==nil and #Capture.Payload==16384 and #Value==4 and #Value[4]==16315)
                assert(Value[1]==string.rep(string.char(64+State.Cycle),16384))
                State.Weak[6]=Value; State.Read+=1
            end
            State.Weak[4]=Capture; State.Weak[5]=Callback
            Store:GetAsync('Retention',Callback)
        end
    )");
    auto Collect = [&] {
        auto* Runtime=GetVm(F.Vm);
        Check(Runtime && Runtime->State && Runtime->Owner==std::this_thread::get_id() &&
            !Runtime->Admission && !Runtime->Thread && !Runtime->ThreadId,"collection outside admission on VM owner thread");
        lua_gc(Runtime->State,LUA_GCCOLLECT,0);
        lua_gc(Runtime->State,LUA_GCCOLLECT,0);
        auto* Owner=GetDomain(*Runtime,F.Root);
        Check(Runtime->StorageReserved==0,"completed storage reservations empty before VM destruction");
        for(size_t I=0;I<Owner->StorageCallbacks.size();++I)
            Check(!Owner->StorageCallbacks[I] && Owner->StorageReservations[I]==0,"completed callback slots and routes empty");
        return Runtime->Used;
    };
    uint64_t Plateau=0, Minimum=UINT64_MAX, Maximum=0;
    for(unsigned Cycle=0;Cycle<32;++Cycle) {
        Check(Requests.empty() && Released.empty(),"synthetic transport buffers reset per cycle");
        F.Run("require('state').SubmitSet()");
        Check(Requests.size()==1 && Requests[0].Envelope.size()==65536,"collection fixture uses exact 64-KiB snapshot");
        F.Run("local W=require('state').Weak; assert(W[1] and W[2] and W[3])");
        P::Bytes Envelope=std::move(Requests[0].Envelope);
        Check(F.Complete(0,0,true)==CL_OK,"collection set completion"); F.Drain(1);
        Check(Released.size()==1,"collection set released once");
        Requests.clear(); Released.clear();
        Collect();
        F.Run("local S=require('state'); assert(S.Saved==S.Cycle); assert(next(S.Weak)==nil); S.SubmitGet()");
        Check(Requests.size()==1,"collection get submission");
        F.Run("local W=require('state').Weak; assert(W[4] and W[5])");
        Check(F.Complete(0,0,true,Envelope)==CL_OK,"collection get materialization input");
        // Drop mock-host byte ownership before delivery; native owns its copy.
        Requests.clear(); P::Bytes{}.swap(Envelope);
        F.Drain(1);
        Check(Released.size()==1,"collection get released once"); Released.clear();
        Collect();
        F.Run("local S=require('state'); assert(S.Read==S.Cycle and next(S.Weak)==nil)");
        uint64_t Used=Collect();
        // Deterministic weak-reference assertions above prove object release.
        // Allocator-accounted live VM bytes, not RSS, additionally bound plateau
        // noise after eight warm-up cycles below one more envelope per cycle.
        if(Cycle==7) Plateau=Used;
        if(Cycle>=7) {
            Minimum=std::min(Minimum,Used); Maximum=std::max(Maximum,Used);
            Check(Used<=Plateau+65536,"post-completion VM memory remains within bounded warm plateau");
        }
    }
    std::fprintf(stderr,"[CarbonLuau:PersistenceTest] Synthetic transport completion collection cycles=32 envelope=65536 weak-references=cleared plateau-bytes=%llu..%llu\n",
        static_cast<unsigned long long>(Minimum),static_cast<unsigned long long>(Maximum));
}
void SubmissionAllocationFaults()
{
    unsigned Failures=0, Successes=0;
    int LastPoint=0;
    for(int FailAfter=0;FailAfter<=128;++FailAfter) {
        LastPoint=FailAfter;
        Fixture F;
        // A large fresh value bypasses Luau's already-populated small-object
        // pages, so allocator injection actually reaches both failing and
        // successful profiles instead of measuring only cached allocations.
        const std::string Source=Prefix+"pcall(function() local Payload=string.rep('x',16384); Store:SetAsync('Fault',{A={true,false,Payload}},function() end) end)";
        ClHandle Thread=0; ClResult Result{};
        Check(cl_domain_load_source(F.Vm,F.Root,"submission.fault",Source.data(),uint32_t(Source.size()),&Thread,&Result)==CL_OK,"load allocation fixture");
        TestAllocationFailureAfter=FailAfter;
        ClStatus Status=cl_thread_resume(Thread,100000000,&Result);
        TestAllocationFailureAfter=-1;
        Check(Status==CL_OK || Status==CL_MEMORY_LIMIT,"controlled preaccept allocation status");
        if(Status==CL_MEMORY_LIMIT) ++Failures;
        else { Check(Requests.size()==1,"successful allocation profile accepts one request"); ++Successes; }
        Check(cl_thread_destroy(Thread)==((Result.Flags&1)?CL_INVALID_ARGUMENT:CL_OK),"fault thread release");
        auto* Runtime=GetVm(F.Vm);
        Check(Runtime->StorageReserved==(Runtime->State ? Requests.size() : 0),"no unaccepted callback reservation leak; fatal retirement suppresses accepted callbacks");
        if(Successes) break;
    }
    std::fprintf(stderr,"[CarbonLuau:PersistenceTest] Submission allocation sweep failures=%u successes=%u last-point=%d bound=128\n",
        Failures,Successes,LastPoint);
    Check(Failures>0 && Successes>0,"allocation sweep reaches failures and successful submission");
}
}
int main(int Count,char** Args)
{
    Check(Count==2,"compiler worker argument"); SetCompilerExecutableForTesting(Args[1]);
    Check(carbonluau_abi_version()==0x00010005,"ABI1.5 required for new completion ingress");
    ValuesAndResults(); RejectValuesAndBounds(); PublicationAndPrivacy(); SchedulingAndRaces(); FairDomains(); FailuresAndDeadlines();
    CorruptionAndFreshBudget(); CompletionCollection(); SubmissionAllocationFaults();
    ResetCompilerForTesting(); std::puts("[CarbonLuau:PersistenceTest] Public native conversion/publication/completion fixtures PASS");
}
