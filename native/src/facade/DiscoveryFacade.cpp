#include "../runtime/RuntimeInternal.hpp"
#include <charconv>
#include <new>

namespace CarbonLuau::Runtime {
namespace {
constexpr uint64_t DiscoveryDeadlineNs = 120000000000ull;
constexpr uint32_t MaximumResults = 256;

void CheckDeadline(Vm& Runtime)
{
    if (std::chrono::steady_clock::now() >= Runtime.Deadline) throw DeadlineExceeded{};
}

bool RouteValue(const char* Bytes, size_t Length, uint64_t& Value)
{
    if (!Length || Length > 20 || Bytes[0] == '0') return false;
    Value = 0;
    for (size_t Index = 0; Index < Length; ++Index) {
        unsigned Digit = unsigned(static_cast<unsigned char>(Bytes[Index])) - '0';
        if (Digit > 9 || Value > (UINT64_MAX - Digit) / 10) return false;
        Value = Value * 10 + Digit;
    }
    return true;
}

size_t RouteText(uint64_t Route, char (&Bytes)[21])
{
    auto Result = std::to_chars(Bytes, Bytes + 20, Route);
    *Result.ptr = 0;
    return size_t(Result.ptr - Bytes) + 1;
}

double Number(lua_State* State, int Index, bool Positive)
{
    if (lua_type(State, Index) != LUA_TNUMBER) luaL_error(State, "discovery expected number");
    double Value = lua_tonumber(State, Index);
    if (!std::isfinite(Value) || (Positive && Value < 0)) luaL_error(State, "discovery expected finite nonnegative radius");
    return Value == 0 ? 0 : Value;
}

void AddNumber(lua_State* State, char* Bytes, size_t& Used, double Value)
{
    // Locale-independent round-trippable binary64 text, at most 32 bytes.
    auto Result = std::to_chars(Bytes + Used, Bytes + Used + 32, Value, std::chars_format::general);
    if (Result.ec != std::errc{}) luaL_error(State, "discovery numeric snapshot failed");
    Used = size_t(Result.ptr - Bytes); Bytes[Used++] = 0;
}

void Options(lua_State* State, std::string& Prefab, uint32_t& Limit)
{
    if (lua_isnil(State, 6)) return;
    if (lua_type(State, 6) != LUA_TTABLE || lua_getmetatable(State, 6))
        luaL_error(State, "discovery Options must be an ordinary table without metatable");
    lua_pushnil(State);
    unsigned Count = 0;
    while (lua_next(State, 6)) {
        if (++Count > 2 || lua_type(State, -2) != LUA_TSTRING)
            luaL_error(State, "unsupported discovery option");
        size_t Length = 0; const char* Key = lua_tolstring(State, -2, &Length);
        if (Length == 6 && !std::memcmp(Key, "Prefab", 6)) {
            if (lua_type(State, -1) != LUA_TSTRING) luaL_error(State, "discovery Prefab must be a string");
            const char* Text = lua_tolstring(State, -1, &Length);
            if (!Length || Length > 512) luaL_error(State, "discovery Prefab exceeds bound");
            Prefab.assign(Text, Length);
            if (!Persistence::ValidText(Prefab, 512)) luaL_error(State, "discovery Prefab must be exact UTF-8 without NUL");
        } else if (Length == 5 && !std::memcmp(Key, "Limit", 5)) {
            double Value = Number(State, -1, true);
            if (Value < 1 || Value > MaximumResults || Value != std::floor(Value))
                luaL_error(State, "discovery Limit must be an integer in 1..256");
            Limit = uint32_t(Value);
        } else luaL_error(State, "unsupported discovery option");
        lua_pop(State, 1);
    }
}

void ValidateResponse(lua_State* State, const char* Bytes, size_t Length)
{
    // The fetch never materializes an unbounded host field list, even if the
    // trusted transport malfunctions. Lifetime/publication validation is managed.
    if (Length > 1 + MaximumResults * 4 * 21) luaL_error(State, "discovery response exceeds bound");
    size_t Start = 0; unsigned Count = 0; bool Error = false;
    for (size_t End = 0; End < Length; ++End) if (!Bytes[End]) {
        size_t Size = End - Start;
        if (++Count == 1) {
            Error = Size != 0;
            if (Size > 200) luaL_error(State, "invalid discovery error response");
        } else {
            uint64_t Value = 0;
            if (Error || Count > 1 + MaximumResults * 4 || !RouteValue(Bytes + Start, Size, Value))
                luaL_error(State, "invalid discovery Entity response");
        }
        Start = End + 1;
    }
    if (!Count || Start != Length || (!Error && (Count - 1) % 4))
        luaL_error(State, "invalid discovery response");
}

int CompleteDiscovery(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain& Owner = BoundDomain(State);
    auto& Work = *static_cast<DiscoveryCallback*>(lua_touserdata(State, lua_upvalueindex(2)));
    if (!CanDispatchDiscovery(Runtime, Owner) || !Work.Accepted || !Work.Ready)
        luaL_error(State, "stale discovery completion");
    CheckDeadline(Runtime);
    if (NowNs() >= Work.Expires) {
        lua_pushvalue(State, lua_upvalueindex(3));
        lua_createtable(State, 1, 0);
        lua_pushliteral(State, "DiscoveryDeadline"); lua_rawseti(State, -2, 1);
        lua_call(State, 1, 0);
        return 0;
    }
    char Route[21]; size_t Length = RouteText(Work.Route, Route);
    uint32_t Written = 0;
    uint32_t Status = Owner.Host(Owner.HostIdentity, 38, Route, uint32_t(Length), Owner.HostBuffer->data(),
        uint32_t(Owner.HostBuffer->size()), &Written);
    CheckDeadline(Runtime);
    if (Written > Owner.HostBuffer->size()) luaL_error(State, "invalid discovery response length");
    lua_pushvalue(State, lua_upvalueindex(3));
    if (Status) {
        // A fetch/admission rejection is an asynchronous controlled failure.
        lua_createtable(State, 1, 0);
        lua_pushliteral(State, "DiscoveryUnavailable"); lua_rawseti(State, -2, 1);
    } else {
        ValidateResponse(State, Owner.HostBuffer->data(), Written);
        PushFields(State, Owner.HostBuffer->data(), Written);
    }
    CheckDeadline(Runtime);
    lua_call(State, 1, 0);
    return 0;
}

struct Submission {
    Vm& Runtime; Domain& Owner; DiscoveryCallback& Work;
    ~Submission() { if (!Work.Accepted) ReleaseDiscovery(Runtime, Owner, Work); }
};

int SubmitDiscovery(lua_State* State)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    Domain& Owner = BoundDomain(State);
    if (!CanDispatchDiscovery(Runtime, Owner))
        luaL_error(State, "discovery requires a committed admission for the Workspace owner without publication");
    if (lua_gettop(State) != 6 || lua_type(State, 5) != LUA_TFUNCTION)
        luaL_error(State, "discovery callback function required");
    double X = Number(State, 1, false), Y = Number(State, 2, false), Z = Number(State, 3, false);
    constexpr double MaximumHostFloat = 3.4028234663852886e38;
    if (std::abs(X) > MaximumHostFloat || std::abs(Y) > MaximumHostFloat || std::abs(Z) > MaximumHostFloat)
        luaL_error(State, "discovery position outside Vector3 range");
    double Radius = Number(State, 4, true);
    std::string Prefab; uint32_t Limit = MaximumResults;
    Options(State, Prefab, Limit);
    CheckDeadline(Runtime);
    DiscoveryCallback* Slot = nullptr;
    for (auto& Work : Owner.DiscoveryCallbacks) if (!Work.Route) { Slot = &Work; break; }
    if (!Slot || Runtime.DiscoveryReserved >= 8 || Runtime.DiscoverySequence == UINT64_MAX)
        luaL_error(State, "discovery queue exhausted");
    Slot->Route = ++Runtime.DiscoverySequence;
    Slot->Expires = NowNs() + DiscoveryDeadlineNs;
    ++Runtime.DiscoveryReserved;
    Submission Guard{Runtime, Owner, *Slot};
    // All fallible callback-root construction precedes host acceptance.
    CallbackScope Reference{State};
    Slot->Thread = lua_newthread(State); Reference.Reference = lua_ref(State, -1); lua_pop(State, 1);
    lua_pushlightuserdata(Slot->Thread, &Owner);
    lua_pushlightuserdata(Slot->Thread, Slot);
    lua_pushvalue(State, 5); lua_xmove(State, Slot->Thread, 1);
    lua_pushcclosure(Slot->Thread, CompleteDiscovery, "discovery completion", 3);
    Slot->Reference = Reference.Reference; Reference.Reference = LUA_NOREF;
    char Request[1024], Route[21]; size_t Used = RouteText(Slot->Route, Route);
    std::memcpy(Request, Route, Used);
    for (double Value : {X, Y, Z, Radius}) AddNumber(State, Request, Used, Value);
    std::memcpy(Request + Used, Prefab.data(), Prefab.size()); Used += Prefab.size(); Request[Used++] = 0;
    auto Result = std::to_chars(Request + Used, Request + Used + 3, Limit);
    Used = size_t(Result.ptr - Request); Request[Used++] = 0;
    CheckDeadline(Runtime);
    uint32_t Written = 0;
    Slot->Submitted = true;
    uint32_t Status = Owner.Host(Owner.HostIdentity, 36, Request, uint32_t(Used), Owner.HostBuffer->data(),
        uint32_t(Owner.HostBuffer->size()), &Written);
    if (Status) luaL_error(State, "discovery submission rejected");
    Slot->Accepted = true;
    if (Written) Runtime.IntegrityFailed = true;
    CheckDeadline(Runtime);
    return 0;
}

int ControlledSubmission(lua_State* State)
{
    try { return SubmitDiscovery(State); }
    catch (const std::bad_alloc&) {
        static_cast<Vm*>(lua_callbacks(State)->userdata)->AllocationFailed = true;
        luaL_error(State, "discovery allocation failure");
    }
}
} // namespace

bool CanDispatchDiscovery(const Vm& Runtime, const Domain& Owner)
{
    return Runtime.Owner == std::this_thread::get_id() && Runtime.State && !Runtime.IntegrityFailed &&
        Owner.Alive && Owner.Active && Owner.Host && Owner.HostBuffer && CanMutateHost(Runtime) &&
        Runtime.Admission->Owner == &Owner;
}

void InstallDiscovery(lua_State* State, Domain& Owner)
{
    lua_pushlightuserdata(State, &Owner);
    lua_pushcclosure(State, ControlledSubmission, "private discovery submission", 1);
}

bool ReleaseDiscovery(Vm& Runtime, Domain& Owner, DiscoveryCallback& Work)
{
    if (!Work.Route) return true;
    bool Success = true;
    if (Work.Submitted) {
        char Route[21]; size_t Length = RouteText(Work.Route, Route); uint32_t Written = 0;
        Success = Owner.Host && Owner.HostBuffer && Owner.Host(Owner.HostIdentity, 37, Route, uint32_t(Length),
            Owner.HostBuffer->data(), uint32_t(Owner.HostBuffer->size()), &Written) == 0 && !Written;
    }
    if (Runtime.State && Work.Reference != LUA_NOREF) lua_unref(Runtime.State, Work.Reference);
    Work = {};
    --Runtime.DiscoveryReserved;
    if (!Success) Runtime.IntegrityFailed = true;
    return Success;
}

void ClearDiscovery(Vm& Runtime, Domain& Owner)
{
    for (auto& Work : Owner.DiscoveryCallbacks) if (Work.Route) {
        ReleaseDiscovery(Runtime, Owner, Work);
        if (Owner.Discarded != UINT64_MAX) ++Owner.Discarded;
        if (Runtime.RetiredDiscarded != UINT64_MAX) ++Runtime.RetiredDiscarded;
    }
}

void ExpireDiscovery(Vm& Runtime, Domain& Owner, uint64_t Now)
{
    for (auto& Work : Owner.DiscoveryCallbacks) if (Work.Accepted && !Work.Ready && Now >= Work.Expires) {
        if (Runtime.Sequence == UINT64_MAX) { Runtime.IntegrityFailed = true; return; }
        // Expiry owes one error callback while the owner remains live. Preserve
        // the root and managed route until that later bounded admission.
        Work.Due = Now; Work.Sequence = ++Runtime.Sequence; Work.Ready = true;
    }
}

DiscoveryCallback* NextDiscovery(Domain& Owner)
{
    DiscoveryCallback* First = nullptr;
    for (auto& Work : Owner.DiscoveryCallbacks) if (Work.Accepted && Work.Ready &&
        (!First || Work.Sequence < First->Sequence)) First = &Work;
    return First;
}

ClStatus ReadyDiscovery(Vm& Runtime, Domain& Owner, const char* Bytes, uint32_t Length)
{
    uint64_t Route = 0;
    if (Length < 12 || Length > 31 || std::memcmp(Bytes, "discovery\0", 10) ||
        Bytes[Length - 1] || !RouteValue(Bytes + 10, Length - 11, Route)) return CL_INVALID_ARGUMENT;
    ExpireDiscovery(Runtime, Owner, NowNs());
    if (Runtime.IntegrityFailed) { Retire(Runtime); return CL_INTERNAL_ERROR; }
    for (auto& Work : Owner.DiscoveryCallbacks) if (Work.Route == Route && Work.Accepted && !Work.Notified) {
        if (!Work.Ready) {
            if (Runtime.Sequence == UINT64_MAX) return CL_INVALID_ARGUMENT;
            Work.Due = NowNs(); Work.Sequence = ++Runtime.Sequence; Work.Ready = true;
        }
        Work.Notified = true;
        return CL_OK;
    }
    return CL_INVALID_ARGUMENT;
}
} // namespace CarbonLuau::Runtime
