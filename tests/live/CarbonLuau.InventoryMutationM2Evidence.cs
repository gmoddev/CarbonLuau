// EXCLUDED from production packages. Inventory-M2 exact-host evidence only.
using System;
using System.Linq;

namespace Carbon.Plugins
{
    [Info("CarbonLuauInventoryMutationM2Evidence", "gmoddev", "1.0.0")]
    [Description("Inventory-M2 exact-build mutation and ownership evidence fixture")]
    public sealed class CarbonLuauInventoryMutationM2Evidence : CarbonPlugin
    {
        private const string Prefix = "[CarbonLuau:InventoryM2] ";
        private bool Started;

        private void Loaded() { NextFrame(StartFixture); }
        private void OnServerInitialized() { NextFrame(StartFixture); }

        private static void Check(bool Value, string Message)
        {
            if (!Value) throw new InvalidOperationException(Message);
        }

        private static int PhysicalCount(BasePlayer Player, ItemDefinition Definition)
        {
            int Total = 0;
            ItemContainer[] Containers = {
                Player.inventory.containerMain,
                Player.inventory.containerBelt,
                Player.inventory.containerWear
            };
            foreach (ItemContainer Container in Containers)
            {
                Check(Container != null && Container.itemList != null, "accepted container unavailable");
                foreach (Item Item in Container.itemList)
                    if (Item != null && Item.IsValid() && Item.info == Definition && Item.parent == Container)
                        Total = checked(Total + Item.amount);
            }
            return Total;
        }

        private void StartFixture()
        {
            if (Started) return;
            Started = true;
            BasePlayer Player = null;
            const ulong Id = 76561198000000013;
            try
            {
                Check(BasePlayer.activePlayerList.Count == 0, "fixture requires an isolated server with zero clients");
                Player = GameManager.server.CreateEntity(
                    "assets/prefabs/player/player.prefab",
                    new UnityEngine.Vector3(0f, -100f, 0f)) as BasePlayer;
                Check(Player != null, "create controlled BasePlayer");
                Player.userID = Id;
                Player.UserIDString = Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Player.displayName = "CarbonLuau Inventory-M2 fixture";
                Player.Spawn();
                Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.net.connection = new Network.Connection {
                    userid = Id,
                    username = Player.displayName,
                    connected = true,
                    active = true,
                    player = Player
                };
                BasePlayer.activePlayerLookup[Id] = Player;
                BasePlayer.activePlayerList.Add(Player);

                Check(Player.inventory.containerMain.capacity == 24, "vanilla main capacity");
                Check(Player.inventory.containerBelt.capacity == 6, "vanilla belt capacity");
                Check(Player.inventory.containerWear.capacity == 8, "vanilla wear capacity");
                Check(Player.inventory.containerMain.capacity + Player.inventory.containerBelt.capacity +
                    Player.inventory.containerWear.capacity == 38, "vanilla accepted-container bound");

                ItemDefinition Scrap = ItemManager.FindItemDefinition("scrap");
                Check(Scrap != null && Scrap.stackable > 35, "scrap definition and stack capacity");
                int Before = PhysicalCount(Player, Scrap);

                Item First = ItemManager.Create(Scrap, 25);
                Check(First != null && First.parent == null && First.GetWorldEntity() == null &&
                    First.IsValid() && First.amount == 25, "Create returned an observable unattached Item");
                Check(First.MoveToContainer(Player.inventory.containerMain, 0, false, false, Player, false),
                    "explicit empty-slot no-swap transfer");
                Check(First.parent == Player.inventory.containerMain &&
                    Player.inventory.containerMain.itemList.Contains(First) && First.position == 0 &&
                    First.GetWorldEntity() == null && First.IsValid(), "accepted inventory terminal state");
                Check(PhysicalCount(Player, Scrap) == Before + 25, "empty-slot physical delta");

                Item Merge = ItemManager.Create(Scrap, 10);
                Check(Merge != null && Merge.MoveToContainer(
                    Player.inventory.containerMain, 0, true, false, Player, false),
                    "explicit exact-fit stack merge");
                Check(Merge.parent == null && Merge.GetWorldEntity() == null && Merge.IsRemoved() &&
                    Merge.amount == 0 && First.amount == 35, "consumed stack-merge terminal state");
                Check(PhysicalCount(Player, Scrap) == Before + 35, "stack-merge physical delta");
                ItemManager.DoRemoves();

                Item Rejected = ItemManager.Create(Scrap, 1);
                Check(Rejected != null, "create rejection fixture");
                Func<BasePlayer, Item, int, bool> OriginalAccept = Player.inventory.containerMain.canAcceptItem;
                Player.inventory.containerMain.canAcceptItem = (TargetPlayer, Item, Position) => false;
                try
                {
                    Check(!Rejected.MoveToContainer(
                        Player.inventory.containerMain, 1, false, false, Player, false),
                        "controlled host rejection");
                }
                finally { Player.inventory.containerMain.canAcceptItem = OriginalAccept; }
                Check(Rejected.parent == null && Rejected.GetWorldEntity() == null &&
                    Rejected.IsValid() && Rejected.amount == 1, "temporary-responsibility state after host false");
                Rejected.Remove();
                Check(Rejected.parent == null && Rejected.GetWorldEntity() == null && Rejected.IsRemoved() &&
                    Rejected.amount == 0 && Rejected.position == -1, "supported cleanup terminal state");
                ItemManager.DoRemoves();

                Item CallbackItem = ItemManager.Create(Scrap, 2);
                Check(CallbackItem != null, "create callback fixture");
                Action<Item, bool> OriginalChanged = Player.inventory.containerMain.onItemAddedRemoved;
                Player.inventory.containerMain.onItemAddedRemoved = OriginalChanged + ((Item, Added) => {
                    if (Added && Item == CallbackItem) throw new InvalidOperationException("expected Inventory-M2 callback failure");
                });
                bool CallbackThrew = false;
                try
                {
                    CallbackItem.MoveToContainer(Player.inventory.containerMain, 1, false, false, Player, false);
                }
                catch (InvalidOperationException Error)
                {
                    CallbackThrew = Error.Message == "expected Inventory-M2 callback failure";
                }
                finally { Player.inventory.containerMain.onItemAddedRemoved = OriginalChanged; }
                Check(CallbackThrew, "post-insertion callback failure propagated");
                Check(CallbackItem.parent == Player.inventory.containerMain &&
                    Player.inventory.containerMain.itemList.Contains(CallbackItem) &&
                    CallbackItem.position == 1 && CallbackItem.GetWorldEntity() == null &&
                    CallbackItem.IsValid(), "callback-failure accepted state remains observable");
                Check(PhysicalCount(Player, Scrap) == Before + 37, "callback-failure physical state");

                int TakeBefore = PhysicalCount(Player, Scrap);
                int Removed = Player.inventory.Take(null, Scrap.itemid, 30);
                int TakeAfter = PhysicalCount(Player, Scrap);
                Check(Removed == 30 && TakeBefore - TakeAfter == 30,
                    "Take host result and physical delta");
                Check(TakeAfter == Before + 7, "Take main-to-belt-to-wear accepted inventory result");

                Puts(Prefix + "PASS G1 explicit-slot/no-swap path produced no world entity or drop");
                Puts(Prefix + "PASS G2 accepted, consumed, temporary-responsibility and exceptional accepted states observable");
                Puts(Prefix + "PASS G3 Take returned amount matched bounded physical delta");
                Puts(Prefix + "PASS G4 Remove cleanup reached scheduled/zeroed/unattached terminal state");
                Puts(Prefix + "PASS G5 vanilla capacities main=24 belt=6 wear=8 total=38");
                Puts(Prefix + "LIVE CARBON PASS");
            }
            catch (Exception Error)
            {
                PrintError(Prefix + "FAIL " + Error);
            }
            finally
            {
                if (Player != null)
                {
                    Player.net.connection = null;
                    BasePlayer.activePlayerLookup.Remove(Id);
                    BasePlayer.activePlayerList.Remove(Player);
                    if (!Player.IsDestroyed) Player.Kill();
                }
            }
        }
    }
}
