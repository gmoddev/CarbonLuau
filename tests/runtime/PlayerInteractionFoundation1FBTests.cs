using System;
using System.Collections.Generic;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class PlayerInteractionFoundation1FBTests
{
    internal sealed class Resource
    {
        internal ulong Id;
        internal int Amount, Slot;
        internal object Parent;
        internal object Definition;
        internal Runtime.InventoryResourceState State = Runtime.InventoryResourceState.Temporary;
    }
    internal sealed class Fixture : Runtime.IInventoryGrantHost
    {
        internal readonly object Definition = new object();
        internal readonly object[] Containers = {new object(), new object(), new object()};
        internal readonly List<Resource> Resources = new List<Resource>();
        internal readonly int[] Capacity = {64, 32, 32};
        internal int Limit = 10, Creates, Cleanups, Scans, Transfers;
        internal bool Current = true;
        internal string Failure = "";
        internal Action DuringTransfer, DuringTake;
        internal ulong NextId;
        internal Runtime.PlayerView View()
        { return new Runtime.PlayerView {Identity = this, Connection = this, UserId = "76561198000000001", Name = "Grant fixture", Connected = Current,
            Inventory = Source, GiveInventory = this, TakeInventory = Take, Permission = Name => true}; }
        internal Resource Add(int Container, int Slot, int Amount)
        {
            var Result = new Resource {Id = ++NextId, Parent = Containers[Container], Slot = Slot, Amount = Amount, State = Runtime.InventoryResourceState.Accepted};
            Resources.Add(Result); return Result;
        }
        internal Runtime.PhysicalInventorySource Source()
        {
            Scans++;
            if (Failure == "verify" && Scans == 2) throw new InvalidOperationException("private scan failure");
            var Views = new Runtime.PhysicalInventoryContainer[3];
            for (int Index = 0; Index < 3; ++Index) {
                object Parent = Containers[Index];
                var Entries = Resources.FindAll(Value => Object.ReferenceEquals(Value.Parent, Parent));
                Views[Index] = new Runtime.PhysicalInventoryContainer { Identity = Parent, Capacity = Capacity[Index], StackCount = Entries.Count,
                    Read = Position => { Resource Value = Entries[Position]; return new Runtime.PhysicalInventoryStack(Parent, Value.Definition ?? Definition, Value.Amount, true, Value, Value.Slot); }};
            }
            return new Runtime.PhysicalInventorySource {Main = Views[0], Belt = Views[1], Wear = Views[2]};
        }
        public int StackLimit(object Definition, Runtime.PhysicalInventoryContainer Container) { return Limit; }
        public int MergeSpace(object Definition, Runtime.PhysicalInventoryStack Stack, Runtime.PhysicalInventoryContainer Container)
        { return (int)Math.Max(0, Limit - Stack.Amount); }
        public bool CanPlace(object Definition, Runtime.PhysicalInventoryContainer Container, int Slot) { return true; }
        public object Create(object Definition, int Amount)
        {
            Creates++;
            if (Failure == "create") throw new InvalidOperationException("private create failure");
            if (Failure == "null") return null;
            var Value = new Resource {Id = ++NextId, Amount = Amount, Definition = Definition}; Resources.Add(Value); return Value;
        }
        public bool Transfer(object Item, object Definition, Runtime.InventoryPlacementChunk Chunk)
        {
            Transfers++; DuringTransfer?.Invoke();
            var Value = (Resource)Item;
            if (Failure == "reject" || Failure == "cleanup" || Failure == "slot-boundary") return false;
            if (Failure == "transfer") throw new InvalidOperationException("private transfer failure");
            if (Failure == "world" || Failure == "inconsistent") {Value.State = Runtime.InventoryResourceState.Unexpected; return true;}
            if (Failure == "identity") {Value.Id++; return true;}
            if (Failure == "amount-boundary" || Failure == "limit-boundary") Value.Amount--;
            if (Chunk.MergeTarget != null) {
                ((Resource)Chunk.MergeTarget).Amount += Value.Amount;
                Value.Amount = 0; Value.State = Runtime.InventoryResourceState.Consumed;
            } else {
                Value.Parent = Chunk.Container.Identity; Value.Slot = Chunk.Slot; Value.State = Runtime.InventoryResourceState.Accepted;
            }
            if (Failure == "insert-throw") throw new InvalidOperationException("private post-insert failure");
            if (Failure == "stale") Current = false;
            return Failure != "insert-false";
        }
        public Runtime.InventoryResourceObservation Observe(object Item)
        { var Value = (Resource)Item; return new Runtime.InventoryResourceObservation { Identity = Value.Id, Amount = Value.Amount, State = Value.State }; }
        public void Cleanup(object Item)
        {
            Cleanups++;
            if (Failure == "cleanup") throw new InvalidOperationException("private cleanup failure");
            var Value = (Resource)Item; Check(Value.Parent == null, "never clean accepted item");
            Value.Amount = 0; Value.State = Runtime.InventoryResourceState.Consumed;
        }
        internal int Take(object Definition, int Amount)
        {
            DuringTake?.Invoke();
            int Removed = 0;
            foreach (Resource Value in Resources) if (Value.Parent != null && Removed < Amount &&
                Object.ReferenceEquals(Value.Definition ?? this.Definition, Definition)) {
                int Used = Math.Min(Value.Amount, Amount - Removed); Value.Amount -= Used; Removed += Used;
                if (Value.Amount == 0) {Value.Parent = null; Value.State = Runtime.InventoryResourceState.Consumed;}
            }
            return Removed;
        }
    }
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Player-1F-B: " + Message); }
    private static string Reject(Action Action)
    { try {Action();} catch (InvalidOperationException Error) {return Error.Message;} throw new Exception("expected grant rejection"); }
    private static bool Execute(Runtime.PlayerGiveItemOperation Operation, Fixture Value, int Amount)
    { return Operation.Execute("player", () => Value.Current ? Value.View() : null, Value.Definition, Amount); }

    internal static void RunModel()
    {
        var Take = new Runtime.PlayerTakeItemOperation();
        var Give = new Runtime.PlayerGiveItemOperation(Take.SharedGate);
        foreach (int Amount in new[] {1, 10, 11, 641, 961, 1280}) {
            var Value = new Fixture();
            Check(Execute(Give, Value, Amount), "complete bounded grant " + Amount);
            Check(Value.Scans == 2 && Give.TrackedResources == 0 && Give.BusyCount == 0, "single VERIFY and baseline resources");
            Check(Value.Creates == (Amount + 9) / 10, "exact chunks, including 128 envelope");
        }
        foreach (int Amount in new[] {1281, Int32.MaxValue}) {
            var Value = new Fixture();
            Check(!Execute(Give, Value, Amount) && Value.Creates == 0 && Value.Scans == 1, "complete PREPARE rejects without creation");
        }
        var Merge = new Fixture(); Merge.Add(0, 0, 7); Merge.Add(1, 0, 8);
        Check(Execute(Give, Merge, 17) && Merge.Creates == 4 && Merge.Cleanups == 0, "merges first then empty chunks");
        Check(Merge.Resources[0].Amount == 10 && Merge.Resources[1].Amount == 10, "exact stack completion");
        var InvalidAmount = new Fixture(); InvalidAmount.Add(0, 0, -10);
        Check(Execute(Give, InvalidAmount, 10) && InvalidAmount.Creates == 1 && InvalidAmount.Resources[0].Amount == -10 &&
            InvalidAmount.Resources[1].Amount == 10, "invalid occupied stack is not merge capacity or an oversized creation premise");
        var Full = new Fixture();
        for (int C = 0; C < 3; ++C) for (int Slot = 0; Slot < Full.Capacity[C]; ++Slot) Full.Add(C, Slot, 10);
        Check(!Execute(Give, Full, 1) && Full.Creates == 0, "full inventory false");
        var Over = new Fixture(); Over.Capacity[0]++;
        Reject(() => Execute(Give, Over, 1)); Check(Over.Creates == 0, "one-over bound precreation");
        var Missing = new Fixture(); Reject(() => Give.Execute("player", Missing.View, null, 1)); Check(Missing.Creates == 0, "unknown definition errors before commit");
        foreach (string Failure in new[] {"create", "null", "reject", "transfer", "cleanup", "verify", "stale", "world", "inconsistent", "identity", "insert-throw", "insert-false", "amount-boundary", "limit-boundary", "slot-boundary"}) {
            var Value = new Fixture {Failure = Failure};
            string Error = Reject(() => Execute(Give, Value, 2));
            Check(Error.Contains("may have changed") && !Error.Contains("private"), "indeterminate not false: " + Failure);
            Check(Give.BusyCount == 0 && Give.TrackedResources == 0, "failure gate/reference release: " + Failure);
            if (Failure == "insert-throw" || Failure == "insert-false") Check(Value.Resources[0].Parent != null && Value.Cleanups == 0, "accepted item preserved after exception/false");
            if (Failure == "reject" || Failure == "transfer" || Failure == "slot-boundary") Check(Value.Cleanups == 1 && Value.Resources[0].State == Runtime.InventoryResourceState.Consumed, "one qualified cleanup");
            Check(Execute(Give, new Fixture(), 1), "same-token reuse after failure");
        }
        var Recursive = new Fixture();
        Recursive.DuringTransfer = () => {
            Check(Reject(() => Execute(Give, Recursive, 1)).Contains("already in progress"), "Give/Give gate");
            Check(Reject(() => Take.Execute("player", Recursive.View, Recursive.Definition, 1)).Contains("already in progress"), "Give/Take gate");
            var Other = new Fixture();
            Check(Give.Execute("other", Other.View, Other.Definition, 1), "different Player independent");
        };
        Check(Execute(Give, Recursive, 1), "outer grant completes after bounded recursive rejection");
        for (int Index = 0; Index < 1000; ++Index) {
            var Value = new Fixture(); Value.Add(0, 0, 9);
            Check(Execute(Give, Value, 1), "merge stress");
            Check(!Execute(Give, Value, Int32.MaxValue), "prepare stress");
            Value.Failure = "reject"; Reject(() => Execute(Give, Value, 1));
            Value.Failure = "transfer"; Reject(() => Execute(Give, Value, 1));
            Check(Value.Cleanups == 2 && Give.BusyCount == 0 && Give.TrackedResources == 0, "cleanup and gate stress baseline");
        }
        Check(Give.CleanupFailures == 1 && Give.UnaccountedResources >= 3, "bounded uncertainty counters");
        Console.WriteLine("[CarbonLuau:Player1FBModel] PASS planner/128-bound/combined VERIFY/cleanup/shared gate; 4000 stress operations; I12 interference separately injected");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var Value = new Fixture(); Runtime.PlayerView View = Value.View();
        var Players = new Runtime.PlayerDirectory(Id => View);
        Players.Connect(View);
        var World = new Runtime.FacadeWorld(Players, new Registrar(), new Runtime.ItemDirectory(Name => Name == "scrap" ? Value.Definition : null));
        const string Player = "local P=game:GetService('Players'):GetPlayers()[1]; ";
        string Source = Player + "assert(not pcall(function() P:GiveItem('scrap',1) end)); task.defer(function() assert(P:GiveItem('scrap',1)); print('granted') end)";
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Value.Creates == 0, "provisional rejection");
            for (int Index = 0; Index < 20 && Host.HasWork; ++Index) foreach (var Result in Host.Drain()) Check(Result.Status == Runtime.RuntimeStatus.OK, Result.Error);
            Check(Value.Creates == 1, "deferred after publication");
            string Checks = Player + "assert(type(GiveItemBehavior.InventoryOnly)=='userdata'); assert(GiveItemBehavior.DropRemainder==nil); " +
                "assert(not pcall(function() GiveItemBehavior.InventoryOnly.X=1 end)); assert(not pcall(function() GiveItemBehavior.X=1 end)); " +
                "for _,V in {true,false,'InventoryOnly',{},GuiFont.DroidSansMono,1} do assert(not pcall(function() P:GiveItem('scrap',1,V) end)) end; " +
                "for _,V in {0,-1,0.5,0/0,math.huge,-math.huge,2147483648,'1',true,{}} do assert(not pcall(function() P:GiveItem('scrap',V) end)) end; " +
                "assert(not pcall(function() P:GiveItem('scrap') end)); assert(not pcall(function() P:GiveItem('missing',1) end)); " +
                "assert(not pcall(function() P:GiveItem('Scrap',1) end)); assert(not pcall(function() P:GiveItem(string.rep('a',129),1) end)); " +
                "assert(P:GiveItem('scrap',2,GiveItemBehavior.InventoryOnly)); assert(P:GiveItem('scrap',3)); assert(P:CountItem('scrap')==6); assert(P:TakeItem('scrap',1)); " +
                "assert(P:CountItem('scrap')==5); Old=P";
            var Checked = Host.Execute("give.api", Checks); Check(Checked.Status == Runtime.RuntimeStatus.OK, "native API: " + Checked.Error);
            int Before = Value.Creates;
            Source = Player + "task.defer(function() P:GiveItem('scrap',1) end); error('candidate failed')";
            Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Value.Creates == Before, "failed candidate no grant");
            Check(Host.Execute("give.error", Player + "P:GiveItem('scrap',1); error('after grant')").Status != Runtime.RuntimeStatus.OK && Value.Creates == Before + 1, "irreversible success before error");
            Players.Disconnect(View.UserId, View.Identity); View = Value.View(); View.Connection = new object(); Players.Connect(View);
            Check(Host.Execute("give.stale", "assert(not pcall(function() Old:GiveItem('scrap',1) end))").Status == Runtime.RuntimeStatus.OK, "reconnect rejects old proxy");
            Source = "-- clean recovery source";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "root replacement");
            Before = Value.Creates;
            Check(Host.Execute("give.timeout", Player + "P:GiveItem('scrap',1); while true do end").Status == Runtime.RuntimeStatus.TIMEOUT, "timeout after irreversible grant");
            Check(Value.Creates == Before + 1 && World.GiveItems.BusyCount == 0 && World.GiveItems.TrackedResources == 0, "recovery no replay, no gate leak");
            bool Allowed = false; View.Permission = Name => Allowed;
            Source = "game:GetService('Commands'):Register('grant',{permission='carbonluau.fixture.grant'},function(C) assert(C.Player:GiveItem('scrap',1)); print('command-grant') end)";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "grant command publication");
            Before = Value.Creates;
            Check(!World.Active.Invoke("grant", View.UserId, new string[0]) && !Host.HasWork && Value.Creates == Before, "denied command never enters Luau/grants");
            Allowed = true; Check(World.Active.Invoke("grant", View.UserId, new string[0]), "authorized command admission");
            string Logs = "";
            for (int Frame = 0; Frame < 20 && Host.HasWork; ++Frame) foreach (var Work in Host.Drain()) {Check(Work.Status == Runtime.RuntimeStatus.OK, Work.Error); Logs += Work.Logs;}
            Check(Logs == "command-grant\n" && Value.Creates == Before + 1, "authorized command grant");
        }
        Check(World.Active == null && World.GiveItems.BusyCount == 0, "unload releases session and gate");
        Console.WriteLine("[CarbonLuau:Player1FBNative] PASS typed API, provisional/deferred, failed candidates, reconnect, root replacement, recovery/no replay, unload");
    }
}
