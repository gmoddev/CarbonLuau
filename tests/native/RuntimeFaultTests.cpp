// White-box executable only; no test controls are exported by the production DLL.
#define CARBONLUAU_TESTING
#include "../../native/src/Runtime.cpp"
#include <cstdio>

static void Check(bool Condition, const char* Message)
{
    if (!Condition) { std::fprintf(stderr, "[CarbonLuau:FaultTest] FAIL: %s\n", Message); std::exit(1); }
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
    std::printf("[CarbonLuau:FaultTest] PASS: realloc growth/shrink/failure/overflow/free; 256 init fault positions (%d failed); 64 load; 768 script/module/callback fault positions; zero retained allocator bytes\n", Failed);
}
