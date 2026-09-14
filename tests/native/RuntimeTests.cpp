#include "carbonluau_native.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <chrono>

static void Check(bool Value, const char* Message)
{
    if (!Value) { std::fprintf(stderr, "[CarbonLuau:Test] FAIL: %s\n", Message); std::exit(1); }
}
static ClResult Execute(ClHandle Vm, const char* Source, ClStatus Expected, uint64_t Budget = 100000000)
{
    ClHandle Thread = 0;
    ClResult Result{};
    ClStatus Status = cl_vm_load_source(Vm, "fixture", Source, static_cast<uint32_t>(std::strlen(Source)), &Thread, &Result);
    if (Status == CL_OK) {
        Status = cl_thread_resume(Thread, Budget, &Result);
        Check(cl_thread_destroy(Thread) == ((Result.Flags & 1) ? CL_INVALID_ARGUMENT : CL_OK), "thread release");
        ClResult StaleResult{};
        Check(cl_thread_resume(Thread, Budget, &StaleResult) == CL_INVALID_ARGUMENT, "stale thread rejected");
    }
    if (Status != Expected) std::fprintf(stderr, "Expected %d, got %d: %s\n", Expected, Status, Source);
    Check(Status == Expected, "execution status");
    return Result;
}
int main()
{
    ClVmConfig Config{16 * 1024 * 1024};
    Check(carbonluau_abi_version() == 0x00010001, "ABI version (compatible 1.1 extension)");
    for (int Index = 0; Index < 1000; ++Index) {
        ClHandle Vm = 0;
        Check(cl_vm_create(&Config, &Vm) == CL_OK && Vm, "create");
        ClVmInfo Info{};
        Check(cl_vm_info(Vm, &Info) == CL_OK && Info.MemoryBytes > 0 && Info.MemoryBytes < Info.MemoryLimitBytes, "allocation accounting");
        Execute(Vm, "return 1 + 2", CL_OK);
        if (Index % 10 == 0) {
            Execute(Vm, "local =", CL_COMPILE_ERROR);
            Execute(Vm, "error('intentional runtime failure')", CL_RUNTIME_ERROR);
            Execute(Vm, "return 3", CL_OK);
            Execute(Vm, "local T = {}; for I = 1, 100 do T[I] = buffer.create(1024 * 1024) end", CL_MEMORY_LIMIT);
            Execute(Vm, "return 3", CL_OK);
            Execute(Vm, "while true do end", CL_TIMEOUT, 1000000);
            Check(cl_vm_info(Vm, &Info) == CL_OK && !Info.Ready && !Info.MemoryBytes, "timeout fully retires VM");
        }
        Check(cl_vm_destroy(Vm) == CL_OK, "destroy");
        Check(cl_vm_info(Vm, &Info) == CL_INVALID_ARGUMENT, "stale handle");
        Check(cl_vm_destroy(Vm) == CL_INVALID_ARGUMENT, "double destroy");
    }
    Check(cl_vm_destroy(0) == CL_OK, "null destroy");
    ClHandle Vm = 99;
    Check(cl_vm_create(nullptr, &Vm) == CL_INVALID_ARGUMENT && Vm == 0, "null config clears handle");
    Check(cl_vm_create(&Config, nullptr) == CL_INVALID_ARGUMENT, "null output");
    ClVmConfig Bad{1};
    Check(cl_vm_create(&Bad, &Vm) == CL_INVALID_ARGUMENT, "cap lower bound");
    Bad.MemoryLimitBytes = UINT64_MAX;
    Check(cl_vm_create(&Bad, &Vm) == CL_INVALID_ARGUMENT, "cap upper/overflow bound");
    Check(cl_vm_create(&Config, &Vm) == CL_OK, "fixture VM");
    std::thread Other([&] { ClVmInfo Info{}; Check(cl_vm_info(Vm, &Info) == CL_INVALID_ARGUMENT, "thread affinity"); });
    Other.join();
    ClHandle Thread = 0;
    ClResult Result{};
    Check(cl_vm_load_source(Vm, "arithmetic", "return 1+2", 10, &Thread, &Result) == CL_OK, "load arithmetic");
    Check(cl_thread_resume(Thread, 0, &Result) == CL_INVALID_ARGUMENT, "mandatory budget");
    Check(cl_thread_resume(Thread, 3000000, &Result) == CL_OK && Result.HasNumber && Result.Number == 3, "result POD");
    Check(cl_thread_resume(Thread, 3000000, &Result) == CL_INVALID_ARGUMENT, "completed thread not resumable");
    Check(cl_thread_destroy(Thread) == CL_OK, "thread cleanup");
    const char* Yielding = "coroutine.yield(7); return 3";
    Check(cl_vm_load_source(Vm, "yield", Yielding, static_cast<uint32_t>(std::strlen(Yielding)), &Thread, &Result) == CL_OK, "yield load");
    Check(cl_thread_resume(Thread, 3000000, &Result) == CL_YIELDED && Result.Number == 7, "yield result");
    Check(cl_thread_resume(Thread, 3000000, &Result) == CL_OK && Result.Number == 3, "fresh-budget resume");
    Check(cl_thread_destroy(Thread) == CL_OK, "yield cleanup");
    Check(cl_vm_load_source(Vm, "syntax", "local =", 7, &Thread, &Result) == CL_COMPILE_ERROR && std::strstr(Result.Error, "syntax"), "compile diagnostic");
    const char* Failure = "local function Boom() error('intentional runtime failure') end Boom()";
    Check(cl_vm_load_source(Vm, "trace", Failure, static_cast<uint32_t>(std::strlen(Failure)), &Thread, &Result) == CL_OK, "load error");
    Check(cl_thread_resume(Thread, 100000000, &Result) == CL_RUNTIME_ERROR && std::strstr(Result.Error, "intentional runtime failure") && std::strstr(Result.Error, "trace") && std::strstr(Result.Error, "Boom"), "runtime traceback");
    Check(cl_thread_destroy(Thread) == CL_OK, "error cleanup");
    const char* Logging = "print(); print('hello', 3, true, nil); print(setmetatable({}, {__tostring = function() error('must not run') end}))";
    Check(cl_vm_load_source(Vm, "print", Logging, static_cast<uint32_t>(std::strlen(Logging)), &Thread, &Result) == CL_OK, "load print");
    Check(cl_thread_resume(Thread, 100000000, &Result) == CL_OK && std::strstr(Result.Logs, "hello\t3\ttrue\tnil") && std::strstr(Result.Logs, "<table>"), "safe bounded print");
    Check(cl_thread_destroy(Thread) == CL_OK, "print cleanup");
    const char* Flood = "for I = 1, 10000 do print(string.rep('x', 1024)) end";
    Check(cl_vm_load_source(Vm, "flood", Flood, static_cast<uint32_t>(std::strlen(Flood)), &Thread, &Result) == CL_OK, "load flood");
    Check(cl_thread_resume(Thread, 100000000, &Result) == CL_OK && (Result.Flags & 2) && std::strlen(Result.Logs) == 4095, "log bound");
    Check(cl_thread_destroy(Thread) == CL_OK, "flood cleanup");
    Execute(Vm, R"(
        assert(math.sqrt(9) == 3 and string.upper('hi') == 'HI')
        assert(table.concat({'a','b'}) == 'ab' and bit32.band(3,1) == 1)
        assert(utf8.len('hello') == 5 and buffer.len(buffer.create(4)) == 4)
        assert(type(coroutine.create(function() end)) == 'thread')
        assert(vector.magnitude(vector.create(3,0,0)) == 3)
        for _, Name in {'os','io','debug','package','require','load','loadfile','loadstring','dofile',
            'getfenv','setfenv','collectgarbage','newproxy','gcinfo','game','task','System','Carbon','BasePlayer'} do
            assert(_G[Name] == nil, Name)
        end
        assert(not pcall(function() math.sqrt = nil end))
        assert(not pcall(function() rawset(_G, 'Escape', true) end))
        assert(not pcall(function() getmetatable('').__index = nil end))
        Transient = true
    )", CL_OK);
    Execute(Vm, "assert(Transient == nil and Escape == nil); return 3", CL_OK);
    Execute(Vm, "local T = {}; pcall(function() for I=1,100 do T[I]=buffer.create(1024*1024) end end)", CL_MEMORY_LIMIT);
    Execute(Vm, "return 3", CL_OK);
    std::string TooLarge(65537, ' ');
    Check(cl_vm_load_source(Vm, "large", TooLarge.data(), 65537, &Thread, &Result) == CL_INVALID_ARGUMENT, "source cap");
    Check(cl_vm_load_source(Vm, "../path", "", 0, &Thread, &Result) == CL_INVALID_ARGUMENT, "chunk identity");
    Check(cl_vm_load_source(UINT64_MAX, "fixture", "", 0, &Thread, &Result) == CL_INVALID_ARGUMENT, "invalid VM");
    Check(cl_vm_info(Vm, nullptr) == CL_INVALID_ARGUMENT, "null info");
    Check(cl_thread_resume(UINT64_MAX, 3000000, &Result) == CL_INVALID_ARGUMENT, "invalid thread");
    Check(cl_thread_destroy(0) == CL_OK, "null thread destruction");
    Check(cl_vm_destroy(Vm) == CL_OK, "fixture VM cleanup");
    const char* Loops[] = {
        "while true do pcall(function() while true do end end) end",
        "coroutine.wrap(function() while true do end end)()",
        "local T=setmetatable({}, {__index=function() while true do pcall(function() while true do end end) end end}); return T.X",
        "table.sort({2,1}, function() while true do pcall(function() while true do end end) end end)",
        "xpcall(function() while true do end end, function() while true do end end)"
    };
    for (const char* Loop : Loops) {
        Check(cl_vm_create(&Config, &Vm) == CL_OK, "watchdog create");
        auto Start = std::chrono::steady_clock::now();
        Execute(Vm, Loop, CL_TIMEOUT, 3000000);
        Check(std::chrono::steady_clock::now() - Start < std::chrono::seconds(2), "watchdog elapsed bound");
        Check(cl_vm_destroy(Vm) == CL_OK, "watchdog retire cleanup");
        Check(cl_vm_create(&Config, &Vm) == CL_OK, "healthy replacement");
        Execute(Vm, "return 3", CL_OK);
        Check(cl_vm_destroy(Vm) == CL_OK, "replacement cleanup");
    }
    std::puts("[CarbonLuau:Test] PASS: 1000 VM cycles; 100 error/memory/timeout sequences; arithmetic, sandbox, logs, traceback, opaque handles, affinity, watchdog escape cases");
}
