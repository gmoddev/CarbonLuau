#include "../runtime/RuntimeInternal.hpp"
#include "../scripts/Compiler.hpp"
#include "Bootstrap.h"
#include <charconv>
#include <new>
#include <string_view>

namespace CarbonLuau::Runtime {
#ifdef CARBONLUAU_TESTING
bool TestGameplayCopyFailure = false;
#endif
namespace {
bool GameplayIdentity(std::string_view Text, uint64_t& Value)
{
    if (Text.empty() || Text.size() > 20 || Text.front() == '0') return false;
    Value = 0;
    for (unsigned char Character : Text) {
        unsigned Digit = unsigned(Character) - '0';
        if (Digit > 9 || Value > (UINT64_MAX - Digit) / 10) return false;
        Value = Value * 10 + Digit;
    }
    return true;
}
bool GameplayUserId(std::string_view Text)
{
    if (Text.empty() || Text.size() > 20) return false;
    for (unsigned char Character : Text) if (Character < '0' || Character > '9') return false;
    return true;
}
bool GameplayName(std::string_view Text, size_t Maximum = 128)
{
    if (Text.size() > Maximum) return false;
    for (size_t Index = 0; Index < Text.size();) {
        uint32_t Code = uint8_t(Text[Index++]);
        if (!Code) return false;
        if (Code < 128) continue;
        unsigned Count; uint32_t Minimum;
        if (Code >= 0xc2 && Code <= 0xdf) { Count = 1; Minimum = 0x80; Code &= 31; }
        else if (Code >= 0xe0 && Code <= 0xef) { Count = 2; Minimum = 0x800; Code &= 15; }
        else if (Code >= 0xf0 && Code <= 0xf4) { Count = 3; Minimum = 0x10000; Code &= 7; }
        else return false;
        if (Count > Text.size() - Index) return false;
        while (Count--) {
            uint8_t Next = uint8_t(Text[Index++]);
            if ((Next & 0xc0) != 0x80) return false;
            Code = (Code << 6) | (Next & 63);
        }
        if (Code < Minimum || Code > 0x10ffff || (Code >= 0xd800 && Code <= 0xdfff)) return false;
    }
    return true;
}
bool GameplayCoordinate(std::string_view Text)
{
    if (Text.empty() || Text.size() > 32) return false;
    size_t Index = Text.front() == '-' ? 1 : 0;
    if (Index == Text.size()) return false;
    if (Text[Index] == '0') ++Index;
    else {
        if (Text[Index] < '1' || Text[Index] > '9') return false;
        while (Index < Text.size() && Text[Index] >= '0' && Text[Index] <= '9') ++Index;
    }
    if (Index < Text.size() && Text[Index] == '.') {
        size_t Start = ++Index;
        while (Index < Text.size() && Text[Index] >= '0' && Text[Index] <= '9') ++Index;
        if (Index == Start) return false;
    }
    if (Index < Text.size() && (Text[Index] == 'E' || Text[Index] == 'e')) {
        ++Index;
        if (Index < Text.size() && (Text[Index] == '+' || Text[Index] == '-')) ++Index;
        size_t Start = Index;
        while (Index < Text.size() && Text[Index] >= '0' && Text[Index] <= '9') ++Index;
        if (Index == Start) return false;
    }
    if (Index != Text.size()) return false;
    // Parse as binary32: Single.MaxValue's managed round-trip decimal may sit
    // just above its exact binary64 value while still rounding to finite max.
    float Value = 0;
    auto Result = std::from_chars(Text.data(), Text.data() + Text.size(), Value, std::chars_format::general);
    return Result.ec == std::errc{} && Result.ptr == Text.data() + Text.size() && std::isfinite(Value) &&
        (Value != 0 || Text == "0");
}
bool GameplaySnapshotCoordinate(std::string_view Text)
{
    if (!GameplayCoordinate(Text)) return false;
    // B3 snapshots materialize ordinary Luau Vector3 values. A Single's short
    // round-trip decimal can round to a finite float while exceeding the exact
    // host-coordinate bound when parsed as Luau's binary64 number.
    double Value = 0;
    auto Result = std::from_chars(Text.data(), Text.data() + Text.size(), Value, std::chars_format::general);
    constexpr double Maximum = 3.4028234663852886e38;
    return Result.ec == std::errc{} && Result.ptr == Text.data() + Text.size() && std::isfinite(Value) &&
        Value >= -Maximum && Value <= Maximum;
}
bool ValidateGameplay(const char* Bytes, uint32_t Length, uint64_t& Reservation)
{
    if (Length > 2048) return false;
    std::array<std::string_view,12> Fields;
    size_t Start = 0, Count = 0;
    for (size_t End = 0; End < Length; ++End) if (!Bytes[End]) {
        if (Count == Fields.size()) return false;
        Fields[Count++] = std::string_view(Bytes + Start, End - Start); Start = End + 1;
    }
    if (Count != Fields.size() || Start != Length) return false;
    uint64_t Ignored = 0;
    if (Fields[0] == "entityspawned") {
        if (!GameplayIdentity(Fields[1], Ignored) || !GameplayIdentity(Fields[2], Ignored) ||
            !GameplayIdentity(Fields[3], Ignored) || Fields[4].empty() || !GameplayName(Fields[4], 512) ||
            !GameplayIdentity(Fields[5], Reservation) || !GameplayIdentity(Fields[6], Ignored) ||
            !GameplayIdentity(Fields[7], Ignored)) return false;
        for (size_t Index = 8; Index < Fields.size(); ++Index) if (!Fields[Index].empty()) return false;
        return true;
    }
    if (Fields[0] == "entitydestroyed") {
        if (!GameplayIdentity(Fields[1], Ignored) || !GameplayIdentity(Fields[2], Ignored) ||
            !GameplayIdentity(Fields[3], Ignored) || Fields[4].empty() || !GameplayName(Fields[4], 512) ||
            !GameplayIdentity(Fields[5], Reservation)) return false;
        bool NoPosition = Fields[6].empty() && Fields[7].empty() && Fields[8].empty();
        if (!NoPosition && (!GameplaySnapshotCoordinate(Fields[6]) || !GameplaySnapshotCoordinate(Fields[7]) ||
            !GameplaySnapshotCoordinate(Fields[8]))) return false;
        for (size_t Index = 9; Index < Fields.size(); ++Index) if (!Fields[Index].empty()) return false;
        return true;
    }
    if (Fields[0] != "died" && Fields[0] != "spawned") return false;
    if (!GameplayIdentity(Fields[1], Ignored) || !GameplayIdentity(Fields[2], Ignored) ||
        !GameplayUserId(Fields[3]) || !GameplayName(Fields[4]) || !GameplayIdentity(Fields[5], Reservation)) return false;
    bool NoPosition = Fields[6].empty() && Fields[7].empty() && Fields[8].empty();
    if (!NoPosition && (!GameplayCoordinate(Fields[6]) || !GameplayCoordinate(Fields[7]) || !GameplayCoordinate(Fields[8]))) return false;
    bool NoKiller = Fields[9].empty() && Fields[10].empty() && Fields[11].empty();
    if (Fields[0] == "spawned") return NoKiller;
    return NoKiller || (GameplayIdentity(Fields[9], Ignored) && GameplayUserId(Fields[10]) && GameplayName(Fields[11]));
}
} // namespace
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
    if (Value < 1 || Value > 35 || Value == 31 || Value == 32 || Value == 33 ||
        Value != std::floor(Value)) luaL_error(State, "invalid host operation");
    uint32_t Operation = uint32_t(Value);
    if ((Operation == 4 || Operation == 28 || Operation == 29 || Operation == 30) && !CanMutateHost(Runtime)) {
        if ((Operation == 29 || Operation == 30) && Owner.Rejected != UINT64_MAX) ++Owner.Rejected;
        luaL_error(State, Operation == 4
            ? "SendMessage requires a committed domain; use task.defer for startup delivery"
            : Operation == 28
                ? "Teleport requires a committed domain; use task.defer for startup movement"
                : Operation == 29 ? "TakeItem requires a committed domain; use task.defer for startup mutation"
                : "GiveItem requires a committed domain; use task.defer for startup mutation");
    }
    if (Runtime.Publication && (Operation == 6 || Operation == 7 || Operation == 8 || Operation == 21 || Operation == 34) && !Runtime.Publication->Uses(&Owner)) {
        if (!ControlPublication(Runtime, Owner, 10)) luaL_error(State, "host publication setup failed");
        Runtime.Publication->Facades.push_back(&Owner);
    }
    if (lua_gettop(State) > 10) luaL_error(State, "host argument count exceeds limit");
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

int MakeFacadeUserdata(lua_State* State)
{
    if (lua_gettop(State) != 1 || lua_type(State, 1) != LUA_TTABLE) luaL_error(State, "invalid private userdata descriptor");
    lua_newuserdata(State, 0); lua_pushvalue(State, 1); lua_setmetatable(State, -2); return 1;
}

struct FacadeInput { Vm* Runtime; Domain* Owner; const std::string* Bytecode; };
int InstallFacade(lua_State* State)
{
    auto& Input = *static_cast<FacadeInput*>(lua_touserdata(State, 1));
    Vm& Runtime = *Input.Runtime; Domain& Owner = *Input.Owner;
    if (luau_load(State, "carbonluau.facade", Input.Bytecode->data(), Input.Bytecode->size(), 0) != LUA_OK) lua_error(State);
    lua_pushlightuserdata(State, &Owner);
    lua_pushcclosure(State, HostPrimitive, "host", 1);
    lua_pushcfunction(State, MakeFacadeUserdata, "private userdata");
    if (Runtime.GuiValueEqual == LUA_NOREF) lua_pushnil(State); else lua_getref(State, Runtime.GuiValueEqual);
    InstallStorage(State, Owner);
    if (Runtime.EntityIdentities == LUA_NOREF) lua_pushnil(State); else lua_getref(State, Runtime.EntityIdentities);
    InstallDiscovery(State, Owner);
    lua_call(State, 6, 6);
    Owner.FacadeCallbackRoots = lua_ref(State, -1); lua_pop(State, 1);
    if (Runtime.EntityIdentities == LUA_NOREF) Runtime.EntityIdentities = lua_ref(State, -1);
    lua_pop(State, 1);
    if (Runtime.GuiValueEqual == LUA_NOREF) Runtime.GuiValueEqual = lua_ref(State, -1);
    lua_pop(State, 1);
    Owner.GuiBindings = lua_ref(State, -1); lua_pop(State, 1);
    Owner.Dispatch = lua_ref(State, -1); lua_pop(State, 1);
    Owner.Game = lua_ref(State, -1); lua_pop(State, 1);
    return 0;
}

struct EventInput { Vm* Runtime; Domain* Owner; const char* Bytes; uint32_t Length; uint64_t GameplayReservation = 0; };
int EnqueueEvent(lua_State* State)
{
    auto& Input = *static_cast<EventInput*>(lua_touserdata(State, 1));
    Vm& Runtime = *Input.Runtime; Domain& Owner = *Input.Owner;
    CallbackScope Scope{State};
    lua_State* Thread = lua_newthread(State); Scope.Reference = lua_ref(State, -1);
    lua_getref(Thread, Owner.Dispatch);
    PushFields(Thread, Input.Bytes, Input.Length);
#ifdef CARBONLUAU_TESTING
    if (Input.GameplayReservation && TestGameplayCopyFailure) throw std::bad_alloc{};
#endif
    Owner.Queue.push_back(Callback{NowNs(), ++Runtime.Sequence, &Owner, Thread, Scope.Reference, 1,
        std::string(Input.Bytes, Input.Length), Input.GameplayReservation});
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
    FacadeInput Input{Runtime, Owner, &Compilation.Payload};
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
    const char* Separator = static_cast<const char*>(std::memchr(Payload, 0, Length));
    std::string_view Kind(Payload, size_t(Separator - Payload));
    // This reserved tag is never an ordinary script Signal/event dispatch.
    if (Kind == "discovery")
        return ReadyDiscovery(*Runtime, *Owner, Payload, Length);
    bool Gameplay = Kind == "died" || Kind == "spawned" || Kind == "entityspawned" || Kind == "entitydestroyed";
    uint64_t Reservation = 0;
    if (Gameplay) {
        if (!ValidateGameplay(Payload, Length, Reservation)) return CL_INVALID_ARGUMENT;
    } else if (Kind != "added" && Kind != "removing" && Kind != "command" && Kind != "activated")
        return CL_INVALID_ARGUMENT;
    if (Owner->Queue.size() >= Owner->MaxQueued || Runtime->Sequence == UINT64_MAX) {
        if (Owner->Rejected != UINT64_MAX) ++Owner->Rejected;
        return CL_INVALID_ARGUMENT;
    }
    if (Gameplay && !ReserveGameplay(*Runtime, *Owner, Reservation)) {
        if (Owner->Rejected != UINT64_MAX) ++Owner->Rejected;
        return CL_INVALID_ARGUMENT;
    }
    EventInput Input{Runtime, Owner, Payload, Length, Reservation};
    try {
        if (lua_cpcall(Runtime->State, EnqueueEvent, &Input) != LUA_OK) {
            if (Gameplay) ReleaseGameplay(*Runtime, *Owner, Reservation, false);
            Retire(*Runtime); return CL_MEMORY_LIMIT;
        }
    } catch (const std::bad_alloc&) {
        if (Gameplay) ReleaseGameplay(*Runtime, *Owner, Reservation, false);
        Retire(*Runtime); return CL_MEMORY_LIMIT;
    } catch (...) {
        if (Gameplay) ReleaseGameplay(*Runtime, *Owner, Reservation, false);
        Retire(*Runtime); return CL_INTERNAL_ERROR;
    }
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
