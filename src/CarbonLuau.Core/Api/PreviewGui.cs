using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Tooling assembly API only; excluded from SharedSources and the server source package.
        public sealed partial class PreviewGuiSession
        {
            private readonly PreviewTree Tree;
            public PreviewGuiSession(ulong Domain, Action<int> AdjustGlobalObjects)
            { Tree = new PreviewTree(Domain, AdjustGlobalObjects); }
            public string[] Call(uint Operation, string[] Fields) { return Tree.Call(Operation, Fields); }
            public JObject Plan(string Screen, double Width, double Height) { return Tree.Plan(Screen, Width, Height); }
            public JArray Screens { get { return Tree.Screens(); } }
            public static int GlobalObjectLimit { get { return new GuiConfig().Validate().MaxObjectsGlobal; } }
            public static int ScreenLimit { get { return new GuiConfig().Validate().MaxScreensPerDomain; } }
            public static int NameByteLimit { get { return new GuiConfig().Validate().MaxNameUtf8Bytes; } }

            private sealed partial class PreviewTree : GuiTree<GuiTreeState>
            {
                private readonly Action<int> AdjustGlobal;
                private readonly Stack<GuiTreeState> Publications = new Stack<GuiTreeState>();
                internal PreviewTree(ulong Domain, Action<int> AdjustGlobal) : base(new GuiTreeState(), new GuiConfig().Validate(), 1, Domain)
                { this.AdjustGlobal = AdjustGlobal ?? throw new ArgumentNullException("AdjustGlobal"); }
                protected override void AdjustObjectCount(int Change) { AdjustGlobal(Change); }
                // Preview captures retained state once, after execution. It has no delivery queue.
                protected override void ScreenCreated(ulong ScreenId) { }
                protected override void ScreenDestroyed(ulong ScreenId) { }
                protected override void TreeChanged(GuiRetainedNode Screen, bool TargetUnavailable = false) { }
                protected override void PropertyChanged(GuiRetainedNode Node, GuiPropertyUse Use) { }
                protected override void BeforeDetach(List<ulong> MovingIds) { }
                protected override string[] BeforeDestroy(GuiRetainedNode Root, List<ulong> RemovedIds) { return new string[0]; }
                protected override void ValidateClippingProjectionCandidate(GuiTreeState Candidate, ulong ScreenId)
                { GuiRenderCompiler.Compile(Candidate, Candidate.Nodes[ScreenId], new GuiProjectionNames("preview_"), Limits); }

                internal string[] Call(uint Operation, string[] Fields)
                {
                    if (Fields == null) throw new InvalidOperationException("invalid preview GUI fields");
                    if (Operation >= 10 && Operation <= 12) {
                        RequireFields(Fields, 0);
                        if (Operation == 10) {
                            if (Publications.Count >= 64) throw new FacadeException("GUI publication nesting limit reached");
                            Publications.Push(State.CopyTree());
                        } else {
                            if (Publications.Count == 0) throw new FacadeException("invalid GUI publication completion");
                            GuiTreeState Previous = Publications.Pop();
                            if (Operation == 12) { AdjustGlobal(Previous.Nodes.Count - State.Nodes.Count); State = Previous; }
                        }
                        return new string[0];
                    }
                    if (Fields.Length == 0) throw new InvalidOperationException("invalid preview GUI operation");
                    if (Operation == 20) {
                        switch (Fields[0]) {
                            case "get": RequireFields(Fields, 3); return ReadProperty(Node(Id(Fields[1])), Fields[2]);
                            case "children": {
                                RequireFields(Fields, 2); var Result = new List<string>();
                                foreach (ulong Child in Node(Id(Fields[1])).Children) Result.AddRange(ObjectResult(Node(Child)));
                                return Result.ToArray();
                            }
                            case "find": {
                                RequireFields(Fields, 3); ValidateText(Fields[2], Limits.MaxNameUtf8Bytes, "GUI name");
                                foreach (ulong Child in Node(Id(Fields[1])).Children)
                                    if (Node(Child).Properties[GuiPropertyId.Name].Text == Fields[2]) return ObjectResult(Node(Child));
                                return new string[0];
                            }
                            case "isa": {
                                RequireFields(Fields, 3); GuiClassDescriptor Ignored;
                                GuiRetainedNode Value = Node(Id(Fields[1]));
                                return new[] { GuiSchema.TryGetClass(Fields[2], out Ignored) && GuiSchema.IsA(Value.ClassId, Fields[2]) ? "1" : "0" };
                            }
                            case "event": {
                                RequireFields(Fields, 3);
                                if (Fields[2] != "Activated" || !IsActivatedClass(Node(Id(Fields[1])).ClassId)) throw new FacadeException("event is not available on GUI object");
                                return new string[0];
                            }
                        }
                    }
                    if (Operation == 21) {
                        switch (Fields[0]) {
                            case "create": RequireFields(Fields, 3); return ObjectResult(Create(Fields[1].Length == 0 ? 0 : Id(Fields[1]), Fields[2]));
                            case "set":
                                if (Fields.Length < 4) throw new FacadeException("invalid GUI property assignment");
                                SetProperty(Node(Id(Fields[1])), Fields[2], Fields, 3); return new string[0];
                            case "clone": RequireFields(Fields, 2); return ObjectResult(Clone(Node(Id(Fields[1]))));
                            case "destroy": RequireFields(Fields, 2); return Destroy(Id(Fields[1]));
                        }
                    }
                    throw new InvalidOperationException("Gui:" + Fields[0] + " is unavailable in the static GUI preview environment.");
                }
                internal JArray Screens()
                {
                    return new JArray(State.Nodes.Values.Where(Value => Value.ClassId == GuiClassId.ScreenGui)
                        .OrderBy(Value => Value.Identity.GuiObjectId).Select(Value => new JObject {
                            ["Id"] = ObjectId(Value), ["Name"] = Value.Properties[GuiPropertyId.Name].Text }));
                }
                internal JObject Plan(string ScreenId, double Width, double Height)
                {
                    GuiRetainedNode Screen;
                    if (String.IsNullOrEmpty(ScreenId)) {
                        var Choices = State.Nodes.Values.Where(Value => Value.ClassId == GuiClassId.ScreenGui).ToArray();
                        if (Choices.Length != 1) throw new InvalidOperationException(Choices.Length == 0 ? "No ScreenGui was produced by this entry." : "Multiple ScreenGui objects require an explicit ScreenId selection.");
                        Screen = Choices[0];
                    } else {
                        Screen = Node(Id(ScreenId));
                        if (Screen.ClassId != GuiClassId.ScreenGui) throw new InvalidOperationException("Preview selection must be a ScreenGui.");
                    }
                    var Names = new GuiProjectionNames("preview_");
                    GuiRenderPlan Render = GuiRenderCompiler.Compile(State, Screen, Names, Limits);
                    Dictionary<string, GuiPixelElement> Pixels = GuiPixelProjection.Resolve(Render, Width, Height);
                    var ClipIds = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var Value in State.Nodes.Values) {
                        if (Value.ClassId == GuiClassId.Frame) ClipIds[Names.ClipClientId(Value.Identity.GuiObjectId)] = ObjectId(Value);
                        if (Value.ClassId == GuiClassId.ScrollingFrame) ClipIds[Names.ObjectClientId(Value.Identity.GuiObjectId)] = ObjectId(Value);
                    }
                    var Clips = new JArray(Render.Elements.Where(Value => Value.Kind == GuiRenderNodeKind.Clip || Value.Kind == GuiRenderNodeKind.ScrollView)
                        .Select(Value => new JObject { ["OwnerId"] = ClipIds[Value.ClientId],
                            ["AncestorOwnerIds"] = new JArray(Pixels[Value.ClientId].ClipOwners.Where(Id => Id != Value.ClientId).Select(Id => ClipIds[Id])),
                            ["RectPx"] = Rect(Pixels[Value.ClientId].Rect), ["EffectiveRectPx"] = Rect(Pixels[Value.ClientId].ChildClip),
                            ["Depth"] = Pixels[Value.ClientId].ClipDepth }));
                    var Nodes = new JArray(); int MaximumClip = Pixels.Values.Max(Value => Value.ClipDepth), MaximumArranged = 0;
                    Action<GuiRetainedNode> Append = null;
                    Append = Node => {
                        bool IsScreen = Node.ClassId == GuiClassId.ScreenGui;
                        string ProjectionId = IsScreen ? Names.RootClientId : Names.ObjectClientId(Node.Identity.GuiObjectId);
                        GuiPixelElement Pixel; Pixels.TryGetValue(ProjectionId, out Pixel);
                        var Retained = new JObject();
                        foreach (var Property in Node.Properties.OrderBy(Value => Value.Key)) {
                            string Name = GuiSchema.GetClass(Node.ClassId).Properties.First(Value => Value.Descriptor.Id == Property.Key).Descriptor.Name;
                            Retained[Name] = Stored(Property.Value);
                        }
                        GuiRetainedNode Parent = Node.ParentId.HasValue ? State.Nodes[Node.ParentId.Value] : null;
                        GuiRetainedNode Layout = Parent == null ? null : Parent.Children.Select(Id => State.Nodes[Id]).FirstOrDefault(Value => IsLayoutManager(Value.ClassId));
                        bool Managed = Pixel != null && !IsScreen && Node.Properties[GuiPropertyId.Visible].Boolean && Layout != null;
                        var Item = new JObject {
                            ["Id"] = ObjectId(Node), ["ParentId"] = Parent == null ? JValue.CreateNull() : (JToken)ObjectId(Parent),
                            ["ClassName"] = GuiSchema.GetClass(Node.ClassId).Name, ["Name"] = Node.Properties[GuiPropertyId.Name].Text,
                            ["Children"] = new JArray(Node.Children.Select(Id => Id.ToString(CultureInfo.InvariantCulture))),
                            ["Retained"] = Retained, ["Source"] = null,
                            ["Projected"] = Pixel == null ? null : new JObject {
                                ["RectPx"] = Rect(Pixel.Rect), ["EffectiveClipRectPx"] = Rect(Pixel.Clip),
                                ["EffectiveVisible"] = Pixel.Visible, ["PaintOrder"] = Pixel.PaintOrder,
                                ["ClipDepth"] = Pixel.ClipDepth, ["LayoutOwnerId"] = Managed ? (JToken)ObjectId(Layout) : JValue.CreateNull(),
                                ["DescendantClipOwnerIds"] = new JArray(Pixel.ClipOwners.Select(Id => ClipIds[Id])),
                                ["Interactive"] = !IsScreen && IsActivatedClass(Node.ClassId) && GuiRenderCompiler.IsEffectivelyInteractive(State, Screen, Node) },
                            ["Paint"] = new JArray(), ["Fidelity"] = new JObject { ["Geometry"] = "Authoritative", ["RetainedValues"] = "Authoritative" }
                        };
                        if (Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.TextButton) {
                            Item["Fidelity"]["FontIdentity"] = "Authoritative"; Item["Fidelity"]["TextRasterization"] = "Approximate";
                        }
                        if (Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton) {
                            Item["Fidelity"]["ImageIdentity"] = "Authoritative"; Item["Fidelity"]["ImagePixels"] = "Unavailable";
                        }
                        if (Pixel != null) MaximumClip = Math.Max(MaximumClip, Pixel.ClipDepth);
                        if (IsLayoutManager(Node.ClassId) && Parent != null)
                            MaximumArranged = Math.Max(MaximumArranged, Parent.Children.Count(Id => GuiSchema.IsA(State.Nodes[Id].ClassId, "GuiObject") && State.Nodes[Id].Properties[GuiPropertyId.Visible].Boolean));
                        var Paint = (JArray)Item["Paint"];
                        foreach (GuiRenderElement Element in Render.Elements.Where(Element => Element.ClientId == ProjectionId ||
                            Element.ClientId == Names.TextClientId(Node.Identity.GuiObjectId) || Element.ClientId == Names.ImageClientId(Node.Identity.GuiObjectId))) {
                            if (IsScreen || Element.Kind == GuiRenderNodeKind.Clip) continue;
                            GuiPixelElement Geometry = Pixels[Element.ClientId];
                            var Properties = new JObject();
                            foreach (GuiRenderProperty Property in Element.Properties) {
                                if (Property.Id == GuiRenderPropertyId.ActionCommand || Property.Id == GuiRenderPropertyId.NeedsCursor ||
                                    Property.Id <= GuiRenderPropertyId.Visible || (Property.Id >= GuiRenderPropertyId.ScrollContentAnchorMin && Property.Id <= GuiRenderPropertyId.ScrollContentPivot)) continue;
                                Properties[Property.Id.ToString()] = PaintValue(Property.Value);
                            }
                            Paint.Add(new JObject { ["Kind"] = Element.Kind.ToString(), ["RectPx"] = Rect(Geometry.Rect),
                                ["ClipRectPx"] = Rect(Geometry.Clip), ["Visible"] = Geometry.Visible, ["Order"] = Geometry.PaintOrder,
                                ["Properties"] = Properties, ["Fidelity"] = Element.Kind == GuiRenderNodeKind.Text ? "Approximate" : Element.Kind == GuiRenderNodeKind.Image ? "Unavailable" : "Authoritative" });
                        }
                        if (Node.ClassId == GuiClassId.ScrollingFrame) {
                            GuiPixelElement Content = Pixels[Names.ScrollContentClientId(Node.Identity.GuiObjectId)];
                            Item["Scroll"] = new JObject { ["ViewportRectPx"] = Rect(Pixel.Rect), ["ContentRectPx"] = Rect(Content.Rect),
                                ["OffsetAuthority"] = "PresentationLocal", ["PreviewLocalIntent"] = "InitialTopLeft", ["Fidelity"] = "ConvenienceOnly" };
                        }
                        Nodes.Add(Item); foreach (ulong Child in Node.Children) Append(State.Nodes[Child]);
                    };
                    Append(Screen);
                    return new JObject { ["Screen"] = new JObject { ["Id"] = ObjectId(Screen), ["Name"] = Screen.Properties[GuiPropertyId.Name].Text },
                        ["Viewport"] = new JObject { ["Width"] = Width, ["Height"] = Height }, ["Nodes"] = Nodes, ["Clips"] = Clips,
                        ["Accounting"] = new JObject {
                            ["RetainedObjects"] = Nodes.Count, ["MaxObjectsPerScreen"] = Limits.MaxObjectsPerScreen,
                            ["ProjectedElements"] = Render.ProjectedElementCount, ["MaxProjectedElementsPerScreen"] = Limits.MaxProjectedElementsPerScreen,
                            ["MaximumArrangedChildren"] = MaximumArranged, ["MaxChildrenPerObject"] = Limits.MaxChildrenPerObject,
                            ["MaximumClipDepth"] = MaximumClip, ["MaxEffectiveClipDepth"] = Limits.MaxEffectiveClipDepth,
                            ["CanonicalProjectionEstimatedBytes"] = Render.EstimatedSerializedBytes,
                            ["EstimateScope"] = "Canonical projection with run-local names; excludes production action/delivery serialization" } };
                }
                private static string ObjectId(GuiRetainedNode Node) { return Node.Identity.GuiObjectId.ToString(CultureInfo.InvariantCulture); }
                private static JObject Rect(GuiPixelRect Value) { return new JObject { ["X"] = Value.X, ["Y"] = Value.Y, ["Width"] = Value.Width, ["Height"] = Value.Height }; }
                private static JObject Stored(GuiStoredValue Value) { return new JObject { ["Kind"] = Value.Kind.ToString(), ["Encoded"] = new JArray(Value.Encode()) }; }
                private static JToken PaintValue(GuiRenderValue Value)
                {
                    switch (Value.Kind) {
                        case GuiRenderValueKind.Boolean: return Value.Boolean;
                        case GuiRenderValueKind.Integer: return Value.Integer;
                        case GuiRenderValueKind.Number: return Value.Number;
                        case GuiRenderValueKind.String: return Value.Text;
                        case GuiRenderValueKind.Color: return new JArray(Value.Color.R, Value.Color.G, Value.Color.B, Value.Color.A);
                        case GuiRenderValueKind.GuiFont: return Value.FontIdentity.ToString();
                        case GuiRenderValueKind.ImageSource: return new JArray(Value.ImageSource.Encode());
                        default: throw new InvalidOperationException("unsupported semantic paint value");
                    }
                }
            }
        }
    }
}
