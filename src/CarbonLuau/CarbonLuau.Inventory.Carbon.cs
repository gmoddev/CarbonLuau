using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Exact-build adaptation only. All references are managed/host-side, operation-local.
        private sealed class RustInventoryGrantHost : IInventoryGrantHost
        {
            private readonly BasePlayer Player;
            internal RustInventoryGrantHost(BasePlayer Player) { this.Player = Player; }
            private bool Accepted(ItemContainer Container)
            {
                return Player != null && Player.inventory != null && Container != null &&
                    (Object.ReferenceEquals(Container, Player.inventory.containerMain) ||
                     Object.ReferenceEquals(Container, Player.inventory.containerBelt) ||
                     Object.ReferenceEquals(Container, Player.inventory.containerWear));
            }
            public int StackLimit(object Definition, PhysicalInventoryContainer Container)
            {
                var Target = (ItemContainer)Container.Identity;
                if (!Accepted(Target) || Target.HasAvailableSlotsDefined) return 0;
                int Limit = ((ItemDefinition)Definition).stackable;
                if (Target.maxStackSize > 0) Limit = Math.Min(Limit, Target.maxStackSize);
                // Never plan multiple copies of the same clothing/restricted item in one container.
                if (Object.ReferenceEquals(Target, Player.inventory.containerWear)) Limit = Math.Min(Limit, 1);
                return Math.Max(0, Limit);
            }
            public int MergeSpace(object Definition, PhysicalInventoryStack Stack, PhysicalInventoryContainer Container)
            {
                var Value = Stack.Identity as Item;
                var Info = (ItemDefinition)Definition;
                // Conservative default-item subset. Complex stacks remain occupied, not guessed compatible.
                if (Value == null || !Stack.Valid || Stack.Amount <= 0 || !Object.ReferenceEquals(Stack.Definition, Definition) ||
                    Value.skin != 0 || !String.IsNullOrEmpty(Value.name) || Value.iconImageId != 0 ||
                    Value.instanceData != null || Value.IsBlueprint() || Info.amountType == ItemDefinition.AmountType.Genetics ||
                    (Value.hasCondition && Value.condition != Info.condition.max)) return 0;
                return (int)Math.Max(0L, (long)StackLimit(Definition, Container) - Stack.Amount);
            }
            public bool CanPlace(object Definition, PhysicalInventoryContainer Container, int Slot)
            {
                var Target = (ItemContainer)Container.Identity;
                var Info = (ItemDefinition)Definition;
                if (!Accepted(Target) || Target.HasAvailableSlotsDefined || Slot < 0 || Slot >= Target.capacity ||
                    Target.itemList == null || Target.itemList.Count > FacadePolicy.InventoryStacks) return false;
                bool Belt = Object.ReferenceEquals(Target, Player.inventory.containerBelt);
                bool Wear = Object.ReferenceEquals(Target, Player.inventory.containerWear);
                if (!Belt && !Wear) return true;
                if (Belt && ((Info.flags & ItemDefinition.Flag.NotAllowedInBelt) != 0 || Player.IsRestrained)) return false;
                var Restriction = Belt ? Info.GetComponent<ItemModContainerRestriction>() : null;
                var Clothing = Wear ? Info.GetComponent<ItemModWearable>() : null;
                // Backpack/parachute wear has additional side effects and is not a fallback destination.
                if (Wear && (Clothing == null || (Info.flags & ItemDefinition.Flag.Backpack) != 0 || Slot == 7 ||
                    Info.GetComponent<ItemModParachute>() != null || Clothing.npcOnly ||
                    (Clothing.preventsMounting && Player.isMounted))) return false;
                foreach (Item Existing in Target.itemList) {
                    if (Existing == null || Existing.info == null) return false;
                    if (Restriction != null && !Restriction.CanExistWith(Existing.info.GetComponent<ItemModContainerRestriction>())) return false;
                    if (Clothing != null && !Clothing.CanExistWith(Existing.info.GetComponent<ItemModWearable>())) return false;
                }
                // Reserve only the first empty slot for clothing/restriction-bearing definitions.
                // This prevents two planned chunks from conflicting with each other after PREPARE.
                if (Clothing != null || Restriction != null) {
                    for (int Index = 0; Index < Target.capacity; ++Index) {
                        if (Wear && Index == 7) continue;
                        if (Target.GetSlot(Index) == null) return Slot == Index;
                    }
                    return false;
                }
                return true;
            }
            public object Create(object Definition, int Amount)
            { return ItemManager.Create((ItemDefinition)Definition, Amount, 0); }
            public bool Transfer(object Resource, object Definition, InventoryPlacementChunk Chunk)
            {
                var Value = (Item)Resource;
                var Target = (ItemContainer)Chunk.Container.Identity;
                // Recheck observed premises after callback-capable creation, before selecting the adapter.
                PhysicalInventoryObservation.ValidateMutationCapacity(ViewInventory(Player));
                if (!Value.IsValid() || Value.parent != null || Value.GetWorldEntity() != null ||
                    !Object.ReferenceEquals(Value.info, Definition) || Value.amount != Chunk.Amount ||
                    Chunk.Amount > StackLimit(Definition, Chunk.Container) ||
                    !CanPlace(Definition, Chunk.Container, Chunk.Slot)) throw new InvalidOperationException("grant premises changed");
                Item Occupant = Target.GetSlot(Chunk.Slot);
                if (!Object.ReferenceEquals(Occupant, Chunk.MergeTarget)) throw new InvalidOperationException("grant slot changed");
                bool AllowStack = Chunk.MergeTarget != null;
                if (AllowStack && (!Occupant.CanStack(Value) ||
                    (long)Occupant.amount + Chunk.Amount > StackLimit(Definition, Chunk.Container)))
                    throw new InvalidOperationException("grant merge changed");
                return Value.MoveToContainer(Target, Chunk.Slot, AllowStack, false, Player, false);
            }
            public InventoryResourceObservation Observe(object Resource)
            {
                var Value = (Item)Resource;
                var Result = new InventoryResourceObservation { Identity = Value.uid.Value, Amount = Value.amount };
                if (Value.GetWorldEntity() != null) return Result;
                if (Value.parent == null) {
                    if (Value.IsValid() && Value.amount > 0) Result.State = InventoryResourceState.Temporary;
                    else if (!Value.IsValid() && Value.amount == 0 && Value.position == -1) Result.State = InventoryResourceState.Consumed;
                    return Result;
                }
                ItemContainer Parent = Value.parent;
                if (!Value.IsValid() || Value.amount <= 0 || !Accepted(Parent) || Parent.capacity < 0 ||
                    Parent.capacity > FacadePolicy.InventoryStacks || Parent.itemList == null ||
                    Parent.itemList.Count > FacadePolicy.InventoryStacks || Value.position < 0 || Value.position >= Parent.capacity) return Result;
                if (Object.ReferenceEquals(Parent.GetSlot(Value.position), Value) && Parent.itemList.Contains(Value))
                    Result.State = InventoryResourceState.Accepted;
                return Result;
            }
            public void Cleanup(object Resource)
            {
                var Value = (Item)Resource;
                if (Observe(Value).State != InventoryResourceState.Temporary) throw new InvalidOperationException("unsafe cleanup state");
                Value.Remove(0);
            }
        }
    }
}
