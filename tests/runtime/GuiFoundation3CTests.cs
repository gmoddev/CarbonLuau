using System;
using System.Collections.Generic;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation3CTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate();
        internal readonly Runtime.InMemoryGuiBackend Backend = new Runtime.InMemoryGuiBackend();
        internal readonly Runtime.GuiRetainedWorld World;
        internal readonly Runtime.GuiRetainedRegistry Gui;
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>();
        private ulong NextRegistration;
        private int NextPlayer;

        internal Fixture()
        {
            Players = new Runtime.PlayerDirectory(Id => { Runtime.PlayerView Value; return Views.TryGetValue(Id, out Value) ? Value : null; });
            World = new Runtime.GuiRetainedWorld(Limits, Players, Backend);
            Gui = new Runtime.GuiRetainedRegistry(World, 90, 91);
        }

        internal Runtime.PlayerLifetime AddPlayer()
        {
            string Id = (76561190004000000L + ++NextPlayer).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Font" + NextPlayer, Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Players.Connect(View);
        }

        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Token)[0]); }
        internal void Set(ulong Id, string Name, params string[] Value)
        { var Fields = new List<string> {"set", Id.ToString(), Name}; Fields.AddRange(Value); Gui.Mutate(Fields.ToArray(), Token); }
        internal string[] Get(ulong Id, string Name) { return Gui.Query(new[] {"get", Id.ToString(), Name}); }
        internal void Show(ulong Screen, Runtime.PlayerLifetime Player)
        { Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Token); }
        internal void Drain()
        { int Guard = 256; while (Gui.HasWork && Guard-- > 0) Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(!Gui.HasWork, "GUI flush converged"); }
        internal string Token() { return (++NextRegistration).ToString(); }
        public void Dispose() { Gui.Dispose(); }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 3C: " + Message); }
    private static void Reject(Action Value, string Message)
    { bool Rejected = false; try { Value(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static Runtime.GuiPropertyUse Descriptor(Runtime.GuiClassId ClassId, Runtime.GuiPropertyId PropertyId)
    { foreach (Runtime.GuiPropertyUse Value in Runtime.GuiSchema.GetClass(ClassId).Properties) if (Value.Descriptor.Id == PropertyId) return Value; return null; }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; return null; }
    private static Runtime.GuiRenderElement TextElement(Runtime.GuiRenderPlan Plan, ulong ObjectId)
    { foreach (Runtime.GuiRenderElement Value in Plan.Elements) if (Value.Kind == Runtime.GuiRenderNodeKind.Text && Value.ClientId.EndsWith("t" + ObjectId.ToString("x"), StringComparison.Ordinal)) return Value; return null; }
    private static Runtime.GuiRenderElement TextElement(Runtime.GuiRenderPatch Patch, ulong ObjectId)
    { foreach (Runtime.GuiRenderElement Value in Patch.Elements) if (Value.Kind == Runtime.GuiRenderNodeKind.Text && Value.ClientId.EndsWith("t" + ObjectId.ToString("x"), StringComparison.Ordinal)) return Value; return null; }

    internal static void RunModel()
    {
        RunSchemaDefaultsAndMapping();
        RunSynchronizationAndPublication();
        RunLifecycleAndRecovery();
        Console.WriteLine("[CarbonLuau:GuiFoundation3CModel] PASS immutable font identity, retained properties, backend mapping, patches, publication and lifecycle");
    }

    private static void RunSchemaDefaultsAndMapping()
    {
        Runtime.GuiSchema.Validate(); Runtime.GuiValueTypeDescriptor Type = Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.GuiFont);
        Check(Type.Name == "GuiFont" && Type.Fields.Length == 0 && Type.Constructors.Length == 0, "GuiFont has no fields or constructor surface");
        foreach (Runtime.GuiClassId ClassId in new[] {Runtime.GuiClassId.TextLabel, Runtime.GuiClassId.TextButton}) {
            Runtime.GuiPropertyUse Font = Descriptor(ClassId, Runtime.GuiPropertyId.Font);
            Check(Font != null && Font.Writable && Font.DefaultValue == "GuiFont.RobotoCondensedRegular" &&
                Font.Descriptor.ValueKind == Runtime.GuiValueKind.GuiFont && Font.Descriptor.MutationKind == Runtime.GuiMutationKind.Patchable,
                "text class exposes the retained patchable GuiFont property");
        }
        foreach (Runtime.GuiClassId ClassId in new[] {Runtime.GuiClassId.Frame, Runtime.GuiClassId.ImageLabel, Runtime.GuiClassId.ImageButton,
            Runtime.GuiClassId.ScrollingFrame, Runtime.GuiClassId.UIListLayout, Runtime.GuiClassId.UIGridLayout, Runtime.GuiClassId.UIPadding})
            Check(Descriptor(ClassId, Runtime.GuiPropertyId.Font) == null, "Font remains unavailable on " + ClassId);

        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.AddPlayer(); ulong Screen = Value.Create("ScreenGui");
            ulong Label = Value.Create("TextLabel", Screen), Button = Value.Create("TextButton", Screen), Frame = Value.Create("Frame", Screen);
            Check(String.Join("|", Value.Get(Label, "Font")) == "guifont|RobotoCondensedRegular" &&
                String.Join("|", Value.Get(Button, "Font")) == "guifont|RobotoCondensedRegular", "existing text defaults remain regular");
            Reject(() => Value.Set(Label, "Font", "string", "RobotoCondensedBold"), "ordinary strings cannot convert to GuiFont");
            Reject(() => Value.Set(Label, "Font", "guifont", "robotocondensed-bold.ttf"), "host paths cannot enter retained font state");
            Reject(() => Value.Set(Label, "Font", "guifont", "Unknown"), "unknown font identities are rejected");
            Reject(() => Value.Set(Frame, "Font", "guifont", "DroidSansMono"), "non-text classes reject Font");
            Value.Set(Label, "Font", "guifont", "RobotoCondensedBold");
            Value.Set(Button, "Font", "guifont", "DroidSansMono"); Value.Show(Screen, Player); Value.Drain();
            Runtime.GuiRenderPlan Plan = Value.Backend.Calls()[0].Plan;
            Check(Property(TextElement(Plan, Label), Runtime.GuiRenderPropertyId.Font).FontIdentity == Runtime.GuiFontIdentity.RobotoCondensedBold &&
                Property(TextElement(Plan, Button), Runtime.GuiRenderPropertyId.Font).FontIdentity == Runtime.GuiFontIdentity.DroidSansMono,
                "TextLabel and TextButton carry canonical render identities");
            string Json = Runtime.RustCuiBackend.Serialize(Plan.Elements, false, true);
            Check(Json.Contains("\"font\":\"robotocondensed-bold.ttf\"") && Json.Contains("\"font\":\"droidsansmono.ttf\""),
                "backend maps canonical identities to explicit host assets");
            Check(!Plan.Describe().Contains(".ttf"), "backend-neutral render plan exposes no host asset path");
        }

        Runtime.GuiFontIdentity[] Identities = {Runtime.GuiFontIdentity.RobotoCondensedRegular, Runtime.GuiFontIdentity.RobotoCondensedBold,
            Runtime.GuiFontIdentity.DroidSansMono, Runtime.GuiFontIdentity.PermanentMarker};
        string[] Assets = {"robotocondensed-regular.ttf", "robotocondensed-bold.ttf", "droidsansmono.ttf", "permanentmarker.ttf"};
        for (int Index = 0; Index < Identities.Length; ++Index) {
            var Limits = new Runtime.GuiConfig().Validate();
            var Element = new Runtime.GuiRenderElement("text", null, Runtime.GuiRenderNodeKind.Text, Limits,
                new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Font, Runtime.GuiRenderValue.FromFont(Identities[Index])));
            string Json = Runtime.RustCuiBackend.Serialize(new[] {Element}, true, false);
            Check(Json.Contains("\"font\":\"" + Assets[Index] + "\""), "exact backend map for " + Identities[Index]);
        }
    }

    private static void RunSynchronizationAndPublication()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.AddPlayer(), B = Value.AddPlayer(); ulong Screen = Value.Create("ScreenGui"), Label = Value.Create("TextLabel", Screen);
            Value.Set(Label, "Font", "guifont", "PermanentMarker"); Value.Show(Screen, A); Value.Show(Screen, B); Value.Drain();
            Check(Value.Backend.Calls().Length == 2, "initial non-default font reaches both Presentations");
            int Calls = Value.Backend.Calls().Length;
            Value.Set(Label, "Font", "guifont", "RobotoCondensedRegular");
            Value.Set(Label, "Font", "guifont", "RobotoCondensedBold");
            Value.Set(Label, "Font", "guifont", "DroidSansMono"); Value.Drain();
            Runtime.InMemoryGuiBackend.Call[] All = Value.Backend.Calls();
            Check(All.Length == Calls + 2, "one coalesced font patch is emitted per Presentation");
            for (int Index = Calls; Index < All.Length; ++Index) {
                Runtime.GuiRenderValue Font = Property(TextElement(All[Index].Patch, Label), Runtime.GuiRenderPropertyId.Font);
                Check(All[Index].Kind == Runtime.GuiBackendOperationKind.Update && Font.FontIdentity == Runtime.GuiFontIdentity.DroidSansMono,
                    "repeated writes coalesce to the newest canonical font");
            }

            Value.Gui.BeginPublication(); Value.Set(Label, "Font", "guifont", "PermanentMarker");
            Check(Value.Get(Label, "Font")[1] == "PermanentMarker" && Value.Backend.Calls().Length == All.Length,
                "provisional font provides read-your-writes without client effect");
            Value.Gui.RollbackPublication(); Check(Value.Get(Label, "Font")[1] == "DroidSansMono", "rollback restores the previous font exactly");
            Value.Gui.BeginPublication(); Value.Set(Label, "Font", "guifont", "PermanentMarker"); Value.Gui.CommitPublication(); Value.Drain();
            Check(Value.Get(Label, "Font")[1] == "PermanentMarker", "publication commit synchronizes retained Font");

            Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "font patch failure");
            Value.Set(Label, "Font", "guifont", "RobotoCondensedBold"); Value.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Check(Value.Gui.FullResyncPresentationCount >= 1, "failed font patch enters existing full-resync path"); Value.Drain();
            Runtime.InMemoryGuiBackend.Call Last = Value.Backend.Calls()[Value.Backend.Calls().Length - 1];
            Check(Last.Kind == Runtime.GuiBackendOperationKind.Replace &&
                Property(TextElement(Last.Plan, Label), Runtime.GuiRenderPropertyId.Font).FontIdentity == Runtime.GuiFontIdentity.RobotoCondensedBold,
                "full reconciliation converges to the latest retained font");
        }
    }

    private static void RunLifecycleAndRecovery()
    {
        Runtime.GuiFontIdentity Escaped = Runtime.GuiFontIdentity.PermanentMarker;
        using (var Value = new Fixture()) {
            ulong Screen = Value.Create("ScreenGui"), Label = Value.Create("TextLabel", Screen);
            Value.Set(Label, "Font", "guifont", "PermanentMarker");
            ulong Clone = UInt64.Parse(Value.Gui.Mutate(new[] {"clone", Label.ToString()}, Value.Token)[0]);
            Check(Value.Get(Clone, "Font")[1] == "PermanentMarker", "Clone copies retained Font");
            Value.Gui.Mutate(new[] {"destroy", Label.ToString()}, Value.Token);
            Reject(() => Value.Get(Label, "Font"), "Destroy stales the text object normally");
        }
        Check(Escaped == Runtime.GuiFontIdentity.PermanentMarker, "immutable font identity survives source-domain retirement");

        var Players = new Runtime.PlayerDirectory(Id => null); var Backend = new Runtime.InMemoryGuiBackend();
        var World = new Runtime.FacadeWorld(Players, new Registrar(), new Runtime.GuiConfig().Validate(), Backend);
        var A1 = new Runtime.FacadeSession(World, 400, 401, 256); World.Commit(A1);
        ulong First = UInt64.Parse(A1.Gui.Mutate(new[] {"create", "", "TextLabel"}, () => "1")[0]);
        A1.Gui.Mutate(new[] {"set", First.ToString(), "Font", "guifont", "RobotoCondensedBold"}, () => "2");
        var Failed = new Runtime.FacadeSession(World, 400, 402, 256);
        ulong FailedLabel = UInt64.Parse(Failed.Gui.Mutate(new[] {"create", "", "TextLabel"}, () => "1")[0]);
        Failed.Gui.Mutate(new[] {"set", FailedLabel.ToString(), "Font", "guifont", "DroidSansMono"}, () => "2"); World.Retire(Failed);
        Check(A1.Gui.Query(new[] {"get", First.ToString(), "Font"})[1] == "RobotoCondensedBold", "failed replacement leaves committed font authority intact");
        var A2 = new Runtime.FacadeSession(World, 400, 403, 256); World.Commit(A2); World.Retire(A1);
        Reject(() => A1.Gui.Query(new[] {"get", First.ToString(), "Font"}), "root replacement stales old text objects but not font values");
        World.Retire(A2);
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0, "replacement/provider-style retirement returns font-owning GUI to baseline");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        string Source = "local G=game:GetService('Gui'); local L=G:Create('TextLabel'); local B=G:Create('TextButton'); " +
            "assert(L.Font==GuiFont.RobotoCondensedRegular and B.Font==GuiFont.RobotoCondensedRegular); " +
            "assert(GuiFont.RobotoCondensedBold==GuiFont.RobotoCondensedBold and GuiFont.RobotoCondensedBold~=GuiFont.DroidSansMono); " +
            "assert(tostring(GuiFont.PermanentMarker)=='GuiFont.PermanentMarker' and not string.find(tostring(GuiFont.PermanentMarker), '.ttf', 1, true)); " +
            "assert(not pcall(function() GuiFont.RobotoCondensedBold.Name='x' end)); assert(not pcall(function() GuiFont.New='x' end)); " +
            "assert(GuiFont.new==nil and not pcall(function() L.Font='RobotoCondensedBold' end)); " +
            "L.Font=GuiFont.RobotoCondensedBold; B.Font=GuiFont.DroidSansMono; assert(L.Font==GuiFont.RobotoCondensedBold and B.Font==GuiFont.DroidSansMono); " +
            "local C=L:Clone(); assert(C.Font==GuiFont.RobotoCondensedBold); L:Destroy(); assert(not pcall(function() return L.Font end)); " +
            "local Escaped=GuiFont.PermanentMarker; assert(Escaped==GuiFont.PermanentMarker)";
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig(), Snapshot, World)) {
            Runtime.ExecutionResult Initial = Host.Reload();
            Check(Initial.Status == Runtime.RuntimeStatus.OK, "native public GuiFont and Font surface: " + Initial.Status + " " + Initial.Error);
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native root replacement reconstructs retained font state");
            Check(Host.Execute("gui3c.timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT,
                "fatal VM recovery preserves the font-capable bootstrap contract");
            Check(World.Active != null && World.Active.Gui.LiveObjectCount == 2, "recovery republishes the current font-bearing source");
        }
        Check(World.Gui.LiveRegistryCount == 0 && World.Gui.LiveObjects == 0, "native unload/reload leaves no font-owned host resource");
        Console.WriteLine("[CarbonLuau:GuiFoundation3CNative] PASS values, type enforcement, Clone, replacement, recovery and teardown");
    }
}
