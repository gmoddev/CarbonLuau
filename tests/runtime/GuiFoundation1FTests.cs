using System;
using System.Collections.Generic;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation1FTests
{
    private static ulong NextRegistration;
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.FacadeWorld World;
        private int NextPlayer;

        internal Fixture(Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.FacadeWorld(Players, new Registrar(), Limits, Backend);
        }

        internal Runtime.PlayerLifetime Add()
        {
            string Id = (76561190000600000L + ++NextPlayer).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Lifecycle" + NextPlayer, Connected = true, Send = Value => { }, Permission = Value => true};
            Views[Id] = View; return Players.Connect(View);
        }

        internal Runtime.PlayerLifetime Reconnect(Runtime.PlayerLifetime Old)
        {
            Runtime.PlayerView Previous = Views[Old.UserId]; Players.Disconnect(Old.UserId, Previous.Identity); Views.Remove(Old.UserId);
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Old.UserId,
                Name = "Reconnected", Connected = true, Send = Value => { }, Permission = Value => true};
            Views[Old.UserId] = View; return Players.Connect(View);
        }

        internal void Drain()
        {
            int Guard = 16384;
            while (World.HasWork && Guard-- > 0) World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
            Check(!World.HasWork, "GUI world drain converged");
        }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 1F: " + Message); }

    private static ulong Create(Runtime.FacadeSession Session, string ClassName, ulong Parent = 0)
    { return UInt64.Parse(Session.Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, () => "1")[0]); }

    private static void Set(Runtime.FacadeSession Session, ulong ObjectId, string Property, params string[] Value)
    {
        var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value);
        Session.Gui.Mutate(Fields.ToArray(), () => "1");
    }

    private static void Show(Runtime.FacadeSession Session, ulong Screen, Runtime.PlayerLifetime Player)
    { Session.Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, () => "1"); }

    private static void Hide(Runtime.FacadeSession Session, ulong Screen, Runtime.PlayerLifetime Player)
    { Session.Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, () => "1"); }

    private static void Destroy(Runtime.FacadeSession Session, ulong ObjectId)
    { Session.Gui.Mutate(new[] {"destroy", ObjectId.ToString()}, () => "1"); }

    private static ulong Clone(Runtime.FacadeSession Session, ulong ObjectId)
    { return UInt64.Parse(Session.Gui.Mutate(new[] {"clone", ObjectId.ToString()}, () => "1")[0]); }

    private static string Connect(Runtime.FacadeSession Session, ulong Button)
    { return Session.Gui.Mutate(new[] {"connect", Button.ToString()}, () => (++NextRegistration).ToString())[0]; }

    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; return null; }

    private static string LatestToken(Runtime.InMemoryGuiBackend Backend, string PlayerToken = null)
    {
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        for (int CallIndex = Calls.Length - 1; CallIndex >= 0; --CallIndex) {
            Runtime.InMemoryGuiBackend.Call Call = Calls[CallIndex];
            if (!Call.Result.Accepted || Call.Kind != Runtime.GuiBackendOperationKind.Replace || Call.Plan == null ||
                (PlayerToken != null && Call.Target.ExactPlayerConnectionToken != PlayerToken)) continue;
            foreach (Runtime.GuiRenderElement Element in Call.Plan.Elements) {
                Runtime.GuiRenderValue Value = Property(Element, Runtime.GuiRenderPropertyId.ActionCommand);
                if (Value != null) return Value.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
            }
        }
        return null;
    }

    private static bool CanAdmit(Fixture Value, Runtime.PlayerLifetime Player, string Token)
    { Runtime.GuiActionAdmission Admission; return Value.World.Gui.TryAdmit(Player, Token, out Admission); }

    private static void Interactive(Runtime.FacadeSession Session, Runtime.PlayerLifetime Player, out ulong Screen, out ulong Button)
    {
        Screen = Create(Session, "ScreenGui"); Button = Create(Session, "TextButton", Screen); Connect(Session, Button); Show(Session, Screen, Player);
    }

    internal static void RunModel()
    {
        RunReplacementAndOwnership();
        RunPendingRetryAndDiagnostics();
        RunStress();
        Console.WriteLine("[CarbonLuau:GuiFoundation1FModel] PASS replacement, ownership, teardown, retry, diagnostics and stress");
    }

    private static void RunReplacementAndOwnership()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime Player = Value.Add();
        var A1 = new Runtime.FacadeSession(Value.World, 10, 11, 256); Value.World.Commit(A1);
        ulong A1Screen, A1Button; Interactive(A1, Player, out A1Screen, out A1Button); Value.Drain();
        string A1Token = LatestToken(Value.Backend); int Calls = Value.Backend.Calls().Length;

        var Failed = new Runtime.FacadeSession(Value.World, 11, 12, 256); ulong FailedScreen, FailedButton;
        Interactive(Failed, Player, out FailedScreen, out FailedButton); Value.World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
        bool A1StillAdmitted = CanAdmit(Value, Player, A1Token);
        Check(Value.Backend.Calls().Length == Calls && A1StillAdmitted,
            "failed root candidate remains provisional while A1 stays authoritative (calls=" + Value.Backend.Calls().Length + "/" + Calls +
            ", token=" + (A1Token == null ? "null" : "present") + ", admitted=" + A1StillAdmitted + ", status=" + Value.World.Gui.ActionStatus + ")");
        Value.World.Retire(Failed);
        Check(Failed.Gui.LiveObjectCount == 0 && Failed.Gui.PresentationCount == 0 && Value.World.Gui.LiveActionCount == 1,
            "failed root candidate retires without changing A1 GUI");

        var A2 = new Runtime.FacadeSession(Value.World, 12, 13, 256); ulong A2Screen, A2Button;
        Interactive(A2, Player, out A2Screen, out A2Button);
        Check(CanAdmit(Value, Player, A1Token), "A1 token remains authoritative before A2 commit");
        Value.World.Commit(A2); Value.World.Retire(A1);
        Check(!Value.World.AdmitGuiAction(Player, A1Token) && A1.PendingCount == 0 && A1.Gui.LiveObjectCount == 0,
            "successful root replacement invalidates A1 tokens, queued callbacks and registry");
        Value.Drain(); string A2Token = LatestToken(Value.Backend);
        Check(A2Token != null && A2Token != A1Token && CanAdmit(Value, Player, A2Token),
            "A2 publishes fresh authority only after commit and synchronization");

        var Owner = new Runtime.FacadeSession(Value.World, 12, 20, 256); Value.World.CommitAddon(null, Owner);
        var Holder = new Runtime.FacadeSession(Value.World, 12, 21, 256); Value.World.CommitAddon(null, Holder);
        ulong SharedScreen, SharedButton; Interactive(Owner, Player, out SharedScreen, out SharedButton);
        Set(Owner, SharedButton, "Text", "string", "mutated by holder");
        string SharedConnection = Connect(Owner, SharedButton); Value.Drain(); string SharedToken = LatestToken(Value.Backend);
        int OwnerObjects = Owner.Gui.LiveObjectCount, OwnerConnections = Owner.Gui.ConnectionCount;
        Value.World.Retire(Holder);
        Check(Owner.Gui.LiveObjectCount == OwnerObjects && Owner.Gui.ConnectionCount == OwnerConnections &&
            CanAdmit(Value, Player, SharedToken), "holder retirement does not destroy owner-domain GUI or Signal resources");
        Value.World.Retire(Owner);
        bool Stale = false; try { Owner.Gui.Query(new[] {"get", SharedButton.ToString(), "Text"}); } catch (InvalidOperationException) { Stale = true; }
        Check(Stale && !Value.World.AdmitGuiAction(Player, SharedToken) && Owner.Gui.ConnectionCount == 0,
            "owner retirement stales escaped references and retires owner-domain Signal authority");
        Check(SharedConnection.Length != 0, "cross-domain sharing fixture registered owner-bound connection");

        Value.World.Retire(A2);
        Check(Value.World.Gui.LiveRegistryCount == 0 && Value.World.Gui.LiveObjects == 0 && Value.World.Gui.LivePresentations == 0 &&
            Value.World.Gui.LiveActionCount == 0 && Value.World.Gui.PlayerActionRateCount == 0 && Value.World.Gui.DomainActionOwnerCount == 0,
            "root/addon/provider-style retirement returns registries and authority to baseline");
    }

    private static void RunPendingRetryAndDiagnostics()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime Player = Value.Add();
        var Session = new Runtime.FacadeSession(Value.World, 30, 31, 256); Value.World.Commit(Session);
        ulong Screen, Button; Interactive(Session, Player, out Screen, out Button);
        Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "offline-1");
        Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "offline-2");
        Value.World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
        Value.World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
        Check(Session.Gui.FullResyncPresentationCount == 1 && Value.World.Gui.RuntimeDiagnostics.BackendSendFailures == 2,
            "repeated full failures retain one latest-state full-resync presentation");
        Value.Drain(); string Token = LatestToken(Value.Backend);
        Check(Token != null && Value.World.Gui.RuntimeDiagnostics.FullRebuilds == 1,
            "full retry succeeds with one accepted latest-state rebuild");

        Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "patch uncertainty");
        Set(Session, Button, "Text", "string", "first"); Value.World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
        Check(Session.Gui.FullResyncPresentationCount == 1 && !Value.World.AdmitGuiAction(Player, Token),
            "failed patch requires full resync and invalidates prior action");
        Set(Session, Button, "Text", "string", "latest"); Value.Drain();
        string Rebuilt = LatestToken(Value.Backend);
        Check(Rebuilt != null && Rebuilt != Token && Value.World.Gui.RuntimeDiagnostics.Patches == 0,
            "latest retained state wins after failed patch without replaying historical revisions");

        Check(Value.World.AdmitGuiAction(Player, Rebuilt) && Session.PendingCount != 0, "Activated work queued before teardown");
        Set(Session, Button, "Text", "string", "pending patch");
        Set(Session, Button, "ZIndex", "integer", "2");
        Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "retire during retry");
        Value.World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
        Hide(Session, Screen, Player);
        Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Destroy, Runtime.GuiBackendResultCode.SendFailed, "best effort destroy");
        Value.World.FlushGui(System.Diagnostics.Stopwatch.StartNew(), 100);
        Runtime.PlayerLifetime Reconnected = Value.Reconnect(Player); Value.World.DisconnectGui(Player);
        Check(!Value.World.AdmitGuiAction(Reconnected, Rebuilt), "old exact Player connection token fails after reconnect");
        Value.World.Retire(Session);
        Check(Session.PendingCount == 0 && !Session.Gui.HasWork && Session.Gui.PendingDestroyCount == 0 &&
            Session.Gui.PresentationCount == 0 && Value.World.Gui.LiveRegistryCount == 0,
            "teardown clears pending patch/full/destroy/hide/action/retry work");

        var Tight = new Fixture(new Runtime.GuiConfig {
            MaxObjectsPerScreen = 2, MaxChildrenPerObject = 2, MaxObjectsPerDomain = 2, MaxObjectsGlobal = 2,
            MaxScreensPerDomain = 1, MaxButtonsPerScreen = 2, MaxTrackedDirtyObjectsPerDomain = 2,
            MaxCloneObjects = 2, MaxRenderElementsPerOperation = 2
        });
        var Limited = new Runtime.FacadeSession(Tight.World, 40, 41, 256); Tight.World.Commit(Limited);
        Create(Limited, "ScreenGui"); Create(Limited, "Frame"); bool Rejected = false;
        try { Create(Limited, "Frame"); } catch (InvalidOperationException) { Rejected = true; }
        Check(Rejected && Tight.World.Gui.RuntimeDiagnostics.ResourceLimitRejections == 1,
            "resource-limit rejection is counted without exposing authority");

        Tight.World.Gui.ActionDiagnostics.Accepted = ulong.MaxValue; Tight.World.Gui.Accepted();
        Tight.World.Gui.RuntimeDiagnostics.BackendSendFailures = ulong.MaxValue; Tight.World.Gui.BackendFailed();
        string Status = Tight.World.Gui.Status;
        Check(Tight.World.Gui.ActionDiagnostics.Accepted == ulong.MaxValue && Tight.World.Gui.RuntimeDiagnostics.BackendSendFailures == ulong.MaxValue &&
            Status.Contains("GUI live registries/objects/screens/presentations/connections/actions") &&
            Status.Contains("dirty/full-resync/blocked/pending-destroy") && Status.Contains("backend full/patch/failures") &&
            Status.IndexOf("carbonluau.gui.action", StringComparison.Ordinal) < 0,
            "bounded saturating diagnostics expose aggregates without token values");
        Tight.World.Retire(Limited);
    }

    private static void RunStress()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime Player = Value.Add();
        Runtime.FacadeSession Root = null;
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            var Next = new Runtime.FacadeSession(Value.World, 1000 + Cycle, 2000 + Cycle, 256);
            ulong Screen = Create(Next, "ScreenGui"); Show(Next, Screen, Player);
            Value.World.Commit(Next); if (Root != null) Value.World.Retire(Root); Root = Next; Value.Drain();
        }
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            var Failed = new Runtime.FacadeSession(Value.World, 3000 + Cycle, 4000 + Cycle, 256);
            ulong Screen = Create(Failed, "ScreenGui"); Show(Failed, Screen, Player); Value.World.Retire(Failed);
        }
        Runtime.FacadeSession Addon = null;
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            var Next = new Runtime.FacadeSession(Value.World, 5000, 6000 + Cycle, 256);
            ulong Screen = Create(Next, "ScreenGui"); Show(Next, Screen, Player);
            Value.World.CommitAddon(Addon, Next); Addon = Next; Value.Drain();
        }

        ulong StressScreen = Create(Root, "ScreenGui"), Template = Create(Root, "Frame", StressScreen);
        for (int Cycle = 0; Cycle < 1000; ++Cycle) { Show(Root, StressScreen, Player); Hide(Root, StressScreen, Player); }
        for (int Cycle = 0; Cycle < 1000; ++Cycle) { ulong Copy = Clone(Root, Template); Destroy(Root, Copy); }
        for (int Cycle = 0; Cycle < 1000; ++Cycle) {
            Runtime.PlayerLifetime Previous = Player; Player = Value.Reconnect(Previous); Value.World.DisconnectGui(Previous);
        }
        Check(Root.Gui.LiveObjectCount == 3 && Root.Gui.PresentationCount == 0 && !Root.Gui.HasWork,
            "1,000 Show/Hide, Clone/Destroy and reconnect cycles retain bounded newest state");

        ulong TokenScreen = Create(Root, "ScreenGui");
        for (int Index = 0; Index < Value.Limits.MaxActionTokensPerPresentation; ++Index) {
            ulong Button = Create(Root, "TextButton", TokenScreen); Connect(Root, Button);
        }
        Show(Root, TokenScreen, Player); Value.Drain();
        Check(Value.World.Gui.LiveActionCount == Value.Limits.MaxActionTokensPerPresentation,
            "action-token saturation reaches but does not exceed the canonical bound");

        var Presentation = new Runtime.FacadeSession(Value.World, 7000, 8000, 256); Value.World.CommitAddon(null, Presentation);
        ulong PresentationScreen = Create(Presentation, "ScreenGui"), PresentationScreen2 = Create(Presentation, "ScreenGui");
        for (int Index = 0; Index < Value.Limits.MaxPresentationsPerDomain; ++Index)
            Show(Presentation, Index < Value.Limits.MaxViewersPerScreen ? PresentationScreen : PresentationScreen2, Value.Add());
        bool PresentationRejected = false;
        try { Show(Presentation, PresentationScreen, Value.Add()); } catch (InvalidOperationException) { PresentationRejected = true; }
        Check(PresentationRejected && Presentation.Gui.PresentationCount == Value.Limits.MaxPresentationsPerDomain,
            "presentation saturation rejects beyond the bounded retained set");

        Value.World.Retire(Presentation); Value.World.Retire(Addon); Value.World.Retire(Root);
        Check(Value.World.Gui.LiveRegistryCount == 0 && Value.World.Gui.LiveObjects == 0 && Value.World.Gui.LivePresentations == 0 &&
            Value.World.Gui.LiveActionCount == 0 && Value.World.Gui.DomainActionOwnerCount == 0 && Value.World.Gui.PlayerActionRateCount == 0,
            "100 root replacements, 100 failed candidates and 100 addon replacements return all GUI registries to baseline");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        const string UserId = "76561190000888888";
        Runtime.PlayerView View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Foundation 1F", Connected = true, Send = Value => { }, Permission = Value => true};
        var Directory = new Runtime.PlayerDirectory(Id => Id == UserId ? View : null); Runtime.PlayerLifetime Player = Directory.Connect(View);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Directory, new Registrar(), Backend);
        string Source = "local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
            "local B=S:Create('TextButton'); B.Text='A1'; B.Activated:Connect(function() print('entered') end); S:Show(P)";
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
        string BeforeCarbonReload;
        using (var Host = new Runtime.ScriptHost(Native,
            new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 5, FrameDrainBudgetMilliseconds = 100}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native root GUI starts"); Host.Drain();
            string A1Token = LatestToken(Backend); Runtime.FacadeSession A1 = World.Active; int Calls = Backend.Calls().Length;
            string Failed = "local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
                "S:Create('TextButton').Text='failed'; S:Show(P); error('reject candidate')";
            Check(Host.Reload(Failed).Status == Runtime.RuntimeStatus.RUNTIME_ERROR && World.Active == A1 && Backend.Calls().Length == Calls &&
                World.AdmitGuiAction(Player, A1Token), "failed native root candidate leaves A1 authoritative and client-visible");

            Source = "local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
                "local B=S:Create('TextButton'); B.Text='A2'; B.Activated:Connect(function() print('fresh') end); S:Show(P)";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && !World.AdmitGuiAction(Player, A1Token) && A1.PendingCount == 0,
                "successful native root replacement retires A1 action and pending callback authority");
            bool StaleA1 = false; try { A1.Gui.Query(new[] {"get", "2", "Text"}); } catch (InvalidOperationException) { StaleA1 = true; }
            Check(StaleA1 && A1.Gui.LiveObjectCount == 0, "native replacement stales old GUI handles"); Host.Drain();

            for (int Cycle = 0; Cycle < 100; ++Cycle) {
                string OldToken = LatestToken(Backend); Runtime.FacadeSession Old = World.Active;
                Check(OldToken != null && World.AdmitGuiAction(Player, OldToken), "pre-recovery action admitted at cycle " + Cycle);
                Check(Host.Execute("gui1f.timeout." + Cycle, "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT,
                    "fatal VM recovery completed at cycle " + Cycle);
                bool OldStale = false; try { Old.Gui.Query(new[] {"get", "2", "Text"}); } catch (InvalidOperationException) { OldStale = true; }
                Check(OldStale && Old.PendingCount == 0 && !World.AdmitGuiAction(Player, OldToken) && World.Gui.LiveRegistryCount == 1,
                    "fatal recovery retires handles, queued work and tokens before reconstruction at cycle " + Cycle);
                Host.Drain(); string NewToken = LatestToken(Backend);
                Check(NewToken != null && NewToken != OldToken && World.Gui.LiveObjects == 2 && World.Gui.LivePresentations == 1 &&
                    World.Gui.LiveActionCount == 1, "committed snapshot reconstructs only fresh GUI state at cycle " + Cycle);
                if (Cycle != 99) { Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "operator reload rearms recovery"); Host.Drain(); }
            }
            BeforeCarbonReload = LatestToken(Backend);
        }
        Check(World.Active == null && World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0 && World.Gui.LivePresentations == 0 &&
            World.Gui.LiveActionCount == 0 && !World.AdmitGuiAction(Player, BeforeCarbonReload),
            "host/Carbon-style unload clears presentations, registries and old action identity");

        using (var Reloaded = new Runtime.ScriptHost(Native,
            new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 20, FrameDrainBudgetMilliseconds = 100}, Snapshot, World)) {
            Check(Reloaded.Reload().Status == Runtime.RuntimeStatus.OK, "fresh host explicitly reconstructs after unload"); Reloaded.Drain();
            string Fresh = LatestToken(Backend);
            Check(Fresh != null && Fresh != BeforeCarbonReload && World.AdmitGuiAction(Player, Fresh) &&
                !World.AdmitGuiAction(Player, BeforeCarbonReload), "fresh host accepts only fresh GUI/token lifetime");
        }
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0 && World.Gui.LivePresentations == 0 &&
            World.Gui.LiveActionCount == 0 && World.Gui.PlayerActionRateCount == 0 && World.Gui.DomainActionOwnerCount == 0,
            "native replacement, 100 recovery cycles and unload/reload return GUI state to baseline");
        Console.WriteLine("[CarbonLuau:GuiFoundation1FNative] PASS root replacement, 100 fatal recoveries, stale authority and host teardown/reload");
    }
}
