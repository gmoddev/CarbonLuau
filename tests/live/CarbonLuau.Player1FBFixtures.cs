// Isolated fixture-only partial. Never include in production packages.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private static void CheckGrant(bool Value, string Message)
        { if (!Value) throw new InvalidOperationException(Message); }
        private static void GrantError(Action Action)
        {
            try { Action(); } catch (FacadeException Error) {
                CheckGrant(Error.Message.Contains("may have changed"), "post-COMMIT error classification"); return;
            }
            throw new InvalidOperationException("expected indeterminate grant");
        }
        [ConsoleCommand("carbonluau.player1fbfixture"), AuthLevel(2)]
        private void Player1FBFixture(ConsoleSystem.Arg Arg)
        {
            if (Host == null || !Host.Ready || BasePlayer.activePlayerList.Count != 0) {Arg.ReplyWith("isolated zero-client server required"); return;}
            NextFrame(RunPlayer1FBFixture);
        }
        private void RunPlayer1FBFixture()
        {
            const ulong Id = 76561198000000114;
            const string Prefix = "[CarbonLuau:Player1FBLive] ";
            BasePlayer Player = null;
            try {
                Player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new UnityEngine.Vector3(0, -100, 0)) as BasePlayer;
                CheckGrant(Player != null, "controlled host Player");
                Player.userID = Id; Player.UserIDString = Id.ToString(); Player.displayName = "CarbonLuau Give fixture";
                Player.Spawn(); Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.net.connection = new Network.Connection {userid = Id, username = Player.displayName, connected = true, active = true, player = Player};
                BasePlayer.activePlayerLookup[Id] = Player; BasePlayer.activePlayerList.Add(Player); OnPlayerConnected(Player);
                PlayerLifetime Lifetime = Gameplay.Players.Find(Player.UserIDString);
                CheckGrant(Lifetime != null, "D11 connection available");
                ItemDefinition Scrap = ItemManager.FindItemDefinition("scrap"), Hat = ItemManager.FindItemDefinition("hat.cap");
                Func<PlayerView> Resolve = () => Gameplay.Players.Resolve(Lifetime.Token, Player.UserIDString);
                Func<ItemDefinition, int, bool> Give = (Definition, Amount) => Gameplay.GiveItems.Execute(Lifetime.Token, Resolve, Definition, Amount);
                Func<ItemDefinition, long> Count = Definition => PhysicalInventoryObservation.CountForMutation(ViewInventory(Player), Definition);
                ItemContainer Main = Player.inventory.containerMain, Belt = Player.inventory.containerBelt, Wear = Player.inventory.containerWear;
                Func<BasePlayer, Item, int, bool> Original = Main.canAcceptItem;
                CheckGrant(Give(Scrap, 2) && Count(Scrap) == 2, "empty production grant");
                CheckGrant(Give(Scrap, Scrap.stackable - 2) && Main.GetSlot(0).amount == Scrap.stackable, "consumed merge source");
                CheckGrant(Give(Scrap, Scrap.stackable + 1) && Count(Scrap) == 2L * Scrap.stackable + 1, "multi chunk");
                foreach (int Mode in new[] {0, 1, 2}) {
                    Main.canAcceptItem = Mode == 0 ? null : Mode == 1 ? new Func<BasePlayer, Item, int, bool>((P, I, S) => true) :
                        new Func<BasePlayer, Item, int, bool>((P, I, S) => {CheckGrant(I.amount == 1, "inspection-only callback"); return true;});
                    CheckGrant(Give(Scrap, 1), "supported callback " + Mode);
                }
                Main.canAcceptItem = (P, I, S) => false;
                for (int Index = 0; Index < 100; ++Index) GrantError(() => Give(Scrap, 1));
                Main.canAcceptItem = (P, I, S) => { throw new InvalidOperationException("controlled acceptance failure"); };
                GrantError(() => Give(Scrap, 1));
                Main.canAcceptItem = Original;
                CheckGrant(Gameplay.GiveItems.CleanupFailures == 0 && Gameplay.GiveItems.UnaccountedResources == 0, "normal rejection and exception cleanup qualified");
                for (int Index = 0; Index < 100; ++Index) {
                    CheckGrant(Give(Scrap, 1), "repeated grant");
                    CheckGrant(Gameplay.TakeItems.Execute(Lifetime.Token, Resolve, Scrap, 1), "paired Take regression");
                }
                Main.canAcceptItem = (P, I, S) => {
                    bool GiveBusy = false, TakeBusy = false;
                    try {Give(Scrap, 1);} catch (FacadeException Error) {GiveBusy = Error.Message.Contains("already in progress");}
                    try {Gameplay.TakeItems.Execute(Lifetime.Token, Resolve, Scrap, 1);} catch (FacadeException Error) {TakeBusy = Error.Message.Contains("already in progress");}
                    CheckGrant(GiveBusy && TakeBusy, "live recursive shared gate"); return true;
                };
                CheckGrant(Give(Scrap, 1), "outer grant after recursion rejection"); Main.canAcceptItem = Original;
                // Fill all planner-accepted scrap storage; no wear acceptance for scrap.
                long Space = (long)(Main.capacity + Belt.capacity) * Scrap.stackable - Count(Scrap);
                CheckGrant(Give(Scrap, (int)Space), "fill main and belt completely");
                long Full = Count(Scrap);
                for (int Index = 0; Index < 100; ++Index) CheckGrant(!Give(Scrap, 1), "full PREPARE false");
                CheckGrant(Count(Scrap) == Full && Give(Hat, 1) && Count(Hat) == 1, "full unchanged and safe wear slot grant");
                CheckGrant(!Give(Hat, 1), "conflicting clothing rejected before host relocation");
                // Remove exact fixture-owned inventory before independent I12 cases.
                foreach (Item Value in Main.itemList.ToArray()) {Value.RemoveFromContainer(); Value.Remove(0);}
                foreach (Item Value in Belt.itemList.ToArray()) {Value.RemoveFromContainer(); Value.Remove(0);}
                int OriginalLimit = Main.maxStackSize;
                int Calls = 0;
                Main.canAcceptItem = (P, I, S) => { if (++Calls == 1) {Main.maxStackSize = 1; return true;} return false; };
                GrantError(() => Give(Scrap, 2)); Main.maxStackSize = OriginalLimit; Main.canAcceptItem = Original;
                Puts(Prefix + "I12 BOUNDARY lower stack limit: indeterminate, not supported-host PASS");
                Main.canAcceptItem = (P, I, S) => {I.amount = 1; return true;};
                GrantError(() => Give(Scrap, 2)); Main.canAcceptItem = Original;
                Puts(Prefix + "I12 BOUNDARY changed amount: physical predicate rejected success");
                // A trusted callback changes the exact planned slot after the adapter guard.
                Main.canAcceptItem = (P, I, S) => {
                    Main.canAcceptItem = Original;
                    Item Previous = Main.GetSlot(S);
                    if (Previous != null) {Previous.RemoveFromContainer(); Previous.Remove(0);}
                    Item Blocker = ItemManager.Create(ItemManager.FindItemDefinition("rock"), 1);
                    CheckGrant(Blocker.MoveToContainer(Main, S, false, false, Player, false), "boundary slot blocker");
                    return true;
                };
                GrantError(() => Give(Scrap, 2)); Main.canAcceptItem = Original;
                Puts(Prefix + "I12 BOUNDARY changed slot occupancy: indeterminate and qualified temporary cleanup");
                string PSource = "local P=game:GetService('Players'):GetPlayerByUserId('" + Id + "'); ";
                var Result = Host.Execute("give.public", PSource + "assert(P:GiveItem('scrap',1,GiveItemBehavior.InventoryOnly)); Saved=P");
                CheckGrant(Result.Status == RuntimeStatus.OK, "public API real host: " + Result.Error);
                OnPlayerDisconnected(Player, "fixture reconnect");
                Player.net.connection = new Network.Connection {userid = Id, username = Player.displayName, connected = true, active = true, player = Player};
                OnPlayerConnected(Player);
                Result = Host.Execute("give.stale", "assert(not pcall(function() Saved:GiveItem('scrap',1) end))");
                CheckGrant(Result.Status == RuntimeStatus.OK, "stale proxy after live reconnect");
                Result = Host.Reload(PSource + "assert(not pcall(function() P:GiveItem('scrap',1) end)); task.defer(function() assert(P:GiveItem('scrap',1)); print('deferred-host-grant') end)");
                CheckGrant(Result.Status == RuntimeStatus.OK, "provisional live rejection");
                for (int Frame = 0; Frame < 30 && Host.HasWork; ++Frame) foreach (ExecutionResult Work in Host.Drain()) CheckGrant(Work.Status == RuntimeStatus.OK, Work.Error);
                long Before = Count(Scrap);
                CheckGrant(Host.Reload(PSource + "task.defer(function() P:GiveItem('scrap',1) end); error('failed candidate')").Status != RuntimeStatus.OK, "failed candidate");
                CheckGrant(Count(Scrap) == Before && Gameplay.GiveItems.BusyCount == 0 && Gameplay.GiveItems.TrackedResources == 0, "no candidate grant/no gate/reference leak");
                Puts(Prefix + "PASS production planner/adapter/VERIFY, main/belt/wear, full, callbacks/cleanup, 300 repeated operations, Give/Take gate, public API/reconnect/provisional/failed reload; no real client");
            } catch (Exception Error) {PrintError(Prefix + "FAIL " + Error);}
            finally {
                if (Player != null) {
                    OnPlayerDisconnected(Player, "fixture cleanup"); Player.net.connection = null;
                    BasePlayer.activePlayerLookup.Remove(Id); BasePlayer.activePlayerList.Remove(Player); Player.Kill();
                }
                // Fixture-only drain/world cleanup in this isolated zero-client server.
                foreach (DroppedItem Entity in BaseNetworkable.serverEntities.OfType<DroppedItem>().ToArray()) Entity.Kill();
                ItemManager.DoRemoves();
            }
        }
    }
}
