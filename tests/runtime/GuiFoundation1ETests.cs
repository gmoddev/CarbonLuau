using System;
using System.Collections.Generic;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation1ETests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.FacadeSession Session;
        internal ulong Registration;

        internal Fixture(int Capacity = 256, Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.FacadeWorld(Players, new Registrar(), Limits, Backend);
            Session = new Runtime.FacadeSession(World, 11, 12, Capacity); World.Commit(Session);
        }
        internal Runtime.PlayerLifetime Add(int Index)
        {
            string Id = (76561190000500000L + Index).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id, Name = "Clicker" + Index,
                Connected = true, Send = Value => { }, Permission = Value => true};
            Views[Id] = View; return Players.Connect(View);
        }
        internal Runtime.PlayerLifetime Reconnect(Runtime.PlayerLifetime Old)
        {
            Runtime.PlayerView Previous = Views[Old.UserId]; Players.Disconnect(Old.UserId, Previous.Identity); Views.Remove(Old.UserId);
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Old.UserId, Name = "Reconnected",
                Connected = true, Send = Value => { }, Permission = Value => true};
            Views[Old.UserId] = View; return Players.Connect(View);
        }
        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Session.Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, NextRegistration)[0]); }
        internal string Connect(ulong Button)
        { return Session.Gui.Mutate(new[] {"connect", Button.ToString()}, NextRegistration)[0]; }
        internal void Set(ulong ObjectId, string Property, params string[] Value)
        { var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value); Session.Gui.Mutate(Fields.ToArray(), NextRegistration); }
        internal void Show(ulong Screen, Runtime.PlayerLifetime Player)
        { Session.Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, NextRegistration); }
        internal void Hide(ulong Screen, Runtime.PlayerLifetime Player)
        { Session.Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, NextRegistration); }
        internal void Flush()
        { int Guard = 1024; while (Session.Gui.HasWork && Guard-- > 0) Session.Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(!Session.Gui.HasWork, "GUI flush converged"); }
        private string NextRegistration() { return (++Registration).ToString(); }
    }

    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("GUI Foundation 1E: " + Message); }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; return null; }
    private static string Token(Runtime.InMemoryGuiBackend Backend, string PlayerToken = null)
    {
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        for (int CallIndex = Calls.Length - 1; CallIndex >= 0; --CallIndex) {
            Runtime.InMemoryGuiBackend.Call Call = Calls[CallIndex];
            if (Call.Kind != Runtime.GuiBackendOperationKind.Replace || Call.Plan == null ||
                (PlayerToken != null && Call.Target.ExactPlayerConnectionToken != PlayerToken)) continue;
            foreach (Runtime.GuiRenderElement Element in Call.Plan.Elements) {
                Runtime.GuiRenderValue Value = Property(Element, Runtime.GuiRenderPropertyId.ActionCommand);
                if (Value != null) return Value.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
            }
        }
        return null;
    }

    internal static void RunModel()
    {
        RunIdentityLifecycleAndIngress();
        RunBoundsAndPublication();
        RunSecurityStress();
        Console.WriteLine("[CarbonLuau:GuiFoundation1EModel] PASS action identity, exact Player binding, lifecycle, rates, queue and publication");
    }

    private static void RunIdentityLifecycleAndIngress()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime A = Value.Add(1), B = Value.Add(2);
        ulong Screen = Value.Create("ScreenGui"), Frame = Value.Create("Frame", Screen), Button = Value.Create("TextButton", Frame);
        Value.Connect(Button); Value.Connect(Button); Value.Show(Screen, A); Value.Show(Screen, B); Value.Flush();
        string AToken = Token(Value.Backend, A.Token), BToken = Token(Value.Backend, B.Token);
        Check(AToken != null && AToken.Length == 32 && BToken != null && AToken != BToken && Value.World.Gui.LiveActionCount == 2,
            "128-bit opaque actions are distinct per exact presentation");
        Check(!Value.World.AdmitGuiAction(B, AToken) && Value.World.Gui.ActionDiagnostics.CrossPlayer == 1, "cross-Player token rejected");
        Check(!Value.World.AdmitGuiAction(A, new string('0', 32)) && Value.World.Gui.ActionDiagnostics.Unknown == 1, "random forged token rejected");
        Check(!Value.World.AdmitGuiAction(A, "bad") && !Value.World.AdmitGuiAction(A, new string('a', 33)) &&
            Value.World.Gui.ActionDiagnostics.Malformed == 2, "malformed and oversized payload rejected before admission");
        Check(Value.World.AdmitGuiAction(A, AToken) && Value.Session.PendingCount == 2 && Value.World.Gui.ActionDiagnostics.Accepted == 1,
            "one admitted action preserves bounded listener fanout");

        Value.Set(Button, "Text", "string", "patched"); Value.Flush();
        Check(Value.World.AdmitGuiAction(A, AToken), "pure property patch retains action identity");
        Value.Set(Frame, "ZIndex", "integer", "2");
        Check(!Value.World.AdmitGuiAction(A, AToken), "full rebuild requirement invalidates old identity immediately");
        Value.Flush(); string Rebuilt = Token(Value.Backend, A.Token);
        Check(Rebuilt != null && Rebuilt != AToken && Value.World.AdmitGuiAction(A, Rebuilt), "full rebuild rotates and activates a fresh identity");

        Value.Set(Button, "Visible", "boolean", "0");
        Check(!Value.World.AdmitGuiAction(A, Rebuilt) && Value.World.Gui.ActionDiagnostics.TargetUnavailable != 0, "hidden button rejects old action");
        Value.Set(Button, "Visible", "boolean", "1"); Value.Flush(); string ButtonVisibleAgain = Token(Value.Backend, A.Token);
        Value.Set(Frame, "Visible", "boolean", "0");
        Check(!Value.World.AdmitGuiAction(A, ButtonVisibleAgain), "hidden ancestry rejects old action");
        Value.Set(Frame, "Visible", "boolean", "1"); Value.Flush(); string VisibleAgain = Token(Value.Backend, A.Token);
        Value.Hide(Screen, A); Check(!Value.World.AdmitGuiAction(A, VisibleAgain), "Hide invalidates active action");
        Value.Show(Screen, A); Value.Flush(); string ShownAgain = Token(Value.Backend, A.Token);
        Check(ShownAgain != VisibleAgain, "Hide then Show creates a new presentation action");
        Runtime.PlayerLifetime Reconnected = Value.Reconnect(A);
        Check(!Value.World.AdmitGuiAction(Reconnected, ShownAgain), "same-account reconnect cannot reuse old connection action");
        Value.World.DisconnectGui(A);

        var Destroy = new Fixture(); Runtime.PlayerLifetime Player = Destroy.Add(3);
        ulong DestroyScreen = Destroy.Create("ScreenGui"), DestroyButton = Destroy.Create("TextButton", DestroyScreen);
        Destroy.Connect(DestroyButton); Destroy.Show(DestroyScreen, Player); Destroy.Flush(); string DestroyToken = Token(Destroy.Backend);
        Destroy.Session.Gui.Mutate(new[] {"destroy", DestroyButton.ToString()}, () => "unused");
        Check(!Destroy.World.AdmitGuiAction(Player, DestroyToken), "destroyed TextButton action rejected");

        var ScreenDestroy = new Fixture(); Runtime.PlayerLifetime ScreenPlayer = ScreenDestroy.Add(4);
        ulong DeadScreen = ScreenDestroy.Create("ScreenGui"), DeadButton = ScreenDestroy.Create("TextButton", DeadScreen);
        ScreenDestroy.Connect(DeadButton); ScreenDestroy.Show(DeadScreen, ScreenPlayer); ScreenDestroy.Flush(); string ScreenToken = Token(ScreenDestroy.Backend);
        ScreenDestroy.Session.Gui.Mutate(new[] {"destroy", DeadScreen.ToString()}, () => "unused");
        Check(!ScreenDestroy.World.AdmitGuiAction(ScreenPlayer, ScreenToken), "destroyed ScreenGui action rejected");

        var Uncertain = new Fixture(); Runtime.PlayerLifetime UncertainPlayer = Uncertain.Add(5);
        ulong UncertainScreen = Uncertain.Create("ScreenGui"), UncertainButton = Uncertain.Create("TextButton", UncertainScreen);
        Uncertain.Connect(UncertainButton); Uncertain.Show(UncertainScreen, UncertainPlayer); Uncertain.Flush(); string UncertainToken = Token(Uncertain.Backend);
        Uncertain.Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "injected uncertainty");
        Uncertain.Set(UncertainButton, "Text", "string", "uncertain"); Uncertain.Session.Gui.FlushOne(Uncertain.Limits.MaxSerializedBytesPerFlush);
        Check(!Uncertain.World.AdmitGuiAction(UncertainPlayer, UncertainToken), "known resync invalidates prior action immediately");
        Uncertain.Flush(); Check(Token(Uncertain.Backend) != UncertainToken, "known resync publishes a fresh action");
    }

    private static void RunBoundsAndPublication()
    {
        var Rate = new Fixture(); Runtime.PlayerLifetime Player = Rate.Add(10);
        ulong Screen = Rate.Create("ScreenGui"), A = Rate.Create("TextButton", Screen), B = Rate.Create("TextButton", Screen), C = Rate.Create("TextButton", Screen);
        Rate.Connect(A); Rate.Connect(B); Rate.Connect(C); Rate.Show(Screen, Player); Rate.Flush();
        var Tokens = new List<string>();
        Runtime.InMemoryGuiBackend.Call Last = Rate.Backend.Calls()[Rate.Backend.Calls().Length - 1];
        foreach (Runtime.GuiRenderElement Element in Last.Plan.Elements) {
            Runtime.GuiRenderValue Command = Property(Element, Runtime.GuiRenderPropertyId.ActionCommand);
            if (Command != null) Tokens.Add(Command.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1));
        }
        for (int Index = 0; Index < 8; ++Index) Check(Rate.World.AdmitGuiAction(Player, Tokens[0]), "per-action burst accepts configured traffic");
        Check(!Rate.World.AdmitGuiAction(Player, Tokens[0]), "per-action limiter rejects excess traffic");
        for (int Index = 0; Index < 8; ++Index) Check(Rate.World.AdmitGuiAction(Player, Tokens[1]), "second action shares Player budget");
        for (int Index = 0; Index < 4; ++Index) Check(Rate.World.AdmitGuiAction(Player, Tokens[2]), "Player burst reaches configured total");
        Check(!Rate.World.AdmitGuiAction(Player, Tokens[2]) && Rate.World.Gui.ActionDiagnostics.RateLimited >= 2,
            "per-Player limiter rejects aggregate excess");

        var Queue = new Fixture(1); Runtime.PlayerLifetime QueuePlayer = Queue.Add(20);
        ulong QueueScreen = Queue.Create("ScreenGui"), QueueButton = Queue.Create("TextButton", QueueScreen);
        Queue.Connect(QueueButton); Queue.Connect(QueueButton); Queue.Show(QueueScreen, QueuePlayer); Queue.Flush();
        Check(!Queue.World.AdmitGuiAction(QueuePlayer, Token(Queue.Backend)) && Queue.Session.PendingCount == 0 &&
            Queue.World.Gui.ActionDiagnostics.QueueFull == 1, "listener batch is atomically rejected at queue capacity");

        var ListenerBound = new Fixture(); ulong ListenerScreen = ListenerBound.Create("ScreenGui");
        ulong ListenerButton = ListenerBound.Create("TextButton", ListenerScreen);
        for (int Index = 0; Index < ListenerBound.Limits.MaxSignalConnectionsPerButton; ++Index) ListenerBound.Connect(ListenerButton);
        bool ListenerRejected = false; try { ListenerBound.Connect(ListenerButton); } catch (InvalidOperationException) { ListenerRejected = true; }
        Check(ListenerRejected, "TextButton Activated listener bound rejects excess registrations");

        var Publication = new Fixture(); Runtime.PlayerLifetime PublicationPlayer = Publication.Add(30);
        Publication.Session.Gui.BeginPublication(); ulong ProvisionalScreen = Publication.Create("ScreenGui");
        ulong ProvisionalButton = Publication.Create("TextButton", ProvisionalScreen); Publication.Connect(ProvisionalButton);
        Publication.Show(ProvisionalScreen, PublicationPlayer); Check(Publication.Session.Gui.FlushOne(Publication.Limits.MaxSerializedBytesPerFlush) == 0,
            "provisional presentation cannot flush");
        Publication.Session.Gui.RollbackPublication(); Check(Publication.World.Gui.LiveActionCount == 0 && Publication.Backend.Calls().Length == 0,
            "publication rollback exposes no token or client GUI");
        Publication.Session.Gui.BeginPublication(); ProvisionalScreen = Publication.Create("ScreenGui");
        ProvisionalButton = Publication.Create("TextButton", ProvisionalScreen); Publication.Connect(ProvisionalButton);
        Publication.Show(ProvisionalScreen, PublicationPlayer); Publication.Session.Gui.CommitPublication();
        Check(Publication.World.Gui.LiveActionCount == 0, "publication commit creates no authority before synchronization");
        Publication.Flush(); Check(Publication.World.Gui.LiveActionCount == 1, "committed synchronization activates bounded authority");

        var TightConfig = new Runtime.GuiConfig {MaxActionTokensPerPresentation = 1};
        var Tight = new Fixture(256, TightConfig); Runtime.PlayerLifetime TightPlayer = Tight.Add(40);
        ulong TightScreen = Tight.Create("ScreenGui"), TightA = Tight.Create("TextButton", TightScreen), TightB = Tight.Create("TextButton", TightScreen);
        Tight.Connect(TightA); Tight.Connect(TightB); Tight.Show(TightScreen, TightPlayer); Tight.Flush();
        Check(Tight.World.Gui.LiveActionCount == 0, "token registry bound fails closed without partial publication");

        var TombstoneConfig = new Runtime.GuiConfig {MaxActionTokensPerPresentation = 1, MaxActionTokensPerDomain = 4, MaxActionTokensGlobal = 4};
        var Tombstones = new Fixture(256, TombstoneConfig); Runtime.PlayerLifetime TombstonePlayer = Tombstones.Add(41);
        ulong TombstoneScreen = Tombstones.Create("ScreenGui"), TombstoneButton = Tombstones.Create("TextButton", TombstoneScreen);
        Tombstones.Connect(TombstoneButton);
        for (int Cycle = 0; Cycle < 20; ++Cycle) {
            Tombstones.Show(TombstoneScreen, TombstonePlayer); Tombstones.Flush();
            Tombstones.Hide(TombstoneScreen, TombstonePlayer); Tombstones.Flush();
        }
        Check(Tombstones.World.Gui.LiveActionCount == 0 && Tombstones.World.Gui.RetiredActionCount <= 4,
            "retired-token diagnostics remain bounded under repeated presentation rotation");
    }

    private static void RunSecurityStress()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime A = Value.Add(50), B = Value.Add(51);
        ulong Screen = Value.Create("ScreenGui"), Button = Value.Create("TextButton", Screen); Value.Connect(Button);
        Value.Show(Screen, A); Value.Flush(); string Action = Token(Value.Backend);
        for (int Index = 1; Index <= 10000; ++Index)
            Check(!Value.World.AdmitGuiAction(A, Index.ToString("x32")), "forged stress input stays rejected");
        for (int Index = 0; Index < 10000; ++Index) Value.World.AdmitGuiAction(B, Action);
        for (int Index = 0; Index < 10000; ++Index) Value.World.AdmitGuiAction(A, Action);
        Value.Set(Button, "ZIndex", "integer", "2");
        for (int Index = 0; Index < 10000; ++Index) Value.World.AdmitGuiAction(A, Action);
        Check(Value.World.Gui.LiveActionCount == 0 && Value.World.Gui.RetiredActionCount <= Value.Limits.MaxActionTokensGlobal &&
            Value.Session.PendingCount <= Value.Limits.MaxActionInteractionBurst && Value.World.Gui.ActionDiagnostics.Unknown >= 10000 &&
            Value.World.Gui.ActionDiagnostics.CrossPlayer >= 10000 && Value.World.Gui.ActionDiagnostics.RateLimited >= 9992 &&
            Value.World.Gui.ActionDiagnostics.Stale >= 10000,
            "high-volume forged, cross-Player, rate-limited and stale input has bounded state and scheduler admission");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        const string UserId = "76561190000999999";
        Runtime.PlayerView View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId, Name = "Native Clicker",
            Connected = true, Send = Value => { }, Permission = Value => true};
        var Directory = new Runtime.PlayerDirectory(Id => Id == UserId ? View : null); Runtime.PlayerLifetime Player = Directory.Connect(View);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Directory, new Registrar(), Backend);
        string Source = "local State=require('state'); local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
            "local B=S:Create('TextButton'); B.Text='Ready'; B.Activated:Connect(function(V) assert(V==P); print('first'); B.Text='Done' end); " +
            "B.Activated:Connect(function(V) assert(V==P); print('second') end); S:Show(P); State.S=S; State.B=B; State.P=P";
        Func<Runtime.ScriptSnapshot> Snapshot = () => { var Result = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source}; Result.Modules.Add("state", "return {}"); return Result; };
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Backend.Calls().Length == 0, "candidate publishes no client action before commit"); Host.Drain();
            string Action = Token(Backend); int Calls = Backend.Calls().Length;
            Check(World.AdmitGuiAction(Player, Action) && World.AdmitGuiAction(Player, Action) && Backend.Calls().Length == Calls,
                "repeated ingress queues without recursive Luau or CUI entry");
            string Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "first\nsecond\nfirst\nsecond\n" &&
                Backend.Calls()[Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Update,
                "repeated Activated preserves listener order, exact Player and later dirty flush");
            Action = Token(Backend); Check(World.AdmitGuiAction(Player, Action), "action admitted for pre-entry stale test");
            Check(Host.Execute("gui1e.hide", "local S=require('state'); S.S:Hide(S.P)").Status == Runtime.RuntimeStatus.OK, "hide mutation succeeds before queued callback");
            Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "" && World.Gui.ActionDiagnostics.PreEntryStale != 0, "scheduler gate suppresses stale action before Luau entry");

            Check(Host.Execute("gui1e.reshow", "local S=require('state'); S.S:Show(S.P)").Status == Runtime.RuntimeStatus.OK,
                "presentation can be shown again before reparent test"); Host.Drain();
            string BeforeReparent = Token(Backend); Check(World.AdmitGuiAction(Player, BeforeReparent), "action admitted before reparent");
            Check(Host.Execute("gui1e.detach", "local S=require('state'); S.B.Parent=nil").Status == Runtime.RuntimeStatus.OK,
                "button can detach before queued callback");
            Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "", "pre-entry gate suppresses action after button reparent away");
            Check(Host.Execute("gui1e.reattach", "local S=require('state'); S.B.Parent=S.S").Status == Runtime.RuntimeStatus.OK,
                "button can reattach for recovery test"); Host.Drain();
            string BeforeRecovery = Token(Backend);
            Check(World.AdmitGuiAction(Player, BeforeRecovery), "action admitted before fatal VM recovery");
            Check(Host.Execute("gui1e.timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT,
                "fatal deadline recovery reconstructs the VM");
            Check(!World.AdmitGuiAction(Player, BeforeRecovery), "fatal VM recovery rejects the prior generation action");
            Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "", "fatal VM recovery discards queued prior-generation action");

            Source = "local State=require('state'); local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
                "local B=S:Create('TextButton'); B.Activated:Connect(function() print('entered'); B:Destroy(); print('done') end); " +
                "B.Activated:Connect(function() print('late') end); S:Show(P); State.S=S";
            string BeforeReplacement = Token(Backend); Check(World.AdmitGuiAction(Player, BeforeReplacement),
                "action admitted before domain replacement");
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "replacement domain installs self-destroy callback");
            Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "", "domain replacement discards queued old-domain action");
            string ReplacementAction = Token(Backend); Check(!World.AdmitGuiAction(Player, BeforeReplacement), "replacement domain rejects old action");
            Check(World.AdmitGuiAction(Player, ReplacementAction), "replacement action admitted");
            Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "entered\ndone\n", "current callback completes while later queued listener is suppressed after self-destroy");
        }
        Console.WriteLine("[CarbonLuau:GuiFoundation1ENative] PASS scheduler-gated Activated, mutation, replacement and no recursive VM entry");
    }
}
