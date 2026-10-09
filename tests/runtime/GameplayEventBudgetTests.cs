using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;

// Private signal registration is intentional: these tests qualify accounting
// and publication, not a public event or a Rust transition/host-proof contract.
internal static class GameplayEventBudgetTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private sealed class Delivery
    {
        internal Runtime.FacadeSession Owner;
        internal byte[] Payload;
        internal string[] Fields;
        internal ulong Nonce;
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.FacadeWorld World;
        internal readonly Dictionary<string,Runtime.PlayerView> Views = new Dictionary<string,Runtime.PlayerView>(StringComparer.Ordinal);
        internal Runtime.PlayerLifetime Victim;
        internal readonly List<Runtime.FacadeSession> Owners = new List<Runtime.FacadeSession>();
        internal readonly List<Delivery> Taken = new List<Delivery>();
        internal long Frame, NextDomain = 1;
        internal Runtime.GameplayEventBudget Budget { get { return World.GameplayEvents; } }
        internal Fixture() {
            var Players = new Runtime.PlayerDirectory(Id=>{Runtime.PlayerView View; return Views.TryGetValue(Id,out View) ? View : null;});
            World = new Runtime.FacadeWorld(Players,new Registrar());
            Victim = Connect("76561190000999888"); Budget.FrameClock = () => Frame;
        }
        internal Runtime.PlayerLifetime Connect(string UserId)
        { var View = Player(UserId); Views[UserId] = View; return World.Players.Connect(View); }
        internal Runtime.PlayerLifetime Disconnect(string UserId)
        { var Removed = World.Players.Disconnect(UserId,Views[UserId].Identity); Views.Remove(UserId); return Removed; }
        internal Runtime.FacadeSession Add(int Listeners = 1, int Capacity = 256, bool Root = false)
        {
            Runtime.FacadeSession Owner = Candidate(Listeners,Capacity);
            if (Root) World.Commit(Owner); else World.CommitAddon(null, Owner);
            return Owner;
        }
        internal Runtime.FacadeSession Candidate(int Listeners = 0, int Capacity = 256)
        {
            var Owner = new Runtime.FacadeSession(World,10,NextDomain++,Capacity); Owners.Add(Owner);
            for (int Index = 0; Index < Listeners; ++Index) Operation(Owner,6,"died");
            return Owner;
        }
        internal void Emit()
        {
            Check(Budget.Capture("died"), "explicit producer capture accepted");
            World.GameplayEvent("died", Victim.Token, Victim.UserId, "Snapshot", new Runtime.PlayerPosition(1, 2, 3));
        }
        internal List<Delivery> Take(Runtime.FacadeSession Owner, bool ToNative = true)
        {
            var Values = new List<Delivery>();
            object Queue = typeof(Runtime.FacadeSession).GetField("Pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Owner);
            MethodInfo Dequeue = Queue.GetType().GetMethod("Dequeue");
            while (Owner.PendingCount != 0) {
                object Item = Dequeue.Invoke(Queue, null);
                byte[] Payload = (byte[])Item.GetType().GetField("Payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Item);
                string[] Fields = Runtime.FacadePolicy.Unpack(Payload);
                var Value = new Delivery {Owner = Owner, Payload = Payload, Fields = Fields,
                    Nonce = Fields.Length == 12 ? UInt64.Parse(Fields[5], CultureInfo.InvariantCulture) : 0};
                if (ToNative && Value.Nonce != 0) Check(Budget.ToNative(Owner, Value.Nonce), "model transfer retains reservation");
                Values.Add(Value); Taken.Add(Value);
            }
            return Values;
        }
        public void Dispose()
        {
            foreach (Runtime.FacadeSession Owner in Owners) World.Retire(Owner);
            foreach (Delivery Value in Taken) Budget.Release(Value.Owner, Value.Nonce);
            Check(Budget.PendingCount == 0 && Budget.RetainedBytes == 0, "model cleanup converges");
        }
    }
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Gameplay event budget: " + Message); }
    private static string[] Operation(Runtime.FacadeSession Owner, uint Code, params string[] Fields)
    {
        try { return (string[])typeof(Runtime.FacadeSession).GetMethod("Operation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Owner, new object[] {Code, Fields}); }
        catch (TargetInvocationException Error) { throw Error.InnerException; }
    }
    private static bool Host(Runtime.FacadeSession Owner, uint Code, string[] Fields, long? ExpectedDomain = null)
    {
        byte[] Bytes = Runtime.FacadePolicy.Pack(Fields);
        IntPtr Input = Marshal.AllocHGlobal(Math.Max(1, Bytes.Length)), Output = Marshal.AllocHGlobal(262144);
        try {
            if (Bytes.Length != 0) Marshal.Copy(Bytes, 0, Input, Bytes.Length);
            uint Written;
            return Owner.Callback((ulong)(ExpectedDomain ?? Owner.DomainLifetimeId), Code, Input, (uint)Bytes.Length, Output, 262144, out Written) == 0;
        } finally { Marshal.FreeHGlobal(Input); Marshal.FreeHGlobal(Output); }
    }
    private static void Reject(Action Action, string Message)
    {
        bool Rejected = false;
        try { Action(); } catch (InvalidOperationException) { Rejected = true; }
        catch (System.Text.EncoderFallbackException) { Rejected = true; }
        Check(Rejected, Message);
    }
    private static string[] Fields(string Listener = "1")
    { return new[] {"died", Listener, "1", "76561190000999888", "Snapshot", "", "0", "1.25", "-2.5", "", "", ""}; }

    internal static void Run()
    {
        FrameLimits(); RetainedReservations(); Publication(); SaturationProgress(); ValidationAndCancellation(); VictimLifetime();
        Console.WriteLine("[CarbonLuau:GameplayBudgetModel] PASS bounded producer/fanout/scans, retained quota, publication, cancellation and100-domain progress; no Rust/VM event qualification");
    }
    private static Runtime.PlayerView Player(string UserId)
    { return new Runtime.PlayerView {Identity=new object(),Connection=new object(),UserId=UserId,Name="Snapshot",Connected=true,
        Send=Text=>{},Permission=Name=>true}; }
    private static Runtime.FacadeWorld LiveWorld()
    {
        Runtime.PlayerView View = Player("76561190000999888");
        var Players = new Runtime.PlayerDirectory(Id=>Id == View.UserId ? View : null); Players.Connect(View);
        return new Runtime.FacadeWorld(Players,new Registrar());
    }
    private static void VictimLifetime()
    {
        using (var Value = new Fixture()) {
            Runtime.FacadeSession Owner = Value.Add(); string Removing = Operation(Owner,6,"removing")[0];
            Value.Emit(); Delivery Death = Value.Take(Owner)[0];
            Runtime.PlayerLifetime Removed = Value.Disconnect(Value.Victim.UserId);
            Check(!Host(Owner,9,Death.Fields),"victim disconnect suppresses queued death before entry");
            Value.World.Event("removing",Removed); Delivery Legacy = Value.Take(Owner)[0];
            Check(Legacy.Fields[1] == Removing && Host(Owner,9,Legacy.Fields),"existing PlayerRemoving retains disconnected snapshot delivery");
            Runtime.PlayerLifetime Reconnected = Value.Connect(Removed.UserId);
            Check(Reconnected.Token != Removed.Token && !Host(Owner,9,Death.Fields),"same-user reconnect cannot retarget queued original victim");
            Check(Host(Owner,39,new[] {Death.Fields[5]}),"suppressed death refunds only on discard");
            Value.Victim = Reconnected;
            Runtime.PlayerLifetime Killer = Value.Connect("76561190000999889");
            Check(Value.Budget.Capture("died"),"killer lifetime capture");
            Value.World.GameplayEvent("died",Reconnected.Token,Reconnected.UserId,"Snapshot",null,Killer.Token,Killer.UserId,"Killer");
            Delivery WithKiller = Value.Take(Owner)[0]; Value.Disconnect(Killer.UserId);
            Check(Host(Owner,9,WithKiller.Fields),"killer disconnect alone does not suppress live victim observation");
        }
    }
    private static void FrameLimits()
    {
        using (var Value = new Fixture()) {
            for (int Index = 0; Index < 256; ++Index) Check(!Value.Budget.Capture("died"), "no subscriber rejects capture");
            Check(Value.Budget.NoSubscribers == 256 && Value.Budget.ProducerRejected == 0 &&
                Value.Budget.PendingCount == 0 && Value.Budget.Accepted == 0, "no demand spends no capture or payload quota");
            Runtime.FacadeSession Owner = Value.Add();
            for (int Index = 0; Index < 128; ++Index) Check(Value.Budget.Capture("died"), "128 global captures permitted");
            Check(!Value.Budget.Capture("died") && Value.Budget.ProducerRejected == 1, "129th capture rejected");
            Value.Frame++;
            for (int Index = 0; Index < 4096; ++Index) Check(Value.Budget.Visit(), "fixed frame scan budget");
            Check(!Value.Budget.Visit() && Value.Budget.ScanRejected == 1, "4097th visit rejected");
            Value.Frame++;
            for (int Index = 0; Index < 32; ++Index) Check(Value.Budget.Delivery(Owner), "domain32 deliveries");
            Check(!Value.Budget.Delivery(Owner), "33rd delivery rejected for same domain");
            for (int Domain = 0; Domain < 3; ++Domain) {
                Runtime.FacadeSession Other = Value.Add();
                for (int Index = 0; Index < 32; ++Index) Check(Value.Budget.Delivery(Other), "other domain progresses within global cap");
            }
            Check(!Value.Budget.Delivery(Value.Add()), "129th global delivery rejected");
            Value.Frame++; Check(Value.Budget.Capture("died") && Value.Budget.Delivery(Owner), "frame reset restores producer and fanout budgets");
        }
    }
    private static void RetainedReservations()
    {
        using (var Value = new Fixture()) {
            Runtime.FacadeSession Owner = Value.Add(), OtherOwner = Value.Add(); int Charge = 0;
            var Reservations = new List<Delivery>();
            for (int Index = 0; Index < 512; ++Index) {
                Runtime.FacadeSession Selected = Index < 256 ? Owner : OtherOwner;
                byte[] Payload; ulong Nonce = Value.Budget.Reserve(Selected, "died", "1", Fields(), out Payload);
                Check(Nonce != 0 && Payload.Length <= 2048, "bounded valid context reservation");
                var Item = new Delivery {Owner=Selected,Nonce=Nonce}; Reservations.Add(Item); Value.Taken.Add(Item);
                Charge += Payload.Length * 2;
                Check(Value.Budget.ToNative(Selected, Nonce), "native ownership retains budget");
            }
            Check(Value.Budget.PendingCount == 512 && Value.Budget.RetainedBytes == Charge && Charge <= 2 * 1024 * 1024,
                "512 end-to-end reservations retain both transport copies under2MiB");
            byte[] RejectedPayload;
            Check(Value.Budget.Reserve(Owner, "died", "1", Fields(), out RejectedPayload) == 0 && RejectedPayload == null,
                "513th retained reservation rejected without extra payload ownership");
            Runtime.FacadeSession WrongOwner = Value.Add();
            Check(!Value.Budget.Release(WrongOwner, Reservations[0].Nonce) && Value.Budget.PendingCount == 512,
                "same nonce cannot be released by another owner");
            foreach (Delivery Item in Reservations) Check(Value.Budget.Release(Item.Owner, Item.Nonce), "native acknowledgement releases one item");
            Check(Value.Budget.PendingCount == 0 && Value.Budget.RetainedBytes == 0 && Value.Budget.Released == 512,
                "release refunds exact double byte charge");
            byte[] InvalidPayload;
            string[] Oversized = Fields(); Oversized[4] = new string('x',2048);
            Check(Value.Budget.Reserve(Owner,"died","1",Oversized,out InvalidPayload) == 0 && Value.Budget.PendingCount == 0,
                "ledger transport ceiling rejects oversize independently of stricter producer name validation");
        }
    }
    private static void Publication()
    {
        using (var Value = new Fixture()) {
            Runtime.FacadeSession Owner = Value.Add(0, Root:true);
            Operation(Owner,10); string Staged = Operation(Owner,6,"died")[0];
            Check(!Value.Budget.Capture("died") && Owner.PendingCount == 0, "foreign publication's staged listener receives no prior event");
            Operation(Owner,10); Operation(Owner,6,"died"); Operation(Owner,11);
            Check(!Value.Budget.Capture("died"), "committed inner listener remains staged behind outer checkpoint");
            Operation(Owner,12); Check(Owner.ListenerCount == 0, "failed outer scope discards both staged listeners");
            string Published = Operation(Owner,6,"died")[0];
            Operation(Owner,10); Operation(Owner,7,Published); Operation(Owner,6,"died");
            Value.Emit(); List<Delivery> Items = Value.Take(Owner);
            Check(Items.Count == 1 && Items[0].Fields[1] == Published && Items[0].Fields[1] != Staged,
                "capture uses oldest committed listener despite staged Disconnect and Connect");
            Operation(Owner,12);
            Check(Host(Owner,9,Items[0].Fields), "failed Disconnect restores original queued callback eligibility");
            Check(Host(Owner,39,new[] {Items[0].Fields[5]}), "model acknowledgement after restored eligibility");
            Value.Emit(); Items = Value.Take(Owner);
            Operation(Owner,10); Operation(Owner,7,Published); Operation(Owner,11);
            Check(!Host(Owner,9,Items[0].Fields), "committed Disconnect cancels actual queued gate");
            Check(Host(Owner,39,new[] {Items[0].Fields[5]}), "cancelled callback remains charged until discard acknowledgement");
            Operation(Owner,10); Operation(Owner,6,"died"); Operation(Owner,11);
            Value.Emit(); Items = Value.Take(Owner);
            Check(Items.Count == 1 && Items[0].Fields[1] != Published && Host(Owner,9,Items[0].Fields),
                "successful publication receives future captures with fresh identity");
        }
    }
    private static void SaturationProgress()
    {
        using (var Value = new Fixture()) {
            var Progress = new HashSet<long>();
            for (int Domain = 0; Domain < 100; ++Domain) Value.Add(32);
            for (int Frame = 0; Frame < 25; ++Frame) {
                Value.Frame++; Value.Emit(); int Delivered = 0;
                foreach (Runtime.FacadeSession Owner in Value.Owners) {
                    List<Delivery> Items = Value.Take(Owner); Delivered += Items.Count;
                    if (Items.Count != 0) {
                        Check(Items.Count == 32, "one saturated domain remains bounded32 per frame");
                        Progress.Add(Owner.DomainLifetimeId);
                    }
                    foreach (Delivery Item in Items) Check(Value.Budget.Release(Owner,Item.Nonce), "saturation model consume");
                }
                Check(Delivered == 128 && Value.Budget.PendingCount == 0, "global128 fanout with bounded model drain");
            }
            Check(Progress.Count == 100 && Value.Budget.FanoutRejected != 0,
                "round-robin cursor permits all100 domains under intentional lossy saturation");
        }
        using (var Value = new Fixture()) {
            Runtime.FacadeSession Owner = Value.Add(2,Capacity:1); Value.Emit();
            Check(Owner.PendingCount == 1 && Value.Budget.PendingCount == 1 && Value.Budget.QueueRejected == 1,
                "shared pending queue full counts rejection without partial extra reservation");
        }
    }
    private static void ValidationAndCancellation()
    {
        using (var Value = new Fixture()) {
            Runtime.FacadeSession Owner = Value.Add(); Value.Emit(); Delivery Item = Value.Take(Owner)[0];
            Check(!Value.Budget.ToNative(Owner,Item.Nonce), "reservation transfers at most once");
            foreach (string Nonce in new[] {"", "0", "01", "+1", "1.0", "18446744073709551616"}) {
                string[] Tampered = (string[])Item.Fields.Clone(); Tampered[5] = Nonce;
                Check(!Host(Owner,9,Tampered) && !Host(Owner,39,new[] {Nonce}), "malformed nonce rejected consistently with native codec");
            }
            Check(Value.Budget.PendingCount == 1 && !Host(Owner,39,new[] {Item.Fields[5]},Owner.DomainLifetimeId+1),
                "private release checks exact expected domain lifetime");
            var Missing = new List<string>(Item.Fields); Missing.RemoveAt(11);
            var Extra = new List<string>(Item.Fields); Extra.Add("");
            Check(!Host(Owner,9,Missing.ToArray()) && !Host(Owner,9,Extra.ToArray()),
                "managed pre-entry validation requires same exact12 fields as native codec");
            Value.World.Retire(Owner);
            Check(Value.Budget.PendingCount == 1 && !Host(Owner,9,Item.Fields), "native-held item remains charged but cancelled after retirement");
            Runtime.FacadeSession Replacement = Value.Add();
            Check(!Value.Budget.Validate(Replacement,Item.Fields), "replacement cannot retarget old reservation");
            Check(Host(Owner,39,new[] {Item.Fields[5]}) && !Host(Owner,39,new[] {Item.Fields[5]}),
                "disposed exact owner releases native-held work once through private39");
            Reject(()=>{Check(Value.Budget.Capture("died"),"invalid token capture quota"); Value.World.GameplayEvent("died","01","76561190000999888","Snapshot",null);},
                "producer token grammar matches canonical native identity");
            Reject(()=>{Check(Value.Budget.Capture("died"),"invalid name capture quota"); Value.World.GameplayEvent("died","1","76561190000999888",new string('x',129),null);}, "producer bounds UTF-8 name");
            Reject(()=>{Check(Value.Budget.Capture("died"),"invalid UTF-16 capture quota"); Value.World.GameplayEvent("died","1","76561190000999888","\ud800",null);}, "producer rejects malformed UTF-16 before packing");
            Reject(()=>{Check(Value.Budget.Capture("died"),"invalid position capture quota"); Value.World.GameplayEvent("died","1","76561190000999888","Snapshot",new Runtime.PlayerPosition(Single.NaN,0,0));}, "producer rejects nonfinite position");
            Operation(Replacement,6,"spawned");
            Reject(()=>{Check(Value.Budget.Capture("spawned"),"invalid killer capture quota"); Value.World.GameplayEvent("spawned","1","76561190000999888","Snapshot",null,"2","76561190000999889","Killer");}, "spawn excludes killer triple");
        }
        using (var Value = new Fixture()) {
            Runtime.FacadeSession Root = Value.Add(Root:true), Addon = Value.Add(); Value.Emit();
            Delivery NativeHeld = Value.Take(Root)[0];
            Runtime.FacadeSession Next = Value.Candidate();
            Value.World.CommitAddon(Addon,Next);
            Check(Addon.PendingCount == 0 && Value.Budget.PendingCount == 1,
                "addon/provider-style replacement drops pending intake without touching other native ownership");
            Value.World.Retire(Root);
            Check(!Value.Budget.Validate(Root,NativeHeld.Fields), "root retirement invalidates native-held callback");
            Check(Host(Root,39,new[] {NativeHeld.Fields[5]}) && Value.Budget.PendingCount == 0,
                "root and addon retirement converge without replay");
            Value.Frame++;
            Check(!Value.Budget.Capture("died"), "new empty owners receive no synthetic history");
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        Run();
        var World = LiveWorld();
        long Frame = 0; World.GameplayEvents.FrameClock = ()=>Frame;
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds=100,FrameDrainBudgetMilliseconds=20,MaxQueuedCallbacks=1};
        using (var Vm = new Runtime.RuntimeGeneration(Native,77,Config)) {
            using (var Domain = new Runtime.RuntimeDomain(Native,Vm,Config,new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource="return true"})) {
                var Owner = new Runtime.FacadeSession(World,Domain.VmGenerationId,Domain.DomainLifetimeId,1);
                Domain.Facade(Owner); Operation(Owner,6,"died");
                Check(Domain.Execute("gameplay.budget.private","return true",100).Status == Runtime.RuntimeStatus.OK, "private native domain initialized");
                Domain.Commit(); World.Commit(Owner);
                Check(World.GameplayEvents.Capture("died"), "private native capture");
                World.GameplayEvent("died","1","76561190000999888","Snapshot",null);
                int Charge = World.GameplayEvents.RetainedBytes;
                Owner.Flush(Domain,Stopwatch.StartNew(),20,1);
                Check(Owner.PendingCount == 0 && Vm.Scheduler.Queued == 1 && World.GameplayEvents.PendingCount == 1 &&
                    World.GameplayEvents.RetainedBytes == Charge, "real native queue retains managed reservation after Flush");
                bool Ran; Runtime.ExecutionResult Result = Vm.Callback(Vm.Scheduler,100,out Ran);
                Check(Ran && Result.Status == Runtime.RuntimeStatus.OK && World.GameplayEvents.PendingCount == 0 &&
                    World.GameplayEvents.RetainedBytes == 0, "real private39 consumption refunds retained reservation");
                Check(Domain.Execute("gameplay.budget.task","task.defer(function() end)",100).Status == Runtime.RuntimeStatus.OK, "occupy shared native queue");
                Frame++; Check(World.GameplayEvents.Capture("died"), "capture before native full rejection");
                World.GameplayEvent("died","1","76561190000999888","Snapshot",null); Owner.Flush(Domain,Stopwatch.StartNew(),20,1);
                Check(World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0 && World.GameplayEvents.NativeRejected == 1,
                    "native admission rejection refunds caller-owned reservation");
                Vm.Callback(Vm.Scheduler,100,out Ran);
                Frame++; Check(World.GameplayEvents.Capture("died"), "capture before native retirement");
                World.GameplayEvent("died","1","76561190000999888","Snapshot",null); Owner.Flush(Domain,Stopwatch.StartNew(),20,1);
                World.Retire(Owner); Check(World.GameplayEvents.PendingCount == 1, "disposed facade holds native reservation until destruction");
                Domain.Dispose(); Check(World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0,
                    "actual native domain destruction releases disposed exact owner");
            }
        }
        RunFalseTransfer(Native); RunDisposedStatus(Native); RunDisposedNativeException(Native); RunRecovery(Native);
        Check(Native.LiveVmCount == 0, "private native budget fixture teardown");
        Console.WriteLine("[CarbonLuau:GameplayBudgetNative] PASS private managed/native ownership, rejection, disposal and D9 no-replay; public gameplay Signals absent");
    }
    private static void RunRecovery(Runtime.NativeRuntime Native)
    {
        var World = LiveWorld();
        World.GameplayEvents.FrameClock = ()=>0;
        using (var Host = new Runtime.ScriptHost(Native,new Runtime.RuntimeConfig {MaxCallbackMilliseconds=10,FrameDrainBudgetMilliseconds=20},
            ()=>new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource="return true"},World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK,"recovery baseline");
            Runtime.FacadeSession Previous = World.Active; Operation(Previous,6,"died");
            Check(Host.Execute("gameplay.budget.runaway","task.defer(function() while true do end end)").Status == Runtime.RuntimeStatus.OK,"fatal deferred callback queued first");
            Check(World.GameplayEvents.Capture("died"),"event captured before fatal callback");
            World.GameplayEvent("died","1","76561190000999888","Snapshot",null);
            long Generation = Host.VmGenerationId; Host.Drain();
            Check(Host.Ready && Host.VmGenerationId != Generation && Host.Recoveries == 1 && Previous.Disposed &&
                World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0,
                "fatal VM recovery discards captured work and rebuilds fresh owner without replay");
            Check(World.Active.ListenerCount == 0 && !World.GameplayEvents.Capture("died") && !Host.HasWork,
                "reconstruction does not synthesize or replay historical lifecycle captures");
        }
    }
    private static Runtime.RuntimeDomain Domain(Runtime.NativeRuntime Native,Runtime.RuntimeGeneration Vm,Runtime.RuntimeConfig Config,
        Runtime.FacadeWorld World,out Runtime.FacadeSession Owner)
    {
        var Value = new Runtime.RuntimeDomain(Native,Vm,Config,new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource="return true"});
        Owner = new Runtime.FacadeSession(World,Value.VmGenerationId,Value.DomainLifetimeId,256);
        Value.Facade(Owner); Operation(Owner,6,"died");
        Check(Value.Execute("gameplay.transfer.fixture","return true",100).Status == Runtime.RuntimeStatus.OK,"transfer fixture initialization");
        Value.Commit(); World.Commit(Owner); return Value;
    }
    private static byte[] PendingPayload(Runtime.FacadeSession Owner)
    {
        var Queue = (IEnumerable)typeof(Runtime.FacadeSession).GetField("Pending",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Owner);
        foreach (object Item in Queue)
            return (byte[])Item.GetType().GetField("Payload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Item);
        throw new Exception("Gameplay event budget: missing pending fixture");
    }
    private static void Emit(Runtime.FacadeWorld World)
    {
        Check(World.GameplayEvents.Capture("died"),"transfer capture admission");
        World.GameplayEvent("died","1","76561190000999888","Snapshot",null);
    }
    private static void RunFalseTransfer(Runtime.NativeRuntime Native)
    {
        foreach (bool GenerationFlush in new[] {false,true}) {
            var World = LiveWorld(); var Config = new Runtime.RuntimeConfig {MaxQueuedCallbacks=4,MaxCallbackMilliseconds=100};
            using (var Vm = new Runtime.RuntimeGeneration(Native,78,Config)) {
                Runtime.FacadeSession Owner;
                using (var Value = Domain(Native,Vm,Config,World,out Owner)) {
                    Emit(World); byte[] Payload = PendingPayload(Owner);
                    ulong Nonce = UInt64.Parse(Runtime.FacadePolicy.Unpack(Payload)[5],CultureInfo.InvariantCulture);
                    Check(World.GameplayEvents.ToNative(Owner,Nonce) && Value.Event(Payload) == Runtime.RuntimeStatus.OK,
                        "actual native queue already owns duplicate intake nonce");
                    int Charge = World.GameplayEvents.RetainedBytes; ulong Before = Vm.Scheduler.Rejected;
                    if (GenerationFlush) Owner.Flush(Vm,Stopwatch.StartNew(),20); else Owner.Flush(Value,Stopwatch.StartNew(),20);
                    Check(Owner.PendingCount == 0 && Vm.Scheduler.Queued == 1 && Vm.Scheduler.Rejected == Before &&
                        World.GameplayEvents.PendingCount == 1 && World.GameplayEvents.RetainedBytes == Charge &&
                        World.GameplayEvents.StaleRejected == 1 && World.GameplayEvents.NativeRejected == 0,
                        "false transfer neither resubmits nor refunds native ownership in either Flush overload");
                    bool Ran; var Result = Vm.Callback(Vm.Scheduler,100,out Ran);
                    Check(Ran && Result.Status == Runtime.RuntimeStatus.OK && Vm.Info.Ready != 0 &&
                        World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.Released == 1,
                        "original native item still acknowledges once after duplicate intake is discarded");
                    World.Retire(Owner);
                }
            }
        }
    }
    private static void RunDisposedStatus(Runtime.NativeRuntime Native)
    {
        var World = LiveWorld(); var Config = new Runtime.RuntimeConfig {MaxQueuedCallbacks=4,MaxCallbackMilliseconds=100};
        using (var Vm = new Runtime.RuntimeGeneration(Native,79,Config)) {
            Runtime.FacadeSession Owner;
            using (var Value = Domain(Native,Vm,Config,World,out Owner)) {
                Emit(World); Value.Dispose(); Owner.Flush(Value,Stopwatch.StartNew(),20);
                Check(Owner.PendingCount == 0 && World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0 &&
                    World.GameplayEvents.NativeRejected == 1 && World.GameplayEvents.Released == 1,
                    "disposed domain INVALID_ARGUMENT refunds just-transferred reservation");
                World.Retire(Owner);
            }
        }
    }
    private static void RunDisposedNativeException(Runtime.NativeRuntime Existing)
    {
        var Loader = (Runtime.NativeLibraryLoader)typeof(Runtime.NativeRuntime).GetField("Loader",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Existing);
        string Data = new DirectoryInfo(Path.GetDirectoryName(Loader.LibraryPath)).Parent.Parent.Parent.FullName;
        foreach (bool GenerationFlush in new[] {false,true}) {
            var Native = new Runtime.NativeRuntime(Data); var World = LiveWorld();
            var Config = new Runtime.RuntimeConfig {MaxQueuedCallbacks=4,MaxCallbackMilliseconds=100};
            var Vm = new Runtime.RuntimeGeneration(Native,80,Config); Runtime.FacadeSession Owner;
            var Value = Domain(Native,Vm,Config,World,out Owner);
            try {
                Emit(World); Native.Dispose(); bool Threw = false;
                try {
                    if (GenerationFlush) Owner.Flush(Vm,Stopwatch.StartNew(),20); else Owner.Flush(Value,Stopwatch.StartNew(),20);
                } catch (ObjectDisposedException) { Threw = true; }
                Check(Threw && Owner.PendingCount == 0 && World.GameplayEvents.PendingCount == 0 &&
                    World.GameplayEvents.RetainedBytes == 0 && World.GameplayEvents.NativeRejected == 1 &&
                    World.GameplayEvents.Released == 1 && Native.LiveVmCount == 0,
                    "actual disposed NativeRuntime exception refunds once before propagating in both overloads");
            } finally {
                World.Retire(Owner);
                // Native.Dispose has already destroyed this actual VM. Release
                // the wrapper handles without trying to reenter its unloaded host.
                try { Value.Dispose(); } catch (ObjectDisposedException) { }
                Vm.Handle=0; Vm.Dispose(); Native.Dispose();
            }
        }
    }
}
