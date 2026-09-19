// White-box executable only; no test controls are exported by the production DLL.
#include "../../native/src/runtime/RuntimeInternal.hpp"
#include "../../native/src/scripts/Compiler.hpp"
#include <atomic>
#include <cstdio>

using namespace CarbonLuau::Runtime;

static void Check(bool Condition, const char* Message)
{
    if (!Condition) { std::fprintf(stderr, "[CarbonLuau:FaultTest] FAIL: %s\n", Message); std::exit(1); }
}
static unsigned FixtureRegistration = 0;
static uint32_t FixtureHost(uint64_t, uint32_t Operation, const char*, uint32_t, char* Output, uint32_t Capacity, uint32_t* Written)
{
    const char* Value = ""; size_t Length = 0;
    static const char Player[] = "1\00076561198000000001\000Fixture\000";
    static const char Info[] = "1\000Fixture\000";
    static const char Id[] = "1\000";
    if (Operation == 1 || Operation == 2) { Value = Player; Length = sizeof(Player) - 1; }
    else if (Operation == 3) { Value = Info; Length = sizeof(Info) - 1; }
    else if (Operation == 5) { Value = Id; Length = sizeof(Id) - 1; }
    else if (Operation == 6 || Operation == 8) {
        *Written = uint32_t(std::snprintf(Output, Capacity, "%u", ++FixtureRegistration) + 1); return 0;
    }
    if (Length > Capacity) return 1;
    std::memcpy(Output, Value, Length); *Written = uint32_t(Length); return 0;
}
int main(int ArgumentCount, char** Arguments)
{
    Vm Budget;
    Budget.Limit = 128;
    void* Pointer = Allocate(&Budget, nullptr, 99, 64);
    Check(Pointer && Budget.Used == 64, "fresh allocation ignores old type tag");
    TestAllocationFailureAfter = 0;
    Check(!Allocate(&Budget, Pointer, 64, 32) && Budget.Used == 64, "failed shrink keeps original accounting");
    TestAllocationFailureAfter = -1;
    Pointer = Allocate(&Budget, Pointer, 64, 32);
    Check(Pointer && Budget.Used == 32, "successful shrink");
    Check(!Allocate(&Budget, Pointer, 32, SIZE_MAX) && Budget.Used == 32, "overflow-safe refusal");
    Pointer = Allocate(&Budget, Pointer, 32, 128);
    Check(Pointer && Budget.Used == 128, "exact cap growth");
    Check(!Allocate(&Budget, nullptr, 0, 1) && Budget.Used == 128, "cap refuses extra byte");
    Allocate(&Budget, Pointer, 128, 0);
    Check(!Budget.Used && !TestLiveBytes, "free accounting");
    ClVmConfig Config{16 * MiB};
    int Failed = 0;
    for (int Position = 0; Position < 256; ++Position) {
        TestAllocationFailureAfter = Position;
        ClHandle Handle = 999;
        ClStatus Status = cl_vm_create(&Config, &Handle);
        if (Status == CL_OK) Check(cl_vm_destroy(Handle) == CL_OK, "successful init teardown");
        else { Check(Status == CL_MEMORY_LIMIT && !Handle, "failed init clears handle and classifies OOM"); ++Failed; }
        Check(!TestLiveBytes, "partial init does not leak allocator bytes");
    }
    Check(Failed > 1, "fault injection exercised partially initialized states");
    for (int Position = 0; Position < 64; ++Position) {
        TestAllocationFailureAfter = -1;
        ClHandle Handle = 0, ThreadHandle = 0;
        ClResult Result{};
        Check(cl_vm_create(&Config, &Handle) == CL_OK, "load fault VM");
        TestAllocationFailureAfter = Position;
        ClStatus Status = cl_vm_load_source(Handle, "fault", "return 3", 8, &ThreadHandle, &Result);
        Check(Status == CL_OK || Status == CL_MEMORY_LIMIT, "load fault classification");
        TestAllocationFailureAfter = -1;
        Check(cl_vm_destroy(Handle) == CL_OK && !TestLiveBytes, "load partial teardown");
    }
    for (int Mode = 0; Mode < 3; ++Mode) for (int Position = 0; Position < 256; ++Position) {
        TestAllocationFailureAfter = -1;
        ClHandle Handle = 0, ThreadHandle = 0; ClResult Result{};
        Check(cl_vm_create(&Config, &Handle) == CL_OK, "script fault VM");
        if (Mode == 0) TestAllocationFailureAfter = Position;
        ClStatus Status = cl_vm_scripts(Handle, 64);
        Check(Status == CL_OK || Status == CL_MEMORY_LIMIT, "script setup failure contained");
        if (Status == CL_OK && Mode != 0) {
            const char* ModuleSource = "return {Value=42}";
            Check(cl_vm_module(Handle, "value", ModuleSource, uint32_t(std::strlen(ModuleSource))) == CL_OK, "fault module source");
            const char* Source = Mode == 1 ? "local V=require('value'); task.defer(function(A) assert(A==42) end,V.Value)" :
                "task.defer(function() local V=require('value'); task.defer(function() assert(V.Value==42) end) end)";
            Check(cl_vm_load_source(Handle,"fault",Source,uint32_t(std::strlen(Source)),&ThreadHandle,&Result)==CL_OK,"fault source load");
            if (Mode == 1) TestAllocationFailureAfter = Position;
            Status=cl_thread_resume(ThreadHandle,100000000,&Result);
            Check(Status==CL_OK || Status==CL_MEMORY_LIMIT,"module/schedule fault contained");
            TestAllocationFailureAfter=-1;
            Check(cl_thread_destroy(ThreadHandle)==CL_OK,"fault thread release");
            if (Mode == 2) {
                ClSchedulerInfo Info{}; Check(cl_vm_scheduler(Handle,&Info)==CL_OK,"fault queue");
                TestAllocationFailureAfter=Position;
                uint32_t Ran=0;
                Status=cl_vm_callback(Handle,Info.NowNs,Info.Sequence,100000000,&Ran,&Result);
                Check(Ran && (Status==CL_OK || Status==CL_MEMORY_LIMIT),"callback module/schedule allocation contained");
            }
        }
        TestAllocationFailureAfter = -1;
        Check(cl_vm_destroy(Handle) == CL_OK && !TestLiveBytes, "script partial teardown retains zero bytes");
    }
    for (int Mode = 0; Mode < 4; ++Mode) for (int Position = 0; Position < 256; ++Position) {
        TestAllocationFailureAfter = -1;
        ClHandle Handle = 0, ThreadHandle = 0; ClResult Result{};
        Check(cl_vm_create(&Config, &Handle) == CL_OK && cl_vm_scripts(Handle, 256) == CL_OK, "facade fault VM");
        if (Mode == 0) TestAllocationFailureAfter = Position;
        FixtureRegistration = 0;
        ClStatus Status = cl_vm_facade(Handle, 1, FixtureHost);
        Check(Status == CL_OK || Status == CL_MEMORY_LIMIT, "facade installation fault contained");
        if (Status == CL_OK && Mode != 0) {
            const char* Source = "local P=game:GetService('Players'); local V=P:GetPlayers()[1]; assert(V.IsConnected); P.PlayerAdded:Connect(function(A) assert(A.UserId==V.UserId); A:SendMessage('ok') end); game:GetService('Commands'):Register('hello',{},function(C) assert(C.Player.IsConnected) end)";
            Check(cl_vm_load_source(Handle,"facade.fault",Source,uint32_t(std::strlen(Source)),&ThreadHandle,&Result)==CL_OK,"facade source");
            if (Mode == 1) TestAllocationFailureAfter = Position;
            Status = cl_thread_resume(ThreadHandle,100000000,&Result);
            Check(Status==CL_OK || Status==CL_MEMORY_LIMIT || Status==CL_RUNTIME_ERROR,"facade entry allocation contained");
            TestAllocationFailureAfter = -1;
            cl_thread_destroy(ThreadHandle);
            if (Mode >= 2) {
                static const char Event[] = "added\0001\0001\00076561198000000001\000Fixture\000";
                if (Mode == 2) TestAllocationFailureAfter = Position;
                Status=cl_vm_event(Handle,Event,sizeof(Event)-1);
                Check(Status==CL_OK || Status==CL_MEMORY_LIMIT,"facade admission allocation contained");
                if (Status==CL_OK && Mode == 3) {
                    ClSchedulerInfo Info{}; Check(cl_vm_scheduler(Handle,&Info)==CL_OK,"facade queue");
                    TestAllocationFailureAfter = Position; uint32_t Ran=0;
                    Status=cl_vm_callback(Handle,Info.NowNs,Info.Sequence,100000000,&Ran,&Result);
                    Check(Ran && (Status==CL_OK || Status==CL_MEMORY_LIMIT || Status==CL_RUNTIME_ERROR),"facade callback allocation contained");
                }
            }
        }
        TestAllocationFailureAfter=-1;
        Check(cl_vm_destroy(Handle)==CL_OK && !TestLiveBytes,"facade teardown has zero retained bytes");
    }
    {
        ClHandle Handle=0, ThreadHandle=0; ClResult Result{}; FixtureRegistration=0;
        Check(cl_vm_create(&Config,&Handle)==CL_OK && cl_vm_scripts(Handle,64)==CL_OK && cl_vm_facade(Handle,1,FixtureHost)==CL_OK,"facade timeout VM");
        const char* Source="game:GetService('Players').PlayerAdded:Connect(function(P) assert(P.IsConnected); while true do end end)";
        Check(cl_vm_load_source(Handle,"facade.timeout",Source,uint32_t(std::strlen(Source)),&ThreadHandle,&Result)==CL_OK && cl_thread_resume(ThreadHandle,100000000,&Result)==CL_OK,"facade timeout setup");
        Check(cl_thread_destroy(ThreadHandle)==CL_OK,"facade timeout host thread release");
        static const char Event[]="added\0001\0001\00076561198000000001\000Fixture\000";
        Check(cl_vm_event(Handle,Event,sizeof(Event)-1)==CL_OK,"facade timeout admission");
        ClSchedulerInfo Info{}; cl_vm_scheduler(Handle,&Info); uint32_t Ran=0;
        Check(cl_vm_callback(Handle,Info.NowNs,Info.Sequence,3000000,&Ran,&Result)==CL_TIMEOUT && Ran && (Result.Flags&1),"facade callback timeout retires whole VM");
        Check(cl_vm_event(Handle,Event,sizeof(Event)-1)==CL_INVALID_ARGUMENT,"retired facade rejects late event");
        Check(cl_vm_destroy(Handle)==CL_OK && !TestLiveBytes,"facade timeout teardown zero bytes");
    }
    if (ArgumentCount == 4) {
        ResetCompilerForTesting();
        ClHandle Handle=0, ThreadHandle=0; ClResult Result{};
        Check(cl_vm_create(&Config,&Handle)==CL_OK && cl_vm_scripts(Handle,64)==CL_OK,"compile containment VM");
        const char* ModuleSource="task.defer(function() error('must not publish') end); return 17";
        Check(cl_vm_module(Handle,"delayed",ModuleSource,uint32_t(std::strlen(ModuleSource)))==CL_OK,"compile containment module");
        Check(cl_vm_module(Handle,"fatal","return 23",9)==CL_OK,"compile timeout module");
        const char* Entry="local Ok=pcall(require,'delayed'); assert(not Ok); task.defer(function() print('parent') end)";
        Check(cl_vm_load_source(Handle,"compile.timeout",Entry,uint32_t(std::strlen(Entry)),&ThreadHandle,&Result)==CL_OK,"compile timeout entry prepared");
        SetCompilerExecutableForTesting(Arguments[3]);
        Check(cl_thread_resume(ThreadHandle,100000000,&Result)==CL_OK,"caught module compiler crash remains ordinary");
        ClSchedulerInfo Info{}; Check(cl_vm_scheduler(Handle,&Info)==CL_OK && Info.Modules==0 && Info.Queued==1,
            "caught compiler crash publishes no module cache or module resource");
        Check(cl_thread_destroy(ThreadHandle)==CL_OK,"compile crash thread cleanup");
        SetCompilerExecutableForTesting(Arguments[1]);
        ThreadHandle=0;
        const char* RetrySource="assert(require('delayed')==17)";
        Check(cl_vm_load_source(Handle,"compile.retry",RetrySource,uint32_t(std::strlen(RetrySource)),&ThreadHandle,&Result)==CL_OK &&
            cl_thread_resume(ThreadHandle,100000000,&Result)==CL_OK,"module compile retries after worker restart");
        Check(cl_thread_destroy(ThreadHandle)==CL_OK,"compile retry thread cleanup");
        Check(cl_vm_scheduler(Handle,&Info)==CL_OK && Info.Modules==1 && Info.Queued==2,"retry publishes cache and resource once");
        SetCompilerExecutableForTesting(Arguments[2]);
        ThreadHandle=999;
        std::atomic<bool> UnrelatedProgress{false}, WrongOwnerRejected{false};
        std::thread Unrelated([&] {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            ClHandle Other=0;
            WrongOwnerRejected = cl_vm_destroy(Handle)==CL_INVALID_ARGUMENT;
            UnrelatedProgress = cl_vm_create(&Config,&Other)==CL_OK && cl_vm_destroy(Other)==CL_OK;
        });
        Check(cl_vm_load_source(Handle,"entry.timeout","return 1",8,&ThreadHandle,&Result)==CL_TIMEOUT && ThreadHandle==0 && !(Result.Flags&1),
            "entry compiler timeout preserves healthy VM");
        Unrelated.join();
        Check(UnrelatedProgress && WrongOwnerRejected,"compiler wait releases registry without weakening VM owner validation");
        SetCompilerExecutableForTesting(Arguments[1]);
        Check(cl_vm_load_source(Handle,"entry.recovery","return 2",8,&ThreadHandle,&Result)==CL_OK,"entry compile recovers with same VM");
        Check(cl_thread_destroy(ThreadHandle)==CL_OK,"entry recovery thread cleanup");

        ClHandle Provider=0, Consumer=0;
        Check(cl_domain_create(Handle,64,&Provider)==CL_OK,"compiler public provider domain");
        const char* PublicSource="task.defer(function() error('public must not publish') end); return 19";
        Check(cl_domain_module(Handle,Provider,"late",PublicSource,uint32_t(std::strlen(PublicSource)))==CL_OK &&
            cl_domain_addon(Handle,Provider,"provider","1.0.0","")==CL_OK &&
            cl_domain_public_module(Handle,Provider,"late")==CL_OK && cl_domain_commit(Handle,Provider)==CL_OK,
            "compiler public provider setup");
        Check(cl_domain_create(Handle,64,&Consumer)==CL_OK && cl_domain_addon(Handle,Consumer,"consumer","1.0.0","")==CL_OK &&
            cl_domain_dependency(Handle,Consumer,"provider",Provider)==CL_OK,"compiler public consumer setup");
        const char* PublicEntry="assert(require('@provider/late')==19)";
        Check(cl_domain_load_source(Handle,Consumer,"public.failure",PublicEntry,uint32_t(std::strlen(PublicEntry)),&ThreadHandle,&Result)==CL_OK,
            "public compiler-failure entry prepared");
        Check(cl_vm_scheduler(Handle,&Info)==CL_OK,"public compiler-failure baseline");
        uint64_t BaselineModules=Info.Modules, BaselineQueued=Info.Queued;
        SetCompilerExecutableForTesting(Arguments[3]);
        Check(cl_thread_resume(ThreadHandle,100000000,&Result)==CL_RUNTIME_ERROR,"provisional public compiler failure controlled");
        Check(cl_thread_destroy(ThreadHandle)==CL_OK && cl_vm_scheduler(Handle,&Info)==CL_OK &&
            Info.Modules==BaselineModules && Info.Queued==BaselineQueued,"public compiler failure publishes no cache or resource");
        SetCompilerExecutableForTesting(Arguments[1]);
        Check(cl_domain_load_source(Handle,Consumer,"public.retry",PublicEntry,uint32_t(std::strlen(PublicEntry)),&ThreadHandle,&Result)==CL_OK &&
            cl_thread_resume(ThreadHandle,100000000,&Result)==CL_OK && cl_thread_destroy(ThreadHandle)==CL_OK &&
            cl_domain_commit(Handle,Consumer)==CL_OK,"provisional public compile retries and commits");
        Check(cl_vm_scheduler(Handle,&Info)==CL_OK && Info.Modules==BaselineModules+1 && Info.Queued==BaselineQueued+1,
            "public retry publishes cache and resource once");
        Check(cl_domain_destroy(Handle,Consumer)==CL_OK && cl_domain_destroy(Handle,Provider)==CL_OK,"compiler public domains teardown");

        const char* FatalEntry="local Ok=pcall(require,'fatal'); assert(not Ok); task.defer(function() error('deadline must retire') end)";
        Check(cl_vm_load_source(Handle,"module.timeout",FatalEntry,uint32_t(std::strlen(FatalEntry)),&ThreadHandle,&Result)==CL_OK,
            "module timeout entry prepared");
        SetCompilerExecutableForTesting(Arguments[2]);
        Check(cl_thread_resume(ThreadHandle,100000000,&Result)==CL_TIMEOUT && (Result.Flags&1),
            "caught module compiler timeout cannot extend original admitted deadline");
        Check(cl_thread_destroy(ThreadHandle)==CL_INVALID_ARGUMENT,"retired timeout thread is stale");
        SetCompilerExecutableForTesting(Arguments[1]);
        Check(cl_vm_destroy(Handle)==CL_OK && !TestLiveBytes,"compile containment teardown");
        ResetCompilerForTesting();
    }
    std::printf("[CarbonLuau:FaultTest] PASS: realloc growth/shrink/failure/overflow/free; 256 init fault positions (%d failed); 64 load; 768 script/module/callback + 1024 facade fault positions; facade event timeout; zero retained allocator bytes\n", Failed);
}
