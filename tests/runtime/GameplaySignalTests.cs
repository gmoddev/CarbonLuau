using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Real pinned-Luau public dispatcher tests. Synthetic host snapshots qualify
// transport/lifetime behavior only, never Rust transition or client behavior.
internal static class GameplaySignalTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private static void Check(bool Value, string Message)
    { if (!Value) throw new InvalidOperationException("Gameplay Signals: " + Message); }
    private const string VictimId = "76561190000999888", KillerId = "76561190000999889";
    private const string State = "local S=require('state'); ";
    private const string Entry = State +
        "S.Died=0; S.Spawned=0; local P=game:GetService('Players'); " +
        "S.Death=P.PlayerDied:Connect(function(Player,Context) " +
        "assert(table.isfrozen(Context)); S.Died+=1; S.Player=Player; S.Context=Context; print('died') end); " +
        "S.Spawn=P.PlayerSpawned:Connect(function(Player,Context) " +
        "assert(table.isfrozen(Context)); assert(Context.Killer==nil and Context.KillerId==nil); " +
        "S.Spawned+=1; S.Player=Player; S.Context=Context; print('spawned') end); ";

    private sealed class Fixture : IDisposable
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>();
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.ScriptHost Host;
        internal Runtime.PlayerLifetime Victim, Killer;
        internal string Source = Entry;
        internal long Frame;
        internal Fixture(Runtime.NativeRuntime Native)
        {
            var Players = new Runtime.PlayerDirectory(Id => Views.ContainsKey(Id) ? Views[Id] : null);
            World = new Runtime.FacadeWorld(Players, new Registrar());
            World.GameplayEvents.FrameClock = () => Frame;
            Victim = Connect(VictimId, "Victim"); Killer = Connect(KillerId, "Killer old");
            Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {
                MaxCallbackMilliseconds = 20, FrameDrainBudgetMilliseconds = 20 }, Snapshot, World);
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "public subscriptions initialize");
        }
        internal Runtime.PlayerLifetime Connect(string Id, string Name)
        {
            var View = new Runtime.PlayerView { Identity = new object(), Connection = new object(),
                UserId = Id, Name = Name, Connected = true, Send = Text => { }, Permission = Permission => true,
                Position = () => new Runtime.PlayerPosition(9, 8, 7) };
            Views[Id] = View; return World.Players.Connect(View);
        }
        internal void Disconnect(Runtime.PlayerLifetime Player)
        {
            Runtime.PlayerView View = Views[Player.UserId]; View.Connected = false;
            Check(World.Players.Disconnect(Player.UserId, View.Identity) == Player, "disconnect exact original connection");
            Views.Remove(Player.UserId);
        }
        private Runtime.ScriptSnapshot Snapshot()
        {
            var Value = new Runtime.ScriptSnapshot { EntryName = "init.luau", EntrySource = Source };
            Value.Modules.Add("state", "return {}");
            Value.Modules.Add("failedconnect", "game:GetService('Players').PlayerDied:Connect(function() error('leak') end); error('candidate failure')");
            Value.Modules.Add("failednested", "assert(not pcall(require,'failedconnect')); return true");
            Value.Modules.Add("faileddisconnect", State + "S.Death:Disconnect(); error('module failure')");
            return Value;
        }
        internal void Execute(string Text)
        {
            Runtime.ExecutionResult Result = Host.Execute("gameplay.signals.fixture", Text);
            Check(Result.Status == Runtime.RuntimeStatus.OK, "script assertions: " + Result.Error);
        }
        internal void Emit(string Kind, Runtime.PlayerLifetime Player, Runtime.PlayerPosition? Position = null,
            Runtime.PlayerLifetime KillerValue = null)
        {
            Check(World.GameplayEvents.Capture(Kind), "capture admitted");
            World.GameplayEvent(Kind, Player.Token, Player.UserId, Player.Name, Position,
                KillerValue == null ? null : KillerValue.Token, KillerValue == null ? null : KillerValue.UserId,
                KillerValue == null ? null : KillerValue.Name);
        }
        internal string Drain(bool AllowError = false)
        {
            var Text = new StringBuilder();
            for (int Turn = 0; Turn < 256 && Host.HasWork; ++Turn)
                foreach (Runtime.ExecutionResult Result in Host.Drain()) {
                    Check(AllowError || Result.Status == Runtime.RuntimeStatus.OK, "callback: " + Result.Error);
                    Text.Append(Result.Logs);
                }
            Check(!Host.HasWork, "bounded scheduler converges");
            Check(World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0,
                "transport accounting converges");
            return Text.ToString();
        }
        public void Dispose()
        {
            Host.Dispose();
            Check(World.Active == null && World.GameplayEvents.PendingCount == 0 && World.GameplayEvents.RetainedBytes == 0,
                "full host teardown retains no lifecycle payload");
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Check(Value.Drain() == "", "registration does not replay or synthesize spawn");
            Value.Emit("spawned", Value.Victim, new Runtime.PlayerPosition(1, -2, 3));
            Check(Value.Drain() == "spawned\n", "spawn dispatch");
            Value.Execute(State + "assert(S.Spawned==1 and S.Died==0); assert(S.Context.Position==Vector3.new(1,-2,3)); " +
                "assert(S.Player.Position==Vector3.new(9,8,7)); " +
                "assert(not pcall(function() S.Context.Position=Vector3.new(0,0,0) end))");
            Value.Emit("died", Value.Victim, new Runtime.PlayerPosition(4, 5, 6), Value.Killer);
            Value.Disconnect(Value.Killer); Value.Killer = Value.Connect(KillerId, "Killer replacement");
            Check(Value.Drain() == "died\n", "killer disconnect does not discard live-victim observation");
            Value.Execute(State + "assert(S.Died==1 and S.Context.KillerId=='" + KillerId + "'); " +
                "assert(S.Context.Killer.Name=='Killer old' and not S.Context.Killer.IsConnected); " +
                "assert(not pcall(function() return S.Context.Killer.Position end)); " +
                "assert(S.Context.Killer~=game:GetService('Players'):GetPlayerByUserId('" + KillerId + "'))");
            Value.Frame++; Value.Emit("died", Value.Victim);
            Value.Disconnect(Value.Victim); Value.Victim = Value.Connect(VictimId, "Victim replacement");
            Check(Value.Drain() == "", "victim disconnect and reused UserId suppress pending old-life event");
            Value.Execute(State + "assert(S.Died==1)");
            Value.Frame++; Value.Emit("died", Value.Victim);
            Check(Value.Drain() == "died\n", "new exact connection receives a new event");
            Value.Execute(State + "assert(S.Context.Position==nil and S.Context.Killer==nil and S.Context.KillerId==nil)");
            Value.Execute("assert(require('failednested')); assert(not pcall(require,'faileddisconnect'))");
            Check(Value.World.Active.ListenerCount == 2, "cold/nested failure restores existing public Signal roots");
            Value.Frame++; Value.Emit("died", Value.Victim); Check(Value.Drain() == "died\n", "failed disconnect is retryable");
            Value.Frame++; Value.Emit("died", Value.Victim);
            Value.Execute(State + "S.Death:Disconnect(); S.Death:Disconnect()");
            Check(Value.Drain() == "", "committed idempotent disconnect cancels queued event");
        }
        RunReplacement(Native);
        RunCallbackFaults(Native);
        RunAddons(Native);
        Check(Native.LiveVmCount == 0, "public fixture releases every VM");
        Console.WriteLine("[CarbonLuau:GameplaySignals] PASS real-VM immutable contexts, exact lifetimes, publication, replacement, faults and addon ownership; synthetic host snapshots only");
    }
    private static void RunReplacement(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Runtime.FacadeSession Previous = Value.World.Active;
            Value.Source = "game:GetService('Players').PlayerDied:Connect(function() error('failed root') end); error('reject')";
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Value.World.Active == Previous,
                "failed root preserves committed subscriptions");
            Value.Frame++; Value.Emit("died", Value.Victim); Check(Value.Drain() == "died\n", "failed root has no callback leak");
            Value.Source = Entry; Value.Frame++; Value.Emit("spawned", Value.Victim);
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.OK && Previous.Disposed,
                "successful root replacement retires old listener owner");
            Check(Value.Drain() == "", "replacement never replays queued lifecycle work");
            Value.Frame++; Value.Emit("spawned", Value.Victim); Check(Value.Drain() == "spawned\n", "new owner receives only future events");
        }
    }
    private static void RunCallbackFaults(Runtime.NativeRuntime Native)
    {
        foreach (string Body in new[] {"error('ordinary callback')", "coroutine.yield()", "while true do end"})
            using (var Value = new Fixture(Native)) {
                Value.Execute("game:GetService('Players').PlayerDied:Connect(function() " + Body + " end)");
                long Generation = Value.Host.VmGenerationId;
                Value.Emit("died", Value.Victim); string Logs = Value.Drain(true);
                Check(Logs.Contains("died\n"), "ordinary listener makes progress beside faulting callback");
                Check(Value.Host.Ready, "runtime remains ready or recovers after callback failure");
                if (Body == "while true do end")
                    Check(Value.Host.VmGenerationId != Generation && Value.Host.Recoveries == 1, "deadline remains VM-fatal with fresh reconstruction");
            }
    }
    private static byte[] Archive(string Id, string EntrySource)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Write(Zip, "addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"}");
                Write(Zip, "init.luau", EntrySource);
            }
            return Output.ToArray();
        }
    }
    private static void Write(ZipArchive Zip, string Name, string Text)
    { using (Stream Stream = Zip.CreateEntry(Name).Open()) { byte[] Bytes = Encoding.UTF8.GetBytes(Text); Stream.Write(Bytes, 0, Bytes.Length); } }
    private static void RunAddons(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            var Provider = new object();
            const string Source = "game:GetService('Players').PlayerDied:Connect(function(_, C) assert(table.isfrozen(C)); print('addon died') end)";
            string[] Registration = Registry.RegisterArchive(Provider, Archive("gameplayfixture", Source));
            int Guard = 16; while (Registry.HasPending && Guard-- > 0) Registry.ProcessOne();
            Check(!Registry.HasPending && Registry.Status(Provider, Registration[1])[2] == "Active", "addon subscribes in committed candidate");
            Value.Emit("died", Value.Victim); string Logs = Value.Drain();
            Check(Logs.Contains("died\n") && Logs.Contains("addon died\n"), "root and addon use shared existing event scheduler");
            Value.Frame++; Value.Emit("died", Value.Victim); Registry.UnloadProvider(Provider);
            Check(Value.Drain() == "died\n", "provider retirement suppresses captured addon work only");
        }
    }
}
