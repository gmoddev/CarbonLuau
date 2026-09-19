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
                if (Element.Kind == GuiRenderNodeKind.Container && Background != null) WriteColorComponent(Writer, "UnityEngine.UI.Image", Background);
                else if (Element.Kind == GuiRenderNodeKind.Button) WriteButton(Writer, Background);
                else if (Element.Kind == GuiRenderNodeKind.Text) WriteText(Writer, Properties);
                WriteRect(Writer, Properties);
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
            private static void WriteButton(JsonTextWriter Writer, GuiRenderValue Background)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.Button");
                if (Background != null) Write(Writer, "color", Color(Require(Background, GuiRenderValueKind.Color).Color));
                Writer.WriteEndObject();
            }
            private static void WriteText(JsonTextWriter Writer, GuiRenderProperty[] Properties)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "UnityEngine.UI.Text");
                Write(Writer, "text", Require(Find(Properties, GuiRenderPropertyId.Text, true), GuiRenderValueKind.String).Text);
                Writer.WritePropertyName("fontSize"); Writer.WriteValue(Require(Find(Properties, GuiRenderPropertyId.FontSize, true), GuiRenderValueKind.Integer).Integer);
                Write(Writer, "font", "robotocondensed-regular.ttf");
                string X = Require(Find(Properties, GuiRenderPropertyId.TextXAlignment, true), GuiRenderValueKind.String).Text;
                string Y = Require(Find(Properties, GuiRenderPropertyId.TextYAlignment, true), GuiRenderValueKind.String).Text;
                Write(Writer, "align", Alignment(X, Y));
                Write(Writer, "color", Color(Require(Find(Properties, GuiRenderPropertyId.TextColor, true), GuiRenderValueKind.Color).Color));
                Writer.WriteEndObject();
            }
            private static void WriteRect(JsonTextWriter Writer, GuiRenderProperty[] Properties)
            {
                Writer.WriteStartObject(); Write(Writer, "type", "RectTransform");
                Write(Writer, "anchormin", Vector(Require(Find(Properties, GuiRenderPropertyId.AnchorMin, true), GuiRenderValueKind.Vector2).Vector));
                Write(Writer, "anchormax", Vector(Require(Find(Properties, GuiRenderPropertyId.AnchorMax, true), GuiRenderValueKind.Vector2).Vector));
                Write(Writer, "offsetmin", Vector(Require(Find(Properties, GuiRenderPropertyId.OffsetMin, true), GuiRenderValueKind.Vector2).Vector));
                Write(Writer, "offsetmax", Vector(Require(Find(Properties, GuiRenderPropertyId.OffsetMax, true), GuiRenderValueKind.Vector2).Vector));
                Write(Writer, "pivot", Vector(Require(Find(Properties, GuiRenderPropertyId.Pivot, true), GuiRenderValueKind.Vector2).Vector));
                Writer.WriteEndObject();
            }
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
            private static void Write(JsonTextWriter Writer, string Name, string Value)
            { Writer.WritePropertyName(Name); Writer.WriteValue(Value); }
        }
    }
}
