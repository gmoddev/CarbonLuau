#include "RuntimeInternal.hpp"

namespace CarbonLuau::Runtime {
Vm::~Vm()
{
    if (State) {
        lua_callbacks(State)->interrupt = nullptr;
        for (const auto& Item : Domains) if (Item) {
            Domain& Value = *Item;
            ClearStorage(*this, Value);
            for (const auto& Work : Value.Queue) lua_unref(State, Work.Reference);
            for (const auto& Work : Value.PendingCallbacks) lua_unref(State, Work.Reference);
            for (const auto& Entry : Value.Modules) if (Entry.second.Loaded) lua_unref(State, Entry.second.Reference);
            for (const auto& Entry : Value.PendingModules) lua_unref(State, Entry.Reference);
            if (Value.Game != LUA_NOREF) lua_unref(State, Value.Game);
            if (Value.Dispatch != LUA_NOREF) lua_unref(State, Value.Dispatch);
            if (Value.GuiBindings != LUA_NOREF) lua_unref(State, Value.GuiBindings);
        }
        Domains.clear();
        if (GuiValueEqual != LUA_NOREF) lua_unref(State, GuiValueEqual);
        if (Reference != LUA_NOREF) lua_unref(State, Reference);
        lua_close(State);
    }
}

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

Domain* GetDomain(Vm& Runtime, ClHandle Id, bool Active)
{
    if (!Id) return nullptr;
    for (const auto& Entry : Runtime.Domains)
        if (Entry && Entry->Id == Id && Entry->Alive && (!Active || Entry->Active)) return Entry.get();
    return nullptr;
}

void ReleaseDomain(Vm& Runtime, Domain& Value)
{
    if (!Value.Alive) return;
    ClearStorage(Runtime, Value);
    Value.Alive = false; Value.Active = false;
    for (auto& Reservation : Value.StorageReservations) {
        if (Reservation) { --Runtime.StorageReserved; Reservation=0; }
    }
    Value.Discarded += Value.Queue.size() + Value.PendingCallbacks.size();
    Runtime.RetiredDiscarded += Value.Queue.size() + Value.PendingCallbacks.size();
    if (Runtime.State) {
        for (const auto& Work : Value.Queue) lua_unref(Runtime.State, Work.Reference);
        for (const auto& Work : Value.PendingCallbacks) lua_unref(Runtime.State, Work.Reference);
        for (const auto& Entry : Value.Modules) if (Entry.second.Loaded) lua_unref(Runtime.State, Entry.second.Reference);
        for (const auto& Entry : Value.PendingModules) lua_unref(Runtime.State, Entry.Reference);
        if (Value.Game != LUA_NOREF) lua_unref(Runtime.State, Value.Game);
        if (Value.Dispatch != LUA_NOREF) lua_unref(Runtime.State, Value.Dispatch);
        if (Value.GuiBindings != LUA_NOREF) lua_unref(Runtime.State, Value.GuiBindings);
    }
    Value.Queue.clear(); Value.PendingCallbacks.clear(); Value.PendingModules.clear();
    Value.Modules.clear(); Value.PublicModules.clear(); Value.Dependencies.clear(); Value.Loading.clear();
    Value.PackageId.clear(); Value.PackageVersion.clear(); Value.MainModule.clear();
    Value.Game = LUA_NOREF; Value.Dispatch = LUA_NOREF; Value.GuiBindings = LUA_NOREF;
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
    Candidate->StorageNames.reserve(64);
    Domain* Result = Candidate.get();
    Runtime.Domains.push_back(std::move(Candidate));
    return Result;
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
#ifdef CARBONLUAU_PREVIEW
    // Preview has no ambient random seed. Explicit script randomseed remains Luau behavior.
    lua_getglobal(State, "math"); lua_getfield(State, -1, "randomseed");
    lua_pushinteger(State, 0); lua_call(State, 1, 0); lua_pop(State, 1);
#endif
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

void Diagnostic(Vm& Runtime, ClResult& Result, const char* Message, lua_State* Trace)
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
void ReleaseThread(Vm& Runtime, bool Full)
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
} // namespace CarbonLuau::Runtime
