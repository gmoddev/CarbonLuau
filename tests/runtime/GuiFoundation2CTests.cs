using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation2CTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture : IDisposable
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.FacadeSession Session;
        private ulong Registration;

        internal Fixture(Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.FacadeWorld(Players, new Registrar(), Limits, Backend);
            Session = new Runtime.FacadeSession(World, 37, 47, 256); World.Commit(Session);
        }
        internal Runtime.PlayerLifetime Add(int Index)
        {
            string Id = (76561190000700000L + Index).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id, Name = "Scroll" + Index,
                Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Players.Connect(View);
        }
        internal Runtime.PlayerLifetime Reconnect(Runtime.PlayerLifetime Previous)
        {
            Runtime.PlayerView Old = Views[Previous.UserId]; Players.Disconnect(Previous.UserId, Old.Identity); Views.Remove(Previous.UserId);
            World.DisconnectGui(Previous); return Add(Int32.Parse(Previous.UserId.Substring(Previous.UserId.Length - 2)));
        }
        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Session.Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Next)[0]); }
        internal void Set(ulong ObjectId, string Property, params string[] Value)
        { var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value); Session.Gui.Mutate(Fields.ToArray(), Next); }
        internal string[] Get(ulong ObjectId, string Property) { return Session.Gui.Query(new[] {"get", ObjectId.ToString(), Property}); }
        internal void Show(ulong Screen, Runtime.PlayerLifetime Player)
        { Session.Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Next); }
        internal void Hide(ulong Screen, Runtime.PlayerLifetime Player)
        { Session.Gui.Mutate(new[] {"hide", Screen.ToString(), Player.Token, Player.UserId}, Next); }
        internal Runtime.InMemoryGuiBackend.Call Flush()
        {
            int Before = Backend.Calls().Length; int Guard = 128;
            while (Session.Gui.HasWork && Guard-- != 0) Session.Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
            Check(!Session.Gui.HasWork && Backend.Calls().Length > Before, "flush converged with output");
            Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls(); return Calls[Calls.Length - 1];
        }
        internal string Next() { return (++Registration).ToString(); }
        public void Dispose() { Session.Gui.Dispose(); }
    }

    private sealed class Bounds
    {
        internal readonly double Left, Top, Width, Height;
        internal Bounds(double Left, double Top, double Width, double Height)
        { this.Left = Left; this.Top = Top; this.Width = Width; this.Height = Height; }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 2C: " + Message); }
    private static void Reject(Action Action, string Message)
    { bool Rejected = false; try { Action(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static bool Near(double Left, double Right) { return Math.Abs(Left - Right) < 0.000000001; }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; return null; }
    private static Runtime.GuiRenderElement Element(Runtime.GuiRenderElement[] Elements, ulong ObjectId)
    {
        string Suffix = "o" + ObjectId.ToString("x");
        foreach (Runtime.GuiRenderElement Value in Elements) if (Value.ClientId.EndsWith(Suffix, StringComparison.Ordinal)) return Value;
        throw new Exception("missing rendered object " + ObjectId);
    }
    private static Bounds Rect(Runtime.GuiRenderElement Element, double ParentWidth, double ParentHeight)
    {
        Runtime.GuiRenderVector2 AnchorMin = Property(Element, Runtime.GuiRenderPropertyId.AnchorMin).Vector;
        Runtime.GuiRenderVector2 AnchorMax = Property(Element, Runtime.GuiRenderPropertyId.AnchorMax).Vector;
        Runtime.GuiRenderVector2 Min = Property(Element, Runtime.GuiRenderPropertyId.OffsetMin).Vector;
        Runtime.GuiRenderVector2 Max = Property(Element, Runtime.GuiRenderPropertyId.OffsetMax).Vector;
        double Left = AnchorMin.X * ParentWidth + Min.X, Right = AnchorMax.X * ParentWidth + Max.X;
        double Bottom = AnchorMin.Y * ParentHeight + Min.Y, TopEdge = AnchorMax.Y * ParentHeight + Max.Y;
        return new Bounds(Left, ParentHeight - TopEdge, Right - Left, TopEdge - Bottom);
    }

    internal static void RunModel()
    {
        RunSchemaAndLifecycle();
        RunProjectionAndLayout();
        RunSynchronizationAndPresentations();
        RunPublicationFailureAndBounds();
        RunPerformance();
        Console.WriteLine("[CarbonLuau:GuiFoundation2CModel] PASS retained scrolling, private content projection, layout, publication and bounds");
    }

    private static void RunSchemaAndLifecycle()
    {
        Runtime.GuiSchema.Validate(); Runtime.GuiClassDescriptor Descriptor = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.ScrollingFrame);
        Check(Descriptor.Public && Descriptor.BaseClass == Runtime.GuiClassId.GuiObject && Descriptor.CanHaveChildren && Descriptor.Events.Length == 0,
            "ScrollingFrame is a public non-interactive GuiObject container");
        using (var Value = new Fixture()) {
            ulong Scroll = Value.Create("ScrollingFrame");
            Check(String.Join("|", Value.Get(Scroll, "CanvasSize")) == "udim2|1|0|1|0" &&
                Value.Get(Scroll, "ScrollingDirection")[1] == "Y" && Value.Get(Scroll, "ScrollingEnabled")[1] == "1",
                "canonical scrolling defaults");
            Value.Set(Scroll, "CanvasSize", "udim2", "2", "10", "3", "20");
            Value.Set(Scroll, "ScrollingDirection", "string", "X"); Value.Set(Scroll, "ScrollingEnabled", "boolean", "0");
            Check(String.Join("|", Value.Get(Scroll, "CanvasSize")) == "udim2|2|10|3|20" &&
                Value.Get(Scroll, "ScrollingDirection")[1] == "X" && Value.Get(Scroll, "ScrollingEnabled")[1] == "0",
                "retained CanvasSize, direction and enabled readback");
            Value.Set(Scroll, "ScrollingDirection", "string", "Y"); Value.Set(Scroll, "ScrollingDirection", "string", "XY");
            Reject(() => Value.Set(Scroll, "ScrollingDirection", "string", "Vertical"), "unknown direction rejected");
            Reject(() => Value.Set(Scroll, "ScrollingDirection", "integer", "1"), "direction type enforced");
            Reject(() => Value.Set(Scroll, "ScrollingEnabled", "string", "true"), "enabled type enforced");
            Reject(() => Value.Set(Scroll, "CanvasSize", "udim2", "9", "0", "1", "0"), "CanvasSize uses bounded UDim2 validation");
            Reject(() => Value.Get(Scroll, "CanvasPosition"), "CanvasPosition is not retained or exposed");
            Reject(() => Value.Get(Scroll, "AutomaticCanvasSize"), "AutomaticCanvasSize is absent");
            ulong Layout = Value.Create("UIListLayout", Scroll), Padding = Value.Create("UIPadding", Scroll), Child = Value.Create("Frame", Scroll);
            ulong Clone = UInt64.Parse(Value.Session.Gui.Mutate(new[] {"clone", Scroll.ToString()}, Value.Next)[0]);
            Check(String.Join("|", Value.Get(Clone, "CanvasSize")) == "udim2|2|10|3|20" &&
                Value.Get(Clone, "ScrollingDirection")[1] == "XY" && Value.Get(Clone, "ScrollingEnabled")[1] == "0" &&
                Value.Session.Gui.Query(new[] {"children", Clone.ToString()}).Length == 6,
                "Clone copies retained scrolling state, helpers and children");
            Value.Session.Gui.Mutate(new[] {"destroy", Scroll.ToString()}, Value.Next);
            Reject(() => Value.Get(Scroll, "CanvasSize"), "Destroy stales ScrollingFrame reference");
            Reject(() => Value.Get(Layout, "Padding"), "Destroy recursively stales layout helper");
            Reject(() => Value.Get(Padding, "PaddingTop"), "Destroy recursively stales padding helper");
            Reject(() => Value.Get(Child, "Name"), "Destroy recursively stales child");
        }
    }

    private static void RunProjectionAndLayout()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.Add(1), B = Value.Add(2); ulong Screen = Value.Create("ScreenGui");
            ulong Scroll = Value.Create("ScrollingFrame", Screen); Value.Set(Scroll, "Size", "udim2", "0", "200", "0", "100");
            Value.Set(Scroll, "CanvasSize", "udim2", "1", "0", "0", "600"); Value.Set(Scroll, "ScrollingDirection", "string", "Y");
            ulong First = Value.Create("Frame", Scroll), Hidden = Value.Create("Frame", Scroll), Nested = Value.Create("Frame", Scroll);
            Value.Set(First, "Size", "udim2", "0", "40", "0", "30"); Value.Set(Hidden, "Size", "udim2", "0", "40", "0", "50");
            Value.Set(Hidden, "Visible", "boolean", "0"); Value.Set(Nested, "Size", "udim2", "0", "60", "0", "40");
            ulong Image = Value.Create("ImageLabel", Nested), Button = Value.Create("ImageButton", Nested);
            Value.Set(Image, "Image", "imagesource", "Png", "42"); Value.Set(Button, "Image", "imagesource", "Sprite", "assets/icons/down.png");
            ulong Padding = Value.Create("UIPadding", Scroll); Value.Set(Padding, "PaddingTop", "udim", "0", "10");
            Value.Set(Padding, "PaddingLeft", "udim", "0", "8");
            ulong Layout = Value.Create("UIListLayout", Scroll); Value.Set(Layout, "Padding", "udim", "0", "5");
            Value.Show(Screen, A); Value.Show(Screen, B); Value.Flush();
            Runtime.InMemoryGuiBackend.Call[] Calls = Value.Backend.Calls(); Runtime.GuiRenderPlan APlan = null, BPlan = null;
            foreach (Runtime.InMemoryGuiBackend.Call Call in Calls) if (Call.Plan != null) {
                if (Call.Target.ExactPlayerConnectionToken == A.Token) APlan = Call.Plan;
                if (Call.Target.ExactPlayerConnectionToken == B.Token) BPlan = Call.Plan;
            }
            Check(APlan != null && BPlan != null && APlan.ProjectedElementCount == BPlan.ProjectedElementCount,
                "shared retained scrolling tree compiles to independent Presentations");
            Runtime.GuiRenderElement ScrollElement = Element(APlan.Elements, Scroll);
            Check(ScrollElement.Kind == Runtime.GuiRenderNodeKind.ScrollView && ScrollElement.ProjectedElementCost == 7 &&
                ScrollElement.PrivateChildRootId == ScrollElement.ClientId + "___Content" && ScrollElement.PrivateChildRootId.Length <= 256,
                "bounded private ScrollView content identity and worst-case projection cost");
            Check(Property(ScrollElement, Runtime.GuiRenderPropertyId.ScrollHorizontal).Boolean == false &&
                Property(ScrollElement, Runtime.GuiRenderPropertyId.ScrollVertical).Boolean &&
                Property(ScrollElement, Runtime.GuiRenderPropertyId.ScrollEnabled).Boolean,
                "Y direction and enabled state remain typed in the backend-neutral plan");
            Check(Element(APlan.Elements, First).ParentClientId == ScrollElement.PrivateChildRootId &&
                Element(APlan.Elements, Nested).ParentClientId == ScrollElement.PrivateChildRootId &&
                Element(APlan.Elements, Image).ParentClientId == Element(APlan.Elements, Nested).ClientId,
                "direct children use private content root while nested children retain ordinary parentage");
            Bounds FirstRect = Rect(Element(APlan.Elements, First), 200, 600), NestedRect = Rect(Element(APlan.Elements, Nested), 200, 600);
            Check(Near(FirstRect.Left, 8) && Near(FirstRect.Top, 10) && Near(NestedRect.Top, 45),
                "UIListLayout and UIPadding compile in explicit CanvasSize content space and hidden child consumes no list space");
            string Json = Runtime.RustCuiBackend.Serialize(APlan.Elements, false, true);
            Check(Json.Contains("\"type\":\"UnityEngine.UI.ScrollView\"") && Json.Contains("\"contentTransform\"") &&
                Json.Contains("\"anchormin\":\"0 1\"") && Json.Contains("\"offsetmin\":\"0 -600\"") &&
                Json.Contains("\"horizontal\":false") && Json.Contains("\"vertical\":true") &&
                Json.Contains("\"movementType\":\"Clamped\"") && Json.Contains("\"verticalScrollbar\"") &&
                Json.Contains("\"parent\":\"" + ScrollElement.PrivateChildRootId + "\"") &&
                !Json.Contains("horizontalNormalizedPosition") && !Json.Contains("verticalNormalizedPosition") && !Json.Contains("CanvasPosition"),
                "Rust CUI ScrollView mapping is exact and emits no retained client offset");
        }
    }

    private static void RunSynchronizationAndPresentations()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.Add(3), B = Value.Add(4); ulong Screen = Value.Create("ScreenGui");
            ulong Scroll = Value.Create("ScrollingFrame", Screen); Value.Create("Frame", Scroll); Value.Show(Screen, A); Value.Show(Screen, B); Value.Flush();
            Runtime.GuiRenderElement Root = Value.Backend.Calls()[0].Plan.Elements[0];
            Check(Property(Root, Runtime.GuiRenderPropertyId.NeedsCursor).Boolean,
                "enabled visible ScrollingFrame requests the existing bounded cursor projection");
            int Calls = Value.Backend.Calls().Length;
            Value.Set(Scroll, "BackgroundColor3", "color3", "0.2", "0.3", "0.4"); Value.Flush();
            Runtime.InMemoryGuiBackend.Call Patch = Value.Backend.Calls()[Value.Backend.Calls().Length - 1];
            Check(Value.Backend.Calls().Length == Calls + 2 && Patch.Kind == Runtime.GuiBackendOperationKind.Update &&
                !Runtime.RustCuiBackend.Serialize(Patch.Patch.Elements, true, false).Contains("UnityEngine.UI.ScrollView"),
                "inherited patchable property updates do not rewrite Presentation-local scroll state");
            Value.Set(Scroll, "CanvasSize", "udim2", "1", "0", "0", "800"); Value.Flush();
            Check(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Replace,
                "CanvasSize uses structural reconciliation");
            Value.Set(Scroll, "ScrollingDirection", "string", "X"); Value.Flush();
            string XJson = Runtime.RustCuiBackend.Serialize(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Plan.Elements, false, true);
            Check(XJson.Contains("\"horizontal\":true") && XJson.Contains("\"vertical\":false") && XJson.Contains("\"horizontalScrollbar\""),
                "X direction maps deterministically");
            Value.Set(Scroll, "ScrollingDirection", "string", "XY"); Value.Flush();
            string XYJson = Runtime.RustCuiBackend.Serialize(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Plan.Elements, false, true);
            Check(XYJson.Contains("\"horizontal\":true") && XYJson.Contains("\"vertical\":true"), "XY direction maps deterministically");
            Value.Set(Scroll, "ScrollingEnabled", "boolean", "0"); Value.Flush();
            Runtime.GuiRenderPlan DisabledPlan = Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Plan;
            string Disabled = Runtime.RustCuiBackend.Serialize(DisabledPlan.Elements, false, true);
            Check(Disabled.Contains("\"enabled\":false") && !Disabled.Contains("NormalizedPosition") &&
                !Property(DisabledPlan.Elements[0], Runtime.GuiRenderPropertyId.NeedsCursor).Boolean,
                "disabled ScrollView and scrollbars expose no fabricated offset");
            string OldRoot = Value.Backend.Calls()[0].Target.ClientRootId; Value.Hide(Screen, A); Value.Flush(); Value.Show(Screen, A); Value.Flush();
            string NewRoot = Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Target.ClientRootId;
            Check(OldRoot != NewRoot, "Hide/Show creates a fresh Presentation and may reset client-local scroll state");
            Runtime.PlayerLifetime Reconnected = Value.Reconnect(B); Value.Show(Screen, Reconnected); Value.Flush();
            Check(Reconnected.Token != B.Token, "disconnect/reconnect creates independent fresh scroll state authority");
        }
    }

    private static void RunPublicationFailureAndBounds()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.Add(5);
            Value.Session.Gui.BeginPublication(); ulong Screen = Value.Create("ScreenGui"), Scroll = Value.Create("ScrollingFrame", Screen);
            Value.Set(Scroll, "CanvasSize", "udim2", "1", "0", "0", "900"); Value.Set(Scroll, "ScrollingEnabled", "boolean", "0");
            Value.Create("Frame", Scroll); Value.Show(Screen, Player);
            Check(Value.Session.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush) == 0,
                "provisional scrolling state cannot synchronize"); Value.Session.Gui.RollbackPublication();
            Check(Value.Backend.Calls().Length == 0, "publication rollback leaks no ScrollView projection");
            Value.Session.Gui.BeginPublication(); Screen = Value.Create("ScreenGui"); Scroll = Value.Create("ScrollingFrame", Screen);
            Value.Set(Scroll, "CanvasSize", "udim2", "1", "0", "0", "900"); Value.Show(Screen, Player); Value.Session.Gui.CommitPublication();
            Value.Flush(); Check(Value.Backend.Calls().Length == 1, "publication commit makes one retained scrolling tree eligible for flush");
            Value.Session.Gui.BeginPublication(); Value.Set(Scroll, "ScrollingDirection", "string", "X"); Value.Session.Gui.RollbackPublication();
            Check(Value.Get(Scroll, "ScrollingDirection")[1] == "Y" && !Value.Session.Gui.HasWork,
                "foreign-owner style provisional mutation rollback preserves committed retained state");
            Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "scroll replace failure");
            Value.Set(Scroll, "ScrollingEnabled", "boolean", "0"); Value.Session.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Value.Session.Gui.FullResyncPresentationCount == 1, "failed ScrollView Replace leaves presentation needing full resync");
            Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Replace && Value.Get(Scroll, "ScrollingEnabled")[1] == "0",
                "later rebuild converges newest retained scrolling state");
        }
        var Tight = new Runtime.GuiConfig {MaxProjectedElementsPerScreen = 9};
        using (var Value = new Fixture(Tight)) {
            ulong Screen = Value.Create("ScreenGui"), Scroll = Value.Create("ScrollingFrame", Screen); Value.Create("Frame", Scroll);
            int Before = Value.Session.Gui.Query(new[] {"children", Scroll.ToString()}).Length;
            Reject(() => Value.Create("Frame", Scroll), "private ScrollView projection overflow rejected");
            Check(Value.Session.Gui.Query(new[] {"children", Scroll.ToString()}).Length == Before,
                "projection overflow rejection is atomic");
        }
        using (var Value = new Fixture()) {
            ulong Scroll = Value.Create("ScrollingFrame");
            for (int Index = 0; Index < 64; ++Index) Value.Create("Frame", Scroll);
            Reject(() => Value.Create("Frame", Scroll), "large direct-child boundary remains enforced");
        }
    }

    private static void RunPerformance()
    {
        foreach (int Count in new[] {1, 10, 32, 64}) using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.Add(10 + Count); ulong Screen = Value.Create("ScreenGui"), Scroll = Value.Create("ScrollingFrame", Screen);
            Value.Set(Scroll, "Size", "udim2", "0", "400", "0", "300"); Value.Set(Scroll, "CanvasSize", "udim2", "1", "0", "0", "2400");
            int Direct = Count == 64 ? 62 : Count; ulong First = 0;
            for (int Index = 0; Index < Direct; ++Index) {
                ulong Child = Value.Create("Frame", Scroll); if (First == 0) First = Child;
                Value.Set(Child, "Size", "udim2", "1", "-16", "0", "28");
            }
            if (Count == 64) { Value.Create("ImageLabel", First); Value.Create("ImageButton", First); }
            ulong Padding = Value.Create("UIPadding", Scroll); Value.Set(Padding, "PaddingLeft", "udim", "0", "8");
            ulong Layout = Value.Create("UIListLayout", Scroll); Value.Set(Layout, "Padding", "udim", "0", "4");
            long BeforeMemory = GC.GetTotalMemory(true); Value.Show(Screen, Player); var Clock = Stopwatch.StartNew();
            Runtime.InMemoryGuiBackend.Call Full = Value.Flush(); Clock.Stop(); long FullTicks = Clock.ElapsedTicks;
            string Json = Runtime.RustCuiBackend.Serialize(Full.Plan.Elements, false, true); long PresentationBytes = Math.Max(0, GC.GetTotalMemory(false) - BeforeMemory);
            Value.Set(Scroll, "CanvasSize", "udim2", "1", "0", "0", (2400 + Count).ToString()); Clock.Restart();
            Runtime.InMemoryGuiBackend.Call Rebuild = Value.Flush(); Clock.Stop();
            Check(Rebuild.Kind == Runtime.GuiBackendOperationKind.Replace && Full.Plan.ProjectedElementCount <= Value.Limits.MaxProjectedElementsPerScreen &&
                Runtime.GuiRenderValue.Utf8Bytes(Json) <= Value.Limits.MaxSerializedOperationBytes,
                Count + "-child scrolling screen remains within projection and serialization bounds");
            Console.WriteLine("[CarbonLuau:GuiFoundation2CPerf] children=" + Count + " projected=" + Full.Plan.ProjectedElementCount +
                " estimatedBytes=" + Full.Plan.EstimatedSerializedBytes + " serializedBytes=" + Runtime.GuiRenderValue.Utf8Bytes(Json) +
                " fullTicks=" + FullTicks + " rebuildTicks=" + Clock.ElapsedTicks + " presentationBytes=" + PresentationBytes);
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        const string UserId = "76561190000799999";
        Runtime.PlayerView View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId, Name = "Native Scroll",
            Connected = true, Send = Value => { }, Permission = Value => true};
        var Players = new Runtime.PlayerDirectory(Id => Id == UserId ? View : null); Runtime.PlayerLifetime Player = Players.Connect(View);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Players, new Registrar(), Backend);
        string Source = "local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
            "local F=S:Create('ScrollingFrame'); assert(F.CanvasSize==UDim2.fromScale(1,1) and F.ScrollingDirection=='Y' and F.ScrollingEnabled); " +
            "F.CanvasSize=UDim2.new(1,0,0,800); F.ScrollingDirection='XY'; F.ScrollingEnabled=false; " +
            "local L=F:Create('UIListLayout'); local Pad=F:Create('UIPadding'); local I=F:Create('ImageLabel'); I.Image=ImageSource.Png('42'); " +
            "assert(not pcall(function() return F.CanvasPosition end)); assert(not pcall(function() F.ScrollingDirection='Vertical' end)); S:Show(P); return true";
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native public ScrollingFrame surface"); Host.Drain();
            Runtime.GuiRenderPlan Plan = Backend.Calls()[Backend.Calls().Length - 1].Plan; string Json = Runtime.RustCuiBackend.Serialize(Plan.Elements, false, true);
            Check(Json.Contains("UnityEngine.UI.ScrollView") && Json.Contains("___Content") && Json.Contains("\"enabled\":false") &&
                !Json.Contains("NormalizedPosition"), "native retained scrolling projection through real compiler and VM");
        }
        Check(World.Gui.LiveObjects == 0, "native scrolling teardown returns retained registry to baseline");
        RunForeignNative(Native);
        Console.WriteLine("[CarbonLuau:GuiFoundation2CNative] PASS public scrolling surface, private content projection and teardown");
    }

    private static void RunForeignNative(Runtime.NativeRuntime Native)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object Provider = new object();
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "foreign scrolling root baseline");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                byte[] Owner = Archive("{\"schema\":1,\"id\":\"scrollowner\",\"version\":\"1.0.0\",\"main\":\"api\"}",
                    "local A=require('api'); game:GetService('Commands'):Register('scrollcheck',{},function() print(A.Scroll.ScrollingDirection) end)",
                    "local G=game:GetService('Gui'); local S=G:Create('ScreenGui'); local F=S:Create('ScrollingFrame'); F.CanvasSize=UDim2.fromOffset(300,600); return {Scroll=F}");
                string[] OwnerRegistration = Registry.RegisterArchive(Provider, Owner); while (Registry.HasPending) Registry.ProcessOne();
                Check(Registry.Status(Provider, OwnerRegistration[1])[2] == "Active", "foreign scrolling owner active");
                byte[] Failed = Archive("{\"schema\":1,\"id\":\"scrollfail\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":[\"scrollowner\"],\"optional\":[]}}",
                    "local A=require('@scrollowner'); A.Scroll.CanvasSize=UDim2.fromOffset(500,900); A.Scroll.ScrollingDirection='X'; error('reject scrolling consumer')", null);
                string[] FailedRegistration = Registry.RegisterArchive(Provider, Failed); while (Registry.HasPending) Registry.ProcessOne();
                Check(Registry.Status(Provider, FailedRegistration[1])[2] == "Failed", "foreign scrolling consumer failure rejected");
                Runtime.FacadeSession OwnerSession = FindCommand(World, "scrollcheck"); ulong Scroll = FindScrollingFrame(OwnerSession);
                Check(String.Join("|", OwnerSession.Gui.Query(new[] {"get", Scroll.ToString(), "CanvasSize"})) == "udim2|0|300|0|600" &&
                    OwnerSession.Gui.Query(new[] {"get", Scroll.ToString(), "ScrollingDirection"})[1] == "Y",
                    "failed foreign candidate leaks no retained scrolling mutation");
                byte[] Success = Archive("{\"schema\":1,\"id\":\"scrollsuccess\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":[\"scrollowner\"],\"optional\":[]}}",
                    "local A=require('@scrollowner'); A.Scroll.CanvasSize=UDim2.fromOffset(500,900); A.Scroll.ScrollingDirection='XY'", null);
                string[] SuccessRegistration = Registry.RegisterArchive(Provider, Success); while (Registry.HasPending) Registry.ProcessOne();
                Check(Registry.Status(Provider, SuccessRegistration[1])[2] == "Active" &&
                    String.Join("|", OwnerSession.Gui.Query(new[] {"get", Scroll.ToString(), "CanvasSize"})) == "udim2|0|500|0|900" &&
                    OwnerSession.Gui.Query(new[] {"get", Scroll.ToString(), "ScrollingDirection"})[1] == "XY",
                    "successful foreign candidate publishes retained scrolling mutation atomically");
            }
        }
        Check(World.Gui.LiveObjects == 0, "foreign scrolling teardown returns retained registry to baseline");
    }

    private static Runtime.FacadeSession FindCommand(Runtime.FacadeWorld World, string Name)
    { foreach (Runtime.FacadeSession Session in World.Sessions()) if (Session.Commands.ContainsKey(Name)) return Session; throw new Exception("GUI Foundation 2C: command owner missing"); }
    private static ulong FindScrollingFrame(Runtime.FacadeSession Session)
    {
        for (ulong Id = 1; Id <= 8; ++Id) {
            Runtime.GuiClassId ClassId; var Identity = new Runtime.GuiObjectIdentity((ulong)Session.VmGenerationId, (ulong)Session.DomainLifetimeId, Id);
            if (Session.Gui.TryGetClass(Identity, out ClassId) && ClassId == Runtime.GuiClassId.ScrollingFrame) return Id;
        }
        throw new Exception("GUI Foundation 2C: owner ScrollingFrame missing");
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
