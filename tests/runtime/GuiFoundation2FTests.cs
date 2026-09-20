using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation2FTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views =
            new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate();
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.FacadeSession Session;
        private ulong NextRegistration;

        internal Fixture()
        {
            Players = new Runtime.PlayerDirectory(Id => {
                Runtime.PlayerView Value;
                return Views.TryGetValue(Id, out Value) ? Value : null;
            });
            World = new Runtime.FacadeWorld(Players, new Registrar(), Limits, Backend);
            Session = new Runtime.FacadeSession(World, 600, 601, 256);
            World.Commit(Session);
        }

        internal Runtime.PlayerLifetime AddPlayer(int Index)
        {
            string Id = (76561190002000000L + Index).ToString();
            var View = new Runtime.PlayerView {
                Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Scale" + Index, Connected = true, Send = Value => { }, Permission = Value => true
            };
            Views.Add(Id, View);
            return Players.Connect(View);
        }

        internal string Registration() { return (++NextRegistration).ToString(); }

        internal void Drain()
        {
            int Guard = 65536;
            while (World.HasWork && Guard-- > 0) World.FlushGui(Stopwatch.StartNew(), 100);
            Check(!World.HasWork, "bounded GUI work converges");
        }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 2F: " + Message); }

    private static ulong Create(Fixture Value, string ClassName, ulong Parent = 0)
    {
        return UInt64.Parse(Value.Session.Gui.Mutate(
            new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Value.Registration)[0]);
    }

    private static void Set(Fixture Value, ulong ObjectId, string Property, params string[] PropertyValue)
    {
        var Fields = new List<string> {"set", ObjectId.ToString(), Property};
        Fields.AddRange(PropertyValue);
        Value.Session.Gui.Mutate(Fields.ToArray(), Value.Registration);
    }

    private static void Show(Fixture Value, ulong Screen, Runtime.PlayerLifetime Player)
    {
        Value.Session.Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Value.Registration);
    }

    private static ulong BuildRichScreen(Fixture Value, out ulong FirstScroll, out ulong Image)
    {
        ulong Screen = Create(Value, "ScreenGui");
        ulong Panel = Create(Value, "Frame", Screen);
        Set(Value, Panel, "Size", "udim2", "0", "720", "0", "520");

        ulong Padding = Create(Value, "UIPadding", Panel);
        Set(Value, Padding, "PaddingTop", "udim", "0", "8");
        Set(Value, Padding, "PaddingBottom", "udim", "0", "8");
        Set(Value, Padding, "PaddingLeft", "udim", "0", "8");
        Set(Value, Padding, "PaddingRight", "udim", "0", "8");
        ulong Layout = Create(Value, "UIListLayout", Panel);
        Set(Value, Layout, "Padding", "udim", "0", "4");
        Set(Value, Layout, "FillDirection", "string", "Vertical");

        ulong Label = Create(Value, "TextLabel", Panel);
        Set(Value, Label, "Text", "string", "Foundation 2 rich screen");
        Set(Value, Label, "LayoutOrder", "integer", "1");
        ulong TextButton = Create(Value, "TextButton", Panel);
        Set(Value, TextButton, "Text", "string", "Text action");
        Value.Session.Gui.Mutate(new[] {"connect", TextButton.ToString()}, Value.Registration);
        Image = Create(Value, "ImageLabel", Panel);
        Set(Value, Image, "Image", "imagesource", "Png", "42");
        ulong ImageButton = Create(Value, "ImageButton", Panel);
        Set(Value, ImageButton, "Image", "imagesource", "Item", "-932201673", "12345678901234567890");
        Value.Session.Gui.Mutate(new[] {"connect", ImageButton.ToString()}, Value.Registration);

        FirstScroll = 0;
        for (int Index = 0; Index < 27; ++Index) {
            ulong Scroll = Create(Value, "ScrollingFrame", Panel);
            if (FirstScroll == 0) FirstScroll = Scroll;
            Set(Value, Scroll, "LayoutOrder", "integer", (10 + Index).ToString());
            Set(Value, Scroll, "Size", "udim2", "1", "-16", "0", "96");
            Set(Value, Scroll, "CanvasSize", "udim2", "1", "0", "0", "192");
            Set(Value, Scroll, "ScrollingDirection", "string", Index % 3 == 0 ? "XY" : "Y");
            ulong InnerPadding = Create(Value, "UIPadding", Scroll);
            Set(Value, InnerPadding, "PaddingLeft", "udim", "0", "4");
            ulong InnerLayout = Create(Value, "UIListLayout", Scroll);
            Set(Value, InnerLayout, "Padding", "udim", "0", "2");
            ulong InnerImage = Create(Value, "ImageLabel", Scroll);
            Set(Value, InnerImage, "Image", "imagesource", "Sprite", "assets/icons/info.png");
        }
        return Screen;
    }

    internal static void RunModel()
    {
        foreach (int ViewerCount in new[] {1, 10, 50, 100}) RunScale(ViewerCount);
        Console.WriteLine("[CarbonLuau:GuiFoundation2FModel] PASS rich-screen 1/10/50/100-viewer scale, bounds and teardown");
    }

    internal static void RunNative(Runtime.NativeRuntime Native, string Root)
    {
        string[] Examples = {
            "hello", "shared-live", "per-player", "activated", "images", "scrolling",
            "layout-vertical", "layout-horizontal", "padding", "layout-order", "image-label", "image-button",
            "item-skin", "steam-avatar", "scrolling-layout", "shared-rich", "per-player-rich"
        };
        foreach (string Example in Examples) {
            string PathName = Path.Combine(Root, "examples", "gui", Example, "init.luau");
            string Source = File.ReadAllText(PathName);
            var Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
            var Players = new Runtime.PlayerDirectory(Id => {
                Runtime.PlayerView Value;
                return Views.TryGetValue(Id, out Value) ? Value : null;
            });
            for (int Index = 1; Index <= 2; ++Index) {
                string Id = (76561190003000000L + Index).ToString();
                var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id,
                    Name = "Example" + Index, Connected = true, Send = Value => { }, Permission = Value => true};
                Views.Add(Id, View); Players.Connect(View);
            }
            var Backend = new Runtime.InMemoryGuiBackend();
            var World = new Runtime.FacadeWorld(Players, new Registrar(), Backend);
            Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {
                EntryName = "examples/gui/" + Example + "/init.luau", EntrySource = Source
            };
            using (var Host = new Runtime.ScriptHost(Native,
                new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 100}, Snapshot, World)) {
                Runtime.ExecutionResult Result = Host.Reload();
                Check(Result.Status == Runtime.RuntimeStatus.OK, "public example executes: " + Example + ": " + Result.Error);
                Host.Drain();
                Check(World.Gui.LivePresentations >= 1 && Backend.Calls().Length >= 1,
                    "public example produces a retained Presentation: " + Example);
            }
            Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0 &&
                World.Gui.LivePresentations == 0 && World.Gui.LiveActionCount == 0,
                "public example teardown returns to baseline: " + Example);
        }
        Console.WriteLine("[CarbonLuau:GuiFoundation2FNative] PASS all bundled GUI examples through the pinned compiler/VM");
    }

    private static void RunScale(int ViewerCount)
    {
        var Value = new Fixture();
        ulong Scroll, Image;
        ulong Screen = BuildRichScreen(Value, out Scroll, out Image);
        var Players = new List<Runtime.PlayerLifetime>();
        for (int Index = 1; Index <= ViewerCount; ++Index) Players.Add(Value.AddPlayer(Index));

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long BeforeMemory = GC.GetTotalMemory(true);
        var Clock = Stopwatch.StartNew();
        foreach (Runtime.PlayerLifetime Player in Players) Show(Value, Screen, Player);
        Value.Drain();
        Clock.Stop();
        long RetainedMemoryUpperBound = Math.Max(0, GC.GetTotalMemory(false) - BeforeMemory);

        Runtime.InMemoryGuiBackend.Call[] Calls = Value.Backend.Calls();
        Runtime.GuiRenderPlan Plan = null;
        int Replacements = 0;
        foreach (Runtime.InMemoryGuiBackend.Call Call in Calls) {
            if (Call.Result.Accepted && Call.Kind == Runtime.GuiBackendOperationKind.Replace && Call.Plan != null) {
                Plan = Call.Plan;
                Replacements++;
            }
        }
        Check(Plan != null && Replacements == ViewerCount, ViewerCount + " viewers receive exactly one initial authoritative plan");
        string Json = Runtime.RustCuiBackend.Serialize(Plan.Elements, false, true);
        int SerializedBytes = Runtime.GuiRenderValue.Utf8Bytes(Json);
        Check(Plan.ProjectedElementCount >= 250 && Plan.ProjectedElementCount <= Value.Limits.MaxProjectedElementsPerScreen,
            "rich screen remains near and within the projection envelope");
        Check(Plan.EstimatedSerializedBytes <= Value.Limits.MaxSerializedOperationBytes &&
            SerializedBytes <= Value.Limits.MaxSerializedOperationBytes,
            "rich screen remains within estimated and actual serialization bounds");
        Check(Value.Session.Gui.PresentationCount == ViewerCount && Value.Session.Gui.DirtyPresentationCount == 0 &&
            Value.Session.Gui.FullResyncPresentationCount == 0,
            "initial scale flush leaves no dirty or reconciliation backlog");

        int BeforeMutations = Value.Backend.Calls().Length;
        Set(Value, Image, "ImageColor3", "color3", "0.5", "0.75", "1");
        Set(Value, Scroll, "CanvasSize", "udim2", "1", "0", "0", "256");
        Value.Drain();
        Check(Value.Backend.Calls().Length >= BeforeMutations + ViewerCount &&
            Value.Session.Gui.DirtyPresentationCount == 0 && Value.Session.Gui.FullResyncPresentationCount == 0,
            "shared mutation converges every Presentation without retry or dirty growth");

        Console.WriteLine("[CarbonLuau:GuiFoundation2FScale] viewers=" + ViewerCount +
            " retainedObjects=" + Value.Session.Gui.LiveObjectCount +
            " projected=" + Plan.ProjectedElementCount +
            " estimatedBytes=" + Plan.EstimatedSerializedBytes +
            " serializedBytes=" + SerializedBytes +
            " aggregateSerializedBytes=" + ((long)SerializedBytes * ViewerCount) +
            " presentationMemoryUpperBound=" + RetainedMemoryUpperBound +
            " initialFlushTicks=" + Clock.ElapsedTicks);

        Value.World.Retire(Value.Session);
        Check(Value.World.Gui.LiveRegistryCount == 0 && Value.World.Gui.LiveObjects == 0 &&
            Value.World.Gui.LivePresentations == 0 && Value.World.Gui.LiveActionCount == 0 &&
            Value.World.Gui.PlayerActionRateCount == 0 && !Value.World.HasWork,
            "rich-screen teardown returns retained, Presentation, action and work state to baseline");
    }
}
