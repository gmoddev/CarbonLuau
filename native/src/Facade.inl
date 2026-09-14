// Private native half of the build-embedded Luau facade.
void PushFields(lua_State* State, const char* Bytes, size_t Length)
{
    lua_newtable(State);
    size_t Start = 0; int Index = 1;
    for (size_t End = 0; End < Length; ++End) if (!Bytes[End]) {
        if (Index > 3072) luaL_error(State, "host field count exceeds limit");
        lua_pushlstring(State, Bytes + Start, End - Start);
        lua_rawseti(State, -2, Index++); Start = End + 1;
    }
    if (Start != Length) luaL_error(State, "invalid host response");
}
int HostPrimitive(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    if (!Runtime.Host) luaL_error(State, "host unavailable");
    if (lua_type(State, 1) != LUA_TNUMBER) luaL_error(State, "invalid host operation");
    double Value = lua_tonumber(State, 1);
    if (Value < 1 || Value > 8 || Value != std::floor(Value)) luaL_error(State, "invalid host operation");
    if (lua_gettop(State) > 4) luaL_error(State, "host argument count exceeds limit");
    char Request[16384]; size_t Used = 0;
    for (int Index = 2; Index <= lua_gettop(State); ++Index) {
        if (lua_type(State, Index) != LUA_TSTRING) luaL_error(State, "expected host string");
        size_t Length = 0; const char* Text = lua_tolstring(State, Index, &Length);
        if (Length > 4096 || Used + Length + 1 > sizeof(Request) || std::memchr(Text, 0, Length)) luaL_error(State, "host input exceeds limit or contains NUL");
        std::memcpy(Request + Used, Text, Length); Used += Length; Request[Used++] = 0;
    }
    uint32_t Written = 0;
    uint32_t Status = Runtime.Host(Runtime.Generation, uint32_t(Value), Request, uint32_t(Used),
        Runtime.HostBuffer->data(), uint32_t(Runtime.HostBuffer->size()), &Written);
    if (Written > Runtime.HostBuffer->size()) luaL_error(State, "invalid host response length");
    if (Status) {
        char Error[256]{};
        std::memcpy(Error, Runtime.HostBuffer->data(), std::min(size_t(Written), sizeof(Error) - 1));
        luaL_error(State, "%s", Written ? Error : "host operation rejected");
    }
    PushFields(State, Runtime.HostBuffer->data(), Written);
    return 1;
}
int InstallFacade(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Luau::CompileOptions Options; Options.optimizationLevel = 1; Options.debugLevel = 1;
    std::string Bytecode = Luau::compile(BootstrapSource, Options);
    if (luau_load(State, "carbonluau.facade", Bytecode.data(), Bytecode.size(), 0) != LUA_OK) lua_error(State);
    lua_pushcfunction(State, HostPrimitive, "host");
    lua_call(State, 1, 2);
    Runtime.Dispatch = lua_ref(State, -1); lua_pop(State, 1);
    lua_setreadonly(State, LUA_GLOBALSINDEX, false);
    lua_setglobal(State, "game");
    luaL_sandbox(State);
    return 0;
}
struct EventInput { Vm* Runtime; const char* Bytes; uint32_t Length; };
int EnqueueEvent(lua_State* State)
{
    auto& Input = *static_cast<EventInput*>(lua_touserdata(State, 1));
    Vm& Runtime = *Input.Runtime;
    CallbackScope Scope{State};
    lua_State* Thread = lua_newthread(State); Scope.Reference = lua_ref(State, -1);
    lua_getref(Thread, Runtime.Dispatch);
    PushFields(Thread, Input.Bytes, Input.Length);
    Runtime.Queue.push_back(Callback{NowNs(), ++Runtime.Sequence, Thread, Scope.Reference, 1, std::string(Input.Bytes, Input.Length)});
    Scope.Reference = LUA_NOREF;
    std::push_heap(Runtime.Queue.begin(), Runtime.Queue.end(), Later{});
    return 0;
}
} // exports have C linkage from the public header
ClStatus cl_vm_facade(ClHandle Id, uint64_t Generation, ClHostCall Host) try
{
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || !Runtime->Scripts || Runtime->Sealed || Runtime->Host || !Generation || !Host) return CL_INVALID_ARGUMENT;
    Runtime->HostBuffer = std::make_unique<std::array<char, 262144>>();
    Runtime->Host = Host; Runtime->Generation = Generation;
    // Bounded managed interop preparation is part of host setup, not user script
    // execution. No outbound operations or host registrations are permitted here.
    uint32_t Written = 0;
    if (Host(Generation, 0, "", 0, Runtime->HostBuffer->data(), uint32_t(Runtime->HostBuffer->size()), &Written) != 0) {
        Retire(*Runtime); return CL_INTERNAL_ERROR;
    }
    if (lua_cpcall(Runtime->State, InstallFacade, nullptr) != LUA_OK) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }
ClStatus cl_vm_event(ClHandle Id, const char* Payload, uint32_t Length) try
{
    if (!Payload || !Length || Length > 16384 || Payload[Length - 1]) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || !Runtime->Host || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    if (Runtime->Queue.size() >= Runtime->MaxQueued || Runtime->Sequence == UINT64_MAX) { ++Runtime->Rejected; return CL_INVALID_ARGUMENT; }
    EventInput Input{Runtime, Payload, Length};
    if (lua_cpcall(Runtime->State, EnqueueEvent, &Input) != LUA_OK) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }
namespace {
