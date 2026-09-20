using System;
using System.Collections.Generic;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation2BTests
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
            Session = new Runtime.FacadeSession(World, 31, 41, 256); World.Commit(Session);
        }
        internal Runtime.PlayerLifetime Add(int Index)
        {
            string Id = (76561190000600000L + Index).ToString();
            var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = Id, Name = "Image" + Index,
                Connected = true, Send = Value => { }, Permission = Value => true};
            Views.Add(Id, View); return Players.Connect(View);
        }
        internal ulong Create(string ClassName, ulong Parent = 0)
        { return UInt64.Parse(Session.Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, Next)[0]); }
        internal void Set(ulong ObjectId, string Property, params string[] Value)
        { var Fields = new List<string> {"set", ObjectId.ToString(), Property}; Fields.AddRange(Value); Session.Gui.Mutate(Fields.ToArray(), Next); }
        internal string[] Get(ulong ObjectId, string Property) { return Session.Gui.Query(new[] {"get", ObjectId.ToString(), Property}); }
        internal string Connect(ulong ObjectId) { return Session.Gui.Mutate(new[] {"connect", ObjectId.ToString()}, Next)[0]; }
        internal void Show(ulong Screen, Runtime.PlayerLifetime Player)
        { Session.Gui.Mutate(new[] {"show", Screen.ToString(), Player.Token, Player.UserId}, Next); }
        internal void Flush()
        { int Guard = 128; while (Session.Gui.HasWork && Guard-- != 0) Session.Gui.FlushOne(Limits.MaxSerializedBytesPerFlush); Check(!Session.Gui.HasWork, "flush converged"); }
        internal string Next() { return (++Registration).ToString(); }
        public void Dispose() { Session.Gui.Dispose(); }
    }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 2B: " + Message); }
    private static void Reject(Action Action, string Message)
    { bool Rejected = false; try { Action(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static Runtime.GuiRenderValue Property(Runtime.GuiRenderElement Element, Runtime.GuiRenderPropertyId Id)
    { foreach (Runtime.GuiRenderProperty Value in Element.Properties) if (Value.Id == Id) return Value.Value; return null; }
    private static Runtime.GuiRenderElement Element(Runtime.GuiRenderElement[] Elements, string Marker)
    { foreach (Runtime.GuiRenderElement Value in Elements) if (Value.ClientId.Contains(Marker)) return Value; throw new Exception("missing render element " + Marker); }
    private static string Token(Runtime.InMemoryGuiBackend Backend, string PlayerToken)
    {
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        for (int Index = Calls.Length - 1; Index >= 0; --Index) {
            Runtime.InMemoryGuiBackend.Call Call = Calls[Index];
            if (Call.Plan == null || Call.Target.ExactPlayerConnectionToken != PlayerToken) continue;
            foreach (Runtime.GuiRenderElement Element in Call.Plan.Elements) {
                Runtime.GuiRenderValue Value = Property(Element, Runtime.GuiRenderPropertyId.ActionCommand);
                if (Value != null) return Value.Text.Substring(Runtime.GuiRetainedWorld.ActionCommand.Length + 1);
            }
        }
        return null;
    }

    internal static void RunModel()
    {
        RunSchemaValuesAndLifecycle();
        RunProjectionAndSynchronization();
        RunActivatedAndPublication();
        RunProjectionBounds();
        Console.WriteLine("[CarbonLuau:GuiFoundation2BModel] PASS typed images, projection, secure actions, publication and bounds");
    }

    private static void RunSchemaValuesAndLifecycle()
    {
        Runtime.GuiSchema.Validate();
        Runtime.GuiClassDescriptor Label = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.ImageLabel);
        Runtime.GuiClassDescriptor Button = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.ImageButton);
        Check(Label.Public && Button.Public && Label.BaseClass == Runtime.GuiClassId.GuiObject && Button.BaseClass == Runtime.GuiClassId.GuiObject &&
            Label.Events.Length == 0 && Button.Events.Length == 1, "public image class descriptors");
        Runtime.GuiValueTypeDescriptor Source = Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.ImageSource);
        Check(Source.Constructors.Length == 5 && Source.Fields.Length == 6 && Source.Fields[0].Name == "Kind", "ImageSource descriptor");

        using (var Value = new Fixture()) {
            ulong Image = Value.Create("ImageLabel"); ulong ImageButton = Value.Create("ImageButton");
            Check(String.Join("|", Value.Get(Image, "Image")) == "imagesource|None" && Value.Get(Image, "ImageColor3")[1] == "1" &&
                Value.Get(Image, "ImageTransparency")[1] == "0", "ImageLabel defaults");
            Check(String.Join("|", Value.Get(ImageButton, "Image")) == "imagesource|None" && Value.Get(ImageButton, "BackgroundTransparency")[1] == "1",
                "ImageButton defaults");
            Value.Set(Image, "Image", "imagesource", "Sprite", "assets/icons/info.png");
            Check(String.Join("|", Value.Get(Image, "Image")) == "imagesource|Sprite|assets/icons/info.png", "sprite retained readback");
            Value.Set(Image, "Image", "imagesource", "Png", "12345678901234567890");
            Value.Set(Image, "Image", "imagesource", "Item", "-932201673", "18446744073709551615");
            Check(String.Join("|", Value.Get(Image, "Image")) == "imagesource|Item|-932201673|18446744073709551615", "precision-safe item skin readback");
            Value.Set(Image, "Image", "imagesource", "SteamAvatar", "76561190000600001");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Sprite", "../secret"), "sprite traversal rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Sprite", "https://example.invalid/x"), "URL-like sprite rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Sprite", "assets/bad\nname.png"), "sprite control character rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Sprite", new string('a', 257)), "sprite byte bound rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Png", "01"), "noncanonical PNG identifier rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Png", "123456789012345678901"), "PNG identifier bound rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Item", "2147483648", ""), "ItemId overflow rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "Item", "1", "18446744073709551616"), "SkinId overflow rejected");
            Reject(() => Value.Set(Image, "Image", "imagesource", "SteamAvatar", "12x"), "malformed Steam identifier rejected");
            ulong Clone = UInt64.Parse(Value.Session.Gui.Mutate(new[] {"clone", Image.ToString()}, Value.Next)[0]);
            Check(String.Join("|", Value.Get(Clone, "Image")) == String.Join("|", Value.Get(Image, "Image")), "Clone preserves typed source");
            Value.Session.Gui.Mutate(new[] {"destroy", ImageButton.ToString()}, Value.Next);
            Reject(() => Value.Get(ImageButton, "Image"), "destroyed ImageButton is stale");
            Runtime.GuiImageSourceValue Ordinary = Runtime.GuiImageSourceValue.Parse(new[] {"imagesource", "Png", "42"}, 0);
            Value.Session.Gui.Dispose();
            Check(Ordinary.Describe() == "imagesource:Png:42", "ordinary immutable ImageSource survives owner retirement");
            Reject(() => Value.Get(Image, "Image"), "domain retirement stales escaped image object references");
        }
    }

    private static void RunProjectionAndSynchronization()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.Add(1); ulong Screen = Value.Create("ScreenGui");
            ulong Sprite = Value.Create("ImageLabel", Screen), Png = Value.Create("ImageLabel", Screen), Item = Value.Create("ImageLabel", Screen);
            ulong Steam = Value.Create("ImageLabel", Screen), None = Value.Create("ImageLabel", Screen), Button = Value.Create("ImageButton", Screen);
            Value.Set(Sprite, "Image", "imagesource", "Sprite", "assets/icons/info.png");
            Value.Set(Png, "Image", "imagesource", "Png", "1234");
            Value.Set(Item, "Image", "imagesource", "Item", "-932201673", "42");
            Value.Set(Steam, "Image", "imagesource", "SteamAvatar", Player.UserId);
            Value.Set(Button, "Image", "imagesource", "Png", "77");
            Value.Show(Screen, Player); Value.Flush();
            Runtime.InMemoryGuiBackend.Call Full = Value.Backend.Calls()[Value.Backend.Calls().Length - 1];
            Check(Full.Plan.Elements.Length == 14, "projection cost includes root, image content and ImageButton overlay");
            Check(Property(Element(Full.Plan.Elements, "i" + Sprite.ToString("x")), Runtime.GuiRenderPropertyId.ImageSource).ImageSource.Kind == Runtime.GuiImageSourceKind.Sprite &&
                Property(Element(Full.Plan.Elements, "i" + Png.ToString("x")), Runtime.GuiRenderPropertyId.ImageSource).ImageSource.Kind == Runtime.GuiImageSourceKind.Png &&
                Property(Element(Full.Plan.Elements, "i" + Item.ToString("x")), Runtime.GuiRenderPropertyId.ImageSource).ImageSource.Kind == Runtime.GuiImageSourceKind.Item &&
                Property(Element(Full.Plan.Elements, "i" + Steam.ToString("x")), Runtime.GuiRenderPropertyId.ImageSource).ImageSource.Kind == Runtime.GuiImageSourceKind.SteamAvatar &&
                Property(Element(Full.Plan.Elements, "i" + None.ToString("x")), Runtime.GuiRenderPropertyId.ImageSource).ImageSource.Kind == Runtime.GuiImageSourceKind.None,
                "all source kinds remain typed in backend-neutral render plan");
            string Json = Runtime.RustCuiBackend.Serialize(Full.Plan.Elements, false, true);
            Check(Json.Contains("\"sprite\":\"assets/icons/info.png\"") && Json.Contains("\"png\":\"1234\"") &&
                Json.Contains("\"itemid\":-932201673") && Json.Contains("\"skinid\":42") && Json.Contains("\"steamid\":\"" + Player.UserId + "\"") &&
                !Json.Contains("\"url\"") && !Json.Contains("http") && !Json.Contains("FileStorage"), "Rust CUI fields are exact and capability-free");

            int Calls = Value.Backend.Calls().Length;
            Value.Set(Sprite, "ImageColor3", "color3", "0.25", "0.5", "0.75"); Value.Flush();
            Runtime.InMemoryGuiBackend.Call Patch = Value.Backend.Calls()[Value.Backend.Calls().Length - 1];
            Check(Value.Backend.Calls().Length == Calls + 1 && Patch.Kind == Runtime.GuiBackendOperationKind.Update && Patch.Patch.Elements.Length == 1 &&
                Patch.Patch.Elements[0].Kind == Runtime.GuiRenderNodeKind.Image, "ImageColor3 uses an image patch");
            Value.Set(Sprite, "ImageTransparency", "number", "0.75"); Value.Flush();
            Check(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Update,
                "ImageTransparency uses an image patch");
            Value.Set(Sprite, "Image", "imagesource", "Png", "999"); Value.Flush();
            Check(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Replace,
                "Image source replacement uses whole-presentation reconciliation");

            Value.Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "image patch failure");
            Value.Set(Sprite, "ImageColor3", "color3", "1", "0", "0"); Value.Session.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush);
            Value.Flush();
            Check(Value.Backend.Calls()[Value.Backend.Calls().Length - 1].Kind == Runtime.GuiBackendOperationKind.Replace,
                "image patch failure converges through full resynchronization");
        }
    }

    private static void RunActivatedAndPublication()
    {
        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime A = Value.Add(10), B = Value.Add(11); ulong Screen = Value.Create("ScreenGui");
            ulong Button = Value.Create("ImageButton", Screen); Value.Connect(Button); Value.Show(Screen, A); Value.Show(Screen, B); Value.Flush();
            Value.Set(Button, "Image", "imagesource", "Png", "123"); Value.Flush();
            string AToken = Token(Value.Backend, A.Token), BToken = Token(Value.Backend, B.Token);
            Check(AToken != null && BToken != null && AToken != BToken && Value.World.Gui.LiveActionCount == 2,
                "ImageButton gets exact-presentation action identities");
            Runtime.InMemoryGuiBackend.Call[] ViewerCalls = Value.Backend.Calls();
            Runtime.GuiImageSourceValue FirstSource = null, SecondSource = null;
            for (int Index = ViewerCalls.Length - 1; Index >= 0; --Index) if (ViewerCalls[Index].Plan != null) {
                Runtime.GuiImageSourceValue Current = Property(Element(ViewerCalls[Index].Plan.Elements, "i" + Button.ToString("x")),
                    Runtime.GuiRenderPropertyId.ImageSource).ImageSource;
                if (ViewerCalls[Index].Target.ExactPlayerConnectionToken == A.Token) FirstSource = Current;
                if (ViewerCalls[Index].Target.ExactPlayerConnectionToken == B.Token) SecondSource = Current;
            }
            Check(FirstSource != null && FirstSource.Equals(SecondSource), "shared multi-viewer image state is canonical retained state");
            Check(!Value.World.AdmitGuiAction(B, AToken) && Value.World.AdmitGuiAction(A, AToken), "ImageButton cross-Player token rejected and exact token admitted");
            Value.Set(Button, "ImageColor3", "color3", "0", "1", "0"); Value.Flush();
            Check(Value.World.AdmitGuiAction(A, AToken), "image color patch retains action authority");
            Value.Set(Button, "Image", "imagesource", "Sprite", "assets/new.png");
            Check(!Value.World.AdmitGuiAction(A, AToken), "structural image source mutation invalidates old action immediately");
            Value.Flush(); string Next = Token(Value.Backend, A.Token); Check(Next != AToken, "image source rebuild rotates action identity");
            Value.Session.Gui.Mutate(new[] {"destroy", Button.ToString()}, Value.Next);
            Check(!Value.World.AdmitGuiAction(A, Next), "Destroy invalidates ImageButton action authority");
        }

        using (var Value = new Fixture()) {
            Runtime.PlayerLifetime Player = Value.Add(20);
            Value.Session.Gui.BeginPublication(); ulong Screen = Value.Create("ScreenGui"), Image = Value.Create("ImageLabel", Screen);
            ulong Button = Value.Create("ImageButton", Screen); Value.Set(Image, "Image", "imagesource", "Png", "7"); Value.Connect(Button); Value.Show(Screen, Player);
            Check(Value.Session.Gui.FlushOne(Value.Limits.MaxSerializedBytesPerFlush) == 0, "provisional images cannot synchronize");
            Value.Session.Gui.RollbackPublication();
            Check(Value.Backend.Calls().Length == 0 && Value.World.Gui.LiveActionCount == 0, "publication rollback leaks no image or token effect");
            Value.Session.Gui.BeginPublication(); Screen = Value.Create("ScreenGui"); Image = Value.Create("ImageLabel", Screen);
            Button = Value.Create("ImageButton", Screen); Value.Set(Image, "Image", "imagesource", "Png", "8"); Value.Connect(Button); Value.Show(Screen, Player);
            Value.Session.Gui.CommitPublication(); Check(Value.Backend.Calls().Length == 0, "publication commit remains deferred until flush");
            Value.Flush(); Check(Value.Backend.Calls().Length == 1 && Value.World.Gui.LiveActionCount == 1, "committed image and ImageButton publish atomically");
        }
    }

    private static void RunProjectionBounds()
    {
        var Config = new Runtime.GuiConfig {MaxProjectedElementsPerScreen = 7};
        using (var Value = new Fixture(Config)) {
            ulong Screen = Value.Create("ScreenGui"); Value.Create("ImageButton", Screen); Value.Create("ImageButton", Screen);
            int Before = Value.Session.Gui.Query(new[] {"children", Screen.ToString()}).Length;
            Reject(() => Value.Create("ImageButton", Screen), "projection overflow rejected before retained mutation");
            Check(Value.Session.Gui.Query(new[] {"children", Screen.ToString()}).Length == Before, "projection overflow rejection is atomic");
        }
        Check(new Runtime.GuiConfig().Validate().MaxProjectedElementsPerScreen == 257,
            "default projection envelope remains within the existing serializer element bound");
        Reject(() => new Runtime.GuiConfig {MaxProjectedElementsPerScreen = 258}.Validate(),
            "projection envelope cannot exceed one render operation");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        const string UserId = "76561190000699999";
        Runtime.PlayerView View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId, Name = "Native Image",
            Connected = true, Send = Value => { }, Permission = Value => true};
        var Players = new Runtime.PlayerDirectory(Id => Id == UserId ? View : null); Runtime.PlayerLifetime Player = Players.Connect(View);
        var Backend = new Runtime.InMemoryGuiBackend(); var World = new Runtime.FacadeWorld(Players, new Registrar(), Backend);
        string Source = "assert(ImageSource.None()==ImageSource.None()); assert(ImageSource.Png('7')==ImageSource.Png('7')); " +
            "assert(ImageSource.Item(-1,'18446744073709551615')==ImageSource.Item(-1,'18446744073709551615')); " +
            "local ok=pcall(function() ImageSource.Sprite('../bad') end); assert(not ok); " +
            "local P=game:GetService('Players'):GetPlayers()[1]; local S=game:GetService('Gui'):Create('ScreenGui'); " +
            "local I=S:Create('ImageLabel'); I.Image=ImageSource.Item(-932201673,'42'); I.ImageColor3=Color3.fromRGB(1,2,3); " +
            "local B=S:Create('ImageButton'); B.Image=ImageSource.SteamAvatar(P.UserId); B.Activated:Connect(function(V) assert(V==P); print('image-click') end); " +
            "S:Show(P); return true";
        Func<Runtime.ScriptSnapshot> Snapshot = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20}, Snapshot, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "native ImageSource constructors and retained image surface"); Host.Drain();
            Check(Backend.Calls().Length != 0 && Backend.Calls()[Backend.Calls().Length - 1].Plan.Elements.Length == 6,
                "native image classes project bounded internal elements");
            string Action = Token(Backend, Player.Token); Check(Action != null && World.AdmitGuiAction(Player, Action), "native ImageButton action admitted");
            string Logs = ""; foreach (Runtime.ExecutionResult Result in Host.Drain()) Logs += Result.Logs;
            Check(Logs == "image-click\n", "native ImageButton Activated enters Luau through existing scheduler path");
            Check(Host.Execute("image.invalid", "assert(not pcall(function() ImageSource.Png('01') end)); assert(not pcall(function() ImageSource.SteamAvatar('18446744073709551616') end)); return true").Status == Runtime.RuntimeStatus.OK,
                "native malformed image identifiers fail as ordinary catchable errors");
            string BeforeReplacement = Action;
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && !World.AdmitGuiAction(Player, BeforeReplacement),
                "healthy replacement stales prior ImageButton authority"); Host.Drain();
            string BeforeRecovery = Token(Backend, Player.Token);
            Check(BeforeRecovery != null && Host.Execute("image.timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT &&
                !World.AdmitGuiAction(Player, BeforeRecovery), "fatal recovery stales prior ImageButton authority");
        }
        Console.WriteLine("[CarbonLuau:GuiFoundation2BNative] PASS immutable values, retained projection and secure ImageButton action");
    }
}
