using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation2ATests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiRetainedWorld World;
        internal readonly Runtime.GuiRetainedRegistry Gui;
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.PlayerLifetime Player;
        internal ulong Registration;
        internal Fixture(Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Runtime.PlayerView View = null;
            Players = new Runtime.PlayerDirectory(Id => View != null && View.UserId == Id ? View : null);
            View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = "76561190000000200",
                Name = "Layout", Connected = true, Send = Value => { }, Permission = Value => true};
            Player = Players.Connect(View);
            World = new Runtime.GuiRetainedWorld(Limits, Players, Backend);
            Gui = new Runtime.GuiRetainedRegistry(World, 20, 40);
        }
        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Token)[0]); }
        internal void Set(ulong ObjectId, string Property, params string[] Value)
        { var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), Token); }
        internal string[] Query(params string[] Fields) { return Gui.Query(Fields); }
        internal void Show(ulong Screen)
        { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Token); }
        internal Runtime.InMemoryGuiBackend.Call Flush()
        {
            int Before = Backend.Calls().Length; int Guard = 0;
            while (Gui.HasWork && Guard++ < 16) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
            Check(!Gui.HasWork && Backend.Calls().Length > Before, "expected layout synchronization output");
            Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls(); return Calls[Calls.Length - 1];
        }
        internal string Token() { return (++Registration).ToString(); }
        public void Dispose() { Gui.Dispose(); }
    }

    private sealed class Bounds
    {
        internal readonly double Left, Top, Width, Height;
        internal Bounds(double Left, double Top, double Width, double Height)
        { this.Left = Left; this.Top = Top; this.Width = Width; this.Height = Height; }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 2A: " + Message); }
    private static void Reject(Action Action, string Message)
    { bool Rejected = false; try { Action(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static bool Near(double Left, double Right) { return Math.Abs(Left - Right) < 0.000000001; }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; throw new Exception("missing " + Id); }
    private static Runtime.GuiRenderElement Element(Runtime.GuiRenderElement[] Elements, ulong ObjectId)
    {
        string Suffix = "o" + ObjectId.ToString("x");
        foreach (Runtime.GuiRenderElement Element in Elements) if (Element.ClientId.EndsWith(Suffix, StringComparison.Ordinal)) return Element;
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
    private static Runtime.GuiPropertyUse Descriptor(Runtime.GuiClassId ClassId, Runtime.GuiPropertyId PropertyId)
    { foreach (Runtime.GuiPropertyUse Value in Runtime.GuiSchema.GetClass(ClassId).Properties) if (Value.Descriptor.Id == PropertyId) return Value; return null; }

    internal static void RunModel()
    {
        RunDescriptorsAndLifecycle();
        RunVerticalGolden();
        RunHorizontalAndAlignment();
        RunOrderingAndContent();
        RunDirtyAndPublication();
        RunBoundsAndPerformance();
        Console.WriteLine("[CarbonLuau:GuiFoundation2AModel] PASS deterministic retained layout, padding, synchronization, lifecycle and bounds");
    }

    private static void RunDescriptorsAndLifecycle()
    {
        Runtime.GuiSchema.Validate();
        Runtime.GuiClassDescriptor LayoutClass = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.UIListLayout);
        Runtime.GuiClassDescriptor PaddingClass = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.UIPadding);
        Check(LayoutClass.Public && PaddingClass.Public && LayoutClass.BaseClass == Runtime.GuiClassId.GuiNode && !LayoutClass.CanHaveChildren &&
            PaddingClass.BaseClass == Runtime.GuiClassId.GuiNode && !PaddingClass.CanHaveChildren, "helpers are public non-rendering GuiNode leaves");
        Runtime.GuiPropertyUse LayoutOrder = Descriptor(Runtime.GuiClassId.Frame, Runtime.GuiPropertyId.LayoutOrder);
        Check(LayoutOrder != null && LayoutOrder.DefaultValue == "0" && LayoutOrder.Descriptor.Minimum == -32768 &&
            LayoutOrder.Descriptor.Maximum == 32767, "LayoutOrder descriptor default and range");
        using (var Value = new Fixture()) {
            ulong Frame = Value.Create("Frame"); ulong ChildA = Value.Create("Frame", Frame); ulong ChildB = Value.Create("Frame", Frame);
            Check(Value.Query("get", ChildA.ToString(), "LayoutOrder")[1] == "0", "LayoutOrder retained default");
            Reject(() => Value.Set(ChildA, "LayoutOrder", "number", "1"), "LayoutOrder rejects non-integer wire type");
            Reject(() => Value.Set(ChildA, "LayoutOrder", "integer", "32768"), "LayoutOrder rejects high range");
            Reject(() => Value.Set(ChildA, "LayoutOrder", "integer", "-32769"), "LayoutOrder rejects low range");
            ulong Layout = Value.Create("UIListLayout", Frame); ulong Padding = Value.Create("UIPadding", Frame);
            Check(Value.Query("isa", Layout.ToString(), "GuiObject")[0] == "0" && Value.Query("isa", Layout.ToString(), "GuiNode")[0] == "1",
                "layout helper IsA boundary");
            Reject(() => Value.Create("UIListLayout", Layout), "layout helper cannot own children");
            int ChildrenBefore = Value.Query("children", Frame.ToString()).Length;
            Reject(() => Value.Create("UIListLayout", Frame), "duplicate UIListLayout rejected");
            Reject(() => Value.Create("UIPadding", Frame), "duplicate UIPadding rejected");
            Check(Value.Query("children", Frame.ToString()).Length == ChildrenBefore, "duplicate helper attempts are atomic");
            Reject(() => Value.Create("UIListLayout"), "helper requires a GuiObject parent");
            Value.Set(Padding, "PaddingLeft", "udim", "0.25", "12");
            Reject(() => Value.Set(Padding, "PaddingLeft", "udim", "-0.01", "0"), "UIPadding rejects negative scale");
            Reject(() => Value.Set(Padding, "PaddingLeft", "udim", "0", "-1"), "UIPadding rejects negative offset");
            ulong Other = Value.Create("Frame"); ulong OtherLayout = Value.Create("UIListLayout", Other);
            Reject(() => Value.Set(Layout, "Parent", "object", Other.ToString()), "same-domain duplicate helper reparent is atomic");
            Check(Value.Query("get", Layout.ToString(), "Parent")[1] == Frame.ToString(), "failed helper reparent retains old parent");
            Value.Gui.Mutate(new[] {"destroy", OtherLayout.ToString()}, Value.Token); Value.Set(Layout, "Parent", "object", Other.ToString());
            Check(Value.Query("get", Layout.ToString(), "Parent")[1] == Other.ToString(), "same-domain helper reparent succeeds after cardinality clears");
            Value.Set(Layout, "Parent", "object", Frame.ToString());
            ulong ScreenA = Value.Create("ScreenGui"), ScreenB = Value.Create("ScreenGui");
            ulong RootA = Value.Create("Frame", ScreenA), RootB = Value.Create("Frame", ScreenB);
            Value.Set(Layout, "Parent", "object", RootA.ToString()); Value.Set(Layout, "Parent", "object", RootB.ToString());
            Check(Value.Query("get", Layout.ToString(), "Parent")[1] == RootB.ToString(), "same-domain cross-root helper reparent");
            Value.Set(Layout, "Parent", "object", Frame.ToString());
            ulong Clone = UInt64.Parse(Value.Gui.Mutate(new[] {"clone", Frame.ToString()}, Value.Token)[0]);
            string[] CloneChildren = Value.Query("children", Clone.ToString());
            int Layouts = 0, Paddings = 0;
            for (int Index = 1; Index < CloneChildren.Length; Index += 2) { if (CloneChildren[Index] == "UIListLayout") Layouts++; if (CloneChildren[Index] == "UIPadding") Paddings++; }
            Check(Layouts == 1 && Paddings == 1, "Clone preserves helpers and cardinality");
            Value.Gui.Mutate(new[] {"destroy", Padding.ToString()}, Value.Token);
            Reject(() => Value.Query("get", Padding.ToString(), "PaddingLeft"), "Destroy retires helper reference");
            Check(Value.World.LiveObjects > 0, "layout objects participate in retained accounting");
        }
    }

    private static void RunVerticalGolden()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"); ulong Parent = Value.Create("Frame", Screen);
            Value.Set(Parent, "Size", "udim2", "1", "0", "1", "0");
            ulong B = Value.Create("Frame", Parent); ulong A = Value.Create("Frame", Parent);
            Value.Set(A, "Size", "udim2", "0", "10", "0", "20"); Value.Set(A, "AnchorPoint", "vector2", "0.5", "0.5");
            Value.Set(B, "Size", "udim2", "0", "30", "0", "40");
            Value.Set(A, "Position", "udim2", "0.75", "99", "0.5", "88");
            Value.Set(A, "LayoutOrder", "integer", "-1"); Value.Set(B, "LayoutOrder", "integer", "2");
            Value.Set(A, "ZIndex", "integer", "9"); Value.Set(B, "ZIndex", "integer", "1");
            ulong Padding = Value.Create("UIPadding", Parent); Value.Set(Padding, "PaddingLeft", "udim", "0", "10");
            Value.Set(Padding, "PaddingRight", "udim", "0", "20"); Value.Set(Padding, "PaddingTop", "udim", "0", "5");
            Value.Set(Padding, "PaddingBottom", "udim", "0", "7");
            ulong Layout = Value.Create("UIListLayout", Parent); Value.Set(Layout, "Padding", "udim", "0", "6");
            Value.Set(Layout, "HorizontalAlignment", "string", "Right"); Value.Set(Layout, "VerticalAlignment", "string", "Center");
            Value.Set(Layout, "Padding", "udim", "0.05", "1");
            Value.Show(Screen); Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements;
            Bounds AR = Rect(Element(Elements, A), 100, 100), BR = Rect(Element(Elements, B), 100, 100);
            Check(Near(AR.Left, 70) && Near(AR.Top, 16.3) && Near(AR.Width, 10) && Near(AR.Height, 20), "vertical affine alignment with AnchorPoint");
            Check(Near(BR.Left, 50) && Near(BR.Top, 41.7) && Near(BR.Width, 30) && Near(BR.Height, 40), "mixed Scale+Offset list gap and retained child Size");
            Check(Array.FindIndex(Elements, E => E.ClientId.EndsWith("o" + B.ToString("x"))) <
                Array.FindIndex(Elements, E => E.ClientId.EndsWith("o" + A.ToString("x"))), "ZIndex remains render order independent from LayoutOrder");
            string[] Attached = Value.Query("children", Parent.ToString());
            Check(Attached[0] == B.ToString() && Attached[2] == A.ToString(), "GetChildren remains attachment order");
            string[] Position = Value.Query("get", A.ToString(), "Position");
            Check(Position[1] == "0.75" && Position[2] == "99" && Position[3] == "0.5" && Position[4] == "88", "list projection does not rewrite retained Position");
            int RenderedObjects = 0; foreach (Runtime.GuiRenderElement Element in Elements) if (Element.Kind != Runtime.GuiRenderNodeKind.Text) RenderedObjects++;
            Check(RenderedObjects == 4, "layout helpers produce no client CUI elements");
            string Json = Runtime.RustCuiBackend.Serialize(Elements, false, true);
            Check(Json.Contains("RectTransform") && !Json.Contains("UIListLayout") && !Json.Contains("LayoutGroup") && !Json.Contains("UIPadding"),
                "Rust CUI receives ordinary rectangles without host layout components");
            Value.Gui.Mutate(new[] {"destroy", Layout.ToString()}, Value.Token); Runtime.GuiRenderElement[] Restored = Value.Flush().Plan.Elements;
            Bounds Authored = Rect(Element(Restored, A), 100, 100);
            Check(Near(Authored.Left, 10 + 0.75 * 70 + 99 - 5) && Near(Authored.Top, 5 + 0.5 * 88 + 88 - 10), "removing layout restores retained Position through padding");
        }
    }

    private static void RunOrderingAndContent()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen);
            Value.Set(Parent, "Size", "udim2", "0", "100", "0", "100");
            ulong First = Value.Create("Frame", Parent), Second = Value.Create("Frame", Parent);
            Value.Set(First, "Size", "udim2", "0", "10", "0", "10"); Value.Set(Second, "Size", "udim2", "0", "10", "0", "10");
            Value.Set(First, "LayoutOrder", "integer", "4"); Value.Set(Second, "LayoutOrder", "integer", "4");
            Value.Create("UIListLayout", Parent); Value.Show(Screen); Runtime.GuiRenderElement[] FirstPlan = Value.Flush().Plan.Elements;
            Check(Near(Rect(Element(FirstPlan, First), 100, 100).Top, 0) && Near(Rect(Element(FirstPlan, Second), 100, 100).Top, 10),
                "duplicate LayoutOrder ties use attachment order");
            Value.Set(First, "Name", "string", "metadata-only"); Check(!Value.Gui.HasWork, "metadata mutation does not perturb projection");
            Value.Set(First, "LayoutOrder", "integer", "5"); Value.Flush(); Value.Set(First, "LayoutOrder", "integer", "4"); Value.Flush();
            Value.Gui.Mutate(new[] {"hide", Screen.ToString(), Value.Player.Token, Value.Player.UserId}, Value.Token);
            Value.Gui.Mutate(new[] {"show", Screen.ToString(), Value.Player.Token, Value.Player.UserId}, Value.Token);
            Runtime.InMemoryGuiBackend.Call Rebuilt = Value.Flush();
            Bounds RebuiltFirst = Rect(Element(Rebuilt.Plan.Elements, First), 100, 100), RebuiltSecond = Rect(Element(Rebuilt.Plan.Elements, Second), 100, 100);
            Check(Near(RebuiltFirst.Top, 0) && Near(RebuiltSecond.Top, 10), "equivalent retained layout compiles deterministically");
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Label = Value.Create("TextLabel", Screen);
            Value.Set(Label, "Size", "udim2", "0", "100", "0", "50"); Value.Set(Label, "Text", "string", "padded");
            ulong Padding = Value.Create("UIPadding", Label); Value.Set(Padding, "PaddingLeft", "udim", "0", "3");
            Value.Set(Padding, "PaddingRight", "udim", "0", "7"); Value.Set(Padding, "PaddingTop", "udim", "0", "5");
            Value.Set(Padding, "PaddingBottom", "udim", "0", "11"); Value.Show(Screen);
            Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements; Runtime.GuiRenderElement Text = null;
            foreach (Runtime.GuiRenderElement Candidate in Elements) if (Candidate.Kind == Runtime.GuiRenderNodeKind.Text) Text = Candidate;
            Bounds Content = Rect(Text, 100, 50);
            Check(Near(Content.Left, 3) && Near(Content.Top, 5) && Near(Content.Width, 90) && Near(Content.Height, 34),
                "UIPadding applies to built-in text content without shrinking the parent surface");
        }
    }

    private static void RunHorizontalAndAlignment()
    {
        string[] Horizontal = {"Left", "Center", "Right"}; string[] Vertical = {"Top", "Center", "Bottom"};
        foreach (string HorizontalValue in Horizontal) foreach (string VerticalValue in Vertical) {
            using (var Value = new Fixture()) {
                ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), Child = Value.Create("Frame", Parent);
                Value.Set(Parent, "Size", "udim2", "0", "100", "0", "80"); Value.Set(Child, "Size", "udim2", "0.25", "5", "0.5", "4");
                ulong Layout = Value.Create("UIListLayout", Parent); Value.Set(Layout, "FillDirection", "string", "Horizontal");
                Value.Set(Layout, "HorizontalAlignment", "string", HorizontalValue); Value.Set(Layout, "VerticalAlignment", "string", VerticalValue);
                Value.Show(Screen); Bounds ChildRect = Rect(Element(Value.Flush().Plan.Elements, Child), 100, 80);
                double Width = 30, Height = 44;
                double Left = HorizontalValue == "Left" ? 0 : HorizontalValue == "Center" ? 35 : 70;
                double Top = VerticalValue == "Top" ? 0 : VerticalValue == "Center" ? 18 : 36;
                Check(Near(ChildRect.Left, Left) && Near(ChildRect.Top, Top) && Near(ChildRect.Width, Width) && Near(ChildRect.Height, Height),
                    "horizontal canonical alignment " + HorizontalValue + "/" + VerticalValue);
            }
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Outer = Value.Create("Frame", Screen), Inner = Value.Create("Frame", Outer), Leaf = Value.Create("Frame", Inner);
            Value.Set(Outer, "Size", "udim2", "0", "200", "0", "200"); Value.Set(Inner, "Size", "udim2", "0.5", "0", "0.5", "0");
            Value.Set(Leaf, "Size", "udim2", "0.5", "0", "0.5", "0"); Value.Create("UIListLayout", Outer); Value.Create("UIListLayout", Inner);
            Value.Show(Screen); Bounds LeafRect = Rect(Element(Value.Flush().Plan.Elements, Leaf), 100, 100);
            Check(Near(LeafRect.Width, 50) && Near(LeafRect.Height, 50), "nested affine layouts remain parent-relative");
        }
    }

    private static void RunDirtyAndPublication()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), A = Value.Create("Frame", Parent), B = Value.Create("Frame", Parent);
            Value.Set(Parent, "Size", "udim2", "0", "200", "0", "200"); Value.Set(A, "Size", "udim2", "0", "20", "0", "20");
            Value.Set(B, "Size", "udim2", "0", "20", "0", "20"); Value.Show(Screen); Value.Flush();
            Value.Set(A, "LayoutOrder", "integer", "4"); Check(!Value.Gui.HasWork, "LayoutOrder without layout is metadata-only");
            ulong Layout = Value.Create("UIListLayout", Parent); Value.Flush();
            Value.Set(A, "LayoutOrder", "integer", "8"); Runtime.InMemoryGuiBackend.Call OrderPatch = Value.Flush();
            Check(OrderPatch.Kind == Runtime.GuiBackendOperationKind.Update && OrderPatch.Patch.Elements.Length == 2,
                "LayoutOrder under list synthesizes direct-child patches only");
            Value.Set(B, "LayoutOrder", "integer", "9"); Value.Flush();
            Value.Set(A, "Size", "udim2", "0", "20", "0", "60"); Runtime.GuiRenderElement[] SizePatch = Value.Flush().Patch.Elements;
            Check(Near(Rect(Element(SizePatch, B), 200, 200).Top, 60), "Size mutation recomputes following direct child");
            Value.Set(A, "Visible", "boolean", "0"); Runtime.GuiRenderElement[] HiddenPatch = Value.Flush().Patch.Elements;
            Check(Near(Rect(Element(HiddenPatch, B), 200, 200).Top, 0), "hidden arranged child consumes no space");
            Value.Set(A, "Visible", "boolean", "1"); Value.Flush();
            Value.Gui.BeginPublication(); Value.Set(Layout, "Padding", "udim", "0", "30");
            int Before = Value.Backend.Calls().Length; Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Value.Backend.Calls().Length == Before, "provisional layout mutation has no client effect"); Value.Gui.RollbackPublication();
            Check(!Value.Gui.HasWork, "layout rollback restores committed synchronization state");
            Value.Gui.BeginPublication(); Value.Set(Layout, "Padding", "udim", "0", "30"); Value.Gui.CommitPublication();
            Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Update, "layout publication commit synchronizes latest geometry");
            Value.Gui.BeginPublication(); ulong Padding = Value.Create("UIPadding", Parent);
            Check(Value.Query("get", Padding.ToString(), "Parent")[1] == Parent.ToString(), "provisional helper creation supports read-your-writes");
            Value.Gui.RollbackPublication();
            Check(Value.Query("children", Parent.ToString()).Length == 6, "provisional helper creation rollback restores tree");
            Value.Gui.BeginPublication(); Padding = Value.Create("UIPadding", Parent); Value.Set(Padding, "PaddingLeft", "udim", "0", "4"); Value.Gui.CommitPublication();
            Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Replace, "provisional helper creation commits atomically");
            ulong NewChild = Value.Create("Frame", Parent); Runtime.InMemoryGuiBackend.Call Added = Value.Flush();
            Check(Added.Kind == Runtime.GuiBackendOperationKind.Replace, "add arranged child uses structural reconciliation");
            Value.Set(NewChild, "Parent", "nil"); Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Replace, "reparent arranged child uses structural reconciliation");
        }
        var Config = new Runtime.GuiConfig {MaxTrackedDirtyObjectsPerDomain = 1};
        using (var Value = new Fixture(Config)) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen); Value.Create("Frame", Parent); Value.Create("Frame", Parent);
            ulong Layout = Value.Create("UIListLayout", Parent); Value.Show(Screen); Value.Flush();
            Value.Set(Layout, "Padding", "udim", "0", "1");
            Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Replace, "layout dirty-detail overflow collapses to full reconciliation");
        }
    }

    private static void RunBoundsAndPerformance()
    {
        int[] Counts = {1, 10, 32, 64};
        foreach (int Count in Counts) using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen); Value.Set(Parent, "Size", "udim2", "1", "0", "1", "0");
            for (int Index = 0; Index < Count; ++Index) { ulong Child = Value.Create("Frame", Parent); Value.Set(Child, "Size", "udim2", "0", "10", "0", "10"); }
            ulong Layout = Value.Create("UIListLayout", Parent); Value.Show(Screen); var Clock = Stopwatch.StartNew(); Runtime.InMemoryGuiBackend.Call Full = Value.Flush(); Clock.Stop();
            long FullTicks = Clock.ElapsedTicks; int PlanBytes = Full.Plan.EstimatedSerializedBytes; Value.Set(Layout, "Padding", "udim", "0", "1"); Clock.Restart(); Runtime.InMemoryGuiBackend.Call Update = Value.Flush(); Clock.Stop();
            int Synthesized = Update.Kind == Runtime.GuiBackendOperationKind.Update ? Update.Patch.Elements.Length : Count;
            Check(Synthesized == Count && PlanBytes <= Value.Limits.MaxSerializedOperationBytes,
                Count + "-child recomputation stays bounded; elapsed ticks=" + Clock.ElapsedTicks + ", bytes=" + PlanBytes);
            Console.WriteLine("[CarbonLuau:GuiFoundation2APerf] children=" + Count + " fullTicks=" + FullTicks +
                " patchTicks=" + Clock.ElapsedTicks + " synthesized=" + Synthesized + " planBytes=" + PlanBytes);
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var Registrar = new EmptyRegistrar(); var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), Registrar);
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource =
            "local G=game:GetService('Gui'); local F=G:Create('Frame'); local A=F:Create('Frame'); " +
            "local L=F:Create('UIListLayout'); local P=F:Create('UIPadding'); " +
            "assert(not L:IsA('GuiObject') and L:IsA('GuiNode') and A.LayoutOrder==0); A.LayoutOrder=-3; " +
            "L.Padding=UDim.new(.1,4); L.FillDirection='Horizontal'; L.HorizontalAlignment='Center'; L.VerticalAlignment='Bottom'; " +
            "P.PaddingLeft=UDim.new(.2,5); assert(P.PaddingLeft==UDim.new(.2,5) and A.LayoutOrder==-3)"};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Snapshot, World))
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native public layout surface");
        Check(World.Gui.LiveObjects == 0, "native layout teardown returns global retained count to zero");
        Console.WriteLine("[CarbonLuau:GuiFoundation2ANative] PASS public userdata surface and teardown");
    }

    private sealed class EmptyRegistrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
}
