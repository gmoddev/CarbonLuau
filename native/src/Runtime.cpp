#include "carbonluau_native.h"
#include "lua.h"
#include "lualib.h"
#include "Luau/Compiler.h"
#include <array>
#include <cstdlib>
#include <memory>
#include <mutex>
#include <thread>
#include <limits>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <string>
#include <map>
#include <vector>
#include <algorithm>
#include <cmath>
#include "Bootstrap.h"

namespace {
constexpr uint64_t MiB = 1024 * 1024;
struct Module { std::string Source; int Reference = LUA_NOREF; bool Loading = false, Loaded = false; };
struct Callback { uint64_t Due, Sequence; lua_State* Thread; int Reference, Arguments; std::string Gate; };
struct Later { bool operator()(const Callback& A, const Callback& B) const {
    return A.Due > B.Due || (A.Due == B.Due && A.Sequence > B.Sequence); } };
struct Vm {
    uint64_t Id = 0;
    std::thread::id Owner = std::this_thread::get_id();
    uint64_t Used = 0, Limit = 0;
    bool AllocationFailed = false;
    lua_State* State = nullptr;
    lua_State* Thread = nullptr;
    int Reference = LUA_NOREF;
    uint64_t ThreadId = 0;
    bool Resumable = false;
    bool Started = false;
    std::chrono::steady_clock::time_point Deadline;
    char Chunk[128]{};
    char Logs[4096]{};
    size_t LogSize = 0;
    bool LogTruncated = false;
    bool Scripts = false, Sealed = false;
    uint32_t MaxQueued = 0;
    uint64_t Sequence = 0, Rejected = 0, Discarded = 0;
    size_t SourceBytes = 0;
    std::map<std::string, Module> Modules;
    std::vector<std::string> Loading;
    std::vector<Callback> Queue;
    ClHostCall Host = nullptr;
    uint64_t Generation = 0;
    int Dispatch = LUA_NOREF;
    std::unique_ptr<std::array<char, 262144>> HostBuffer;
    ~Vm() {
        if (State) {
            lua_callbacks(State)->interrupt = nullptr;
            for (const auto& Work : Queue) lua_unref(State, Work.Reference);
            Queue.clear();
            for (const auto& Entry : Modules) if (Entry.second.Loaded) lua_unref(State, Entry.second.Reference);
            Modules.clear();
            if (Reference != LUA_NOREF) lua_unref(State, Reference);
            if (Dispatch != LUA_NOREF) lua_unref(State, Dispatch);
            lua_close(State);
        }
    }
};
std::mutex RegistryMutex;
std::array<std::unique_ptr<Vm>, 32> Registry;
uint64_t NextId = 1;
#ifdef CARBONLUAU_TESTING
// Fault injection exists only in the standalone white-box test executable.
int TestAllocationFailureAfter = -1;
uint64_t TestLiveBytes = 0;
#endif

void* Allocate(void* Context, void* Pointer, size_t OldSize, size_t NewSize)
{
    Vm& Runtime = *static_cast<Vm*>(Context);
    // Luau supplies a type tag as OldSize for some fresh allocations.
    uint64_t Old = Pointer ? OldSize : 0;
    if (!NewSize) {
        std::free(Pointer);
        Runtime.Used -= Old;
#ifdef CARBONLUAU_TESTING
        TestLiveBytes -= Old;
#endif
        return nullptr;
    }
    if (Old > Runtime.Used || NewSize > Runtime.Limit - (Runtime.Used - Old)) {
        Runtime.AllocationFailed = true;
        return nullptr;
    }
#ifdef CARBONLUAU_TESTING
    if (TestAllocationFailureAfter == 0) { Runtime.AllocationFailed = true; return nullptr; }
    if (TestAllocationFailureAfter > 0) --TestAllocationFailureAfter;
#endif
    void* Result = std::realloc(Pointer, NewSize);
    if (!Result) { Runtime.AllocationFailed = true; return nullptr; }
    Runtime.Used = Runtime.Used - Old + NewSize;
#ifdef CARBONLUAU_TESTING
    TestLiveBytes = TestLiveBytes - Old + NewSize;
#endif
    return Result;
}
Vm* GetVm(ClHandle Id)
{
    for (auto& Entry : Registry)
        if (Entry && Entry->Id == Id && Entry->Owner == std::this_thread::get_id()) return Entry.get();
    return nullptr;
}

Vm* GetThread(ClHandle Id)
{
    if (!Id) return nullptr;
    for (auto& Entry : Registry)
        if (Entry && Entry->ThreadId == Id && Entry->Owner == std::this_thread::get_id()) return Entry.get();
    return nullptr;
}

// Deliberately not std::exception: script pcall/xpcall must not catch a host
// cancellation. Only thrown at non-GC safepoints. Never reuse the interrupted
// state: close the complete global VM immediately at the ABI boundary.
struct DeadlineExceeded {};
void Interrupt(lua_State* State, int Gc)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    if (Gc < 0 && std::chrono::steady_clock::now() >= Runtime.Deadline) throw DeadlineExceeded{};
}

void Append(Vm& Runtime, const char* Text, size_t Length)
{
    for (size_t Index = 0; Index < Length; ++Index) {
        if (Runtime.LogSize == sizeof(Runtime.Logs) - 1) { Runtime.LogTruncated = true; break; }
        Runtime.Logs[Runtime.LogSize++] = Text[Index];
    }
    Runtime.Logs[Runtime.LogSize] = 0;
}
int Print(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    if (Runtime.LogTruncated) return 0;
    int Count = lua_gettop(State);
    for (int Index = 1; Index <= Count && Index <= 32; ++Index) {
        if (Index > 1) Append(Runtime, "\t", 1);
        char Text[64];
        const char* Value = nullptr;
        size_t Length = 0;
        switch (lua_type(State, Index)) {
        case LUA_TSTRING:
            Value = lua_tolstring(State, Index, &Length);
            for (size_t Character = 0; Character < Length && Character < 256; ++Character) {
                char Byte = static_cast<unsigned char>(Value[Character]) < 32 ? '.' : Value[Character];
                Append(Runtime, &Byte, 1);
            }
            if (Length > 256) Append(Runtime, "...", 3);
            continue;
        case LUA_TNUMBER: std::snprintf(Text, sizeof(Text), "%.17g", lua_tonumber(State, Index)); Value = Text; break;
        case LUA_TBOOLEAN: Value = lua_toboolean(State, Index) ? "true" : "false"; break;
        case LUA_TNIL: Value = "nil"; break;
        default: std::snprintf(Text, sizeof(Text), "<%s>", lua_typename(State, lua_type(State, Index))); Value = Text; break;
        }
        Append(Runtime, Value, std::strlen(Value));
    }
    if (Count > 32) Append(Runtime, "...", 3);
    Append(Runtime, "\n", 1);
    return 0;
}

int Initialize(lua_State* State)
{
    luaopen_base(State);
    lua_settop(State, 0);
    lua_newtable(State);
    const char* Allowed[] = {"assert", "error", "getmetatable", "next", "ipairs", "pairs", "pcall", "xpcall",
        "rawequal", "rawget", "rawset", "rawlen", "select", "setmetatable", "tonumber", "tostring", "type", "typeof", "_VERSION"};
    for (const char* Name : Allowed) { lua_getglobal(State, Name); lua_setfield(State, -2, Name); }
    lua_pushvalue(State, -1);
    lua_setfield(State, -2, "_G");
    lua_replace(State, LUA_GLOBALSINDEX);
    const luaL_Reg Libraries[] = {{"math", luaopen_math}, {"string", luaopen_string}, {"table", luaopen_table},
        {"coroutine", luaopen_coroutine}, {"bit32", luaopen_bit32}, {"utf8", luaopen_utf8},
        {"buffer", luaopen_buffer}, {"vector", luaopen_vector}};
    for (const auto& Library : Libraries) {
        lua_pushcfunction(State, Library.func, Library.name);
        lua_pushstring(State, Library.name);
        lua_call(State, 1, 0);
    }
    lua_pushcfunction(State, Print, "print");
    lua_setglobal(State, "print");
    luaL_sandbox(State);
    return 0;
}

void Retire(Vm& Runtime)
{
    Runtime.Discarded += Runtime.Queue.size();
    Runtime.Queue.clear();
    Runtime.Modules.clear();
    Runtime.Loading.clear();
    Runtime.Thread = nullptr;
    Runtime.ThreadId = 0;
    Runtime.Reference = LUA_NOREF;
    Runtime.Resumable = false;
    Runtime.Host = nullptr;
    Runtime.Dispatch = LUA_NOREF;
    lua_State* Owned = Runtime.State;
    Runtime.State = nullptr;
    if (Owned) { lua_callbacks(Owned)->interrupt = nullptr; lua_close(Owned); }
}

void Diagnostic(Vm& Runtime, ClResult& Result, const char* Message, lua_State* Trace = nullptr)
{
    std::snprintf(Result.Error, sizeof(Result.Error), "%s: %s\n%s", Runtime.Chunk, Message,
        Trace ? lua_debugtrace(Trace) : "");
}
int CreateThread(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_touserdata(State, 1));
    Runtime.Thread = lua_newthread(State);
    Runtime.Reference = lua_ref(State, -1);
    return 0;
}
int SandboxThread(lua_State* State) { luaL_sandboxthread(State); return 0; }
int Collect(lua_State* State) { lua_gc(State, LUA_GCCOLLECT, 0); return 0; }
int StepCollect(lua_State* State) { lua_gc(State, LUA_GCSTEP, 16); return 0; }
void ReleaseThread(Vm& Runtime, bool Full = true)
{
    Runtime.Thread = nullptr;
    Runtime.ThreadId = 0;
    Runtime.Resumable = false;
    if (Runtime.Reference != LUA_NOREF) lua_unref(Runtime.State, Runtime.Reference);
    Runtime.Reference = LUA_NOREF;
    // No script callbacks/finalizers exist in this surface. Reclaim failed
    // execution environments, coroutine stacks and allocation-bomb objects.
    if (lua_cpcall(Runtime.State, Full ? Collect : StepCollect, nullptr) != LUA_OK) Retire(Runtime);
    else lua_settop(Runtime.State, 0);
}
#include "Scripts.inl"
#include "Facade.inl"
}

uint32_t carbonluau_abi_version(void) { return 0x00010002; }
ClStatus cl_luau_revision(char* Buffer, uint32_t Capacity)
{
    if (!Buffer || Capacity < sizeof(CARBONLUAU_REVISION)) return CL_INVALID_ARGUMENT;
    std::memcpy(Buffer, CARBONLUAU_REVISION, sizeof(CARBONLUAU_REVISION));
    return CL_OK;
}

ClStatus cl_vm_create(const ClVmConfig* Config, ClHandle* OutVm) try
{
    if (OutVm) *OutVm = 0;
    if (!Config || !OutVm || Config->MemoryLimitBytes < 16 * MiB || Config->MemoryLimitBytes > 256 * MiB)
        return CL_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    for (auto& Entry : Registry) if (!Entry) {
        if (NextId == std::numeric_limits<uint64_t>::max()) return CL_INTERNAL_ERROR;
        auto Candidate = std::make_unique<Vm>();
        Candidate->Limit = Config->MemoryLimitBytes;
        Candidate->State = lua_newstate(Allocate, Candidate.get());
        if (!Candidate->State) return CL_MEMORY_LIMIT;
        lua_callbacks(Candidate->State)->userdata = Candidate.get();
        int Status = lua_cpcall(Candidate->State, Initialize, nullptr);
        if (Status != LUA_OK) return Status == LUA_ERRMEM ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
        lua_settop(Candidate->State, 0);
        Candidate->Id = NextId++;
        *OutVm = Candidate->Id;
        Entry = std::move(Candidate);
        return CL_OK;
    }
    return CL_INTERNAL_ERROR;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_destroy(ClHandle Id) try
{
    if (!Id) return CL_OK;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    for (auto& Entry : Registry) if (Entry.get() == Runtime) { Entry.reset(); return CL_OK; }
    return CL_INTERNAL_ERROR;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_info(ClHandle Id, ClVmInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    *Info = {Runtime->Used, Runtime->Limit, Runtime->State ? uint64_t(1) : uint64_t(0)};
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_load_source(ClHandle Id, const char* Chunk, const char* Source, uint32_t Length,
    ClHandle* OutThread, ClResult* Result) try
{
    if (OutThread) *OutThread = 0;
    if (Result) *Result = {};
    if (!Chunk || !Source || !OutThread || !Result || Length > 65536) return CL_INVALID_ARGUMENT;
    size_t ChunkLength = 0;
    while (ChunkLength < 128 && Chunk[ChunkLength]) {
        unsigned char Character = Chunk[ChunkLength++];
        if (!(Character >= 'a' && Character <= 'z') && !(Character >= 'A' && Character <= 'Z') &&
            !(Character >= '0' && Character <= '9') && Character != '_' && Character != '-' && Character != '.') return CL_INVALID_ARGUMENT;
    }
    if (!ChunkLength || ChunkLength >= 128) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    if (!Runtime->State) { Result->Flags = 1; return CL_INTERNAL_ERROR; }
    Runtime->Sealed = true;
    std::memcpy(Runtime->Chunk, Chunk, ChunkLength + 1);
    Runtime->AllocationFailed = false;
    try {
        Luau::CompileOptions Options;
        Options.optimizationLevel = 1;
        Options.debugLevel = 1;
        std::string Bytecode = Luau::compile(std::string(Source, Length), Options);
        if (Bytecode.size() > 1024 * 1024) {
            Diagnostic(*Runtime, *Result, "compiled bytecode exceeds 1 MiB ingestion bound");
            return CL_INVALID_ARGUMENT;
        }
        if (Bytecode.empty()) { Diagnostic(*Runtime, *Result, "compiler returned no bytecode"); return CL_INTERNAL_ERROR; }
        if (Bytecode[0] == 0) {
            Diagnostic(*Runtime, *Result, Bytecode.c_str() + 1);
            return CL_COMPILE_ERROR;
        }
        int Status = lua_cpcall(Runtime->State, CreateThread, Runtime);
        if (Status == LUA_OK) Status = lua_cpcall(Runtime->Thread, SandboxThread, nullptr);
        if (Status == LUA_OK) {
            lua_settop(Runtime->Thread, 0);
            Status = luau_load(Runtime->Thread, Runtime->Chunk, Bytecode.data(), Bytecode.size(), 0);
        }
        if (Status != LUA_OK) {
            Diagnostic(*Runtime, *Result, "source loading failed");
            bool Memory = Runtime->AllocationFailed || Status == LUA_ERRMEM;
            Retire(*Runtime);
            Result->Flags = 1;
            return Memory ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
        }
        if (NextId == std::numeric_limits<uint64_t>::max()) { Retire(*Runtime); Result->Flags = 1; return CL_INTERNAL_ERROR; }
        Runtime->ThreadId = NextId++;
        Runtime->Resumable = true;
        Runtime->Started = false;
        *OutThread = Runtime->ThreadId;
        return CL_OK;
    } catch (...) {
        bool Memory = Runtime->AllocationFailed;
        Diagnostic(*Runtime, *Result, "native compile/load failure; VM retired");
        Retire(*Runtime);
        Result->Flags = 1;
        return Memory ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
    }
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_scripts(ClHandle Id, uint32_t MaxQueued) try
{
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->Sealed || Runtime->Scripts || MaxQueued < 1 || MaxQueued > 4096) return CL_INVALID_ARGUMENT;
    Runtime->Queue.reserve(MaxQueued);
    Runtime->Loading.reserve(32);
    Runtime->MaxQueued = MaxQueued;
    if (lua_cpcall(Runtime->State, InstallScripts, nullptr) != LUA_OK) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    Runtime->Scripts = true;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_module(ClHandle Id, const char* Name, const char* Source, uint32_t Length) try
{
    if (!Name || !Source || Length > 65536) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || !Runtime->Scripts || Runtime->Sealed || Runtime->Modules.size() >= 256 ||
        Runtime->SourceBytes + Length > 4 * MiB || !ModuleName(Name, 128)) return CL_INVALID_ARGUMENT;
    auto Added = Runtime->Modules.emplace(Name, Module{std::string(Source, Length)});
    if (!Added.second) return CL_INVALID_ARGUMENT;
    Runtime->SourceBytes += Length;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_scheduler(ClHandle Id, ClSchedulerInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->Scripts) return CL_INVALID_ARGUMENT;
    Info->NowNs = NowNs(); Info->Sequence = Runtime->Sequence;
    Info->Queued = Runtime->Queue.size(); Info->Rejected = Runtime->Rejected;
    Info->Discarded = Runtime->Discarded;
    Info->NextDueNs = Runtime->Queue.empty() ? 0 : Runtime->Queue.front().Due;
    for (const auto& Entry : Runtime->Modules) if (Entry.second.Loaded) ++Info->Modules;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_callback(ClHandle Id, uint64_t CutoffNs, uint64_t Sequence, uint64_t BudgetNs,
    uint32_t* Ran, ClResult* Result) try
{
    if (Ran) *Ran = 0;
    if (Result) *Result = {};
    if (!Ran || !Result || BudgetNs < 1000000 || BudgetNs > 100000000) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || !Runtime->Scripts || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    if (Runtime->Queue.empty() || Runtime->Queue.front().Due > CutoffNs || Runtime->Queue.front().Sequence > Sequence) return CL_OK;
    std::pop_heap(Runtime->Queue.begin(), Runtime->Queue.end(), Later{});
    Callback Work = std::move(Runtime->Queue.back()); Runtime->Queue.pop_back();
    *Ran = 1;
    Runtime->Thread = Work.Thread; Runtime->Reference = Work.Reference;
    Runtime->LogSize = 0; Runtime->Logs[0] = 0; Runtime->LogTruncated = false;
    Runtime->AllocationFailed = false;
    if (!Work.Gate.empty()) {
        uint32_t Written = 0;
        if (!Runtime->Host || Runtime->Host(Runtime->Generation, 9, Work.Gate.data(), uint32_t(Work.Gate.size()),
            Runtime->HostBuffer->data(), uint32_t(Runtime->HostBuffer->size()), &Written) != 0) {
            ++Runtime->Rejected;
            ReleaseThread(*Runtime, false);
            if (!Runtime->State) Result->Flags = 1;
            return CL_OK;
        }
    }
    std::snprintf(Runtime->Chunk, sizeof(Runtime->Chunk), "scheduled-%llu", (unsigned long long)Work.Sequence);
    Runtime->Deadline = std::chrono::steady_clock::now() + std::chrono::nanoseconds(BudgetNs);
    lua_callbacks(Runtime->State)->interrupt = Interrupt;
    ClStatus Status;
    try {
        int Code = lua_resume(Work.Thread, nullptr, Work.Arguments);
        lua_callbacks(Runtime->State)->interrupt = nullptr;
        if (Runtime->AllocationFailed || Code == LUA_ERRMEM) {
            Status = CL_MEMORY_LIMIT; Diagnostic(*Runtime, *Result, "callback memory limit", Work.Thread);
        } else if (Code == LUA_OK) Status = CL_OK;
        else {
            Status = CL_RUNTIME_ERROR;
            const char* Message = Code == LUA_YIELD ? "scheduled callbacks cannot yield" :
                (lua_type(Work.Thread, -1) == LUA_TSTRING ? lua_tostring(Work.Thread, -1) : "callback non-string error");
            Diagnostic(*Runtime, *Result, Message, Work.Thread);
        }
        ReleaseThread(*Runtime, Status == CL_MEMORY_LIMIT);
        if (!Runtime->State) Result->Flags |= 1;
    } catch (const DeadlineExceeded&) {
        Status = CL_TIMEOUT; Diagnostic(*Runtime, *Result, "callback deadline exceeded; VM retired");
        Retire(*Runtime); Result->Flags |= 1;
    } catch (...) {
        Status = CL_INTERNAL_ERROR; Diagnostic(*Runtime, *Result, "callback native failure; VM retired");
        Retire(*Runtime); Result->Flags |= 1;
    }
    std::memcpy(Result->Logs, Runtime->Logs, sizeof(Result->Logs));
    if (Runtime->LogTruncated) Result->Flags |= 2;
    return Status;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_thread_resume(ClHandle Id, uint64_t BudgetNs, ClResult* Result) try
{
    if (Result) *Result = {};
    if (!Result || BudgetNs < 1000000 || BudgetNs > 100000000) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetThread(Id);
    if (!Runtime || !Runtime->Resumable) return CL_INVALID_ARGUMENT;
    Runtime->LogSize = 0;
    Runtime->Logs[0] = 0;
    Runtime->LogTruncated = false;
    Runtime->AllocationFailed = false;
    Runtime->Deadline = std::chrono::steady_clock::now() + std::chrono::nanoseconds(BudgetNs);
    lua_callbacks(Runtime->State)->interrupt = Interrupt;
    ClStatus Status = CL_INTERNAL_ERROR;
    try {
        if (Runtime->Started) lua_settop(Runtime->Thread, 0);
        Runtime->Started = true;
        int Code = lua_resume(Runtime->Thread, nullptr, 0);
        lua_callbacks(Runtime->State)->interrupt = nullptr;
        Runtime->Resumable = Code == LUA_YIELD;
        if (Runtime->AllocationFailed || Code == LUA_ERRMEM) {
            Status = CL_MEMORY_LIMIT;
            Diagnostic(*Runtime, *Result, "VM allocation limit/exhaustion", Runtime->Thread);
            // Even a pcall-caught allocation refusal invalidates this execution.
            Runtime->Resumable = false;
        } else if (Code == LUA_OK || Code == LUA_YIELD) {
            Status = Code == LUA_OK ? CL_OK : CL_YIELDED;
            if (lua_gettop(Runtime->Thread) && lua_type(Runtime->Thread, 1) == LUA_TNUMBER) {
                Result->Number = lua_tonumber(Runtime->Thread, 1); Result->HasNumber = 1;
            }
        } else {
            Status = CL_RUNTIME_ERROR;
            const char* Message = lua_type(Runtime->Thread, -1) == LUA_TSTRING ? lua_tostring(Runtime->Thread, -1) : "non-string runtime error";
            Diagnostic(*Runtime, *Result, Message, Runtime->Thread);
        }
    } catch (const DeadlineExceeded&) {
        Status = CL_TIMEOUT;
        Diagnostic(*Runtime, *Result, "monotonic execution deadline exceeded; VM retired");
        Retire(*Runtime);
        Result->Flags |= 1;
    } catch (...) {
        Status = CL_INTERNAL_ERROR;
        Diagnostic(*Runtime, *Result, "unexpected native execution failure; VM retired");
        Retire(*Runtime);
        Result->Flags |= 1;
    }
    std::memcpy(Result->Logs, Runtime->Logs, sizeof(Result->Logs));
    if (Runtime->LogTruncated) Result->Flags |= 2;
    return Status;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_thread_destroy(ClHandle Id) try
{
    if (!Id) return CL_OK;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetThread(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    ReleaseThread(*Runtime);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }
