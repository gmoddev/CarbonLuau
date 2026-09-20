using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal enum GuiClassId
        { GuiNode = 1, GuiObject = 2, ScreenGui = 3, Frame = 4, TextLabel = 5, TextButton = 6, UIListLayout = 7, UIPadding = 8, ImageLabel = 9, ImageButton = 10 }
        internal enum GuiValueTypeId { UDim = 1, UDim2 = 2, Vector2 = 3, Color3 = 4, ImageSource = 5 }
        internal enum GuiPropertyId
        {
            Name = 1, ClassName = 2, Parent = 3, Position = 4, Size = 5, AnchorPoint = 6, Visible = 7,
            BackgroundColor3 = 8, BackgroundTransparency = 9, ZIndex = 10, Text = 11, TextColor3 = 12,
            TextTransparency = 13, TextSize = 14, TextXAlignment = 15, TextYAlignment = 16,
            LayoutOrder = 17, Padding = 18, FillDirection = 19, HorizontalAlignment = 20, VerticalAlignment = 21,
            PaddingTop = 22, PaddingBottom = 23, PaddingLeft = 24, PaddingRight = 25,
            Image = 26, ImageColor3 = 27, ImageTransparency = 28,
            LayoutProjection = 100, ContentProjection = 101
        }
        internal enum GuiMethodId
        { Create = 1, Clone = 2, Destroy = 3, GetChildren = 4, FindFirstChild = 5, IsA = 6, Show = 7, Hide = 8, IsShown = 9 }
        internal enum GuiEventId { Activated = 1 }
        [Flags] internal enum GuiCreationScope { None = 0, GuiService = 1, GuiObject = 2 }
        internal enum GuiLimitId { None = 0, NameUtf8Bytes = 1, TextUtf8Bytes = 2 }
        internal enum GuiValueKind { String, Boolean, Integer, Number, GuiNodeReference, UDim, UDim2, Vector2, Color3, ImageSource }
        internal enum GuiMutationKind { Metadata, Patchable, LayoutAffecting, Structural }

        internal sealed class GuiPropertyDescriptor
        {
            internal readonly GuiPropertyId Id; internal readonly string Name; internal readonly GuiValueKind ValueKind;
            internal readonly GuiMutationKind MutationKind; internal readonly double? Minimum, Maximum;
            internal readonly GuiLimitId Utf8Limit; private readonly string[] AllowedValueData;
            internal string[] AllowedValues { get { return (string[])AllowedValueData.Clone(); } }
            internal GuiPropertyDescriptor(GuiPropertyId Id, string Name, GuiValueKind ValueKind, GuiMutationKind MutationKind,
                double? Minimum = null, double? Maximum = null, GuiLimitId Utf8Limit = GuiLimitId.None, params string[] AllowedValues)
            {
                this.Id = Id; this.Name = Name; this.ValueKind = ValueKind; this.MutationKind = MutationKind;
                this.Minimum = Minimum; this.Maximum = Maximum; this.Utf8Limit = Utf8Limit;
                AllowedValueData = AllowedValues == null ? new string[0] : (string[])AllowedValues.Clone();
            }
        }

        internal sealed class GuiPropertyUse
        {
            internal readonly GuiPropertyDescriptor Descriptor; internal readonly bool Writable; internal readonly string DefaultValue;
            internal GuiPropertyUse(GuiPropertyDescriptor Descriptor, bool Writable, string DefaultValue)
            { this.Descriptor = Descriptor; this.Writable = Writable; this.DefaultValue = DefaultValue; }
        }

        internal sealed class GuiClassDescriptor
        {
            internal readonly GuiClassId Id; internal readonly string Name; internal readonly GuiClassId? BaseClass;
            internal readonly bool Public, CanHaveChildren; internal readonly GuiCreationScope CreationScope;
            internal bool Constructible { get { return CreationScope != GuiCreationScope.None; } }
            private readonly GuiPropertyUse[] PropertyValues; private readonly GuiMethodId[] MethodValues; private readonly GuiEventId[] EventValues;
            internal GuiPropertyUse[] Properties { get { return (GuiPropertyUse[])PropertyValues.Clone(); } }
            internal GuiMethodId[] Methods { get { return (GuiMethodId[])MethodValues.Clone(); } }
            internal GuiEventId[] Events { get { return (GuiEventId[])EventValues.Clone(); } }
            internal GuiClassDescriptor(GuiClassId Id, string Name, GuiClassId? BaseClass, bool Public, GuiCreationScope CreationScope,
                bool CanHaveChildren, GuiPropertyUse[] Properties, GuiMethodId[] Methods, GuiEventId[] Events)
            {
                this.Id = Id; this.Name = Name; this.BaseClass = BaseClass; this.Public = Public;
                this.CreationScope = CreationScope; this.CanHaveChildren = CanHaveChildren;
                PropertyValues = (GuiPropertyUse[])Properties.Clone(); MethodValues = (GuiMethodId[])Methods.Clone();
                EventValues = (GuiEventId[])Events.Clone();
            }
        }

        internal sealed class GuiMethodDescriptor
        {
            internal readonly GuiMethodId Id; internal readonly string Name;
            internal GuiMethodDescriptor(GuiMethodId Id, string Name) { this.Id = Id; this.Name = Name; }
        }

        internal sealed class GuiEventDescriptor
        {
            internal readonly GuiEventId Id; internal readonly string Name; private readonly string[] CallbackArgumentValues;
            internal string[] CallbackArguments { get { return (string[])CallbackArgumentValues.Clone(); } }
            internal GuiEventDescriptor(GuiEventId Id, string Name, params string[] CallbackArguments)
            { this.Id = Id; this.Name = Name; CallbackArgumentValues = (string[])CallbackArguments.Clone(); }
        }

        internal sealed class GuiValueConstructorDescriptor
        {
            internal readonly string Name; private readonly string[] ArgumentValues;
            internal string[] Arguments { get { return (string[])ArgumentValues.Clone(); } }
            internal GuiValueConstructorDescriptor(string Name, params string[] Arguments)
            { this.Name = Name; ArgumentValues = (string[])Arguments.Clone(); }
        }

        internal sealed class GuiValueFieldDescriptor
        {
            internal readonly string Name; internal readonly GuiValueKind Kind; internal readonly double? Minimum, Maximum;
            internal GuiValueFieldDescriptor(string Name, GuiValueKind Kind, double? Minimum = null, double? Maximum = null)
            { this.Name = Name; this.Kind = Kind; this.Minimum = Minimum; this.Maximum = Maximum; }
        }

        internal sealed class GuiValueTypeDescriptor
        {
            internal readonly GuiValueTypeId Id; internal readonly string Name; private readonly GuiValueFieldDescriptor[] FieldValues;
            private readonly GuiValueConstructorDescriptor[] ConstructorValues;
            internal GuiValueFieldDescriptor[] Fields { get { return (GuiValueFieldDescriptor[])FieldValues.Clone(); } }
            internal GuiValueConstructorDescriptor[] Constructors { get { return (GuiValueConstructorDescriptor[])ConstructorValues.Clone(); } }
            internal GuiValueTypeDescriptor(GuiValueTypeId Id, string Name, GuiValueFieldDescriptor[] Fields, params GuiValueConstructorDescriptor[] Constructors)
            { this.Id = Id; this.Name = Name; FieldValues = (GuiValueFieldDescriptor[])Fields.Clone(); ConstructorValues = (GuiValueConstructorDescriptor[])Constructors.Clone(); }
        }

        internal static class GuiSchema
        {
            private static readonly GuiMethodId[] CommonMethods =
                { GuiMethodId.Create, GuiMethodId.Clone, GuiMethodId.Destroy, GuiMethodId.GetChildren, GuiMethodId.FindFirstChild, GuiMethodId.IsA };
            private static readonly GuiPropertyDescriptor Name = new GuiPropertyDescriptor(GuiPropertyId.Name, "Name", GuiValueKind.String, GuiMutationKind.Metadata, Utf8Limit: GuiLimitId.NameUtf8Bytes);
            private static readonly GuiPropertyDescriptor ClassName = new GuiPropertyDescriptor(GuiPropertyId.ClassName, "ClassName", GuiValueKind.String, GuiMutationKind.Metadata);
            private static readonly GuiPropertyDescriptor Parent = new GuiPropertyDescriptor(GuiPropertyId.Parent, "Parent", GuiValueKind.GuiNodeReference, GuiMutationKind.Structural);
            private static readonly GuiPropertyDescriptor Position = new GuiPropertyDescriptor(GuiPropertyId.Position, "Position", GuiValueKind.UDim2, GuiMutationKind.Patchable);
            private static readonly GuiPropertyDescriptor Size = new GuiPropertyDescriptor(GuiPropertyId.Size, "Size", GuiValueKind.UDim2, GuiMutationKind.Patchable);
            private static readonly GuiPropertyDescriptor AnchorPoint = new GuiPropertyDescriptor(GuiPropertyId.AnchorPoint, "AnchorPoint", GuiValueKind.Vector2, GuiMutationKind.Patchable, 0, 1);
            private static readonly GuiPropertyDescriptor Visible = new GuiPropertyDescriptor(GuiPropertyId.Visible, "Visible", GuiValueKind.Boolean, GuiMutationKind.Patchable);
            private static readonly GuiPropertyDescriptor BackgroundColor3 = new GuiPropertyDescriptor(GuiPropertyId.BackgroundColor3, "BackgroundColor3", GuiValueKind.Color3, GuiMutationKind.Patchable);
            private static readonly GuiPropertyDescriptor BackgroundTransparency = new GuiPropertyDescriptor(GuiPropertyId.BackgroundTransparency, "BackgroundTransparency", GuiValueKind.Number, GuiMutationKind.Patchable, 0, 1);
            private static readonly GuiPropertyDescriptor ZIndex = new GuiPropertyDescriptor(GuiPropertyId.ZIndex, "ZIndex", GuiValueKind.Integer, GuiMutationKind.Structural, 0, 1000);
            private static readonly GuiPropertyDescriptor Text = new GuiPropertyDescriptor(GuiPropertyId.Text, "Text", GuiValueKind.String, GuiMutationKind.Patchable, Utf8Limit: GuiLimitId.TextUtf8Bytes);
            private static readonly GuiPropertyDescriptor TextColor3 = new GuiPropertyDescriptor(GuiPropertyId.TextColor3, "TextColor3", GuiValueKind.Color3, GuiMutationKind.Patchable);
            private static readonly GuiPropertyDescriptor TextTransparency = new GuiPropertyDescriptor(GuiPropertyId.TextTransparency, "TextTransparency", GuiValueKind.Number, GuiMutationKind.Patchable, 0, 1);
            private static readonly GuiPropertyDescriptor TextSize = new GuiPropertyDescriptor(GuiPropertyId.TextSize, "TextSize", GuiValueKind.Integer, GuiMutationKind.Patchable, 1, 128);
            private static readonly GuiPropertyDescriptor TextXAlignment = new GuiPropertyDescriptor(GuiPropertyId.TextXAlignment, "TextXAlignment", GuiValueKind.String, GuiMutationKind.Patchable, null, null, GuiLimitId.None, "Left", "Center", "Right");
            private static readonly GuiPropertyDescriptor TextYAlignment = new GuiPropertyDescriptor(GuiPropertyId.TextYAlignment, "TextYAlignment", GuiValueKind.String, GuiMutationKind.Patchable, null, null, GuiLimitId.None, "Top", "Center", "Bottom");
            private static readonly GuiPropertyDescriptor LayoutOrder = new GuiPropertyDescriptor(GuiPropertyId.LayoutOrder, "LayoutOrder", GuiValueKind.Integer, GuiMutationKind.LayoutAffecting, -32768, 32767);
            private static readonly GuiPropertyDescriptor Padding = new GuiPropertyDescriptor(GuiPropertyId.Padding, "Padding", GuiValueKind.UDim, GuiMutationKind.LayoutAffecting, -8, 8);
            private static readonly GuiPropertyDescriptor FillDirection = new GuiPropertyDescriptor(GuiPropertyId.FillDirection, "FillDirection", GuiValueKind.String, GuiMutationKind.LayoutAffecting, null, null, GuiLimitId.None, "Vertical", "Horizontal");
            private static readonly GuiPropertyDescriptor HorizontalAlignment = new GuiPropertyDescriptor(GuiPropertyId.HorizontalAlignment, "HorizontalAlignment", GuiValueKind.String, GuiMutationKind.LayoutAffecting, null, null, GuiLimitId.None, "Left", "Center", "Right");
            private static readonly GuiPropertyDescriptor VerticalAlignment = new GuiPropertyDescriptor(GuiPropertyId.VerticalAlignment, "VerticalAlignment", GuiValueKind.String, GuiMutationKind.LayoutAffecting, null, null, GuiLimitId.None, "Top", "Center", "Bottom");
            private static readonly GuiPropertyDescriptor PaddingTop = new GuiPropertyDescriptor(GuiPropertyId.PaddingTop, "PaddingTop", GuiValueKind.UDim, GuiMutationKind.LayoutAffecting, 0, 1);
            private static readonly GuiPropertyDescriptor PaddingBottom = new GuiPropertyDescriptor(GuiPropertyId.PaddingBottom, "PaddingBottom", GuiValueKind.UDim, GuiMutationKind.LayoutAffecting, 0, 1);
            private static readonly GuiPropertyDescriptor PaddingLeft = new GuiPropertyDescriptor(GuiPropertyId.PaddingLeft, "PaddingLeft", GuiValueKind.UDim, GuiMutationKind.LayoutAffecting, 0, 1);
            private static readonly GuiPropertyDescriptor PaddingRight = new GuiPropertyDescriptor(GuiPropertyId.PaddingRight, "PaddingRight", GuiValueKind.UDim, GuiMutationKind.LayoutAffecting, 0, 1);
            private static readonly GuiPropertyDescriptor Image = new GuiPropertyDescriptor(GuiPropertyId.Image, "Image", GuiValueKind.ImageSource, GuiMutationKind.Structural);
            private static readonly GuiPropertyDescriptor ImageColor3 = new GuiPropertyDescriptor(GuiPropertyId.ImageColor3, "ImageColor3", GuiValueKind.Color3, GuiMutationKind.Patchable);
            private static readonly GuiPropertyDescriptor ImageTransparency = new GuiPropertyDescriptor(GuiPropertyId.ImageTransparency, "ImageTransparency", GuiValueKind.Number, GuiMutationKind.Patchable, 0, 1);

            private static readonly GuiClassDescriptor[] ClassValues = BuildClasses();
            private static readonly GuiMethodDescriptor[] MethodValues =
            {
                new GuiMethodDescriptor(GuiMethodId.Create, "Create"), new GuiMethodDescriptor(GuiMethodId.Clone, "Clone"),
                new GuiMethodDescriptor(GuiMethodId.Destroy, "Destroy"), new GuiMethodDescriptor(GuiMethodId.GetChildren, "GetChildren"),
                new GuiMethodDescriptor(GuiMethodId.FindFirstChild, "FindFirstChild"), new GuiMethodDescriptor(GuiMethodId.IsA, "IsA"),
                new GuiMethodDescriptor(GuiMethodId.Show, "Show"), new GuiMethodDescriptor(GuiMethodId.Hide, "Hide"),
                new GuiMethodDescriptor(GuiMethodId.IsShown, "IsShown")
            };
            private static readonly GuiEventDescriptor[] EventValues =
            { new GuiEventDescriptor(GuiEventId.Activated, "Activated", "Player") };
            private static readonly GuiValueTypeDescriptor[] ValueValues =
            {
                new GuiValueTypeDescriptor(GuiValueTypeId.UDim, "UDim", new[] {
                    new GuiValueFieldDescriptor("Scale", GuiValueKind.Number, -8, 8), new GuiValueFieldDescriptor("Offset", GuiValueKind.Number, -32768, 32768)},
                    new GuiValueConstructorDescriptor("new", "Scale", "Offset")),
                new GuiValueTypeDescriptor(GuiValueTypeId.UDim2, "UDim2", new[] {
                    new GuiValueFieldDescriptor("X", GuiValueKind.UDim), new GuiValueFieldDescriptor("Y", GuiValueKind.UDim)},
                    new GuiValueConstructorDescriptor("new", "XScale", "XOffset", "YScale", "YOffset"),
                    new GuiValueConstructorDescriptor("fromScale", "XScale", "YScale"), new GuiValueConstructorDescriptor("fromOffset", "XOffset", "YOffset")),
                new GuiValueTypeDescriptor(GuiValueTypeId.Vector2, "Vector2", new[] {
                    new GuiValueFieldDescriptor("X", GuiValueKind.Number, -32768, 32768), new GuiValueFieldDescriptor("Y", GuiValueKind.Number, -32768, 32768)},
                    new GuiValueConstructorDescriptor("new", "X", "Y")),
                new GuiValueTypeDescriptor(GuiValueTypeId.Color3, "Color3", new[] {
                    new GuiValueFieldDescriptor("R", GuiValueKind.Number, 0, 1), new GuiValueFieldDescriptor("G", GuiValueKind.Number, 0, 1),
                    new GuiValueFieldDescriptor("B", GuiValueKind.Number, 0, 1)},
                    new GuiValueConstructorDescriptor("new", "R", "G", "B"), new GuiValueConstructorDescriptor("fromRGB", "R", "G", "B")),
                new GuiValueTypeDescriptor(GuiValueTypeId.ImageSource, "ImageSource", new[] {
                    new GuiValueFieldDescriptor("Kind", GuiValueKind.String), new GuiValueFieldDescriptor("Name", GuiValueKind.String),
                    new GuiValueFieldDescriptor("Id", GuiValueKind.String), new GuiValueFieldDescriptor("ItemId", GuiValueKind.Integer),
                    new GuiValueFieldDescriptor("SkinId", GuiValueKind.String), new GuiValueFieldDescriptor("UserId", GuiValueKind.String)},
                    new GuiValueConstructorDescriptor("None"), new GuiValueConstructorDescriptor("Sprite", "Name"),
                    new GuiValueConstructorDescriptor("Png", "Id"), new GuiValueConstructorDescriptor("Item", "ItemId", "SkinId?"),
                    new GuiValueConstructorDescriptor("SteamAvatar", "UserId"))
            };

            internal static GuiClassDescriptor[] Classes { get { return (GuiClassDescriptor[])ClassValues.Clone(); } }
            internal static GuiValueTypeDescriptor[] ValueTypes { get { return (GuiValueTypeDescriptor[])ValueValues.Clone(); } }
            internal static GuiMethodDescriptor[] Methods { get { return (GuiMethodDescriptor[])MethodValues.Clone(); } }
            internal static GuiEventDescriptor[] Events { get { return (GuiEventDescriptor[])EventValues.Clone(); } }

            internal static GuiClassDescriptor GetClass(GuiClassId Id)
            {
                foreach (GuiClassDescriptor Value in ClassValues) if (Value.Id == Id) return Value;
                throw new InvalidOperationException("unknown GUI class descriptor");
            }
            internal static bool TryGetClass(string Name, out GuiClassDescriptor Result)
            {
                foreach (GuiClassDescriptor Value in ClassValues) if (Value.Name == Name) { Result = Value; return true; }
                Result = null; return false;
            }
            internal static bool TryGetProperty(GuiClassId ClassId, string Name, out GuiPropertyUse Result)
            {
                foreach (GuiPropertyUse Value in GetClass(ClassId).Properties)
                    if (Value.Descriptor.Name == Name) { Result = Value; return true; }
                Result = null; return false;
            }
            internal static bool IsA(GuiClassId ClassId, string Name)
            {
                GuiClassDescriptor Value = GetClass(ClassId);
                while (Value != null) {
                    if (Value.Name == Name) return true;
                    Value = Value.BaseClass.HasValue ? GetClass(Value.BaseClass.Value) : null;
                }
                return false;
            }
            internal static GuiValueTypeDescriptor GetValueType(GuiValueTypeId Id)
            {
                foreach (GuiValueTypeDescriptor Value in ValueValues) if (Value.Id == Id) return Value;
                throw new InvalidOperationException("unknown GUI value descriptor");
            }
            internal static GuiMethodDescriptor GetMethod(GuiMethodId Id)
            {
                foreach (GuiMethodDescriptor Value in MethodValues) if (Value.Id == Id) return Value;
                throw new InvalidOperationException("unknown GUI method descriptor");
            }
            internal static GuiEventDescriptor GetEvent(GuiEventId Id)
            {
                foreach (GuiEventDescriptor Value in EventValues) if (Value.Id == Id) return Value;
                throw new InvalidOperationException("unknown GUI event descriptor");
            }

            internal static void Validate()
            {
                var ClassIds = new HashSet<GuiClassId>(); var ClassNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiClassDescriptor Class in ClassValues) {
                    if (!ClassIds.Add(Class.Id) || !ClassNames.Add(Class.Name)) throw new InvalidOperationException("duplicate GUI class descriptor");
                    var Properties = new HashSet<GuiPropertyId>(); var PropertyNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (GuiPropertyUse Property in Class.Properties)
                        if (!Properties.Add(Property.Descriptor.Id) || !PropertyNames.Add(Property.Descriptor.Name))
                            throw new InvalidOperationException("duplicate GUI property descriptor on " + Class.Name);
                    var Methods = new HashSet<GuiMethodId>(); foreach (GuiMethodId Method in Class.Methods)
                        if (!Methods.Add(Method)) throw new InvalidOperationException("duplicate GUI method descriptor on " + Class.Name);
                    var Events = new HashSet<GuiEventId>(); foreach (GuiEventId Event in Class.Events)
                        if (!Events.Add(Event)) throw new InvalidOperationException("duplicate GUI event descriptor on " + Class.Name);
                }
                var ValueIds = new HashSet<GuiValueTypeId>(); var ValueNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiValueTypeDescriptor Value in ValueValues) {
                    if (!ValueIds.Add(Value.Id) || !ValueNames.Add(Value.Name)) throw new InvalidOperationException("duplicate GUI value descriptor");
                    var Fields = new HashSet<string>(StringComparer.Ordinal); foreach (GuiValueFieldDescriptor Field in Value.Fields)
                        if (!Fields.Add(Field.Name)) throw new InvalidOperationException("duplicate GUI value field on " + Value.Name);
                    var Constructors = new HashSet<string>(StringComparer.Ordinal); foreach (GuiValueConstructorDescriptor Constructor in Value.Constructors)
                        if (!Constructors.Add(Constructor.Name)) throw new InvalidOperationException("duplicate GUI value constructor on " + Value.Name);
                }
                var MethodIds = new HashSet<GuiMethodId>(); var MethodNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiMethodDescriptor Method in MethodValues)
                    if (!MethodIds.Add(Method.Id) || !MethodNames.Add(Method.Name)) throw new InvalidOperationException("duplicate GUI method descriptor");
                var EventIds = new HashSet<GuiEventId>(); var EventNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiEventDescriptor Event in EventValues)
                    if (!EventIds.Add(Event.Id) || !EventNames.Add(Event.Name)) throw new InvalidOperationException("duplicate GUI event descriptor");
            }

            private static GuiClassDescriptor[] BuildClasses()
            {
                GuiPropertyUse[] Node = { Use(Name, true, "class name"), Use(ClassName, false, "concrete class") };
                GuiPropertyUse[] Screen = { Use(Name, true, "ScreenGui"), Use(ClassName, false, "ScreenGui"), Use(Parent, false, "nil") };
                return new[] {
                    new GuiClassDescriptor(GuiClassId.GuiNode, "GuiNode", null, false, GuiCreationScope.None, true, Node, CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.GuiObject, "GuiObject", GuiClassId.GuiNode, false, GuiCreationScope.None, true,
                        ObjectProperties("UDim2.fromOffset(100, 100)", "0"), CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.ScreenGui, "ScreenGui", GuiClassId.GuiNode, true, GuiCreationScope.GuiService, true, Screen,
                        Append(CommonMethods, GuiMethodId.Show, GuiMethodId.Hide, GuiMethodId.IsShown), new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.Frame, "Frame", GuiClassId.GuiObject, true, GuiCreationScope.GuiService | GuiCreationScope.GuiObject, true,
                        ObjectProperties("UDim2.fromOffset(100, 100)", "0"), CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.TextLabel, "TextLabel", GuiClassId.GuiObject, true, GuiCreationScope.GuiService | GuiCreationScope.GuiObject, true,
                        Append(ObjectProperties("UDim2.fromOffset(100, 30)", "1"), TextProperties()), CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.TextButton, "TextButton", GuiClassId.GuiObject, true, GuiCreationScope.GuiService | GuiCreationScope.GuiObject, true,
                        Append(ObjectProperties("UDim2.fromOffset(100, 36)", "0"), TextProperties()), CommonMethods, new[] {GuiEventId.Activated}),
                    new GuiClassDescriptor(GuiClassId.UIListLayout, "UIListLayout", GuiClassId.GuiNode, true, GuiCreationScope.GuiObject, false,
                        LayoutProperties(), CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.UIPadding, "UIPadding", GuiClassId.GuiNode, true, GuiCreationScope.GuiObject, false,
                        PaddingProperties(), CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.ImageLabel, "ImageLabel", GuiClassId.GuiObject, true, GuiCreationScope.GuiService | GuiCreationScope.GuiObject, true,
                        Append(ObjectProperties("UDim2.fromOffset(100, 100)", "1"), ImageProperties()), CommonMethods, new GuiEventId[0]),
                    new GuiClassDescriptor(GuiClassId.ImageButton, "ImageButton", GuiClassId.GuiObject, true, GuiCreationScope.GuiService | GuiCreationScope.GuiObject, true,
                        Append(ObjectProperties("UDim2.fromOffset(100, 100)", "1"), ImageProperties()), CommonMethods, new[] {GuiEventId.Activated})
                };
            }
            private static GuiPropertyUse[] ObjectProperties(string SizeDefault, string TransparencyDefault)
            {
                return new[] {Use(Name, true, "class name"), Use(ClassName, false, "concrete class"), Use(Parent, true, "nil"),
                    Use(Position, true, "UDim2(0, 0, 0, 0)"), Use(Size, true, SizeDefault), Use(AnchorPoint, true, "Vector2(0, 0)"),
                    Use(Visible, true, "true"), Use(BackgroundColor3, true, "Color3(1, 1, 1)"),
                    Use(BackgroundTransparency, true, TransparencyDefault), Use(ZIndex, true, "1"), Use(LayoutOrder, true, "0")};
            }
            private static GuiPropertyUse[] TextProperties()
            {
                return new[] {Use(Text, true, ""), Use(TextColor3, true, "Color3(0, 0, 0)"), Use(TextTransparency, true, "0"),
                    Use(TextSize, true, "14"), Use(TextXAlignment, true, "Center"), Use(TextYAlignment, true, "Center")};
            }
            private static GuiPropertyUse[] LayoutProperties()
            {
                return new[] {Use(Name, true, "UIListLayout"), Use(ClassName, false, "UIListLayout"), Use(Parent, true, "nil"),
                    Use(Padding, true, "UDim.new(0, 0)"), Use(FillDirection, true, "Vertical"),
                    Use(HorizontalAlignment, true, "Left"), Use(VerticalAlignment, true, "Top")};
            }
            private static GuiPropertyUse[] PaddingProperties()
            {
                return new[] {Use(Name, true, "UIPadding"), Use(ClassName, false, "UIPadding"), Use(Parent, true, "nil"),
                    Use(PaddingTop, true, "UDim.new(0, 0)"), Use(PaddingBottom, true, "UDim.new(0, 0)"),
                    Use(PaddingLeft, true, "UDim.new(0, 0)"), Use(PaddingRight, true, "UDim.new(0, 0)")};
            }
            private static GuiPropertyUse[] ImageProperties()
            {
                return new[] {Use(Image, true, "ImageSource.None()"), Use(ImageColor3, true, "Color3(1, 1, 1)"),
                    Use(ImageTransparency, true, "0")};
            }
            private static GuiPropertyUse Use(GuiPropertyDescriptor Descriptor, bool Writable, string DefaultValue)
            { return new GuiPropertyUse(Descriptor, Writable, DefaultValue); }
            private static T[] Append<T>(T[] First, params T[] Second)
            { var Result = new T[First.Length + Second.Length]; Array.Copy(First, Result, First.Length); Array.Copy(Second, 0, Result, First.Length, Second.Length); return Result; }
        }
    }
}
