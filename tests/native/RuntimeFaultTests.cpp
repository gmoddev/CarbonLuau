// White-box executable only; no test controls are exported by the production DLL.
#include "../../native/src/runtime/RuntimeInternal.hpp"
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
int main()
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
    std::printf("[CarbonLuau:FaultTest] PASS: realloc growth/shrink/failure/overflow/free; 256 init fault positions (%d failed); 64 load; 768 script/module/callback + 1024 facade fault positions; facade event timeout; zero retained allocator bytes\n", Failed);
}
