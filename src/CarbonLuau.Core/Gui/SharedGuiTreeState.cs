using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiStoredValue
        {
            internal readonly GuiValueKind Kind;
            internal readonly string Text;
            internal readonly bool Boolean;
            internal readonly int Integer;
            internal readonly GuiImageSourceValue ImageSource;
            internal readonly GuiFontIdentity FontIdentity;
            private readonly double[] NumberValues;
            internal double[] Numbers { get { return NumberValues == null ? null : (double[])NumberValues.Clone(); } }

            private GuiStoredValue(GuiValueKind Kind, string Text = null, bool Boolean = false, int Integer = 0,
                GuiImageSourceValue ImageSource = null, GuiFontIdentity FontIdentity = GuiFontIdentity.RobotoCondensedRegular,
                params double[] Numbers)
            { this.Kind = Kind; this.Text = Text; this.Boolean = Boolean; this.Integer = Integer; this.ImageSource = ImageSource; this.FontIdentity = FontIdentity; NumberValues = Numbers; }
            internal static GuiStoredValue String(string Value) { return new GuiStoredValue(GuiValueKind.String, Text: Value); }
            internal static GuiStoredValue Bool(bool Value) { return new GuiStoredValue(GuiValueKind.Boolean, Boolean: Value); }
            internal static GuiStoredValue Int(int Value) { return new GuiStoredValue(GuiValueKind.Integer, Integer: Value); }
            internal static GuiStoredValue Number(double Value) { return new GuiStoredValue(GuiValueKind.Number, Numbers: new[] {Normalize(Value)}); }
            internal static GuiStoredValue UDim(double Scale, double Offset) { return new GuiStoredValue(GuiValueKind.UDim, Numbers: new[] {Normalize(Scale), Normalize(Offset)}); }
            internal static GuiStoredValue UDim2(double XScale, double XOffset, double YScale, double YOffset)
            { return new GuiStoredValue(GuiValueKind.UDim2, Numbers: new[] {Normalize(XScale), Normalize(XOffset), Normalize(YScale), Normalize(YOffset)}); }
            internal static GuiStoredValue Vector2(double X, double Y) { return new GuiStoredValue(GuiValueKind.Vector2, Numbers: new[] {Normalize(X), Normalize(Y)}); }
            internal static GuiStoredValue Color3(double R, double G, double B) { return new GuiStoredValue(GuiValueKind.Color3, Numbers: new[] {Normalize(R), Normalize(G), Normalize(B)}); }
            internal static GuiStoredValue Image(GuiImageSourceValue Value) { return new GuiStoredValue(GuiValueKind.ImageSource, ImageSource: Value); }
            internal static GuiStoredValue Font(GuiFontIdentity Value) { return new GuiStoredValue(GuiValueKind.GuiFont, FontIdentity: Value); }
            private static double Normalize(double Value) { return Value == 0 ? 0 : Value; }

            internal string[] Encode()
            {
                switch (Kind) {
                    case GuiValueKind.String: return new[] {"string", Text};
                    case GuiValueKind.Boolean: return new[] {"boolean", Boolean ? "1" : "0"};
                    case GuiValueKind.Integer: return new[] {"integer", Integer.ToString(CultureInfo.InvariantCulture)};
                    case GuiValueKind.Number: return new[] {"number", Format(NumberValues[0])};
                    case GuiValueKind.UDim: return new[] {"udim", Format(NumberValues[0]), Format(NumberValues[1])};
                    case GuiValueKind.UDim2: return new[] {"udim2", Format(NumberValues[0]), Format(NumberValues[1]), Format(NumberValues[2]), Format(NumberValues[3])};
                    case GuiValueKind.Vector2: return new[] {"vector2", Format(NumberValues[0]), Format(NumberValues[1])};
                    case GuiValueKind.Color3: return new[] {"color3", Format(NumberValues[0]), Format(NumberValues[1]), Format(NumberValues[2])};
                    case GuiValueKind.ImageSource: return ImageSource.Encode();
                    case GuiValueKind.GuiFont: return new[] {"guifont", FontIdentity.ToString()};
                    default: throw new FacadeException("unsupported GUI value kind");
                }
            }
            internal bool SameAs(GuiStoredValue Other)
            {
                if (Other == null || Kind != Other.Kind || Text != Other.Text || Boolean != Other.Boolean || Integer != Other.Integer ||
                    !Object.Equals(ImageSource, Other.ImageSource) || FontIdentity != Other.FontIdentity) return false;
                if (NumberValues == null || Other.NumberValues == null) return NumberValues == Other.NumberValues;
                if (NumberValues.Length != Other.NumberValues.Length) return false;
                for (int Index = 0; Index < NumberValues.Length; ++Index) if (NumberValues[Index] != Other.NumberValues[Index]) return false;
                return true;
            }
            private static string Format(double Value) { return Value.ToString("R", CultureInfo.InvariantCulture); }
        }

        internal sealed class GuiRetainedNode
        {
            internal readonly GuiObjectIdentity Identity;
            internal readonly GuiClassId ClassId;
            internal ulong? ParentId;
            internal readonly List<ulong> Children = new List<ulong>();
            internal readonly Dictionary<GuiPropertyId, GuiStoredValue> Properties = new Dictionary<GuiPropertyId, GuiStoredValue>();
            internal GuiRetainedNode(GuiObjectIdentity Identity, GuiClassId ClassId) { this.Identity = Identity; this.ClassId = ClassId; }
            internal GuiRetainedNode Copy()
            {
                var Result = new GuiRetainedNode(Identity, ClassId) {ParentId = ParentId};
                Result.Children.AddRange(Children); foreach (var Value in Properties) Result.Properties.Add(Value.Key, Value.Value);
                return Result;
            }
        }

        internal class GuiTreeState
        {
            internal readonly Dictionary<ulong, GuiRetainedNode> Nodes = new Dictionary<ulong, GuiRetainedNode>();
            internal int Screens;
            internal virtual GuiTreeState CopyTree()
            {
                var Result = new GuiTreeState { Screens = Screens };
                foreach (var Value in Nodes) Result.Nodes.Add(Value.Key, Value.Value.Copy());
                return Result;
            }
        }
    }
}
