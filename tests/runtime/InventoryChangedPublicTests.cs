using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Real public facade/compiler/VM tests with synthetic physical inventories.
// Host completion, authenticated clients and an operation ledger are not claimed.
internal static class InventoryChangedPublicTests
{
    [DllImport("libc")] private static extern int chmod(string Path, int Mode);
    private const string State = "local S=require('state'); ";
    private const string Context = "assert(type(C)=='table' and table.isfrozen(C)); local N=0; " +
        "for K in C do assert(K=='Item' or K=='Before' or K=='After' or K=='Delta'); N+=1 end; assert(N==4); " +
        "assert(type(C.Item)=='string' and type(C.Before)=='number' and type(C.After)=='number' and type(C.Delta)=='number'); " +
        "assert(C.Before>=0 and C.After>=0 and C.Before<=9007199254740991 and C.After<=9007199254740991); " +
        "assert(C.Before%1==0 and C.After%1==0 and C.Delta%1==0 and C.Delta==C.After-C.Before and C.Delta~=0); " +
        "assert(not pcall(function() C.Item='spoof' end)); assert(not pcall(function() C.Before=0 end)); " +
        "assert(not pcall(function() C.After=0 end)); assert(not pcall(function() C.Delta=0 end)); ";
    private const string Acquire = State + "S.P=game:GetService('Players'):GetPlayerByUserId('42'); S.I=S.P:GetInventory(); ";
    private const string Connect = "S.Connection=S.I.ItemChanged:Connect(function(C) " + Context +
        "S.Count+=1; S.Last=C; print('changed '..C.Item..' '..C.Before..' '..C.After..' '..C.Delta) end); ";
    private const string Entry = Acquire + "S.Count=0; " + Connect;

    private static void Check(bool Value, string Message)
    { if (!Value) throw new Exception("[CarbonLuau:InventoryChangedTest] " + Message); }

    private sealed class Definition
    {
        internal readonly string Name;
        internal Definition(string Name) { this.Name = Name; }
    }
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        internal Action<Runtime.FacadeSession, Runtime.FacadeSession> DuringPublish;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        { if (DuringPublish != null) DuringPublish(Previous, Next); }
    }
    private sealed class Player
    {
        internal readonly object[] Containers = {new object(), new object(), new object()};
        internal readonly List<Runtime.PhysicalInventoryStack>[] Stacks = {
            new List<Runtime.PhysicalInventoryStack>(), new List<Runtime.PhysicalInventoryStack>(), new List<Runtime.PhysicalInventoryStack>()};
        internal Runtime.PlayerView View;
        internal Runtime.PlayerLifetime Lifetime;
        internal int Scans;
        internal bool ThrowRead;
        internal void Add(object Definition, long Amount, int Container = 0, bool Valid = true)
        { Stacks[Container].Add(new Runtime.PhysicalInventoryStack(Containers[Container], Definition, Amount, Valid, new object(), Stacks[Container].Count)); }
        internal void Clear() { foreach (var Values in Stacks) Values.Clear(); }
        internal void Set(object Definition, long Amount)
        { Clear(); if (Amount != 0) Add(Definition, Amount); }
        internal Runtime.PhysicalInventorySource Source()
        {
            Scans++;
            var Values = new Runtime.PhysicalInventoryContainer[3];
            for (int Index = 0; Index < 3; Index++) {
                int Slot = Index;
                Values[Index] = new Runtime.PhysicalInventoryContainer { Identity = Containers[Slot], Capacity = 128,
                    StackCount = Stacks[Slot].Count, Read = Position => {
                        if (ThrowRead) throw new InvalidOperationException("controlled physical read failure");
                        return Stacks[Slot][Position]; } };
            }
            return new Runtime.PhysicalInventorySource {Main=Values[0], Belt=Values[1], Wear=Values[2]};
        }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Dictionary<string, object> Definitions = new Dictionary<string, object>(StringComparer.Ordinal);
        internal readonly Registrar Registrar = new Registrar();
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.ScriptHost Host;
        internal readonly Player Player;
        internal readonly object Scrap;
        internal string Source;
        internal long Frame;
        internal Fixture(Runtime.NativeRuntime Native, string Source = Entry)
        {
            this.Source = Source;
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.FacadeWorld(Players, Registrar, new Runtime.ItemDirectory(Name => {
                object Value; return Definitions.TryGetValue(Name, out Value) ? Value : null;
            }, Value => {
                foreach (var Item in Definitions) if (ReferenceEquals(Item.Value,Value)) return Item.Key;
                return null;
            }));
            World.GameplayEvents.FrameClock = () => Frame;
            Scrap = Item("scrap"); Player = AddPlayer("42"); Player.Set(Scrap, 3);
            Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds=30, FrameDrainBudgetMilliseconds=30}, Snapshot, World);
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "initial public fixture load");
        }
        internal object Item(string Name)
        { object Value; if (!Definitions.TryGetValue(Name, out Value)) { Value = new Definition(Name); Definitions.Add(Name, Value); } return Value; }
        internal Player AddPlayer(string Id)
        {
            var Value = new Player(); Value.View = new Runtime.PlayerView {Identity=new object(), Connection=new object(),
                UserId=Id, Name="C3 model connection", Connected=true, Inventory=Value.Source};
            Views[Id] = Value.View; Value.Lifetime = Players.Connect(Value.View); return Value;
        }
        private Runtime.ScriptSnapshot Snapshot()
        {
            var Value = new Runtime.ScriptSnapshot {EntryName="init.luau", EntrySource=Source};
            Value.Modules.Add("state", "return {}");
            Value.Modules.Add("bad", State + "S.Leak=S.P:GetInventory(); S.Bad=S.Leak.ItemChanged:Connect(function() error('rolled back listener') end); error('module failed')");
            Value.Modules.Add("nested", "return require('bad')");
            Value.Modules.Add("disconnectfail", State + "S.Connection:Disconnect(); error('rollback disconnect')");
            Value.Modules.Add("forward", "return function() " + State + "S.Forward=S.I.ItemChanged:Connect(function(C) " + Context + "print('forward') end) end");
            Value.Modules.Add("coldfail", "require('forward')(); error('rollback forwarded registration')");
            return Value;
        }
        internal Runtime.ExecutionResult Execute(string Code, bool AllowError = false)
        { var Result = Host.Execute("inventorychanged.fixture", Code); Check(AllowError || Result.Status == Runtime.RuntimeStatus.OK, Code + ": " + Result.Error); return Result; }
        internal void Tick(bool Flush = true)
        {
            Frame++; World.InventorySamples.Tick(Frame);
            Check(World.InventorySamples.LastScans <= 4 && World.InventorySamples.PendingCount <= 256 &&
                World.InventorySamples.WatchCount <= 64 && World.InventorySamples.DemandCount <= 128, "C2 public source bounds");
            if (Flush) World.FlushInventoryChanges();
            Check(World.GameplayEvents.PendingCount <= 512 && World.GameplayEvents.RetainedBytes <= 2097152, "shared D23 retention bounds");
        }
        internal string DrainDeferred(bool AllowError = false, bool AllowBuffered = false)
        {
            var Text = new StringBuilder();
            for (int Turn=0; Turn<256 && Host.HasWork; Turn++) foreach (var Result in Host.Drain()) {
                Check(AllowError || Result.Status == Runtime.RuntimeStatus.OK, Result.Error); Text.Append(Result.Logs);
            }
            Check(!Host.HasWork && World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0, "deferred consumption and exact refund");
            Check(AllowBuffered || World.InventorySamples.PendingCount == 0, "callback acknowledgements release combined buffered/native source charge");
            return Text.ToString();
        }
        internal void FlushNative()
        {
            var Domains = new List<Runtime.RuntimeDomain> {(Runtime.RuntimeDomain)typeof(Runtime.ScriptHost)
                .GetField("Current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Host)};
            Domains.AddRange(((SortedDictionary<long,Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost)
                .GetField("AddonDomains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Host)).Values);
            foreach (var Domain in Domains) Domain.FacadeSession.Flush(Domain, Stopwatch.StartNew(), 30, 256);
        }
        internal string Change(long Amount)
        { Player.Set(Scrap, Amount); Tick(); return DrainDeferred(); }
        public void Dispose()
        {
            Registrar.DuringPublish = null; Host.Dispose();
            Check(World.InventorySamples.WatchCount == 0 && World.InventorySamples.DemandCount == 0 &&
                World.InventorySamples.PendingCount == 0 && World.GameplayEvents.PendingCount == 0 &&
                World.GameplayEvents.RetainedBytes == 0, "public teardown refunds private and D23 ownership");
            World.InventorySamples.Dispose();
        }
    }

    private static void ScalarsAndCutoffs(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native, Acquire + "S.Count=0")) {
            Check(Value.Player.Scans == 0 && Value.World.InventorySamples.WatchCount == 0, "acquisition without scans or watches");
            Value.Execute(State + "assert(game.ApiVersion=='0.7.5-experimental'); for _,K in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(S.P[K]==nil) end; assert(type(S.I.GiveItem)=='function' and type(S.I.TakeItem)=='function')");
            Value.Execute(State + "local Current=S.P:GetInventory(); assert(Current==S.P:GetInventory()); assert(S.I:CountItem('scrap')==Current:CountItem('scrap') and S.I:CountItem('scrap')==3 and S.P:GetInventory():CountItem('scrap')==3); " +
                "assert(S.I:HasItem('scrap',3) and not S.I:HasItem('scrap',4)); assert(not pcall(function() S.I.ItemChanged=1 end)); " +
                "local Signal=S.I.ItemChanged; assert(Signal==S.I.ItemChanged); assert(not pcall(function() Signal.X=1 end)); " +
                "assert(not pcall(function() Signal:Connect(1) end)); assert(not pcall(function() Signal.Connect({},function() end) end)); " +
                "for _,K in {'ItemAdded','ItemRemoved','Changed'} do assert(not pcall(function() return S.I[K] end)) end; " + Connect);
            Check(Value.World.InventorySamples.WatchCount == 1 && Value.World.InventorySamples.DemandCount == 1, "public connection creates bounded source demand");
            Value.Player.Set(Value.Scrap, 8); Value.Tick(); Check(Value.DrainDeferred() == "", "first Tick establishes fresh subscription baseline");
            Check(Value.Change(10) == "changed scrap 8 10 2\n", "positive deferred delta");
            int Scans = Value.Player.Scans; ulong Accepted = Value.World.GameplayEvents.Accepted;
            Value.World.InventorySamples.Tick(Value.Frame); Value.World.FlushInventoryChanges();
            Check(Value.Player.Scans == Scans && Value.World.GameplayEvents.Accepted == Accepted, "duplicate actual-frame Tick/Flush cannot rescan or replay");
            Check(Value.Change(7) == "changed scrap 10 7 -3\n", "signed negative conversion uses tonumber");
            Check(Value.Change(7) == "", "zero difference has no callback");
            Value.Player.Clear(); Value.Player.Add(Value.Scrap, 7, 1); Value.Tick(); Check(Value.DrainDeferred() == "", "completed physical transfer zero total");
            Value.Player.Clear(); Value.Player.Add(Value.Scrap, 3); Value.Player.Add(Value.Scrap, 4, 2); Value.Tick();
            Check(Value.DrainDeferred() == "", "completed physical split zero total");
            Check(Value.Change(3) == "changed scrap 7 3 -4\n" && Value.Change(7) == "changed scrap 3 7 4\n", "explicit intermediate loss/restoration");
            Value.Player.Set(Value.Scrap, 17); Value.Player.Set(Value.Scrap, 7); Value.Tick();
            Check(Value.DrainDeferred() == "", "balanced gain/loss interval invisible");
            Value.Player.Set(Value.Scrap, 9); Value.Tick();
            Value.Execute(State + "S.Second=0; S.SecondConnection=S.I.ItemChanged:Connect(function(C) " + Context + "S.Second+=1 end)");
            Check(Value.DrainDeferred() == "changed scrap 7 9 2\n", "second subscriber cannot receive already captured work");
            Value.Player.Set(Value.Scrap, 11); Value.Tick(); Value.DrainDeferred();
            Value.Execute(State + "assert(S.Second==0)");
            Value.Player.Set(Value.Scrap, 12); Value.Tick(); Value.DrainDeferred();
            Value.Execute(State + "assert(S.Second==1); S.SecondConnection:Disconnect()");
            Value.Execute(State + "S.Connection:Disconnect(); " + Connect);
            Value.Player.Set(Value.Scrap, Runtime.FacadePolicy.MaxExactLuauInteger); Value.Tick(); Check(Value.DrainDeferred() == "", "boundary fresh baseline");
            Value.Player.Set(Value.Scrap, 0); Value.Tick(); Value.DrainDeferred();
            Value.Execute(State + "assert(S.Last.Before==9007199254740991 and S.Last.After==0 and S.Last.Delta==-9007199254740991)");
            Value.Player.Set(Value.Scrap, Runtime.FacadePolicy.MaxExactLuauInteger); Value.Tick(); Value.DrainDeferred();
            Value.Execute(State + "assert(S.Last.Before==0 and S.Last.After==9007199254740991 and S.Last.Delta==9007199254740991)");
            Value.Player.Add(Value.Scrap, 1, 1); Value.Tick(); Check(Value.DrainDeferred() == "", "sum at 2^53 invalidates sample");
            Value.Player.Set(Value.Scrap, 5); Value.Tick(); Check(Value.DrainDeferred() == "", "no fabricated bridge after unsafe integer");
            Check(Value.Change(6) == "changed scrap 5 6 1\n", "valid interval resumes");
        }
    }

    private static void MutationIntegration(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            var Grant = new PlayerInteractionFoundation1FBTests.Fixture();
            Value.Definitions["scrap"] = Grant.Definition;
            Value.Player.View.Inventory = Grant.Source; Value.Player.View.GiveInventory = Grant;
            Value.Player.View.TakeInventory = Grant.Take;
            Value.Tick(); Check(Value.DrainDeferred() == "", "C4 empty baseline before facade mutations");
            Value.Execute(State + "assert(S.I:GiveItem('scrap',2)); assert(S.I:GiveItem('scrap',3,GiveItemBehavior.InventoryOnly)); assert(S.Count==0 and S.I:CountItem('scrap')==5)");
            Value.Tick(); Check(Value.DrainDeferred() == "changed scrap 0 5 5\n", "Give through captured facade uses sampled deferred ItemChanged");
            Value.Execute(State + "assert(S.I:TakeItem('scrap',2)); assert(S.Count==1 and S.I:CountItem('scrap')==3)");
            Value.Tick(); Check(Value.DrainDeferred() == "changed scrap 5 3 -2\n", "Take through same facade uses existing signed context");
            Value.Execute(State + "assert(S.I:GiveItem('scrap',1,nil)); assert(S.I:TakeItem('scrap',1)); assert(not S.I:TakeItem('scrap',4)); assert(not S.I:TakeItem('missing',1)); assert(S.Count==2)");
            Value.Tick(); Check(Value.DrainDeferred() == "", "balanced Give/Take interval is invisible, no operation ledger");
            Grant.Failure = "reject";
            Value.Execute(State + "local Ok,Error=pcall(function() S.I:GiveItem('scrap',1) end); assert(not Ok and string.find(Error,'may have changed',1,true)); assert(S.I:CountItem('scrap')==3)");
            Value.Tick(); Check(Value.DrainDeferred() == "" && Grant.Cleanups == 1, "indeterminate rejected transfer cleans returned item, no invented delta");
            Grant.Failure = "insert-throw";
            Value.Execute(State + "local Ok,Error=pcall(function() S.I:GiveItem('scrap',1) end); assert(not Ok and string.find(Error,'may have changed',1,true)); assert(S.Count==2 and S.I:CountItem('scrap')==4)");
            Grant.Failure = ""; Value.Tick();
            Check(Value.DrainDeferred() == "changed scrap 3 4 1\n", "sampled physical change remains visible after indeterminate post-insert failure");
            Value.Player.View.TakeInventory = (Definition,Amount) => Grant.Take(Definition,Amount) - 1;
            Value.Execute(State + "local Ok,Error=pcall(function() S.I:TakeItem('scrap',1) end); assert(not Ok and string.find(Error,'may have changed',1,true)); assert(S.Count==3)");
            Value.Tick(); Check(Value.DrainDeferred() == "changed scrap 4 3 -1\n", "Take VERIFY mismatch remains error with independent sampled observation");
            Check(Value.World.TakeItems.BusyCount == 0 && Value.World.GiveItems.BusyCount == 0 &&
                Value.World.GiveItems.TrackedResources == 0, "C4 mutation/signal errors converge shared gate and temporary resources");
        }
    }
    private static void PublicationAndLifecycle(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Tick(); Check(Value.DrainDeferred() == "", "initial no replay");
            int Demands = Value.World.InventorySamples.DemandCount;
            Value.Execute("assert(not pcall(require,'nested')); assert(not pcall(require,'disconnectfail')); assert(not pcall(require,'coldfail'))");
            Check(Value.World.InventorySamples.DemandCount == Demands && Value.World.Active.ListenerCount == 1, "failed nested/forwarded modules synchronously restore owned registrations");
            Value.Execute(State + "assert(not pcall(function() return S.Leak.ItemChanged:Connect(function() end) end)); require('forward')()");
            Value.Tick(); Value.DrainDeferred();
            Check(Value.Change(4).Contains("forward\n"), "cached forward invocation publishes during committed execution");
            Value.Execute(State + "S.Forward:Disconnect()");
            var Previous = Value.World.Active;
            Value.Source = Entry + "error('failed root')";
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Value.World.Active == Previous, "failed root preserves committed subscriber");
            Check(Value.Change(5) == "changed scrap 4 5 1\n", "old root remains live after failure");
            Value.Registrar.DuringPublish = (Old, Next) => { if (Next != null) throw new InvalidOperationException("reject final publication"); };
            Value.Source = Entry;
            Check(Value.Host.Reload().Status != Runtime.RuntimeStatus.OK && Value.World.Active == Previous, "commit failure preserves old source demand");
            Value.Registrar.DuringPublish = null;
            Value.Player.Set(Value.Scrap, 6); Value.Tick(); Value.FlushNative();
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.OK && Previous.Disposed, "successful root retires native-held callback");
            Check(Value.DrainDeferred() == "", "replacement does not inherit old event");
            Value.Tick(); Check(Value.DrainDeferred() == "", "replacement baselines fresh");
            Check(Value.Change(7) == "changed scrap 6 7 1\n", "replacement future delta");
            Value.Player.Set(Value.Scrap, 8); Value.Tick(); Value.FlushNative();
            string OldToken = Value.Player.Lifetime.Token; Value.Player.View.Connected = false;
            Player Reconnected = Value.AddPlayer("42"); Reconnected.Set(Value.Scrap, 20);
            Check(Reconnected.Lifetime.Token != OldToken && Value.DrainDeferred() == "", "same UserId cannot retarget original queued connection");
            Value.Execute(State + "assert(not pcall(function() S.I:CountItem('scrap') end)); assert(not pcall(function() S.I.ItemChanged:Connect(function() end) end)); " +
                "S.P=game:GetService('Players'):GetPlayerByUserId('42'); S.I=S.P:GetInventory(); " + Connect);
            Value.Tick(); Check(Value.DrainDeferred() == "", "same-account reconnect baseline");
            Reconnected.Set(Value.Scrap, 21); Value.Tick(); Check(Value.DrainDeferred() == "changed scrap 20 21 1\n", "new exact connection future only");
            Reconnected.Set(Value.Scrap, 22); Value.Tick(); Value.FlushNative();
            var BeforeFatal = Value.World.Active; long Generation = Value.Host.VmGenerationId;
            Check(Value.Execute("while true do end", true).Status == Runtime.RuntimeStatus.TIMEOUT && Value.Host.Ready &&
                Value.Host.VmGenerationId != Generation && BeforeFatal.Disposed, "fatal generation reconstruction");
            Check(Value.DrainDeferred() == "", "fatal reconstruction no replay"); Value.Tick(); Check(Value.DrainDeferred() == "", "recovered baseline");
            Reconnected.Set(Value.Scrap, 23); Value.Tick(); Check(Value.DrainDeferred() == "changed scrap 22 23 1\n", "recovered future delta");
        }
        foreach (string Body in new[] {"error('callback')", "coroutine.yield()", "while true do end"}) using (var Value = new Fixture(Native)) {
            Value.Execute(State + "S.Fault=S.I.ItemChanged:Connect(function() " + Body + " end)"); Value.Tick(); Value.DrainDeferred();
            long Generation = Value.Host.VmGenerationId; Value.Player.Set(Value.Scrap, 4); Value.Tick();
            Check(Value.DrainDeferred(true).Contains("changed scrap 3 4 1\n"), "independent subscriber progresses beside callback fault");
            if (Body == "while true do end") {
                Check(Value.Host.Ready && Value.Host.VmGenerationId != Generation && Value.Host.Recoveries == 1, "callback timeout is fatal");
                Value.Tick(); Check(Value.DrainDeferred() == "", "callback fatal baseline no replay");
            } else {
                Value.Execute(State + "S.Fault:Disconnect(); S.Connection:Disconnect(); S.Connection:Disconnect()");
                Check(Value.World.InventorySamples.DemandCount == 0, "disconnect synchronously reclaims source demands");
            }
        }
    }

    private static byte[] Package(string Id, string Init, string Api = null, string Required = null)
    {
        using (var Bytes = new MemoryStream()) {
            using (var Zip = new ZipArchive(Bytes, ZipArchiveMode.Create, true)) {
                Action<string,string> Add = (Name, Source) => { using (var Writer = new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Source); };
                Add("addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" +
                    (Api == null ? "" : ",\"publicModules\":[\"api\"]") +
                    (Required == null ? "" : ",\"dependencies\":{\"required\":[\"" + Required + "\"],\"optional\":[]}") + "}");
                Add("init.luau", Init); if (Api != null) Add("api.luau", Api);
            } return Bytes.ToArray();
        }
    }
    private static void Process(Runtime.AddonRegistry Registry)
    { int Guard = 256; while (Registry.HasPending && Guard-- > 0) Registry.ProcessOne(); Check(!Registry.HasPending, "bounded addon convergence"); }
    private static string Subscription(string Marker, string Id = "42")
    { return "game:GetService('Players'):GetPlayerByUserId('" + Id + "'):GetInventory().ItemChanged:Connect(function(C) " + Context + "print('" + Marker + "') end)"; }

    private static void Addons(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            object Owner = new object(), Consumer = new object();
            string[] Registration = Registry.RegisterArchive(Owner, Package("c3owner", Subscription("owner"),
                "local I=game:GetService('Players'):GetPlayerByUserId('42'):GetInventory(); return {I=I,Connect=function() return I.ItemChanged:Connect(function(C) " + Context + "print('foreign') end) end}"));
            Check(Registration[0] == "OK", "owner registration"); Process(Registry);
            Check(Registry.Status(Owner, Registration[1])[2] == "Active", "owner activation");
            string[] Failed = Registry.RegisterArchive(Consumer, Package("c3failed", "require('@c3owner/api').Connect(); error('failed foreign candidate')", null, "c3owner"));
            Process(Registry); Check(Registry.Status(Consumer, Failed[1])[2] == "Failed" && Value.World.InventorySamples.DemandCount == 2, "failed foreign provisional call cannot publish into active owner");
            string[] Success = Registry.RegisterArchive(Consumer, Package("c3consumer", "local A=require('@c3owner/api'); A.Connect(); A.I.ItemChanged:Connect(function(C) " + Context + "print('shared') end); return A", null, "c3owner"));
            Process(Registry); Check(Registry.Status(Consumer, Success[1])[2] == "Active", "shared defining-owner facade publication");
            Value.Tick(); Value.DrainDeferred(); string Logs = Value.Change(4);
            Check(Logs.Contains("owner\n") && Logs.Contains("foreign\n") && Logs.Contains("shared\n"), "published root/addon/shared callbacks coexist");
            Registry.ReplaceArchive(Owner, Registration[1], Package("c3owner", Subscription("bad") + "; error('replacement')", "return {}")); Process(Registry);
            Check(Value.Change(5).Contains("owner\n"), "failed addon replacement preserves original subscribers");
            Value.Player.Set(Value.Scrap, 6); Value.Tick(); Value.FlushNative();
            Registry.ReplaceArchive(Owner, Registration[1], Package("c3owner", Subscription("replacement"), "return {}")); Process(Registry);
            Logs = Value.DrainDeferred(); Check(!Logs.Contains("owner\n") && !Logs.Contains("foreign\n") && !Logs.Contains("shared\n"), "addon replacement cancels original owner and required consumers");
            Value.Tick(); Value.DrainDeferred(); Check(Value.Change(7).Contains("replacement\n"), "new addon observes future interval");
            Value.Player.Set(Value.Scrap, 8); Value.Tick(); Value.FlushNative(); Registry.UnloadProvider(Owner);
            Logs = Value.DrainDeferred(); Check(!Logs.Contains("replacement\n"), "provider unload cancels native-held callback");
            Registry.UnloadProvider(Consumer); Check(Value.World.InventorySamples.DemandCount == 1, "providers release all addon source capacity");
        }
    }

    private static void FaultsAndQueues(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Tick(); Value.DrainDeferred();
            Value.World.InventorySamples.AllocationAdmission = () => false; Value.Player.Set(Value.Scrap, 5); Value.Tick();
            Check(Value.DrainDeferred() == "", "sample allocation failure has no callback");
            Value.World.InventorySamples.AllocationAdmission = null; Value.Tick(); Check(Value.DrainDeferred() == "", "allocation failure requires new baseline");
            Check(Value.Change(6) == "changed scrap 5 6 1\n", "allocation fault resumes future only");
            Value.World.InventorySamples.AllocationAdmission = () => false;
            Value.Execute(State + "assert(not pcall(function() S.I.ItemChanged:Connect(function() end) end))");
            Value.World.InventorySamples.AllocationAdmission = null;
            Check(Value.World.InventorySamples.DemandCount == 1 && Value.World.Active.ListenerCount == 1, "failed registration leaves no callback/demand");
            Value.World.InventorySamples.PumpAvailable = () => false;
            Value.Execute(State + "assert(not pcall(function() S.I.ItemChanged:Connect(function() end) end))");
            Value.World.InventorySamples.PumpAvailable = null;
            Check(Value.World.InventorySamples.DemandCount == 1, "unavailable source pump rejects public registration");
            Value.Player.ThrowRead = true; Value.Tick(); Check(Value.DrainDeferred() == "", "host read failure");
            Value.Player.ThrowRead = false; Value.Player.Set(Value.Scrap, 10); Value.Tick(); Check(Value.DrainDeferred() == "", "host read failure not bridged");
            Value.Player.Clear(); for (int Index=0; Index<129; Index++) Value.Player.Add(Value.Scrap, 1);
            Value.Tick(); Check(Value.DrainDeferred() == "", "129 stacks rejected");
            Value.Player.Set(Value.Scrap, 10); Value.Tick(); Check(Value.DrainDeferred() == "", "stack overflow resets baseline");
            Value.Frame++; for (int Index=0; Index<4096; Index++) Check(Value.World.GameplayEvents.Visit(), "shared visit charge");
            int Scans = Value.Player.Scans; Value.World.InventorySamples.Tick(Value.Frame); Value.World.FlushInventoryChanges();
            Check(Value.Player.Scans == Scans, "shared D23 exhaustion prevents physical scan");
        }
        using (var Value = new Fixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            object[] Items = new object[32]; for (int Index=0; Index<32; Index++) Items[Index] = Value.Item("c3." + Index.ToString("D2"));
            Value.Player.Clear(); foreach (var Item in Items) Value.Player.Add(Item, 1); Value.Tick(); Value.DrainDeferred();
            for (int Turn=0; Turn<9; Turn++) {
                Value.Player.Clear(); foreach (var Item in Items) Value.Player.Add(Item, Turn+2); Value.Tick(false);
                Check(Value.World.InventorySamples.PendingCount == Math.Min(256,(Turn+1)*32), "32-record and 256-pending exact private ceilings");
            }
            Check(Value.World.InventorySamples.Dropped > 0, "private saturation drops counted");
            Value.Frame++; Value.World.FlushInventoryChanges(); Value.DrainDeferred(false, true);
            // Drain bounded old records over fresh frames, without generating differences.
            for (int Turn=0; Turn<16 && Value.World.InventorySamples.PendingCount!=0; Turn++) { Value.Tick(); Value.DrainDeferred(false, true); }
            Check(Value.World.InventorySamples.PendingCount == 0, "private queue converges within bounded pumps");
            Value.Tick(); Check(Value.DrainDeferred() == "", "private loss advances baseline without replay");
            // The native per-domain queue caps at256. A real second domain is
            // required to reach the shared512 reservation ceiling without drain.
            object Provider = new object();
            string[] Registration = Registry.RegisterArchive(Provider, Package("c3ledger",
                "game:GetService('Players').PlayerDied:Connect(function() end)"));
            Check(Registration[0] == "OK", "second native-bound ledger domain registration"); Process(Registry);
            Check(Registry.Status(Provider, Registration[1])[2] == "Active" && Value.Host.DomainCount == 2,
                "lifecycle reservations use a real addon domain beside inventory root");
            for (int Turn=0; Turn<9; Turn++) {
                Value.Player.Clear(); foreach (var Item in Items) Value.Player.Add(Item, Turn+20); Value.Tick(); Value.FlushNative();
                Check(Value.World.InventorySamples.PendingCount == Math.Min(256,(Turn+1)*32), "combined buffered and native-held inventory ceiling256");
            }
            Check(Value.World.GameplayEvents.PendingCount == 256 && Value.Host.SchedulerSnapshot.Queued == 256,
                "native-held inventory retains source charge until code39 release");
            for (int Turn=0; Turn<8; Turn++) {
                Value.Frame++;
                for (int Index=0; Index<128; Index++) {
                    Check(Value.World.GameplayEvents.Capture("died"), "mixed source capture while inventory holds256");
                    Value.World.GameplayEvent("died", Value.Player.Lifetime.Token, "42", Value.Player.View.Name, null);
                }
                Value.FlushNative();
            }
            Check(Value.World.GameplayEvents.PendingCount == 512 && Value.World.InventorySamples.PendingCount == 256,
                "shared512 D23 reservations combine256 root inventory and256 addon lifecycle callbacks");
            Check(Value.Host.SchedulerSnapshot.Queued == 512, "two native domain queues retain256 callbacks each");
            Value.Frame++; Check(Value.World.GameplayEvents.Capture("died"), "full-ledger capture");
            Value.World.GameplayEvent("died", Value.Player.Lifetime.Token, "42", Value.Player.View.Name, null);
            Check(Value.World.GameplayEvents.QueueRejected > 0, "full shared ledger rejects without overcharging either source");
            Value.DrainDeferred(); Value.Tick(); Check(Value.DrainDeferred() == "", "D23 dropped inventory interval never replayed");
            Check(Registry.UnloadProvider(Provider) == 1, "second ledger domain cleanup");
        }
        using (var Value = new Fixture(Native)) {
            Value.Execute("game:GetService('Players').PlayerDied:Connect(function() print('mixed died') end)"); Value.Tick(); Value.DrainDeferred();
            Value.Frame++;
            for (int Index=0; Index<128; Index++) {
                Check(Value.World.GameplayEvents.Capture("died"), "mixed captures");
                Value.World.GameplayEvent("died", Value.Player.Lifetime.Token, "42", Value.Player.View.Name, null);
            }
            Value.Player.Set(Value.Scrap, 4); Value.World.InventorySamples.Tick(Value.Frame); Value.World.FlushInventoryChanges();
            Check(Value.World.GameplayEvents.PendingCount == 32, "mixed kinds share D23 per-domain fanout32");
            Value.DrainDeferred(); Value.Tick(); Check(Value.DrainDeferred() == "", "mixed-kind rejection does not replay inventory");
            Check(Value.Change(5) == "changed scrap 4 5 1\n", "inventory resumes after mixed saturation");
        }
    }

    private static void Transport(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Tick(); Value.DrainDeferred(); Value.Player.Set(Value.Scrap, 4); Value.Tick();
            byte[] Payload = null;
            var Pending = (IEnumerable)typeof(Runtime.FacadeSession).GetField("Pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Value.World.Active);
            foreach (object Record in Pending) {
                Payload = (byte[])Record.GetType().GetField("Payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Record); break;
            }
            Check(Payload != null, "real public intake payload");
            string[] Fields = Runtime.FacadePolicy.Unpack(Payload);
            Check(Fields.Length == 12 && Fields[0] == "itemchanged" && Fields[2] == Value.Player.Lifetime.Token &&
                Fields[3] == "42" && Fields[4] == "scrap" && Fields[6] == "3" && Fields[7] == "4" &&
                Fields[8] == "1" && Fields[9] != "" && Fields[10] != "" && Fields[11] == "", "exact private twelve-field inventory protocol");
            Value.FlushNative();
            Check(Value.World.GameplayEvents.Validate(Value.World.Active, Fields), "fresh exact-owner native admission gate");
            Check(Value.World.InventorySamples.PendingCount == 1 && Value.World.GameplayEvents.PendingCount == 1,
                "native transfer preserves one reservation in both ledgers");
            for (int Index=1; Index<12; Index++) {
                string[] Tampered = (string[])Fields.Clone(); Tampered[Index] = "tampered";
                Check(!Value.World.GameplayEvents.Validate(Value.World.Active, Tampered), "each exact field independently gates admission: " + Index);
            }
            string[] Extra = new string[13]; Array.Copy(Fields, Extra, Fields.Length); Extra[12] = "";
            Check(!Value.World.GameplayEvents.Validate(Value.World.Active, Extra), "extra protocol field rejects");
            Value.Execute(State + "S.Connection:Disconnect()");
            Check(Value.World.InventorySamples.WatchCount == 0 && Value.World.InventorySamples.DemandCount == 0 &&
                Value.World.InventorySamples.PendingCount == 1 && Value.World.GameplayEvents.PendingCount == 1,
                "native-held cancelled source remains charged until acknowledgement");
            Check(!Value.World.GameplayEvents.Validate(Value.World.Active, Fields) && Value.DrainDeferred() == "", "disconnect gate discards original native callback without replay");
        }
    }

    private static void BoundsAndScale(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native, Acquire + "S.Count=0")) {
            var Players = new List<Player> {Value.Player};
            for (int Index=1; Index<65; Index++) { var Player = Value.AddPlayer((42+Index).ToString(CultureInfo.InvariantCulture)); Player.Set(Value.Scrap, 3); Players.Add(Player); }
            Value.Execute("local P=game:GetService('Players'); for Id=42,105 do P:GetPlayerByUserId(tostring(Id)):GetInventory().ItemChanged:Connect(function(C) " + Context + "end) end; " +
                "assert(not pcall(function() P:GetPlayerByUserId('106'):GetInventory().ItemChanged:Connect(function() end) end))");
            Check(Value.World.InventorySamples.WatchCount == 64 && Value.World.InventorySamples.DemandCount == 64, "exact64 public watches");
            for (int Turn=0; Turn<16; Turn++) { Value.Tick(); Check(Value.World.InventorySamples.LastScans == 4, "four actual-frame scans"); Value.DrainDeferred(); }
            for (int Index=0; Index<64; Index++) Check(Players[Index].Scans == 1, "64 watches revisited within16 uncontended pumps");
            Check(Players[64].Scans == 0, "rejected watch never scans");
        }
        using (var Value = new Fixture(Native, Acquire + "S.Count=0")) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            Value.Execute(State + "for Index=1,64 do S.I.ItemChanged:Connect(function() end) end");
            object Provider = new object(); string[] Registration = Registry.RegisterArchive(Provider, Package("c3capacity",
                "local I=game:GetService('Players'):GetPlayerByUserId('42'):GetInventory(); for Index=1,64 do I.ItemChanged:Connect(function() end) end")); Process(Registry);
            Check(Registry.Status(Provider, Registration[1])[2] == "Active" && Value.World.InventorySamples.DemandCount == 128, "128 demands across actual domains");
            Value.Execute(State + "assert(not pcall(function() S.I.ItemChanged:Connect(function() end) end))");
            Check(Value.World.InventorySamples.DemandCount == 128 && Value.World.InventorySamples.WatchCount == 1, "129th demand fails without leaking slot");
            Registry.UnloadProvider(Provider); Check(Value.World.InventorySamples.DemandCount == 64, "provider releases64 demands");
        }
        using (var Value = new Fixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            Value.Execute(State + "for Index=1,27 do S.I.ItemChanged:Connect(function(C) " + Context + "end) end");
            object[] Providers = {new object(), new object(), new object(), new object()}; var Names = new HashSet<string>(); var Tokens = new List<string>();
            for (int Index=0; Index<100; Index++) {
                string Name = "c3a" + Index.ToString("D3"); Names.Add(Name);
                string[] Registration = Registry.RegisterArchive(Providers[Index/25], Package(Name, Subscription(Name))); Tokens.Add(Registration[1]); Process(Registry);
                Check(Registry.Status(Providers[Index/25], Registration[1])[2] == "Active", "100 real addons activation");
            }
            Check(Value.Host.DomainCount == 101 && Native.LiveVmCount == 1 && Value.World.InventorySamples.WatchCount == 1 &&
                Value.World.InventorySamples.DemandCount == 128, "100 addons share one exact watch and one VM");
            Value.Tick(); Value.DrainDeferred(); var Progress = new HashSet<string>();
            for (int Turn=0; Turn<16; Turn++) foreach (string Line in Value.Change(Turn+4).Split('\n')) if (Names.Contains(Line)) Progress.Add(Line);
            Check(Progress.SetEquals(Names) && Value.World.InventorySamples.Dropped > 0, "bounded lossy saturation rotates across100 addons");
            // Four pumps give every listener a native-held record, then provider retirement gates it.
            for (int Turn=0; Turn<4; Turn++) { Value.Player.Set(Value.Scrap, Turn+30); Value.Tick(); Value.FlushNative(); }
            Registry.UnloadProvider(Providers[0]); string Logs = Value.DrainDeferred();
            for (int Index=0; Index<25; Index++) Check(!Logs.Contains("c3a" + Index.ToString("D3") + "\n"), "provider unload cancels native-held addon callback");
            Registry.ReplaceArchive(Providers[1], Tokens[25], Package("c3a025", Subscription("new025"))); Process(Registry);
            Value.Tick(); Value.DrainDeferred();
            foreach (object Provider in Providers) Registry.UnloadProvider(Provider);
            Check(Registry.Count == 0 && Value.World.InventorySamples.DemandCount == 28, "100-addon cleanup retains only root");
        }
    }

    private static void FairnessRegressions(Runtime.NativeRuntime Native)
    {
        // Exhaust all32 records on one subscriber of one watch on every pump.
        // A global demand cursor previously revisited A1/B1 and starved A2/B2.
        using (var Value = new Fixture(Native, Acquire + "S.Count=0")) {
            Player Other = Value.AddPlayer("43");
            object[] Items = new object[32];
            for (int Index=0; Index<Items.Length; Index++) Items[Index] = Value.Item("fair." + Index.ToString("D2"));
            Value.Player.Clear(); Other.Clear();
            foreach (object Item in Items) { Value.Player.Add(Item, 1); Other.Add(Item, 1); }
            Value.Execute("local P=game:GetService('Players'); local A=P:GetPlayerByUserId('42'):GetInventory(); " +
                "local B=P:GetPlayerByUserId('43'):GetInventory(); " +
                "A.ItemChanged:Connect(function(C) " + Context + "print('A1') end); " +
                "A.ItemChanged:Connect(function(C) " + Context + "print('A2') end); " +
                "B.ItemChanged:Connect(function(C) " + Context + "print('B1') end); " +
                "B.ItemChanged:Connect(function(C) " + Context + "print('B2') end)");
            Check(Value.World.InventorySamples.WatchCount == 2 && Value.World.InventorySamples.DemandCount == 4,
                "two distinct watch slots with two public subscribers each");
            Value.Tick(); Check(Value.DrainDeferred() == "", "both watches baseline without replay");
            var Counts = new Dictionary<string,int> {{"A1",0},{"A2",0},{"B1",0},{"B2",0}};
            for (int Turn=0; Turn<8; Turn++) {
                Value.Player.Clear(); Other.Clear();
                foreach (object Item in Items) { Value.Player.Add(Item, Turn+2); Other.Add(Item, Turn+2); }
                Value.Tick();
                Check(Value.World.GameplayEvents.PendingCount == 32 && Value.World.InventorySamples.PendingCount == 32,
                    "each saturated watch turn admits exactly32 public records");
                int Delivered = 0;
                foreach (string Line in Value.DrainDeferred().Split('\n')) if (Counts.ContainsKey(Line)) { Counts[Line]++; Delivered++; }
                Check(Delivered == 32, "all32 records reach real native callbacks");
            }
            foreach (var Count in Counts) Check(Count.Value >= 32, "per-watch fanout gives bounded progress to " + Count.Key);
            Check(Value.World.InventorySamples.Dropped > 0, "fairness under intentional lossy record saturation");
        }
        using (var Value = new Fixture(Native, "return true")) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            object Provider = new object();
            string[] Listening = Registry.RegisterArchive(Provider, Package("c3orderlisten", Subscription("order addon")));
            Check(Listening[0] == "OK", "ordering addon registration"); Process(Registry);
            string[] EmptyLow = Registry.RegisterArchive(Provider, Package("c3orderemptylow", "return true")); Process(Registry);
            Check(Registry.Status(Provider, Listening[1])[2] == "Active" && Registry.Status(Provider, EmptyLow[1])[2] == "Active",
                "lower addon domains active before root replacement");
            long AddonDomain = Int64.Parse(Registry.Status(Provider, Listening[1])[7], CultureInfo.InvariantCulture);
            long EmptyLowDomain = Int64.Parse(Registry.Status(Provider, EmptyLow[1])[7], CultureInfo.InvariantCulture);
            var Previous = Value.World.Active; Value.Source = Subscription("order root");
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.OK && Previous.Disposed, "root replacement after addon activation");
            long RootDomain = Value.World.Active.DomainLifetimeId;
            string[] EmptyHigh = Registry.RegisterArchive(Provider, Package("c3orderemptyhigh", "return true")); Process(Registry);
            Check(Registry.Status(Provider, EmptyHigh[1])[2] == "Active", "empty higher domain activation");
            long EmptyHighDomain = Int64.Parse(Registry.Status(Provider, EmptyHigh[1])[7], CultureInfo.InvariantCulture);
            Check(RootDomain > AddonDomain && RootDomain > EmptyLowDomain && EmptyHighDomain > RootDomain &&
                Value.Host.DomainCount == 4 && Value.World.InventorySamples.DemandCount == 2,
                "root has higher identity than old addons, with empty sessions around it");
            Value.Tick(); Check(Value.DrainDeferred() == "", "new root and addon establish their first samples");
            Value.Player.Set(Value.Scrap, 4); Value.Tick(false);
            Check(Value.World.InventorySamples.PendingCount == 2, "one real buffered record per active subscriber");
            Value.Frame++; Value.World.FlushInventoryChanges();
            long Cursor = (long)typeof(Runtime.FacadeWorld).GetField("InventoryDeliveryCursor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Value.World);
            Check(Cursor == RootDomain && Value.World.GameplayEvents.PendingCount == 2,
                "sorted traversal ends at the last domain that actually consumed records, not an empty session");
            string Logs = Value.DrainDeferred();
            Check(Logs.Contains("order addon\n") && Logs.Contains("order root\n"), "older addon and replacement root both deliver through native callbacks");
            for (int Turn=0; Turn<4; Turn++) {
                Value.Player.Set(Value.Scrap, Turn+5); Value.Tick(); Logs = Value.DrainDeferred();
                Check(Logs.Contains("order addon\n") && Logs.Contains("order root\n"), "mixed domain identities retain progress across repeated pumps");
            }
            Check(Registry.UnloadProvider(Provider) == 3 && Value.World.InventorySamples.DemandCount == 1, "ordering regression addon cleanup");
        }
        Console.WriteLine("[CarbonLuau:InventoryChangedFairness] PASS two-watch/four-subscriber record saturation, replacement-root domain ordering and empty-session cursor stability");
    }

    private static void MixedSources(Runtime.NativeRuntime Native)
    {
        // Reuse B1's synthetic exact-lifetime host. B2/B3 certificates below
        // contain only scalar completion proof; this is public transport/VM
        // qualification, not Rust completion or authenticated-client evidence.
        var Entities = new EntitySpawnSignalTests.EntityHost();
        try {
            using (var Value = new Fixture(Native, Acquire + "S.Count=0")) {
                Value.World.Entities = Entities;
                Value.World.GameplayAvailable = Kind => Entities.Ready;
                Player Killer = Value.AddPlayer("43");
                Value.Execute(State + "S.Mixed={}; S.Killer=game:GetService('Players'):GetPlayerByUserId('43'); " +
                    "local function Keys(C,Allowed,Expected) assert(type(C)=='table' and table.isfrozen(C)); " +
                    "local N=0; for K in C do assert(Allowed[K]); N+=1 end; assert(N==Expected) end; " +
                    "local function Original(C) assert(C.Killer==S.Killer and C.KillerId=='43'); " +
                    "assert(not C.Killer.IsConnected and C.Killer.Name=='C3 model connection'); " +
                    "assert(C.Killer~=game:GetService('Players'):GetPlayerByUserId('43')) end; " +
                    "local P=game:GetService('Players'); local W=game:GetService('Workspace'); " +
                    "S.Connection=S.I.ItemChanged:Connect(function(C) " + Context +
                    "assert(C.Item=='scrap' and C.Before==3 and C.After==4 and C.Delta==1); S.Count+=1; S.Mixed.Inventory=C; print('mixed inventory') end); " +
                    "P.PlayerDied:Connect(function(Player,C) assert(Player==S.P and Player.UserId=='42'); " +
                    "Keys(C,{Position=true,Killer=true,KillerId=true},3); Original(C); assert(C.Position==Vector3.new(1,2,3)); " +
                    "assert(not pcall(function() C.KillerId='replacement' end)); S.Mixed.PlayerDied=C; print('mixed player died') end); " +
                    "P.PlayerSpawned:Connect(function(Player,C) assert(Player==S.P and Player.UserId=='42'); " +
                    "Keys(C,{Position=true},1); assert(C.Position==Vector3.new(1,2,3)); " +
                    "assert(not pcall(function() C.Position=nil end)); S.Mixed.PlayerSpawned=C; print('mixed player spawned') end); " +
                    "W.EntitySpawned:Connect(function(E) assert(E.Id=='700' and E.Prefab=='assets/fixture.prefab' and E.Position==Vector3.new(1,2,3)); " +
                    "assert(not pcall(function() E.Id='replacement' end)); S.Mixed.EntitySpawned=E; print('mixed entity spawned') end); " +
                    "W.EntityDied:Connect(function(C) Keys(C,{Id=true,Prefab=true,Position=true,Killer=true,KillerId=true,DamageType=true,Weapon=true},7); " +
                    "assert(C.Id=='702' and C.Prefab=='assets/fixture.prefab' and C.Position==Vector3.new(1,2,3)); Original(C); " +
                    "assert(C.DamageType=='Bullet' and C.Weapon=='assets/weapon.prefab'); " +
                    "assert(not pcall(function() C.Id='replacement' end)); S.Mixed.EntityDied=C; print('mixed entity died') end); " +
                    "W.EntityDestroyed:Connect(function(C) Keys(C,{Id=true,Prefab=true,Position=true},3); " +
                    "assert(C.Id=='701' and C.Prefab=='assets/fixture.prefab' and C.Position==Vector3.new(1,2,3)); " +
                    "assert(not pcall(function() C.Id='replacement' end)); S.Mixed.EntityDestroyed=C; print('mixed entity destroyed') end)");
                Check(Value.World.Active.ListenerCount == 6 && Value.World.InventorySamples.DemandCount == 1,
                    "six public mixed subscriptions with one inventory source demand");
                Value.Tick(); Check(Value.DrainDeferred() == "", "mixed registration/baseline creates no history");
                var Entity = new EntitySpawnSignalTests.ObjectState {Id=700}; Entities.Objects[700] = Entity;
                var Attempt = Entities.Model.BeginSpawn(Entity);
                Check(Entities.Model.CompleteSpawn(Attempt, true, true), "synthetic B1 exact completion certificate");
                int DeathChecks = 0, RemovalChecks = 0;
                var Position = new Runtime.PlayerPosition(1,2,3);
                var Death = new Runtime.GameplayEntityDiedObservation("702", "assets/fixture.prefab", 702, Position,
                    () => { DeathChecks++; return true; }, Killer.Lifetime.Token, "43", Killer.Lifetime.Name, "Bullet", "assets/weapon.prefab");
                var Removal = new Runtime.GameplayEntityDestroyedObservation("701", "assets/fixture.prefab", 701, Position,
                    () => { RemovalChecks++; return true; });
                ulong Accepted = Value.World.GameplayEvents.Accepted, Released = Value.World.GameplayEvents.Released;
                int SourceCharge = Value.World.InventorySamples.ChargedBytes;
                Value.Player.Set(Value.Scrap, 4); Value.Tick();
                Action EmitOthers = () => {
                    Check(Value.World.GameplayEvents.Capture("died"), "mixed A death capture");
                    Value.World.GameplayEvent("died", Value.Player.Lifetime.Token, "42", Value.Player.Lifetime.Name, Position,
                        Killer.Lifetime.Token, "43", Killer.Lifetime.Name);
                    Check(Value.World.GameplayEvents.Capture("spawned"), "mixed A spawn capture");
                    Value.World.GameplayEvent("spawned", Value.Player.Lifetime.Token, "42", Value.Player.Lifetime.Name, Position);
                    Check(Value.World.GameplayEvents.Capture("entityspawned"), "mixed B1 capture");
                    Value.World.EntitySpawned(77, Session => Entities.Capture(Session, Entity));
                    Check(Value.World.GameplayEvents.Capture("entitydied"), "mixed B2 capture"); Value.World.EntityDied(Death);
                    Check(Value.World.GameplayEvents.Capture("entitydestroyed"), "mixed B3 capture"); Value.World.EntityDestroyed(Removal);
                };
                EmitOthers();
                string[] Kinds = {"itemchanged","died","spawned","entityspawned","entitydied","entitydestroyed"};
                var Seen = new HashSet<string>(); int Charge = 0;
                var Pending = (IEnumerable)typeof(Runtime.FacadeSession).GetField("Pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Value.World.Active);
                foreach (object Record in Pending) {
                    byte[] Payload = (byte[])Record.GetType().GetField("Payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Record);
                    string[] Fields = Runtime.FacadePolicy.Unpack(Payload); Charge += Payload.Length * 2;
                    Check(Seen.Add(Fields[0]) && Fields.Length == (Fields[0] == "entitydied" ? 14 : 12) &&
                        Payload.Length <= Runtime.GameplayEventPolicy.PayloadBytes, "mixed exact12/14 payload shapes and per-payload cap");
                    if (Fields[0] == "died" || Fields[0] == "spawned" || Fields[0] == "itemchanged")
                        Check(Fields[2] == Value.Player.Lifetime.Token && Fields[3] == "42", "mixed original source Player token/UserId");
                    if (Fields[0] == "entitydied") Check(Fields[9] == Killer.Lifetime.Token && Fields[10] == "43" &&
                        Fields[12] == "Bullet" && Fields[13] == "assets/weapon.prefab", "mixed B2 original attribution scalars");
                }
                Check(Seen.SetEquals(Kinds) && Value.World.GameplayEvents.Accepted-Accepted == 6 &&
                    Value.World.GameplayEvents.PendingCount == 6 && Value.World.GameplayEvents.RetainedBytes == Charge &&
                    Charge <= Runtime.GameplayEventPolicy.RetainedTransportBytes && Value.World.InventorySamples.PendingCount == 1 &&
                    Value.World.InventorySamples.PendingCount <= Runtime.InventoryNetObservationPolicy.Pending &&
                    Value.World.InventorySamples.ChargedBytes == SourceCharge+2048, "all mixed kinds share D23 reservations while only inventory retains source charge");
                Value.Execute(State + "assert(S.Count==0 and next(S.Mixed)==nil)");
                Value.FlushNative();
                Check(Value.Host.SchedulerSnapshot.Queued == 6 && Value.World.GameplayEvents.PendingCount == 6 &&
                    Value.World.GameplayEvents.RetainedBytes == Charge && Value.World.InventorySamples.PendingCount == 1,
                    "six simultaneous actual native queue entries preserve both ledgers before callbacks");
                Killer.View.Connected = false;
                Check(Value.Players.Disconnect("43", Killer.View.Identity) == Killer.Lifetime, "retire original captured killer connection");
                Player Replacement = Value.AddPlayer("43");
                Check(Replacement.Lifetime.Token != Killer.Lifetime.Token, "same-ID replacement has a fresh D11 token");
                string Logs = Value.DrainDeferred();
                Check(Logs == "mixed inventory\nmixed player died\nmixed player spawned\nmixed entity spawned\nmixed entity died\nmixed entity destroyed\n",
                    "all six typed public callbacks deliver once in intake order through the shared native queue");
                Value.Execute(State + "local N=0; for _ in S.Mixed do N+=1 end; assert(N==6 and S.Count==1); " +
                    "assert(S.Mixed.PlayerDied.Killer==S.Killer and S.Mixed.EntityDied.Killer==S.Killer)");
                Check(DeathChecks > 0 && RemovalChecks > 0 && Entities.Lookups == 0 &&
                    Value.World.GameplayEvents.Released-Released == 6 && Value.World.InventorySamples.ChargedBytes == SourceCharge,
                    "fresh synthetic completion gates, no entity-ID retarget lookup and exact refunds for every kind");

                // Shared quotas are exercised across the same six live subscriptions.
                // Capture-only quota probes do not create extra callbacks or history.
                Value.Frame++;
                for (int Index=0; Index<Runtime.GameplayEventPolicy.CapturesPerFrame; Index++)
                    Check(Value.World.GameplayEvents.Capture(Kinds[Index % Kinds.Length]), "mixed streams share one128 capture allowance");
                foreach (string Kind in Kinds) Check(!Value.World.GameplayEvents.Capture(Kind), "shared producer cap rejects " + Kind);
                Value.Frame++;
                for (int Index=0; Index<Runtime.GameplayEventPolicy.VisitsPerFrame; Index++)
                    Check(Value.World.GameplayEvents.Visit(), "mixed shared visit allowance");
                int Scans = Value.Player.Scans;
                Value.World.InventorySamples.Tick(Value.Frame); Value.World.FlushInventoryChanges(); EmitOthers();
                Check(Value.Player.Scans == Scans && !Value.Host.HasWork && Value.World.GameplayEvents.PendingCount == 0 &&
                    Value.World.GameplayEvents.RetainedBytes == 0 && Value.World.InventorySamples.PendingCount == 0,
                    "common4096 visit exhaustion blocks inventory scanning and A/B1/B2/B3 fanout without orphaned reservations");
            }
        } finally { Entities.Model.Dispose(); }
        Console.WriteLine("[CarbonLuau:InventoryChangedMixed] PASS simultaneous ItemChanged/A/B1/B2/B3 actual native delivery, strict contexts/intake order, original D11 attribution,12/14 payloads, shared capture/visit limits and exact refunds; synthetic completion certificates");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        ScalarsAndCutoffs(Native); MutationIntegration(Native); PublicationAndLifecycle(Native); Addons(Native); FaultsAndQueues(Native); Transport(Native); BoundsAndScale(Native); FairnessRegressions(Native); MixedSources(Native);
        Check(Native.LiveVmCount == 0, "real VM fixture cleanup");
        Console.WriteLine("[CarbonLuau:InventoryChangedTest] PASS real VM sampled immutable context, exact integers, publication/cutoffs, D11/domain retirement, recovery, shared bounds and100 addons; synthetic physical source");
    }
    internal static void Run(string Library, string Compiler)
    {
        string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CarbonLuauInventoryChanged-" + Guid.NewGuid().ToString("N")));
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid); Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(Library, Target);
            string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, Worker); if (Rid == "linux-x64") Check(chmod(Worker,493) == 0, "compiler executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) RunNative(Native);
        } finally {
            string Temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Check(Root.StartsWith(Temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(Root).StartsWith("CarbonLuauInventoryChanged-", StringComparison.Ordinal), "confined fixture cleanup");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
