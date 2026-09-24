#include "../runtime/RuntimeInternal.hpp"
#include <new>

// @carbonluau-api {"Type":"DataStore","Kind":"Class","Summary":"Sealed private namespace/store facade; no fields, authority transfer or automatic retry.","SinceApi":"0.5.0-experimental","Qualification":"Experimental","Preview":"Unavailable"}
// @carbonluau-api {"Type":"PersistedValue","Kind":"Value","Representation":"Alias","TypeExpression":"boolean | number | string | {PersistedValue} | {[string]: PersistedValue}","Summary":"Finite binary64, UTF-8 strings, dense arrays or string-keyed maps; no nil, metatables, cycles or host objects. D21 bounds: depth 16; 1,024 entries/table, 4,096 expanded entries; 64 KiB envelope; 16 KiB strings; 128-byte map keys. Runtime validation is authoritative.","SinceApi":"0.5.0-experimental","Qualification":"Experimental","Preview":"Unavailable"}

namespace CarbonLuau::Runtime {
#ifdef CARBONLUAU_TESTING
bool TestStorageCopyFailure = false;
#endif
namespace {
namespace P = CarbonLuau::Persistence;
constexpr const char* ServiceType = "CarbonLuau.DataStoreService";
constexpr const char* StoreType = "CarbonLuau.DataStore";
constexpr size_t MaximumDepth = 16;
constexpr size_t MaximumTableEntries = 1024;
constexpr size_t MaximumEntries = 4096;
constexpr size_t MaximumStoreNames = 64;
struct StorageFacade {
    Domain* Owner;
    std::shared_ptr<StoragePublication> Publication;
    std::string Store;
};
void Deadline(Vm& Runtime)
{
    if (P::Clock::now() >= Runtime.Deadline) throw DeadlineExceeded{};
}
bool Published(const std::shared_ptr<StoragePublication>& Publication)
{
    for (auto Item = Publication; Item; Item = Item->Parent) if (!Item->Alive) return false;
    return true;
}
std::shared_ptr<StoragePublication> Publication(PublicationScope* Scope)
{
    if (!Scope) return {};
    if (!Scope->Storage) {
        auto Parent = Publication(Scope->Parent);
        Scope->Storage = std::make_shared<StoragePublication>();
        Scope->Storage->Parent = std::move(Parent);
    }
    return Scope->Storage;
}
Vm& RuntimeFor(lua_State* State) { return *static_cast<Vm*>(lua_callbacks(State)->userdata); }
void Authority(lua_State* State, Vm& Runtime, Domain& Owner)
{
    if (!GetDomain(Runtime, Owner.Id) || !Owner.Host) luaL_error(State, "StaleDataStore");
    if (!Runtime.Admission || Runtime.Admission->Owner != &Owner) luaL_error(State, "ForeignDataStore");
    Deadline(Runtime);
}
StorageFacade& Facade(lua_State* State, const char* Type)
{
    auto& Value = *static_cast<StorageFacade*>(luaL_checkudata(State, 1, Type));
    Authority(State, RuntimeFor(State), *Value.Owner);
    if (!Published(Value.Publication)) luaL_error(State, "StaleDataStore");
    return Value;
}
std::string Text(lua_State* State, int Index, size_t Maximum, bool Name)
{
    if (lua_type(State, Index) != LUA_TSTRING) luaL_error(State, "DataStore expected string");
    size_t Length = 0; const char* Bytes = lua_tolstring(State, Index, &Length);
    P::Require(Length <= Maximum);
    std::string Value(Bytes, Length);
    P::Require(P::ValidText(Value, Maximum, Name));
    return Value;
}
void PushFacade(lua_State* State, Domain& Owner, const char* Type, const std::string& Store)
{
    auto Scope = Publication(RuntimeFor(State).Publication);
    // Construct before any further VM allocation so the VM destructor always
    // observes a fully initialized C++ object, including protected failures.
    void* Memory = lua_newuserdatadtor(State, sizeof(StorageFacade), [](lua_State*, void* Pointer) {
        static_cast<StorageFacade*>(Pointer)->~StorageFacade();
    });
    new (Memory) StorageFacade{&Owner, std::move(Scope), {}};
    static_cast<StorageFacade*>(Memory)->Store = Store;
    luaL_getmetatable(State, Type); lua_setmetatable(State, -2);
}
template<int (*Body)(lua_State*)> int Controlled(lua_State* State)
{
    try { return Body(State); }
    catch (const P::Failure& Failure) {
        if (Failure.Code == P::Error::DeadlineExceeded) throw DeadlineExceeded{};
        luaL_error(State, "DataStore invalid argument or value bound");
    } catch (const std::bad_alloc&) {
        RuntimeFor(State).AllocationFailed = true;
        luaL_error(State, "DataStore allocation failure");
    }
}

struct Snapshot {
    Vm& Runtime;
    size_t Bytes = 44;
    unsigned Entries = 0;
    std::array<const void*,MaximumDepth> Ancestors{};
    void Room(size_t Count) {
        Deadline(Runtime);
        P::Require(Count <= P::MaximumEnvelope - Bytes); Bytes += Count;
    }
    std::shared_ptr<P::Value> Read(lua_State* State, int Index, unsigned Depth = 0)
    {
        Deadline(Runtime); luaL_checkstack(State, 6, "DataStore snapshot stack bound");
        Index = lua_absindex(State, Index);
        Room(1);
        auto Value = std::make_shared<P::Value>();
        switch (lua_type(State, Index)) {
        case LUA_TBOOLEAN:
            Room(1); Value->Type = P::Kind::Boolean; Value->Boolean = lua_toboolean(State, Index) != 0; break;
        case LUA_TNUMBER:
            Room(8); Value->Type = P::Kind::Number; Value->Number = lua_tonumber(State, Index);
            P::Require(std::isfinite(Value->Number)); break;
        case LUA_TSTRING: {
            size_t Length = 0; lua_tolstring(State, Index, &Length);
            Room(4 + Length); Value->Type = P::Kind::String; Value->String = Text(State, Index, 16384, false); break;
        }
        case LUA_TTABLE: {
            P::Require(Depth < MaximumDepth);
            if (lua_getmetatable(State, Index)) { lua_pop(State, 1); throw P::Failure(P::Error::InvalidArgument); }
            const void* Pointer = lua_topointer(State, Index);
            for (unsigned I = 0; I < Depth; ++I) P::Require(Ancestors[I] != Pointer);
            Ancestors[Depth] = Pointer;
            Room(4); unsigned Count = 0; bool Array = false, Map = false;
            lua_pushnil(State);
            while (lua_next(State, Index)) {
                Deadline(Runtime);
                P::Require(++Count <= MaximumTableEntries && ++Entries <= MaximumEntries);
                if (lua_type(State, -2) == LUA_TNUMBER) {
                    Array = true; P::Require(!Map);
                    double Key = lua_tonumber(State, -2);
                    // Bound indices before resizing or traversing any array.
                    P::Require(std::isfinite(Key) && Key >= 1 && Key <= MaximumTableEntries && Key == std::floor(Key));
                    size_t Position = size_t(Key);
                    if (Value->Array.size() < Position) Value->Array.resize(Position);
                    Value->Array[Position - 1] = Read(State, -1, Depth + 1);
                } else {
                    Map = true; P::Require(!Array && lua_type(State, -2) == LUA_TSTRING);
                    size_t Length = 0; lua_tolstring(State, -2, &Length); Room(4 + Length);
                    auto Key = Text(State, -2, 128, false);
                    auto Child = Read(State, -1, Depth + 1);
                    Value->Map.emplace_back(std::move(Key), std::move(Child));
                }
                lua_pop(State, 1);
            }
            if (Array) { P::Require(Value->Array.size() == Count); Value->Type = P::Kind::Array; }
            else Value->Type = P::Kind::Map;
            break;
        }
        default: throw P::Failure(P::Error::InvalidArgument);
        }
        Deadline(Runtime); return Value;
    }
};
void PushValue(lua_State* State, const P::Value& Value)
{
    Deadline(RuntimeFor(State)); luaL_checkstack(State, 6, "DataStore materialization stack bound");
    switch (Value.Type) {
    case P::Kind::Boolean: lua_pushboolean(State, Value.Boolean); break;
    case P::Kind::Number: lua_pushnumber(State, Value.Number); break;
    case P::Kind::String: lua_pushlstring(State, Value.String.data(), Value.String.size()); break;
    case P::Kind::Array:
        lua_createtable(State, int(Value.Array.size()), 0);
        for (size_t I = 0; I < Value.Array.size(); ++I) { PushValue(State, *Value.Array[I]); lua_rawseti(State, -2, int(I + 1)); }
        break;
    case P::Kind::Map:
        lua_createtable(State, 0, int(Value.Map.size()));
        for (const auto& Pair : Value.Map) {
            lua_pushlstring(State, Pair.first.data(), Pair.first.size()); PushValue(State, *Pair.second); lua_rawset(State, -3);
        }
        break;
    }
    Deadline(RuntimeFor(State));
}
const char* ErrorName(uint32_t Code)
{
    static const char* Names[] = {"", "InvalidArgument", "QuotaExceeded", "StorageUnavailable", "StorageBusy",
        "StorageFull", "StorageCorrupt", "FormatUnsupported", "DeadlineExceeded", "StorageError", "Indeterminate"};
    return Code < std::size(Names) ? Names[Code] : "StorageError";
}
int CompleteCallback(lua_State* State)
{
    auto& Work = *static_cast<StorageCallback*>(lua_touserdata(State, lua_upvalueindex(2)));
    Vm& Runtime = RuntimeFor(State);
    Deadline(Runtime);
    uint32_t Error = Work.Error;
    std::shared_ptr<P::Value> Value;
    if (!Error && Work.Operation == 1 && Work.Found) {
        try { Value = P::Decode(Work.Identity, Work.Envelope, Runtime.Deadline); }
        catch (const P::Failure& Failure) {
            if (Failure.Code == P::Error::DeadlineExceeded) throw DeadlineExceeded{};
            Error = uint32_t(Failure.Code == P::Error::FormatUnsupported ? P::Error::FormatUnsupported : P::Error::StorageCorrupt);
        }
    }
    lua_pushvalue(State, lua_upvalueindex(1));
    if (Error || (Work.Operation == 1 && !Work.Found)) lua_pushnil(State);
    else if (Work.Operation == 1) PushValue(State, *Value);
    else lua_pushboolean(State, Work.Operation == 2 || Work.Found);
    if (Error) lua_pushstring(State, ErrorName(Error)); else lua_pushnil(State);
    Deadline(Runtime);
    lua_call(State, 2, 0);
    return 0;
}
struct Submission {
    Vm& Runtime;
    Domain& Owner;
    size_t Index;
    ~Submission() {
        auto& Slot = Owner.StorageCallbacks[Index];
        if (!Slot || Slot->Accepted) return;
        if (Slot->Reference != LUA_NOREF) lua_unref(Runtime.State, Slot->Reference);
        ReleaseStorage(Runtime, Owner, Slot->Route); Slot.reset();
    }
};
int Submit(lua_State* State, uint32_t Operation)
{
    auto& Store = Facade(State, StoreType);
    Vm& Runtime = RuntimeFor(State); Domain& Owner = *Store.Owner;
    if (!CanDispatchStorage(Runtime, Owner)) luaL_error(State, "DataStore async operations require a committed admission without publication");
    int CallbackIndex = Operation == 2 ? 4 : 3;
    if (lua_gettop(State) != CallbackIndex || lua_type(State, CallbackIndex) != LUA_TFUNCTION)
        luaL_error(State, "DataStore callback function required");
    P::Identity Identity{!Owner.PackageId.empty(), Owner.PackageId, Store.Store, Text(State, 2, 128, true)};
    P::Bytes Envelope;
    if (Operation == 2) {
        Snapshot Reader{Runtime}; auto Value = Reader.Read(State, 3);
        Envelope = P::Encode(Identity, *Value, Runtime.Deadline);
    }
    Deadline(Runtime);
    if (Runtime.StorageSequence == UINT64_MAX) luaL_error(State, "DataStore route exhausted");
    uint64_t Route = ++Runtime.StorageSequence;
    if (!ReserveStorage(Runtime, Owner, Route)) luaL_error(State, "DataStore queue exhausted");
    size_t Index = size_t(std::find(Owner.StorageReservations.begin(), Owner.StorageReservations.end(), Route) - Owner.StorageReservations.begin());
    // A guard exists before any fallible construction after the reservation.
    try { Owner.StorageCallbacks[Index] = std::make_unique<StorageCallback>(); }
    catch (...) { ReleaseStorage(Runtime, Owner, Route); throw; }
    auto& Slot = *Owner.StorageCallbacks[Index]; Slot.Route = Route;
    Submission Guard{Runtime, Owner, Index};
    Slot.Operation = Operation; Slot.Identity = std::move(Identity);
    CallbackScope Reference{State};
    Slot.Thread = lua_newthread(State); Reference.Reference = lua_ref(State, -1); lua_pop(State, 1);
    lua_pushvalue(State, CallbackIndex); lua_xmove(State, Slot.Thread, 1);
    lua_pushlightuserdata(Slot.Thread, &Slot);
    lua_pushcclosure(Slot.Thread, Controlled<CompleteCallback>, "DataStore completion", 2);
    Slot.Reference = Reference.Reference; Reference.Reference = LUA_NOREF;
    P::Bytes Frame{'C','L','P','B'};
    Frame.reserve(56 + Slot.Identity.Package.size() + Slot.Identity.Store.size() + Slot.Identity.Key.size() + Envelope.size());
    P::Put32(Frame, 1); P::Put32(Frame, Operation);
    P::Put64(Frame, Runtime.GenerationId); P::Put64(Frame, Owner.Id); P::Put64(Frame, Route);
    P::Put32(Frame, Slot.Identity.Addon ? 1 : 0);
    P::Put32(Frame, uint32_t(Slot.Identity.Package.size())); P::Put32(Frame, uint32_t(Slot.Identity.Store.size()));
    P::Put32(Frame, uint32_t(Slot.Identity.Key.size())); P::Put32(Frame, uint32_t(Envelope.size()));
    for (const auto* Value : {&Slot.Identity.Package, &Slot.Identity.Store, &Slot.Identity.Key})
        Frame.insert(Frame.end(), Value->begin(), Value->end());
    Frame.insert(Frame.end(), Envelope.begin(), Envelope.end());
    Deadline(Runtime);
    uint32_t Written = 0;
    uint32_t Status = Owner.Host(Owner.HostIdentity, 31, reinterpret_cast<const char*>(Frame.data()), uint32_t(Frame.size()),
        Owner.HostBuffer->data(), uint32_t(Owner.HostBuffer->size()), &Written);
    if (!Status) {
        Slot.Accepted = true;
        // Acceptance is irrevocable: no allocation follows the host response.
        // Cancellation still retires the VM without undoing or replaying storage.
        if (Written) Runtime.IntegrityFailed = true;
        Deadline(Runtime);
        return 0;
    }
    static const char* Rejections[] = {"", "DataStore invalid request", "StorageUnavailable", "DataStore queue exhausted",
        "DataStore rate exhausted", "ForeignDataStore", "DataStore admission failed"};
    luaL_error(State, "%s", Status < std::size(Rejections) ? Rejections[Status] : "DataStore host protocol failure");
}
// @carbonluau-api {"Owner":"DataStoreService","Name":"GetDataStore","Kind":"Method","Args":[["StoreName","string"]],"Returns":["DataStore"],"Summary":"Disk-free private store acquisition, allowed during publication; exact current admission must match resource owner. D21 store names: 1..64 UTF-8 bytes; 64 distinct names/domain. No success or I/O implied.","SinceApi":"0.5.0-experimental","Qualification":"Experimental","Preview":"Unavailable","Binding":"int GetDataStore(lua_State* State)"}
int GetDataStore(lua_State* State)
{
    auto& Service = Facade(State, ServiceType);
    if (lua_gettop(State) != 2) luaL_error(State, "GetDataStore expects one store name");
    std::string Name = Text(State, 2, 64, true);
    auto& Names = Service.Owner->StorageNames;
    Names.erase(std::remove_if(Names.begin(), Names.end(), [](const StorageName& Item) { return !Published(Item.Publication); }), Names.end());
    bool Existing = std::any_of(Names.begin(), Names.end(), [&](const StorageName& Item) { return Item.Name == Name; });
    if (!Existing && Names.size() == MaximumStoreNames) luaL_error(State, "DataStore acquired name limit");
    PushFacade(State, *Service.Owner, StoreType, Name);
    if (!Existing) Names.push_back(StorageName{std::move(Name), Publication(RuntimeFor(State).Publication)});
    return 1;
}
// @carbonluau-api {"Owner":"DataStore","Name":"GetAsync","Kind":"Method","Args":[["Key","string"],["Callback","(PersistedValue?, string?) -> ()"]],"Returns":[],"Summary":"Committed-only non-yielding read submission. Later callback receives fresh value/nil, nil/nil for absence, or nil/ErrorCode. Immediate return means acceptance only; rejected calls owe no callback. D21 keys: 1..128 UTF-8 bytes; 8 pending/namespace, 128 globally; 5-second request deadline. Private owner required; retirement suppresses delivery.","SinceApi":"0.5.0-experimental","Qualification":"Experimental","Preview":"Unavailable","Binding":"int GetAsync(lua_State* State)"}
int GetAsync(lua_State* State)
{ return Submit(State, 1); }
// @carbonluau-api {"Owner":"DataStore","Name":"SetAsync","Kind":"Method","Args":[["Key","string"],["Value","PersistedValue"],["Callback","(boolean?, string?) -> ()"]],"Returns":[],"Summary":"Committed-only irreversible write; bounded snapshot before acceptance. Later true/nil follows durable commit and validated response; nil/ErrorCode is failure. Indeterminate may have committed. No rollback or automatic replay; stale callbacks are suppressed.","SinceApi":"0.5.0-experimental","Qualification":"Experimental","Preview":"Unavailable","Binding":"int SetAsync(lua_State* State)"}
int SetAsync(lua_State* State)
{ return Submit(State, 2); }
// @carbonluau-api {"Owner":"DataStore","Name":"RemoveAsync","Kind":"Method","Args":[["Key","string"],["Callback","(boolean?, string?) -> ()"]],"Returns":[],"Summary":"Committed-only irreversible removal. Later true/nil means removed, false/nil means absent; nil/ErrorCode means failure, possibly Indeterminate. Immediate return means acceptance only; no rollback, replay or guaranteed delivery after retirement.","SinceApi":"0.5.0-experimental","Qualification":"Experimental","Preview":"Unavailable","Binding":"int RemoveAsync(lua_State* State)"}
int RemoveAsync(lua_State* State)
{ return Submit(State, 3); }
int ReadOnly(lua_State* State) { luaL_error(State, "DataStore facades are immutable"); }
int GetService(lua_State* State)
{
    auto& Owner = BoundDomain(State); Authority(State, RuntimeFor(State), Owner);
#ifdef CARBONLUAU_PREVIEW
    luaL_error(State, "[Preview:UnsupportedHost] DataStoreService is unavailable in preview");
#endif
    PushFacade(State, Owner, ServiceType, {}); return 1;
}
void Metatable(lua_State* State, const char* Name, bool Service)
{
    if (luaL_newmetatable(State, Name)) {
        lua_pushstring(State, Service ? "DataStoreService" : "DataStore"); lua_setfield(State, -2, "__type");
        lua_pushstring(State, "locked"); lua_setfield(State, -2, "__metatable");
        lua_pushcfunction(State, ReadOnly, "immutable DataStore"); lua_setfield(State, -2, "__newindex");
        lua_newtable(State);
        if (Service) { lua_pushcfunction(State, Controlled<GetDataStore>, "GetDataStore"); lua_setfield(State, -2, "GetDataStore"); }
        else {
            lua_pushcfunction(State, Controlled<GetAsync>, "GetAsync"); lua_setfield(State, -2, "GetAsync");
            lua_pushcfunction(State, Controlled<SetAsync>, "SetAsync"); lua_setfield(State, -2, "SetAsync");
            lua_pushcfunction(State, Controlled<RemoveAsync>, "RemoveAsync"); lua_setfield(State, -2, "RemoveAsync");
        }
        lua_setreadonly(State, -1, 1); lua_setfield(State, -2, "__index"); lua_setreadonly(State, -1, 1);
    }
    lua_pop(State, 1);
}
}

void InstallStorage(lua_State* State, Domain& Owner)
{
    Metatable(State, ServiceType, true); Metatable(State, StoreType, false);
    lua_pushlightuserdata(State, &Owner); lua_pushcclosure(State, Controlled<GetService>, "DataStoreService", 1);
}
bool ReleaseStorageHost(Vm& Runtime, Domain& Owner, uint64_t Route)
{
    if (!Owner.Host || !Owner.HostBuffer) return false;
    std::array<char,32> Frame{{'C','L','P','R',1,0,0,0}};
    const uint64_t Values[] = {Runtime.GenerationId, Owner.Id, Route};
    for (size_t I = 0; I < 3; ++I) for (unsigned Shift = 0; Shift < 64; Shift += 8)
        Frame[8 + I * 8 + Shift / 8] = char(Values[I] >> Shift);
    uint32_t Written = 0;
    return Owner.Host(Owner.HostIdentity, 32, Frame.data(), uint32_t(Frame.size()), Owner.HostBuffer->data(),
        uint32_t(Owner.HostBuffer->size()), &Written) == 0 && Written == 0;
}
void ClearStorage(Vm& Runtime, Domain& Owner)
{
    for (auto& Slot : Owner.StorageCallbacks) if (Slot) {
        if (Slot->Accepted) ReleaseStorageHost(Runtime, Owner, Slot->Route);
        if (Runtime.State && Slot->Reference != LUA_NOREF) lua_unref(Runtime.State, Slot->Reference);
        ReleaseStorage(Runtime, Owner, Slot->Route); Slot.reset();
        ++Runtime.RetiredDiscarded; ++Owner.Discarded;
    }
    Owner.StorageNames.clear();
}
StorageCallback* NextStorage(Domain& Owner)
{
    StorageCallback* First = nullptr;
    for (auto& Item : Owner.StorageCallbacks) if (Item && Item->Accepted && (!First || Item->Route < First->Route)) First = Item.get();
    return First && First->Ready ? First : nullptr;
}
} // namespace CarbonLuau::Runtime

ClStatus cl_domain_storage_completion(ClHandle Id, ClHandle DomainId, uint64_t VmGeneration, uint64_t Route,
    uint32_t Error, uint32_t Found, const uint8_t* Envelope, uint32_t Length) try
{
    using namespace CarbonLuau::Runtime;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || Runtime->GenerationId != VmGeneration)
        return CL_INVALID_ARGUMENT;
    Domain* Owner = GetDomain(*Runtime, DomainId, true);
    if (!Owner || !Route || Found > 1 || Error > 10 || Error == 1 || Length > CarbonLuau::Persistence::MaximumEnvelope || (Length && !Envelope))
        return CL_INVALID_ARGUMENT;
    StorageCallback* Slot = nullptr;
    for (auto& Item : Owner->StorageCallbacks) if (Item && Item->Route == Route) Slot = Item.get();
    if (!Slot || !Slot->Accepted || Slot->Ready || Runtime->Sequence == UINT64_MAX) return CL_INVALID_ARGUMENT;
    if ((Error && (Found || Length)) || (Slot->Operation != 1 && Length) ||
        (!Error && Slot->Operation == 2 && !Found) ||
        (!Error && Slot->Operation == 1 && ((Found != 0) != (Length != 0) || (Found && Length < 45)))) return CL_INVALID_ARGUMENT;
    try {
#ifdef CARBONLUAU_TESTING
        if (TestStorageCopyFailure) throw std::bad_alloc();
#endif
        if (Length) Slot->Envelope.assign(Envelope, Envelope + Length);
        Slot->Error = Error; Slot->Found = Found != 0;
        Slot->Due = NowNs(); Slot->Sequence = ++Runtime->Sequence; Slot->Ready = true;
        return CL_OK;
    } catch (const std::bad_alloc&) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    catch (...) { Retire(*Runtime); return CL_INTERNAL_ERROR; }
} catch (...) { return CL_INTERNAL_ERROR; }
