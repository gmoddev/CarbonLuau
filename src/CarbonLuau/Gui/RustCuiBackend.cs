using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal interface IRustCuiTransport
        {
            GuiBackendResult Replace(GuiBackendTarget Target, string Payload);
            GuiBackendResult Update(GuiBackendTarget Target, string Payload);
            GuiBackendResult Destroy(GuiBackendTarget Target);
        }

        internal sealed class RustCuiBackend : IGuiBackend
        {
            private readonly GuiLimits Limits;
            private readonly IRustCuiTransport Transport;
            internal RustCuiBackend(GuiLimits Limits, IRustCuiTransport Transport)
            { this.Limits = Limits ?? throw new ArgumentNullException("Limits"); this.Transport = Transport ?? throw new ArgumentNullException("Transport"); }
            public int MeasureReplace(GuiBackendTarget Target, GuiRenderPlan Plan)
            { return Measure(Target, Plan == null ? null : Plan.Elements, false, true); }
            public int MeasureUpdate(GuiBackendTarget Target, GuiRenderPatch Patch)
            { return Measure(Target, Patch == null ? null : Patch.Elements, true, false); }
            public int MeasureDestroy(GuiBackendTarget Target)
            { if (Target == null) throw new InvalidOperationException("backend target is required"); return GuiRenderValue.Utf8Bytes(Target.ClientRootId); }
            public GuiBackendResult Replace(GuiBackendTarget Target, GuiRenderPlan Plan)
            { return Send(Target, Plan == null ? null : Plan.Elements, false, true, GuiBackendOperationKind.Replace); }
            public GuiBackendResult Update(GuiBackendTarget Target, GuiRenderPatch Patch)
            { return Send(Target, Patch == null ? null : Patch.Elements, true, false, GuiBackendOperationKind.Update); }
            public GuiBackendResult Destroy(GuiBackendTarget Target)
            {
                if (Target == null) throw new InvalidOperationException("backend target is required");
                try { return Transport.Destroy(Target) ?? GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI destroy returned no result"); }
                catch { return GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI destroy failed"); }
            }
            private GuiBackendResult Send(GuiBackendTarget Target, GuiRenderElement[] Elements, bool Update, bool Replace, GuiBackendOperationKind Kind)
            {
                if (Target == null || Elements == null || Elements.Length == 0) throw new InvalidOperationException("backend target and request are required");
                try {
                    string Payload = Serialize(Elements, Update, Replace);
                    if (GuiRenderValue.Utf8Bytes(Payload) > Limits.MaxSerializedOperationBytes)
                        return GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI payload exceeds the serialized operation bound");
                    GuiBackendResult Result = Kind == GuiBackendOperationKind.Replace ? Transport.Replace(Target, Payload) : Transport.Update(Target, Payload);
                    return Result ?? GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI transport returned no result");
                } catch (InvalidOperationException Error) {
                    string Message = Error.Message.Length > 200 ? "Rust CUI serialization failed" : Error.Message;
                    return GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, Message);
                } catch { return GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI serialization/send failed"); }
            }

            private int Measure(GuiBackendTarget Target, GuiRenderElement[] Elements, bool Update, bool Replace)
            {
                if (Target == null || Elements == null || Elements.Length == 0) throw new InvalidOperationException("backend target and request are required");
                int Bytes = GuiRenderValue.Utf8Bytes(Serialize(Elements, Update, Replace));
                if (Bytes > Limits.MaxSerializedOperationBytes) throw new InvalidOperationException("Rust CUI payload exceeds the serialized operation bound");
                return Bytes;
            }

            internal static string Serialize(GuiRenderElement[] Elements, bool Update, bool Replace)
            {
                if (Elements == null || Elements.Length == 0) throw new InvalidOperationException("Rust CUI elements are required");
                var Builder = new StringBuilder();
                using (var Writer = new JsonTextWriter(new StringWriter(Builder, CultureInfo.InvariantCulture))) {
                    Writer.Formatting = Formatting.None; Writer.WriteStartArray();
                    for (int Index = 0; Index < Elements.Length; ++Index) WriteElement(Writer, Elements[Index], Update, Replace && Index == 0);
                    Writer.WriteEndArray(); Writer.Flush();
                }
                return Builder.ToString();
            }
            private static void WriteElement(JsonTextWriter Writer, GuiRenderElement Element, bool Update, bool ReplaceRoot)
            {
                if (Element == null) throw new InvalidOperationException("Rust CUI element is required");
                Writer.WriteStartObject(); Write(Writer, "name", Element.ClientId);
                if (!Update) Write(Writer, "parent", Element.ParentClientId ?? "Overlay");
                if (ReplaceRoot) Write(Writer, "destroyUi", Element.ClientId);
                if (Update) { Writer.WritePropertyName("update"); Writer.WriteValue(true); }
                GuiRenderProperty[] Properties = Element.Properties;
                GuiRenderValue Visible = Find(Properties, GuiRenderPropertyId.Visible, false);
                if (Visible != null) { Writer.WritePropertyName("activeSelf"); Writer.WriteValue(Require(Visible, GuiRenderValueKind.Boolean).Boolean); }
                Writer.WritePropertyName("components"); Writer.WriteStartArray();
                GuiRenderValue Background = Find(Properties, GuiRenderPropertyId.BackgroundColor, false);
                if (Element.Kind == GuiRenderNodeKind.ScrollView) {
                    if (Background != null) WriteColorComponent(Writer, "UnityEngine.UI.Image", Background);
                    if (HasScrollProperty(Properties) || !Update) WriteScrollView(Writer, Properties);
                } else if (Element.Kind == GuiRenderNodeKind.Clip) WriteClip(Writer);
                else if (Element.Kind == GuiRenderNodeKind.Container && Background != null) WriteColorComponent(Writer, "UnityEngine.UI.Image", Background);
                else if (Element.Kind == GuiRenderNodeKind.Button && (Background != null || !Update))
                    WriteButton(Writer, Background, Find(Properties, GuiRenderPropertyId.ActionCommand, false));
                else if (Element.Kind == GuiRenderNodeKind.Text && (HasTextProperty(Properties) || !Update)) WriteText(Writer, Properties, Update);
                else if (Element.Kind == GuiRenderNodeKind.Image && (Find(Properties, GuiRenderPropertyId.ImageSource, false) != null ||
                    Find(Properties, GuiRenderPropertyId.ImageColor, false) != null || !Update)) WriteImage(Writer, Properties);
                if (HasRectProperty(Properties) || !Update) WriteRect(Writer, Properties, Update);
                GuiRenderValue Cursor = Find(Properties, GuiRenderPropertyId.NeedsCursor, false);
                if (Cursor != null && Require(Cursor, GuiRenderValueKind.Boolean).Boolean) {
                    Writer.WriteStartObject(); Write(Writer, "type", "NeedsCursor"); Writer.WriteEndObject();
                }
                Writer.WriteEndArray(); Writer.WriteEndObject();
            }
            private static void WriteColorComponent(JsonTextWriter Writer, string Type, GuiRenderValue Value)
            {
                Writer.WriteStartObject(); Write(Writer, "type", Type); Write(Writer, "color", Color(Require(Value, GuiRenderValueKind.Color).Color));
                Writer.WriteEndObject();
            }
            private static void WriteClip(JsonTextWriter Writer)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.Image"); Write(Writer, "color", "0 0 0 0"); Writer.WriteEndObject();
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.Mask");
                Writer.WritePropertyName("showMaskGraphic"); Writer.WriteValue(false); Writer.WriteEndObject();
            }
            private static void WriteButton(JsonTextWriter Writer, GuiRenderValue Background, GuiRenderValue Action)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.Button");
                if (Background != null) Write(Writer, "color", Color(Require(Background, GuiRenderValueKind.Color).Color));
                if (Action != null) Write(Writer, "command", Require(Action, GuiRenderValueKind.String).Text);
                Writer.WriteEndObject();
            }
            private static void WriteText(JsonTextWriter Writer, GuiRenderProperty[] Properties, bool Partial)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.Text");
                GuiRenderValue Value = Find(Properties, GuiRenderPropertyId.Text, false);
                if (Value != null) Write(Writer, "text", Require(Value, GuiRenderValueKind.String).Text);
                Value = Find(Properties, GuiRenderPropertyId.FontSize, false);
                if (Value != null) { Writer.WritePropertyName("fontSize"); Writer.WriteValue(Require(Value, GuiRenderValueKind.Integer).Integer); }
                Value = Find(Properties, GuiRenderPropertyId.Font, false);
                if (Value != null) Write(Writer, "font", Font(Require(Value, GuiRenderValueKind.GuiFont).FontIdentity));
                else if (!Partial) Require(Value, GuiRenderValueKind.GuiFont);
                GuiRenderValue XValue = Find(Properties, GuiRenderPropertyId.TextXAlignment, false);
                GuiRenderValue YValue = Find(Properties, GuiRenderPropertyId.TextYAlignment, false);
                if (XValue != null || YValue != null) {
                    string X = Require(XValue, GuiRenderValueKind.String).Text;
                    string Y = Require(YValue, GuiRenderValueKind.String).Text;
                    Write(Writer, "align", Alignment(X, Y));
                }
                Value = Find(Properties, GuiRenderPropertyId.TextColor, false);
                if (Value != null) Write(Writer, "color", Color(Require(Value, GuiRenderValueKind.Color).Color));
                Writer.WriteEndObject();
            }
            private static void WriteImage(JsonTextWriter Writer, GuiRenderProperty[] Properties)
            {
                GuiRenderValue SourceValue = Require(Find(Properties, GuiRenderPropertyId.ImageSource, true), GuiRenderValueKind.ImageSource);
                GuiImageSourceValue Source = SourceValue.ImageSource;
                if (Source.Kind == GuiImageSourceKind.None) return;
                Writer.WriteStartObject();
                Write(Writer, "type", Source.Kind == GuiImageSourceKind.SteamAvatar ? "UnityEngine.UI.RawImage" : "UnityEngine.UI.Image");
                GuiRenderValue ColorValue = Find(Properties, GuiRenderPropertyId.ImageColor, false);
                if (ColorValue != null) Write(Writer, "color", Color(Require(ColorValue, GuiRenderValueKind.Color).Color));
                switch (Source.Kind) {
                    case GuiImageSourceKind.Sprite: Write(Writer, "sprite", Source.Primary); break;
                    case GuiImageSourceKind.Png: Write(Writer, "png", Source.Primary); break;
                    case GuiImageSourceKind.Item:
                        Writer.WritePropertyName("itemid"); Writer.WriteValue(Source.ItemId);
                        if (Source.Secondary.Length != 0) { Writer.WritePropertyName("skinid"); Writer.WriteValue(UInt64.Parse(Source.Secondary, CultureInfo.InvariantCulture)); }
                        break;
                    case GuiImageSourceKind.SteamAvatar: Write(Writer, "steamid", Source.Primary); break;
                    default: throw new InvalidOperationException("unknown image source kind");
                }
                Writer.WriteEndObject();
            }
            private static void WriteScrollView(JsonTextWriter Writer, GuiRenderProperty[] Properties)
            {
                bool Horizontal = Require(Find(Properties, GuiRenderPropertyId.ScrollHorizontal, true), GuiRenderValueKind.Boolean).Boolean;
                bool Vertical = Require(Find(Properties, GuiRenderPropertyId.ScrollVertical, true), GuiRenderValueKind.Boolean).Boolean;
                bool Enabled = Require(Find(Properties, GuiRenderPropertyId.ScrollEnabled, true), GuiRenderValueKind.Boolean).Boolean;
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.ScrollView");
                Writer.WritePropertyName("contentTransform"); Writer.WriteStartObject();
                WriteScrollVector(Writer, Properties, GuiRenderPropertyId.ScrollContentAnchorMin, "anchormin");
                WriteScrollVector(Writer, Properties, GuiRenderPropertyId.ScrollContentAnchorMax, "anchormax");
                WriteScrollVector(Writer, Properties, GuiRenderPropertyId.ScrollContentOffsetMin, "offsetmin");
                WriteScrollVector(Writer, Properties, GuiRenderPropertyId.ScrollContentOffsetMax, "offsetmax");
                WriteScrollVector(Writer, Properties, GuiRenderPropertyId.ScrollContentPivot, "pivot");
                Writer.WriteEndObject();
                Writer.WritePropertyName("horizontal"); Writer.WriteValue(Horizontal);
                Writer.WritePropertyName("vertical"); Writer.WriteValue(Vertical);
                Write(Writer, "movementType", "Clamped");
                Writer.WritePropertyName("inertia"); Writer.WriteValue(false);
                Writer.WritePropertyName("scrollSensitivity"); Writer.WriteValue(1);
                Writer.WritePropertyName("enabled"); Writer.WriteValue(Enabled);
                if (Horizontal) WriteScrollbar(Writer, "horizontalScrollbar", Enabled);
                if (Vertical) WriteScrollbar(Writer, "verticalScrollbar", Enabled);
                Writer.WriteEndObject();
            }
            private static void WriteScrollVector(JsonTextWriter Writer, GuiRenderProperty[] Properties, GuiRenderPropertyId Id, string Name)
            { Write(Writer, Name, Vector(Require(Find(Properties, Id, true), GuiRenderValueKind.Vector2).Vector)); }
            private static void WriteScrollbar(JsonTextWriter Writer, string Name, bool Enabled)
            {
                Writer.WritePropertyName(Name); Writer.WriteStartObject();
                Writer.WritePropertyName("autoHide"); Writer.WriteValue(true);
                Writer.WritePropertyName("enabled"); Writer.WriteValue(Enabled);
                Writer.WriteEndObject();
            }
            private static void WriteRect(JsonTextWriter Writer, GuiRenderProperty[] Properties, bool Partial)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "RectTransform");
                WriteVector(Writer, Properties, GuiRenderPropertyId.AnchorMin, "anchormin", Partial);
                WriteVector(Writer, Properties, GuiRenderPropertyId.AnchorMax, "anchormax", Partial);
                WriteVector(Writer, Properties, GuiRenderPropertyId.OffsetMin, "offsetmin", Partial);
                WriteVector(Writer, Properties, GuiRenderPropertyId.OffsetMax, "offsetmax", Partial);
                WriteVector(Writer, Properties, GuiRenderPropertyId.Pivot, "pivot", Partial);
                Writer.WriteEndObject();
            }
            private static void WriteVector(JsonTextWriter Writer, GuiRenderProperty[] Properties, GuiRenderPropertyId Id, string Name, bool Partial)
            {
                GuiRenderValue Value = Find(Properties, Id, false);
                if (Value != null) Write(Writer, Name, Vector(Require(Value, GuiRenderValueKind.Vector2).Vector));
                else if (!Partial) Require(Value, GuiRenderValueKind.Vector2);
            }
            private static bool HasRectProperty(GuiRenderProperty[] Properties)
            { return Find(Properties, GuiRenderPropertyId.AnchorMin, false) != null || Find(Properties, GuiRenderPropertyId.AnchorMax, false) != null ||
                Find(Properties, GuiRenderPropertyId.OffsetMin, false) != null || Find(Properties, GuiRenderPropertyId.OffsetMax, false) != null ||
                Find(Properties, GuiRenderPropertyId.Pivot, false) != null; }
            private static bool HasTextProperty(GuiRenderProperty[] Properties)
            { return Find(Properties, GuiRenderPropertyId.Text, false) != null || Find(Properties, GuiRenderPropertyId.TextColor, false) != null ||
                Find(Properties, GuiRenderPropertyId.FontSize, false) != null || Find(Properties, GuiRenderPropertyId.TextXAlignment, false) != null ||
                Find(Properties, GuiRenderPropertyId.TextYAlignment, false) != null || Find(Properties, GuiRenderPropertyId.Font, false) != null; }
            private static bool HasScrollProperty(GuiRenderProperty[] Properties)
            { return Find(Properties, GuiRenderPropertyId.ScrollContentAnchorMin, false) != null ||
                Find(Properties, GuiRenderPropertyId.ScrollHorizontal, false) != null ||
                Find(Properties, GuiRenderPropertyId.ScrollVertical, false) != null ||
                Find(Properties, GuiRenderPropertyId.ScrollEnabled, false) != null; }
            private static GuiRenderValue Find(GuiRenderProperty[] Properties, GuiRenderPropertyId Id, bool Required)
            {
                foreach (GuiRenderProperty Property in Properties) if (Property.Id == Id) return Property.Value;
                if (Required) throw new InvalidOperationException("Rust CUI render property is missing: " + Id);
                return null;
            }
            private static GuiRenderValue Require(GuiRenderValue Value, GuiRenderValueKind Kind)
            { if (Value == null || Value.Kind != Kind) throw new InvalidOperationException("Rust CUI render property has the wrong type"); return Value; }
            private static string Vector(GuiRenderVector2 Value) { return Number(Value.X) + " " + Number(Value.Y); }
            private static string Color(GuiRenderColor Value) { return Number(Value.R) + " " + Number(Value.G) + " " + Number(Value.B) + " " + Number(Value.A); }
            private static string Number(double Value) { return Value.ToString("R", CultureInfo.InvariantCulture); }
            private static string Alignment(string X, string Y)
            {
                string Vertical = Y == "Top" ? "Upper" : Y == "Bottom" ? "Lower" : "Middle";
                string Horizontal = X == "Left" ? "Left" : X == "Right" ? "Right" : "Center";
                return Vertical + Horizontal;
            }
            private static string Font(GuiFontIdentity Value)
            {
                switch (Value) {
                    case GuiFontIdentity.RobotoCondensedRegular: return "robotocondensed-regular.ttf";
                    case GuiFontIdentity.RobotoCondensedBold: return "robotocondensed-bold.ttf";
                    case GuiFontIdentity.DroidSansMono: return "droidsansmono.ttf";
                    case GuiFontIdentity.PermanentMarker: return "permanentmarker.ttf";
                    default: throw new InvalidOperationException("unknown GUI font identity");
                }
            }
            private static void Write(JsonTextWriter Writer, string Name, string Value)
            { Writer.WritePropertyName(Name); Writer.WriteValue(Value); }
        }
    }
}
