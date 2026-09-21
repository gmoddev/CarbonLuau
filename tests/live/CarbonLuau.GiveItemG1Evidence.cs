// Isolated exact-build fixture only. Never ship with production plugins.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Carbon.Plugins
{
    [Info("CarbonLuauGiveItemG1Evidence", "gmoddev", "1.0.0")]
    [Description("Player-1F-B supported-host and separate I12 boundary evidence")]
    public sealed class CarbonLuauGiveItemG1Evidence : CarbonPlugin
    {
        private const string Prefix = "[CarbonLuau:GiveItemG1] ";
        private void Loaded() { NextFrame(Run); }
        private static void Check(bool Value, string Label)
        { if (!Value) throw new InvalidOperationException(Label); }
        private static int Count(BasePlayer Player, ItemDefinition Definition)
        {
            int Total = 0;
            foreach (ItemContainer Container in new[] { Player.inventory.containerMain, Player.inventory.containerBelt, Player.inventory.containerWear })
                foreach (Item Value in Container.itemList)
                    if (Value != null && Value.IsValid() && Value.parent == Container && Value.info == Definition && Value.amount > 0)
                        Total = checked(Total + Value.amount);
            return Total;
        }
        private static bool Accepted(Item Value, ItemContainer Container)
        { return Value.IsValid() && Value.parent == Container && Container.itemList.Contains(Value) && Value.position >= 0 && Value.position < Container.capacity && Value.GetWorldEntity() == null; }
        private static void Clean(Item Value)
        {
            Check(Value.parent == null && Value.GetWorldEntity() == null && Value.IsValid() && Value.amount > 0, "qualified temporary before cleanup");
            Value.Remove(0);
            Check(Value.IsRemoved() && Value.amount == 0 && Value.parent == null && Value.position == -1 && Value.GetWorldEntity() == null, "qualified cleanup terminal");
        }
        private void Run()
        {
            BasePlayer Player = null;
            var Returned = new List<Item>();
            const ulong Id = 76561198000000113;
            try {
                Check(BasePlayer.activePlayerList.Count == 0, "isolated zero-client server required");
                Player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new UnityEngine.Vector3(0, -100, 0)) as BasePlayer;
                Check(Player != null, "controlled Player created");
                Player.userID = Id; Player.UserIDString = Id.ToString(); Player.displayName = "CarbonLuau G1 fixture";
                Player.Spawn(); Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.net.connection = new Network.Connection { userid = Id, username = Player.displayName, connected = true, active = true, player = Player };
                BasePlayer.activePlayerLookup[Id] = Player; BasePlayer.activePlayerList.Add(Player);
                ItemContainer Main = Player.inventory.containerMain, Belt = Player.inventory.containerBelt, Wear = Player.inventory.containerWear;
                Check(!Main.HasAvailableSlotsDefined && !Belt.HasAvailableSlotsDefined && !Wear.HasAvailableSlotsDefined, "baseline excludes conflict-slot relocation");
                ItemDefinition Scrap = ItemManager.FindItemDefinition("scrap");
                Check(Scrap != null && Scrap.stackable >= 100, "scrap baseline");
                int Before = Count(Player, Scrap);
                Func<BasePlayer, Item, int, bool> Original = Main.canAcceptItem;
                foreach (int Mode in new[] { 0, 1, 2, 3 }) {
                    Main.canAcceptItem = Mode == 0 ? null : Mode == 1 ? Original : Mode == 2 ?
                        new Func<BasePlayer, Item, int, bool>((P, I, S) => true) :
                        new Func<BasePlayer, Item, int, bool>((P, I, S) => { Check(I.amount == 10 && S == Mode, "inspection callback input"); return true; });
                    Item Value = ItemManager.Create(Scrap, 10); Returned.Add(Value);
                    Check(Value.MoveToContainer(Main, Mode, false, false, Player, false), "explicit-slot acceptance mode " + Mode);
                    Check(Accepted(Value, Main), "accepted returned source " + Mode);
                }
                Main.canAcceptItem = Original;
                Check(Count(Player, Scrap) == Before + 40, "multi-chunk physical delivery");
                Item Target = Main.GetSlot(0);
                Item Merge = ItemManager.Create(Scrap, Scrap.stackable - Target.amount); Returned.Add(Merge);
                Check(Merge.MoveToContainer(Main, 0, true, false, Player, false), "exact compatible completion");
                Check(Merge.IsRemoved() && Merge.amount == 0 && Merge.parent == null && Merge.GetWorldEntity() == null && Target.amount == Scrap.stackable, "consumed source terminal");
                Item BeltItem = ItemManager.Create(Scrap, 1); Returned.Add(BeltItem);
                Check(BeltItem.MoveToContainer(Belt, 0, false, false, Player, false) && Accepted(BeltItem, Belt), "baseline belt placement");
                ItemDefinition Hat = ItemManager.FindItemDefinition("hat.cap");
                Check(Hat != null, "wearable definition");
                Item WearItem = ItemManager.Create(Hat, 1); Returned.Add(WearItem);
                Check(WearItem.MoveToContainer(Wear, 0, false, false, Player, false) && Accepted(WearItem, Wear), "empty wear placement");

                for (int Index = 0; Index < 100; ++Index) {
                    Main.canAcceptItem = (P, I, S) => false;
                    Item Rejected = ItemManager.Create(Scrap, 2);
                    Check(!Rejected.MoveToContainer(Main, 4, false, false, Player, false), "normal reject");
                    Clean(Rejected);
                }
                Main.canAcceptItem = (P, I, S) => { throw new InvalidOperationException("G1 expected accept failure"); };
                Item Thrown = ItemManager.Create(Scrap, 1); Returned.Add(Thrown);
                bool DidThrow = false;
                try { Thrown.MoveToContainer(Main, 4, false, false, Player, false); }
                catch (InvalidOperationException) { DidThrow = true; }
                Check(DidThrow, "direct callback failure propagates"); Clean(Thrown);
                Main.canAcceptItem = Original;
                Item Inserted = ItemManager.Create(Scrap, 1); Returned.Add(Inserted);
                Action<Item, bool> Changed = Main.onItemAddedRemoved;
                Main.onItemAddedRemoved = Changed + ((I, Added) => { if (Added && I == Inserted) throw new InvalidOperationException("G1 expected insertion failure"); });
                DidThrow = false;
                try { Inserted.MoveToContainer(Main, 4, false, false, Player, false); }
                catch (InvalidOperationException) { DidThrow = true; }
                finally { Main.onItemAddedRemoved = Changed; }
                Check(DidThrow && Accepted(Inserted, Main), "exceptional accepted state must not be rolled back");
                Puts(Prefix + "SUPPORTED HOST PASS explicit slots/no swap/no ignoreStackLimit; default/accept/inspect/reject; merge/multi-chunk/main/belt/wear; cleanup/exceptional insertion");

                // Deliberate external mutation. Not supported-host success evidence.
                int OriginalLimit = Main.maxStackSize;
                int Calls = 0;
                int BoundaryBefore = Count(Player, Scrap);
                Item Boundary = ItemManager.Create(Scrap, 2); Returned.Add(Boundary);
                Main.canAcceptItem = (P, I, S) => { Calls++; Main.maxStackSize = 1; return Calls == 1; };
                bool BoundaryReturn;
                try { BoundaryReturn = Boundary.MoveToContainer(Main, 5, false, false, Player, false); }
                finally { Main.maxStackSize = OriginalLimit; Main.canAcceptItem = Original; }
                Check(Calls >= 2 && BoundaryReturn && Count(Player, Scrap) < BoundaryBefore + 2 && Boundary.parent == null, "I12 lowered limit invalidates combined success");
                Clean(Boundary);
                Puts(Prefix + "I12 BOUNDARY lower maxStackSize: host true does not prove inventory delivery; source-derived split/drop exposure reproduced");

                Boundary = ItemManager.Create(Scrap, 2); Returned.Add(Boundary);
                BoundaryBefore = Count(Player, Scrap);
                Main.canAcceptItem = (P, I, S) => { I.amount = 1; return true; };
                try { BoundaryReturn = Boundary.MoveToContainer(Main, 5, false, false, Player, false); }
                finally { Main.canAcceptItem = Original; }
                Check(BoundaryReturn && Count(Player, Scrap) < BoundaryBefore + 2, "I12 amount mutation fails quantity predicate");
                Puts(Prefix + "I12 BOUNDARY Item.amount: host true fails requested-increase predicate");

                Item Occupant = ItemManager.Create(Scrap, 1); Returned.Add(Occupant);
                Boundary = ItemManager.Create(Scrap, 2); Returned.Add(Boundary);
                Main.canAcceptItem = (P, I, S) => {
                    if (I == Boundary) { Main.canAcceptItem = Original; Check(Occupant.MoveToContainer(Main, S, false, false, Player, false), "boundary occupies slot"); }
                    return true;
                };
                try { BoundaryReturn = Boundary.MoveToContainer(Main, 6, false, false, Player, false); }
                finally { Main.canAcceptItem = Original; }
                Check(!BoundaryReturn && Boundary.parent == null, "I12 slot mutation fails transfer predicate"); Clean(Boundary);
                Puts(Prefix + "I12 BOUNDARY slot occupancy: host false remains indeterminate after creation");
                Puts(Prefix + "HOST ADAPTER FIXTURE COMPLETE; production planner/facade qualification is separate");
            }
            catch (Exception Error) { PrintError(Prefix + "FAIL " + Error); }
            finally {
                if (Player != null) {
                    Player.net.connection = null; BasePlayer.activePlayerLookup.Remove(Id); BasePlayer.activePlayerList.Remove(Player);
                    foreach (Item Value in Returned) if (Value != null && Value.IsValid() && Value.parent == null && Value.GetWorldEntity() == null) Value.Remove(0);
                    if (!Player.IsDestroyed) Player.Kill();
                }
                // Fixture-only cleanup of deliberate boundary drops in this zero-client server.
                foreach (DroppedItem Entity in BaseNetworkable.serverEntities.OfType<DroppedItem>().ToArray())
                    if (!Entity.IsDestroyed) Entity.Kill();
                ItemManager.DoRemoves();
            }
        }
    }
}
