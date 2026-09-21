using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation3ATests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiRetainedWorld World;
        internal readonly Runtime.GuiRetainedRegistry Gui;
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerLifetime Player;
        private ulong NextRegistration;
        private int NextPlayer;

        internal Fixture(Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.GuiRetainedWorld(Limits, Players, Backend);
            Gui = new Runtime.GuiRetainedRegistry(World, 30, 60);
            Player = AddPlayer();
        }

        internal Runtime.PlayerLifetime AddPlayer()
        {
            string Id = (76561190003000000L + ++NextPlayer).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Grid" + NextPlayer, Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Players.Connect(View);
        }

        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Token)[0]); }

        internal void Set(ulong ObjectId, string Property, params string[] Value)
        { var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), Token); }

        internal string[] Query(params string[] Fields) { return Gui.Query(Fields); }

        internal void Show(ulong Screen, Runtime.PlayerLifetime Value = null)
        {
            Runtime.PlayerLifetime Target = Value ?? Player;
            Gui.Mutate(new[] {"show", Screen.ToString(), Target.Token, Target.UserId}, Token);
        }

        internal Runtime.InMemoryGuiBackend.Call Flush()
        {
            int Before = Backend.Calls().Length, Guard = 0;
            while (Gui.HasWork && Guard++ < 64) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
            Check(!Gui.HasWork && Backend.Calls().Length > Before, "expected grid synchronization output");
            Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls(); return Calls[Calls.Length - 1];
        }

        internal string Token() { return (++NextRegistration).ToString(); }
        public void Dispose() { Gui.Dispose(); }
    }

    private sealed class Bounds
    {
        internal readonly double Left, Top, Width, Height;
        internal Bounds(double Left, double Top, double Width, double Height)
        { this.Left = Left; this.Top = Top; this.Width = Width; this.Height = Height; }
    }

    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 3A: " + Message); }

    private static void Reject(Action Action, string Message)
    { bool Rejected = false; try { Action(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }

    private static bool Near(double Left, double Right) { return Math.Abs(Left - Right) < 0.000000001; }

    private static Runtime.GuiPropertyUse Descriptor(Runtime.GuiClassId ClassId, Runtime.GuiPropertyId PropertyId)
    { foreach (Runtime.GuiPropertyUse Value in Runtime.GuiSchema.GetClass(ClassId).Properties) if (Value.Descriptor.Id == PropertyId) return Value; return null; }

    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; throw new Exception("missing " + Id); }

    private static Runtime.GuiRenderElement Element(Runtime.GuiRenderElement[] Elements, ulong ObjectId)
    {
        string Suffix = "o" + ObjectId.ToString("x");
        foreach (Runtime.GuiRenderElement Element in Elements) if (Element.ClientId.EndsWith(Suffix, StringComparison.Ordinal)) return Element;
        throw new Exception("missing rendered grid object " + ObjectId);
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

    private static void SetGrid(Fixture Value, ulong Grid, double Width, double Height, double XGap, double YGap, int Maximum, string Direction = "Horizontal")
    {
        Value.Set(Grid, "CellSize", "udim2", "0", Width.ToString(System.Globalization.CultureInfo.InvariantCulture), "0", Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Value.Set(Grid, "CellPadding", "udim2", "0", XGap.ToString(System.Globalization.CultureInfo.InvariantCulture), "0", YGap.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Value.Set(Grid, "FillDirectionMaxCells", "integer", Maximum.ToString()); Value.Set(Grid, "FillDirection", "string", Direction);
    }

    internal static void RunModel()
    {
        RunDescriptorsAndExclusivity();
        RunHorizontalAndVerticalGolden();
        RunAlignmentPaddingNestedAndScrolling();
        RunRetainedAuthorityOrderingAndVisibility();
        RunSynchronizationAndPublication();
        RunReplacementLifecycle();
        RunBoundsAndPerformance();
        Console.WriteLine("[CarbonLuau:GuiFoundation3AModel] PASS deterministic grid schema, geometry, authority, synchronization, publication, lifecycle and bounds");
    }

    private static void RunDescriptorsAndExclusivity()
    {
        Runtime.GuiSchema.Validate(); Runtime.GuiClassDescriptor GridClass = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.UIGridLayout);
        Check(GridClass.Public && GridClass.BaseClass == Runtime.GuiClassId.GuiNode && !GridClass.CanHaveChildren &&
            GridClass.CreationScope == Runtime.GuiCreationScope.GuiObject, "UIGridLayout is a public childless GuiNode helper");
        Check(Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.CellSize).DefaultValue == "UDim2.fromOffset(100, 100)" &&
            Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.CellPadding).DefaultValue == "UDim2.fromOffset(0, 0)" &&
            Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.FillDirection).DefaultValue == "Horizontal" &&
            Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.FillDirectionMaxCells).DefaultValue == "1" &&
            Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.HorizontalAlignment).DefaultValue == "Left" &&
            Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.VerticalAlignment).DefaultValue == "Top",
            "canonical grid descriptor defaults");
        Runtime.GuiPropertyUse Maximum = Descriptor(Runtime.GuiClassId.UIGridLayout, Runtime.GuiPropertyId.FillDirectionMaxCells);
        Check(Maximum.Descriptor.Minimum == 1 && Maximum.Descriptor.Maximum == 64, "canonical max-cell descriptor range");

        using (var Value = new Fixture()) {
            ulong Parent = Value.Create("Frame"), Grid = Value.Create("UIGridLayout", Parent), Padding = Value.Create("UIPadding", Parent);
            Check(Value.Query("isa", Grid.ToString(), "GuiObject")[0] == "0" && Value.Query("isa", Grid.ToString(), "GuiNode")[0] == "1",
                "grid IsA boundary");
            Check(String.Join("|", Value.Query("get", Grid.ToString(), "CellSize")) == "udim2|0|100|0|100" &&
                Value.Query("get", Grid.ToString(), "FillDirectionMaxCells")[1] == "1", "retained grid defaults");
            Reject(() => Value.Create("Frame", Grid), "grid helper cannot own children");
            Reject(() => Value.Create("UIListLayout", Parent), "grid then list conflict rejected");
            Check(Value.Query("children", Parent.ToString()).Length == 4, "conflicting creation is atomic and padding coexists");
            Value.Set(Grid, "CellSize", "udim2", "0.25", "5", "0.5", "6");
            Value.Set(Grid, "CellPadding", "udim2", "0.1", "2", "0.2", "3");
            Reject(() => Value.Set(Grid, "CellSize", "udim2", "-0.01", "0", "0", "1"), "negative CellSize scale rejected");
            Reject(() => Value.Set(Grid, "CellSize", "udim2", "0", "-1", "0", "1"), "negative CellSize offset rejected");
            Reject(() => Value.Set(Grid, "CellPadding", "udim2", "0", "0", "-0.01", "0"), "negative CellPadding scale rejected");
            Reject(() => Value.Set(Grid, "CellPadding", "udim2", "0", "0", "0", "-1"), "negative CellPadding offset rejected");
            Reject(() => Value.Set(Grid, "CellSize", "udim2", "8.01", "0", "0", "0"), "CellSize canonical scale bound");
            Value.Set(Grid, "FillDirection", "string", "Vertical"); Value.Set(Grid, "FillDirection", "string", "Horizontal");
            Reject(() => Value.Set(Grid, "FillDirection", "string", "Diagonal"), "invalid grid direction rejected");
            foreach (int Count in new[] {1, 7, 64}) Value.Set(Grid, "FillDirectionMaxCells", "integer", Count.ToString());
            Reject(() => Value.Set(Grid, "FillDirectionMaxCells", "integer", "0"), "zero max cells rejected");
            Reject(() => Value.Set(Grid, "FillDirectionMaxCells", "integer", "65"), "65 max cells rejected");
            Reject(() => Value.Set(Grid, "FillDirectionMaxCells", "number", "4"), "max cells requires integer wire type");
            foreach (string Alignment in new[] {"Left", "Center", "Right"}) Value.Set(Grid, "HorizontalAlignment", "string", Alignment);
            foreach (string Alignment in new[] {"Top", "Center", "Bottom"}) Value.Set(Grid, "VerticalAlignment", "string", Alignment);
            Reject(() => Value.Set(Grid, "HorizontalAlignment", "string", "Stretch"), "invalid horizontal alignment rejected");
            Reject(() => Value.Set(Grid, "VerticalAlignment", "string", "Stretch"), "invalid vertical alignment rejected");

            ulong Other = Value.Create("Frame"), ExistingList = Value.Create("UIListLayout", Other);
            Reject(() => Value.Set(Grid, "Parent", "object", Other.ToString()), "grid reparent conflict rejected before mutation");
            Check(Value.Query("get", Grid.ToString(), "Parent")[1] == Parent.ToString(), "failed grid reparent retains old parent");
            Value.Gui.Mutate(new[] {"destroy", ExistingList.ToString()}, Value.Token); Value.Set(Grid, "Parent", "object", Other.ToString());
            Check(Value.Query("get", Grid.ToString(), "Parent")[1] == Other.ToString(), "grid reparent succeeds after conflict clears");

            ulong ListParent = Value.Create("Frame"), List = Value.Create("UIListLayout", ListParent);
            Reject(() => Value.Create("UIGridLayout", ListParent), "list then grid conflict rejected");
            Check(Value.Query("get", List.ToString(), "Parent")[1] == ListParent.ToString(), "existing list remains attached after conflict");

            Value.Gui.BeginPublication(); ulong ProvisionalParent = Value.Create("Frame"); Value.Create("UIGridLayout", ProvisionalParent);
            Reject(() => Value.Create("UIListLayout", ProvisionalParent), "provisional list/grid conflict rejected"); Value.Gui.RollbackPublication();
            Reject(() => Value.Query("children", ProvisionalParent.ToString()), "provisional conflicting tree rollback retires staged objects");

            Value.Set(Grid, "Parent", "object", Parent.ToString()); Value.Set(Grid, "FillDirectionMaxCells", "integer", "4");
            ulong Clone = UInt64.Parse(Value.Gui.Mutate(new[] {"clone", Parent.ToString()}, Value.Token)[0]);
            string[] CloneChildren = Value.Query("children", Clone.ToString()); ulong CloneGrid = 0;
            for (int Index = 0; Index < CloneChildren.Length; Index += 2) if (CloneChildren[Index + 1] == "UIGridLayout") CloneGrid = UInt64.Parse(CloneChildren[Index]);
            Check(CloneGrid != 0 && Value.Query("get", CloneGrid.ToString(), "FillDirectionMaxCells")[1] == "4",
                "Clone copies grid class and all retained properties");
            Reject(() => Value.Create("UIListLayout", Clone), "cloned layout manager preserves exclusivity");
        }
    }

    private static void RunHorizontalAndVerticalGolden()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen); Value.Set(Parent, "Size", "udim2", "0", "100", "0", "80");
            var Children = new List<ulong>(); for (int Index = 0; Index < 5; ++Index) Children.Add(Value.Create("Frame", Parent));
            Value.Set(Children[0], "ZIndex", "integer", "9"); Value.Set(Children[1], "ZIndex", "integer", "1");
            ulong Grid = Value.Create("UIGridLayout", Parent); SetGrid(Value, Grid, 20, 10, 5, 2, 3);
            Value.Set(Grid, "HorizontalAlignment", "string", "Center"); Value.Set(Grid, "VerticalAlignment", "string", "Bottom");
            Value.Show(Screen); Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements;
            double[,] Expected = {{15,58},{40,58},{65,58},{15,70},{40,70}};
            for (int Index = 0; Index < Children.Count; ++Index) {
                Bounds Actual = Rect(Element(Elements, Children[Index]), 100, 80);
                Check(Near(Actual.Left, Expected[Index,0]) && Near(Actual.Top, Expected[Index,1]) && Near(Actual.Width, 20) && Near(Actual.Height, 10),
                    "horizontal golden cell " + Index);
            }
            Check(Array.FindIndex(Elements, E => E.ClientId.EndsWith("o" + Children[1].ToString("x"))) <
                Array.FindIndex(Elements, E => E.ClientId.EndsWith("o" + Children[0].ToString("x"))),
                "ZIndex render order remains independent from grid LayoutOrder");
            string[] Attached = Value.Query("children", Parent.ToString());
            Check(Attached[0] == Children[0].ToString() && Attached[2] == Children[1].ToString(), "GetChildren remains attachment order");
            string Json = Runtime.RustCuiBackend.Serialize(Elements, false, true);
            Check(Json.Contains("RectTransform") && !Json.Contains("UIGridLayout") && !Json.Contains("GridLayoutGroup") &&
                !Json.Contains("ContentSizeFitter") && !Json.Contains("LayoutElement"), "grid projects ordinary rectangles without host layout components");

            Runtime.PlayerLifetime OtherPlayer = Value.AddPlayer(); Value.Show(Screen, OtherPlayer); Runtime.GuiRenderElement[] OtherView = Value.Flush().Plan.Elements;
            Bounds FirstViewer = Rect(Element(Elements, Children[3]), 100, 80), SecondViewer = Rect(Element(OtherView, Children[3]), 100, 80);
            Check(Near(FirstViewer.Left, SecondViewer.Left) && Near(FirstViewer.Top, SecondViewer.Top) &&
                Near(FirstViewer.Width, SecondViewer.Width) && Near(FirstViewer.Height, SecondViewer.Height),
                "identical retained grid state compiles to identical geometry for independent viewers");

            Value.Set(Grid, "FillDirection", "string", "Vertical"); Runtime.GuiRenderElement[] Vertical = Value.Flush().Patch.Elements;
            double[,] VerticalExpected = {{27.5,46},{27.5,58},{27.5,70},{52.5,46},{52.5,58}};
            for (int Index = 0; Index < Children.Count; ++Index) {
                Bounds Actual = Rect(Element(Vertical, Children[Index]), 100, 80);
                Check(Near(Actual.Left, VerticalExpected[Index,0]) && Near(Actual.Top, VerticalExpected[Index,1]), "vertical golden cell " + Index);
            }
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), Grid = Value.Create("UIGridLayout", Parent);
            Value.Show(Screen); Runtime.GuiRenderPlan Plan = Value.Flush().Plan;
            Check(Plan.Elements.Length == 2 && Plan.ProjectedElementCount == 2, "zero-child grid has no invalid arithmetic or synthesized cells");
        }
    }

    private static void RunAlignmentPaddingNestedAndScrolling()
    {
        foreach (string Horizontal in new[] {"Left", "Center", "Right"}) foreach (string Vertical in new[] {"Top", "Center", "Bottom"}) {
            using (var Value = new Fixture()) {
                ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), Child = Value.Create("Frame", Parent);
                Value.Set(Parent, "Size", "udim2", "0", "100", "0", "80"); ulong Grid = Value.Create("UIGridLayout", Parent);
                Value.Set(Grid, "CellSize", "udim2", "0.25", "5", "0.5", "4");
                Value.Set(Grid, "HorizontalAlignment", "string", Horizontal); Value.Set(Grid, "VerticalAlignment", "string", Vertical);
                Value.Show(Screen); Bounds Actual = Rect(Element(Value.Flush().Plan.Elements, Child), 100, 80);
                double Left = Horizontal == "Left" ? 0 : Horizontal == "Center" ? 35 : 70;
                double Top = Vertical == "Top" ? 0 : Vertical == "Center" ? 18 : 36;
                Check(Near(Actual.Left, Left) && Near(Actual.Top, Top) && Near(Actual.Width, 30) && Near(Actual.Height, 44),
                    "affine grid alignment " + Horizontal + "/" + Vertical);
            }
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Outer = Value.Create("Frame", Screen), Inner = Value.Create("Frame", Outer), Leaf = Value.Create("Frame", Inner);
            Value.Set(Outer, "Size", "udim2", "0", "200", "0", "200"); ulong Padding = Value.Create("UIPadding", Outer);
            Value.Set(Padding, "PaddingLeft", "udim", "0", "10"); Value.Set(Padding, "PaddingTop", "udim", "0", "20");
            Value.Set(Padding, "PaddingRight", "udim", "0", "10"); Value.Set(Padding, "PaddingBottom", "udim", "0", "20");
            ulong OuterGrid = Value.Create("UIGridLayout", Outer); Value.Set(OuterGrid, "CellSize", "udim2", "0.5", "0", "0.5", "0");
            ulong InnerGrid = Value.Create("UIGridLayout", Inner); Value.Set(InnerGrid, "CellSize", "udim2", "0.5", "0", "0.5", "0");
            Value.Show(Screen); Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements;
            Bounds InnerBounds = Rect(Element(Elements, Inner), 200, 200), LeafBounds = Rect(Element(Elements, Leaf), 90, 80);
            Check(Near(InnerBounds.Left, 10) && Near(InnerBounds.Top, 20) && Near(InnerBounds.Width, 90) && Near(InnerBounds.Height, 80) &&
                Near(LeafBounds.Width, 45) && Near(LeafBounds.Height, 40), "UIPadding precedes grid and nested grids remain direct-child affine computations");
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Scroll = Value.Create("ScrollingFrame", Screen), A = Value.Create("Frame", Scroll), B = Value.Create("Frame", Scroll);
            Value.Set(Scroll, "Size", "udim2", "0", "100", "0", "100"); Value.Set(Scroll, "CanvasSize", "udim2", "0", "300", "0", "400");
            ulong Grid = Value.Create("UIGridLayout", Scroll); SetGrid(Value, Grid, 50, 40, 10, 5, 2);
            Value.Show(Screen); Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements;
            Bounds AR = Rect(Element(Elements, A), 300, 400), BR = Rect(Element(Elements, B), 300, 400);
            Check(Near(AR.Left, 0) && Near(AR.Width, 50) && Near(BR.Left, 60) && Near(BR.Height, 40),
                "grid inside ScrollingFrame uses explicit retained CanvasSize content coordinates");
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), A = Value.Create("Frame", Parent), B = Value.Create("Frame", Parent);
            Value.Set(Parent, "Size", "udim2", "0", "100", "0", "80"); ulong Grid = Value.Create("UIGridLayout", Parent);
            Value.Set(Grid, "CellSize", "udim2", "0", "10", "0", "10");
            Value.Set(Grid, "CellPadding", "udim2", "0.1", "2", "0.05", "1"); Value.Set(Grid, "FillDirectionMaxCells", "integer", "2");
            Value.Show(Screen); Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements;
            Check(Near(Rect(Element(Elements, A), 100, 80).Left, 0) && Near(Rect(Element(Elements, B), 100, 80).Left, 22),
                "mixed scale and offset CellPadding is interpreted against padded content geometry");
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), A = Value.Create("Frame", Parent), B = Value.Create("Frame", Parent);
            Value.Set(Parent, "Size", "udim2", "0", "100", "0", "80"); ulong Grid = Value.Create("UIGridLayout", Parent);
            SetGrid(Value, Grid, 70, 20, 10, 0, 2); Value.Set(Grid, "HorizontalAlignment", "string", "Center");
            Value.Show(Screen); Runtime.GuiRenderElement[] Elements = Value.Flush().Plan.Elements;
            Check(Near(Rect(Element(Elements, A), 100, 80).Left, -25) && Near(Rect(Element(Elements, B), 100, 80).Left, 55),
                "oversized grid remains algebraically aligned without shrinking cells or changing topology");
        }
    }

    private static void RunRetainedAuthorityOrderingAndVisibility()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen); Value.Set(Parent, "Size", "udim2", "0", "100", "0", "100");
            ulong A = Value.Create("Frame", Parent), B = Value.Create("Frame", Parent), C = Value.Create("Frame", Parent);
            Value.Set(A, "LayoutOrder", "integer", "4"); Value.Set(B, "LayoutOrder", "integer", "4"); Value.Set(C, "LayoutOrder", "integer", "5");
            ulong Grid = Value.Create("UIGridLayout", Parent); SetGrid(Value, Grid, 10, 12, 0, 0, 3);
            Value.Show(Screen); Runtime.GuiRenderElement[] First = Value.Flush().Plan.Elements;
            Check(Near(Rect(Element(First, A), 100, 100).Left, 0) && Near(Rect(Element(First, B), 100, 100).Left, 10),
                "duplicate LayoutOrder ties use retained attachment order then identity");

            Value.Set(A, "Position", "udim2", "0.75", "9", "0.5", "8"); Value.Set(A, "Size", "udim2", "0.25", "7", "0.5", "6");
            Check(!Value.Gui.HasWork, "grid-managed Position and Size mutation is retained-only");
            Check(String.Join("|", Value.Query("get", A.ToString(), "Position")) == "udim2|0.75|9|0.5|8" &&
                String.Join("|", Value.Query("get", A.ToString(), "Size")) == "udim2|0.25|7|0.5|6", "grid-managed retained geometry provides immediate reads");
            Value.Set(A, "AnchorPoint", "vector2", "0.5", "0.5"); Runtime.GuiRenderElement[] AnchorPatch = Value.Flush().Patch.Elements;
            Bounds Anchored = Rect(Element(AnchorPatch, A), 100, 100);
            Check(Near(Anchored.Left, 0) && Near(Anchored.Top, 0) && Near(Anchored.Width, 10) && Near(Anchored.Height, 12),
                "AnchorPoint re-encodes but does not alter computed grid cell bounds");

            Value.Set(B, "Visible", "boolean", "0"); Runtime.GuiRenderElement[] Hidden = Value.Flush().Patch.Elements;
            Check(Near(Rect(Element(Hidden, C), 100, 100).Left, 10), "hidden child consumes no cell or gap");
            Value.Set(B, "Visible", "boolean", "1"); Runtime.GuiRenderElement[] Shown = Value.Flush().Patch.Elements;
            Check(Near(Rect(Element(Shown, C), 100, 100).Left, 20), "show restores deterministic occupied cell");

            Value.Gui.Mutate(new[] {"destroy", Grid.ToString()}, Value.Token); Runtime.GuiRenderElement[] Restored = Value.Flush().Plan.Elements;
            Bounds Authored = Rect(Element(Restored, A), 100, 100);
            Check(Near(Authored.Left, 68) && Near(Authored.Top, 30) && Near(Authored.Width, 32) && Near(Authored.Height, 56),
                "destroying grid restores latest retained Position and Size authority");

            ulong List = Value.Create("UIListLayout", Parent); Value.Flush(); Bounds Listed = Rect(Element(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Plan.Elements, A), 100, 100);
            Check(Near(Listed.Width, 32) && Near(Listed.Height, 56), "grid to list transition uses existing retained-Size list semantics");
            Value.Gui.Mutate(new[] {"destroy", List.ToString()}, Value.Token); Value.Flush();
            ulong Other = Value.Create("Frame", Screen), OtherGrid = Value.Create("UIGridLayout", Other); SetGrid(Value, OtherGrid, 25, 30, 0, 0, 1);
            Value.Set(A, "Parent", "object", Other.ToString()); Bounds Moved = Rect(Element(Value.Flush().Plan.Elements, A), 100, 100);
            Check(Near(Moved.Width, 25) && Near(Moved.Height, 30), "child reparent into another grid adopts destination cell geometry");
            Value.Set(A, "Parent", "nil"); Check(Value.Query("get", A.ToString(), "Size")[2] == "7", "reparent out preserves latest retained Size");
        }
    }

    private static void RunSynchronizationAndPublication()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen), A = Value.Create("Frame", Parent), B = Value.Create("Frame", Parent);
            ulong Grid = Value.Create("UIGridLayout", Parent); SetGrid(Value, Grid, 20, 20, 2, 2, 2); Value.Show(Screen); Value.Flush();
            Value.Set(Grid, "CellSize", "udim2", "0", "25", "0", "25"); Value.Set(Grid, "CellPadding", "udim2", "0", "3", "0", "4");
            Runtime.InMemoryGuiBackend.Call Coalesced = Value.Flush();
            Check(Coalesced.Kind == Runtime.GuiBackendOperationKind.Update && Coalesced.Patch.Elements.Length == 2,
                "multiple grid mutations coalesce to direct arranged child patches");
            Value.Set(A, "LayoutOrder", "integer", "4"); Check(Value.Flush().Patch.Elements.Length == 2,
                "LayoutOrder under grid is layout-affecting");

            Value.Gui.BeginPublication(); Value.Set(Grid, "CellSize", "udim2", "0", "60", "0", "60");
            Value.Set(A, "Position", "udim2", "0", "91", "0", "92"); Value.Set(A, "Size", "udim2", "0", "93", "0", "94");
            int Before = Value.Backend.Calls().Length; Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Value.Backend.Calls().Length == Before && Value.Query("get", Grid.ToString(), "CellSize")[2] == "60",
                "provisional grid mutation has read-your-writes and no client effect");
            Value.Gui.RollbackPublication();
            Check(Value.Query("get", Grid.ToString(), "CellSize")[2] == "25" && Value.Query("get", A.ToString(), "Position")[2] == "0" &&
                Value.Query("get", A.ToString(), "Size")[2] == "100" && !Value.Gui.HasWork,
                "grid rollback restores retained state and projection authority");

            Value.Gui.BeginPublication(); Value.Set(Grid, "CellSize", "udim2", "0", "30", "0", "30");
            Value.Set(A, "Position", "udim2", "0", "11", "0", "12"); Value.Set(A, "Size", "udim2", "0", "13", "0", "14");
            Value.Gui.CommitPublication(); Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Update,
                "grid publication commit synchronizes one newest geometry state");
            Check(Value.Query("get", A.ToString(), "Position")[2] == "11" && Value.Query("get", A.ToString(), "Size")[2] == "13",
                "committed grid-managed retained Position and Size survive publication");

            Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "grid patch failure");
            Value.Set(Grid, "CellPadding", "udim2", "0", "5", "0", "5"); Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Value.Gui.FullResyncPresentationCount == 1, "grid backend patch failure requests normal full reconciliation");
            Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Replace && Value.Gui.FullResyncPresentationCount == 0,
                "grid backend failure converges through authoritative newest full state");

            Value.Gui.BeginPublication(); Value.Gui.Mutate(new[] {"destroy", Grid.ToString()}, Value.Token);
            Check(Value.Query("get", A.ToString(), "Position")[2] == "11", "provisional grid removal exposes retained authority in staged state");
            Value.Gui.RollbackPublication(); Check(Value.Query("get", Grid.ToString(), "Parent")[1] == Parent.ToString(),
                "provisional grid destruction rollback restores layout manager");
        }

        var Config = new Runtime.GuiConfig {MaxTrackedDirtyObjectsPerDomain = 1};
        using (var Value = new Fixture(Config)) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen); Value.Create("Frame", Parent); Value.Create("Frame", Parent);
            ulong Grid = Value.Create("UIGridLayout", Parent); Value.Show(Screen); Value.Flush();
            Value.Set(Grid, "CellPadding", "udim2", "0", "1", "0", "1");
            Check(Value.Flush().Kind == Runtime.GuiBackendOperationKind.Replace, "grid dirty-detail overflow collapses to full reconciliation");
        }
    }

    private static void RunReplacementLifecycle()
    {
        var Players = new Runtime.PlayerDirectory(Id => null); var World = new Runtime.FacadeWorld(Players, new Registrar());
        var A1 = new Runtime.FacadeSession(World, 1000, 1001, 256); World.Commit(A1);
        ulong Parent = UInt64.Parse(A1.Gui.Mutate(new[] {"create", "", "Frame"}, () => "1")[0]);
        ulong Grid = UInt64.Parse(A1.Gui.Mutate(new[] {"create", Parent.ToString(), "UIGridLayout"}, () => "2")[0]);
        A1.Gui.Mutate(new[] {"set", Grid.ToString(), "FillDirectionMaxCells", "integer", "4"}, () => "3");
        var Failed = new Runtime.FacadeSession(World, 1001, 1002, 256);
        ulong FailedParent = UInt64.Parse(Failed.Gui.Mutate(new[] {"create", "", "Frame"}, () => "4")[0]);
        Failed.Gui.Mutate(new[] {"create", FailedParent.ToString(), "UIGridLayout"}, () => "5"); World.Retire(Failed);
        Check(A1.Gui.Query(new[] {"get", Grid.ToString(), "FillDirectionMaxCells"})[1] == "4",
            "failed candidate retirement leaves active grid lifetime unchanged");
        var A2 = new Runtime.FacadeSession(World, 1002, 1003, 256); World.Commit(A2); World.Retire(A1);
        Reject(() => A1.Gui.Query(new[] {"get", Grid.ToString(), "CellSize"}), "successful replacement stales old grid lifetime");
        World.Retire(A2);
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0, "grid replacement and teardown return retained counters to baseline");
    }

    private static void RunBoundsAndPerformance()
    {
        foreach (int Count in new[] {1, 10, 32, 64}) using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Parent = Value.Create("Frame", Screen); Value.Set(Parent, "Size", "udim2", "1", "0", "1", "0");
            for (int Index = 0; Index < Count; ++Index) Value.Create("Frame", Parent);
            ulong Grid = Value.Create("UIGridLayout", Parent); Value.Set(Grid, "FillDirectionMaxCells", "integer", "8");
            Value.Set(Grid, "CellSize", "udim2", "0", "10", "0", "10"); Value.Show(Screen);
            var Clock = Stopwatch.StartNew(); Runtime.InMemoryGuiBackend.Call Full = Value.Flush(); Clock.Stop(); long FullTicks = Clock.ElapsedTicks;
            Check(Full.Plan.ProjectedElementCount == Count + 2 && Full.Plan.EstimatedSerializedBytes <= Value.Limits.MaxSerializedOperationBytes,
                "grid full plan remains within projection and serialization bounds at " + Count);
            Value.Set(Grid, "CellPadding", "udim2", "0", "1", "0", "1"); Clock.Restart(); Runtime.InMemoryGuiBackend.Call Patch = Value.Flush(); Clock.Stop();
            int Synthesized = Patch.Kind == Runtime.GuiBackendOperationKind.Update ? Patch.Patch.Elements.Length : Count;
            Check(Synthesized == Count, "grid recomputation remains bounded to direct children at " + Count);
            Console.WriteLine("[CarbonLuau:GuiFoundation3APerf] children=" + Count + " fullTicks=" + FullTicks +
                " patchTicks=" + Clock.ElapsedTicks + " synthesized=" + Synthesized + " projected=" + Full.Plan.ProjectedElementCount +
                " planBytes=" + Full.Plan.EstimatedSerializedBytes);
            if (Count == 64) Reject(() => Value.Create("Frame", Parent), "65th arranged child rejected by existing direct-child bound");
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Outer = Value.Create("Frame", Screen), Inner = Value.Create("Frame", Outer), Leaf = Value.Create("Frame", Inner);
            Value.Create("UIGridLayout", Outer); Value.Create("UIGridLayout", Inner); Value.Show(Screen);
            var Clock = Stopwatch.StartNew(); Runtime.InMemoryGuiBackend.Call Nested = Value.Flush(); Clock.Stop();
            Check(Nested.Plan.ProjectedElementCount == 4, "nested grid emits no helper or empty-cell projection elements");
            Console.WriteLine("[CarbonLuau:GuiFoundation3APerf] nestedTicks=" + Clock.ElapsedTicks + " projected=" + Nested.Plan.ProjectedElementCount +
                " planBytes=" + Nested.Plan.EstimatedSerializedBytes);
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native, string Root)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource =
            "local G=game:GetService('Gui'); local F=G:Create('Frame'); local A=F:Create('Frame'); " +
            "A.Position=UDim2.fromOffset(11,12); A.Size=UDim2.fromOffset(13,14); local Grid=F:Create('UIGridLayout'); " +
            "assert(Grid:IsA('GuiNode') and not Grid:IsA('GuiObject') and Grid.CellSize==UDim2.fromOffset(100,100)); " +
            "Grid.CellSize=UDim2.new(.25,5,.5,6); Grid.CellPadding=UDim2.fromOffset(2,3); Grid.FillDirection='Vertical'; " +
            "Grid.FillDirectionMaxCells=4; Grid.HorizontalAlignment='Center'; Grid.VerticalAlignment='Bottom'; " +
            "assert(Grid.FillDirectionMaxCells==4 and A.Position==UDim2.fromOffset(11,12) and A.Size==UDim2.fromOffset(13,14)); " +
            "assert(not pcall(function() Grid.CellSize=UDim2.fromOffset(-1,1) end)); " +
            "assert(not pcall(function() F:Create('UIListLayout') end)); local C=F:Clone(); " +
            "assert(C:FindFirstChild('UIGridLayout').FillDirectionMaxCells==4); Grid:Destroy(); " +
            "assert(A.Position==UDim2.fromOffset(11,12) and A.Size==UDim2.fromOffset(13,14))"};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Snapshot, World))
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native public UIGridLayout userdata surface");
        Check(World.Gui.LiveObjects == 0, "native grid teardown returns global retained count to zero");

        string ExampleSource = File.ReadAllText(Path.Combine(Root, "examples", "gui", "grid", "init.luau"));
        var ExampleWorld = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        Func<Runtime.ScriptSnapshot> Example = () => new Runtime.ScriptSnapshot {
            EntryName = "examples/gui/grid/init.luau", EntrySource = ExampleSource
        };
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Example, ExampleWorld))
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "public grid example executes through pinned compiler/VM");
        Check(ExampleWorld.Gui.LiveObjects == 0, "public grid example teardown returns global retained count to zero");
        Console.WriteLine("[CarbonLuau:GuiFoundation3ANative] PASS public userdata surface, validation, exclusivity, Clone, example and teardown");
    }
}
