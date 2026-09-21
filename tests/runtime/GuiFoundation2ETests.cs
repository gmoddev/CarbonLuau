using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation2ETests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class RichTree
    {
        internal ulong Screen, Scroll, Layout, Padding, Image, Button, First, Second;
    }

    private sealed class Fixture
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.FacadeWorld World;
        private int NextPlayer;
        private ulong NextRegistration;

        internal Fixture(Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.FacadeWorld(Players, new Registrar(), Limits, Backend);
        }

        internal Runtime.PlayerLifetime Add()
        {
            string Id = (76561190001200000L + ++NextPlayer).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Rich" + NextPlayer, Connected = true, Send = Value => { }, Permission = Value => true};
            Views[Id] = View; return Players.Connect(View);
        }

        internal Runtime.PlayerLifetime Reconnect(Runtime.PlayerLifetime Previous)
        {
            Runtime.PlayerView Old = Views[Previous.UserId]; Players.Disconnect(Previous.UserId, Old.Identity); Views.Remove(Previous.UserId);
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Previous.UserId,
                Name = "Reconnected", Connected = true, Send = Value => { }, Permission = Value => true};
            Views[Previous.UserId] = View; return Players.Connect(View);
        }

        internal string Registration() { return (++NextRegistration).ToString(); }

        internal void Drain()
        {
            int Guard = 65536;
            while (World.HasWork && Guard-- > 0) World.FlushGui(Stopwatch.StartNew(), 100);
            Check(!World.HasWork, "GUI world drain converged");
        }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 2E: " + Message); }

    private static void Reject(Action Action, string Message)
    { bool Rejected = false; try { Action(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }

    private static ulong Create(Fixture Value, Runtime.FacadeSession Session, string ClassName, ulong Parent = 0)
    { return UInt64.Parse(Session.Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Value.Registration)[0]); }

    private static void Set(Fixture Value, Runtime.FacadeSession Session, ulong ObjectId, string Property, params string[] PropertyValue)
    {
        var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(PropertyValue);
        Session.Gui.Mutate(Fields.ToArray(), Value.Registration);
    }

    private static string[] Get(Runtime.FacadeSession Session, ulong ObjectId, string Property)
    { return Session.Gui.Query(new[] {"get", ObjectId.ToString(), Property}); }

    private static void Show(Fixture Value, Runtime.FacadeSession Session, ulong Screen, Runtime.PlayerLifetime Player)
    { Session.Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Value.Registration); }

    private static void Hide(Fixture Value, Runtime.FacadeSession Session, ulong Screen, Runtime.PlayerLifetime Player)
    { Session.Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, Value.Registration); }

    private static void Destroy(Fixture Value, Runtime.FacadeSession Session, ulong ObjectId)
    { Session.Gui.Mutate(new[] {"destroy", ObjectId.ToString()}, Value.Registration); }

    private static ulong Clone(Fixture Value, Runtime.FacadeSession Session, ulong ObjectId)
    { return UInt64.Parse(Session.Gui.Mutate(new[] {"clone", ObjectId.ToString()}, Value.Registration)[0]); }

    private static string Connect(Fixture Value, Runtime.FacadeSession Session, ulong Button)
    { return Session.Gui.Mutate(new[] {"connect", Button.ToString()}, Value.Registration)[0]; }

    private static RichTree Build(Fixture Value, Runtime.FacadeSession Session, Runtime.PlayerLifetime Player = null)
    {
        var Result = new RichTree();
        Result.Screen = Create(Value, Session, "ScreenGui");
        Result.Scroll = Create(Value, Session, "ScrollingFrame", Result.Screen);
        Set(Value, Session, Result.Scroll, "Size", "udim2", "0", "480", "0", "320");
        Set(Value, Session, Result.Scroll, "CanvasSize", "udim2", "1", "0", "0", "1200");
        Result.First = Create(Value, Session, "Frame", Result.Scroll);
        Result.Second = Create(Value, Session, "Frame", Result.Scroll);
        Set(Value, Session, Result.First, "Size", "udim2", "1", "-16", "0", "48");
        Set(Value, Session, Result.Second, "Size", "udim2", "1", "-16", "0", "48");
        Result.Image = Create(Value, Session, "ImageLabel", Result.First);
        Set(Value, Session, Result.Image, "Image", "imagesource", "Png", "42");
        Result.Button = Create(Value, Session, "ImageButton", Result.Second);
        Set(Value, Session, Result.Button, "Image", "imagesource", "Sprite", "assets/icons/accept.png");
        Connect(Value, Session, Result.Button);
        Result.Padding = Create(Value, Session, "UIPadding", Result.Scroll);
        Set(Value, Session, Result.Padding, "PaddingLeft", "udim", "0", "8");
        Result.Layout = Create(Value, Session, "UIListLayout", Result.Scroll);
        Set(Value, Session, Result.Layout, "Padding", "udim", "0", "4");
        if (Player != null) Show(Value, Session, Result.Screen, Player);
        return Result;
    }

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
                Runtime.GuiRenderValue Command = Property(Element, Runtime.GuiRenderPropertyId.ActionCommand);
                if (Command != null) return Command.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
            }
        }
        return null;
    }

    private static Runtime.GuiRenderPlan LatestPlan(Runtime.InMemoryGuiBackend Backend, string PlayerToken)
    {
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        for (int Index = Calls.Length - 1; Index >= 0; --Index)
            if (Calls[Index].Result.Accepted && Calls[Index].Plan != null && Calls[Index].Target.ExactPlayerConnectionToken == PlayerToken)
                return Calls[Index].Plan;
        return null;
    }

    private static Runtime.GuiRenderElement Element(Runtime.GuiRenderElement[] Elements, ulong ObjectId, string Marker = "o")
    {
        string Suffix = Marker + ObjectId.ToString("x");
        foreach (Runtime.GuiRenderElement Element in Elements)
            if (Element.ClientId.EndsWith(Suffix, StringComparison.Ordinal)) return Element;
        throw new Exception("GUI Foundation 2E: rendered object missing " + ObjectId);
    }

    internal static void RunModel()
    {
        RunSharedViewAndOwnership();
        RunReplacementAndPublication();
        RunFailureInteractionAndLifecycle();
        RunStressAndDiagnostics();
        Console.WriteLine("[CarbonLuau:GuiFoundation2EModel] PASS shared-view, ownership, replacement, recovery, lifecycle and rich-control stress");
    }

    private static void RunSharedViewAndOwnership()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime A = Value.Add(), B = Value.Add();
        var Owner = new Runtime.FacadeSession(Value.World, 100, 101, 256); Value.World.Commit(Owner);
        RichTree Tree = Build(Value, Owner); Show(Value, Owner, Tree.Screen, A); Show(Value, Owner, Tree.Screen, B); Value.Drain();
        Runtime.GuiRenderPlan APlan = LatestPlan(Value.Backend, A.Token), BPlan = LatestPlan(Value.Backend, B.Token);
        string AToken = LatestToken(Value.Backend, A.Token), BToken = LatestToken(Value.Backend, B.Token);
        Check(APlan != null && BPlan != null && APlan.ProjectedElementCount == BPlan.ProjectedElementCount &&
            AToken != null && BToken != null && AToken != BToken, "one retained rich tree produces equivalent independent Presentations and action authority");
        Check(Property(Element(APlan.Elements, Tree.Scroll), Runtime.GuiRenderPropertyId.ScrollVertical).Boolean &&
            Property(Element(BPlan.Elements, Tree.Image, "i"), Runtime.GuiRenderPropertyId.ImageSource).ImageSource.Describe() == "imagesource:Png:42",
            "layout, image and scrolling retained state is shared across Presentations");

        Set(Value, Owner, Tree.First, "LayoutOrder", "integer", "9");
        Set(Value, Owner, Tree.Padding, "PaddingLeft", "udim", "0", "16");
        Set(Value, Owner, Tree.Image, "ImageColor3", "color3", "0.25", "0.5", "0.75");
        Set(Value, Owner, Tree.Button, "Image", "imagesource", "Png", "99");
        Set(Value, Owner, Tree.Scroll, "CanvasSize", "udim2", "1", "0", "0", "1600");
        Set(Value, Owner, Tree.Scroll, "ScrollingDirection", "string", "XY"); Value.Drain();
        string A2Token = LatestToken(Value.Backend, A.Token), B2Token = LatestToken(Value.Backend, B.Token);
        Check(A2Token != AToken && B2Token != BToken && A2Token != B2Token &&
            !Value.World.AdmitGuiAction(A, AToken) && !Value.World.AdmitGuiAction(B, BToken),
            "shared structural state rotates each Presentation authority without retargeting old tokens");
        Check(String.Join("|", Get(Owner, Tree.Scroll, "CanvasSize")) == "udim2|1|0|0|1600" &&
            Get(Owner, Tree.First, "LayoutOrder")[1] == "9" && Get(Owner, Tree.Padding, "PaddingLeft")[2] == "16",
            "layout and scrolling mutations remain one canonical retained value");
        string Json = Runtime.RustCuiBackend.Serialize(LatestPlan(Value.Backend, A.Token).Elements, false, true);
        Check(!Json.Contains("NormalizedPosition") && !Json.Contains("CanvasPosition"),
            "client-local scroll offset is neither retained nor projected back to CarbonLuau");

        var Consumer = new Runtime.FacadeSession(Value.World, 100, 102, 256); Value.World.CommitAddon(null, Consumer);
        int OwnerObjects = Owner.Gui.LiveObjectCount, OwnerConnections = Owner.Gui.ConnectionCount;
        Value.World.Retire(Consumer);
        Check(Owner.Gui.LiveObjectCount == OwnerObjects && Owner.Gui.ConnectionCount == OwnerConnections &&
            Value.World.AdmitGuiAction(A, A2Token), "consumer retirement does not destroy owner-domain rich GUI resources");
        Value.World.Retire(Owner);
        Reject(() => Get(Owner, Tree.Scroll, "CanvasSize"), "owner retirement stales escaped scrolling references");
        Check(!Value.World.AdmitGuiAction(A, A2Token) && Value.World.Gui.LiveObjects == 0 && Value.World.Gui.LivePresentations == 0,
            "owner retirement invalidates all rich controls, Presentations and action authority");
    }

    private static void RunReplacementAndPublication()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime Player = Value.Add();
        var A1 = new Runtime.FacadeSession(Value.World, 200, 201, 256); Value.World.Commit(A1);
        RichTree Old = Build(Value, A1, Player); Value.Drain(); string OldToken = LatestToken(Value.Backend, Player.Token); int Calls = Value.Backend.Calls().Length;

        var Failed = new Runtime.FacadeSession(Value.World, 201, 202, 256); RichTree FailedTree = Build(Value, Failed, Player);
        Value.World.FlushGui(Stopwatch.StartNew(), 100);
        Check(Value.Backend.Calls().Length == Calls && Value.World.AdmitGuiAction(Player, OldToken),
            "failed root candidate remains provisional while A1 rich GUI stays authoritative");
        Value.World.Retire(Failed);
        Check(Failed.Gui.LiveObjectCount == 0 && Failed.Gui.PresentationCount == 0,
            "failed root candidate retires all provisional Foundation 2 resources");

        var A2 = new Runtime.FacadeSession(Value.World, 202, 203, 256); RichTree Fresh = Build(Value, A2, Player);
        Value.World.Commit(A2); Value.World.Retire(A1); Value.Drain(); string FreshToken = LatestToken(Value.Backend, Player.Token);
        Check(!Value.World.AdmitGuiAction(Player, OldToken) && FreshToken != null && FreshToken != OldToken &&
            A1.Gui.LiveObjectCount == 0 && A2.Gui.LiveObjectCount == 8,
            "successful root replacement retires A1 and publishes fresh rich GUI identities");

        var Addon1 = new Runtime.FacadeSession(Value.World, 202, 301, 256); RichTree AddonOld = Build(Value, Addon1, Player);
        Value.World.CommitAddon(null, Addon1); Value.Drain(); string AddonOldToken = LatestToken(Value.Backend, Player.Token);
        var AddonFailed = new Runtime.FacadeSession(Value.World, 202, 302, 256); Build(Value, AddonFailed, Player); Value.World.Retire(AddonFailed);
        Check(Value.World.AdmitGuiAction(Player, AddonOldToken), "failed addon candidate leaves A1 action authority intact");
        var Addon2 = new Runtime.FacadeSession(Value.World, 202, 303, 256); Build(Value, Addon2, Player);
        Value.World.CommitAddon(Addon1, Addon2); Value.Drain(); string AddonNewToken = LatestToken(Value.Backend, Player.Token);
        Check(!Value.World.AdmitGuiAction(Player, AddonOldToken) && AddonNewToken != null && AddonNewToken != AddonOldToken &&
            Addon1.Gui.LiveObjectCount == 0, "addon replacement never retargets old ImageButton authority");

        int Connections = A2.Gui.ConnectionCount;
        A2.Gui.BeginPublication();
        Set(Value, A2, Fresh.Layout, "Padding", "udim", "0", "25");
        Set(Value, A2, Fresh.Padding, "PaddingTop", "udim", "0", "11");
        Set(Value, A2, Fresh.Button, "Image", "imagesource", "Png", "777");
        Set(Value, A2, Fresh.Scroll, "ScrollingEnabled", "boolean", "0"); Connect(Value, A2, Fresh.Button);
        Check(Get(A2, Fresh.Layout, "Padding")[2] == "25", "provisional rich mutation provides read-your-writes");
        A2.Gui.RollbackPublication();
        Check(Get(A2, Fresh.Layout, "Padding")[2] == "4" && Get(A2, Fresh.Padding, "PaddingTop")[2] == "0" &&
            Get(A2, Fresh.Scroll, "ScrollingEnabled")[1] == "1" && A2.Gui.ConnectionCount == Connections && !A2.Gui.HasWork,
            "failed provisional rich mutation leaks no retained, dirty or connection state");

        A2.Gui.BeginPublication();
        Set(Value, A2, Fresh.Layout, "Padding", "udim", "0", "30");
        Set(Value, A2, Fresh.Image, "Image", "imagesource", "SteamAvatar", Player.UserId);
        Set(Value, A2, Fresh.Scroll, "ScrollingDirection", "string", "X");
        A2.Gui.CommitPublication(); Value.Drain();
        Check(Get(A2, Fresh.Layout, "Padding")[2] == "30" && Get(A2, Fresh.Scroll, "ScrollingDirection")[1] == "X",
            "successful provisional rich mutation commits atomically and synchronizes newest state");

        Value.World.Retire(Addon2); Value.World.Retire(A2);
        Check(Value.World.Gui.LiveRegistryCount == 0 && Value.World.Gui.LiveObjects == 0 && Value.World.Gui.LivePresentations == 0 &&
            Value.World.Gui.LiveActionCount == 0 && Value.World.Gui.DomainActionOwnerCount == 0,
            "replacement and publication closure returns Foundation 2 resources to baseline");
    }

    private static void RunFailureInteractionAndLifecycle()
    {
        var Value = new Fixture(new Runtime.GuiConfig {MaxTrackedDirtyObjectsPerDomain = 3});
        Runtime.PlayerLifetime A = Value.Add(), B = Value.Add();
        var Session = new Runtime.FacadeSession(Value.World, 400, 401, 256); Value.World.Commit(Session);
        RichTree Tree = Build(Value, Session); Show(Value, Session, Tree.Screen, A); Show(Value, Session, Tree.Screen, B); Value.Drain();
        string AToken = LatestToken(Value.Backend, A.Token);
        Check(!Value.World.AdmitGuiAction(B, AToken), "ImageButton rejects cross-Player authority");
        Check(!Value.World.AdmitGuiAction(A, new string('0', 32)), "ImageButton rejects forged authority");
        Runtime.GuiActionAdmission Admission;
        Check(Value.World.Gui.TryAdmit(A, AToken, out Admission) && Admission.Registrations.Length == 1,
            "valid ImageButton action resolves one bounded callback without synchronous Luau entry");
        Destroy(Value, Session, Tree.Button);
        Check(!Value.World.AdmitGuiAction(A, AToken), "Destroy immediately invalidates ImageButton authority");

        ulong ReplacementButton = Create(Value, Session, "ImageButton", Tree.Second); Connect(Value, Session, ReplacementButton);
        Set(Value, Session, ReplacementButton, "Image", "imagesource", "Png", "55"); Value.Drain();
        string BeforeFailure = LatestToken(Value.Backend, A.Token);
        Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "rich replacement failure");
        Set(Value, Session, ReplacementButton, "Image", "imagesource", "Png", "56");
        Session.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
        Check(Session.Gui.FullResyncPresentationCount == 2 && !Value.World.AdmitGuiAction(A, BeforeFailure),
            "image source backend failure marks both Presentations uncertain and invalidates old authority");
        Set(Value, Session, Tree.Layout, "Padding", "udim", "0", "7");
        Set(Value, Session, Tree.Padding, "PaddingTop", "udim", "0", "9");
        Set(Value, Session, Tree.First, "LayoutOrder", "integer", "5");
        Set(Value, Session, Tree.Scroll, "CanvasSize", "udim2", "1", "0", "0", "1900");
        Set(Value, Session, Tree.Scroll, "ScrollingEnabled", "boolean", "0"); Value.Drain();
        Runtime.GuiRenderPlan Final = LatestPlan(Value.Backend, A.Token);
        Check(Final != null && Property(Element(Final.Elements, Tree.Scroll), Runtime.GuiRenderPropertyId.ScrollEnabled).Boolean == false &&
            Get(Session, Tree.Layout, "Padding")[2] == "7" && Session.Gui.FullResyncPresentationCount == 0,
            "layout overflow and image/scroll failure converge through one newest-state reconciliation per Presentation");

        ulong CloneId = Clone(Value, Session, Tree.Scroll); string[] CloneChildren = Session.Gui.Query(new[] {"children", CloneId.ToString()});
        Check(CloneChildren.Length == 8 && Get(Session, CloneId, "ScrollingEnabled")[1] == "0",
            "Clone copies scrolling, layout, padding and image state without Presentation state");
        Destroy(Value, Session, CloneId); Reject(() => Get(Session, CloneId, "CanvasSize"), "destroyed rich clone is stale");

        ulong Parent = Create(Value, Session, "Frame", Tree.Screen), Other = Create(Value, Session, "Frame", Tree.Screen);
        ulong Layout = Create(Value, Session, "UIListLayout", Parent), Padding = Create(Value, Session, "UIPadding", Parent);
        ulong ExistingLayout = Create(Value, Session, "UIListLayout", Other);
        Reject(() => Set(Value, Session, Layout, "Parent", "object", Other.ToString()), "helper reparent preserves duplicate cardinality invariant");
        Destroy(Value, Session, ExistingLayout); Set(Value, Session, Layout, "Parent", "object", Other.ToString());
        Destroy(Value, Session, Layout); Destroy(Value, Session, Padding);
        Check(Get(Session, Tree.First, "Position")[0] == "udim2", "helper destruction leaves authored Position retained and authoritative");

        Hide(Value, Session, Tree.Screen, A); Value.Drain(); Show(Value, Session, Tree.Screen, A); Value.Drain();
        string Reshown = LatestToken(Value.Backend, A.Token); Runtime.PlayerLifetime Reconnected = Value.Reconnect(A); Value.World.DisconnectGui(A);
        Check(!Value.World.AdmitGuiAction(Reconnected, Reshown), "Hide/Show and reconnect do not preserve old Presentation authority or scroll state");
        Value.World.Retire(Session);
        Check(Session.PendingCount == 0 && Value.World.Gui.LiveObjects == 0 && Value.World.Gui.LivePresentations == 0 && Value.World.Gui.LiveActionCount == 0,
            "retirement clears queued callbacks, dirty state, private scrolling projection and rich resources");
    }

    private static void RunStressAndDiagnostics()
    {
        var Value = new Fixture(); Runtime.PlayerLifetime Player = Value.Add(); Runtime.FacadeSession Root = null;
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            var Next = new Runtime.FacadeSession(Value.World, 5000 + Cycle, 6000 + Cycle, 256); Build(Value, Next, Player);
            Value.World.Commit(Next); if (Root != null) Value.World.Retire(Root); Root = Next; Value.Drain();
        }
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            var Failed = new Runtime.FacadeSession(Value.World, 7000 + Cycle, 8000 + Cycle, 256); Build(Value, Failed, Player); Value.World.Retire(Failed);
        }
        Runtime.FacadeSession Addon = null;
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            var Next = new Runtime.FacadeSession(Value.World, 9000, 10000 + Cycle, 256); Build(Value, Next, Player);
            Value.World.CommitAddon(Addon, Next); Addon = Next; Value.Drain();
        }

        RichTree Stress = Build(Value, Root);
        for (int Cycle = 0; Cycle < 1000; ++Cycle) { ulong Copy = Clone(Value, Root, Stress.Scroll); Destroy(Value, Root, Copy); }
        for (int Cycle = 0; Cycle < 1000; ++Cycle) { Show(Value, Root, Stress.Screen, Player); Hide(Value, Root, Stress.Screen, Player); }
        for (int Cycle = 0; Cycle < 1000; ++Cycle) {
            Runtime.PlayerLifetime Previous = Player; Player = Value.Reconnect(Previous); Value.World.DisconnectGui(Previous);
        }
        Show(Value, Root, Stress.Screen, Player); Value.Drain();
        string PreviousToken = LatestToken(Value.Backend, Player.Token);
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            Set(Value, Root, Stress.Button, "Image", "imagesource", "Png", (1000 + Cycle).ToString()); Value.Drain();
            string NextToken = LatestToken(Value.Backend, Player.Token);
            Check(NextToken != null && NextToken != PreviousToken && !Value.World.AdmitGuiAction(Player, PreviousToken),
                "repeated ImageButton full rebuild rotates stale authority at cycle " + Cycle);
            PreviousToken = NextToken;
        }
        for (int Cycle = 0; Cycle < 100; ++Cycle) {
            Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "stress full resync");
            Set(Value, Root, Stress.Scroll, "CanvasSize", "udim2", "1", "0", "0", (2000 + Cycle).ToString());
            Value.World.FlushGui(Stopwatch.StartNew(), 100); Value.Drain();
        }
        Check(Root.Gui.LiveObjectCount == 16 && Root.Gui.PresentationCount == 1 && Root.Gui.ConnectionCount == 2 &&
            Root.Gui.FullResyncPresentationCount == 0, "1,000 clone/destroy, show/hide and reconnect cycles retain one bounded newest rich state");
        string Status = Value.World.GuiStatus;
        Check(Status.Contains("GUI live registries/objects/screens/presentations/connections/actions") &&
            Status.Contains("dirty/full-resync/blocked/pending-destroy") && Status.Contains("resource-limit rejections") &&
            Status.IndexOf(Runtime.GuiRetainedWorld.ActionCommand, StringComparison.Ordinal) < 0,
            "bounded aggregate diagnostics cover rich resources and failures without exposing action tokens");

        Value.World.Retire(Addon); Value.World.Retire(Root);
        Check(Value.World.Gui.LiveRegistryCount == 0 && Value.World.Gui.LiveObjects == 0 && Value.World.Gui.LivePresentations == 0 &&
            Value.World.Gui.LiveActionCount == 0 && Value.World.Gui.DomainActionOwnerCount == 0 && Value.World.Gui.PlayerActionRateCount == 0,
            "rich replacement, failure and lifecycle stress returns every live resource counter to baseline");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        RunRootRecoveryNative(Native);
        RunAddonProviderNative(Native);
        Console.WriteLine("[CarbonLuau:GuiFoundation2ENative] PASS rich root/addon replacement, fatal recovery, provider lifecycle and teardown");
    }

    private static void RunRootRecoveryNative(Runtime.NativeRuntime Native)
    {
        const string UserId = "76561190001299999";
        Runtime.PlayerView View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Rich Native", Connected = true, Send = Value => { }, Permission = Value => true};
        var Players = new Runtime.PlayerDirectory(Id => Id == UserId ? View : null); Runtime.PlayerLifetime Player = Players.Connect(View);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Players, new Registrar(), Backend);
        string Source = RichSource("A1");
        Func<Runtime.ScriptSnapshot> Snapshot = () => { var Result = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source}; Result.Modules.Add("state", "return {}"); return Result; };
        string BeforeUnload;
        using (var Host = new Runtime.ScriptHost(Native,
            new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 5, FrameDrainBudgetMilliseconds = 100}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native rich root initializes"); Host.Drain();
            Runtime.FacadeSession A1 = World.Active; string A1Token = LatestToken(Backend, Player.Token); int Calls = Backend.Calls().Length;
            string Failed = RichSource("failed") + "; error('reject rich candidate')";
            Check(Host.Reload(Failed).Status == Runtime.RuntimeStatus.RUNTIME_ERROR && World.Active == A1 &&
                Backend.Calls().Length == Calls && World.AdmitGuiAction(Player, A1Token),
                "failed native rich candidate preserves A1 retained and client authority");

            Source = RichSource("A2");
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && !World.AdmitGuiAction(Player, A1Token),
                "healthy native rich replacement invalidates A1 authority"); Host.Drain();
            string Action = LatestToken(Backend, Player.Token);
            Check(World.AdmitGuiAction(Player, Action), "native ImageButton action admitted before callback mutation");
            string Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "rich-click\n" && Backend.Calls()[Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Replace,
                "ImageButton callback mutates layout, image and scrolling state through later synchronization");

            Action = LatestToken(Backend, Player.Token); Check(World.AdmitGuiAction(Player, Action), "action queued before target retirement");
            Check(Host.Execute("gui2e.destroy", "local S=require('state'); S.Button:Destroy()").Status == Runtime.RuntimeStatus.OK,
                "native target destroy succeeds before callback entry");
            Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "" && World.Gui.ActionDiagnostics.PreEntryStale != 0,
                "queued ImageButton callback is suppressed after target retirement");
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "rich source recreates after target retirement"); Host.Drain();

            for (int Cycle = 0; Cycle < 100; ++Cycle) {
                string OldToken = LatestToken(Backend, Player.Token); Runtime.FacadeSession Old = World.Active;
                Check(OldToken != null && World.AdmitGuiAction(Player, OldToken), "rich action admitted before fatal recovery " + Cycle);
                Check(Host.Execute("gui2e.timeout." + Cycle, "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT,
                    "fatal recovery completes for rich GUI cycle " + Cycle);
                Check(Old.Gui.LiveObjectCount == 0 && Old.PendingCount == 0 && !World.AdmitGuiAction(Player, OldToken),
                    "fatal recovery stales rich handles, Presentation and queued authority at cycle " + Cycle);
                Host.Drain(); string NewToken = LatestToken(Backend, Player.Token);
                Check(NewToken != null && NewToken != OldToken && World.Gui.LiveObjects == 11 && World.Gui.LivePresentations == 1 &&
                    World.Gui.LiveActionCount == 1, "fatal recovery reconstructs only fresh rich retained state at cycle " + Cycle);
                if (Cycle != 99) { Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "operator reload rearms rich recovery"); Host.Drain(); }
            }
            BeforeUnload = LatestToken(Backend, Player.Token);
        }
        Check(World.Active == null && World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0 && World.Gui.LivePresentations == 0 &&
            World.Gui.LiveActionCount == 0 && !World.AdmitGuiAction(Player, BeforeUnload),
            "CarbonLuau-style unload clears rich Presentations, dirties, actions and runtime state");

        using (var Reloaded = new Runtime.ScriptHost(Native,
            new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 20, FrameDrainBudgetMilliseconds = 100}, Snapshot, World)) {
            Check(Reloaded.Reload().Status == Runtime.RuntimeStatus.OK, "fresh host reconstructs rich GUI after unload"); Reloaded.Drain();
            string Fresh = LatestToken(Backend, Player.Token);
            Check(Fresh != null && Fresh != BeforeUnload && World.AdmitGuiAction(Player, Fresh) && !World.AdmitGuiAction(Player, BeforeUnload),
                "host reload creates only fresh rich GUI and action identities");
        }
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0 && World.Gui.LivePresentations == 0 &&
            World.Gui.LiveActionCount == 0 && World.Gui.PlayerActionRateCount == 0,
            "native rich recovery and unload/reload return all resources to baseline");
    }

    private static string RichSource(string Label)
    {
        return "local State=require('state'); local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
            "local F=S:Create('ScrollingFrame'); F.CanvasSize=UDim2.fromOffset(480,1200); local Pad=F:Create('UIPadding'); Pad.PaddingLeft=UDim.new(0,8); " +
            "local L=F:Create('UIListLayout'); L.Padding=UDim.new(0,4); local A=F:Create('Frame'); local B=F:Create('Frame'); A.LayoutOrder=1; B.LayoutOrder=2; " +
            "local Clip=A:Create('Frame'); Clip.ClipsDescendants=true; local Grid=Clip:Create('UIGridLayout'); Grid.CellSize=UDim2.fromOffset(32,24); local Label=Clip:Create('TextLabel'); Label.Font=GuiFont.RobotoCondensedBold; " +
            "local I=A:Create('ImageLabel'); I.Image=ImageSource.Png('42'); local Button=B:Create('ImageButton'); Button.Image=ImageSource.Sprite('assets/icons/accept.png'); " +
            "Button.Activated:Connect(function(V) assert(V==P); print('rich-click'); L.Padding=UDim.new(0,9); Grid.CellPadding=UDim2.fromOffset(3,3); Label.Font=GuiFont.DroidSansMono; I.ImageColor3=Color3.fromRGB(1,2,3); F.CanvasSize=UDim2.fromOffset(480,1600) end); " +
            "S.Name='" + Label + "'; S:Show(P); F:ScrollTo(P,Vector2.new(0.25,0.75)); State.Screen=S; State.Scroll=F; State.Layout=L; State.Grid=Grid; State.Clip=Clip; State.Label=Label; State.Image=I; State.Button=Button";
    }

    private static void RunAddonProviderNative(Runtime.NativeRuntime Native)
    {
        const string UserId = "76561190001399999";
        Runtime.PlayerView View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Addon Rich", Connected = true, Send = Value => { }, Permission = Value => true};
        var Players = new Runtime.PlayerDirectory(Id => Id == UserId ? View : null); Runtime.PlayerLifetime Player = Players.Connect(View);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Players, new Registrar(), Backend);
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object Provider = new object(), ConsumerProvider = new object();
        using (var Host = new Runtime.ScriptHost(Native,
            new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 100}, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "provider rich root baseline");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                byte[] Owner = OwnerArchive("1.0.0"); string[] OwnerRegistration = Registry.RegisterArchive(Provider, Owner); Process(Registry); Host.Drain();
                Check(Registry.Status(Provider, OwnerRegistration[1])[2] == "Active", "rich owner addon activates");
                Runtime.FacadeSession OwnerSession = FindCommand(World, "richowner"); ulong Scroll = FindClass(OwnerSession, Runtime.GuiClassId.ScrollingFrame);
                ulong Button = FindClass(OwnerSession, Runtime.GuiClassId.ImageButton); string OwnerToken = LatestToken(Backend, Player.Token);

                byte[] Failed = ConsumerArchive("richfail", true,
                    "local A=require('@richowner'); A.Layout.Padding=UDim.new(0,31); A.Grid.CellPadding=UDim2.fromOffset(31,31); A.Clip.ClipsDescendants=false; A.Label.Font=GuiFont.PermanentMarker; A.Scroll.CanvasSize=UDim2.fromOffset(900,1800); A.Scroll:ScrollTo(A.Player,Vector2.new(0.9,0.9)); A.Button.Image=ImageSource.Png('700'); error('reject rich consumer')");
                string[] FailedRegistration = Registry.RegisterArchive(ConsumerProvider, Failed); Process(Registry);
                Check(Registry.Status(ConsumerProvider, FailedRegistration[1])[2] == "Failed" &&
                    Get(OwnerSession, Scroll, "CanvasSize")[2] == "480" && Get(OwnerSession, Scroll, "CanvasSize")[4] == "1200",
                    "failed foreign rich candidate rolls back retained owner mutation");

                byte[] Required = ConsumerArchive("richrequired", true,
                    "local A=require('@richowner'); A.Layout.Padding=UDim.new(0,17); A.Grid.CellPadding=UDim2.fromOffset(7,7); A.Clip.ClipsDescendants=true; A.Label.Font=GuiFont.DroidSansMono; A.Scroll.ScrollingDirection='XY'; A.Scroll:ScrollTo(A.Player,Vector2.new(0.2,0.8)); A.Button.ImageColor3=Color3.fromRGB(7,8,9); A.Button.Activated:Connect(function() print('consumer-click') end)");
                string[] RequiredRegistration = Registry.RegisterArchive(ConsumerProvider, Required); Process(Registry); Host.Drain();
                string[] RequiredStatus = Registry.Status(ConsumerProvider, RequiredRegistration[1]);
                Check(RequiredStatus[2] == "Active" &&
                    Get(OwnerSession, Scroll, "ScrollingDirection")[1] == "XY",
                    "successful foreign rich candidate commits against live owner: " + String.Join("|", RequiredStatus));

                byte[] Optional = ConsumerArchive("richoptional", false,
                    "local A=require('@richowner'); local Saved=A.Value; game:GetService('Commands'):Register('richoptionalcheck',{},function() assert(Saved==ImageSource.Png('42')); print('image-value-live') end)");
                string[] OptionalRegistration = Registry.RegisterArchive(ConsumerProvider, Optional); Process(Registry);
                string OptionalDomain = Registry.Status(ConsumerProvider, OptionalRegistration[1])[7];
                Check(OptionalDomain.Length != 0, "optional GUI consumer activates against exact owner lifetime");

                string PendingToken = LatestToken(Backend, Player.Token);
                Check(World.AdmitGuiAction(Player, PendingToken), "provider action queued before retirement");
                SetDirect(OwnerSession, Button, "Image", "imagesource", "Png", "701");
                int Retired = Registry.UnloadProvider(Provider);
                Check(Retired == 1 && Registry.Status(ConsumerProvider, RequiredRegistration[1])[2] == "Blocked" &&
                    Registry.Status(ConsumerProvider, OptionalRegistration[1])[2] == "Active" &&
                    Registry.Status(ConsumerProvider, OptionalRegistration[1])[7] == OptionalDomain &&
                    Registry.Status(Provider, OwnerRegistration[1])[0] == "ERROR" && OwnerSession.PendingCount == 0 &&
                    !World.AdmitGuiAction(Player, OwnerToken),
                    "provider unload retires rich owner, blocks required consumer, leaves optional lifetime fixed and rejects stale authority");
                Runtime.FacadeSession OptionalSession = FindCommand(World, "richoptionalcheck");
                Check(OptionalSession.Invoke("richoptionalcheck", Player.UserId, new string[0]), "lifetime-independent ImageSource remains usable after owner retirement");
                string Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
                Check(Logs == "image-value-live\n", "immutable ImageSource survives as ordinary value without reviving owner GUI");

                object ReloadedProvider = new object(); string[] Reloaded = Registry.RegisterArchive(ReloadedProvider, OwnerArchive("2.0.0")); Process(Registry); Process(Registry); Host.Drain();
                Check(Registry.Status(ReloadedProvider, Reloaded[1])[2] == "Active" &&
                    Registry.Status(ConsumerProvider, RequiredRegistration[1])[2] == "Active" &&
                    Registry.Status(ConsumerProvider, OptionalRegistration[1])[7] == OptionalDomain &&
                    Registry.Status(ConsumerProvider, RequiredRegistration[1])[7] != OptionalDomain,
                    "provider reload reconstructs required consumer while optional consumer does not hot-rebind");
                string Fresh = LatestToken(Backend, Player.Token);
                Check(Fresh != null && Fresh != OwnerToken && !World.AdmitGuiAction(Player, OwnerToken),
                    "provider reload creates fresh rich Presentation and action identity only");
            }
            Check(Host.DomainCount == 1 && World.Gui.LiveRegistryCount == 1,
                "provider registry teardown returns all addon rich resources to root baseline");
        }
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0 && World.Gui.LivePresentations == 0 &&
            World.Gui.LiveActionCount == 0, "CarbonLuau host teardown clears provider rich GUI state");
    }

    private static void SetDirect(Runtime.FacadeSession Session, ulong ObjectId, string Property, params string[] PropertyValue)
    {
        var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(PropertyValue);
        Session.Gui.Mutate(Fields.ToArray(), () => "9999");
    }

    private static Runtime.FacadeSession FindCommand(Runtime.FacadeWorld World, string Name)
    { foreach (Runtime.FacadeSession Session in World.Sessions()) if (Session.Commands.ContainsKey(Name)) return Session; throw new Exception("GUI Foundation 2E: command owner missing " + Name); }

    private static ulong FindClass(Runtime.FacadeSession Session, Runtime.GuiClassId Expected)
    {
        for (ulong Id = 1; Id <= 32; ++Id) {
            Runtime.GuiClassId Actual; var Identity = new Runtime.GuiObjectIdentity((ulong)Session.VmGenerationId, (ulong)Session.DomainLifetimeId, Id);
            if (Session.Gui.TryGetClass(Identity, out Actual) && Actual == Expected) return Id;
        }
        throw new Exception("GUI Foundation 2E: retained class missing " + Expected);
    }

    private static void Process(Runtime.AddonRegistry Registry)
    { int Guard = 256; while (Registry.HasPending && Guard-- > 0) Registry.ProcessOne(); Check(!Registry.HasPending, "addon lifecycle processing converged"); }

    private static byte[] OwnerArchive(string Version)
    {
        string Manifest = "{\"schema\":1,\"id\":\"richowner\",\"version\":\"" + Version + "\",\"main\":\"api\"}";
        string Init = "local A=require('api'); local P=game:GetService('Players'):GetPlayers()[1]; A.Player=P; A.Screen:Show(P); A.Scroll:ScrollTo(P,Vector2.new(0.5,0.5)); game:GetService('Commands'):Register('richowner',{},function() print(A.Scroll.ScrollingDirection) end)";
        string Api = "local G=game:GetService('Gui'); local S=G:Create('ScreenGui'); local F=S:Create('ScrollingFrame'); F.CanvasSize=UDim2.fromOffset(480,1200); " +
            "local L=F:Create('UIListLayout'); local Pad=F:Create('UIPadding'); local C=F:Create('Frame'); C.ClipsDescendants=true; local Grid=C:Create('UIGridLayout'); Grid.CellSize=UDim2.fromOffset(32,24); " +
            "local Label=C:Create('TextButton'); Label.Font=GuiFont.RobotoCondensedBold; local I=F:Create('ImageLabel'); I.Image=ImageSource.Png('42'); " +
            "local B=F:Create('ImageButton'); B.Image=ImageSource.Png('43'); B.Activated:Connect(function() print('owner-click') end); return {Screen=S,Scroll=F,Layout=L,Padding=Pad,Clip=C,Grid=Grid,Label=Label,Image=I,Button=B,Value=ImageSource.Png('42')}";
        return Archive(Manifest, Init, Api);
    }

    private static byte[] ConsumerArchive(string Id, bool Required, string Init)
    {
        string Dependencies = Required ? "{\"required\":[\"richowner\"],\"optional\":[]}" : "{\"required\":[],\"optional\":[\"richowner\"]}";
        return Archive("{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\",\"dependencies\":" + Dependencies + "}", Init, null);
    }

    private static byte[] Archive(string Manifest, string Init, string Api)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Write(Zip, "addon.json", Manifest); Write(Zip, "init.luau", Init); if (Api != null) Write(Zip, "api.luau", Api);
            }
            return Output.ToArray();
        }
    }

    private static void Write(ZipArchive Zip, string Name, string Source)
    { using (Stream Stream = Zip.CreateEntry(Name).Open()) { byte[] Bytes = Encoding.UTF8.GetBytes(Source); Stream.Write(Bytes, 0, Bytes.Length); } }
}
