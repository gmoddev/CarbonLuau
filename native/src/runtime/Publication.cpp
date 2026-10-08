#include "RuntimeInternal.hpp"

namespace CarbonLuau::Runtime {
bool CanMutateHost(const Vm& Runtime)
{
    // A publication scope is either a provisional candidate or an executing
    // first-load module. It follows synchronous cross-domain calls, but is gone
    // before later calls to cached exports. Do not alter admission or deadline.
    return Runtime.Admission && !Runtime.Admission->Provisional && !Runtime.Publication;
}

bool CanDispatchStorage(const Vm& Runtime, const Domain& ResourceOwner)
{
    return Runtime.Owner==std::this_thread::get_id() && Runtime.State && !Runtime.IntegrityFailed &&
        ResourceOwner.Alive && ResourceOwner.Active && CanMutateHost(Runtime) &&
        Runtime.Admission->Owner==&ResourceOwner;
}

bool ReserveStorage(Vm& Runtime, Domain& ResourceOwner, uint64_t RequestId)
{
    if (!RequestId || !CanDispatchStorage(Runtime,ResourceOwner) || Runtime.StorageReserved>=128) return false;
    auto& Slots=ResourceOwner.StorageReservations;
    if (std::find(Slots.begin(),Slots.end(),RequestId)!=Slots.end()) return false;
    auto Slot=std::find(Slots.begin(),Slots.end(),0);
    if (Slot==Slots.end()) return false;
    *Slot=RequestId; ++Runtime.StorageReserved; return true;
}

bool ReleaseStorage(Vm& Runtime, Domain& ResourceOwner, uint64_t RequestId)
{
    if (!RequestId || Runtime.Owner!=std::this_thread::get_id() ||
        GetDomain(Runtime,ResourceOwner.Id)!=&ResourceOwner) return false;
    auto& Slots=ResourceOwner.StorageReservations;
    auto Slot=std::find(Slots.begin(),Slots.end(),RequestId);
    if (Slot==Slots.end()) return false;
    *Slot=0; --Runtime.StorageReserved; return true;
}

struct FacadeCallbackPublicationInput { Domain* Owner; uint32_t Operation; };
static int ControlFacadeCallbacks(lua_State* State)
{
    auto& Input = *static_cast<FacadeCallbackPublicationInput*>(lua_touserdata(State, 1));
    Domain& Owner = *Input.Owner;
    lua_getref(State, Owner.FacadeCallbackRoots);
    if (Input.Operation == 10) {
        lua_getfield(State, -1, "Roots");
        // This table contains only host-admitted Player/GUI listeners and
        // commands, bounded by their existing per-domain envelopes. Cloning
        // is charged to the same VM heap; at most one clone per touched scope.
        lua_clonetable(State, -1);
        CallbackScope Snapshot{State};
        Snapshot.Reference = lua_ref(State, -1);
        Owner.FacadeCallbackPublications.push_back(Snapshot.Reference);
        Snapshot.Reference = LUA_NOREF;
    } else if (Input.Operation == 12) {
        // Swap the private root map rather than allocating while restoring
        // individual keys. All bootstrap closures access the same holder.
        lua_getref(State, Owner.FacadeCallbackPublications.back());
        lua_setfield(State, -2, "Roots");
    }
    return 0;
}

void ClearFacadeCallbacks(Vm& Runtime, Domain& Owner)
{
    if (Runtime.State) {
        for (int Reference : Owner.FacadeCallbackPublications) lua_unref(Runtime.State, Reference);
        if (Owner.FacadeCallbackRoots != LUA_NOREF) {
            // Escaped service/Connection closures may still retain the holder
            // in another domain. Retire its owned callbacks as well as our ref.
            lua_getref(Runtime.State, Owner.FacadeCallbackRoots);
            lua_getfield(Runtime.State, -1, "Roots");
            lua_cleartable(Runtime.State, -1);
            lua_pop(Runtime.State, 2);
            lua_unref(Runtime.State, Owner.FacadeCallbackRoots);
        }
    }
    Owner.FacadeCallbackPublications.clear();
    Owner.FacadeCallbackRoots = LUA_NOREF;
}

bool ControlPublication(Vm& Runtime, Domain& Owner, uint32_t Operation)
{
    if (!Owner.Host || !Owner.HostBuffer) return true;
    bool CallbackRoots = Owner.FacadeCallbackRoots != LUA_NOREF;
    if (CallbackRoots) {
        if (Operation != 10 && Owner.FacadeCallbackPublications.empty()) {
            Runtime.IntegrityFailed = true; return false;
        }
        if (Operation == 10 || Operation == 12) {
            FacadeCallbackPublicationInput Input{&Owner, Operation};
            if (lua_cpcall(Runtime.State, ControlFacadeCallbacks, &Input) != LUA_OK) {
                Runtime.IntegrityFailed = true; return false;
            }
        }
    }
    uint32_t Written = 0;
    if (Owner.Host(Owner.HostIdentity, Operation, "", 0, Owner.HostBuffer->data(),
        uint32_t(Owner.HostBuffer->size()), &Written) != 0) {
        if (CallbackRoots && Operation == 10) {
            lua_unref(Runtime.State, Owner.FacadeCallbackPublications.back());
            Owner.FacadeCallbackPublications.pop_back();
        }
        Runtime.IntegrityFailed = true;
        return false;
    }
    if (CallbackRoots && Operation != 10) {
        lua_unref(Runtime.State, Owner.FacadeCallbackPublications.back());
        Owner.FacadeCallbackPublications.pop_back();
    }
    return true;
}

void RollbackPublication(PublicationScope& Scope)
{
    if (Scope.Storage) Scope.Storage->Alive = false;
    Scope.StorageHints.clear();
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
        for (const auto& Hint : StorageHints)
            MergeStorageHint(Parent->StorageHints, *Hint.Owner, Hint.Store, Hint.Fields);
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
    if (!Parent) for (const auto& Hint : StorageHints) if (Hint.Owner->Alive && !Runtime.IntegrityFailed) {
        bool Changed = MergeStorageHint(Hint.Owner->StorageHints, *Hint.Owner, Hint.Store, Hint.Fields);
        if (Changed && Hint.Owner->Active) {
            const auto& Union = *std::find_if(Hint.Owner->StorageHints.begin(), Hint.Owner->StorageHints.end(),
                [&](const StorageHint& Item) { return Item.Store == Hint.Store; });
            if (!PublishStorageHint(Runtime, *Hint.Owner, Union)) Runtime.IntegrityFailed = true;
        }
    }
    StorageHints.clear();
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
} // namespace CarbonLuau::Runtime
