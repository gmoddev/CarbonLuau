using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Existing Player and GUI Signals exercise the real embedded bootstrap/compiler
// and VM. These fixtures introduce no public gameplay event or test host opcode.
internal static class GameplayPublicationTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Gameplay publication: " + Message); }

    private const string State = "local S=require('state'); ";
    private const string Setup = State +
        "S.Signal=game:GetService('Players').PlayerAdded; " +
        "S.PlayerConnection=S.Signal:Connect(function() print('player old') end); " +
        "S.Screen=game:GetService('Gui'):Create('ScreenGui'); S.Button=S.Screen:Create('TextButton'); " +
        "S.GuiConnection=S.Button.Activated:Connect(function() print('gui old') end); " +
        "S.OldPlayerConnection=S.PlayerConnection; S.OldGuiConnection=S.GuiConnection; " +
        "S.Screen:Show(game:GetService('Players'):GetPlayers()[1]); " +
        "S.Weak=setmetatable({},{__mode='v'}); S.FailedIndex=0; S.Mutations=0; ";
    private const string Disconnect = "S.OldPlayerConnection:Disconnect(); S.OldGuiConnection:Disconnect(); ";
    private const string Connect =
        "S.PlayerConnection=S.Signal:Connect(function() print('player new') end); " +
        "S.GuiConnection=S.Button.Activated:Connect(function() print('gui new') end); ";
    private const string FailedConnect = State +
        "S.FailedIndex+=1; local Payload={Buffer=buffer.create(65536)}; S.Weak[S.FailedIndex]=Payload; " +
        "S.LeakedPlayer=S.Signal:Connect(function() print(buffer.len(Payload.Buffer)) end); " +
        "S.LeakedGui=S.Button.Activated:Connect(function() print(buffer.len(Payload.Buffer)) end); " +
        "error('failed connect')";

    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.PlayerLifetime Player;
        internal readonly Runtime.ScriptHost Host;
        internal string Source = Setup;

        internal Fixture(Runtime.NativeRuntime Native)
        {
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(),
                UserId = "76561190000999888", Name = "Publication fixture", Connected = true,
                Send = Text => { }, Permission = Name => true};
            var Players = new Runtime.PlayerDirectory(Id => Id == View.UserId ? View : null);
            Player = Players.Connect(View);
            var Limits = new Runtime.GuiConfig {MaxPlayerInteractionBurst = 64, MaxActionInteractionBurst = 64}.Validate();
            World = new Runtime.FacadeWorld(Players, new Registrar(), Limits, Backend);
            Host = new Runtime.ScriptHost(Native,
                new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World);
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "initial committed subscriptions");
            Drain(Host);
        }
        private Runtime.ScriptSnapshot Snapshot()
        {
            var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
            Value.Modules.Add("state", "return {}");
            Value.Modules.Add("disconnectfail", State + "S.Mutations+=1; " + Disconnect + "error('failed disconnect')");
            Value.Modules.Add("connectfail", FailedConnect);
            Value.Modules.Add("destroyfail", State + "S.Button:Destroy(); error('failed destroy')");
            Value.Modules.Add("innersuccess", State + Disconnect + Connect + "return true");
            Value.Modules.Add("outerfail", "assert(require('innersuccess')); error('failed outer scope')");
            Value.Modules.Add("caughtinner", "assert(not pcall(require,'disconnectfail')); return true");
            Value.Modules.Add("disconnectsuccess", State + Disconnect + "return true");
            Value.Modules.Add("connectsuccess", State + Connect + "return true");
            return Value;
        }
        internal void Execute(string Source)
        {
            Runtime.ExecutionResult Result = Host.Execute("gameplay.publication", Source);
            Check(Result.Status == Runtime.RuntimeStatus.OK, "real VM execution: " + Result.Error);
        }
        internal void Both(string Expected)
        {
            Drain(Host);
            World.Event("added", Player);
            Check(World.AdmitGuiAction(Player, ActionToken(Backend)), "GUI production action admission");
            Check(Drain(Host) == Expected, "both existing Signal callback roots: " + Expected);
        }
        internal void CollectThroughFailedCandidate()
        {
            Runtime.FacadeSession Previous = World.Active;
            string PreviousSource = Source; Source = "error('collection candidate rejected')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && World.Active == Previous,
                "failed candidate preserves active domain while native domain teardown collects");
            Source = PreviousSource;
        }
        public void Dispose()
        {
            Host.Dispose();
            Check(World.Active == null && World.Gui.LiveObjects == 0 && World.Gui.LiveRegistryCount == 0,
                "teardown releases all owned registrations and GUI state");
        }
    }

    private static string Drain(Runtime.ScriptHost Host)
    {
        var Logs = new StringBuilder();
        for (int Frame = 0; Frame < 100 && Host.HasWork; ++Frame)
            foreach (Runtime.ExecutionResult Result in Host.Drain()) {
                Check(Result.Status == Runtime.RuntimeStatus.OK, "callback error: " + Result.Error);
                Logs.Append(Result.Logs);
            }
        Check(!Host.HasWork, "bounded drain converges"); return Logs.ToString();
    }
    private static string ActionToken(Runtime.InMemoryGuiBackend Backend)
    {
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        for (int Index = Calls.Length - 1; Index >= 0; --Index) {
            if (Calls[Index].Plan == null) continue;
            foreach (Runtime.GuiRenderElement Element in Calls[Index].Plan.Elements)
                foreach (Runtime.GuiRenderProperty Property in Element.Properties)
                    if (Property.Id == Runtime.GuiRenderPropertyId.ActionCommand)
                        return Property.Value.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
        }
        throw new Exception("Gameplay publication: no synchronized action token");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            Value.Both("player old\ngui old\n");
            foreach (string Module in new[] {"disconnectfail", "connectfail", "destroyfail", "outerfail"}) {
                Value.Execute("assert(not pcall(require,'" + Module + "'))");
                Check(Value.World.Active.ListenerCount == 1 && Value.World.Active.Gui.ConnectionCount == 1,
                    "failed scope restores managed registration state: " + Module);
                Value.Both("player old\ngui old\n");
            }
            Value.Execute("assert(require('caughtinner')); " + State + "assert(S.Mutations==2)");
            Value.Both("player old\ngui old\n");
            // The Lua shared state counter remains mutated; only owned callback
            // roots and registrations roll back under D7.
            Value.World.Event("added", Value.Player);
            Check(Value.World.AdmitGuiAction(Value.Player, ActionToken(Value.Backend)), "queue before successful disconnect");
            Value.Execute("assert(require('disconnectsuccess'))");
            Check(Value.World.Active.ListenerCount == 0 && Value.World.Active.Gui.ConnectionCount == 0 &&
                Drain(Value.Host) == "", "committed disconnect suppresses previously queued callbacks");
            Value.Execute("assert(require('connectsuccess'))");
            Value.Both("player new\ngui new\n");
            Value.Execute(State + "S.PlayerConnection:Disconnect(); S.PlayerConnection:Disconnect(); " +
                "S.GuiConnection:Disconnect(); S.GuiConnection:Disconnect()");
            Check(Value.World.Active.ListenerCount == 0 && Value.World.Active.Gui.ConnectionCount == 0,
                "restored GUI Connection can later disconnect and remains idempotent");
        }
        using (var Value = new Fixture(Native)) {
            Value.Execute("assert(not pcall(require,'connectfail'))");
            Value.CollectThroughFailedCandidate();
            ulong Before = Value.Host.VmMemoryBytes;
            for (int Index = 0; Index < 80; ++Index) Value.Execute("assert(not pcall(require,'connectfail'))");
            Value.CollectThroughFailedCandidate();
            Value.Execute(State + "assert(next(S.Weak)==nil, 'failed Connect retained callback capture')");
            Check(Value.Host.VmMemoryBytes <= Before + 256 * 1024,
                "failed Connect closures converge after full collection instead of retaining 5 MiB");
            Value.Both("player old\ngui old\n");
        }
        RunForeign(Native);
        Check(Native.LiveVmCount == 0, "real VM fixture ownership converges");
        Console.WriteLine("[CarbonLuau:GameplayPublication] PASS existing Player/GUI callback publication, nested/foreign failures and memory convergence");
    }

    private static byte[] Archive(string Manifest, string Entry, params string[] Modules)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Write(Zip, "addon.json", Manifest); Write(Zip, "init.luau", Entry);
                for (int Index = 0; Index < Modules.Length; Index += 2) Write(Zip, Modules[Index] + ".luau", Modules[Index + 1]);
            }
            return Output.ToArray();
        }
    }
    private static void Write(ZipArchive Zip, string Name, string Text)
    { using (Stream Stream = Zip.CreateEntry(Name).Open()) { byte[] Bytes = Encoding.UTF8.GetBytes(Text); Stream.Write(Bytes, 0, Bytes.Length); } }

    private static void Activate(Runtime.AddonRegistry Registry, object Provider, byte[] Package, string Expected)
    {
        string[] Registration = Registry.RegisterArchive(Provider, Package);
        int Guard = 16; while (Registry.HasPending && Guard-- > 0) Registry.ProcessOne();
        Check(!Registry.HasPending && Registry.Status(Provider, Registration[1])[2] == Expected, "bounded foreign addon activation: " + Expected);
    }
    private static void RunForeign(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture(Native)) {
            // Retire the root's fixture subscriptions, keeping the same VM.
            Value.Source = "return true"; Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.OK, "foreign fixture root");
            object Provider = new object();
            using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
                Activate(Registry, Provider, Archive(
                    "{\"schema\":1,\"id\":\"publicationowner\",\"version\":\"1.0.0\",\"main\":\"api\",\"publicModules\":[\"failed\",\"success\"]}",
                    "require('api')", "state", "return {}", "api", Setup + "return S",
                    "failed", State + Disconnect + "S.Signal:Connect(function() print('foreign leak') end); " +
                        "S.Button.Activated:Connect(function() print('foreign GUI leak') end); error('foreign cold failure')",
                    "success", State + Disconnect + "return true"), "Active");
                Value.Both("player old\ngui old\n");
                const string Dependencies = ",\"dependencies\":{\"required\":[\"publicationowner\"],\"optional\":[]}}";
                Activate(Registry, Provider, Archive("{\"schema\":1,\"id\":\"publicationcaught\",\"version\":\"1.0.0\"" + Dependencies,
                    "assert(not pcall(require,'@publicationowner/failed'))"), "Active");
                Value.Both("player old\ngui old\n");
                Activate(Registry, Provider, Archive("{\"schema\":1,\"id\":\"publicationfailed\",\"version\":\"1.0.0\"" + Dependencies,
                    "assert(require('@publicationowner/success')); error('parent candidate failure')"), "Failed");
                Value.Both("player old\ngui old\n");
                Activate(Registry, Provider, Archive("{\"schema\":1,\"id\":\"publicationcommitted\",\"version\":\"1.0.0\"" + Dependencies,
                    "assert(require('@publicationowner/success'))"), "Active");
                foreach (Runtime.FacadeSession Session in Value.World.Sessions())
                    Check(Session.ListenerCount == 0 && Session.Gui.ConnectionCount == 0, "foreign successful scope commits disconnect");
                Value.World.Event("added", Value.Player);
                Check(Drain(Value.Host) == "", "foreign committed disconnect has no delivery");
            }
        }
    }
}
