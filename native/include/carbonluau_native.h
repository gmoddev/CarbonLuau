#pragma once
#include <stdint.h>

#if defined(_WIN32)
#define CARBONLUAU_EXPORT __declspec(dllexport)
#else
#define CARBONLUAU_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif
CARBONLUAU_EXPORT int carbonluau_probe(void);
/* ABI 1.0: fixed-width POD, cdecl, caller-owned buffers, opaque integer tokens.
   All pointer arguments must refer to valid caller-owned storage of stated size.
   Tokens are checked without dereferencing caller memory and never reused within
   a library lifetime. Each VM belongs to its creating OS thread. */
typedef uint64_t ClHandle;
typedef enum ClStatus {
    CL_OK = 0, CL_YIELDED = 1, CL_RUNTIME_ERROR = 2, CL_COMPILE_ERROR = 3,
    CL_MEMORY_LIMIT = 4, CL_TIMEOUT = 5, CL_INVALID_ARGUMENT = 6, CL_INTERNAL_ERROR = 7
} ClStatus;
typedef struct ClVmConfig { uint64_t MemoryLimitBytes; } ClVmConfig;
typedef struct ClVmInfo { uint64_t MemoryBytes; uint64_t MemoryLimitBytes; uint64_t Ready; } ClVmInfo;
typedef struct ClResult {
    double Number;
    uint32_t HasNumber;
    uint32_t Flags; /* 1 = VM retired; 2 = log truncated */
    char Error[2048];
    char Logs[4096];
} ClResult;
CARBONLUAU_EXPORT uint32_t carbonluau_abi_version(void);
CARBONLUAU_EXPORT ClStatus cl_luau_revision(char* Buffer, uint32_t Capacity);
CARBONLUAU_EXPORT ClStatus cl_vm_create(const ClVmConfig* Config, ClHandle* OutVm);
CARBONLUAU_EXPORT ClStatus cl_vm_destroy(ClHandle Vm);
CARBONLUAU_EXPORT ClStatus cl_vm_info(ClHandle Vm, ClVmInfo* Info);
CARBONLUAU_EXPORT ClStatus cl_vm_load_source(ClHandle Vm, const char* Chunk, const char* Source,
    uint32_t SourceLength, ClHandle* OutThread, ClResult* Result);
/* Relative nanosecond budget, converted to a steady_clock deadline internally.
   Allowed 1..100 ms. A fresh budget is mandatory on every resume. */
CARBONLUAU_EXPORT ClStatus cl_thread_resume(ClHandle Thread, uint64_t BudgetNs, ClResult* Result);
CARBONLUAU_EXPORT ClStatus cl_thread_destroy(ClHandle Thread);
/* ABI 1.1 additions. Script sources are snapshotted by the host before execution.
   Native callback references never escape their owning VM. The managed scheduler
   captures a cutoff and drives one callback per call, enforcing its frame budget. */
typedef struct ClSchedulerInfo {
    uint64_t NowNs, NextDueNs, Sequence, Queued, Modules, Rejected, Discarded;
} ClSchedulerInfo;
CARBONLUAU_EXPORT ClStatus cl_vm_scripts(ClHandle Vm, uint32_t MaxQueued);
CARBONLUAU_EXPORT ClStatus cl_vm_module(ClHandle Vm, const char* Name, const char* Source, uint32_t Length);
CARBONLUAU_EXPORT ClStatus cl_vm_scheduler(ClHandle Vm, ClSchedulerInfo* Info);
/* No eligible callback: OK with Ran=0. Otherwise consumes/releases exactly one.
   Cutoff time/sequence from scheduler() prevent same-drain recursive execution. */
CARBONLUAU_EXPORT ClStatus cl_vm_callback(ClHandle Vm, uint64_t CutoffNs, uint64_t Sequence,
    uint64_t BudgetNs, uint32_t* Ran, ClResult* Result);
#ifdef __cplusplus
}
#endif
