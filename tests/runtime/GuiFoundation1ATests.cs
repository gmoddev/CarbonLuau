using System;
using System.Collections.Generic;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GuiFoundation1ATests
{
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("GUI Foundation 1A: " + Message); }
    private static void Reject(Action Action, string Message)
    {
        bool Rejected = false; try { Action(); } catch (InvalidOperationException) { Rejected = true; }
        Check(Rejected, Message);
    }
    private static Runtime.GuiPropertyUse Property(Runtime.GuiClassDescriptor Class, Runtime.GuiPropertyId Id)
    {
        foreach (Runtime.GuiPropertyUse Value in Class.Properties) if (Value.Descriptor.Id == Id) return Value;
        return null;
    }
    private static bool Has(Runtime.GuiMethodId[] Values, Runtime.GuiMethodId Expected)
    { foreach (Runtime.GuiMethodId Value in Values) if (Value == Expected) return true; return false; }
    private static bool Has(Runtime.GuiEventId[] Values, Runtime.GuiEventId Expected)
    { foreach (Runtime.GuiEventId Value in Values) if (Value == Expected) return true; return false; }

    internal static void Run(string Repository)
    {
        RunDescriptors();
        Runtime.GuiLimits Limits = RunLimits();
        RunRenderPlans(Limits);
        RunBackend(Limits);
        RunHostCapabilities(Repository);
        Console.WriteLine("[CarbonLuau:GuiFoundation1A] PASS descriptors, limits, deterministic plans, mock backend and host capability record");
    }

    private static void RunDescriptors()
    {
        Runtime.GuiSchema.Validate();
        Runtime.GuiClassDescriptor[] Classes = Runtime.GuiSchema.Classes;
        Check(Classes.Length == 12 && Runtime.GuiSchema.Methods.Length == 12 && Runtime.GuiSchema.Events.Length == 1 &&
            Runtime.GuiSchema.ValueTypes.Length == 6, "schema descriptor counts are complete");
        var ClassIds = new HashSet<Runtime.GuiClassId>(); var ClassNames = new HashSet<string>(StringComparer.Ordinal);
        var PublicNames = new List<string>();
        foreach (Runtime.GuiClassDescriptor Class in Classes) {
            Check(ClassIds.Add(Class.Id) && ClassNames.Add(Class.Name), "class descriptors are unique");
            if (Class.Public) PublicNames.Add(Class.Name);
        }
        PublicNames.Sort(StringComparer.Ordinal);
        Check(String.Join(",", PublicNames) == "Frame,ImageButton,ImageLabel,ScreenGui,ScrollingFrame,TextButton,TextLabel,UIGridLayout,UIListLayout,UIPadding", "additive public class boundary");

        Runtime.GuiClassDescriptor Screen = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.ScreenGui);
        Runtime.GuiClassDescriptor Frame = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.Frame);
        Runtime.GuiClassDescriptor Label = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.TextLabel);
        Runtime.GuiClassDescriptor Button = Runtime.GuiSchema.GetClass(Runtime.GuiClassId.TextButton);
        Check(Screen.BaseClass == Runtime.GuiClassId.GuiNode && Frame.BaseClass == Runtime.GuiClassId.GuiObject,
            "class relationships preserve ScreenGui versus GuiObject");
        Check(Screen.CreationScope == Runtime.GuiCreationScope.GuiService &&
            Frame.CreationScope == (Runtime.GuiCreationScope.GuiService | Runtime.GuiCreationScope.GuiObject),
            "explicit construction scopes prevent object-created ScreenGui");
        Check(Property(Screen, Runtime.GuiPropertyId.Parent) != null && !Property(Screen, Runtime.GuiPropertyId.Parent).Writable,
            "ScreenGui Parent is explicit and read-only");
        Check(Property(Frame, Runtime.GuiPropertyId.Parent).Writable && Property(Frame, Runtime.GuiPropertyId.Size).DefaultValue.Contains("100, 100"),
            "Frame retained property schema");
        Check(Property(Label, Runtime.GuiPropertyId.Text) != null &&
            Property(Label, Runtime.GuiPropertyId.Text).Descriptor.Utf8Limit == Runtime.GuiLimitId.TextUtf8Bytes,
            "TextLabel text schema");
        Check(!Has(Label.Events, Runtime.GuiEventId.Activated) && Has(Button.Events, Runtime.GuiEventId.Activated),
            "Foundation 1 Activated boundary remains intact");
        Check(Has(Screen.Methods, Runtime.GuiMethodId.Show) && Has(Screen.Methods, Runtime.GuiMethodId.Hide) && Has(Screen.Methods, Runtime.GuiMethodId.IsShown),
            "ScreenGui lifecycle methods");
        Check(!Has(Frame.Methods, Runtime.GuiMethodId.Show), "GuiObjects cannot be shown directly");
        Check(Runtime.GuiSchema.GetMethod(Runtime.GuiMethodId.FindFirstChild).Name == "FindFirstChild" &&
            Runtime.GuiSchema.GetEvent(Runtime.GuiEventId.Activated).Name == "Activated" &&
            String.Join(",", Runtime.GuiSchema.GetEvent(Runtime.GuiEventId.Activated).CallbackArguments) == "Player",
            "method and event descriptors are explicit");
        Check(Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.UDim).Fields.Length == 2 &&
            Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.UDim2).Fields.Length == 2 &&
            Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.Vector2).Fields.Length == 2 &&
            Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.Color3).Fields.Length == 3,
            "exact value type field schemas");
        Check(Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.UDim).Constructors.Length == 1 &&
            Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.UDim2).Constructors.Length == 3 &&
            Runtime.GuiSchema.GetValueType(Runtime.GuiValueTypeId.Color3).Constructors.Length == 2,
            "value constructors are explicit descriptors");
        Runtime.GuiPropertyUse[] DetachedProperties = Frame.Properties; DetachedProperties[0] = null;
        Check(Frame.Properties[0] != null, "class descriptor arrays are immutable snapshots");
        string[] DetachedArguments = Runtime.GuiSchema.GetEvent(Runtime.GuiEventId.Activated).CallbackArguments; DetachedArguments[0] = "Changed";
        Check(Runtime.GuiSchema.GetEvent(Runtime.GuiEventId.Activated).CallbackArguments[0] == "Player", "event descriptor arrays are immutable snapshots");
    }

    private static Runtime.GuiLimits RunLimits()
    {
        Runtime.GuiLimits Limits = new Runtime.GuiConfig().Validate();
        Check(Limits.MaxObjectsPerScreen == 128 && Limits.MaxObjectsPerDomain == 1024 && Limits.MaxObjectsGlobal == 8192,
            "retained object defaults");
        Check(Limits.MaxProjectedElementsPerScreen == 257 && Limits.MaxRenderElementsPerOperation == 257 && Limits.MaxSerializedOperationBytes == 65536 &&
            Limits.MaxPresentationSendsPerFlush == 64 && Limits.MaxSerializedBytesPerFlush == 262144 &&
            Limits.GuiFlushBudgetMicroseconds == 1000 && Limits.PatchBatchesBeforeFull == 32,
            "render tuning candidates remain internal configuration");
        Reject(() => new Runtime.GuiConfig {MaxObjectsPerScreen = 0}.Validate(), "zero bound rejected");
        Reject(() => new Runtime.GuiConfig {MaxObjectsPerScreen = 129, MaxObjectsPerDomain = 128}.Validate(), "scope hierarchy rejected");
        Reject(() => new Runtime.GuiConfig {MaxSerializedOperationBytes = 65537, MaxSerializedBytesPerFlush = 65536}.Validate(),
            "operation must fit flush byte bound");
        Reject(() => new Runtime.GuiConfig {MaxRenderElementsPerOperation = 127}.Validate(), "render plan must cover retained objects");
        return Limits;
    }

    private static Runtime.GuiRenderElement Root(Runtime.GuiLimits Limits, params Runtime.GuiRenderProperty[] Properties)
    { return new Runtime.GuiRenderElement("root", null, Runtime.GuiRenderNodeKind.Container, Limits, Properties); }
    private static Runtime.GuiRenderElement Child(Runtime.GuiLimits Limits, params Runtime.GuiRenderProperty[] Properties)
    { return new Runtime.GuiRenderElement("child", "root", Runtime.GuiRenderNodeKind.Text, Limits, Properties); }
    private static Runtime.GuiRenderProperty Text(string Value)
    { return new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Text, Runtime.GuiRenderValue.FromString(Value)); }
    private static Runtime.GuiRenderProperty Visible(bool Value)
    { return new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.Visible, Runtime.GuiRenderValue.FromBoolean(Value)); }

    private static void RunRenderPlans(Runtime.GuiLimits Limits)
    {
        var FirstRoot = Root(Limits,
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.BackgroundColor, Runtime.GuiRenderValue.FromColor(1, 0.5, 0, 1)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.AnchorMin, Runtime.GuiRenderValue.FromVector(0, 0)));
        var SecondRoot = Root(Limits,
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.AnchorMin, Runtime.GuiRenderValue.FromVector(0, 0)),
            new Runtime.GuiRenderProperty(Runtime.GuiRenderPropertyId.BackgroundColor, Runtime.GuiRenderValue.FromColor(1, 0.5, 0, 1)));
        var First = new Runtime.GuiRenderPlan(Limits, 128, FirstRoot, Child(Limits, Text("hello"), Visible(true)));
        var Second = new Runtime.GuiRenderPlan(Limits, 128, SecondRoot, Child(Limits, Visible(true), Text("hello")));
        Check(First.Describe() == Second.Describe(), "property input order canonicalizes deterministically");
        Check(First.Describe().StartsWith("FULL|128\n4:root|-|1|1=5:0,0|7=6:1,0.5,0,1", StringComparison.Ordinal),
            "full plan has stable invariant representation");
        Runtime.GuiRenderElement[] DetachedElements = First.Elements; DetachedElements[0] = null;
        Check(First.Elements[0] != null, "render plan arrays are immutable snapshots");
        Reject(() => new Runtime.GuiRenderPlan(Limits, 1, Child(Limits, Text("bad")), Root(Limits)), "child-before-parent rejected");
        Reject(() => new Runtime.GuiRenderPlan(Limits, 1, Root(Limits), Root(Limits)), "duplicate render id rejected");
        Reject(() => new Runtime.GuiRenderPlan(Limits, Limits.MaxSerializedOperationBytes + 1, Root(Limits)), "oversized full request rejected");
        Reject(() => new Runtime.GuiRenderElement("root", null, Runtime.GuiRenderNodeKind.Container, Limits, Visible(true), Visible(false)),
            "duplicate render property rejected");
        Reject(() => Root(Limits, Text(new string('x', Limits.MaxSerializedOperationBytes))), "oversized render string rejected independently of estimates");
        Reject(() => Root(Limits, Text("\uD800")), "invalid UTF-16 cannot enter the UTF-8 render contract");
        Reject(() => Runtime.GuiRenderValue.FromNumber(Double.NaN), "non-finite render number rejected");
        var Patch = new Runtime.GuiRenderPatch(Limits, 32, Child(Limits, Text("new")));
        Check(Patch.Describe().StartsWith("PATCH|32\n", StringComparison.Ordinal), "patch has deterministic representation");
        Reject(() => new Runtime.GuiRenderPatch(Limits, 1, Child(Limits)), "empty patch element rejected");
    }

    private static void RunBackend(Runtime.GuiLimits Limits)
    {
        var Backend = new Runtime.InMemoryGuiBackend();
        var Target = new Runtime.GuiBackendTarget("connection-7", "76561190000000007", "root-9");
        var Plan = new Runtime.GuiRenderPlan(Limits, 64, Root(Limits, Visible(true)));
        var Patch = new Runtime.GuiRenderPatch(Limits, 16, Root(Limits, Visible(false)));
        Check(Backend.Replace(Target, Plan).Accepted && Backend.IsLive(Target) && Object.ReferenceEquals(Backend.CurrentPlan(Target), Plan),
            "full replace publishes deterministic mock state");
        Check(Backend.Update(Target, Patch).Accepted && Object.ReferenceEquals(Backend.LastPatch(Target), Patch),
            "patch updates existing mock state");
        Backend.FailNext(Runtime.GuiBackendOperationKind.Update, Runtime.GuiBackendResultCode.SendFailed, "injected update failure");
        var FailedPatch = new Runtime.GuiRenderPatch(Limits, 16, Root(Limits, Visible(true)));
        Runtime.GuiBackendResult Failure = Backend.Update(Target, FailedPatch);
        Check(!Failure.Accepted && Failure.Code == Runtime.GuiBackendResultCode.SendFailed &&
            Object.ReferenceEquals(Backend.LastPatch(Target), Patch), "injected failure is deterministic and does not mutate state");
        Check(Backend.Destroy(Target).Accepted && !Backend.IsLive(Target), "destroy removes mock state");
        Check(!Backend.Update(Target, Patch).Accepted, "update of missing target reports controlled unavailability");
        Runtime.InMemoryGuiBackend.Call[] Calls = Backend.Calls();
        Check(Calls.Length == 5, "all attempts recorded");
        for (int Index = 0; Index < Calls.Length; ++Index) Check(Calls[Index].Sequence == Index + 1, "strict backend call ordering");
        Check(Calls[0].Kind == Runtime.GuiBackendOperationKind.Replace && Calls[1].Kind == Runtime.GuiBackendOperationKind.Update &&
            Calls[2].Kind == Runtime.GuiBackendOperationKind.Update && Calls[3].Kind == Runtime.GuiBackendOperationKind.Destroy,
            "mock operation order is stable");
    }

    private static void RunHostCapabilities(string Repository)
    {
        Check(Runtime.GuiHostCapabilities.TargetCarbonVersion == "2.0.259.0" && Runtime.GuiHostCapabilities.TargetRustProtocol == "2633.288.1" &&
            Runtime.GuiHostCapabilities.TargetRustSteamBuild == "25230300",
            "target host identity");
        Check(Runtime.GuiHostCapabilities.OrderedCreate && Runtime.GuiHostCapabilities.UpdateExisting && Runtime.GuiHostCapabilities.DestroyBeforeCreate &&
            Runtime.GuiHostCapabilities.RectAnchorsAndOffsets && Runtime.GuiHostCapabilities.UpdateParent &&
            Runtime.GuiHostCapabilities.UpdateSiblingIndex && Runtime.GuiHostCapabilities.ButtonRunsServerCommand &&
            Runtime.GuiHostCapabilities.NeedsCursor && !Runtime.GuiHostCapabilities.ApplicationAcknowledgement,
            "required host capability record");
        if (Repository == null) return;
        string Documentation = File.ReadAllText(Path.Combine(Repository, "docs", "GuiFoundation1A.md"));
        foreach (string Identity in new[] {Runtime.GuiHostCapabilities.TargetCarbonVersion, Runtime.GuiHostCapabilities.TargetRustProtocol,
            Runtime.GuiHostCapabilities.TargetRustSteamBuild,
            Runtime.GuiHostCapabilities.CarbonSourceRevision, Runtime.GuiHostCapabilities.RustCommunitySourceRevision,
            Runtime.GuiHostCapabilities.OxideAdapterSourceRevision})
            Check(Documentation.Contains(Identity), "host capability documentation contains " + Identity);
    }
}
