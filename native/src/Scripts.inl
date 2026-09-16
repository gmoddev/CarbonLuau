// Included inside Runtime.cpp's private namespace; no Luau symbols cross the ABI.
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
void InstallDomainBindings(lua_State* State, Domain& Owner);
int RequireModule(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain& Owner = BoundDomain(State);
    if (lua_type(State, 1) != LUA_TSTRING) luaL_error(State, "module INVALID_ARGUMENT: expected logical name");
    size_t Length = 0;
    const char* Name = lua_tolstring(State, 1, &Length);
    if (Length >= 128 || std::strlen(Name) != Length || !ModuleName(Name, Length + 1))
        luaL_error(State, "module INVALID_ARGUMENT: use lowercase segments joined by single '/'; no extension or traversal");
    auto Found = Owner.Modules.find(Name);
    if (Found == Owner.Modules.end()) luaL_error(State, "module %s: NOT_FOUND", Name);
    Module& Value = Found->second;
    if (Value.Loaded) { lua_getref(State, Value.Reference); return 1; }
    int Staged = FindStaged(Runtime, &Owner, &Value);
    if (Staged != LUA_NOREF) { lua_getref(State, Staged); return 1; }
    if (Value.Loading) {
        char Chain[768]{};
        for (const auto& Item : Owner.Loading) {
            size_t Used = std::strlen(Chain);
            std::snprintf(Chain + Used, sizeof(Chain) - Used, "%s -> ", Item.c_str());
        }
        luaL_error(State, "module %s: CYCLE: %s%s", Name, Chain, Name);
    }
    if (Owner.Loading.size() >= 32) luaL_error(State, "module %s: dependency depth exceeds 32", Name);
    Owner.Loading.emplace_back(Name);
    Value.Loading = true;
    struct LoadingScope {
        Vm& Runtime; Domain& Owner; Module& Value; int ThreadReference = LUA_NOREF;
        ~LoadingScope() {
            Value.Loading = false; Owner.Loading.pop_back();
            if (ThreadReference != LUA_NOREF && Runtime.State) lua_unref(Runtime.State, ThreadReference);
        }
    } Loading{Runtime, Owner, Value};
    PublicationScope Publication(Runtime);
    Luau::CompileOptions Options; Options.optimizationLevel = 1; Options.debugLevel = 1;
    std::string Bytecode = Luau::compile(Value.Source, Options);
    if (Bytecode.empty() || Bytecode.size() > 1024 * 1024) luaL_error(State, "module %s: compiled size exceeds bound", Name);
    if (Bytecode[0] == 0) luaL_error(State, "module %s: COMPILE_ERROR: %.1024s", Name, Bytecode.c_str() + 1);
    lua_State* Thread = lua_newthread(Runtime.State);
    Loading.ThreadReference = lua_ref(Runtime.State, -1);
    lua_pop(Runtime.State, 1);
    luaL_sandboxthread(Thread);
    InstallDomainBindings(Thread, Owner);
    std::string Chunk = "modules/" + Found->first + ".luau";
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
    Publication.Modules.push_back(StagedModule{&Owner, &Value, Reference});
    lua_getref(State, Reference);
    Publication.Commit();
    if (Runtime.IntegrityFailed) luaL_error(State, "module publication integrity failure");
    return 1;
}
struct CallbackScope {
    lua_State* State; int Reference = LUA_NOREF;
    ~CallbackScope() { if (Reference != LUA_NOREF) lua_unref(State, Reference); }
};
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
}
int InstallScripts(lua_State*) { return 0; }
