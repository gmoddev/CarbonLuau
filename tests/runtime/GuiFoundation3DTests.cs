using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation3DTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.GuiRetainedWorld World;
        internal readonly Runtime.GuiRetainedRegistry Gui;
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>();
        private int NextPlayer; private ulong NextRegistration;

        internal Fixture() : this(new Runtime.GuiConfig().Validate()) { }
        internal Fixture(Runtime.GuiLimits Limits)
        {
            this.Limits = Limits;
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.GuiRetainedWorld(Limits, Players, Backend); Gui = new Runtime.GuiRetainedRegistry(World, 120, 121);
        }
        internal Runtime.PlayerLifetime AddPlayer()
        {
            string Id = (76561190005000000L + ++NextPlayer).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id, Name = "Scroll" + NextPlayer,
                Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Players.Connect(View);
        }
        internal Runtime.PlayerLifetime Reconnect(Runtime.PlayerLifetime Old)
        {
            Runtime.PlayerView View = Views[Old.UserId]; Players.Disconnect(Old.UserId, View.Identity); Gui.Disconnect(Old);
            View.Identity = new object(); View.Connection = new object(); return Players.Connect(View);
        }
        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Token)[0]); }
        internal void Set(ulong Id, string Name, params string[] Value)
        { var Fields = new List<string> {"set", Id.ToString(), Name}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), Token); }
        internal void Show(ulong Screen, Runtime.PlayerLifetime Player)
        { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Token); }
        internal void Hide(ulong Screen, Runtime.PlayerLifetime Player)
        { Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, Token); }
        internal void Scroll(ulong Id, Runtime.PlayerLifetime Player, double X, double Y)
        { Gui.Mutate(new[] {"scroll", Id.ToString(), Player.Token, Player.UserId, X.ToString("R", System.Globalization.CultureInfo.InvariantCulture), Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, Token); }
        internal void Top(ulong Id, Runtime.PlayerLifetime Player)
        { Gui.Mutate(new[] {"scrolltop", Id.ToString(), Player.Token, Player.UserId}, Token); }
        internal void Bottom(ulong Id, Runtime.PlayerLifetime Player)
        { Gui.Mutate(new[] {"scrollbottom", Id.ToString(), Player.Token, Player.UserId}, Token); }
        internal void Drain()
        { int Guard = 10000; while (Gui.HasWork && Guard-- > 0) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(!Gui.HasWork, "GUI flush converged"); }
        internal string Token() { return (++NextRegistration).ToString(); }
        public void Dispose() { Gui.Dispose(); }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 3D: " + Message); }
    private static void Reject(Action Value, string Message)
    { bool Rejected = false; try { Value(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static Runtime.InMemoryGuiBackend.Call Last(Runtime.InMemoryGuiBackend Backend)
    { Runtime.InMemoryGuiBackend.Call[] Values = Backend.Calls(); return Values[Values.Length - 1]; }
    private static bool HasMethod(Runtime.GuiClassId ClassId, Runtime.GuiMethodId Method)
    { foreach (Runtime.GuiMethodId Value in Runtime.GuiSchema.GetClass(ClassId).Methods) if (Value == Method) return true; return false; }

    internal static void RunModel()
    {
        RunSchemaAndBackendMapping();
        RunTargetingAxesAndValidation();
        RunPublicationRebuildAndRetry();
        RunLifecycleStressAndFairness();
        RunBounds();
        Console.WriteLine("[CarbonLuau:GuiFoundation3DModel] PASS targeting, axes, coalescing, bounds, publication, retry, lifecycle, fairness and stress");
    }

    private static void RunSchemaAndBackendMapping()
    {
        Runtime.GuiSchema.Validate();
        foreach (Runtime.GuiMethodId Method in new[] {Runtime.GuiMethodId.ScrollTo, Runtime.GuiMethodId.ScrollToTop, Runtime.GuiMethodId.ScrollToBottom}) {
            Check(HasMethod(Runtime.GuiClassId.ScrollingFrame, Method), "ScrollingFrame exposes " + Method);
            Check(!HasMethod(Runtime.GuiClassId.Frame, Method) && !HasMethod(Runtime.GuiClassId.ScreenGui, Method), Method + " is ScrollingFrame-only");
        }
        Runtime.GuiPropertyUse Ignored;
        Check(!Runtime.GuiSchema.TryGetProperty(Runtime.GuiClassId.ScrollingFrame, "CanvasPosition", out Ignored), "CanvasPosition remains absent");
        var XY = new Runtime.GuiScrollEffect("scroll", 1, 2, 0.25, 0.75);
        string Json = Runtime.RustCuiBackend.SerializeScroll(XY);
        Check(Json.Contains("\"horizontalNormalizedPosition\":0.25") && Json.Contains("\"verticalNormalizedPosition\":0.25"),
            "public midpoint-style coordinates map to backend horizontal and inverted vertical fields");
        string Top = Runtime.RustCuiBackend.SerializeScroll(new Runtime.GuiScrollEffect("scroll", 1, 2, null, 0));
        string Bottom = Runtime.RustCuiBackend.SerializeScroll(new Runtime.GuiScrollEffect("scroll", 1, 2, null, 1));
        string Left = Runtime.RustCuiBackend.SerializeScroll(new Runtime.GuiScrollEffect("scroll", 1, 2, 0, null));
        string Right = Runtime.RustCuiBackend.SerializeScroll(new Runtime.GuiScrollEffect("scroll", 1, 2, 1, null));
        Check(Top.Contains("\"verticalNormalizedPosition\":1.0") && Bottom.Contains("\"verticalNormalizedPosition\":0.0"), "top and bottom map exactly");
        Check(Left.Contains("\"horizontalNormalizedPosition\":0.0") && Right.Contains("\"horizontalNormalizedPosition\":1.0"), "left and right map exactly");
        Check(!Top.Contains("horizontalNormalizedPosition") && !Left.Contains("verticalNormalizedPosition"), "axis-specific effects serialize only their axis");
        Check(!XY.Describe().Contains("NormalizedPosition") && !XY.Describe().Contains("Unity"), "private host convention is absent from backend-neutral effect state");
    }

    private static void RunTargetingAxesAndValidation()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.AddPlayer(), B = Value.AddPlayer(), Hidden = Value.AddPlayer();
            ulong Screen = Value.Create("ScreenGui"), Scroll = Value.Create("ScrollingFrame", Screen), Frame = Value.Create("Frame", Screen);
            Value.Set(Scroll, "ScrollingDirection", "string", "XY"); Value.Show(Screen, A); Value.Show(Screen, B); Value.Drain();
            int Before = Value.Backend.Calls().Length; Value.Scroll(Scroll, A, 0.2, 0.8); Value.Drain();
            Runtime.InMemoryGuiBackend.Call Call = Last(Value.Backend);
            Check(Value.Backend.Calls().Length == Before + 1 && Call.Kind == Runtime.GuiBackendOperationKind.Scroll &&
                Call.Target.ExactPlayerConnectionToken == A.Token && Call.ScrollEffect.Horizontal == 0.2 && Call.ScrollEffect.Vertical == 0.8,
                "ScrollTo targets only the exact Player Presentation");
            Value.Scroll(Scroll, B, 0.7, 0.3); Value.Drain();
            Check(Last(Value.Backend).Target.ExactPlayerConnectionToken == B.Token, "two viewers receive isolated effects");

            Value.Set(Scroll, "ScrollingDirection", "string", "X"); Value.Drain(); Value.Scroll(Scroll, A, 0.4, 0.9); Value.Drain();
            Check(Last(Value.Backend).ScrollEffect.Horizontal == 0.4 && !Last(Value.Backend).ScrollEffect.Vertical.HasValue, "X transmits no Y");
            Reject(() => Value.Top(Scroll, A), "Top rejects when Y scrolling is unavailable");
            Value.Set(Scroll, "ScrollingDirection", "string", "Y"); Value.Drain(); Value.Top(Scroll, A); Value.Drain();
            Check(!Last(Value.Backend).ScrollEffect.Horizontal.HasValue && Last(Value.Backend).ScrollEffect.Vertical == 0, "Top transmits only Y=0");
            Value.Bottom(Scroll, A); Value.Drain(); Check(Last(Value.Backend).ScrollEffect.Vertical == 1, "Bottom transmits only Y=1");

            Reject(() => Value.Gui.Mutate(new[] {"scroll", Frame.ToString(), A.Token, A.UserId, "0", "0"}, Value.Token), "non-ScrollingFrame invocation rejects");
            Reject(() => Value.Gui.Mutate(new[] {"scroll", Scroll.ToString(), A.Token, A.UserId, "NaN", "0"}, Value.Token), "NaN rejects");
            Reject(() => Value.Gui.Mutate(new[] {"scroll", Scroll.ToString(), A.Token, A.UserId, "Infinity", "0"}, Value.Token), "infinity rejects");
            Reject(() => Value.Gui.Mutate(new[] {"scroll", Scroll.ToString(), A.Token, A.UserId, "-0.1", "0"}, Value.Token), "negative rejects");
            Reject(() => Value.Gui.Mutate(new[] {"scroll", Scroll.ToString(), A.Token, A.UserId, "0", "1.1"}, Value.Token), "greater-than-one rejects");
            Reject(() => Value.Scroll(Scroll, Hidden, 0, 0), "screen-not-shown rejects");
            Runtime.PlayerLifetime Reconnected = Value.Reconnect(A);
            Reject(() => Value.Scroll(Scroll, A, 0, 0), "stale same-account connection rejects");
            Reject(() => Value.Scroll(Scroll, Reconnected, 0, 0), "reconnect does not inherit a Presentation");
        }
    }

    private static void RunPublicationRebuildAndRetry()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.AddPlayer(); ulong Screen = Value.Create("ScreenGui"), Scroll = Value.Create("ScrollingFrame", Screen);
            Value.Set(Scroll, "ScrollingDirection", "string", "XY"); Value.Show(Screen, Player); Value.Drain(); int Calls = Value.Backend.Calls().Length;
            Value.Gui.BeginPublication(); Value.Scroll(Scroll, Player, 0.1, 0.2);
            Check(Value.Gui.PendingScrollEffectCount == 1 && Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush) == 0 && Value.Backend.Calls().Length == Calls,
                "provisional effect is bounded but not client-visible");
            Value.Gui.RollbackPublication(); Check(Value.Gui.PendingScrollEffectCount == 0, "provisional rollback publishes no effect");
            Value.Gui.BeginPublication(); Value.Scroll(Scroll, Player, 0.2, 0.3); Value.Set(Scroll, "CanvasSize", "udim2", "0", "0", "0", "800");
            Value.Gui.CommitPublication(); Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Replace, "retained rebuild is emitted before committed effect");
            Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Scroll && Last(Value.Backend).ScrollEffect.Horizontal == 0.2,
                "effect follows the resulting rebuilt Presentation");

            for (int Index = 0; Index < 16; ++Index)
                Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Scroll, Runtime.GuiBackendResultCode.SendFailed, "scroll failure");
            Value.Scroll(Scroll, Player, 0.3, 0.4);
            for (int Index = 0; Index < 16; ++Index) {
                Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
                if (Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Replace)
                    Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
                Check(Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Scroll &&
                    !Last(Value.Backend).Result.Accepted && Value.Gui.PendingScrollEffectCount == 1,
                    "repeated send failures retain one bounded newest effect");
            }
            Check(Value.Gui.FullResyncPresentationCount == 1, "send failure requests authoritative rebuild");
            Value.Scroll(Scroll, Player, 0.8, 0.9); Check(Value.Gui.PendingScrollEffectCount == 1, "newer intent replaces failed older intent");
            Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush); Check(Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Replace, "retry rebuild occurs first");
            Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Scroll && Last(Value.Backend).ScrollEffect.Horizontal == 0.8 &&
                Last(Value.Backend).ScrollEffect.Vertical == 0.9 && Value.Gui.PendingScrollEffectCount == 0, "later success consumes only newest retry");
            int AfterAccepted = Value.Backend.Calls().Length; Value.Set(Scroll, "CanvasSize", "udim2", "0", "0", "0", "900"); Value.Drain();
            Check(Value.Backend.Calls().Length == AfterAccepted + 1 && Last(Value.Backend).Kind == Runtime.GuiBackendOperationKind.Replace,
                "unrelated later rebuild does not replay consumed effect");

            Value.Gui.BeginPublication(); Value.Scroll(Scroll, Player, 0.6, 0.7); Value.Hide(Screen, Player); Value.Show(Screen, Player); Value.Gui.CommitPublication();
            int RebindStart = Value.Backend.Calls().Length; Value.Drain(); Runtime.InMemoryGuiBackend.Call NewPresentation = null, NewEffect = null;
            Runtime.InMemoryGuiBackend.Call[] RebindCalls = Value.Backend.Calls();
            for (int Index = RebindStart; Index < RebindCalls.Length; ++Index) {
                if (RebindCalls[Index].Kind == Runtime.GuiBackendOperationKind.Replace) NewPresentation = RebindCalls[Index];
                if (RebindCalls[Index].Kind == Runtime.GuiBackendOperationKind.Scroll) { NewEffect = RebindCalls[Index]; break; }
            }
            Check(NewPresentation != null && NewEffect != null, "replacement Presentation publishes before staged effect");
            Check(NewEffect.Target.ClientRootId == NewPresentation.Target.ClientRootId,
                "provisional effect binds to resulting Presentation client IDs");

            Value.Gui.BeginPublication(); Value.Scroll(Scroll, Player, 0.4, 0.5);
            Runtime.PlayerView View = Value.Views[Player.UserId]; Value.Players.Disconnect(Player.UserId, View.Identity); Value.Gui.Disconnect(Player);
            Value.Gui.CommitPublication(); Check(Value.Gui.PendingScrollEffectCount == 0, "Player vanished before commit discards effect without rolling back publication");
        }
    }

    private static void RunLifecycleStressAndFairness()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.AddPlayer(), B = Value.AddPlayer(); ulong Screen = Value.Create("ScreenGui");
            ulong Scroll = Value.Create("ScrollingFrame", Screen); Value.Show(Screen, A); Value.Show(Screen, B); Value.Drain();
            for (int Index = 0; Index < 1000; ++Index) Value.Scroll(Scroll, A, (Index % 10) / 10.0, ((Index + 1) % 10) / 10.0);
            Check(Value.Gui.PendingScrollEffectCount == 1 && Value.World.ScrollDiagnostics.Coalesced >= 999, "1,000 same-target effects remain one latest value");
            for (int Index = 0; Index < 1000; ++Index) Value.Scroll(Scroll, (Index & 1) == 0 ? A : B, 0.5, 0.5);
            Check(Value.Gui.PendingScrollEffectCount == 2, "1,000 alternating two-Player effects remain two isolated values");
            ulong Clone = UInt64.Parse(Value.Gui.Mutate(new[] {"clone", Scroll.ToString()}, Value.Token)[0]);
            Value.Gui.Mutate(new[] {"destroy", Clone.ToString()}, Value.Token); Check(Value.Gui.PendingScrollEffectCount == 2, "Clone copies no effect");
            Value.Hide(Screen, A); Check(Value.Gui.PendingScrollEffectCount == 1, "Hide discards only its Presentation effect");
            Value.Gui.Mutate(new[] {"destroy", Scroll.ToString()}, Value.Token); Check(Value.Gui.PendingScrollEffectCount == 0, "ScrollingFrame Destroy discards pending effects");
        }

        var Views = new Dictionary<string, Runtime.PlayerView>();
        var Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView View; return Views.TryGetValue(Id, out View) ? View : null; });
        Runtime.PlayerLifetime P1 = Connect(Players, Views, "76561190005900001"), P2 = Connect(Players, Views, "76561190005900002");
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Players, new Registrar(), new Runtime.GuiConfig().Validate(), Backend);
        var Root = new Runtime.FacadeSession(World, 700, 701, 256); var Addon = new Runtime.FacadeSession(World, 700, 702, 256);
        World.Commit(Root); World.CommitAddon(null, Addon);
        ulong RootScreen = Create(Root.Gui, "ScreenGui"), RootScroll = Create(Root.Gui, "ScrollingFrame", RootScreen);
        ulong AddonScreen = Create(Addon.Gui, "ScreenGui"), AddonScroll = Create(Addon.Gui, "ScrollingFrame", AddonScreen);
        Show(Root.Gui, RootScreen, P1); Show(Addon.Gui, AddonScreen, P2); Drain(World);
        Scroll(Root.Gui, RootScroll, P1, 0.2, 0.2); Scroll(Addon.Gui, AddonScroll, P2, 0.8, 0.8);
        Backend.FailNext(Runtime.GuiBackendOperationKind.Scroll, Runtime.GuiBackendResultCode.SendFailed, "fairness retry");
        World.FlushGui(Stopwatch.StartNew(), 100); World.FlushGui(Stopwatch.StartNew(), 100);
        bool AddonProgress = false; foreach (Runtime.InMemoryGuiBackend.Call Call in Backend.Calls())
            if (Call.Kind == Runtime.GuiBackendOperationKind.Scroll && Call.Target.ExactPlayerConnectionToken == P2.Token && Call.Result.Accepted) AddonProgress = true;
        Check(AddonProgress, "one failing Presentation does not starve another domain");
        World.Retire(Addon); World.Retire(Root);
        Check(World.Gui.PendingScrollEffectCount == 0 && World.Gui.LiveRegistryCount == 0, "replacement/provider teardown returns effect resources to baseline");
    }

    private static void RunBounds()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.AddPlayer(); ulong Screen = Value.Create("ScreenGui"); var Scrolls = new List<ulong>();
            for (int Index = 0; Index < 17; ++Index) Scrolls.Add(Value.Create("ScrollingFrame", Screen)); Value.Show(Screen, Player);
            for (int Index = 0; Index < 16; ++Index) Value.Scroll(Scrolls[Index], Player, 0, 0);
            Check(Value.Gui.PendingScrollEffectCount == 16, "Presentation bound admits exactly 16 effects");
            Reject(() => Value.Scroll(Scrolls[16], Player, 0, 0), "Presentation one-over-limit rejects atomically");
        }

        var DomainViews = new Dictionary<string, Runtime.PlayerView>();
        var DomainPlayers = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView View; return DomainViews.TryGetValue(Id, out View) ? View : null; });
        var DomainWorld = new Runtime.GuiRetainedWorld(new Runtime.GuiConfig().Validate(), DomainPlayers, new Runtime.InMemoryGuiBackend());
        using (var Domain = new Runtime.GuiRetainedRegistry(DomainWorld, 780, 781)) {
            var DomainPlayerValues = new List<Runtime.PlayerLifetime>();
            for (int Index = 0; Index < 33; ++Index)
                DomainPlayerValues.Add(Connect(DomainPlayers, DomainViews, (76561190005800000L + Index).ToString()));
            ulong DomainScreen = Create(Domain, "ScreenGui"); var DomainScrolls = new List<ulong>();
            for (int Index = 0; Index < 16; ++Index) DomainScrolls.Add(Create(Domain, "ScrollingFrame", DomainScreen));
            for (int PlayerIndex = 0; PlayerIndex < 32; ++PlayerIndex) {
                Show(Domain, DomainScreen, DomainPlayerValues[PlayerIndex]);
                foreach (ulong ScrollFrame in DomainScrolls) Scroll(Domain, ScrollFrame, DomainPlayerValues[PlayerIndex], 0.5, 0.5);
            }
            Show(Domain, DomainScreen, DomainPlayerValues[32]);
            Check(Domain.PendingScrollEffectCount == 512, "domain bound admits exactly 512 effects");
            Reject(() => Scroll(Domain, DomainScrolls[0], DomainPlayerValues[32], 0, 0), "domain one-over-limit rejects atomically");
        }
        Check(DomainWorld.PendingScrollEffectCount == 0 && DomainWorld.LiveRegistryCount == 0,
            "domain-bound teardown returns effect resources to baseline");

        var Views = new Dictionary<string, Runtime.PlayerView>();
        var Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView View; return Views.TryGetValue(Id, out View) ? View : null; });
        var Limits = new Runtime.GuiConfig().Validate(); var World = new Runtime.GuiRetainedWorld(Limits, Players, new Runtime.InMemoryGuiBackend());
        var Registries = new List<Runtime.GuiRetainedRegistry>(); var PlayerValues = new List<Runtime.PlayerLifetime>();
        for (int Index = 0; Index < 33; ++Index) PlayerValues.Add(Connect(Players, Views, (76561190006000000L + Index).ToString()));
        for (int Domain = 0; Domain < 8; ++Domain) {
            var Gui = new Runtime.GuiRetainedRegistry(World, 800, checked((ulong)(801 + Domain))); Registries.Add(Gui);
            ulong Screen = Create(Gui, "ScreenGui"); var Scrolls = new List<ulong>();
            for (int Index = 0; Index < 16; ++Index) Scrolls.Add(Create(Gui, "ScrollingFrame", Screen));
            for (int PlayerIndex = 0; PlayerIndex < 32; ++PlayerIndex) {
                Show(Gui, Screen, PlayerValues[PlayerIndex]);
                foreach (ulong ScrollFrame in Scrolls) Scroll(Gui, ScrollFrame, PlayerValues[PlayerIndex], 0.5, 0.5);
            }
            Check(Gui.PendingScrollEffectCount == 512, "domain " + Domain + " admits exactly 512 effects");
        }
        Check(World.PendingScrollEffectCount == 4096, "global bound admits exactly 4096 effects");
        var Extra = new Runtime.GuiRetainedRegistry(World, 800, 900); Registries.Add(Extra);
        ulong ExtraScreen = Create(Extra, "ScreenGui"), ExtraScroll = Create(Extra, "ScrollingFrame", ExtraScreen); Show(Extra, ExtraScreen, PlayerValues[32]);
        Reject(() => Scroll(Extra, ExtraScroll, PlayerValues[32], 0, 0), "global one-over-limit rejects atomically");
        Check(World.PendingScrollEffectCount == 4096 && World.ScrollDiagnostics.BoundRejected >= 1, "rejection creates no partial effect state");
        foreach (Runtime.GuiRetainedRegistry Gui in Registries) Gui.Dispose();
        Check(World.PendingScrollEffectCount == 0 && World.LiveRegistryCount == 0 && World.LiveObjects == 0, "saturation teardown returns global resources to baseline");
    }

    private static Runtime.PlayerLifetime Connect(Runtime.PlayerDirectory Players, IDictionary<string, Runtime.PlayerView> Views, string Id)
    {
        var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id, Name = "Scroll", Connected = true,
            Send = Value => { }, Permission = Value => true}; Views.Add(Id, View); return Players.Connect(View);
    }
    private static ulong Create(Runtime.GuiRetainedRegistry Gui, string ClassName, ulong Parent = 0)
    { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, () => "1")[0]); }
    private static void Show(Runtime.GuiRetainedRegistry Gui, ulong Screen, Runtime.PlayerLifetime Player)
    { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, () => "1"); }
    private static void Scroll(Runtime.GuiRetainedRegistry Gui, ulong ScrollFrame, Runtime.PlayerLifetime Player, double X, double Y)
    { Gui.Mutate(new[] {"scroll", ScrollFrame.ToString(), Player.Token, Player.UserId, X.ToString("R", System.Globalization.CultureInfo.InvariantCulture), Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, () => "1"); }
    private static void Drain(Runtime.FacadeWorld World)
    { int Guard = 10000; while (World.HasWork && Guard-- > 0) World.FlushGui(Stopwatch.StartNew(), 100); Check(!World.HasWork, "world GUI flush converged"); }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var Views = new Dictionary<string, Runtime.PlayerView>();
        var Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView View; return Views.TryGetValue(Id, out View) ? View : null; });
        Runtime.PlayerLifetime Player = Connect(Players, Views, "76561190007000001"); var Backend = new Runtime.InMemoryGuiBackend();
        var World = new Runtime.FacadeWorld(Players, new Registrar(), new Runtime.GuiConfig().Validate(), Backend);
        string Source = "local P=game:GetService('Players'):GetPlayers()[1]; local G=game:GetService('Gui'); local S=G:Create('ScreenGui'); " +
            "local F=S:Create('ScrollingFrame'); S:Show(P); assert(not pcall(function() F:ScrollTo(P, {}) end)); " +
            "assert(not pcall(function() F:ScrollTo(P, Vector2.new(-1, 0)) end)); assert(not pcall(function() G:Create('Frame'):ScrollTo(P, Vector2.new(0, 0)) end)); " +
            "F.ScrollingDirection='X'; assert(not pcall(function() F:ScrollToTop(P) end)); F.ScrollingDirection='XY'; " +
            "F:ScrollTo(P, Vector2.new(0, 0)); F:ScrollToBottom(P); F:ScrollTo(P, Vector2.new(0.75, 0.25))";
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Snapshot, World)) {
            Runtime.ExecutionResult Initial = Host.Reload();
            Check(Initial.Status == Runtime.RuntimeStatus.OK, "native public ScrollTo surface: " + Initial.Status + " " + Initial.Error);
            Drain(World); Runtime.InMemoryGuiBackend.Call Call = Last(Backend);
            Check(Call.Kind == Runtime.GuiBackendOperationKind.Scroll && Call.Target.ExactPlayerConnectionToken == Player.Token &&
                Call.ScrollEffect.Horizontal == 0.75 && Call.ScrollEffect.Vertical == 0.25, "native provisional latest-wins effect publishes after initial rebuild");
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native replacement rebuilds scroll-capable source"); Drain(World);
            Check(Host.Execute("gui3d.timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT,
                "fatal VM recovery preserves scroll-effect bootstrap contract"); Drain(World);
        }
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.PendingScrollEffectCount == 0, "native teardown leaves no pending effect resource");
        Console.WriteLine("[CarbonLuau:GuiFoundation3DNative] PASS public methods, validation, provisional publication, replacement, recovery and teardown");
    }
}
