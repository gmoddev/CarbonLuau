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
#include <iterator>
#include <string>
#include <map>
#include <vector>
#include <algorithm>
#include <cmath>
#include "Bootstrap.h"

namespace {
constexpr uint64_t MiB = 1024 * 1024;
struct Module { std::string Source; int Reference = LUA_NOREF; bool Loading = false, Loaded = false; };
struct Domain;
struct DependencyBinding { std::string Id; Domain* Target = nullptr; };
struct Callback { uint64_t Due, Sequence; Domain* Owner; lua_State* Thread; int Reference, Arguments; std::string Gate; };
struct Later { bool operator()(const Callback& A, const Callback& B) const {
    return A.Due > B.Due || (A.Due == B.Due && A.Sequence > B.Sequence); } };
struct StagedModule { Domain* Owner; Module* Value; int Reference; };
struct PublicationScope;
struct Domain {
    uint64_t Id = 0;
    bool Alive = true, Active = false;
    uint32_t MaxQueued = 0;
    uint64_t Rejected = 0, Discarded = 0;
    size_t SourceBytes = 0;
    std::map<std::string, Module> Modules;
    std::string PackageId, PackageVersion, MainModule;
    std::vector<std::string> PublicModules;
    std::vector<DependencyBinding> Dependencies;
    std::vector<std::string> Loading;
    std::vector<Callback> Queue;
    std::vector<StagedModule> PendingModules;
    std::vector<Callback> PendingCallbacks;
    ClHostCall Host = nullptr;
    uint64_t HostIdentity = 0;
    int Game = LUA_NOREF, Dispatch = LUA_NOREF;
    std::unique_ptr<std::array<char, 262144>> HostBuffer;
};
struct AdmissionContext {
    Domain* Owner = nullptr;
    uint64_t OperationId = 0;
    bool Provisional = false;
};
struct Vm {
    uint64_t Id = 0;
    uint64_t GenerationId = 0;
    std::thread::id Owner = std::this_thread::get_id();
    uint64_t Used = 0, Limit = 0;
    bool AllocationFailed = false;
    lua_State* State = nullptr;
    lua_State* Thread = nullptr;
    int Reference = LUA_NOREF;
    uint64_t ThreadId = 0;
    Domain* ThreadDomain = nullptr;
    bool Resumable = false;
    bool Started = false;
    std::chrono::steady_clock::time_point Deadline;
    char Chunk[128]{};
    char Logs[4096]{};
    size_t LogSize = 0;
    bool LogTruncated = false;
    bool Scripts = false, Sealed = false;
    uint64_t Sequence = 0, OperationSequence = 0, RetiredDiscarded = 0;
    std::vector<std::string> ModuleLoads;
    std::vector<std::unique_ptr<Domain>> Domains;
    Domain* LegacyDomain = nullptr;
    AdmissionContext* Admission = nullptr;
    PublicationScope* Publication = nullptr;
    bool IntegrityFailed = false;
    ~Vm() {
        if (State) {
            lua_callbacks(State)->interrupt = nullptr;
            for (const auto& Item : Domains) if (Item) {
                Domain& Value = *Item;
                for (const auto& Work : Value.Queue) lua_unref(State, Work.Reference);
                for (const auto& Work : Value.PendingCallbacks) lua_unref(State, Work.Reference);
                for (const auto& Entry : Value.Modules) if (Entry.second.Loaded) lua_unref(State, Entry.second.Reference);
                for (const auto& Entry : Value.PendingModules) lua_unref(State, Entry.Reference);
                if (Value.Game != LUA_NOREF) lua_unref(State, Value.Game);
                if (Value.Dispatch != LUA_NOREF) lua_unref(State, Value.Dispatch);
            }
            Domains.clear();
            if (Reference != LUA_NOREF) lua_unref(State, Reference);
            lua_close(State);
        }
    }
};
std::recursive_mutex RegistryMutex;
std::array<std::unique_ptr<Vm>, 32> Registry;
uint64_t NextId = 1;
uint64_t NextVmGenerationId = 1;
struct PublicationScope {
    Vm& Runtime;
    PublicationScope* Parent;
    std::vector<StagedModule> Modules;
    std::vector<Callback> Callbacks;
    std::vector<Domain*> Facades;
    bool Complete = false;
    PublicationScope(Vm& Runtime) : Runtime(Runtime), Parent(Runtime.Publication) { Runtime.Publication = this; }
    ~PublicationScope();
    bool Uses(Domain* Owner) const { return std::find(Facades.begin(), Facades.end(), Owner) != Facades.end(); }
    void Commit();
};
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

Domain* GetDomain(Vm& Runtime, ClHandle Id, bool Active = false)
{
    if (!Id) return nullptr;
    for (const auto& Entry : Runtime.Domains)
        if (Entry && Entry->Id == Id && Entry->Alive && (!Active || Entry->Active)) return Entry.get();
    return nullptr;
}

void ReleaseDomain(Vm& Runtime, Domain& Value)
{
    if (!Value.Alive) return;
    Value.Alive = false; Value.Active = false;
    Value.Discarded += Value.Queue.size() + Value.PendingCallbacks.size();
    Runtime.RetiredDiscarded += Value.Queue.size() + Value.PendingCallbacks.size();
    if (Runtime.State) {
        for (const auto& Work : Value.Queue) lua_unref(Runtime.State, Work.Reference);
        for (const auto& Work : Value.PendingCallbacks) lua_unref(Runtime.State, Work.Reference);
        for (const auto& Entry : Value.Modules) if (Entry.second.Loaded) lua_unref(Runtime.State, Entry.second.Reference);
        for (const auto& Entry : Value.PendingModules) lua_unref(Runtime.State, Entry.Reference);
        if (Value.Game != LUA_NOREF) lua_unref(Runtime.State, Value.Game);
        if (Value.Dispatch != LUA_NOREF) lua_unref(Runtime.State, Value.Dispatch);
    }
    Value.Queue.clear(); Value.PendingCallbacks.clear(); Value.PendingModules.clear();
    Value.Modules.clear(); Value.PublicModules.clear(); Value.Dependencies.clear(); Value.Loading.clear();
    Value.PackageId.clear(); Value.PackageVersion.clear(); Value.MainModule.clear();
    Value.Game = LUA_NOREF; Value.Dispatch = LUA_NOREF;
    Value.Host = nullptr; Value.HostBuffer.reset();
}

Domain* AddDomain(Vm& Runtime, uint32_t MaxQueued)
{
    size_t Live = 0;
    for (const auto& Item : Runtime.Domains) if (Item && Item->Alive) ++Live;
    if (!Runtime.State || MaxQueued < 1 || MaxQueued > 4096 || Live >= 256 || Runtime.Domains.size() >= 4096 ||
        NextId == std::numeric_limits<uint64_t>::max()) return nullptr;
    auto Candidate = std::make_unique<Domain>();
    Candidate->Id = NextId++;
    Candidate->MaxQueued = MaxQueued;
    Candidate->Queue.reserve(MaxQueued);
    Candidate->PendingCallbacks.reserve(MaxQueued);
    Candidate->Loading.reserve(32);
    Domain* Result = Candidate.get();
    Runtime.Domains.push_back(std::move(Candidate));
    return Result;
}

bool ControlPublication(Vm& Runtime, Domain& Owner, uint32_t Operation)
{
    if (!Owner.Host || !Owner.HostBuffer) return true;
    uint32_t Written = 0;
    if (Owner.Host(Owner.HostIdentity, Operation, "", 0, Owner.HostBuffer->data(),
        uint32_t(Owner.HostBuffer->size()), &Written) != 0) {
        Runtime.IntegrityFailed = true;
        return false;
    }
    return true;
}

void RollbackPublication(PublicationScope& Scope)
{
    for (auto Item = Scope.Facades.rbegin(); Item != Scope.Facades.rend(); ++Item)
        ControlPublication(Scope.Runtime, **Item, 12);
    if (Scope.Runtime.State) {
        for (const auto& Item : Scope.Modules) lua_unref(Scope.Runtime.State, Item.Reference);
        for (const auto& Item : Scope.Callbacks) lua_unref(Scope.Runtime.State, Item.Reference);
    }
    Scope.Runtime.RetiredDiscarded += Scope.Callbacks.size();
    for (const auto& Item : Scope.Callbacks) if (Item.Owner && Item.Owner->Discarded != UINT64_MAX) ++Item.Owner->Discarded;
    Scope.Modules.clear(); Scope.Callbacks.clear(); Scope.Facades.clear();
}

PublicationScope::~PublicationScope()
{
    Runtime.Publication = Parent;
    if (!Complete) RollbackPublication(*this);
}

void PublicationScope::Commit()
{
    if (Complete) return;
    if (Parent) {
        for (Domain* Owner : Facades) {
            if (Parent->Uses(Owner)) {
                if (!ControlPublication(Runtime, *Owner, 11)) { RollbackPublication(*this); Complete = true; return; }
            } else Parent->Facades.push_back(Owner);
        }
        Parent->Modules.insert(Parent->Modules.end(), std::make_move_iterator(Modules.begin()), std::make_move_iterator(Modules.end()));
        Parent->Callbacks.insert(Parent->Callbacks.end(), std::make_move_iterator(Callbacks.begin()), std::make_move_iterator(Callbacks.end()));
    } else {
        for (auto Item = Facades.rbegin(); Item != Facades.rend(); ++Item)
            if (!ControlPublication(Runtime, **Item, 11)) { RollbackPublication(*this); Complete = true; return; }
        for (auto& Item : Modules) {
            if (!Item.Owner->Alive) { lua_unref(Runtime.State, Item.Reference); continue; }
            if (Item.Owner->Active) { Item.Value->Reference = Item.Reference; Item.Value->Loaded = true; }
            else Item.Owner->PendingModules.push_back(Item);
        }
        for (auto& Item : Callbacks) {
            Domain& Owner = *Item.Owner;
            if (!Owner.Alive || (!Owner.Active && Owner.Id != (Runtime.Admission ? Runtime.Admission->Owner->Id : 0))) {
                lua_unref(Runtime.State, Item.Reference); continue;
            }
            std::vector<Callback>& Queue = Owner.Active ? Owner.Queue : Owner.PendingCallbacks;
            Queue.push_back(std::move(Item)); std::push_heap(Queue.begin(), Queue.end(), Later{});
        }
    }
    Modules.clear(); Callbacks.clear(); Facades.clear(); Complete = true;
}

int FindStaged(Vm& Runtime, Domain* Owner, Module* Value)
{
    for (PublicationScope* Scope = Runtime.Publication; Scope; Scope = Scope->Parent)
        for (auto Item = Scope->Modules.rbegin(); Item != Scope->Modules.rend(); ++Item)
            if (Item->Owner == Owner && Item->Value == Value) return Item->Reference;
    for (auto Item = Owner->PendingModules.rbegin(); Item != Owner->PendingModules.rend(); ++Item)
        if (Item->Value == Value) return Item->Reference;
    return LUA_NOREF;
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
    for (const auto& Item : Runtime.Domains) if (Item) ReleaseDomain(Runtime, *Item);
    Runtime.Thread = nullptr;
    Runtime.ThreadId = 0;
    Runtime.ThreadDomain = nullptr;
    Runtime.Reference = LUA_NOREF;
    Runtime.Resumable = false;
    Runtime.Admission = nullptr;
    Runtime.Publication = nullptr;
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
    Runtime.ThreadDomain = nullptr;
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

uint32_t carbonluau_abi_version(void) { return 0x00010004; }
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
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    for (auto& Entry : Registry) if (!Entry) {
        if (NextId == std::numeric_limits<uint64_t>::max() || NextVmGenerationId == std::numeric_limits<uint64_t>::max()) return CL_INTERNAL_ERROR;
        auto Candidate = std::make_unique<Vm>();
        Candidate->Limit = Config->MemoryLimitBytes;
        Candidate->State = lua_newstate(Allocate, Candidate.get());
        if (!Candidate->State) return CL_MEMORY_LIMIT;
        lua_callbacks(Candidate->State)->userdata = Candidate.get();
        int Status = lua_cpcall(Candidate->State, Initialize, nullptr);
        if (Status != LUA_OK) return Status == LUA_ERRMEM ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
        lua_settop(Candidate->State, 0);
        Candidate->Id = NextId++;
        Candidate->GenerationId = NextVmGenerationId++;
        *OutVm = Candidate->Id;
        Entry = std::move(Candidate);
        return CL_OK;
    }
    return CL_INTERNAL_ERROR;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_destroy(ClHandle Id) try
{
    if (!Id) return CL_OK;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || Runtime->Admission) return CL_INVALID_ARGUMENT;
    for (auto& Entry : Registry) if (Entry.get() == Runtime) { Entry.reset(); return CL_OK; }
    return CL_INTERNAL_ERROR;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_info(ClHandle Id, ClVmInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    *Info = {Runtime->Used, Runtime->Limit, Runtime->State ? uint64_t(1) : uint64_t(0)};
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

static ClStatus LoadSourceLocked(Vm* Runtime, Domain* Owner, const char* Chunk, const char* Source, uint32_t Length,
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
    if (!Runtime || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    if (Runtime->Admission || (Owner && !Owner->Alive)) return CL_INVALID_ARGUMENT;
    if (!Runtime->State) { Result->Flags = 1; return CL_INTERNAL_ERROR; }
    Runtime->Sealed = true;
    std::memcpy(Runtime->Chunk, Chunk, ChunkLength + 1);
    Runtime->AllocationFailed = false;
    Runtime->IntegrityFailed = false;
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
            if (Owner) InstallDomainBindings(Runtime->Thread, *Owner);
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
        Runtime->ThreadDomain = Owner;
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

ClStatus cl_vm_load_source(ClHandle Id, const char* Chunk, const char* Source, uint32_t Length,
    ClHandle* OutThread, ClResult* Result) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    return LoadSourceLocked(Runtime, Runtime && Runtime->Scripts ? Runtime->LegacyDomain : nullptr,
        Chunk, Source, Length, OutThread, Result);
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_load_source(ClHandle Id, ClHandle DomainId, const char* Chunk, const char* Source,
    uint32_t Length, ClHandle* OutThread, ClResult* Result) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Owner) { if (OutThread) *OutThread = 0; if (Result) *Result = {}; return CL_INVALID_ARGUMENT; }
    return LoadSourceLocked(Runtime, Owner, Chunk, Source, Length, OutThread, Result);
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_scripts(ClHandle Id, uint32_t MaxQueued) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->Sealed || Runtime->Scripts || MaxQueued < 1 || MaxQueued > 4096) return CL_INVALID_ARGUMENT;
    Runtime->LegacyDomain = AddDomain(*Runtime, MaxQueued);
    if (!Runtime->LegacyDomain) return CL_INVALID_ARGUMENT;
    Runtime->LegacyDomain->Active = true;
    if (lua_cpcall(Runtime->State, InstallScripts, nullptr) != LUA_OK) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    Runtime->Scripts = true;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_module(ClHandle Id, const char* Name, const char* Source, uint32_t Length) try
{
    if (!Name || !Source || Length > 65536) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? Runtime->LegacyDomain : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || !Runtime->Scripts || !Owner || Runtime->Sealed || Owner->Modules.size() >= 256 ||
        Owner->SourceBytes + Length > 4 * MiB || !ModuleName(Name, 128)) return CL_INVALID_ARGUMENT;
    auto Added = Owner->Modules.emplace(Name, Module{std::string(Source, Length)});
    if (!Added.second) return CL_INVALID_ARGUMENT;
    Owner->SourceBytes += Length;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_module(ClHandle Id, ClHandle DomainId, const char* Name, const char* Source, uint32_t Length) try
{
    if (!Name || !Source || Length > 65536) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || !Owner || Owner->Active || Owner->Modules.size() >= 256 ||
        Owner->SourceBytes + Length > 4 * MiB || !ModuleName(Name, 128)) return CL_INVALID_ARGUMENT;
    auto Added = Owner->Modules.emplace(Name, Module{std::string(Source, Length)});
    if (!Added.second) return CL_INVALID_ARGUMENT;
    Owner->SourceBytes += Length;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_addon(ClHandle Id, ClHandle DomainId, const char* PackageId, const char* Version,
    const char* MainModule) try
{
    if (!PackageId || !Version || !MainModule) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Active ||
        !Owner->PackageId.empty() || !PackageName(PackageId) || !PackageVersion(Version)) return CL_INVALID_ARGUMENT;
    if (*MainModule) {
        if (!ModuleName(MainModule, 128) || Owner->Modules.find(MainModule) == Owner->Modules.end()) return CL_INVALID_ARGUMENT;
        Owner->MainModule = MainModule;
        Owner->PublicModules.push_back(MainModule);
    }
    Owner->PackageId = PackageId;
    Owner->PackageVersion = Version;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_public_module(ClHandle Id, ClHandle DomainId, const char* Name) try
{
    if (!Name) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Active ||
        Owner->PackageId.empty() || !ModuleName(Name, 128) || Owner->Modules.find(Name) == Owner->Modules.end() ||
        std::find(Owner->PublicModules.begin(), Owner->PublicModules.end(), Name) != Owner->PublicModules.end() ||
        Owner->PublicModules.size() >= 256) return CL_INVALID_ARGUMENT;
    Owner->PublicModules.emplace_back(Name);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_dependency(ClHandle Id, ClHandle DomainId, const char* PackageId,
    ClHandle TargetDomainId) try
{
    if (!PackageId) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    Domain* Target = TargetDomainId && Runtime ? GetDomain(*Runtime, TargetDomainId, true) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Active ||
        Owner->PackageId.empty() || !PackageName(PackageId) || (TargetDomainId && !Target) ||
        (Target && Target->PackageId != PackageId) || Owner->Dependencies.size() >= 32) return CL_INVALID_ARGUMENT;
    for (const auto& Binding : Owner->Dependencies) if (Binding.Id == PackageId) return CL_INVALID_ARGUMENT;
    Owner->Dependencies.push_back(DependencyBinding{PackageId, Target});
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_generation(ClHandle Id, ClVmGenerationInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    Info->VmGenerationId = Runtime->GenerationId;
    for (const auto& Item : Runtime->Domains) if (Item && Item->Alive) ++Info->Domains;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_create(ClHandle Id, uint32_t MaxQueued, ClHandle* OutDomain) try
{
    if (OutDomain) *OutDomain = 0;
    if (!OutDomain) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    Domain* Owner = AddDomain(*Runtime, MaxQueued);
    if (!Owner) return CL_INVALID_ARGUMENT;
    *OutDomain = Owner->Id;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_commit(ClHandle Id, ClHandle DomainId) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || !Owner || Owner->Active || Runtime->Admission || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    for (const auto& Item : Owner->PendingModules) {
        if (Item.Owner != Owner || !Item.Value || Item.Value->Loaded) { Retire(*Runtime); return CL_INTERNAL_ERROR; }
        Item.Value->Reference = Item.Reference; Item.Value->Loaded = true;
    }
    Owner->PendingModules.clear();
    Owner->Queue.insert(Owner->Queue.end(), std::make_move_iterator(Owner->PendingCallbacks.begin()),
        std::make_move_iterator(Owner->PendingCallbacks.end()));
    Owner->PendingCallbacks.clear();
    std::make_heap(Owner->Queue.begin(), Owner->Queue.end(), Later{});
    Owner->Active = true;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_destroy(ClHandle Id, ClHandle DomainId) try
{
    if (!DomainId) return CL_OK;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = nullptr;
    if (Runtime) for (const auto& Item : Runtime->Domains) if (Item && Item->Id == DomainId) { Owner = Item.get(); break; }
    if (!Runtime || !Owner || Runtime->Admission) return CL_INVALID_ARGUMENT;
    if (!Owner->Alive) return CL_OK;
    if (Runtime->ThreadDomain == Owner || (Runtime->Admission && Runtime->Admission->Owner == Owner))
        return CL_INVALID_ARGUMENT;
    ReleaseDomain(*Runtime, *Owner);
    if (Runtime->LegacyDomain == Owner) Runtime->LegacyDomain = nullptr;
    if (Runtime->State) {
        if (lua_cpcall(Runtime->State, Collect, nullptr) != LUA_OK) { Retire(*Runtime); return CL_INTERNAL_ERROR; }
        lua_settop(Runtime->State, 0);
    }
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_scheduler(ClHandle Id, ClSchedulerInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || (!Runtime->Scripts && Runtime->Domains.empty())) return CL_INVALID_ARGUMENT;
    Info->NowNs = NowNs(); Info->Sequence = Runtime->Sequence;
    Info->Discarded = Runtime->RetiredDiscarded;
    for (const auto& Item : Runtime->Domains) if (Item) {
        const Domain& Owner = *Item;
        Info->Rejected += Owner.Rejected;
        if (!Owner.Alive || !Owner.Active) continue;
        Info->Queued += Owner.Queue.size();
        if (!Owner.Queue.empty() && (!Info->NextDueNs || Owner.Queue.front().Due < Info->NextDueNs)) Info->NextDueNs = Owner.Queue.front().Due;
        for (const auto& Entry : Owner.Modules) if (Entry.second.Loaded) ++Info->Modules;
    }
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_callback(ClHandle Id, uint64_t CutoffNs, uint64_t Sequence, uint64_t BudgetNs,
    uint32_t* Ran, ClResult* Result) try
{
    if (Ran) *Ran = 0;
    if (Result) *Result = {};
    if (!Ran || !Result || BudgetNs < 1000000 || BudgetNs > 100000000) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->ThreadId || Runtime->Admission) return CL_INVALID_ARGUMENT;
    Domain* Selected = nullptr;
    for (const auto& Item : Runtime->Domains) if (Item && Item->Alive && Item->Active && !Item->Queue.empty()) {
        const Callback& Candidate = Item->Queue.front();
        if (Candidate.Due > CutoffNs || Candidate.Sequence > Sequence) continue;
        if (!Selected || Later{}(Selected->Queue.front(), Candidate)) Selected = Item.get();
    }
    if (!Selected) return CL_OK;
    std::pop_heap(Selected->Queue.begin(), Selected->Queue.end(), Later{});
    Callback Work = std::move(Selected->Queue.back()); Selected->Queue.pop_back();
    *Ran = 1;
    Runtime->Thread = Work.Thread; Runtime->Reference = Work.Reference;
    Runtime->ThreadDomain = Work.Owner;
    Runtime->LogSize = 0; Runtime->Logs[0] = 0; Runtime->LogTruncated = false;
    Runtime->AllocationFailed = false;
    Runtime->IntegrityFailed = false;
    if (!Work.Gate.empty()) {
        uint32_t Written = 0;
        Domain& Owner = *Work.Owner;
        if (!Owner.Alive || !Owner.Active || !Owner.Host || Owner.Host(Owner.HostIdentity, 9, Work.Gate.data(), uint32_t(Work.Gate.size()),
            Owner.HostBuffer->data(), uint32_t(Owner.HostBuffer->size()), &Written) != 0) {
            ++Owner.Rejected;
            ReleaseThread(*Runtime, false);
            if (!Runtime->State) Result->Flags = 1;
            return CL_OK;
        }
    }
    std::snprintf(Runtime->Chunk, sizeof(Runtime->Chunk), "scheduled-%llu", (unsigned long long)Work.Sequence);
    Runtime->Deadline = std::chrono::steady_clock::now() + std::chrono::nanoseconds(BudgetNs);
    AdmissionContext Admission{Work.Owner, ++Runtime->OperationSequence, false};
    Runtime->Admission = &Admission;
    lua_callbacks(Runtime->State)->interrupt = Interrupt;
    ClStatus Status;
    try {
        int Code = lua_resume(Work.Thread, nullptr, Work.Arguments);
        lua_callbacks(Runtime->State)->interrupt = nullptr;
        Runtime->Admission = nullptr;
        if (Runtime->AllocationFailed || Code == LUA_ERRMEM) {
            Status = CL_MEMORY_LIMIT; Diagnostic(*Runtime, *Result, "callback memory limit", Work.Thread);
        } else if (Code == LUA_OK) Status = CL_OK;
        else {
            Status = CL_RUNTIME_ERROR;
            const char* Message = Code == LUA_YIELD ? "scheduled callbacks cannot yield" :
                (lua_type(Work.Thread, -1) == LUA_TSTRING ? lua_tostring(Work.Thread, -1) : "callback non-string error");
            Diagnostic(*Runtime, *Result, Message, Work.Thread);
        }
        if (Runtime->IntegrityFailed) {
            Status = CL_INTERNAL_ERROR;
            Diagnostic(*Runtime, *Result, "publication integrity failure; VM retired");
            Retire(*Runtime);
        } else ReleaseThread(*Runtime, Status == CL_MEMORY_LIMIT);
        if (!Runtime->State) Result->Flags |= 1;
    } catch (const DeadlineExceeded&) {
        Runtime->Admission = nullptr;
        Status = CL_TIMEOUT; Diagnostic(*Runtime, *Result, "callback deadline exceeded; VM retired");
        Retire(*Runtime); Result->Flags |= 1;
    } catch (...) {
        Runtime->Admission = nullptr;
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
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetThread(Id);
    if (!Runtime || Runtime->Admission || !Runtime->Resumable) return CL_INVALID_ARGUMENT;
    Runtime->LogSize = 0;
    Runtime->Logs[0] = 0;
    Runtime->LogTruncated = false;
    Runtime->AllocationFailed = false;
    Runtime->IntegrityFailed = false;
    Runtime->Deadline = std::chrono::steady_clock::now() + std::chrono::nanoseconds(BudgetNs);
    lua_callbacks(Runtime->State)->interrupt = Interrupt;
    ClStatus Status = CL_INTERNAL_ERROR;
    AdmissionContext Admission{Runtime->ThreadDomain, ++Runtime->OperationSequence,
        Runtime->ThreadDomain && !Runtime->ThreadDomain->Active};
    Runtime->Admission = &Admission;
    try {
        std::unique_ptr<PublicationScope> Publication;
        if (Admission.Provisional) Publication = std::make_unique<PublicationScope>(*Runtime);
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
            if (Code == LUA_OK && Publication) Publication->Commit();
            if (lua_gettop(Runtime->Thread) && lua_type(Runtime->Thread, 1) == LUA_TNUMBER) {
                Result->Number = lua_tonumber(Runtime->Thread, 1); Result->HasNumber = 1;
            }
        } else {
            Status = CL_RUNTIME_ERROR;
            const char* Message = lua_type(Runtime->Thread, -1) == LUA_TSTRING ? lua_tostring(Runtime->Thread, -1) : "non-string runtime error";
            Diagnostic(*Runtime, *Result, Message, Runtime->Thread);
        }
        if (Runtime->IntegrityFailed) {
            Status = CL_INTERNAL_ERROR;
            Diagnostic(*Runtime, *Result, "publication integrity failure; VM retired");
            Retire(*Runtime); Result->Flags |= 1;
        }
        Runtime->Admission = nullptr;
    } catch (const DeadlineExceeded&) {
        Runtime->Admission = nullptr;
        Status = CL_TIMEOUT;
        Diagnostic(*Runtime, *Result, "monotonic execution deadline exceeded; VM retired");
        Retire(*Runtime);
        Result->Flags |= 1;
    } catch (...) {
        Runtime->Admission = nullptr;
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
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetThread(Id);
    if (!Runtime || Runtime->Admission) return CL_INVALID_ARGUMENT;
    ReleaseThread(*Runtime);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }
