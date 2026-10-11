// Isolated C2 sampled candidate only; never include in production packages.
// Implements the private C2 startup seam; package this fixture EXCLUSIVELY.
// Controlled human/D11 connection, not an authenticated Rust client.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string C2Prefix = "[CarbonLuau:InventoryNetChangeFixture] ";
        private MethodInfo C2LoadedIsValid, C2LoadedIsRemoved;
        partial void RunInventoryNetChangeFixtures() { NextTick(RunC2InventoryFixture); }

        private static void C2Check(bool Value, string Message)
        { if (!Value) throw new InvalidOperationException(Message); }

        private static string C2ReceiptText(string Value)
        {
            Value = Value ?? "<null>";
            C2Check(Value.Length <= 1024, "loaded receipt text bound");
            return Value.Replace('\r', ' ').Replace('\n', ' ');
        }

        private static void C2ReadPin(MethodInfo Method, int Token, int Length, string ExpectedHash)
        {
            C2Check(Method != null && Method.MetadataToken == Token && !Method.IsStatic && !Method.IsVirtual &&
                Method.ReturnType == typeof(bool) && Method.GetParameters().Length == 0, "exact loaded read method identity");
            MethodBody Body = Method.GetMethodBody();
            C2Check(Body != null && Body.ExceptionHandlingClauses.Count == 0 && Body.LocalVariables.Count == 0,
                "pinned read method has no EH/locals");
            byte[] IL = Body.GetILAsByteArray();
            C2Check(IL != null && IL.Length == Length, "pinned read method IL length");
            string Hash;
            using (SHA256 Digest = SHA256.Create())
                Hash = BitConverter.ToString(Digest.ComputeHash(IL)).Replace("-", "").ToLowerInvariant();
            C2Check(Hash == ExpectedHash, "loaded read body hash drift: " + Method.Name);
            // Restricted decoding of these two tiny, hash-pinned bodies only.
            // Skip field/constant operands rather than mistaking their bytes for
            // opcodes. Every admitted instruction is call-free.
            int Offset = 0;
            while (Offset < IL.Length) {
                byte Code = IL[Offset++];
                if (Code == 0x02 || Code == 0x16 || Code == 0x17 || Code == 0x2a) continue;
                if (Code == 0x7b || Code == 0x22) Offset += 4; // ldfld / ldc.r4
                else if (Code == 0x36) Offset++; // ble.un.s
                else if (Code == 0xfe) {
                    C2Check(Offset < IL.Length && IL[Offset++] == 0x02, "pinned read cgt instruction");
                } else C2Check(false, "unexpected/callback-capable pinned read opcode");
                C2Check(Offset <= IL.Length, "pinned read operand extent");
            }
            var Info = Harmony.GetPatchInfo(Method);
            // No owner allowlist: every effective Harmony patch category must
            // be empty, including transpilers and return-value postfixes.
            C2Check(Info == null || (Info.Prefixes.Count == 0 && Info.Postfixes.Count == 0 &&
                Info.Transpilers.Count == 0 && Info.Finalizers.Count == 0), "loaded read Harmony topology drift: " + Method.Name);
        }

        private void C2ReadPath()
        {
            C2ReadPin(C2LoadedIsValid, 0x06006a4a, 17,
                "477480dbc895bfd4d0b69e6660104e5c98b23c6bebd9ae0c01182382da848951");
            C2ReadPin(C2LoadedIsRemoved, 0x06006a3b, 14,
                "d80e2530bb0ad5c9dd617a1e19ac1370be37b6e7e0c4416b3f653645ca058a38");
        }

        private void C2PatchReceipts(MethodInfo Method, string Kind, IEnumerable<Patch> Patches, ref int Total)
        {
            int Count = 0;
            foreach (Patch Value in Patches) {
                C2Check(++Count <= 64 && ++Total <= 256, "loaded patch receipt bound");
                C2Check(Value.PatchMethod != null, "loaded patch method available");
                Puts(C2Prefix + "LOADED PATCH target=" + Method.DeclaringType.FullName + "." + Method.Name +
                    " token=0x" + Method.MetadataToken.ToString("x8") + " kind=" + Kind +
                    " owner=" + C2ReceiptText(Value.owner) + " priority=" + Value.priority +
                    " method=" + C2ReceiptText((Value.PatchMethod.DeclaringType == null ? "<dynamic>" :
                        Value.PatchMethod.DeclaringType.FullName) + "." + Value.PatchMethod) +
                    " patchMVID=" + Value.PatchMethod.Module.ModuleVersionId);
            }
        }

        private void C2LoadedHostReceipts()
        {
            Type[] Types = { typeof(ItemContainer), typeof(Item), typeof(PlayerInventory) };
            string[][] Names = {
                new[] { "Insert", "Remove", "Take" },
                new[] { "MoveToContainer", "UseItem", "Take", "Remove", "SplitItem", "IsValid", "IsRemoved" },
                new[] { "Take" }
            };
            int Selected = 0, Patches = 0, Bytes = 0;
            for (int TypeIndex = 0; TypeIndex < Types.Length; ++TypeIndex) {
                Type Target = Types[TypeIndex];
                MethodInfo[] Methods = Target.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                C2Check(Methods.Length <= 1024, "cold loaded method count bound: " + Target.FullName);
                Array.Sort(Methods, (Left, Right) => Left.MetadataToken.CompareTo(Right.MetadataToken));
                foreach (string Name in Names[TypeIndex]) {
                    int Matches = 0;
                    foreach (MethodInfo Method in Methods) {
                        if (Method.Name != Name) continue;
                        Matches++; C2Check(++Selected <= 64, "selected loaded method receipt bound");
                        MethodBody Body = Method.GetMethodBody();
                        C2Check(Body != null, "loaded method IL available: " + Target.FullName + "." + Name);
                        byte[] IL = Body.GetILAsByteArray();
                        C2Check(IL != null && IL.Length <= 262144, "individual loaded IL extent bound");
                        Bytes = checked(Bytes + IL.Length); C2Check(Bytes <= 1048576, "total loaded IL extent bound");
                        string Hash;
                        using (SHA256 Digest = SHA256.Create())
                            Hash = BitConverter.ToString(Digest.ComputeHash(IL)).Replace("-", "").ToLowerInvariant();
                        var Info = Harmony.GetPatchInfo(Method);
                        // GetMethodBody is this loaded module's reflected IL,
                        // not Harmony's generated replacement/detour body. Its
                        // current patch topology is reported independently.
                        Puts(C2Prefix + "LOADED METHOD type=" + Target.FullName + " signature=" + C2ReceiptText(Method.ToString()) +
                            " token=0x" + Method.MetadataToken.ToString("x8") + " bodySHA256=" + Hash +
                            " MVID=" + Method.Module.ModuleVersionId + " ILBytes=" + IL.Length +
                            " ILSource=loaded-reflection-original prefixes=" + (Info == null ? 0 : Info.Prefixes.Count) +
                            " postfixes=" + (Info == null ? 0 : Info.Postfixes.Count) +
                            " transpilers=" + (Info == null ? 0 : Info.Transpilers.Count) +
                            " finalizers=" + (Info == null ? 0 : Info.Finalizers.Count));
                        if (Info != null) {
                            C2PatchReceipts(Method, "prefix", Info.Prefixes, ref Patches);
                            C2PatchReceipts(Method, "postfix", Info.Postfixes, ref Patches);
                            C2PatchReceipts(Method, "transpiler", Info.Transpilers, ref Patches);
                            C2PatchReceipts(Method, "finalizer", Info.Finalizers, ref Patches);
                        }
                        if (Target == typeof(Item) && Name == "IsValid" && !Method.IsStatic && Method.GetParameters().Length == 0) {
                            C2Check(C2LoadedIsValid == null, "unique loaded no-argument Item.IsValid");
                            C2LoadedIsValid = Method;
                        }
                        if (Target == typeof(Item) && Name == "IsRemoved" && !Method.IsStatic && Method.GetParameters().Length == 0) {
                            C2Check(C2LoadedIsRemoved == null, "unique loaded no-argument Item.IsRemoved");
                            C2LoadedIsRemoved = Method;
                        }
                    }
                    if (Matches == 0) {
                        Puts(C2Prefix + "LOADED ABSENT type=" + Target.FullName + " method=" + Name);
                        // Some builds have no Item.Take; ItemContainer.Take and
                        // PlayerInventory.Take are separate, mandatory receipts.
                        C2Check(Target == typeof(Item) && Name == "Take", "required loaded method absent");
                    }
                }
            }
            FieldInfo Changed = typeof(ItemContainer).GetField("onItemAddedRemoved",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            C2Check(Changed != null && Changed.FieldType == typeof(Action<Item, bool, BasePlayer>), "exact loaded container callback signature");
            Puts(C2Prefix + "LOADED FIELD ItemContainer.onItemAddedRemoved type=" + Changed.FieldType +
                " token=0x" + Changed.MetadataToken.ToString("x8") + " MVID=" + Changed.Module.ModuleVersionId);
            C2ReadPath();
            Puts(C2Prefix + "PASS loaded IsValid/IsRemoved exact tokens/body hashes, no EH/locals/calls and zero Harmony patches");
            Puts(C2Prefix + "PASS loaded IL/topology receipts and Item.IsValid prefixes=0 before physical tests");
        }

        private InventoryQuantitySnapshot C2Snapshot(PlayerLifetime Lifetime)
        {
            C2ReadPath();
            PlayerView View = Gameplay.Players.Resolve(Lifetime.Token, Lifetime.UserId);
            if (View == null) throw new FacadeException("stale C2 Player connection");
            var Source = ViewInventory((BasePlayer)View.Identity);
            InventoryQuantitySnapshot Result = PhysicalInventoryObservation.Snapshot(Source,
                Definition => ((ItemDefinition)Definition).shortname, ReadItemDefinition);
            C2Check(Result.Length <= FacadePolicy.InventoryStacks, "bounded snapshot names");
            foreach (string Name in new[] { "scrap", "hat.cap" })
                C2Check(Result.Count(Name) == PhysicalInventoryObservation.Count(Source, ItemManager.FindItemDefinition(Name)),
                    "D18 physical count parity: " + Name);
            C2ReadPath();
            return Result;
        }

        private static void C2Delta(InventoryQuantitySnapshot Before, InventoryQuantitySnapshot After,
            string Name, long Old, long Next, string Label)
        {
            C2Check(Before.Count(Name) == Old && After.Count(Name) == Next, Label + " snapshot quantities");
            InventoryQuantityChange[] Changes = InventoryQuantitySnapshot.Difference(Before, After);
            C2Check(Changes.Length == 1, Label + " exactly one changed shortname");
            C2Check(Changes[0].Item == Name && Changes[0].Before == Old && Changes[0].After == Next &&
                Changes[0].Delta == Next - Old, Label + " Item/Before/After/Delta");
        }

        private static void C2Zero(InventoryQuantitySnapshot Before, InventoryQuantitySnapshot After, string Label)
        { C2Check(InventoryQuantitySnapshot.Difference(Before, After).Length == 0, Label + " zero net change"); }

        private static Item C2Item(List<Item> Temporary, ItemDefinition Definition, int Amount)
        {
            Item Value = ItemManager.Create(Definition, Amount);
            C2Check(Value != null, "ItemManager.Create returned fixture item");
            Temporary.Add(Value);
            C2Check(Value.IsValid() && Value.parent == null && Value.GetWorldEntity() == null && Value.amount == Amount,
                "created item temporary ownership");
            return Value;
        }

        private static void C2Move(Item Value, ItemContainer Target, int Slot, bool Stack, BasePlayer Player)
        {
            C2Check(Value.MoveToContainer(Target, Slot, Stack, false, Player, false), "exact-slot MoveToContainer");
            if (Value.IsRemoved()) {
                C2Check(Stack && !Value.IsValid() && Value.amount == 0 && Value.parent == null && Value.GetWorldEntity() == null,
                    "consumed merge source terminal state");
            } else {
                C2Check(Value.IsValid() && ReferenceEquals(Value.parent, Target) && Target.itemList.Contains(Value) &&
                    Value.position == Slot && Value.GetWorldEntity() == null, "reciprocal accepted inventory terminal state");
            }
        }

        private void RunC2InventoryFixture()
        {
            const ulong Id = 76561198000000114;
            BasePlayer Player = null;
            BaseEntity DroppedWorld = null;
            var Temporary = new List<Item>();
            bool RegistryScheduled = false;
            try {
                C2Check(BasePlayer.activePlayerList.Count == 0 && Host != null && Host.Ready,
                    "isolated ready zero-client server required");
                C2LoadedHostReceipts();
                C2Check(!BasePlayer.activePlayerLookup.ContainsKey(Id), "controlled identity unused");
                Player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new Vector3(0, -100, 0)) as BasePlayer;
                C2Check(Player != null, "controlled Player creation");
                Player.enableSaving = false;
                Player.userID = Id; Player.UserIDString = Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Player.displayName = "C2 sampled inventory fixture";
                Player.Spawn(); Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.net.connection = new Network.Connection {
                    userid = Id, username = Player.displayName, connected = true, active = true, player = Player
                };
                BasePlayer.activePlayerLookup[Id] = Player; BasePlayer.activePlayerList.Add(Player); OnPlayerConnected(Player);
                PlayerLifetime Lifetime = Gameplay.Players.Find(Player.UserIDString);
                C2Check(Lifetime != null && ReferenceEquals(Lifetime.Identity, Player) &&
                    ReferenceEquals(Lifetime.Connection, Player.net.connection), "original D11 human connection");
                ItemContainer Main = Player.inventory.containerMain, Belt = Player.inventory.containerBelt, Wear = Player.inventory.containerWear;
                C2Check(Main.itemList.Count + Belt.itemList.Count + Wear.itemList.Count == 0, "empty controlled inventory");
                ItemDefinition Scrap = ItemManager.FindItemDefinition("scrap"), Hat = ItemManager.FindItemDefinition("hat.cap");
                C2Check(Scrap != null && Scrap.stackable >= 100 && Hat != null, "fixture definitions and stack capacity");
                Func<InventoryQuantitySnapshot> Sample = () => C2Snapshot(Lifetime);
                InventoryQuantitySnapshot Before = Sample(), After;
                C2Check(Before.Count("c2.unknown") == 0, "unknown canonical shortname absent");

                Item First = C2Item(Temporary, Scrap, 100);
                C2Move(First, Main, 0, false, Player); Temporary.Remove(First);
                After = Sample(); C2Delta(Before, After, "scrap", 0, 100, "create/insert gain");
                Before = After; First.UseItem(20);
                After = Sample(); C2Delta(Before, After, "scrap", 100, 80, "UseItem consumption");
                Before = After; First.amount += 10; First.MarkDirty();
                After = Sample(); C2Delta(Before, After, "scrap", 80, 90, "direct amount gain");
                Before = After; First.amount -= 10; First.MarkDirty();
                After = Sample(); C2Delta(Before, After, "scrap", 90, 80, "direct amount loss");
                Before = After; First.amount += 10; First.MarkDirty(); First.amount -= 10; First.MarkDirty();
                After = Sample(); C2Zero(Before, After, "gain 10/lose 10 between samples");
                Puts(C2Prefix + "PASS create/use/direct amounts and balanced interval");

                Before = After; C2Move(First, Belt, 0, false, Player);
                After = Sample(); C2Zero(Before, After, "completed main to belt");
                Before = After; C2Move(First, Main, 0, false, Player);
                After = Sample(); C2Zero(Before, After, "completed belt to main");
                Item Clothing = C2Item(Temporary, Hat, 1);
                Before = After; C2Move(Clothing, Main, 1, false, Player); Temporary.Remove(Clothing);
                After = Sample(); C2Delta(Before, After, "hat.cap", 0, 1, "clothing gain");
                Before = After; C2Move(Clothing, Wear, 0, false, Player);
                After = Sample(); C2Zero(Before, After, "completed main to wear");
                Before = After; C2Move(Clothing, Main, 1, false, Player);
                After = Sample(); C2Zero(Before, After, "completed wear to main");

                // Deliberately place a sample between the remove and insert legs.
                // A legal physical state is not a completed-operation fence.
                Before = After; First.RemoveFromContainer(); Temporary.Add(First);
                C2Check(First.parent == null && !Main.itemList.Contains(First), "transfer source unlinked");
                InventoryQuantitySnapshot Intermediate = Sample();
                C2Delta(Before, Intermediate, "scrap", 80, 0, "intermediate transfer removal");
                C2Move(First, Belt, 0, false, Player); Temporary.Remove(First);
                After = Sample(); C2Delta(Intermediate, After, "scrap", 0, 80, "intermediate transfer restoration");
                C2Zero(Before, After, "complete composed transfer");
                Puts(C2Prefix + "NEGATIVE intermediate transfer sampled -80/+80; completed transfer zero");
                C2Move(First, Main, 0, false, Player);
                Before = Sample(); Item Split = First.SplitItem(30);
                C2Check(Split != null, "SplitItem returned piece"); Temporary.Add(Split);
                C2Check(Split.parent == null && Split.amount == 30 && First.amount == 50, "split intermediate ownership");
                Intermediate = Sample(); C2Delta(Before, Intermediate, "scrap", 80, 50, "intermediate split removal");
                C2Move(Split, Main, 2, false, Player); Temporary.Remove(Split);
                After = Sample(); C2Delta(Intermediate, After, "scrap", 50, 80, "placed split restoration");
                C2Zero(Before, After, "completed split");
                Puts(C2Prefix + "NEGATIVE intermediate split sampled -30/+30; completed split zero");
                Before = After; C2Move(Split, Main, 0, true, Player);
                After = Sample(); C2Zero(Before, After, "completed same-Player merge");
                C2Check(First.amount == 80, "merge destination quantity");
                Puts(C2Prefix + "PASS completed main/belt/wear transfers and split/merge zero net");

                Before = After; Item Drop = First.SplitItem(30);
                C2Check(Drop != null, "drop split returned item"); Temporary.Add(Drop);
                DroppedWorld = Drop.Drop(Player.transform.position, Vector3.zero);
                C2Check(DroppedWorld != null && !DroppedWorld.IsDestroyed && ReferenceEquals(Drop.GetWorldEntity(), DroppedWorld) &&
                    Drop.parent == null && Drop.amount == 30, "fixture-owned physical world drop");
                Temporary.Remove(Drop);
                After = Sample(); C2Delta(Before, After, "scrap", 80, 50, "world drop loss");
                Before = After; C2Move(Drop, Main, 0, true, Player);
                After = Sample(); C2Delta(Before, After, "scrap", 50, 80, "world item receipt");
                C2Check(DroppedWorld.IsDestroyed, "pickup removed fixture world entity"); DroppedWorld = null;
                Puts(C2Prefix + "PASS world drop/pickup sampled quantities");

                Item CallbackItem = C2Item(Temporary, Scrap, 5);
                Action<Item, bool, BasePlayer> OriginalChanged = Main.onItemAddedRemoved;
                int CallbackCalls = 0; InventoryQuantitySnapshot CallbackSample = null;
                Before = After;
                Main.onItemAddedRemoved = OriginalChanged + ((Value, Added, SourcePlayer) => {
                    if (!Added || !ReferenceEquals(Value, CallbackItem)) return;
                    C2Check(++CallbackCalls == 1, "bounded controlled callback");
                    Value.amount += 7; Value.MarkDirty(); CallbackSample = Sample();
                });
                try { C2Move(CallbackItem, Main, 3, false, Player); Temporary.Remove(CallbackItem); }
                finally { Main.onItemAddedRemoved = OriginalChanged; }
                C2Check(CallbackCalls == 1 && CallbackSample != null, "actual container callback interference exercised");
                After = Sample(); C2Delta(Before, After, "scrap", 80, 92, "callback changed physical gain");
                C2Zero(CallbackSample, After, "callback sample parity after return");
                Puts(C2Prefix + "PASS callback interference sampled +12 for requested +5; no causal attribution");

                Before = After; CallbackItem.Remove(0);
                After = Sample(); C2Delta(Before, After, "scrap", 92, 80, "Remove entire stack");
                Before = After; int Taken = Player.inventory.Take(null, Scrap.itemid, 20);
                C2Check(Taken == 20, "actual PlayerInventory.Take return quantity");
                After = Sample(); C2Delta(Before, After, "scrap", 80, 60, "Take physical loss");
                Before = After; First.Remove(0); Clothing.Remove(0);
                After = Sample(); InventoryQuantityChange[] Batch = InventoryQuantitySnapshot.Difference(Before, After);
                C2Check(Batch.Length == 2 && Batch.Length <= 2 * FacadePolicy.InventoryStacks, "bounded batch changes");
                C2Check(Batch[0].Item == "hat.cap" && Batch[0].Before == 1 && Batch[0].After == 0 && Batch[0].Delta == -1 &&
                    Batch[1].Item == "scrap" && Batch[1].Before == 60 && Batch[1].After == 0 && Batch[1].Delta == -60,
                    "ordinal bounded Item/Before/After/Delta batch");
                C2Check(Before.Count("scrap") == 60 && After.Count("scrap") == 0, "snapshot scalars survive host removal");
                Puts(C2Prefix + "PASS Take/Remove and bounded ordered changes");

                var Public = Host.Execute("c2.readonly", "local P=game:GetService('Players'):GetPlayerByUserId('" + Id +
                    "'); local I=P:GetInventory(); assert(I:CountItem('scrap')==0 and P:GetInventory():CountItem('scrap')==0); " +
                    "assert(not pcall(function() return I.ItemAdded end)); assert(not pcall(function() return I.ItemRemoved end)); " +
                    "assert(not pcall(function() return I.ItemChanged end))");
                C2Check(Public.Status == RuntimeStatus.OK, "existing C1 reads/no public C3: " + Public.Error);
                OnPlayerDisconnected(Player, "controlled C2 disconnect");
                C2Check(Gameplay.Players.Resolve(Lifetime.Token, Lifetime.UserId) == null, "old D11 lifetime invalidated");
                Player.net.connection = new Network.Connection {
                    userid = Id, username = Player.displayName, connected = true, active = true, player = Player
                };
                OnPlayerConnected(Player);
                PlayerLifetime Reconnected = Gameplay.Players.Find(Player.UserIDString);
                C2Check(Reconnected != null && Reconnected.Token != Lifetime.Token, "reconnect has new exact lifetime");
                bool Stale = false;
                try { Sample(); } catch (FacadeException) { Stale = true; }
                C2Check(Stale && C2Snapshot(Reconnected).Count("scrap") == 0, "old sampler cannot retarget reconnect");
                Puts(C2Prefix + "PASS controlled D11 lifetime and C1 read parity; no authenticated client");
                C2RegistryFixtures(Player, Reconnected, Temporary);
                RegistryScheduled = true;
            } catch (Exception Error) { PrintError(C2Prefix + "FAIL " + Error); }
            finally {
                if (!RegistryScheduled) C2Finish(Player, Temporary, DroppedWorld, false);
            }
        }

        // Use actual distinct Unity frames. Never reset/fake the shared frame
        // clock or pump a synchronous invented frame sequence to obtain receipts.
        private void C2RegistryFrame(Action Step, Action<Exception> Failed)
        {
            int Frame = Time.frameCount, Attempts = 0;
            Action Wait = null;
            Wait = () => {
                try {
                    if (Time.frameCount == Frame) {
                        C2Check(++Attempts <= 120, "distinct Unity frame scheduling bound");
                        NextTick(Wait); return;
                    }
                    Step();
                } catch (Exception Error) { Failed(Error); }
            };
            NextTick(Wait);
        }

        private void C2RegistryFixtures(BasePlayer Player, PlayerLifetime Lifetime, List<Item> Temporary)
        {
            InventoryNetObservation Samples = Gameplay.InventorySamples;
            C2Check(Samples.WatchCount == 0 && Samples.DemandCount == 0 && Samples.PendingCount == 0,
                "isolated private registry starts empty");
            ulong Handle = Samples.Stage(Gameplay.Active, Lifetime.Token, Lifetime.UserId);
            Action<Exception> Failed = Error => {
                PrintError(C2Prefix + "FAIL registry " + Error);
                C2Finish(Player, Temporary, null, false);
            };
            Action Tick = () => {
                C2ReadPath();
                Samples.Tick(Time.frameCount);
                C2ReadPath();
                C2Check(Samples.LastScans <= InventoryNetObservationPolicy.ScansPerFrame &&
                    Samples.PendingCount <= InventoryNetObservationPolicy.Pending &&
                    Samples.ChargedBytes <= InventoryNetObservationPolicy.RetainedChargeCeiling, "private registry bounds");
            };
            C2RegistryFrame(() => {
                Tick(); InventoryQuantityChange Change;
                C2Check(!Samples.Consume(Handle, out Change), "first real registry baseline has no replay");
                Item Value = C2Item(Temporary, ItemManager.FindItemDefinition("scrap"), 5);
                C2Move(Value, Player.inventory.containerMain, 0, false, Player); Temporary.Remove(Value);
                ulong Scans = Samples.Scans;
                Tick();
                C2Check(Samples.Scans == Scans && !Samples.Consume(Handle, out Change), "duplicate Unity frame does not rescan");
                C2RegistryFrame(() => {
                    Tick();
                    C2Check(Samples.Consume(Handle, out Change) && Change.Item == "scrap" &&
                        Change.Before == 0 && Change.After == 5 && Change.Delta == 5, "real registry gain receipt");
                    C2Check(!Samples.Consume(Handle, out Change), "record consumed only once");
                    Value.amount = 7; Value.MarkDirty();
                    C2RegistryFrame(() => {
                        Tick(); C2Check(Samples.PendingCount == 1, "root retirement has actual queued sample");
                        FacadeSession OldOwner = Gameplay.Active;
                        C2Check(Host.Reload().Status == RuntimeStatus.OK && OldOwner.Disposed, "actual root replacement");
                        C2Check(!Samples.Consume(Handle, out Change) && Samples.PendingCount == 0,
                            "root replacement cancels original private demand");
                        Handle = Samples.Stage(Gameplay.Active, Lifetime.Token, Lifetime.UserId);
                        C2RegistryFrame(() => {
                            Tick(); C2Check(!Samples.Consume(Handle, out Change), "replacement baselines without historical gain");
                            Value.amount = 9; Value.MarkDirty();
                            C2RegistryFrame(() => {
                                Tick(); C2Check(Samples.PendingCount == 1, "reconnect has actual queued sample");
                                OnPlayerDisconnected(Player, "C2 registry reconnect");
                                Player.net.connection = new Network.Connection {
                                    userid = Player.userID, username = Player.displayName, connected = true, active = true, player = Player
                                };
                                OnPlayerConnected(Player);
                                PlayerLifetime Next = Gameplay.Players.Find(Player.UserIDString);
                                C2Check(Next != null && Next.Token != Lifetime.Token && !Samples.Consume(Handle, out Change),
                                    "Consume cannot deliver original D11 sample to reconnect");
                                Samples.Cancel(Handle);
                                Handle = Samples.Stage(Gameplay.Active, Next.Token, Next.UserId);
                                C2RegistryFrame(() => {
                                    Tick(); C2Check(!Samples.Consume(Handle, out Change), "reconnect baseline without replay");
                                    Value.amount = 11; Value.MarkDirty();
                                    C2RegistryFrame(() => {
                                        Tick(); C2Check(Samples.PendingCount == 1, "fatal recovery has actual queued sample");
                                        OldOwner = Gameplay.Active;
                                        C2Check(Host.Execute("c2.registry.fatal", "while true do end").Status == RuntimeStatus.TIMEOUT &&
                                            Host.Ready && OldOwner.Disposed, "actual fatal VM recovery");
                                        C2Check(!Samples.Consume(Handle, out Change) && Samples.PendingCount == 0 &&
                                            Samples.DemandCount == 0 && Samples.WatchCount == 0, "fatal VM retires private sample owners");
                                        Handle = Samples.Stage(Gameplay.Active, Next.Token, Next.UserId);
                                        C2RegistryFrame(() => {
                                            Tick(); C2Check(!Samples.Consume(Handle, out Change), "recovered root baseline without replay");
                                            Samples.Cancel(Handle);
                                            C2Check(Samples.PendingCount == 0 && Samples.DemandCount == 0 && Samples.WatchCount == 0,
                                                "private Cancel releases final watch and demand");
                                            Puts(C2Prefix + "PASS live registry Stage/Tick/Consume/Cancel, distinct frames, root replacement, reconnect and fatal recovery");
                                            C2Finish(Player, Temporary, null, true);
                                        }, Failed);
                                    }, Failed);
                                }, Failed);
                            }, Failed);
                        }, Failed);
                    }, Failed);
                }, Failed);
            }, Failed);
        }

        private void C2Finish(BasePlayer Player, List<Item> Temporary, BaseEntity DroppedWorld, bool Passed)
        {
                try {
                    ReleaseNative();
                    if (DroppedWorld != null && !DroppedWorld.IsDestroyed) DroppedWorld.Kill();
                    foreach (Item Value in Temporary) {
                        if (Value != null && !Value.IsRemoved()) { Value.RemoveFromContainer(); Value.Remove(0); }
                    }
                    if (Player != null) {
                        OnPlayerDisconnected(Player, "C2 cleanup");
                        BasePlayer.activePlayerLookup.Remove(Player.userID); BasePlayer.activePlayerList.Remove(Player);
                        if (Player.net != null) Player.net.connection = null;
                        foreach (ItemContainer Container in new[] {
                            Player.inventory.containerMain, Player.inventory.containerBelt, Player.inventory.containerWear
                        }) {
                            C2Check(Container.itemList.Count <= FacadePolicy.InventoryStacks, "bounded fixture cleanup");
                            foreach (Item Value in Container.itemList.ToArray()) { Value.RemoveFromContainer(); Value.Remove(0); }
                        }
                        if (!Player.IsDestroyed) Player.Kill();
                    }
                    ItemManager.DoRemoves();
                    Puts(C2Prefix + "CLEANUP fixture-owned Player/items/world/delegates released");
                    if (Passed) Puts(C2Prefix + "PASS actual physical sampled inventory; no public Signals or completed-operation claim");
                } catch (Exception Error) { PrintError(C2Prefix + "FAIL cleanup " + Error); }
                finally { ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"); }
        }
    }
}
