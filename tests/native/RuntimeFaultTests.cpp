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
    std::printf("[CarbonLuau:FaultTest] PASS: realloc growth/shrink/failure/overflow/free; 256 init fault positions (%d failed); 64 load fault positions; zero retained allocator bytes\n", Failed);
}
