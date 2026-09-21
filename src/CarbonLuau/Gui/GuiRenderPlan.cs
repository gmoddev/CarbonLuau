using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal enum GuiRenderNodeKind { Container = 1, Text = 2, Button = 3 }
        internal enum GuiRenderPropertyId
        {
            AnchorMin = 1, AnchorMax = 2, OffsetMin = 3, OffsetMax = 4, Pivot = 5, Visible = 6,
            BackgroundColor = 7, Text = 8, TextColor = 9, FontSize = 10,
            TextXAlignment = 11, TextYAlignment = 12, ActionCommand = 13, NeedsCursor = 14
        }
        internal enum GuiRenderValueKind { Boolean = 1, Integer = 2, Number = 3, String = 4, Vector2 = 5, Color = 6 }

        internal struct GuiRenderVector2
        {
            internal readonly double X, Y;
            internal GuiRenderVector2(double X, double Y)
            { Finite(X); Finite(Y); this.X = Normalize(X); this.Y = Normalize(Y); }
            private static void Finite(double Value)
            { if (Double.IsNaN(Value) || Double.IsInfinity(Value)) throw new InvalidOperationException("render vector must be finite"); }
            private static double Normalize(double Value) { return Value == 0 ? 0 : Value; }
        }

        internal struct GuiRenderColor
        {
            internal readonly double R, G, B, A;
            internal GuiRenderColor(double R, double G, double B, double A)
            {
                Component(R); Component(G); Component(B); Component(A);
                this.R = R == 0 ? 0 : R; this.G = G == 0 ? 0 : G; this.B = B == 0 ? 0 : B; this.A = A == 0 ? 0 : A;
            }
            private static void Component(double Value)
            { if (Double.IsNaN(Value) || Double.IsInfinity(Value) || Value < 0 || Value > 1) throw new InvalidOperationException("render color components must be finite 0..1"); }
        }

        internal sealed class GuiRenderValue
        {
            private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
            internal readonly GuiRenderValueKind Kind; internal readonly bool Boolean;
            internal readonly int Integer; internal readonly double Number; internal readonly string Text;
            internal readonly GuiRenderVector2 Vector; internal readonly GuiRenderColor Color;
            private GuiRenderValue(GuiRenderValueKind Kind, bool Boolean = false, int Integer = 0, double Number = 0,
                string Text = null, GuiRenderVector2 Vector = default(GuiRenderVector2), GuiRenderColor Color = default(GuiRenderColor))
            { this.Kind = Kind; this.Boolean = Boolean; this.Integer = Integer; this.Number = Number; this.Text = Text; this.Vector = Vector; this.Color = Color; }
            internal static GuiRenderValue FromBoolean(bool Value) { return new GuiRenderValue(GuiRenderValueKind.Boolean, Boolean: Value); }
            internal static GuiRenderValue FromInteger(int Value) { return new GuiRenderValue(GuiRenderValueKind.Integer, Integer: Value); }
            internal static GuiRenderValue FromNumber(double Value)
            {
                if (Double.IsNaN(Value) || Double.IsInfinity(Value)) throw new InvalidOperationException("render number must be finite");
                return new GuiRenderValue(GuiRenderValueKind.Number, Number: Value == 0 ? 0 : Value);
            }
            internal static GuiRenderValue FromString(string Value)
            {
                if (Value == null || Value.IndexOf('\0') >= 0) throw new InvalidOperationException("render string is invalid");
                Utf8Bytes(Value);
                return new GuiRenderValue(GuiRenderValueKind.String, Text: Value);
            }
            internal static int Utf8Bytes(string Value)
            {
                try { return StrictUtf8.GetByteCount(Value); }
                catch (EncoderFallbackException) { throw new InvalidOperationException("render string must be valid UTF-8"); }
            }
            internal static GuiRenderValue FromVector(double X, double Y)
            { return new GuiRenderValue(GuiRenderValueKind.Vector2, Vector: new GuiRenderVector2(X, Y)); }
            internal static GuiRenderValue FromColor(double R, double G, double B, double A)
            { return new GuiRenderValue(GuiRenderValueKind.Color, Color: new GuiRenderColor(R, G, B, A)); }
            internal string Describe()
            {
                switch (Kind) {
                    case GuiRenderValueKind.Boolean: return Boolean ? "true" : "false";
                    case GuiRenderValueKind.Integer: return Integer.ToString(CultureInfo.InvariantCulture);
                    case GuiRenderValueKind.Number: return Number.ToString("R", CultureInfo.InvariantCulture);
                    case GuiRenderValueKind.String: return Text.Length.ToString(CultureInfo.InvariantCulture) + ":" + Text;
                    case GuiRenderValueKind.Vector2: return Vector.X.ToString("R", CultureInfo.InvariantCulture) + "," + Vector.Y.ToString("R", CultureInfo.InvariantCulture);
                    case GuiRenderValueKind.Color: return Color.R.ToString("R", CultureInfo.InvariantCulture) + "," + Color.G.ToString("R", CultureInfo.InvariantCulture) + "," + Color.B.ToString("R", CultureInfo.InvariantCulture) + "," + Color.A.ToString("R", CultureInfo.InvariantCulture);
                    default: throw new InvalidOperationException("unknown render value kind");
                }
            }
        }

        internal sealed class GuiRenderProperty
        {
            internal readonly GuiRenderPropertyId Id; internal readonly GuiRenderValue Value;
            internal GuiRenderProperty(GuiRenderPropertyId Id, GuiRenderValue Value)
            { if (Value == null) throw new InvalidOperationException("render property value is required"); this.Id = Id; this.Value = Value; }
        }

        internal sealed class GuiRenderElement
        {
            internal readonly string ClientId, ParentClientId; internal readonly GuiRenderNodeKind Kind;
            internal readonly int CanonicalUtf8Bytes;
            private readonly GuiRenderProperty[] PropertyValues;
            internal GuiRenderProperty[] Properties { get { return (GuiRenderProperty[])PropertyValues.Clone(); } }
            internal GuiRenderElement(string ClientId, string ParentClientId, GuiRenderNodeKind Kind, GuiLimits Limits,
                params GuiRenderProperty[] Properties)
            {
                ValidateId(ClientId, "client id"); if (ParentClientId != null) ValidateId(ParentClientId, "parent client id");
                if (Limits == null) throw new InvalidOperationException("GUI limits are required");
                if (Properties == null || Properties.Length > Limits.MaxRenderPropertiesPerElement)
                    throw new InvalidOperationException("render element property bound exceeded");
                var Copy = (GuiRenderProperty[])Properties.Clone(); Array.Sort(Copy, (Left, Right) => Left.Id.CompareTo(Right.Id));
                for (int Index = 0; Index < Copy.Length; ++Index) {
                    if (Copy[Index] == null) throw new InvalidOperationException("render property is required");
                    if (Index != 0 && Copy[Index - 1].Id == Copy[Index].Id) throw new InvalidOperationException("duplicate render property");
                    if (Copy[Index].Value.Kind == GuiRenderValueKind.String &&
                        GuiRenderValue.Utf8Bytes(Copy[Index].Value.Text) > Limits.MaxSerializedOperationBytes)
                        throw new InvalidOperationException("render string exceeds the serialized operation bound");
                }
                this.ClientId = ClientId; this.ParentClientId = ParentClientId; this.Kind = Kind; PropertyValues = Copy;
                CanonicalUtf8Bytes = GuiRenderValue.Utf8Bytes(Describe());
                if (CanonicalUtf8Bytes > Limits.MaxSerializedOperationBytes)
                    throw new InvalidOperationException("render element exceeds the serialized operation bound");
            }
            private static void ValidateId(string Value, string Label)
            { if (String.IsNullOrEmpty(Value) || Value.Length > 256 || Value.IndexOf('\0') >= 0) throw new InvalidOperationException(Label + " is invalid"); }
            internal string Describe()
            {
                var Result = new StringBuilder(); Result.Append(ClientId.Length).Append(':').Append(ClientId).Append('|');
                Result.Append(ParentClientId == null ? "-" : ParentClientId.Length.ToString(CultureInfo.InvariantCulture) + ":" + ParentClientId);
                Result.Append('|').Append((int)Kind);
                foreach (GuiRenderProperty Property in PropertyValues)
                    Result.Append('|').Append((int)Property.Id).Append('=').Append((int)Property.Value.Kind).Append(':').Append(Property.Value.Describe());
                return Result.ToString();
            }
        }

        internal sealed class GuiBackendTarget : IEquatable<GuiBackendTarget>
        {
            internal readonly string ExactPlayerConnectionToken, ClientRootId;
            internal GuiBackendTarget(string ExactPlayerConnectionToken, string ClientRootId)
            {
                Validate(ExactPlayerConnectionToken, "exact Player connection token"); Validate(ClientRootId, "client root id");
                this.ExactPlayerConnectionToken = ExactPlayerConnectionToken; this.ClientRootId = ClientRootId;
            }
            private static void Validate(string Value, string Label)
            { if (String.IsNullOrEmpty(Value) || Value.Length > 256 || Value.IndexOf('\0') >= 0) throw new InvalidOperationException(Label + " is invalid"); }
            public bool Equals(GuiBackendTarget Other)
            { return Other != null && ExactPlayerConnectionToken == Other.ExactPlayerConnectionToken && ClientRootId == Other.ClientRootId; }
            public override bool Equals(object Value) { return Equals(Value as GuiBackendTarget); }
            public override int GetHashCode()
            { unchecked { return (ExactPlayerConnectionToken.GetHashCode() * 397) ^ ClientRootId.GetHashCode(); } }
            internal string Key { get { return ExactPlayerConnectionToken.Length.ToString(CultureInfo.InvariantCulture) + ":" + ExactPlayerConnectionToken + ClientRootId; } }
        }

        internal sealed class GuiRenderPlan
        {
            private readonly GuiRenderElement[] ElementValues; internal readonly int EstimatedSerializedBytes;
            internal GuiRenderElement[] Elements { get { return (GuiRenderElement[])ElementValues.Clone(); } }
            internal GuiRenderPlan(GuiLimits Limits, int EstimatedSerializedBytes, params GuiRenderElement[] Elements)
            {
                ValidateRequest(Limits, EstimatedSerializedBytes, Elements);
                var Seen = new HashSet<string>(StringComparer.Ordinal); int Roots = 0;
                for (int Index = 0; Index < Elements.Length; ++Index) {
                    GuiRenderElement Element = Elements[Index];
                    if (Element == null || !Seen.Add(Element.ClientId)) throw new InvalidOperationException("full render elements require unique client ids");
                    if (Element.ParentClientId == null) ++Roots;
                    else if (!Seen.Contains(Element.ParentClientId)) throw new InvalidOperationException("full render plan must be parent-before-child");
                }
                if (Roots != 1) throw new InvalidOperationException("full render plan requires exactly one root");
                int CanonicalBytes = GuiRenderValue.Utf8Bytes("FULL|" + EstimatedSerializedBytes.ToString(CultureInfo.InvariantCulture));
                foreach (GuiRenderElement Element in Elements) {
                    CanonicalBytes = checked(CanonicalBytes + 1 + Element.CanonicalUtf8Bytes);
                    if (CanonicalBytes > Limits.MaxSerializedOperationBytes) throw new InvalidOperationException("full render plan exceeds the serialized operation bound");
                }
                this.EstimatedSerializedBytes = EstimatedSerializedBytes; ElementValues = (GuiRenderElement[])Elements.Clone();
            }
            internal string Describe()
            {
                var Result = new StringBuilder("FULL|"); Result.Append(EstimatedSerializedBytes);
                foreach (GuiRenderElement Element in ElementValues) Result.Append('\n').Append(Element.Describe());
                return Result.ToString();
            }
            private static void ValidateRequest(GuiLimits Limits, int EstimatedSerializedBytes, GuiRenderElement[] Elements)
            {
                if (Limits == null) throw new InvalidOperationException("GUI limits are required");
                if (EstimatedSerializedBytes < 0 || EstimatedSerializedBytes > Limits.MaxSerializedOperationBytes)
                    throw new InvalidOperationException("render request byte bound exceeded");
                if (Elements == null || Elements.Length == 0 || Elements.Length > Limits.MaxRenderElementsPerOperation)
                    throw new InvalidOperationException("render element bound exceeded");
            }
        }

        internal sealed class GuiRenderPatch
        {
            private readonly GuiRenderElement[] ElementValues; internal readonly int EstimatedSerializedBytes;
            internal GuiRenderElement[] Elements { get { return (GuiRenderElement[])ElementValues.Clone(); } }
            internal GuiRenderPatch(GuiLimits Limits, int EstimatedSerializedBytes, params GuiRenderElement[] Elements)
            {
                if (Limits == null) throw new InvalidOperationException("GUI limits are required");
                if (EstimatedSerializedBytes < 0 || EstimatedSerializedBytes > Limits.MaxSerializedOperationBytes)
                    throw new InvalidOperationException("render patch byte bound exceeded");
                if (Elements == null || Elements.Length == 0 || Elements.Length > Limits.MaxRenderElementsPerOperation)
                    throw new InvalidOperationException("render patch element bound exceeded");
                var Seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiRenderElement Element in Elements) {
                    if (Element == null || Element.Properties.Length == 0 || !Seen.Add(Element.ClientId))
                        throw new InvalidOperationException("patch elements require unique ids and properties");
                }
                int CanonicalBytes = GuiRenderValue.Utf8Bytes("PATCH|" + EstimatedSerializedBytes.ToString(CultureInfo.InvariantCulture));
                foreach (GuiRenderElement Element in Elements) {
                    CanonicalBytes = checked(CanonicalBytes + 1 + Element.CanonicalUtf8Bytes);
                    if (CanonicalBytes > Limits.MaxSerializedOperationBytes) throw new InvalidOperationException("render patch exceeds the serialized operation bound");
                }
                this.EstimatedSerializedBytes = EstimatedSerializedBytes; ElementValues = (GuiRenderElement[])Elements.Clone();
            }
            internal string Describe()
            {
                var Result = new StringBuilder("PATCH|"); Result.Append(EstimatedSerializedBytes);
                foreach (GuiRenderElement Element in ElementValues) Result.Append('\n').Append(Element.Describe());
                return Result.ToString();
            }
        }
    }
}
