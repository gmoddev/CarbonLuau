using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation1CTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private sealed class PlayerFixture
    {
        internal readonly Runtime.PlayerDirectory Directory;
        internal Runtime.PlayerView Current;
        internal PlayerFixture(string UserId, string Name)
        {
            Directory = new Runtime.PlayerDirectory(Id => Current != null && Current.UserId == Id ? Current : null);
            Reconnect(UserId, Name);
        }
        internal Runtime.PlayerLifetime Reconnect(string UserId, string Name)
        {
            Current = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId, Name = Name,
                Connected = true, Send = Value => { }, Permission = Value => true};
            return Directory.Connect(Current);
        }
    }
    private sealed class Transport : Runtime.IRustCuiTransport
    {
        internal string LastPayload; internal Runtime.GuiBackendTarget LastTarget;
        internal int Replaces, Updates, Destroys; internal Runtime.GuiBackendResultCode Next = Runtime.GuiBackendResultCode.Accepted;
        public Runtime.GuiBackendResult Replace(Runtime.GuiBackendTarget Target, string Payload)
        { LastTarget = Target; LastPayload = Payload; Replaces++; return Result(); }
        public Runtime.GuiBackendResult Update(Runtime.GuiBackendTarget Target, string Payload)
        { LastTarget = Target; LastPayload = Payload; Updates++; return Result(); }
        public Runtime.GuiBackendResult Destroy(Runtime.GuiBackendTarget Target)
        { LastTarget = Target; Destroys++; return Result(); }
        private Runtime.GuiBackendResult Result()
        {
            Runtime.GuiBackendResultCode Value = Next; Next = Runtime.GuiBackendResultCode.Accepted;
            return Value == Runtime.GuiBackendResultCode.Accepted ? Runtime.GuiBackendResult.Success() : Runtime.GuiBackendResult.Failure(Value, "injected transport result");
        }
    }

    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("GUI Foundation 1C: " + Message); }
    private static bool Near(double Left, double Right) { return Math.Abs(Left - Right) < 0.000000001; }
    private static void Reject(Action Value, string Message)
    { bool Rejected = false; try { Value(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static ulong Id(string[] Value) { return UInt64.Parse(Value[0]); }
    private static string[] Create(Runtime.GuiRetainedRegistry Gui, string ClassName, ulong Parent = 0)
    { return Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, () => Guid.NewGuid().ToString("N")); }
    private static void Set(Runtime.GuiRetainedRegistry Gui, ulong Object, string Property, params string[] Value)
    { var Fields = new List<string> {"set", Object.ToString(), Property}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), () => Guid.NewGuid().ToString("N")); }
    private static void Show(Runtime.GuiRetainedRegistry Gui, ulong Screen, Runtime.PlayerLifetime Player)
    { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, () => "unused"); }
    private static void Hide(Runtime.GuiRetainedRegistry Gui, ulong Screen, Runtime.PlayerLifetime Player)
    { Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, () => "unused"); }
    private static bool Shown(Runtime.GuiRetainedRegistry Gui, ulong Screen, Runtime.PlayerLifetime Player)
    { return Gui.Query(new[] {"shown", Screen.ToString(), Player.Token, Player.UserId})[0] == "1"; }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId IdValue)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == IdValue) return Value.Value; throw new Exception("missing render property " + IdValue); }
    private static void DrainGui(Runtime.GuiRetainedRegistry Gui, Runtime.GuiLimits Limits)
    { int Guard = 0; while (Gui.HasWork && Guard++ < 32) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(!Gui.HasWork, "focused GUI drain completed"); }

    internal static void RunModel()
    {
        var Limits = new Runtime.GuiConfig().Validate(); var Players = new PlayerFixture("76561190000000001", "One");
        Runtime.PlayerLifetime One = Players.Directory.Find(Players.Current.UserId); var Backend = new Runtime.InMemoryGuiBackend();
        var World = new Runtime.GuiRetainedWorld(Limits, Players.Directory, Backend); var Gui = new Runtime.GuiRetainedRegistry(World, 7, 9);
        ulong Screen = Id(Create(Gui, "ScreenGui")); ulong FrameA = Id(Create(Gui, "Frame", Screen)); ulong FrameB = Id(Create(Gui, "Frame", Screen));
        Set(Gui, FrameA, "Name", "string", "not-an-id"); Set(Gui, FrameA, "ZIndex", "integer", "9"); Set(Gui, FrameB, "ZIndex", "integer", "2");
        Set(Gui, FrameA, "Position", "udim2", "0.5", "10", "0.25", "-20"); Set(Gui, FrameA, "Size", "udim2", "0.4", "100", "0.2", "50");
        Set(Gui, FrameA, "AnchorPoint", "vector2", "0.5", "1"); Set(Gui, FrameA, "BackgroundColor3", "color3", "0.25", "0.5", "0.75");
        Set(Gui, FrameA, "BackgroundTransparency", "number", "0.25");
        ulong Label = Id(Create(Gui, "TextLabel", FrameA)); Set(Gui, Label, "Text", "string", "hello"); Set(Gui, Label, "TextColor3", "color3", "1", "0.5", "0");
        Set(Gui, Label, "TextTransparency", "number", "0.2"); Set(Gui, Label, "TextSize", "integer", "24");
        Set(Gui, Label, "TextXAlignment", "string", "Right"); Set(Gui, Label, "TextYAlignment", "string", "Bottom");
        ulong Button = Id(Create(Gui, "TextButton", FrameA)); Set(Gui, Button, "Position", "udim2", "0", "-20", "1", "-40");

        Show(Gui, Screen, One); Check(Shown(Gui, Screen, One) && Gui.PresentationCount == 1 && Backend.Calls().Length == 0, "Show stages desired state without sending");
        Show(Gui, Screen, One); Check(Gui.PresentationCount == 1, "duplicate Show is idempotent");
        Hide(Gui, Screen, One); Check(!Shown(Gui, Screen, One) && Gui.PresentationCount == 0 && Backend.Calls().Length == 0, "Show then Hide before flush emits nothing");
        Hide(Gui, Screen, One); Check(Gui.PresentationCount == 0, "duplicate Hide is idempotent");
        Show(Gui, Screen, One); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call FirstSend = Backend.Calls()[0]; Check(FirstSend.Kind == Runtime.GuiBackendOperationKind.Replace && FirstSend.Result.Accepted, "initial full replacement");
        Runtime.GuiRenderElement[] Elements = FirstSend.Plan.Elements;
        Check(Elements[0].ParentClientId == null && Elements[1].ClientId.EndsWith(FrameB.ToString("x")) && Elements[2].ClientId.EndsWith(FrameA.ToString("x")), "parent-before-child and Z order");
        Runtime.GuiRenderElement FrameElement = Elements[2];
        Runtime.GuiRenderVector2 AnchorMin = Property(FrameElement, Runtime.GuiRenderPropertyId.AnchorMin).Vector;
        Runtime.GuiRenderVector2 AnchorMax = Property(FrameElement, Runtime.GuiRenderPropertyId.AnchorMax).Vector;
        Runtime.GuiRenderVector2 OffsetMin = Property(FrameElement, Runtime.GuiRenderPropertyId.OffsetMin).Vector;
        Runtime.GuiRenderVector2 OffsetMax = Property(FrameElement, Runtime.GuiRenderPropertyId.OffsetMax).Vector;
        Check(Near(AnchorMin.X, 0.3) && Near(AnchorMin.Y, 0.75) && Near(AnchorMax.X, 0.7) && Near(AnchorMax.Y, 0.95) &&
            OffsetMin.X == -40 && OffsetMin.Y == 20 && OffsetMax.X == 60 && OffsetMax.Y == 70, "UDim2/AnchorPoint golden translation");
        Runtime.GuiRenderColor Background = Property(FrameElement, Runtime.GuiRenderPropertyId.BackgroundColor).Color;
        Check(Background.R == 0.25 && Background.G == 0.5 && Background.B == 0.75 && Background.A == 0.75, "background color/transparency translation");
        Runtime.GuiRenderElement TextElement = null;
        foreach (Runtime.GuiRenderElement Element in Elements) if (Element.Kind == Runtime.GuiRenderNodeKind.Text && Property(Element, Runtime.GuiRenderPropertyId.Text).Text == "hello") TextElement = Element;
        Check(TextElement != null && Property(TextElement, Runtime.GuiRenderPropertyId.FontSize).Integer == 24 &&
            Property(TextElement, Runtime.GuiRenderPropertyId.TextXAlignment).Text == "Right" && Property(TextElement, Runtime.GuiRenderPropertyId.TextYAlignment).Text == "Bottom" &&
            Property(TextElement, Runtime.GuiRenderPropertyId.TextColor).Color.A == 0.8, "text rendering properties");
        Check(Property(Elements[0], Runtime.GuiRenderPropertyId.NeedsCursor).Boolean, "effectively visible TextButton requests cursor");
        Runtime.GuiRenderElement ButtonElement = null;
        foreach (Runtime.GuiRenderElement Element in Elements) if (Element.Kind == Runtime.GuiRenderNodeKind.Button) ButtonElement = Element;
        Check(ButtonElement != null && ButtonElement.ParentClientId == FrameElement.ClientId &&
            Property(ButtonElement, Runtime.GuiRenderPropertyId.AnchorMin).Vector.Y == 0 &&
            Property(ButtonElement, Runtime.GuiRenderPropertyId.OffsetMin).Vector.X == -20 &&
            Property(ButtonElement, Runtime.GuiRenderPropertyId.OffsetMin).Vector.Y == 4,
            "nested negative-offset layout remains parent-relative");
        string FirstRoot = FirstSend.Target.ClientRootId; Check(FirstSend.Plan.Describe().IndexOf("not-an-id", StringComparison.Ordinal) < 0, "Name is absent from client ids and plans");

        Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "injected full send failure");
        Set(Gui, FrameA, "Name", "string", "renamed-without-identity"); Set(Gui, FrameB, "ZIndex", "integer", "10"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Shown(Gui, Screen, One) && Gui.PresentationCount == 1, "backend failure preserves retained desired state");
        Show(Gui, Screen, One); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls()[Backend.Calls().Length - 1].Target.ClientRootId == FirstRoot, "retry and Name changes retain presentation client ids");

        Hide(Gui, Screen, One); Show(Gui, Screen, One); Check(Shown(Gui, Screen, One), "Hide then Show restores desired state");
        Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls(); string FreshRoot = Calls[Calls.Length - 1].Target.ClientRootId;
        Check(Calls[Calls.Length - 2].Kind == Runtime.GuiBackendOperationKind.Destroy && FreshRoot != FirstRoot, "Hide then Show destroys old root and uses a fresh epoch");

        var PlayersTwo = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = "76561190000000002", Name = "Two", Connected = true,
            Send = Value => { }, Permission = Value => true}; Runtime.PlayerLifetime Two = Players.Directory.Connect(PlayersTwo);
        // Switch the bounded lookup fixture between exact players for direct resolution.
        Runtime.PlayerView OneView = Players.Current; Players.Current = PlayersTwo; Show(Gui, Screen, Two); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Gui.PresentationCount == 2, "one ScreenGui supports multiple exact viewers");
        ulong Clone = Id(Gui.Mutate(new[] {"clone", Screen.ToString()}, () => "clone")); Hide(Gui, Screen, Two); Show(Gui, Clone, Two);
        Check(Shown(Gui, Clone, Two), "cloned ScreenGui is independent per-player retained state");

        Gui.BeginPublication(); Hide(Gui, Clone, Two); Check(!Shown(Gui, Clone, Two), "provisional Hide read-your-writes"); Gui.RollbackPublication();
        Check(Shown(Gui, Clone, Two), "provisional Hide rollback");
        Gui.BeginPublication(); Hide(Gui, Clone, Two); Gui.CommitPublication(); Check(!Shown(Gui, Clone, Two), "provisional Hide commit");
        Gui.BeginPublication(); Show(Gui, Clone, Two); Check(Shown(Gui, Clone, Two), "provisional Show read-your-writes");
        int CallsBeforeProvisionalFlush = Backend.Calls().Length; Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls().Length == CallsBeforeProvisionalFlush, "provisional Show cannot flush client UI before commit"); Gui.RollbackPublication();
        Check(!Shown(Gui, Clone, Two), "provisional Show rollback");
        Gui.BeginPublication(); Show(Gui, Clone, Two); Gui.CommitPublication(); Check(Shown(Gui, Clone, Two), "provisional Show commit");

        Players.Current = OneView; Runtime.PlayerLifetime Disconnected = Players.Directory.Disconnect(One.UserId, One.Identity); Gui.Disconnect(Disconnected);
        Check(Gui.PresentationCount == 1, "disconnect drops exact presentation");
        Runtime.PlayerLifetime Reconnected = Players.Reconnect(One.UserId, "OneAgain");
        Check(!Shown(Gui, Screen, Reconnected), "same-account reconnect does not inherit presentation");

        Players.Current = PlayersTwo; DrainGui(Gui, Limits); int CallsBeforeScreenDestroy = Backend.Calls().Length;
        Gui.Mutate(new[] {"destroy", Clone.ToString()}, () => "destroy"); Check(Gui.PresentationCount == 0, "ScreenGui Destroy removes desired presentation");
        Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(Backend.Calls().Length == CallsBeforeScreenDestroy + 1 &&
            Backend.Calls()[Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Destroy, "ScreenGui Destroy schedules best-effort root destruction");
        Show(Gui, Screen, Two); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); int CallsBeforeDomainDispose = Backend.Calls().Length;
        Gui.Dispose(); Check(World.LiveObjects == 0 && World.LivePresentations == 0 && Backend.Calls().Length == CallsBeforeDomainDispose + 1 &&
            Backend.Calls()[Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Destroy,
            "domain teardown returns counts to zero and destroys sent roots");

        RunBackendTests(Limits);
        RunUnavailableAndStaleTests(Limits);
        RunCursorAbsenceTest(Limits);
        RunBoundedPayloadTest();
        Console.WriteLine("[CarbonLuau:GuiFoundation1CModel] PASS presentations, layout, ordering, cursor, exact lifetime, publication and Rust CUI backend");
    }

    private static void RunUnavailableAndStaleTests(Runtime.GuiLimits Limits)
    {
        var Players = new PlayerFixture("76561190000000005", "Unavailable"); Runtime.PlayerLifetime Player = Players.Directory.Find(Players.Current.UserId);
        var TransportValue = new Transport(); TransportValue.Next = Runtime.GuiBackendResultCode.TargetUnavailable;
        var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, Players.Directory,
            new Runtime.RustCuiBackend(Limits, TransportValue)), 4, 5);
        ulong Screen = Id(Create(Gui, "ScreenGui")); Show(Gui, Screen, Player); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Gui.PresentationCount == 0 && !ShownAfterReconnect(Gui, Screen, Players), "target-unavailable send drops desired presentation");
        Show(Gui, Screen, Player); Players.Directory.Disconnect(Player.UserId, Player.Identity); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Gui.PresentationCount == 0 && TransportValue.Replaces == 1, "stale exact connection is dropped before backend send");
        Gui.Dispose();
    }

    private static bool ShownAfterReconnect(Runtime.GuiRetainedRegistry Gui, ulong Screen, PlayerFixture Players)
    {
        Runtime.PlayerLifetime Current = Players.Directory.Find(Players.Current.UserId);
        return Current != null && Shown(Gui, Screen, Current);
    }

    private static void RunCursorAbsenceTest(Runtime.GuiLimits Limits)
    {
        var Players = new PlayerFixture("76561190000000004", "NoCursor"); Runtime.PlayerLifetime Player = Players.Directory.Find(Players.Current.UserId);
        var Backend = new Runtime.InMemoryGuiBackend(); var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, Players.Directory, Backend), 2, 3);
        ulong Screen = Id(Create(Gui, "ScreenGui")); Create(Gui, "TextLabel", Screen); Show(Gui, Screen, Player); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(!Property(Backend.Calls()[0].Plan.Elements[0], Runtime.GuiRenderPropertyId.NeedsCursor).Boolean,
            "presentation without a visible interactive button does not request cursor");
        Gui.Dispose();
    }

    private static void RunBackendTests(Runtime.GuiLimits Limits)
    {
        var Transport = new Transport(); var Backend = new Runtime.RustCuiBackend(Limits, Transport);
        var Target = new Runtime.GuiBackendTarget("7", "76561190000000007", "cluau_root");
        var Root = new Runtime.GuiRenderElement("cluau_root", null, Runtime.GuiRenderNodeKind.Container, Limits,
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.AnchorMin, Runtime.GuiRenderValue.FromVector(0, 0)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.AnchorMax, Runtime.GuiRenderValue.FromVector(1, 1)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.OffsetMin, Runtime.GuiRenderValue.FromVector(0, 0)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.OffsetMax, Runtime.GuiRenderValue.FromVector(0, 0)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Pivot, Runtime.GuiRenderValue.FromVector(.5, .5)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Visible, Runtime.GuiRenderValue.FromBoolean(true)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.NeedsCursor, Runtime.GuiRenderValue.FromBoolean(true)));
        var Button = new Runtime.GuiRenderElement("cluau_button", "cluau_root", Runtime.GuiRenderNodeKind.Button, Limits,
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.AnchorMin, Runtime.GuiRenderValue.FromVector(0, 0)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.AnchorMax, Runtime.GuiRenderValue.FromVector(0, 0)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.OffsetMin, Runtime.GuiRenderValue.FromVector(5, 6)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.OffsetMax, Runtime.GuiRenderValue.FromVector(105, 42)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Pivot, Runtime.GuiRenderValue.FromVector(0, 1)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Visible, Runtime.GuiRenderValue.FromBoolean(true)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.BackgroundColor, Runtime.GuiRenderValue.FromColor(1, .5, 0, .75)));
        var Plan = new Runtime.GuiRenderPlan(Limits, 256, Root, Button);
        Check(Backend.Replace(Target, Plan).Accepted && Transport.LastPayload.StartsWith("[{") && Transport.LastPayload.Contains("\"destroyUi\":\"cluau_root\"") &&
            Transport.LastPayload.Contains("\"type\":\"NeedsCursor\"") && Transport.LastPayload.Contains("\"parent\":\"Overlay\"") &&
            Transport.LastPayload.Contains("\"type\":\"UnityEngine.UI.Button\"") && Transport.LastPayload.IndexOf("\"command\"", StringComparison.Ordinal) < 0,
            "bounded deterministic Rust CUI full serialization without a client command");
        Transport.Next = Runtime.GuiBackendResultCode.TargetUnavailable; Check(Backend.Destroy(Target).Code == Runtime.GuiBackendResultCode.TargetUnavailable, "backend unavailable classification");
        Transport.Next = Runtime.GuiBackendResultCode.SendFailed; Check(Backend.Replace(Target, Plan).Code == Runtime.GuiBackendResultCode.SendFailed, "backend send failure classification");
    }

    private static void RunBoundedPayloadTest()
    {
        var Config = new Runtime.GuiConfig {MaxSerializedOperationBytes = 128, MaxSerializedBytesPerFlush = 128, MaxTextUtf8Bytes = 8, MaxTextUtf8BytesPerScreen = 8};
        Runtime.GuiLimits Limits = Config.Validate(); var Players = new PlayerFixture("76561190000000003", "Bounded");
        Runtime.PlayerLifetime Player = Players.Directory.Find(Players.Current.UserId); var Backend = new Runtime.InMemoryGuiBackend();
        var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, Players.Directory, Backend), 1, 1);
        ulong Screen = Id(Create(Gui, "ScreenGui"));
        Reject(() => Show(Gui, Screen, Player), "oversized full plan rejected before desired presentation publication");
        Check(Gui.PresentationCount == 0, "payload rejection leaves no presentation state"); Gui.Dispose();
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var Players = new PlayerFixture("76561190000000011", "Native"); Runtime.PlayerLifetime Player = Players.Directory.Find(Players.Current.UserId);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Players.Directory, new Registrar(), Backend);
        string Source = "local State=require('state'); local P=game:GetService('Players'):GetPlayers()[1]; local G=game:GetService('Gui'); local S=G:Create('ScreenGui'); " +
            "local F=S:Create('Frame'); F.Name='private-name'; F:Create('TextButton').Text='Open'; assert(not pcall(function() return F.Show end)); " +
            "S:Show(P); S:Show(P); assert(S:IsShown(P)); State.Screen=S; State.Player=P; return true";
        Func<Runtime.ScriptSnapshot> Snapshot = () => { var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source}; Value.Modules.Add("state", "return {}"); return Value; };
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Runtime.ExecutionResult Loaded = Host.Reload(); Check(Loaded.Status == Runtime.RuntimeStatus.OK && World.Active.Gui.PresentationCount == 1 && Backend.Calls().Length == 0,
                "native provisional Show commits without sending during Luau entry: " + Loaded.Error);
            Host.Drain(); Check(Backend.Calls().Length == 1 && Backend.Calls()[0].Kind == Runtime.GuiBackendOperationKind.Replace, "native drain sends initial full plan outside Luau entry");
            Runtime.ExecutionResult Hidden = Host.Execute("gui.hide", "local State=require('state'); State.Screen:Hide(State.Player); State.Screen:Hide(State.Player); assert(not State.Screen:IsShown(State.Player))");
            Check(Hidden.Status == Runtime.RuntimeStatus.OK && World.Active.Gui.PresentationCount == 0 && Backend.Calls().Length == 1,
                "native duplicate Hide updates desired state without sending during Luau entry: " + Hidden.Error);
            Host.Drain(); Check(Backend.Calls().Length == 2 && Backend.Calls()[1].Kind == Runtime.GuiBackendOperationKind.Destroy,
                "native drain destroys the hidden presentation outside Luau entry");
            Runtime.ExecutionResult ShownAgain = Host.Execute("gui.show-again", "local State=require('state'); State.Screen:Show(State.Player); assert(State.Screen:IsShown(State.Player))");
            Check(ShownAgain.Status == Runtime.RuntimeStatus.OK && World.Active.Gui.PresentationCount == 1 && Backend.Calls().Length == 2,
                "native Show after Hide creates desired state without reentrant client send: " + ShownAgain.Error);
            Host.Drain(); Check(Backend.Calls().Length == 3 && Backend.Calls()[2].Kind == Runtime.GuiBackendOperationKind.Replace,
                "native Show after Hide flushes a fresh replacement");
            Source = "local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); S:Show(P); error('rollback show')";
            Runtime.FacadeSession Previous = World.Active; Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && World.Active == Previous && World.Gui.LivePresentations == 1,
                "failed candidate Show rolls back with no client side effect");
        }
        Check(World.Gui.LivePresentations == 0, "native teardown clears presentations");
        Console.WriteLine("[CarbonLuau:GuiFoundation1CNative] PASS public Show/Hide/IsShown surface, deferred send, rollback and teardown");
    }
}
