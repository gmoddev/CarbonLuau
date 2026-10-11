// Isolated C3 public sampled Signal fixture; package EXCLUSIVELY.
// Controlled human/D11 connection, not an authenticated Rust client.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string C3Prefix = "[CarbonLuau:InventoryChangedPublicFixture] ";
        private MethodInfo C3LoadedIsValid, C3LoadedIsRemoved;
        partial void RunInventoryChangedPublicFixtures() { NextTick(RunC3InventoryFixture); }

        private static void C3Check(bool Value, string Message)
        { if (!Value) throw new InvalidOperationException(Message); }

        private static string C3ReceiptText(string Value)
        {
            Value = Value ?? "<null>";
            C3Check(Value.Length <= 1024, "loaded receipt text bound");
            return Value.Replace('\r', ' ').Replace('\n', ' ');
        }

        private static void C3ReadPin(MethodInfo Method, int Token, int Length, string ExpectedHash)
        {
            C3Check(Method != null && Method.MetadataToken == Token && !Method.IsStatic && !Method.IsVirtual &&
                Method.ReturnType == typeof(bool) && Method.GetParameters().Length == 0, "exact loaded read method identity");
            MethodBody Body = Method.GetMethodBody();
            C3Check(Body != null && Body.ExceptionHandlingClauses.Count == 0 && Body.LocalVariables.Count == 0,
                "pinned read method has no EH/locals");
            byte[] IL = Body.GetILAsByteArray();
            C3Check(IL != null && IL.Length == Length, "pinned read method IL length");
            string Hash;
            using (SHA256 Digest = SHA256.Create())
                Hash = BitConverter.ToString(Digest.ComputeHash(IL)).Replace("-", "").ToLowerInvariant();
            C3Check(Hash == ExpectedHash, "loaded read body hash drift: " + Method.Name);
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
                    C3Check(Offset < IL.Length && IL[Offset++] == 0x02, "pinned read cgt instruction");
                } else C3Check(false, "unexpected/callback-capable pinned read opcode");
                C3Check(Offset <= IL.Length, "pinned read operand extent");
            }
            var Info = Harmony.GetPatchInfo(Method);
            // No owner allowlist: every effective Harmony patch category must
            // be empty, including transpilers and return-value postfixes.
            C3Check(Info == null || (Info.Prefixes.Count == 0 && Info.Postfixes.Count == 0 &&
                Info.Transpilers.Count == 0 && Info.Finalizers.Count == 0), "loaded read Harmony topology drift: " + Method.Name);
        }

        private void C3ReadPath()
        {
            C3ReadPin(C3LoadedIsValid, 0x06006a4a, 17,
                "477480dbc895bfd4d0b69e6660104e5c98b23c6bebd9ae0c01182382da848951");
            C3ReadPin(C3LoadedIsRemoved, 0x06006a3b, 14,
                "d80e2530bb0ad5c9dd617a1e19ac1370be37b6e7e0c4416b3f653645ca058a38");
        }

        private void C3PatchReceipts(MethodInfo Method, string Kind, IEnumerable<Patch> Patches, ref int Total)
        {
            int Count = 0;
            foreach (Patch Value in Patches) {
                C3Check(++Count <= 64 && ++Total <= 256, "loaded patch receipt bound");
                C3Check(Value.PatchMethod != null, "loaded patch method available");
                Puts(C3Prefix + "LOADED PATCH target=" + Method.DeclaringType.FullName + "." + Method.Name +
                    " token=0x" + Method.MetadataToken.ToString("x8") + " kind=" + Kind +
                    " owner=" + C3ReceiptText(Value.owner) + " priority=" + Value.priority +
                    " method=" + C3ReceiptText((Value.PatchMethod.DeclaringType == null ? "<dynamic>" :
                        Value.PatchMethod.DeclaringType.FullName) + "." + Value.PatchMethod) +
                    " patchMVID=" + Value.PatchMethod.Module.ModuleVersionId);
            }
        }

        private void C3LoadedHostReceipts()
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
                C3Check(Methods.Length <= 1024, "cold loaded method count bound: " + Target.FullName);
                Array.Sort(Methods, (Left, Right) => Left.MetadataToken.CompareTo(Right.MetadataToken));
                foreach (string Name in Names[TypeIndex]) {
                    int Matches = 0;
                    foreach (MethodInfo Method in Methods) {
                        if (Method.Name != Name) continue;
                        Matches++; C3Check(++Selected <= 64, "selected loaded method receipt bound");
                        MethodBody Body = Method.GetMethodBody();
                        C3Check(Body != null, "loaded method IL available: " + Target.FullName + "." + Name);
                        byte[] IL = Body.GetILAsByteArray();
                        C3Check(IL != null && IL.Length <= 262144, "individual loaded IL extent bound");
                        Bytes = checked(Bytes + IL.Length); C3Check(Bytes <= 1048576, "total loaded IL extent bound");
                        string Hash;
                        using (SHA256 Digest = SHA256.Create())
                            Hash = BitConverter.ToString(Digest.ComputeHash(IL)).Replace("-", "").ToLowerInvariant();
                        var Info = Harmony.GetPatchInfo(Method);
                        // GetMethodBody is this loaded module's reflected IL,
                        // not Harmony's generated replacement/detour body. Its
                        // current patch topology is reported independently.
                        Puts(C3Prefix + "LOADED METHOD type=" + Target.FullName + " signature=" + C3ReceiptText(Method.ToString()) +
                            " token=0x" + Method.MetadataToken.ToString("x8") + " bodySHA256=" + Hash +
                            " MVID=" + Method.Module.ModuleVersionId + " ILBytes=" + IL.Length +
                            " ILSource=loaded-reflection-original prefixes=" + (Info == null ? 0 : Info.Prefixes.Count) +
                            " postfixes=" + (Info == null ? 0 : Info.Postfixes.Count) +
                            " transpilers=" + (Info == null ? 0 : Info.Transpilers.Count) +
                            " finalizers=" + (Info == null ? 0 : Info.Finalizers.Count));
                        if (Info != null) {
                            C3PatchReceipts(Method, "prefix", Info.Prefixes, ref Patches);
                            C3PatchReceipts(Method, "postfix", Info.Postfixes, ref Patches);
                            C3PatchReceipts(Method, "transpiler", Info.Transpilers, ref Patches);
                            C3PatchReceipts(Method, "finalizer", Info.Finalizers, ref Patches);
                        }
                        if (Target == typeof(Item) && Name == "IsValid" && !Method.IsStatic && Method.GetParameters().Length == 0) {
                            C3Check(C3LoadedIsValid == null, "unique loaded no-argument Item.IsValid");
                            C3LoadedIsValid = Method;
                        }
                        if (Target == typeof(Item) && Name == "IsRemoved" && !Method.IsStatic && Method.GetParameters().Length == 0) {
                            C3Check(C3LoadedIsRemoved == null, "unique loaded no-argument Item.IsRemoved");
                            C3LoadedIsRemoved = Method;
                        }
                    }
                    if (Matches == 0) {
                        Puts(C3Prefix + "LOADED ABSENT type=" + Target.FullName + " method=" + Name);
                        // Some builds have no Item.Take; ItemContainer.Take and
                        // PlayerInventory.Take are separate, mandatory receipts.
                        C3Check(Target == typeof(Item) && Name == "Take", "required loaded method absent");
                    }
                }
            }
            FieldInfo Changed = typeof(ItemContainer).GetField("onItemAddedRemoved",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            C3Check(Changed != null && Changed.FieldType == typeof(Action<Item, bool, BasePlayer>), "exact loaded container callback signature");
            Puts(C3Prefix + "LOADED FIELD ItemContainer.onItemAddedRemoved type=" + Changed.FieldType +
                " token=0x" + Changed.MetadataToken.ToString("x8") + " MVID=" + Changed.Module.ModuleVersionId);
            C3ReadPath();
            Puts(C3Prefix + "PASS loaded IsValid/IsRemoved exact tokens/body hashes, no EH/locals/calls and zero Harmony patches");
            Puts(C3Prefix + "PASS loaded IL/topology receipts and Item.IsValid prefixes=0 before physical tests");
        }


        private const string C3State = "local S=require('state'); ";
        private const string C3Context = "assert(type(C)=='table' and table.isfrozen(C)); local N=0; " +
            "for K in C do assert(K=='Item' or K=='Before' or K=='After' or K=='Delta'); N+=1 end; assert(N==4); " +
            "assert(type(C.Item)=='string' and type(C.Before)=='number' and type(C.After)=='number' and type(C.Delta)=='number'); " +
            "assert(C.Before>=0 and C.After>=0 and C.Before%1==0 and C.After%1==0 and C.Delta==C.After-C.Before and C.Delta~=0); " +
            "assert(not pcall(function() C.Item='spoof' end)); assert(not pcall(function() C.Before=0 end)); " +
            "assert(not pcall(function() C.After=0 end)); assert(not pcall(function() C.Delta=0 end)); ";
        private string C3Source;
        private BasePlayer C3Player;
        private readonly List<Item> C3Temporary = new List<Item>();
        private readonly Queue<Action> C3Steps = new Queue<Action>();
        private bool C3Finished;
        private bool C3AllowCallbackError;
        private int C3Expected;
        private AddonRegistry C3Registry;

        private void C3Run(string Code)
        {
            ExecutionResult Result = Host.Execute("c3.live", Code);
            C3Check(Result.Status == RuntimeStatus.OK, Code + ": " + Result.Error);
        }
        private void C3Drain(bool AllowError = false)
        {
            for (int Turn=0; Turn<256 && Host.HasWork; Turn++) foreach (ExecutionResult Result in Host.Drain()) {
                C3Check(AllowError || Result.Status == RuntimeStatus.OK, "deferred callback: " + Result.Error);
            }
            C3Check(!Host.HasWork && Gameplay.GameplayEvents.PendingCount == 0 &&
                Gameplay.GameplayEvents.RetainedBytes == 0 && Gameplay.InventorySamples.PendingCount == 0,
                "deferred delivery refunds both D23 and combined source retention");
        }
        private void C3Count(int Expected)
        { C3Run(C3State + "assert(S.Count==" + Expected.ToString(CultureInfo.InvariantCulture) + ", 'count '..S.Count)"); }
        private void C3Expect(string Item, int Before, int After)
        {
            C3Expected++;
            C3Run(C3State + "assert(S.Count==" + C3Expected + " and S.Last.Item=='" + Item +
                "' and S.Last.Before==" + Before + " and S.Last.After==" + After +
                " and S.Last.Delta==" + (After-Before) + ")");
        }
        private Item C3Item(ItemDefinition Definition, int Amount)
        {
            C3ReadPath();
            Item Value = ItemManager.Create(Definition, Amount);
            C3Check(Value != null, "controlled ItemManager.Create");
            C3Temporary.Add(Value);
            C3Check(Value.IsValid() && Value.parent == null && Value.amount == Amount && Value.GetWorldEntity() == null,
                "temporary fixture ownership");
            return Value;
        }
        private void C3Move(Item Value, ItemContainer Target, int Slot, bool Stack = false)
        {
            C3ReadPath();
            ItemContainer Source = Value.parent;
            C3Check(Source == null || Source.itemList.Contains(Value), "actual source reciprocal membership");
            C3Check(Value.MoveToContainer(Target, Slot, Stack, false, C3Player, false), "exact-slot physical transfer");
            if (Value.IsRemoved()) C3Check(Stack && !Value.IsValid() && Value.amount == 0 && Value.parent == null,
                "merge consumed actual source");
            else C3Check(Value.IsValid() && ReferenceEquals(Value.parent, Target) && Target.itemList.Contains(Value) &&
                Value.position == Slot && Value.GetWorldEntity() == null, "accepted physical destination");
            if (Source != null && !ReferenceEquals(Source, Target))
                C3Check(!Source.itemList.Contains(Value), "actual source container no longer holds transferred item");
            C3Temporary.Remove(Value);
        }
        private static byte[] C3Package(string Id, string Init, string Api = null, string Required = null)
        {
            using (var Bytes = new MemoryStream()) {
                using (var Zip = new ZipArchive(Bytes, ZipArchiveMode.Create, true)) {
                    Action<string,string> Add = (Name, Text) => { using (var Writer=new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Text); };
                    Add("addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" +
                        (Api == null ? "" : ",\"publicModules\":[\"api\"]") +
                        (Required == null ? "" : ",\"dependencies\":{\"required\":[\"" + Required + "\"],\"optional\":[]}") + "}");
                    Add("init.luau", Init); if (Api != null) Add("api.luau", Api);
                } return Bytes.ToArray();
            }
        }
        private void C3Process()
        { int Guard=256; while (C3Registry.HasPending && Guard-- > 0) C3Registry.ProcessOne(); C3Check(!C3Registry.HasPending, "bounded addon convergence"); }
        private string C3Entry()
        {
            return C3State + "S.Count=0; S.P=game:GetService('Players'):GetPlayerByUserId('" + C3Player.UserIDString +
                "'); S.I=S.P:GetInventory(); S.Connection=S.I.ItemChanged:Connect(function(C) " + C3Context +
                "S.Count+=1; S.Last=C end)";
        }

        private void RunC3InventoryFixture()
        {
            const ulong Id = 76561198000000114;
            try {
                C3Check(BasePlayer.activePlayerList.Count == 0 && Host != null && Host.Ready, "isolated ready zero-client server");
                C3LoadedHostReceipts();
                C3Check(!BasePlayer.activePlayerLookup.ContainsKey(Id), "controlled unused identity");
                C3Player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new Vector3(0,-100,0)) as BasePlayer;
                C3Check(C3Player != null, "controlled Player creation");
                C3Player.enableSaving=false; C3Player.userID=Id; C3Player.UserIDString=Id.ToString(CultureInfo.InvariantCulture);
                C3Player.displayName="C3 controlled inventory connection";
                C3Player.Spawn(); C3Player.lifestate=BaseCombatEntity.LifeState.Alive;
                C3Player.net.connection=new Network.Connection {userid=Id,username=C3Player.displayName,connected=true,active=true,player=C3Player};
                BasePlayer.activePlayerLookup[Id]=C3Player; BasePlayer.activePlayerList.Add(C3Player); OnPlayerConnected(C3Player);
                PlayerLifetime Lifetime=Gameplay.Players.Find(C3Player.UserIDString);
                C3Check(Lifetime!=null && ReferenceEquals(Lifetime.Identity,C3Player) &&
                    ReferenceEquals(Lifetime.Connection,C3Player.net.connection), "actual original D11 connection");
                ItemContainer Main=C3Player.inventory.containerMain, Belt=C3Player.inventory.containerBelt, Wear=C3Player.inventory.containerWear;
                C3Check(Main.itemList.Count+Belt.itemList.Count+Wear.itemList.Count==0, "empty controlled direct inventory");
                ItemDefinition Scrap=ItemManager.FindItemDefinition("scrap"), Hat=ItemManager.FindItemDefinition("hat.cap");
                C3Check(Scrap!=null && Scrap.stackable>=100 && Hat!=null, "controlled item definitions");

                if (Addons!=null) { Addons.Dispose(); Addons=null; } Host.Dispose();
                C3Source=C3Entry();
                Host=new ScriptHost(Native,new RuntimeConfig {MaxCallbackMilliseconds=50,FrameDrainBudgetMilliseconds=30},()=>{
                    var Snapshot=new ScriptSnapshot {EntryName="init.luau",EntrySource=C3Source};
                    Snapshot.Modules.Add("state","return {}");
                    Snapshot.Modules.Add("bad",C3State+"S.Leak=S.P:GetInventory(); S.Leak.ItemChanged:Connect(function() error('leak') end); error('failed module')");
                    Snapshot.Modules.Add("disconnectfail",C3State+"S.Connection:Disconnect(); error('failed disconnect')");
                    Snapshot.Modules.Add("forward","return function() "+C3State+"S.Forward=S.I.ItemChanged:Connect(function(C) "+C3Context+"S.ForwardCount=(S.ForwardCount or 0)+1 end) end");
                    Snapshot.Modules.Add("coldfail","require('forward')(); error('failed forward')");
                    return Snapshot;
                },Gameplay);
                C3Check(Host.Reload().Status==RuntimeStatus.OK, "real public root load");
                C3Registry=new AddonRegistry(Host,Native.HostLifetimeId);
                C3Run(C3State+"local Current=S.P:GetInventory(); assert(Current==S.P:GetInventory()); assert(Current:CountItem('scrap')==S.I:CountItem('scrap')); assert(not pcall(function() S.I.ItemChanged=1 end)); local T=S.I.ItemChanged; "+
                    "assert(not pcall(function() T.X=1 end)); for _,K in {'ItemAdded','ItemRemoved','GiveItem','TakeItem'} do assert(not pcall(function() return S.I[K] end)) end");
                Item First=null, Split=null, Clothing=null;
                C3Steps.Enqueue(()=>{
                    C3Count(0); First=C3Item(Scrap,100); C3Move(First,Main,0);
                    C3Count(0); // Host mutation never synchronously enters a Luau callback.
                });
                C3Steps.Enqueue(()=>{C3Expect("scrap",0,100); First.UseItem(20);});
                C3Steps.Enqueue(()=>{C3Expect("scrap",100,80); First.amount+=10; First.MarkDirty();});
                C3Steps.Enqueue(()=>{C3Expect("scrap",80,90); First.amount-=10; First.MarkDirty();});
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",90,80);
                    First.amount+=10; First.MarkDirty(); First.amount-=10; First.MarkDirty();
                });
                C3Steps.Enqueue(()=>{C3Count(C3Expected); C3Move(First,Belt,0);});
                C3Steps.Enqueue(()=>{C3Count(C3Expected); C3Move(First,Main,0);});
                C3Steps.Enqueue(()=>{
                    C3Count(C3Expected); Split=First.SplitItem(30); C3Check(Split!=null && Split.parent==null && Split.amount==30, "actual split source");
                    C3Temporary.Add(Split); C3Move(Split,Main,2);
                });
                C3Steps.Enqueue(()=>{C3Count(C3Expected); C3Move(Split,Main,0,true);});
                C3Steps.Enqueue(()=>{
                    C3Count(C3Expected);
                    Puts(C3Prefix+"PASS deferred gains/losses, balanced interval and completed transfer/split/merge zero");
                    First.RemoveFromContainer(); C3Temporary.Add(First);
                    C3Check(First.parent==null && !Main.itemList.Contains(First), "actual transfer remove leg");
                });
                C3Steps.Enqueue(()=>{C3Expect("scrap",80,0); C3Move(First,Belt,0);});
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",0,80); C3Move(First,Main,0);
                    Puts(C3Prefix+"NEGATIVE explicit intermediate transfer sampled -80/+80; complete total unchanged");
                    Split=First.SplitItem(30); C3Check(Split!=null && Split.parent==null && First.amount==50, "actual split intermediate");
                    C3Temporary.Add(Split);
                });
                C3Steps.Enqueue(()=>{C3Expect("scrap",80,50); C3Move(Split,Main,2);});
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",50,80); C3Move(Split,Main,0,true);
                    Puts(C3Prefix+"NEGATIVE explicit intermediate split sampled -30/+30; complete total unchanged");
                    Clothing=C3Item(Hat,1); C3Move(Clothing,Main,1);
                });
                C3Steps.Enqueue(()=>{C3Expect("hat.cap",0,1); C3Move(Clothing,Wear,0);});
                C3Steps.Enqueue(()=>{C3Count(C3Expected); C3Move(Clothing,Main,1);});
                C3Steps.Enqueue(()=>{
                    C3Count(C3Expected); int Demands=Gameplay.InventorySamples.DemandCount;
                    C3Run("assert(not pcall(require,'bad')); assert(not pcall(require,'disconnectfail')); assert(not pcall(require,'coldfail'))");
                    C3Check(Gameplay.InventorySamples.DemandCount==Demands, "failed provisional module/forward roll back demands");
                    C3Run(C3State+"assert(not pcall(function() S.Leak:CountItem('scrap') end)); S.ForwardCount=0; require('forward')()");
                });
                C3Steps.Enqueue(()=>{C3Count(C3Expected); First.amount=81; First.MarkDirty();});
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",80,81); C3Run(C3State+"assert(S.ForwardCount==1); S.Forward:Disconnect()");
                    Puts(C3Prefix+"PASS immutable strict four-key context, Signal readonly and provisional/cached forward publication");
                    C3Run(C3State+"S.SecondCount=0; S.Second=S.I.ItemChanged:Connect(function(C) "+C3Context+"S.SecondCount+=1 end)");
                    First.amount=82; First.MarkDirty();
                });
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",81,82); C3Run(C3State+"assert(S.SecondCount==0)"); First.amount=83; First.MarkDirty();
                });
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",82,83); C3Run(C3State+"assert(S.SecondCount==1); S.Second:Disconnect()");
                    Puts(C3Prefix+"PASS two subscribers with fresh first-Tick cutoff and no replay");
                    C3Run(C3State+"S.Fault=S.I.ItemChanged:Connect(function() error('controlled callback error') end)");
                });
                C3Steps.Enqueue(()=>{C3AllowCallbackError=true; First.amount=84; First.MarkDirty();});
                // Ordinary callback error is explicitly consumed and does not cancel the independent listener.
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",83,84); C3Run(C3State+"S.Fault:Disconnect()");
                    C3AllowCallbackError=false;
                    C3Source=C3Entry()+"; error('failed root')";
                    C3Check(Host.Reload().Status==RuntimeStatus.RUNTIME_ERROR, "failed real root replacement");
                    First.amount=85; First.MarkDirty();
                });
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",84,85); C3Source=C3Entry(); FacadeSession Old=Gameplay.Active;
                    C3Check(Host.Reload().Status==RuntimeStatus.OK && Old.Disposed, "successful real root replacement");
                    C3Expected=0;
                });
                C3Steps.Enqueue(()=>{
                    C3Count(0); C3Run(C3State+"S.Connection:Disconnect(); S.Connection:Disconnect()");
                    C3Check(Gameplay.InventorySamples.DemandCount==0, "disconnect reclaims private source");
                    First.amount=86; First.MarkDirty();
                });
                C3Steps.Enqueue(()=>{
                    C3Count(0); C3Run(C3State+"S.Connection=S.I.ItemChanged:Connect(function(C) "+C3Context+"S.Count+=1; S.Last=C end)");
                });
                C3Steps.Enqueue(()=>{C3Count(0); First.amount=87; First.MarkDirty();});
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",86,87);
                    OnPlayerDisconnected(C3Player,"controlled C3 reconnect");
                    C3Player.net.connection=new Network.Connection {userid=Id,username=C3Player.displayName,connected=true,active=true,player=C3Player};
                    OnPlayerConnected(C3Player); PlayerLifetime Next=Gameplay.Players.Find(C3Player.UserIDString);
                    C3Check(Next!=null && Next.Token!=Lifetime.Token, "same-account actual new D11 connection");
                    C3Run(C3State+"assert(not pcall(function() S.I:CountItem('scrap') end)); assert(not pcall(function() S.I.ItemChanged:Connect(function() end) end))");
                    C3Check(Host.Reload().Status==RuntimeStatus.OK, "reconnect fresh facade"); C3Expected=0;
                });
                C3Steps.Enqueue(()=>{
                    C3Count(0); FacadeSession Old=Gameplay.Active; long Generation=Host.VmGenerationId;
                    C3Check(Host.Execute("c3.fatal","while true do end").Status==RuntimeStatus.TIMEOUT &&
                        Host.Ready && Old.Disposed && Host.VmGenerationId!=Generation, "real fatal VM recovery"); C3Expected=0;
                });
                C3Steps.Enqueue(()=>{C3Count(0); First.amount=88; First.MarkDirty();});
                C3Steps.Enqueue(()=>{
                    C3Expect("scrap",87,88);
                    Puts(C3Prefix+"PASS callback error/disconnect, failed/successful root, old-token same-ID reconnect and fatal recovery without replay");
                    string Acquire="game:GetService('Players'):GetPlayerByUserId('"+Id+"'):GetInventory()";
                    object Provider=new object();
                    string[] Registration=C3Registry.RegisterArchive(Provider,C3Package("c3live",Acquire+
                        ".ItemChanged:Connect(function(C) "+C3Context+"end)", "return "+Acquire));
                    C3Check(Registration[0]=="OK", "real addon registration"); C3Process();
                    C3Check(C3Registry.Status(Provider,Registration[1])[2]=="Active", "real addon activation");
                    C3Registry.ReplaceArchive(Provider,Registration[1],C3Package("c3live","error('failed addon')","return "+Acquire)); C3Process();
                    C3Check(C3Registry.Status(Provider,Registration[1])[2]=="Active", "failed addon retains original");
                    C3Registry.ReplaceArchive(Provider,Registration[1],C3Package("c3live",Acquire+
                        ".ItemChanged:Connect(function(C) "+C3Context+"end)", "return "+Acquire)); C3Process();
                    C3Check(C3Registry.Status(Provider,Registration[1])[2]=="Active", "successful addon replacement");
                    C3Check(C3Registry.UnloadProvider(Provider)==1 && Gameplay.InventorySamples.DemandCount==1, "provider unload releases exact addon source");
                    Puts(C3Prefix+"PASS actual addon replacement/provider retirement and D23 cleanup");
                    C3Finish(true);
                });
                C3NextFrame();
            } catch (Exception Error) { PrintError(C3Prefix+"FAIL "+Error); C3Finish(false); }
        }

        private void C3NextFrame()
        {
            if (C3Finished) return;
            int Frame=Time.frameCount, Attempts=0;
            Action Wait=null;
            Wait=()=>{
                if (C3Finished) return;
                try {
                    if (Time.frameCount==Frame) { C3Check(++Attempts<=120, "distinct Unity frame scheduling bound"); NextTick(Wait); return; }
                    C3ReadPath(); Gameplay.InventorySamples.Tick(Time.frameCount); Gameplay.FlushInventoryChanges();
                    C3Drain(C3AllowCallbackError); C3ReadPath();
                    C3Check(Gameplay.InventorySamples.LastScans<=4 && Gameplay.InventorySamples.PendingCount<=256 &&
                        Gameplay.InventorySamples.WatchCount<=64 && Gameplay.InventorySamples.DemandCount<=128 &&
                        Gameplay.InventorySamples.ChargedBytes<=InventoryNetObservationPolicy.RetainedChargeCeiling,
                        "actual-frame source limits");
                    C3Check(C3Steps.Count!=0, "bounded step plan"); C3Steps.Dequeue()();
                    if (!C3Finished) C3NextFrame();
                } catch (Exception Error) { PrintError(C3Prefix+"FAIL frame "+Error); C3Finish(false); }
            };
            NextTick(Wait);
        }
        private void C3Finish(bool Passed)
        {
            if (C3Finished) return; C3Finished=true; C3Steps.Clear();
            try {
                if (C3Registry!=null) { C3Registry.Dispose(); C3Registry=null; }
                ReleaseNative();
                foreach (Item Value in C3Temporary) if (Value!=null && !Value.IsRemoved()) {Value.RemoveFromContainer(); Value.Remove(0);}
                C3Temporary.Clear();
                if (C3Player!=null) {
                    OnPlayerDisconnected(C3Player,"C3 cleanup");
                    BasePlayer.activePlayerLookup.Remove(C3Player.userID); BasePlayer.activePlayerList.Remove(C3Player);
                    if (C3Player.net!=null) C3Player.net.connection=null;
                    foreach (ItemContainer Container in new[] {C3Player.inventory.containerMain,C3Player.inventory.containerBelt,C3Player.inventory.containerWear}) {
                        C3Check(Container.itemList.Count<=128, "bounded cleanup");
                        foreach (Item Value in Container.itemList.ToArray()) {Value.RemoveFromContainer(); Value.Remove(0);}
                    }
                    if (!C3Player.IsDestroyed) C3Player.Kill();
                }
                ItemManager.DoRemoves();
                Puts(C3Prefix+"CLEANUP fixture-owned Player/items/delegates/domains released");
                if (Passed) Puts(C3Prefix+"PASS actual physical sampled ItemChanged deferred Signal; controlled connection, no authenticated client or completed-operation claim");
            } catch (Exception Error) { PrintError(C3Prefix+"FAIL cleanup "+Error); }
            finally { ConsoleSystem.Run(ConsoleSystem.Option.Server,"quit"); }
        }
    }
}
