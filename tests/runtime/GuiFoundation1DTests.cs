using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation1DTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Players
    {
        internal readonly Runtime.PlayerDirectory Directory;
        private readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal Players() { Directory = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; }); }
        internal Runtime.PlayerLifetime Add(int Index)
        {
            string Id = (76561190000100000L + Index).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id, Name = "Viewer" + Index,
                Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Directory.Connect(View);
        }
        internal Runtime.PlayerLifetime Remove(Runtime.PlayerLifetime Player)
        { Runtime.PlayerView View = Views[Player.UserId]; Views.Remove(Player.UserId); return Directory.Disconnect(Player.UserId, View.Identity); }
    }

    private sealed class Transport : Runtime.IRustCuiTransport
    {
        internal Runtime.GuiBackendResultCode Next = Runtime.GuiBackendResultCode.Accepted;
        internal int Replaces, Updates, Destroys; internal long Bytes;
        internal string LastPayload;
        public Runtime.GuiBackendResult Replace(Runtime.GuiBackendTarget Target, string Payload)
        { Replaces++; Bytes += Runtime.GuiRenderValue.Utf8Bytes(Payload); LastPayload = Payload; return Result(); }
        public Runtime.GuiBackendResult Update(Runtime.GuiBackendTarget Target, string Payload)
        { Updates++; Bytes += Runtime.GuiRenderValue.Utf8Bytes(Payload); LastPayload = Payload; return Result(); }
        public Runtime.GuiBackendResult Destroy(Runtime.GuiBackendTarget Target)
        { Destroys++; return Result(); }
        private Runtime.GuiBackendResult Result()
        {
            Runtime.GuiBackendResultCode Value = Next; Next = Runtime.GuiBackendResultCode.Accepted;
            return Value == Runtime.GuiBackendResultCode.Accepted ? Runtime.GuiBackendResult.Success() :
                Runtime.GuiBackendResult.Failure(Value, "injected GUI-1D transport result");
        }
        internal void Reset() { Replaces = Updates = Destroys = 0; Bytes = 0; LastPayload = null; }
    }

    private sealed class SlowBackend : Runtime.IGuiBackend
    {
        private readonly Runtime.InMemoryGuiBackend Inner = new Runtime.InMemoryGuiBackend();
        internal int Calls { get { return Inner.Calls().Length; } }
        public int MeasureReplace(Runtime.GuiBackendTarget Target, Runtime.GuiRenderPlan Plan) { return Inner.MeasureReplace(Target, Plan); }
        public int MeasureUpdate(Runtime.GuiBackendTarget Target, Runtime.GuiRenderPatch Patch) { return Inner.MeasureUpdate(Target, Patch); }
        public int MeasureDestroy(Runtime.GuiBackendTarget Target) { return Inner.MeasureDestroy(Target); }
        public Runtime.GuiBackendResult Replace(Runtime.GuiBackendTarget Target, Runtime.GuiRenderPlan Plan)
        { Delay(); return Inner.Replace(Target, Plan); }
        public Runtime.GuiBackendResult Update(Runtime.GuiBackendTarget Target, Runtime.GuiRenderPatch Patch)
        { Delay(); return Inner.Update(Target, Patch); }
        public Runtime.GuiBackendResult Destroy(Runtime.GuiBackendTarget Target)
        { Delay(); return Inner.Destroy(Target); }
        private static void Delay()
        { var Watch = Stopwatch.StartNew(); while (Watch.Elapsed.TotalMilliseconds < 3) { } }
    }

    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("GUI Foundation 1D: " + Message); }
    private static ulong Id(string[] Value) { return UInt64.Parse(Value[0]); }
    private static ulong Create(Runtime.GuiRetainedRegistry Gui, string ClassName, ulong Parent = 0)
    { return Id(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, () => Guid.NewGuid().ToString("N"))); }
    private static void Set(Runtime.GuiRetainedRegistry Gui, ulong ObjectId, string Property, params string[] Value)
    { var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), () => "unused"); }
    private static void Parent(Runtime.GuiRetainedRegistry Gui, ulong ObjectId, ulong ParentId)
    { Set(Gui, ObjectId, "Parent", ParentId == 0 ? new[] {"nil"} : new[] {"object", ParentId.ToString()}); }
    private static void Show(Runtime.GuiRetainedRegistry Gui, ulong Screen, Runtime.PlayerLifetime Player)
    { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, () => "unused"); }
    private static void Hide(Runtime.GuiRetainedRegistry Gui, ulong Screen, Runtime.PlayerLifetime Player)
    { Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, () => "unused"); }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId IdValue)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == IdValue) return Value.Value; return null; }
    private static void Drain(Runtime.GuiRetainedRegistry Gui, Runtime.GuiLimits Limits, int Guard = 4096)
    { while (Gui.HasWork && Guard-- > 0) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(!Gui.HasWork, "bounded focused drain converged"); }

    internal static void RunModel()
    {
        RunPatchAndStructural();
        RunDirtyOverflowAndCheckpoint();
        RunFailureConvergence();
        RunOrderingAndPayloadBounds();
        RunPublicationAndLifetime();
        RunFairness();
        RunPerformance();
        Console.WriteLine("[CarbonLuau:GuiFoundation1DModel] PASS dirty coalescing, patch/full convergence, bounds, fairness and viewer scale");
    }

    private static void RunPatchAndStructural()
    {
        Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate(); var PlayersValue = new Players(); Runtime.PlayerLifetime Player = PlayersValue.Add(1);
        var Backend = new Runtime.InMemoryGuiBackend(); var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, Backend), 11, 12);
        ulong ScreenA = Create(Gui, "ScreenGui"), FrameA = Create(Gui, "Frame", ScreenA), LabelA = Create(Gui, "TextLabel", FrameA);
        ulong FrameB = Create(Gui, "Frame", ScreenA); Show(Gui, ScreenA, Player); Drain(Gui, Limits); int Calls = Backend.Calls().Length;

        Set(Gui, LabelA, "Name", "string", "metadata"); Check(!Gui.HasWork && Backend.Calls().Length == Calls, "metadata-only Name creates no synchronization work");
        Set(Gui, LabelA, "Text", "string", "A"); Set(Gui, LabelA, "Text", "string", "B");
        Set(Gui, LabelA, "TextSize", "integer", "23"); Set(Gui, LabelA, "TextXAlignment", "string", "Right");
        Set(Gui, LabelA, "TextYAlignment", "string", "Bottom"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call TextPatch = Backend.Calls()[Calls++];
        Check(TextPatch.Kind == Runtime.GuiBackendOperationKind.Update && TextPatch.Patch.Elements.Length == 1 &&
            Property(TextPatch.Patch.Elements[0], Runtime.GuiRenderPropertyId.Text).Text == "B" &&
            Property(TextPatch.Patch.Elements[0], Runtime.GuiRenderPropertyId.FontSize).Integer == 23 &&
            TextPatch.Patch.Describe().IndexOf("1:A", StringComparison.Ordinal) < 0, "repeated text writes coalesce to latest text and text properties");

        Set(Gui, FrameA, "Position", "udim2", ".25", "-10", ".5", "20");
        Set(Gui, FrameA, "Size", "udim2", ".5", "100", ".25", "40"); Set(Gui, FrameA, "AnchorPoint", "vector2", ".5", ".25");
        Set(Gui, FrameA, "BackgroundColor3", "color3", ".1", ".2", ".3"); Set(Gui, FrameA, "BackgroundTransparency", "number", ".4");
        Set(Gui, FrameB, "Visible", "boolean", "0"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call MultiPatch = Backend.Calls()[Calls++];
        Check(MultiPatch.Kind == Runtime.GuiBackendOperationKind.Update && MultiPatch.Patch.Elements.Length == 2 &&
            Property(MultiPatch.Patch.Elements[0], Runtime.GuiRenderPropertyId.AnchorMin) != null &&
            Property(MultiPatch.Patch.Elements[0], Runtime.GuiRenderPropertyId.BackgroundColor).Color.A == .6 &&
            Property(MultiPatch.Patch.Elements[1], Runtime.GuiRenderPropertyId.Visible).Boolean == false,
            "layout, color/transparency, visibility and multiple dirty objects share one deterministic patch");

        ulong Created = Create(Gui, "Frame", FrameA); Set(Gui, Created, "BackgroundTransparency", "number", ".5"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls()[Calls++].Kind == Runtime.GuiBackendOperationKind.Replace, "structural creation plus mutation emits only final full tree");
        Set(Gui, Created, "ZIndex", "integer", "9"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls()[Calls++].Kind == Runtime.GuiBackendOperationKind.Replace, "ZIndex is structural");

        Set(Gui, LabelA, "Text", "string", "obsolete"); Gui.Mutate(new[] {"destroy", LabelA.ToString()}, () => "unused"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call Destroyed = Backend.Calls()[Calls++];
        Check(Destroyed.Kind == Runtime.GuiBackendOperationKind.Replace && Destroyed.Plan.Describe().IndexOf("obsolete", StringComparison.Ordinal) < 0,
            "Destroy before flush suppresses obsolete property patch");
        Parent(Gui, Created, FrameB); Set(Gui, Created, "Visible", "boolean", "0"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls()[Calls++].Kind == Runtime.GuiBackendOperationKind.Replace, "reparent plus mutation emits final structural tree");

        ulong ScreenB = Create(Gui, "ScreenGui"), Other = Create(Gui, "Frame", ScreenB); Show(Gui, ScreenB, Player); Drain(Gui, Limits); Calls = Backend.Calls().Length;
        Parent(Gui, Created, Other); Drain(Gui, Limits);
        Runtime.InMemoryGuiBackend.Call[] All = Backend.Calls(); int Replaces = 0;
        for (int Index = Calls; Index < All.Length; ++Index) if (All[Index].Kind == Runtime.GuiBackendOperationKind.Replace) Replaces++;
        Check(Replaces == 2, "cross-ScreenGui reparent fully reconciles both shown roots");

        Hide(Gui, ScreenB, Player); Drain(Gui, Limits); Calls = Backend.Calls().Length; Set(Gui, Created, "Visible", "boolean", "1");
        Check(!Gui.HasWork && Backend.Calls().Length == Calls, "mutation while hidden creates no client work");
        Show(Gui, ScreenB, Player); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls()[Calls].Kind == Runtime.GuiBackendOperationKind.Replace, "later Show renders current hidden-state mutation");
        Gui.Dispose();
    }

    private static void RunDirtyOverflowAndCheckpoint()
    {
        var Config = new Runtime.GuiConfig {MaxTrackedDirtyObjectsPerDomain = 1, PatchBatchesBeforeFull = 2}; Runtime.GuiLimits Limits = Config.Validate();
        var PlayersValue = new Players(); Runtime.PlayerLifetime Player = PlayersValue.Add(2); var Backend = new Runtime.InMemoryGuiBackend();
        var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, Backend), 20, 21);
        ulong Screen = Create(Gui, "ScreenGui"), First = Create(Gui, "Frame", Screen), Second = Create(Gui, "Frame", Screen); Show(Gui, Screen, Player); Drain(Gui, Limits);
        Set(Gui, First, "Position", "udim2", "0", "1", "0", "2"); Set(Gui, Second, "Position", "udim2", "0", "3", "0", "4");
        Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(Backend.Calls()[Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Replace,
            "dirty-object saturation collapses to full rebuild without rejecting mutations");
        Set(Gui, First, "Visible", "boolean", "0"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Set(Gui, First, "Visible", "boolean", "1"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Set(Gui, First, "BackgroundTransparency", "number", ".5"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        Check(Calls[Calls.Length - 3].Kind == Runtime.GuiBackendOperationKind.Update && Calls[Calls.Length - 2].Kind == Runtime.GuiBackendOperationKind.Update &&
            Calls[Calls.Length - 1].Kind == Runtime.GuiBackendOperationKind.Replace, "patch-history threshold forces the next dirty synchronization full");
        Gui.Dispose();
    }

    private static void RunFailureConvergence()
    {
        Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate(); var PlayersValue = new Players(); Runtime.PlayerLifetime Player = PlayersValue.Add(3);
        var Backend = new Runtime.InMemoryGuiBackend(); var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, Backend), 30, 31);
        ulong Screen = Create(Gui, "ScreenGui"), Label = Create(Gui, "TextLabel", Screen); Show(Gui, Screen, Player); Drain(Gui, Limits);
        Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "update failed");
        Set(Gui, Label, "Text", "string", "one"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Set(Gui, Label, "Text", "string", "two"); Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "replace failed");
        Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Set(Gui, Label, "Text", "string", "final"); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Runtime.InMemoryGuiBackend.Call Last = Backend.Calls()[Backend.Calls().Length - 1];
        Check(Last.Kind == Runtime.GuiBackendOperationKind.Replace && Last.Result.Accepted && Last.Plan.Describe().IndexOf("final", StringComparison.Ordinal) >= 0 && !Gui.HasWork,
            "update/replace failures converge with one newest-state full replacement");

        var TransportValue = new Transport(); var RustBackend = new Runtime.RustCuiBackend(Limits, TransportValue);
        var Other = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, RustBackend), 32, 33);
        ulong OtherScreen = Create(Other, "ScreenGui"), OtherLabel = Create(Other, "TextLabel", OtherScreen); Show(Other, OtherScreen, Player); Drain(Other, Limits);
        TransportValue.Next = Runtime.GuiBackendResultCode.TargetUnavailable; Set(Other, OtherLabel, "Text", "string", "retry"); Other.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(TransportValue.Updates == 1 && Other.HasWork, "Update target unavailable promotes presentation to full resync");
        Other.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(TransportValue.Replaces == 2 && !Other.HasWork, "full resync recovers after unavailable Update");

        Set(Other, OtherLabel, "Text", "string", "budget"); int Before = TransportValue.Updates;
        Check(Other.FlushOne(1) < 0 && TransportValue.Updates == Before && Other.HasWork, "byte budget stops cleanly and retains newest dirty work");
        Other.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(!Other.HasWork && TransportValue.LastPayload.IndexOf("\"update\":true", StringComparison.Ordinal) >= 0 &&
            TransportValue.LastPayload.IndexOf("RectTransform", StringComparison.Ordinal) < 0 &&
            TransportValue.LastPayload.IndexOf("destroyUi", StringComparison.Ordinal) < 0,
            "Rust CUI emits a text-only update=true patch without unrelated layout or destroy fields");
        Gui.Dispose(); Other.Dispose();
    }

    private static void RunOrderingAndPayloadBounds()
    {
        Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate(); var PlayersValue = new Players(); Runtime.PlayerLifetime Player = PlayersValue.Add(30);
        var Backend = new Runtime.InMemoryGuiBackend(); var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, Backend), 34, 35);
        ulong Screen = Create(Gui, "ScreenGui"), Label = Create(Gui, "TextLabel", Screen);
        Set(Gui, Label, "Text", "string", "newest-before-show"); Show(Gui, Screen, Player); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls().Length == 1 && Backend.Calls()[0].Kind == Runtime.GuiBackendOperationKind.Replace &&
            Backend.Calls()[0].Plan.Describe().IndexOf("newest-before-show", StringComparison.Ordinal) >= 0,
            "mutation before initial Show is folded into one newest-state full replacement");
        Hide(Gui, Screen, Player); Show(Gui, Screen, Player); Drain(Gui, Limits);
        Runtime.InMemoryGuiBackend.Call[] HideShow = Backend.Calls();
        Check(HideShow.Length == 3 && HideShow[1].Kind == Runtime.GuiBackendOperationKind.Destroy && HideShow[2].Kind == Runtime.GuiBackendOperationKind.Replace,
            "Hide then Show retires the old epoch before a fresh full replacement");
        ulong NeverSent = Create(Gui, "ScreenGui"); Show(Gui, NeverSent, Player); Hide(Gui, NeverSent, Player);
        Check(!Gui.HasWork && Backend.Calls().Length == 3, "Show then Hide before flush sends neither obsolete AddUI nor unnecessary DestroyUI");
        Gui.Dispose();

        var BoundConfig = new Runtime.GuiConfig {MaxSerializedOperationBytes = 1024}; Runtime.GuiLimits BoundLimits = BoundConfig.Validate();
        var BoundTransport = new Transport(); var BoundGui = new Runtime.GuiRetainedRegistry(
            new Runtime.GuiRetainedWorld(BoundLimits, PlayersValue.Directory, new Runtime.RustCuiBackend(BoundLimits, BoundTransport)), 36, 37);
        ulong BoundScreen = Create(BoundGui, "ScreenGui"), BoundLabel = Create(BoundGui, "TextLabel", BoundScreen);
        Show(BoundGui, BoundScreen, Player); Drain(BoundGui, BoundLimits); Check(BoundTransport.Replaces == 1, "bounded Rust fixture starts coherent");
        BoundTransport.Reset(); Set(BoundGui, BoundLabel, "Text", "string", new string('X', 2048));
        BoundGui.FlushOne(BoundLimits.MaxSerializedBytesPerFlush);
        Check(!BoundGui.HasWork && BoundTransport.Updates == 0 && BoundTransport.Replaces == 0,
            "oversized Rust patch is promoted to full, then the oversized full projection is revision-blocked without retrying or sending");
        Set(BoundGui, BoundLabel, "Text", "string", "recovered"); BoundGui.FlushOne(BoundLimits.MaxSerializedBytesPerFlush);
        Check(!BoundGui.HasWork && BoundTransport.Updates == 1 && BoundTransport.LastPayload.IndexOf("recovered", StringComparison.Ordinal) >= 0,
            "a newer bounded retained revision recovers projection without historical payloads");
        BoundGui.Dispose();
    }

    private static void RunPublicationAndLifetime()
    {
        Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate(); var PlayersValue = new Players(); Runtime.PlayerLifetime Player = PlayersValue.Add(4);
        var Backend = new Runtime.InMemoryGuiBackend(); var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, Backend), 40, 41);
        ulong Screen = Create(Gui, "ScreenGui"), Label = Create(Gui, "TextLabel", Screen); Show(Gui, Screen, Player); Drain(Gui, Limits); int Calls = Backend.Calls().Length;
        Gui.BeginPublication(); Set(Gui, Label, "Text", "string", "rollback"); Check(Gui.FlushOne(Limits.MaxSerializedBytesPerFlush) == 0, "publication prevents recursive/precommit flush");
        Gui.RollbackPublication(); Check(!Gui.HasWork && Backend.Calls().Length == Calls, "provisional mutation rollback leaves zero GUI send");
        Gui.BeginPublication(); Set(Gui, Label, "Text", "string", "commit"); Gui.CommitPublication(); Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
        Check(Backend.Calls().Length == Calls + 1 && Backend.Calls()[Calls].Kind == Runtime.GuiBackendOperationKind.Update, "publication commit synchronizes later");
        Set(Gui, Label, "Text", "string", "pending"); Runtime.PlayerLifetime Removed = PlayersValue.Remove(Player); Gui.Disconnect(Removed);
        Check(!Gui.HasWork && Gui.PresentationCount == 0, "Player disconnect removes pending dirty presentation work");
        Player = PlayersValue.Add(5); Show(Gui, Screen, Player); Drain(Gui, Limits); Set(Gui, Label, "Text", "string", "teardown"); Gui.Dispose();
        Check(!Gui.HasWork, "domain teardown clears pending dirty work");
    }

    private static void RunFairness()
    {
        var PlayersValue = new Players(); var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(PlayersValue.Directory, new Registrar(), Backend);
        var First = new Runtime.FacadeSession(World, 50, 51, 256); var Second = new Runtime.FacadeSession(World, 50, 52, 256); World.Commit(First); World.CommitAddon(null, Second);
        ulong FirstScreen = Create(First.Gui, "ScreenGui"), FirstLabel = Create(First.Gui, "TextLabel", FirstScreen);
        var FirstPlayers = new List<Runtime.PlayerLifetime>();
        for (int Index = 0; Index < 100; ++Index) { Runtime.PlayerLifetime Player = PlayersValue.Add(100 + Index); FirstPlayers.Add(Player); Show(First.Gui, FirstScreen, Player); }
        ulong SecondScreen = Create(Second.Gui, "ScreenGui"), SecondLabel = Create(Second.Gui, "TextLabel", SecondScreen); Runtime.PlayerLifetime Other = PlayersValue.Add(500); Show(Second.Gui, SecondScreen, Other);
        Drain(First.Gui, World.Gui.Limits); Drain(Second.Gui, World.Gui.Limits);
        World.FlushGui(Stopwatch.StartNew(), 100); int Calls = Backend.Calls().Length;
        Set(First.Gui, FirstLabel, "Text", "string", "saturated"); Set(Second.Gui, SecondLabel, "Text", "string", "unrelated");
        var Watch = Stopwatch.StartNew(); World.FlushGui(Watch, 100); Runtime.InMemoryGuiBackend.Call[] Values = Backend.Calls(); bool SecondProgress = false;
        for (int Index = Calls; Index < Values.Length; ++Index)
            if (Values[Index].Target.ExactPlayerConnectionToken == Other.Token) SecondProgress = true;
        Check(SecondProgress && Second.Gui.HasWork == false && First.Gui.HasWork,
            "persistent domain/presentation fairness advances unrelated work under 100-viewer saturation (secondProgress=" + SecondProgress +
            ", secondHasWork=" + Second.Gui.HasWork + ", firstHasWork=" + First.Gui.HasWork + ", newCalls=" + (Values.Length - Calls) + ")");
        int Cycles = 1; while (World.HasWork && Cycles++ < 32) World.FlushGui(Stopwatch.StartNew(), 100);
        Check(!World.HasWork, "shared bounded flush backlog converges across domains"); World.Retire(First); World.Retire(Second);

        var SamePlayers = new Players(); var SameBackend = new Runtime.InMemoryGuiBackend(); var SameWorld = new Runtime.FacadeWorld(SamePlayers.Directory, new Registrar(), SameBackend);
        var Same = new Runtime.FacadeSession(SameWorld, 53, 54, 256); SameWorld.Commit(Same);
        ulong BusyScreen = Create(Same.Gui, "ScreenGui"), BusyLabel = Create(Same.Gui, "TextLabel", BusyScreen);
        for (int Index = 0; Index < 100; ++Index) Show(Same.Gui, BusyScreen, SamePlayers.Add(600 + Index));
        ulong UnrelatedScreen = Create(Same.Gui, "ScreenGui"), UnrelatedLabel = Create(Same.Gui, "TextLabel", UnrelatedScreen);
        Runtime.PlayerLifetime UnrelatedPlayer = SamePlayers.Add(800); Show(Same.Gui, UnrelatedScreen, UnrelatedPlayer); Drain(Same.Gui, SameWorld.Gui.Limits);
        SameWorld.FlushGui(Stopwatch.StartNew(), 100); int SameCalls = SameBackend.Calls().Length;
        Set(Same.Gui, BusyLabel, "Text", "string", "busy"); Set(Same.Gui, UnrelatedLabel, "Text", "string", "unrelated");
        bool SameProgress = false; int ServiceCycles = 0;
        while (!SameProgress && ServiceCycles < 101) {
            ServiceCycles++;
            SameWorld.FlushGui(Stopwatch.StartNew(), 100); Runtime.InMemoryGuiBackend.Call[] SameValues = SameBackend.Calls();
            for (int Index = SameCalls; Index < SameValues.Length; ++Index)
                if (SameValues[Index].Target.ExactPlayerConnectionToken == UnrelatedPlayer.Token) SameProgress = true;
            SameCalls = SameValues.Length;
        }
        Check(SameProgress, "persistent presentation fairness services an unrelated ScreenGui within one pass across eligible presentations");
        Console.WriteLine("[CarbonLuau:GuiFoundation1D] FAIR presentations=101 unrelatedServiceCycles=" + ServiceCycles);
        while (SameWorld.HasWork) SameWorld.FlushGui(Stopwatch.StartNew(), 100); SameWorld.Retire(Same);

        var SlowPlayers = new Players(); var SlowBackendValue = new SlowBackend(); var SlowWorld = new Runtime.FacadeWorld(SlowPlayers.Directory, new Registrar(), SlowBackendValue);
        var Slow = new Runtime.FacadeSession(SlowWorld, 55, 56, 256); SlowWorld.Commit(Slow);
        ulong SlowScreen = Create(Slow.Gui, "ScreenGui");
        for (int Index = 0; Index < 3; ++Index) Show(Slow.Gui, SlowScreen, SlowPlayers.Add(900 + Index));
        SlowWorld.FlushGui(Stopwatch.StartNew(), 100);
        Check(SlowBackendValue.Calls == 1 && SlowWorld.HasWork, "CPU budget stops after the in-flight send and preserves remaining work");
        while (SlowWorld.HasWork) SlowWorld.FlushGui(Stopwatch.StartNew(), 100);
        Check(SlowBackendValue.Calls == 3, "CPU-budget backlog later converges without duplicate sends"); SlowWorld.Retire(Slow);
    }

    private static void RunPerformance()
    {
        foreach (int Viewers in new[] {1, 10, 50, 100, 256}) {
            Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate(); var PlayersValue = new Players(); var TransportValue = new Transport();
            var Gui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits, PlayersValue.Directory, new Runtime.RustCuiBackend(Limits, TransportValue)), 60, (ulong)(1000 + Viewers));
            ulong Screen = Create(Gui, "ScreenGui"), Label = Create(Gui, "TextLabel", Screen);
            for (int Index = 0; Index < 48; ++Index) Create(Gui, "Frame", Screen);
            var ViewerValues = new List<Runtime.PlayerLifetime>();
            for (int Index = 0; Index < Viewers; ++Index) ViewerValues.Add(PlayersValue.Add(1000 + Viewers * 1000 + Index));
            long MemoryBefore = GC.GetTotalMemory(true);
            foreach (Runtime.PlayerLifetime Viewer in ViewerValues) Show(Gui, Screen, Viewer);
            var InitialWatch = Stopwatch.StartNew(); int InitialFlushes = 0;
            while (Gui.HasWork && InitialFlushes++ < 4096) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); InitialWatch.Stop(); Check(!Gui.HasWork, "large initial presentation converged");
            long PresentationMemory = Math.Max(0, GC.GetTotalMemory(true) - MemoryBefore); long InitialBytes = TransportValue.Bytes; TransportValue.Reset();
            var PatchWatch = Stopwatch.StartNew(); Set(Gui, Label, "Text", "string", "viewer-scale-" + Viewers); int PatchFlushes = 0;
            while (Gui.HasWork && PatchFlushes++ < 4096) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); PatchWatch.Stop();
            long TextBytes = TransportValue.Bytes; int TextSends = TransportValue.Updates; TransportValue.Reset();
            var PositionWatch = Stopwatch.StartNew(); Set(Gui, Label, "Position", "udim2", ".5", "-10", ".25", "20"); int PositionFlushes = 0;
            while (Gui.HasWork && PositionFlushes++ < 4096) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); PositionWatch.Stop();
            long PositionBytes = TransportValue.Bytes; TransportValue.Reset();
            var StructuralWatch = Stopwatch.StartNew(); Create(Gui, "Frame", Screen); int StructuralFlushes = 0;
            while (Gui.HasWork && StructuralFlushes++ < 4096) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); StructuralWatch.Stop();
            long StructuralBytes = TransportValue.Bytes;
            Check(TextSends == Viewers && !Gui.HasWork, "viewer-scale newest-state synchronization count");
            Console.WriteLine("[CarbonLuau:GuiFoundation1D] SCALE viewers=" + Viewers + " objects=50 presentationBytes=" + PresentationMemory +
                " initialMs=" + InitialWatch.Elapsed.TotalMilliseconds.ToString("F3") + " textMs=" + PatchWatch.Elapsed.TotalMilliseconds.ToString("F3") +
                " positionMs=" + PositionWatch.Elapsed.TotalMilliseconds.ToString("F3") +
                " structuralMs=" + StructuralWatch.Elapsed.TotalMilliseconds.ToString("F3") + " initialBytes=" + InitialBytes + " textBytes=" + TextBytes +
                " positionBytes=" + PositionBytes + " structuralBytes=" + StructuralBytes +
                " textSends=" + TextSends + " flushes=" + InitialFlushes + "/" + PatchFlushes + "/" + PositionFlushes + "/" + StructuralFlushes);
            Gui.Dispose();
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var PlayersValue = new Players(); PlayersValue.Add(9000); var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(PlayersValue.Directory, new Registrar(), Backend);
        string Source = "local State=require('state'); local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); local L=S:Create('TextLabel'); L.Text='initial'; S:Show(P); State.L=L";
        Func<Runtime.ScriptSnapshot> Snapshot = () => { var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source}; Value.Modules.Add("state", "return {}"); return Value; };
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Backend.Calls().Length == 0, "native candidate creates no precommit client work"); Host.Drain();
            Runtime.ExecutionResult Mutation = Host.Execute("gui1d.mutate", "local S=require('state'); S.L.Text='A'; S.L.Text='B'");
            Check(Mutation.Status == Runtime.RuntimeStatus.OK && Backend.Calls().Length == 1, "native mutation does not send during Luau entry"); Host.Drain();
            Runtime.InMemoryGuiBackend.Call Last = Backend.Calls()[Backend.Calls().Length - 1];
            Check(Last.Kind == Runtime.GuiBackendOperationKind.Update && Last.Patch.Describe().IndexOf("1:B", StringComparison.Ordinal) >= 0,
                "post-Luau GUI phase synchronizes only newest native retained state");
        }
        Console.WriteLine("[CarbonLuau:GuiFoundation1DNative] PASS committed dirty mutation flushes after Luau return without recursive VM entry");
    }
}
