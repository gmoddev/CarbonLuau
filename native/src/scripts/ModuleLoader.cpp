#include "../runtime/RuntimeInternal.hpp"
#include "Compiler.hpp"

namespace CarbonLuau::Runtime {
uint64_t NowNs()
{
    return uint64_t(std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count());
}
bool ModuleName(const char* Name, size_t Capacity)
{
    size_t Length = 0, Segment = 0;
    while (Length < Capacity && Name[Length]) {
        char C = Name[Length++];
        if (C == '/') { if (!Segment) return false; Segment = 0; }
        else if ((C >= 'a' && C <= 'z') || (C >= '0' && C <= '9') || C == '_' || C == '-') ++Segment;
        else return false;
    }
    return Length > 0 && Length < Capacity && Segment > 0;
}
Domain& BoundDomain(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain* Value = static_cast<Domain*>(lua_touserdata(State, lua_upvalueindex(1)));
    if (!Value || !GetDomain(Runtime, Value->Id)) luaL_error(State, "stale domain lifetime");
    return *Value;
}
bool PackageName(const char* Name)
{
    size_t Length = 0, Segment = 0, Segments = 1;
    while (Length < 66 && Name[Length]) {
        char C = Name[Length++];
        if (C == '.') {
            if (!Segment || Segments == 2) return false;
            Segment = 0; ++Segments;
        } else if ((C >= 'a' && C <= 'z') || (C >= '0' && C <= '9') || C == '_' || C == '-') {
            if (!Segment && (C == '_' || C == '-')) return false;
            if (++Segment > 32) return false;
        } else return false;
    }
    if (!Length || Length > 65 || !Segment || Name[Length] || Name[Length - 1] == '_' || Name[Length - 1] == '-') return false;
    return std::strcmp(Name, "carbonluau") != 0 && std::strncmp(Name, "carbonluau.", 11) != 0;
}
bool PackageVersion(const char* Version)
{
    size_t Length = 0;
    while (Length < 33 && Version[Length]) ++Length;
    if (!Length || Length > 32 || Version[Length]) return false;
    const char* Cursor = Version;
    for (int Part = 0; Part < 3; ++Part) {
        const char* Start = Cursor; uint64_t Value = 0;
        while (*Cursor && *Cursor != '.') {
            if (*Cursor < '0' || *Cursor > '9' || Cursor - Start >= 10) return false;
            Value = Value * 10 + uint64_t(*Cursor++ - '0');
            if (Value > UINT32_MAX) return false;
        }
        if (Cursor == Start || (Cursor - Start > 1 && *Start == '0')) return false;
        if (Part < 2) { if (*Cursor != '.') return false; ++Cursor; }
        else if (*Cursor) return false;
    }
    return true;
}
void InstallDomainBindings(lua_State* State, Domain& Owner);
int RequireModule(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain& Consumer = BoundDomain(State);
    if (lua_type(State, 1) != LUA_TSTRING) luaL_error(State, "module INVALID_ARGUMENT: expected logical name");
    size_t Length = 0;
    const char* Name = lua_tolstring(State, 1, &Length);
    if (Length >= 196 || std::strlen(Name) != Length)
        luaL_error(State, "module INVALID_ARGUMENT: expected a bounded canonical logical name");
    Domain* Owner = &Consumer;
    std::string LogicalName;
    if (Length && Name[0] == '@') {
        const char* Slash = std::strchr(Name + 1, '/');
        std::string Package(Name + 1, Slash ? size_t(Slash - Name - 1) : Length - 1);
        if (!PackageName(Package.c_str())) luaL_error(State, "package %s: INVALID_ARGUMENT", Name);
        DependencyBinding* Binding = nullptr;
        for (auto& Candidate : Consumer.Dependencies) if (Candidate.Id == Package) { Binding = &Candidate; break; }
        if (!Binding) luaL_error(State, "package @%s: UNDECLARED_DEPENDENCY", Package.c_str());
        if (!Binding->Target || !GetDomain(Runtime, Binding->Target->Id, true))
            luaL_error(State, "package @%s: DEPENDENCY_UNAVAILABLE", Package.c_str());
        Owner = Binding->Target;
        if (!Slash) {
            if (Owner->MainModule.empty()) luaL_error(State, "package @%s: MAIN_NOT_DECLARED", Package.c_str());
            LogicalName = Owner->MainModule;
        } else {
            LogicalName.assign(Slash + 1);
            if (!ModuleName(LogicalName.c_str(), 128))
                luaL_error(State, "package %s: INVALID_ARGUMENT", Name);
            if (std::find(Owner->PublicModules.begin(), Owner->PublicModules.end(), LogicalName) == Owner->PublicModules.end())
                luaL_error(State, "package %s: MODULE_NOT_PUBLIC", Name);
        }
    } else {
        if (Length >= 128 || !ModuleName(Name, Length + 1))
            luaL_error(State, "module INVALID_ARGUMENT: use lowercase segments joined by single '/'; no extension or traversal");
        LogicalName.assign(Name, Length);
    }
    auto Found = Owner->Modules.find(LogicalName);
    if (Found == Owner->Modules.end()) luaL_error(State, "module %s: NOT_FOUND", Name);
    Module& Value = Found->second;
    if (Value.Loaded) { lua_getref(State, Value.Reference); return 1; }
    int Staged = FindStaged(Runtime, Owner, &Value);
    if (Staged != LUA_NOREF) { lua_getref(State, Staged); return 1; }
    if (Value.Loading) {
        char Chain[768]{};
        for (const auto& Item : Runtime.ModuleLoads) {
            size_t Used = std::strlen(Chain);
            std::snprintf(Chain + Used, sizeof(Chain) - Used, "%s -> ", Item.c_str());
        }
        luaL_error(State, "module %s: CYCLE: %s%s", Name, Chain, Name);
    }
    if (Runtime.ModuleLoads.size() >= 32) luaL_error(State, "module %s: dependency depth exceeds 32", Name);
    Runtime.ModuleLoads.emplace_back(Name, Length);
    Owner->Loading.emplace_back(LogicalName);
    Value.Loading = true;
    struct LoadingScope {
        Vm& Runtime; Domain& Owner; Module& Value; int ThreadReference = LUA_NOREF;
        ~LoadingScope() {
            Value.Loading = false; Owner.Loading.pop_back(); Runtime.ModuleLoads.pop_back();
            if (ThreadReference != LUA_NOREF && Runtime.State) lua_unref(Runtime.State, ThreadReference);
        }
    } Loading{Runtime, *Owner, Value};
    PublicationScope Publication(Runtime);
    CompileResult Compilation;
    { RegistryWaitScope Wait; Compilation = CompileSource(Value.Source); }
    if (Compilation.Status != CompileStatus::Success)
        luaL_error(State, "module %s: COMPILE_ERROR: %.1024s", Name, Compilation.Diagnostic.c_str());
    std::string& Bytecode = Compilation.Payload;
    if (Bytecode.empty() || Bytecode.size() > 1024 * 1024) luaL_error(State, "module %s: compiled size exceeds bound", Name);
    if (Bytecode[0] == 0) luaL_error(State, "module %s: COMPILE_ERROR: %.1024s", Name, Bytecode.c_str() + 1);
    lua_State* Thread = lua_newthread(Runtime.State);
    Loading.ThreadReference = lua_ref(Runtime.State, -1);
    lua_pop(Runtime.State, 1);
    luaL_sandboxthread(Thread);
    InstallDomainBindings(Thread, *Owner);
    std::string Chunk = Owner->PackageId.empty() ? "modules/" + Found->first + ".luau" :
        "addons/" + Owner->PackageId + "/modules/" + Found->first + ".luau";
    int Code = luau_load(Thread, Chunk.c_str(), Bytecode.data(), Bytecode.size(), 0);
    if (Code == LUA_OK) Code = lua_resume(Thread, State, 0);
    if (Runtime.AllocationFailed || Code == LUA_ERRMEM) luaL_error(State, "module %s: MEMORY_LIMIT", Name);
    if (Code != LUA_OK) {
        const char* Error = Code == LUA_YIELD ? "module must not yield" :
            (lua_type(Thread, -1) == LUA_TSTRING ? lua_tostring(Thread, -1) : "non-string error");
        luaL_error(State, "module %s: RUNTIME_ERROR: %.768s\n%.768s", Name, Error, lua_debugtrace(Thread));
    }
    // Cache exactly one result. Nil/no return becomes true, never confused with unloaded.
    if (lua_gettop(Thread) == 0 || lua_isnil(Thread, 1)) lua_pushboolean(State, true);
    else { lua_pushvalue(Thread, 1); lua_xmove(Thread, State, 1); }
    int Reference = lua_ref(State, -1);
    Publication.Modules.push_back(StagedModule{Owner, &Value, Reference});
    lua_getref(State, Reference);
    Publication.Commit();
    if (Runtime.IntegrityFailed) luaL_error(State, "module publication integrity failure");
    return 1;
}
int IsDependencyAvailable(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain& Owner = BoundDomain(State);
    if (lua_gettop(State) != 2 || lua_type(State, 1) != LUA_TTABLE || lua_type(State, 2) != LUA_TSTRING)
        luaL_error(State, "addon:IsDependencyAvailable expects a dependency id");
    size_t Length = 0; const char* Id = lua_tolstring(State, 2, &Length);
    if (Length > 65 || std::strlen(Id) != Length || !PackageName(Id))
        luaL_error(State, "addon:IsDependencyAvailable expects a canonical dependency id");
    bool Available = false;
    for (const auto& Binding : Owner.Dependencies) if (Binding.Id == Id) {
        Available = Binding.Target && GetDomain(Runtime, Binding.Target->Id, true); break;
    }
    lua_pushboolean(State, Available);
    return 1;
}
int Schedule(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain& Owner = BoundDomain(State);
    bool Delayed = lua_toboolean(State, lua_upvalueindex(2)) != 0;
    int Function = Delayed ? 2 : 1;
    double Delay = 0;
    if (Delayed) {
        if (lua_type(State, 1) != LUA_TNUMBER) luaL_error(State, "task.delay: expected seconds");
        Delay = lua_tonumber(State, 1);
        if (!std::isfinite(Delay) || Delay < 0 || Delay > 86400) luaL_error(State, "task.delay: seconds must be finite, 0..86400");
    }
    if (!lua_isfunction(State, Function)) luaL_error(State, "task: expected function");
    int Arguments = lua_gettop(State) - Function;
    if (Arguments > 16) luaL_error(State, "task: at most 16 primitive arguments");
    for (int Index = Function + 1; Index <= lua_gettop(State); ++Index) {
        int Type = lua_type(State, Index);
        if (Type != LUA_TNIL && Type != LUA_TBOOLEAN && Type != LUA_TNUMBER && Type != LUA_TSTRING)
            luaL_error(State, "task: arguments must be nil, boolean, number or string");
        size_t Length = 0;
        if (Type == LUA_TSTRING) { lua_tolstring(State, Index, &Length); if (Length > 4096) luaL_error(State, "task: argument string exceeds 4096 bytes"); }
    }
    size_t Queued = Owner.Queue.size() + Owner.PendingCallbacks.size();
    for (PublicationScope* Scope = Runtime.Publication; Scope; Scope = Scope->Parent)
        for (const auto& Item : Scope->Callbacks) if (Item.Owner == &Owner) ++Queued;
    if (Queued >= Owner.MaxQueued) {
        if (Owner.Rejected != UINT64_MAX) ++Owner.Rejected;
        luaL_error(State, "task: queue capacity exceeded");
    }
    if (Runtime.Sequence == UINT64_MAX) luaL_error(State, "task: sequence exhausted");
    CallbackScope Scope{State};
    lua_State* Thread = lua_newthread(State);
    Scope.Reference = lua_ref(State, -1);
    // Function closures retain their original sandbox environment. No new globals.
    luaL_checkstack(Thread, Arguments + 1, "callback argument stack");
    for (int Index = Function; Index <= Function + Arguments; ++Index) lua_pushvalue(State, Index);
    lua_xmove(State, Thread, Arguments + 1);
    uint64_t Now = NowNs(), Offset = uint64_t(std::ceil(Delay * 1000000000.0));
    if (Offset > UINT64_MAX - Now) luaL_error(State, "task: delay overflow");
    Callback Work{Now + Offset, ++Runtime.Sequence, &Owner, Thread, Scope.Reference, Arguments};
    Scope.Reference = LUA_NOREF;
    if (Runtime.Publication) Runtime.Publication->Callbacks.push_back(std::move(Work));
    else {
        if (!Owner.Active) luaL_error(State, "task: domain is provisional without publication context");
        Owner.Queue.push_back(std::move(Work));
        std::push_heap(Owner.Queue.begin(), Owner.Queue.end(), Later{});
    }
    return 0;
}
void InstallDomainBindings(lua_State* State, Domain& Owner)
{
    lua_setreadonly(State, LUA_GLOBALSINDEX, false);
    lua_pushlightuserdata(State, &Owner); lua_pushcclosure(State, RequireModule, "require", 1); lua_setglobal(State, "require");
    lua_newtable(State);
    for (const char* Name : {"spawn", "defer", "delay"}) {
        lua_pushlightuserdata(State, &Owner);
        lua_pushboolean(State, std::strcmp(Name, "delay") == 0);
        lua_pushcclosure(State, Schedule, Name, 2); lua_setfield(State, -2, Name);
    }
    lua_setreadonly(State, -1, true);
    lua_setglobal(State, "task");
    if (Owner.Game != LUA_NOREF) { lua_getref(State, Owner.Game); lua_setglobal(State, "game"); }
    if (Owner.GuiBindings != LUA_NOREF) {
        lua_getref(State, Owner.GuiBindings);
        for (const char* Name : {"UDim", "UDim2", "Vector2", "Vector3", "Color3", "ImageSource", "GuiFont", "GiveItemBehavior"}) { lua_getfield(State, -1, Name); lua_setglobal(State, Name); }
        lua_pop(State, 1);
    }
    if (!Owner.PackageId.empty()) {
        lua_newtable(State);
        lua_pushlstring(State, Owner.PackageId.data(), Owner.PackageId.size()); lua_setfield(State, -2, "Id");
        lua_pushlstring(State, Owner.PackageVersion.data(), Owner.PackageVersion.size()); lua_setfield(State, -2, "Version");
        lua_pushlightuserdata(State, &Owner);
        lua_pushcclosure(State, IsDependencyAvailable, "IsDependencyAvailable", 1);
        lua_setfield(State, -2, "IsDependencyAvailable");
        lua_setreadonly(State, -1, true);
        lua_setglobal(State, "addon");
    }
}
int InstallScripts(lua_State*) { return 0; }

} // namespace CarbonLuau::Runtime
