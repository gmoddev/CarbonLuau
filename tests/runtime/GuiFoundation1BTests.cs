using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation1BTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        public readonly List<Runtime.FacadeSession> Sessions = new List<Runtime.FacadeSession>();
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        { if (Previous != null) Sessions.Remove(Previous); if (Next != null) Sessions.Add(Next); }
    }
    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("GUI Foundation 1B: " + Message); }
    private static void Reject(Action Value, string Message)
    { bool Rejected = false; try { Value(); } catch (InvalidOperationException) { Rejected = true; } Check(Rejected, Message); }
    private static ulong Id(string[] Value) { return UInt64.Parse(Value[0]); }
    private static string[] Create(Runtime.GuiRetainedRegistry Gui, ref ulong Registration, string ClassName, ulong Parent = 0)
    { string Next = (++Registration).ToString(); return Gui.Mutate(new[] {"create", Parent == 0 ? "" : Parent.ToString(), ClassName}, () => Next); }
    private static void Set(Runtime.GuiRetainedRegistry Gui, ref ulong Registration, ulong Object, string Property, params string[] Value)
    {
        var Fields = new List<string> {"set", Object.ToString(), Property}; Fields.AddRange(Value);
        string Next = (++Registration).ToString(); Gui.Mutate(Fields.ToArray(), () => Next);
    }
    private static string[] Query(Runtime.GuiRetainedRegistry Gui, params string[] Fields) { return Gui.Query(Fields); }

    internal static void RunModel()
    {
        var Limits = new Runtime.GuiConfig().Validate(); var World = new Runtime.GuiRetainedWorld(Limits);
        var Gui = new Runtime.GuiRetainedRegistry(World, 10, 20); ulong Registration = 0;
        ulong ScreenA = Id(Create(Gui, ref Registration, "ScreenGui"));
        ulong Frame = Id(Create(Gui, ref Registration, "Frame", ScreenA));
        ulong First = Id(Create(Gui, ref Registration, "TextLabel", Frame));
        ulong Second = Id(Create(Gui, ref Registration, "TextLabel", Frame));
        Check(String.Join("|", Query(Gui, "get", Frame.ToString(), "Name")) == "string|Frame" &&
            String.Join("|", Query(Gui, "get", Frame.ToString(), "Size")) == "udim2|0|100|0|100" &&
            String.Join("|", Query(Gui, "get", First.ToString(), "BackgroundTransparency")) == "number|1" &&
            String.Join("|", Query(Gui, "get", Second.ToString(), "TextSize")) == "integer|14", "class defaults");

        Set(Gui, ref Registration, First, "Name", "string", "duplicate"); Set(Gui, ref Registration, Second, "Name", "string", "duplicate");
        string[] Children = Query(Gui, "children", Frame.ToString());
        Check(Children[0] == First.ToString() && Children[2] == Second.ToString() && Query(Gui, "find", Frame.ToString(), "duplicate")[0] == First.ToString(),
            "duplicate names and retained lookup order");
        Check(Query(Gui, "isa", ScreenA.ToString(), "GuiObject")[0] == "0" && Query(Gui, "isa", Frame.ToString(), "GuiObject")[0] == "1", "IsA hierarchy");

        Set(Gui, ref Registration, Frame, "Name", "string", "Panel");
        Set(Gui, ref Registration, Frame, "Position", "udim2", "0.5", "12", "-0.5", "-12");
        Set(Gui, ref Registration, Frame, "Size", "udim2", "0", "640", "0", "480");
        Set(Gui, ref Registration, Frame, "AnchorPoint", "vector2", "0.5", "1");
        Set(Gui, ref Registration, Frame, "Visible", "boolean", "0");
        Set(Gui, ref Registration, Frame, "BackgroundColor3", "color3", "0.1", "0.2", "0.3");
        Set(Gui, ref Registration, Frame, "BackgroundTransparency", "number", "0.25");
        Set(Gui, ref Registration, Frame, "ZIndex", "integer", "1000");
        Set(Gui, ref Registration, First, "Text", "string", "hello");
        Set(Gui, ref Registration, First, "TextColor3", "color3", "1", "0", "0");
        Set(Gui, ref Registration, First, "TextTransparency", "number", "0.5");
        Set(Gui, ref Registration, First, "TextSize", "integer", "128");
        Set(Gui, ref Registration, First, "TextXAlignment", "string", "Left");
        Set(Gui, ref Registration, First, "TextYAlignment", "string", "Bottom");
        Check(Query(Gui, "get", Frame.ToString(), "Visible")[1] == "0" && Query(Gui, "get", First.ToString(), "Text")[1] == "hello", "all writable property families");
        Reject(() => Set(Gui, ref Registration, Frame, "ClassName", "string", "TextLabel"), "ClassName read-only");
        Reject(() => Set(Gui, ref Registration, Frame, "AnchorPoint", "vector2", "1.1", "0"), "AnchorPoint range");
        Reject(() => Set(Gui, ref Registration, Frame, "BackgroundTransparency", "number", "NaN"), "finite number requirement");
        Reject(() => Set(Gui, ref Registration, Frame, "ZIndex", "integer", "1001"), "ZIndex range");
        Reject(() => Set(Gui, ref Registration, First, "TextXAlignment", "string", "Justify"), "alignment allowlist");
        Reject(() => Set(Gui, ref Registration, Frame, "Text", "string", "wrong class"), "class property relationship");

        ulong Child = Id(Create(Gui, ref Registration, "Frame", Frame));
        Reject(() => Set(Gui, ref Registration, Frame, "Parent", "object", Child.ToString()), "cycle rejected transactionally");
        Check(Query(Gui, "get", Frame.ToString(), "Parent")[1] == ScreenA.ToString(), "cycle rejection leaves parent unchanged");
        Set(Gui, ref Registration, Child, "Parent", "nil"); Check(Query(Gui, "get", Child.ToString(), "Parent")[0] == "nil", "detach");
        ulong ScreenB = Id(Create(Gui, ref Registration, "ScreenGui"));
        Set(Gui, ref Registration, Frame, "Parent", "object", ScreenB.ToString());
        Check(Query(Gui, "get", Frame.ToString(), "Parent")[1] == ScreenB.ToString() && Query(Gui, "children", ScreenA.ToString()).Length == 0, "cross-ScreenGui reparent");

        ulong Button = Id(Create(Gui, ref Registration, "TextButton", Frame));
        string Connection = Gui.Mutate(new[] {"connect", Button.ToString()}, () => (++Registration).ToString())[0];
        ulong Clone = Id(Gui.Mutate(new[] {"clone", Frame.ToString()}, () => (++Registration).ToString()));
        Check(Query(Gui, "get", Clone.ToString(), "Parent")[0] == "nil" && Query(Gui, "children", Clone.ToString()).Length == 6 && Gui.ConnectionCount == 1,
            "deep clone copies hierarchy/order without parent or connections");
        string CloneButton = Query(Gui, "children", Clone.ToString())[4];
        Gui.Mutate(new[] {"connect", CloneButton}, () => (++Registration).ToString()); Check(Gui.ConnectionCount == 2, "clone button owns fresh Signal");
        string[] Removed = Gui.Mutate(new[] {"destroy", Frame.ToString()}, () => (++Registration).ToString());
        Check(Array.IndexOf(Removed, Connection) >= 0 && Gui.ConnectionCount == 1, "recursive Destroy disconnects original subtree Signals");
        Gui.Mutate(new[] {"destroy", Frame.ToString()}, () => (++Registration).ToString());
        Reject(() => Query(Gui, "get", Frame.ToString(), "Name"), "destroyed reference rejects use");

        Set(Gui, ref Registration, Clone, "Name", "string", "committed");
        Gui.BeginPublication(); Set(Gui, ref Registration, Clone, "Name", "string", "staged");
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "staged", "publication read-your-writes"); Gui.RollbackPublication();
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "committed", "publication rollback leaves committed state");
        Gui.BeginPublication(); Set(Gui, ref Registration, Clone, "Name", "string", "outer");
        Gui.BeginPublication(); Set(Gui, ref Registration, Clone, "Name", "string", "inner"); Gui.RollbackPublication();
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "outer", "nested rollback restores parent overlay"); Gui.CommitPublication();
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "outer", "outer publication commit");

        var Foreign = new Runtime.GuiRetainedRegistry(World, 10, 30);
        Gui.BeginPublication(); Set(Gui, ref Registration, Clone, "Name", "string", "foreign staged");
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "foreign staged", "foreign publication sees A overlay"); Gui.RollbackPublication();
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "outer", "foreign failure leaks no A mutation");
        Gui.BeginPublication(); Set(Gui, ref Registration, Clone, "Name", "string", "foreign committed"); Gui.CommitPublication();
        Check(Query(Gui, "get", Clone.ToString(), "Name")[1] == "foreign committed", "foreign success publishes atomically");
        Gui.BeginPublication(); Set(Gui, ref Registration, Clone, "Name", "string", "must not publish"); Gui.Dispose();
        Reject(() => Gui.CommitPublication(), "owner retirement rejects publication commit");
        Foreign.Dispose(); Check(World.LiveObjects == 0, "domain teardown releases all GUI objects");

        var TightConfig = new Runtime.GuiConfig {MaxObjectsPerScreen = 1, MaxTreeDepth = 1, MaxChildrenPerObject = 1,
            MaxObjectsPerDomain = 1, MaxObjectsGlobal = 1, MaxScreensPerDomain = 1, MaxButtonsPerScreen = 1,
            MaxTrackedDirtyObjectsPerDomain = 1, MaxCloneObjects = 1, MaxCloneDepth = 1, MaxRenderElementsPerOperation = 1};
        var TightWorld = new Runtime.GuiRetainedWorld(TightConfig.Validate());
        var TightA = new Runtime.GuiRetainedRegistry(TightWorld, 1, 1); var TightB = new Runtime.GuiRetainedRegistry(TightWorld, 1, 2); ulong TightRegistration = 0;
        Create(TightA, ref TightRegistration, "ScreenGui");
        Reject(() => Create(TightA, ref TightRegistration, "Frame"), "domain object bound");
        Reject(() => Create(TightB, ref TightRegistration, "Frame"), "global object bound");
        TightA.Dispose(); Create(TightB, ref TightRegistration, "Frame"); TightB.Dispose();

        var BoundGui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits), 2, 3); ulong BoundRegistration = 0;
        ulong BoundRoot = Id(Create(BoundGui, ref BoundRegistration, "Frame"));
        ulong FirstBoundChild = 0;
        for (int Index = 0; Index < Limits.MaxChildrenPerObject; ++Index) {
            ulong Created = Id(Create(BoundGui, ref BoundRegistration, "Frame", BoundRoot)); if (Index == 0) FirstBoundChild = Created;
        }
        Reject(() => Create(BoundGui, ref BoundRegistration, "Frame", BoundRoot), "children bound");
        for (int Index = 0; Index < Limits.MaxChildrenPerObject; ++Index) Create(BoundGui, ref BoundRegistration, "Frame", FirstBoundChild);
        Reject(() => BoundGui.Mutate(new[] {"clone", BoundRoot.ToString()}, () => "clone-bound"), "clone object bound");
        Reject(() => Set(BoundGui, ref BoundRegistration, BoundRoot, "Name", "string", new string('n', Limits.MaxNameUtf8Bytes + 1)), "name byte bound");
        ulong Label = Id(Create(BoundGui, ref BoundRegistration, "TextLabel"));
        Reject(() => Set(BoundGui, ref BoundRegistration, Label, "Text", "string", new string('t', Limits.MaxTextUtf8Bytes + 1)), "text byte bound");
        ulong SignalButton = Id(Create(BoundGui, ref BoundRegistration, "TextButton"));
        for (int Index = 0; Index < Limits.MaxSignalConnectionsPerButton; ++Index)
            BoundGui.Mutate(new[] {"connect", SignalButton.ToString()}, () => "signal-" + Index);
        Reject(() => BoundGui.Mutate(new[] {"connect", SignalButton.ToString()}, () => "signal-over"), "connections per button bound");
        BoundGui.Dispose();

        var ScreenConfig = new Runtime.GuiConfig {MaxObjectsPerScreen = 3, MaxTreeDepth = 3, MaxChildrenPerObject = 3,
            MaxObjectsPerDomain = 10, MaxObjectsGlobal = 10, MaxScreensPerDomain = 1, MaxButtonsPerScreen = 3,
            MaxTrackedDirtyObjectsPerDomain = 10, MaxCloneObjects = 3, MaxCloneDepth = 3, MaxRenderElementsPerOperation = 5,
            MaxTextUtf8Bytes = 4, MaxTextUtf8BytesPerScreen = 6};
        var ScreenGui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(ScreenConfig.Validate()), 4, 5); ulong ScreenRegistration = 0;
        ulong LimitedScreen = Id(Create(ScreenGui, ref ScreenRegistration, "ScreenGui"));
        ulong LimitedLabelA = Id(Create(ScreenGui, ref ScreenRegistration, "TextLabel", LimitedScreen));
        ulong LimitedLabelB = Id(Create(ScreenGui, ref ScreenRegistration, "TextLabel", LimitedScreen));
        Set(ScreenGui, ref ScreenRegistration, LimitedLabelA, "Text", "string", "1234");
        Reject(() => Set(ScreenGui, ref ScreenRegistration, LimitedLabelB, "Text", "string", "123"), "aggregate ScreenGui text bound");
        Set(ScreenGui, ref ScreenRegistration, LimitedLabelB, "Text", "string", "12");
        Reject(() => Create(ScreenGui, ref ScreenRegistration, "Frame", LimitedScreen), "objects per ScreenGui bound");
        Reject(() => Create(ScreenGui, ref ScreenRegistration, "ScreenGui"), "screens per domain bound"); ScreenGui.Dispose();

        var DepthGui = new Runtime.GuiRetainedRegistry(new Runtime.GuiRetainedWorld(Limits), 6, 7); ulong DepthRegistration = 0;
        ulong DepthParent = Id(Create(DepthGui, ref DepthRegistration, "Frame"));
        for (int Depth = 1; Depth < Limits.MaxTreeDepth; ++Depth) DepthParent = Id(Create(DepthGui, ref DepthRegistration, "Frame", DepthParent));
        ulong FinalDepthParent = DepthParent;
        Reject(() => Create(DepthGui, ref DepthRegistration, "Frame", FinalDepthParent), "tree depth bound"); DepthGui.Dispose();
        Console.WriteLine("[CarbonLuau:GuiFoundation1BModel] PASS retained trees, properties, bounds, lifecycle, Signals and publication journal");
    }

    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        var Registrar = new Registrar(); var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(IdValue => null), Registrar);
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        string Source = "", FailureModule = null;
        Func<Runtime.ScriptSnapshot> Snapshot = () => {
            var Value = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source}; Value.Modules.Add("state", "return {}");
            if (FailureModule != null) Value.Modules.Add("badgui", FailureModule); return Value;
        };
        using (var Host = new Runtime.ScriptHost(Native, Config, Snapshot, World)) {
            Source = "local State=require('state'); local Gui=game:GetService('Gui'); assert(Gui==game:GetService('Gui')); " +
                "assert(typeof(UDim.new(1,2))=='userdata' and UDim.new(-0,0)==UDim.new(0,-0)); " +
                "local D=UDim2.new(.5,10,.25,20); assert(D.X.Scale==.5 and D.Y.Offset==20 and D==UDim2.new(.5,10,.25,20)); " +
                "assert(not pcall(function() D.X=UDim.new(0,0) end)); " +
                "assert(UDim2.fromScale(1,2)==UDim2.new(1,0,2,0) and UDim2.fromOffset(3,4)==UDim2.new(0,3,0,4)); " +
                "assert(Vector2.new(1,2)==Vector2.new(1,2)); local C=Color3.fromRGB(255,128,0); assert(C.R==1 and C.G>0.5 and C.G<0.502 and C.B==0); " +
                "assert(not pcall(function() UDim.new(0/0,0) end) and not pcall(function() Color3.fromRGB(1.5,0,0) end)); " +
                "local Screen=Gui:Create('ScreenGui'); local Frame=Screen:Create('Frame'); local A=Frame:Create('TextLabel'); local B=Frame:Create('TextLabel'); " +
                "assert(not pcall(function() Screen:Create('ScreenGui') end)); local Detached=Gui:Create('Frame'); assert(Detached.Parent==nil); Detached:Destroy(); " +
                "A.Name='same'; B.Name='same'; assert(Frame.ClassName=='Frame' and Frame.Parent==Screen and Screen.Parent==nil); " +
                "assert(Frame:IsA('GuiObject') and not Screen:IsA('GuiObject')); local Children=Frame:GetChildren(); assert(Children[1]==A and Children[2]==B and Frame:FindFirstChild('same')==A); " +
                "Frame.Position=D; Frame.AnchorPoint=Vector2.new(.5,1); Frame.BackgroundColor3=C; Frame.Visible=false; Frame.ZIndex=9; " +
                "local Button=Frame:Create('TextButton'); assert(Button.Size==UDim2.fromOffset(100,36) and Button.Text=='' and Button.TextSize==14); local Connection=Button.Activated:Connect(function() error('no ingress in 1B') end); " +
                "local Copy=Frame:Clone(); assert(Copy.Parent==nil and #Copy:GetChildren()==3 and Copy~=Frame); State.Screen=Screen; State.Frame=Frame; State.Button=Button; State.Connection=Connection; State.Copy=Copy";
            Runtime.ExecutionResult Loaded = Host.Reload(); Check(Loaded.Status == Runtime.RuntimeStatus.OK, "public facade/value load: " + Loaded.Error);
            Check(World.Active.Gui.LiveObjectCount == 9 && World.Active.Gui.ConnectionCount == 1, "native facade retained state counts");
            Runtime.ExecutionResult Active = Host.Execute("gui.active", "local S=require('state'); S.Frame.Parent=nil; assert(S.Frame.Parent==nil); S.Frame.Parent=S.Screen; S.Button:Destroy(); S.Button:Destroy(); assert(not pcall(function() return S.Button.Name end)); S.Connection:Disconnect()");
            Check(Active.Status == Runtime.RuntimeStatus.OK && World.Active.Gui.ConnectionCount == 0, "detach/reparent/destroy through userdata");

            int CommittedObjects = World.Gui.LiveObjects; Runtime.FacadeSession Previous = World.Active;
            Source = "local G=game:GetService('Gui'); G:Create('ScreenGui'):Create('Frame'); error('candidate rollback')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && World.Active == Previous && World.Gui.LiveObjects == CommittedObjects, "failed candidate publishes no GUI state");

            FailureModule = "local S=require('state'); S.Frame.Name='leak'; S.Frame:Create('Frame'); error('caught GUI failure')";
            Source = "local S=require('state'); local G=game:GetService('Gui'); S.Frame=G:Create('Frame'); S.Frame.Name='before'; assert(not pcall(require,'badgui')); assert(S.Frame.Name=='before' and #S.Frame:GetChildren()==0)";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && World.Active.Gui.LiveObjectCount == 1, "caught failed module rolls GUI journal back");
            FailureModule = null;
        }
        Check(World.Gui.LiveObjects == 0, "native domain teardown releases GUI state");
        RunForeignNative(Native);
        Console.WriteLine("[CarbonLuau:GuiFoundation1BNative] PASS userdata, values, public object surface, rollback, foreign ownership and teardown");
    }

    private static void RunForeignNative(Runtime.NativeRuntime Native)
    {
        var Registrar = new Registrar(); var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(IdValue => null), Registrar);
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object Provider = new object();
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "foreign root baseline");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                byte[] A = Archive("{\"schema\":1,\"id\":\"guia\",\"version\":\"1.0.0\",\"main\":\"api\"}",
                    "local O=require('api'); game:GetService('Commands'):Register('guicheck',{},function() print(O.Frame.Name) end)",
                    "local G=game:GetService('Gui'); local S=G:Create('ScreenGui'); local F=S:Create('Frame'); F.Name='original'; return {Frame=F,Color=Color3.new(.25,.5,.75)}");
                string[] ARegistration = Registry.RegisterArchive(Provider, A); while (Registry.HasPending) Registry.ProcessOne();
                Check(Registry.Status(Provider, ARegistration[1])[2] == "Active", "foreign owner A active");
                byte[] FailedB = Archive("{\"schema\":1,\"id\":\"guibfail\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":[\"guia\"],\"optional\":[]}}",
                    "local A=require('@guia'); assert(A.Color==Color3.new(.25,.5,.75)); A.Frame.Name='staged'; assert(A.Frame.Name=='staged'); error('reject B')", null);
                string[] Failed = Registry.RegisterArchive(Provider, FailedB); while (Registry.HasPending) Registry.ProcessOne();
                Check(Registry.Status(Provider, Failed[1])[2] == "Failed", "foreign B failure rejected");
                Runtime.FacadeSession ASession = FindCommand(World, "guicheck"); Check(ASession.Invoke("guicheck", "0", new string[0]) == false, "fixture has no player command admission");
                Check(ASession.Gui.Query(new[] {"find", "1", "impossible"}).Length == 0, "A registry remains live after failed B");
                Check(ReadOnlyFrameName(ASession) == "original", "failed foreign B leaked no A mutation");

                byte[] SuccessB = Archive("{\"schema\":1,\"id\":\"guibok\",\"version\":\"1.0.0\",\"dependencies\":{\"required\":[\"guia\"],\"optional\":[]}}",
                    "local A=require('@guia'); local Local=game:GetService('Gui'):Create('Frame'); assert(not pcall(function() A.Frame.Parent=Local end)); A.Frame.Name='committed'; assert(A.Frame.Name=='committed')", null);
                string[] Success = Registry.RegisterArchive(Provider, SuccessB); while (Registry.HasPending) Registry.ProcessOne();
                Check(Registry.Status(Provider, Success[1])[2] == "Active" && ReadOnlyFrameName(ASession) == "committed", "successful foreign B publishes A mutation");
            }
        }
        Check(World.Gui.LiveObjects == 0, "foreign addon teardown returns GUI global count to zero");
    }

    private static Runtime.FacadeSession FindCommand(Runtime.FacadeWorld World, string Name)
    { foreach (Runtime.FacadeSession Session in World.Sessions()) if (Session.Commands.ContainsKey(Name)) return Session; throw new Exception("GUI Foundation 1B: command owner missing"); }
    private static string ReadOnlyFrameName(Runtime.FacadeSession Session)
    {
        for (ulong IdValue = 1; IdValue <= 8; ++IdValue) {
            Runtime.GuiClassId ClassId;
            var Identity = new Runtime.GuiObjectIdentity((ulong)Session.VmGenerationId, (ulong)Session.DomainLifetimeId, IdValue);
            if (Session.Gui.TryGetClass(Identity, out ClassId) && ClassId == Runtime.GuiClassId.Frame) return Session.Gui.Query(new[] {"get", IdValue.ToString(), "Name"})[1];
        }
        throw new Exception("GUI Foundation 1B: A frame missing");
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
    private static void Write(ZipArchive Zip, string Name, string Text)
    { using (Stream Stream = Zip.CreateEntry(Name).Open()) { byte[] Bytes = Encoding.UTF8.GetBytes(Text); Stream.Write(Bytes, 0, Bytes.Length); } }
}
