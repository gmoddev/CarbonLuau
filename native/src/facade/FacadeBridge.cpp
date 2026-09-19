#include "../runtime/RuntimeInternal.hpp"
#include "../scripts/Compiler.hpp"
#include "Bootstrap.h"

namespace CarbonLuau::Runtime {
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
    Domain& Owner = BoundDomain(State);
    if (!Owner.Host || !Owner.HostBuffer) luaL_error(State, "host unavailable for domain");
    if (lua_type(State, 1) != LUA_TNUMBER) luaL_error(State, "invalid host operation");
    double Value = lua_tonumber(State, 1);
    if (Value < 1 || Value > 8 || Value != std::floor(Value)) luaL_error(State, "invalid host operation");
    uint32_t Operation = uint32_t(Value);
    if (Runtime.Admission && Runtime.Admission->Provisional && Operation == 4)
        luaL_error(State, "SendMessage requires a committed domain; use task.defer for startup delivery");
    if (Runtime.Publication && (Operation == 6 || Operation == 7 || Operation == 8) && !Runtime.Publication->Uses(&Owner)) {
        if (!ControlPublication(Runtime, Owner, 10)) luaL_error(State, "host publication setup failed");
        Runtime.Publication->Facades.push_back(&Owner);
    }
    if (lua_gettop(State) > 4) luaL_error(State, "host argument count exceeds limit");
    char Request[16384]; size_t Used = 0;
    for (int Index = 2; Index <= lua_gettop(State); ++Index) {
        if (lua_type(State, Index) != LUA_TSTRING) luaL_error(State, "expected host string");
        size_t Length = 0; const char* Text = lua_tolstring(State, Index, &Length);
        if (Length > 4096 || Used + Length + 1 > sizeof(Request) || std::memchr(Text, 0, Length))
            luaL_error(State, "host input exceeds limit or contains NUL");
        std::memcpy(Request + Used, Text, Length); Used += Length; Request[Used++] = 0;
    }
    uint32_t Written = 0;
    uint32_t Status = Owner.Host(Owner.HostIdentity, Operation, Request, uint32_t(Used), Owner.HostBuffer->data(),
        uint32_t(Owner.HostBuffer->size()), &Written);
    if (Written > Owner.HostBuffer->size()) luaL_error(State, "invalid host response length");
    if (Status) {
        char Error[256]{};
        std::memcpy(Error, Owner.HostBuffer->data(), std::min(size_t(Written), sizeof(Error) - 1));
        luaL_error(State, "%s", Written ? Error : "host operation rejected");
    }
    PushFields(State, Owner.HostBuffer->data(), Written);
    return 1;
}

struct FacadeInput { Domain* Owner; const std::string* Bytecode; };
int InstallFacade(lua_State* State)
{
    auto& Input = *static_cast<FacadeInput*>(lua_touserdata(State, 1));
    Domain& Owner = *Input.Owner;
    if (luau_load(State, "carbonluau.facade", Input.Bytecode->data(), Input.Bytecode->size(), 0) != LUA_OK) lua_error(State);
    lua_pushlightuserdata(State, &Owner);
    lua_pushcclosure(State, HostPrimitive, "host", 1);
    lua_call(State, 1, 2);
    Owner.Dispatch = lua_ref(State, -1); lua_pop(State, 1);
    Owner.Game = lua_ref(State, -1); lua_pop(State, 1);
    return 0;
}

struct EventInput { Vm* Runtime; Domain* Owner; const char* Bytes; uint32_t Length; };
int EnqueueEvent(lua_State* State)
{
    auto& Input = *static_cast<EventInput*>(lua_touserdata(State, 1));
    Vm& Runtime = *Input.Runtime; Domain& Owner = *Input.Owner;
    CallbackScope Scope{State};
    lua_State* Thread = lua_newthread(State); Scope.Reference = lua_ref(State, -1);
    lua_getref(Thread, Owner.Dispatch);
    PushFields(Thread, Input.Bytes, Input.Length);
    Owner.Queue.push_back(Callback{NowNs(), ++Runtime.Sequence, &Owner, Thread, Scope.Reference, 1,
        std::string(Input.Bytes, Input.Length)});
    Scope.Reference = LUA_NOREF;
    std::push_heap(Owner.Queue.begin(), Owner.Queue.end(), Later{});
    return 0;
}
} // namespace CarbonLuau::Runtime

using namespace CarbonLuau::Runtime;

ClStatus cl_domain_facade(ClHandle Id, ClHandle DomainId, ClHostCall Host) try
{
    std::unique_lock<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Host || !Host)
        return CL_INVALID_ARGUMENT;
    CompileResult Compilation;
    { RegistryWaitScope Wait; Compilation = CompileSource(BootstrapSource); }
    if (Compilation.Status != CompileStatus::Success || Compilation.Payload.empty() || Compilation.Payload[0] == 0)
        return Compilation.Status == CompileStatus::Timeout ? CL_TIMEOUT : CL_INTERNAL_ERROR;
    Owner->HostBuffer = std::make_unique<std::array<char, 262144>>();
    Owner->Host = Host;
    if (!Owner->HostIdentity) Owner->HostIdentity = Owner->Id;
    uint32_t Written = 0;
    if (Host(Owner->HostIdentity, 0, "", 0, Owner->HostBuffer->data(), uint32_t(Owner->HostBuffer->size()), &Written) != 0) {
        ReleaseDomain(*Runtime, *Owner); return CL_INTERNAL_ERROR;
    }
    FacadeInput Input{Owner, &Compilation.Payload};
    if (lua_cpcall(Runtime->State, InstallFacade, &Input) != LUA_OK) { ReleaseDomain(*Runtime, *Owner); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_facade(ClHandle Id, uint64_t Generation, ClHostCall Host) try
{
    std::unique_lock<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    ClHandle DomainId = Runtime && Runtime->LegacyDomain ? Runtime->LegacyDomain->Id : 0;
    if (!DomainId || Runtime->Admission || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    Runtime->LegacyDomain->HostIdentity = Generation;
    Lock.unlock();
    return cl_domain_facade(Id, DomainId, Host);
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_event(ClHandle Id, ClHandle DomainId, const char* Payload, uint32_t Length) try
{
    if (!Payload || !Length || Length > 16384 || Payload[Length - 1]) return CL_INVALID_ARGUMENT;
    std::unique_lock<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId, true) : nullptr;
    if (!Runtime || !Runtime->State || !Owner || !Owner->Host || Runtime->ThreadId || Runtime->Admission) return CL_INVALID_ARGUMENT;
    if (Owner->Queue.size() >= Owner->MaxQueued || Runtime->Sequence == UINT64_MAX) { ++Owner->Rejected; return CL_INVALID_ARGUMENT; }
    EventInput Input{Runtime, Owner, Payload, Length};
    if (lua_cpcall(Runtime->State, EnqueueEvent, &Input) != LUA_OK) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_event(ClHandle Id, const char* Payload, uint32_t Length) try
{
    std::unique_lock<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    ClHandle DomainId = Runtime && Runtime->LegacyDomain ? Runtime->LegacyDomain->Id : 0;
    if (!DomainId) return CL_INVALID_ARGUMENT;
    Lock.unlock();
    return cl_domain_event(Id, DomainId, Payload, Length);
} catch (...) { return CL_INTERNAL_ERROR; }
