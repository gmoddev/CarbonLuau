#include "RuntimeInternal.hpp"

namespace CarbonLuau::Runtime {
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
} // namespace CarbonLuau::Runtime
