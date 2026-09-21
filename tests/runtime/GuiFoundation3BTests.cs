using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation3BTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.GuiLimits Limits;
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiRetainedWorld World;
        internal readonly Runtime.GuiRetainedRegistry Gui;
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>();
        internal readonly Runtime.PlayerLifetime Player;
        private ulong NextRegistration;

        internal Fixture(Runtime.GuiConfig Config = null)
        {
            Limits = (Config ?? new Runtime.GuiConfig()).Validate();
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.GuiRetainedWorld(Limits, Players, Backend);
            Gui = new Runtime.GuiRetainedRegistry(World, 70, 80);
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = "76561190003000001",
                Name = "Clip", Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(View.UserId, View); Player = Players.Connect(View);
        }

        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Token)[0]); }
        internal void Set(ulong Id, string Name, params string[] Value)
        { var Fields = new List<string> {"set", Id.ToString(), Name}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), Token); }
        internal string[] Get(ulong Id, string Name) { return Gui.Query(new[] {"get", Id.ToString(), Name}); }
        internal void Connect(ulong Id) { Gui.Mutate(new[] {"connect", Id.ToString()}, Token); }
        internal void Show(ulong Screen) { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Token); }
        internal Runtime.InMemoryGuiBackend.Call Flush()
        {
            int Guard = 128; while (Gui.HasWork && Guard-- > 0) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush);
            Check(!Gui.HasWork, "GUI flush converged"); Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
            return Calls.Length == 0 ? null : Calls[Calls.Length - 1];
        }
        internal string Token() { return (++NextRegistration).ToString(); }
        public void Dispose() { Gui.Dispose(); }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 3B: " + Message); }
    private static void Reject(Action Value, string Message)
    { bool Rejected = false; try { Value(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static Runtime.GuiPropertyUse Descriptor(Runtime.GuiClassId ClassId, Runtime.GuiPropertyId PropertyId)
    { foreach (Runtime.GuiPropertyUse Value in Runtime.GuiSchema.GetClass(ClassId).Properties) if (Value.Descriptor.Id == PropertyId) return Value; return null; }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; return null; }
    private static Runtime.GuiRenderElement Find(Runtime.GuiRenderPlan Plan, string Marker)
    { foreach (Runtime.GuiRenderElement Value in Plan.Elements) if (Value.ClientId.EndsWith(Marker, StringComparison.Ordinal)) return Value; return null; }
    private static int KindCount(Runtime.GuiRenderPlan Plan, Runtime.GuiRenderNodeKind Kind)
    { int Result = 0; foreach (Runtime.GuiRenderElement Value in Plan.Elements) if (Value.Kind == Kind) Result++; return Result; }
    private static string ActionToken(Runtime.GuiRenderPlan Plan)
    {
        foreach (Runtime.GuiRenderElement Element in Plan.Elements) {
            Runtime.GuiRenderValue Value = Property(Element, Runtime.GuiRenderPropertyId.ActionCommand);
            if (Value != null) return Value.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
        }
        return null;
    }

    internal static void RunModel()
    {
        RunSchemaRetainedAndProjection();
        RunDepthScrollingAndReparent();
        RunInteractionAndSynchronization();
        RunBoundsPublicationBackendAndPerformance();
        Console.WriteLine("[CarbonLuau:GuiFoundation3BModel] PASS clipping schema, projection, depth, interaction, publication, recovery and bounds");
    }

    private static void RunSchemaRetainedAndProjection()
    {
        Runtime.GuiSchema.Validate(); Runtime.GuiPropertyUse Clip = Descriptor(Runtime.GuiClassId.Frame, Runtime.GuiPropertyId.ClipsDescendants);
        Check(Clip != null && Clip.Writable && Clip.DefaultValue == "false" && Clip.Descriptor.ValueKind == Runtime.GuiValueKind.Boolean &&
            Clip.Descriptor.MutationKind == Runtime.GuiMutationKind.Structural, "Frame-only structural boolean descriptor");
        Check(Descriptor(Runtime.GuiClassId.GuiObject, Runtime.GuiPropertyId.ClipsDescendants) == null &&
            Descriptor(Runtime.GuiClassId.TextLabel, Runtime.GuiPropertyId.ClipsDescendants) == null &&
            Descriptor(Runtime.GuiClassId.ScrollingFrame, Runtime.GuiPropertyId.ClipsDescendants) == null, "property is unavailable outside Frame");
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Frame = Value.Create("Frame", Screen), Child = Value.Create("TextLabel", Frame);
            Value.Set(Frame, "BackgroundTransparency", "number", "1");
            Check(String.Join("|", Value.Get(Frame, "ClipsDescendants")) == "boolean|0", "default false readback");
            Reject(() => Value.Set(Frame, "ClipsDescendants", "number", "1"), "wrong wire type rejected");
            Reject(() => Value.Set(Child, "ClipsDescendants", "boolean", "1"), "non-Frame property rejected");
            Value.Show(Screen); Runtime.GuiRenderPlan Unclipped = Value.Flush().Plan;
            Check(KindCount(Unclipped, Runtime.GuiRenderNodeKind.Clip) == 0, "false emits no clip root");
            Value.Set(Frame, "ClipsDescendants", "boolean", "1"); Runtime.GuiRenderPlan Clipped = Value.Flush().Plan;
            Runtime.GuiRenderElement ClipElement = Find(Clipped, "c" + Frame.ToString("x"));
            Runtime.GuiRenderElement FrameElement = Find(Clipped, "o" + Frame.ToString("x"));
            Runtime.GuiRenderElement ChildElement = Find(Clipped, "o" + Child.ToString("x"));
            Check(ClipElement != null && ClipElement.Kind == Runtime.GuiRenderNodeKind.Clip && KindCount(Clipped, Runtime.GuiRenderNodeKind.Clip) == 1,
                "true emits one deterministic private clip root");
            Check(ClipElement.ParentClientId == FrameElement.ClientId && ChildElement.ParentClientId == ClipElement.ClientId,
                "descendants parent through private clip root");
            Check(Property(FrameElement, Runtime.GuiRenderPropertyId.BackgroundColor).Color.A == 0,
                "transparent author Frame stays independent of clip authority");
            string Json = Runtime.RustCuiBackend.Serialize(Clipped.Elements, false, true);
            Check(Json.Contains("UnityEngine.UI.Mask") && Json.Contains("\"showMaskGraphic\":false") && Json.Contains("\"color\":\"0 0 0 0\""),
                "host mapping uses an invisible independent rectangular mask");
            Check(Value.Gui.Query(new[] {"children", Frame.ToString()}).Length == 2, "private clip root is absent from retained children");
            ulong Clone = UInt64.Parse(Value.Gui.Mutate(new[] {"clone", Frame.ToString()}, Value.Token)[0]);
            Check(Value.Get(Clone, "ClipsDescendants")[1] == "1" && Value.Gui.Query(new[] {"children", Clone.ToString()}).Length == 2,
                "Clone copies retained clip state but no helper object");
            Value.Set(Frame, "ClipsDescendants", "boolean", "0"); Runtime.GuiRenderPlan Restored = Value.Flush().Plan;
            Check(KindCount(Restored, Runtime.GuiRenderNodeKind.Clip) == 0 && Restored.Describe() != Clipped.Describe(),
                "toggle rebuild removes clip deterministically");
        }
    }

    private static void RunDepthScrollingAndReparent()
    {
        using (var Value = new Fixture()) {
            Check(Value.Limits.MaxEffectiveClipDepth == 4, "canonical effective depth bound");
            ulong Screen = Value.Create("ScreenGui"), A = Value.Create("Frame", Screen); Value.Set(A, "ClipsDescendants", "boolean", "1");
            ulong ScrollA = Value.Create("ScrollingFrame", A), B = Value.Create("Frame", ScrollA); Value.Set(B, "ClipsDescendants", "boolean", "1");
            ulong ScrollB = Value.Create("ScrollingFrame", B), C = Value.Create("Frame", ScrollB);
            Reject(() => Value.Set(C, "ClipsDescendants", "boolean", "1"), "depth five mutation rejected atomically");
            Check(Value.Get(C, "ClipsDescendants")[1] == "0", "rejected depth mutation retains false");
            ulong Detached = Value.Create("Frame"); Value.Set(Detached, "ClipsDescendants", "boolean", "1");
            Reject(() => Value.Set(Detached, "Parent", "object", ScrollB.ToString()), "reparent into depth five rejected atomically");
            Check(Value.Get(Detached, "Parent")[0] == "nil", "failed reparent preserves detached state");
            Value.Set(B, "ClipsDescendants", "boolean", "0"); Value.Set(Detached, "Parent", "object", ScrollB.ToString());
            Check(Value.Get(Detached, "Parent")[1] == ScrollB.ToString(), "reparent succeeds after depth is reduced");
            Value.Show(Screen); Runtime.GuiRenderPlan Plan = Value.Flush().Plan;
            Check(KindCount(Plan, Runtime.GuiRenderNodeKind.Clip) == 2 && Plan.ProjectedElementCount >= 18,
                "explicit Frame clips and private ScrollingFrame clips share projection accounting");
        }
        using (var Layout = new Fixture()) {
            ulong Screen = Layout.Create("ScreenGui"), Frame = Layout.Create("Frame", Screen), Padding = Layout.Create("UIPadding", Frame);
            ulong Grid = Layout.Create("UIGridLayout", Frame), Cell = Layout.Create("Frame", Frame);
            Layout.Set(Frame, "ClipsDescendants", "boolean", "1");
            Layout.Set(Padding, "PaddingLeft", "udim", "0", "8");
            Layout.Set(Grid, "CellSize", "udim2", "0", "140", "0", "140");
            Layout.Set(Cell, "Position", "udim2", "0.75", "17", "0.5", "19");
            Layout.Set(Cell, "Size", "udim2", "0", "23", "0", "29");
            string Position = String.Join("|", Layout.Get(Cell, "Position")), Size = String.Join("|", Layout.Get(Cell, "Size"));
            Layout.Show(Screen); Runtime.GuiRenderPlan Plan = Layout.Flush().Plan;
            Check(KindCount(Plan, Runtime.GuiRenderNodeKind.Clip) == 1 && Find(Plan, "o" + Cell.ToString("x")).ParentClientId.EndsWith("c" + Frame.ToString("x")),
                "grid and padding project through clipping without a second layout authority");
            Check(String.Join("|", Layout.Get(Cell, "Position")) == Position && String.Join("|", Layout.Get(Cell, "Size")) == Size,
                "clipping and grid projection do not rewrite retained geometry");
        }
    }

    private static void RunInteractionAndSynchronization()
    {
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Frame = Value.Create("Frame", Screen);
            ulong Text = Value.Create("TextButton", Frame), Nested = Value.Create("Frame", Frame), Image = Value.Create("ImageButton", Nested);
            Value.Set(Nested, "Size", "udim2", "1", "0", "1", "0");
            Value.Set(Nested, "Position", "udim2", "0.5", "0", "0", "0");
            Value.Set(Nested, "ClipsDescendants", "boolean", "1");
            Value.Set(Image, "Position", "udim2", "0.75", "0", "0", "0");
            Value.Connect(Text); Value.Connect(Image); Value.Show(Screen); Runtime.GuiRenderPlan Initial = Value.Flush().Plan;
            string OldToken = ActionToken(Initial); Check(OldToken != null, "visible controls receive action authority");
            Value.Set(Text, "Position", "udim2", "2", "0", "0", "0");
            Value.Set(Frame, "ClipsDescendants", "boolean", "1");
            Runtime.GuiActionAdmission Admission;
            Check(!Value.World.TryAdmit(Value.Player, OldToken, out Admission), "clip structural mutation invalidates the old token immediately");
            Runtime.GuiRenderPlan Clipped = Value.Flush().Plan;
            Check(ActionToken(Clipped) == null && !Property(Clipped.Elements[0], Runtime.GuiRenderPropertyId.NeedsCursor).Boolean,
                "fully outside TextButton and ImageButton receive no effective authority");
            Value.Set(Text, "Position", "udim2", "0.25", "0", "0.25", "0"); Runtime.GuiRenderPlan Visible = Value.Flush().Plan;
            Check(ActionToken(Visible) != null, "geometry re-entry under a clip rebuilds visible authority");
            Value.Set(Frame, "ClipsDescendants", "boolean", "0"); Runtime.GuiRenderPlan Unclipped = Value.Flush().Plan;
            Check(ActionToken(Unclipped) != null && KindCount(Unclipped, Runtime.GuiRenderNodeKind.Clip) == 1,
                "outer clip exit restores interaction eligibility while preserving the nested clip");
        }
    }

    private static void RunBoundsPublicationBackendAndPerformance()
    {
        using (var Tight = new Fixture(new Runtime.GuiConfig {MaxProjectedElementsPerScreen = 2})) {
            ulong Screen = Tight.Create("ScreenGui"), Frame = Tight.Create("Frame", Screen);
            Reject(() => Tight.Set(Frame, "ClipsDescendants", "boolean", "1"), "private clip cannot exceed the 257-derived configured envelope");
            Check(Tight.Get(Frame, "ClipsDescendants")[1] == "0", "projection rejection is atomic");
        }
        Reject(() => new Runtime.GuiConfig {MaxEffectiveClipDepth = 5}.Validate(), "clip depth configuration cannot exceed canonical four");
        using (var Exact = new Fixture()) {
            ulong Screen = Exact.Create("ScreenGui"), Frame = Exact.Create("Frame", Screen);
            for (int Index = 0; Index < 36; ++Index) Exact.Create("ScrollingFrame", Frame);
            Exact.Create("TextLabel", Frame); Exact.Set(Frame, "ClipsDescendants", "boolean", "1"); Exact.Show(Screen);
            Check(Exact.Flush().Plan.ProjectedElementCount == 257, "private clip is accepted at the exact 257-element boundary");
        }
        using (var Excess = new Fixture()) {
            ulong Screen = Excess.Create("ScreenGui"), Frame = Excess.Create("Frame", Screen);
            for (int Index = 0; Index < 36; ++Index) Excess.Create("ScrollingFrame", Frame);
            Excess.Create("TextLabel", Frame); Excess.Create("Frame", Frame);
            Reject(() => Excess.Set(Frame, "ClipsDescendants", "boolean", "1"), "258th projected element is rejected");
            Check(Excess.Get(Frame, "ClipsDescendants")[1] == "0", "near-bound rejection preserves retained state");
        }
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Frame = Value.Create("Frame", Screen);
            Value.Gui.BeginPublication(); Value.Set(Frame, "ClipsDescendants", "boolean", "1");
            Check(Value.Get(Frame, "ClipsDescendants")[1] == "1" && Value.Backend.Calls().Length == 0, "provisional read-your-writes has no client effect");
            Value.Gui.RollbackPublication(); Check(Value.Get(Frame, "ClipsDescendants")[1] == "0", "rollback restores committed clipping state");
            Value.Gui.BeginPublication(); Value.Set(Frame, "ClipsDescendants", "boolean", "1"); Value.Gui.CommitPublication();
            Value.Show(Screen); Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Replace, Runtime.GuiBackendResultCode.SendFailed, "clip failure");
            Value.Flush(); Runtime.InMemoryGuiBackend.Call Failed = Value.Backend.Calls()[0];
            Check(!Failed.Result.Accepted && Value.Get(Frame, "ClipsDescendants")[1] == "1", "failed rebuild retains authoritative clip state");
            Value.Set(Frame, "ClipsDescendants", "boolean", "0"); Value.Set(Frame, "ClipsDescendants", "boolean", "1");
            Runtime.GuiRenderPlan Recovered = Value.Flush().Plan;
            Check(KindCount(Recovered, Runtime.GuiRenderNodeKind.Clip) == 1, "later reconciliation converges to latest clipping state");

            var Clock = Stopwatch.StartNew();
            for (int Index = 0; Index < 64; ++Index) Value.Create("Frame", Frame);
            Value.Set(Frame, "ClipsDescendants", "boolean", "0"); Value.Set(Frame, "ClipsDescendants", "boolean", "1");
            Runtime.GuiRenderPlan Many = Value.Flush().Plan; Clock.Stop();
            Check(Many.ProjectedElementCount == 67 && Many.EstimatedSerializedBytes <= Value.Limits.MaxSerializedOperationBytes,
                "64 descendants plus private clip stay bounded");
            Console.WriteLine("[CarbonLuau:GuiFoundation3BPerf] descendants=64 projected=" + Many.ProjectedElementCount +
                " estimatedBytes=" + Many.EstimatedSerializedBytes + " rebuildTicks=" + Clock.ElapsedTicks);
        }
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource =
            "local G=game:GetService('Gui'); local F=G:Create('Frame'); assert(F.ClipsDescendants==false); " +
            "F.ClipsDescendants=true; assert(F.ClipsDescendants==true); local C=F:Create('Frame'); " +
            "local B=C:Create('TextButton'); B.Position=UDim2.fromScale(2,0); local Copy=F:Clone(); " +
            "assert(Copy.ClipsDescendants and #Copy:GetChildren()==1 and Copy:FindFirstChild('Frame')~=nil); " +
            "assert(not pcall(function() B.ClipsDescendants=true end)); F.ClipsDescendants=false; assert(not F.ClipsDescendants)"};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Snapshot, World))
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native public ClipsDescendants userdata surface");
        Check(World.Gui.LiveObjects == 0, "native clipping teardown returns global retained count to zero");
        Console.WriteLine("[CarbonLuau:GuiFoundation3BNative] PASS public property, validation, Clone and teardown");
    }
}
