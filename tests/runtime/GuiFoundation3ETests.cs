using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation3ETests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate();
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.GuiRetainedWorld World;
        internal readonly Runtime.GuiRetainedRegistry Gui;
        private int NextPlayer;
        private ulong NextRegistration;

        internal Fixture()
        {
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.GuiRetainedWorld(Limits, Players, Backend);
            Gui = new Runtime.GuiRetainedRegistry(World, 900, 901);
        }

        internal Runtime.PlayerLifetime AddPlayer()
        {
            string Id = (76561190008000000L + ++NextPlayer).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Foundation3" + NextPlayer, Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Players.Connect(View);
        }

        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Token)[0]); }

        internal void Set(ulong Id, string Name, params string[] Value)
        { var Fields = new List<string> {"set", Id.ToString(), Name}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), Token); }

        internal void Show(ulong Screen, Runtime.PlayerLifetime Player)
        { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Token); }

        internal void Scroll(ulong Id, Runtime.PlayerLifetime Player, double X, double Y)
        { Gui.Mutate(new[] {"scroll", Id.ToString(), Player.Token, Player.UserId, Number(X), Number(Y)}, Token); }

        internal void Drain()
        {
            int Guard = 65536;
            while (Gui.HasWork && Guard-- > 0) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
            Check(!Gui.HasWork, "GUI synchronization converged");
        }

        internal string Token() { return (++NextRegistration).ToString(); }
        public void Dispose() { Gui.Dispose(); }
    }

    private sealed class RichTree
    {
        internal ulong Screen, Clip, Scroll, Grid, Button, Image;
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 3E: " + Message); }

    private static string Number(double Value)
    { return Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }

    private static RichTree Build(Fixture Value, int CellCount, bool Dense)
    {
        var Result = new RichTree();
        Result.Screen = Value.Create("ScreenGui");
        Result.Clip = Value.Create("Frame", Result.Screen);
        Value.Set(Result.Clip, "Size", "udim2", "0", "720", "0", "520");
        Value.Set(Result.Clip, "ClipsDescendants", "boolean", "1");
        Result.Scroll = Value.Create("ScrollingFrame", Result.Clip);
        Value.Set(Result.Scroll, "Size", "udim2", "1", "0", "1", "0");
        Value.Set(Result.Scroll, "CanvasSize", "udim2", "0", "960", "0", "1600");
        Value.Set(Result.Scroll, "ScrollingDirection", "string", "XY");
        ulong Padding = Value.Create("UIPadding", Result.Scroll);
        Value.Set(Padding, "PaddingLeft", "udim", "0", "8");
        Value.Set(Padding, "PaddingTop", "udim", "0", "8");
        Result.Grid = Value.Create("UIGridLayout", Result.Scroll);
        Value.Set(Result.Grid, "CellSize", "udim2", "0", "132", "0", "76");
        Value.Set(Result.Grid, "CellPadding", "udim2", "0", "6", "0", "6");
        Value.Set(Result.Grid, "FillDirectionMaxCells", "integer", "8");
        for (int Index = 0; Index < CellCount; ++Index) {
            if (Dense) {
                ulong NestedScroll = Value.Create("ScrollingFrame", Result.Scroll);
                Value.Set(NestedScroll, "LayoutOrder", "integer", Index.ToString());
                Value.Set(NestedScroll, "CanvasSize", "udim2", "0", "264", "0", "152");
                continue;
            }
            ulong Cell = Value.Create("Frame", Result.Scroll);
            Value.Set(Cell, "LayoutOrder", "integer", Index.ToString());
            ulong Label = Value.Create("TextLabel", Cell);
            Value.Set(Label, "Text", "string", "Cell " + Index);
            Value.Set(Label, "Font", "guifont", (Index & 1) == 0 ? "RobotoCondensedBold" : "DroidSansMono");
            Result.Image = Value.Create("ImageLabel", Cell);
            Value.Set(Result.Image, "Image", "imagesource", "Sprite", "assets/icons/info.png");
            Result.Button = Value.Create("TextButton", Cell);
            Value.Set(Result.Button, "Text", "string", "Open");
            Value.Set(Result.Button, "Font", "guifont", "PermanentMarker");
            Value.Gui.Mutate(new[] {"connect", Result.Button.ToString()}, Value.Token);
        }
        if (Dense) {
            Result.Button = Value.Create("TextButton", Result.Scroll);
            Value.Set(Result.Button, "Text", "string", "Open");
            Value.Set(Result.Button, "Font", "guifont", "PermanentMarker");
            Value.Gui.Mutate(new[] {"connect", Result.Button.ToString()}, Value.Token);
            Result.Image = Value.Create("ImageLabel", Result.Scroll);
            Value.Set(Result.Image, "Image", "imagesource", "Sprite", "assets/icons/info.png");
        }
        return Result;
    }

    private static Runtime.GuiRenderPlan LatestPlan(Runtime.InMemoryGuiBackend Backend, string PlayerToken)
    {
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        for (int Index = Calls.Length - 1; Index >= 0; --Index)
            if (Calls[Index].Result.Accepted && Calls[Index].Plan != null && Calls[Index].Target.ExactPlayerConnectionToken == PlayerToken)
                return Calls[Index].Plan;
        return null;
    }

    private static int KindCount(Runtime.GuiRenderPlan Plan, Runtime.GuiRenderNodeKind Kind)
    { int Count = 0; foreach (Runtime.GuiRenderElement Element in Plan.Elements) if (Element.Kind == Kind) Count++; return Count; }

    internal static void RunModel()
    {
        RunCombinedSharedViewAndFailureConvergence();
        foreach (int ViewerCount in new[] {1, 10, 50, 100}) RunScale(ViewerCount);
        RunPublicationAndStress();
        Console.WriteLine("[CarbonLuau:GuiFoundation3EModel] PASS combined interactions, shared views, publication, 1/10/50/100-viewer scale and bounded stress");
    }

    private static void RunCombinedSharedViewAndFailureConvergence()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.AddPlayer(), B = Value.AddPlayer(); RichTree Tree = Build(Value, 8, false);
            Value.Show(Tree.Screen, A); Value.Show(Tree.Screen, B); Value.Drain();
            Runtime.GuiRenderPlan APlan = LatestPlan(Value.Backend, A.Token), BPlan = LatestPlan(Value.Backend, B.Token);
            Check(APlan != null && BPlan != null && APlan.ProjectedElementCount == BPlan.ProjectedElementCount,
                "one retained grid/clip/font/scroll tree projects equivalently to two independent Presentations");
            Check(KindCount(APlan, Runtime.GuiRenderNodeKind.Clip) == 1 && KindCount(APlan, Runtime.GuiRenderNodeKind.ScrollView) == 1 &&
                KindCount(APlan, Runtime.GuiRenderNodeKind.Text) >= 16, "combined projection contains bounded clip, scroll, grid text and interaction nodes");

            int Before = Value.Backend.Calls().Length;
            Value.Set(Tree.Grid, "CellPadding", "udim2", "0", "12", "0", "10");
            Value.Set(Tree.Button, "Font", "guifont", "RobotoCondensedBold");
            Value.Scroll(Tree.Scroll, A, 0.25, 0.75); Value.Drain();
            bool AEffect = false, BEffect = false;
            Runtime.InMemoryGuiBackend.Call[] Calls = Value.Backend.Calls();
            for (int Index = Before; Index < Calls.Length; ++Index) if (Calls[Index].Kind == Runtime.GuiBackendOperationKind.Scroll) {
                if (Calls[Index].Target.ExactPlayerConnectionToken == A.Token) AEffect = true;
                if (Calls[Index].Target.ExactPlayerConnectionToken == B.Token) BEffect = true;
            }
            Check(AEffect && !BEffect, "retained grid/font changes reach shared views while ScrollTo remains exact-Presentation local");

            Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "combined reconciliation failure");
            Value.Set(Tree.Clip, "ClipsDescendants", "boolean", "0"); Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Value.Set(Tree.Clip, "ClipsDescendants", "boolean", "1"); Value.Set(Tree.Grid, "FillDirection", "string", "Vertical"); Value.Drain();
            Runtime.GuiRenderPlan Recovered = LatestPlan(Value.Backend, A.Token);
            Check(Recovered != null && KindCount(Recovered, Runtime.GuiRenderNodeKind.Clip) == 1 && Value.Gui.FullResyncPresentationCount == 0,
                "backend failure converges to the newest combined retained state without historical replay");
        }
    }

    private static void RunScale(int ViewerCount)
    {
        using (var Value = new Fixture()) {
            RichTree Tree = Build(Value, 34, true); var Players = new List<Runtime.PlayerLifetime>();
            for (int Index = 0; Index < ViewerCount; ++Index) Players.Add(Value.AddPlayer());
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long BeforeMemory = GC.GetTotalMemory(true);
            var Clock = Stopwatch.StartNew(); foreach (Runtime.PlayerLifetime Player in Players) Value.Show(Tree.Screen, Player); Value.Drain(); Clock.Stop();
            Runtime.GuiRenderPlan Plan = LatestPlan(Value.Backend, Players[0].Token);
            int SerializedBytes = Runtime.GuiRenderValue.Utf8Bytes(Runtime.RustCuiBackend.Serialize(Plan.Elements, false, true));
            long PresentationMemoryUpperBound = Math.Max(0, GC.GetTotalMemory(false) - BeforeMemory);
            Check(Plan.ProjectedElementCount >= 250 && Plan.ProjectedElementCount <= Value.Limits.MaxProjectedElementsPerScreen,
                "combined rich screen remains near and within the 257-element envelope");
            Check(SerializedBytes <= Value.Limits.MaxSerializedOperationBytes && Value.Gui.PresentationCount == ViewerCount &&
                Value.Gui.DirtyPresentationCount == 0 && Value.Gui.FullResyncPresentationCount == 0,
                "combined scale flush stays within serialization bounds and leaves no backlog");
            foreach (Runtime.PlayerLifetime Player in Players) Value.Scroll(Tree.Scroll, Player, 0.5, 0.5);
            Check(Value.Gui.PendingScrollEffectCount == ViewerCount, "one bounded pending effect is isolated per Presentation"); Value.Drain();
            Console.WriteLine("[CarbonLuau:GuiFoundation3EScale] viewers=" + ViewerCount + " retainedObjects=" + Value.Gui.LiveObjectCount +
                " projected=" + Plan.ProjectedElementCount + " serializedBytes=" + SerializedBytes +
                " presentationMemoryUpperBound=" + PresentationMemoryUpperBound + " initialFlushTicks=" + Clock.ElapsedTicks);
        }
    }

    private static void RunPublicationAndStress()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.AddPlayer(); RichTree Tree = Build(Value, 4, false); Value.Show(Tree.Screen, Player); Value.Drain();
            string OriginalPadding = String.Join("|", Value.Gui.Query(new[] {"get", Tree.Grid.ToString(), "CellPadding"}));
            int BeforeCalls = Value.Backend.Calls().Length;
            for (int Index = 0; Index < 100; ++Index) {
                Value.Gui.BeginPublication(); Value.Set(Tree.Grid, "CellPadding", "udim2", "0", "31", "0", "31");
                Value.Set(Tree.Clip, "ClipsDescendants", "boolean", "0"); Value.Set(Tree.Button, "Font", "guifont", "DroidSansMono");
                Value.Scroll(Tree.Scroll, Player, 0.9, 0.9); Value.Gui.RollbackPublication();
            }
            Check(String.Join("|", Value.Gui.Query(new[] {"get", Tree.Grid.ToString(), "CellPadding"})) == OriginalPadding &&
                Value.Gui.PendingScrollEffectCount == 0 && Value.Backend.Calls().Length == BeforeCalls,
                "100 failed combined candidates publish no retained state or Presentation effect");

            for (int Index = 0; Index < 1000; ++Index) {
                ulong Clone = UInt64.Parse(Value.Gui.Mutate(new[] {"clone", Tree.Button.ToString()}, Value.Token)[0]);
                Value.Gui.Mutate(new[] {"destroy", Clone.ToString()}, Value.Token);
            }
            Value.Gui.BeginPublication(); Value.Set(Tree.Grid, "CellPadding", "udim2", "0", "9", "0", "9");
            Value.Set(Tree.Button, "Font", "guifont", "RobotoCondensedRegular"); Value.Scroll(Tree.Scroll, Player, 0.4, 0.6);
            Value.Gui.CommitPublication(); Value.Drain();
            Check(Value.Gui.PendingScrollEffectCount == 0 && Value.Gui.DirtyPresentationCount == 0 && Value.Gui.FullResyncPresentationCount == 0,
                "successful combined publication and 1,000 Clone/Destroy cycles converge without resource backlog");
        }
    }
}
