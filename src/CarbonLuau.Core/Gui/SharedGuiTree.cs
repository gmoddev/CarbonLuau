using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Canonical retained mutations and validation. Delivery/publication are adapter hooks.
        internal abstract class GuiTree<TState> where TState : GuiTreeState
        {
            protected TState State;
            protected readonly GuiLimits Limits;
            protected readonly ulong VmGenerationId, DomainLifetimeId;
            protected ulong NextObjectId = 1;
            protected bool Disposed;
            protected GuiTree(TState State, GuiLimits Limits, ulong VmGenerationId, ulong DomainLifetimeId)
            {
                if (VmGenerationId == 0 || DomainLifetimeId == 0) throw new ArgumentOutOfRangeException("GUI owner identity");
                this.State = State ?? throw new ArgumentNullException("State");
                this.Limits = Limits ?? throw new ArgumentNullException("Limits");
                this.VmGenerationId = VmGenerationId; this.DomainLifetimeId = DomainLifetimeId;
            }
            protected abstract void AdjustObjectCount(int Change);
            protected abstract void ScreenCreated(ulong ScreenId);
            protected abstract void ScreenDestroyed(ulong ScreenId);
            protected abstract void TreeChanged(GuiRetainedNode Screen, bool TargetUnavailable = false);
            protected abstract void PropertyChanged(GuiRetainedNode Node, GuiPropertyUse Use);
            protected abstract string[] BeforeDestroy(GuiRetainedNode Root, List<ulong> RemovedIds);
            protected abstract void BeforeDetach(List<ulong> MovingIds);
            protected abstract void ValidateClippingProjectionCandidate(GuiTreeState Candidate, ulong ScreenId);
            protected GuiRetainedNode Create(ulong ParentId, string ClassName)
            {
                GuiClassDescriptor Descriptor;
                if (!GuiSchema.TryGetClass(ClassName, out Descriptor) || !Descriptor.Public || !Descriptor.Constructible)
                    throw new FacadeException("unknown or nonconstructible GUI class");
                GuiCreationScope Scope = ParentId == 0 ? GuiCreationScope.GuiService : GuiCreationScope.GuiObject;
                if ((Descriptor.CreationScope & Scope) == 0) throw new FacadeException("GUI class cannot be created in this scope");
                if (Descriptor.Id == GuiClassId.ScreenGui && ParentId != 0) throw new FacadeException("ScreenGui must be root-level");
                if (State.Nodes.Count >= Limits.MaxObjectsPerDomain) throw new FacadeException("domain GUI object limit reached");
                if (Descriptor.Id == GuiClassId.ScreenGui && State.Screens >= Limits.MaxScreensPerDomain) throw new FacadeException("domain ScreenGui limit reached");
                GuiRetainedNode Parent = ParentId == 0 ? null : Node(ParentId);
                if (Parent != null) ValidateAttachment(null, Descriptor.Id, Parent, 1, 1, IsActivatedClass(Descriptor.Id) ? 1 : 0,
                    TextBytes(Descriptor.Id), ProjectionCost(Descriptor.Id));
                if (NextObjectId == ulong.MaxValue) throw new FacadeException("GUI object identity exhausted");
                AdjustObjectCount(1); ulong ObjectId = NextObjectId++;
                var Result = new GuiRetainedNode(new GuiObjectIdentity(VmGenerationId, DomainLifetimeId, ObjectId), Descriptor.Id);
                ApplyDefaults(Result); State.Nodes.Add(ObjectId, Result); if (Descriptor.Id == GuiClassId.ScreenGui) State.Screens++;
                if (Descriptor.Id == GuiClassId.ScreenGui) ScreenCreated(ObjectId);
                if (Parent != null) { Attach(Result, Parent); TreeChanged(RootScreen(Parent)); }
                return Result;
            }
            protected GuiRetainedNode Clone(GuiRetainedNode Source)
            {
                var SourceIds = new List<ulong>(); Collect(Source, SourceIds);
                int Depth = SubtreeDepth(Source); if (SourceIds.Count > Limits.MaxCloneObjects || Depth > Limits.MaxCloneDepth)
                    throw new FacadeException("GUI clone limit reached");
                if (State.Nodes.Count + SourceIds.Count > Limits.MaxObjectsPerDomain) throw new FacadeException("domain GUI object limit reached");
                int ScreenCopies = Source.ClassId == GuiClassId.ScreenGui ? 1 : 0;
                if (State.Screens + ScreenCopies > Limits.MaxScreensPerDomain) throw new FacadeException("domain ScreenGui limit reached");
                if (NextObjectId > ulong.MaxValue - (ulong)SourceIds.Count) throw new FacadeException("GUI object identity exhausted");
                AdjustObjectCount(SourceIds.Count);
                var Map = new Dictionary<ulong, GuiRetainedNode>();
                foreach (ulong OldId in SourceIds) {
                    GuiRetainedNode Old = State.Nodes[OldId]; ulong NewId = NextObjectId++;
                    var Copy = new GuiRetainedNode(new GuiObjectIdentity(VmGenerationId, DomainLifetimeId, NewId), Old.ClassId);
                    foreach (var Property in Old.Properties) Copy.Properties.Add(Property.Key, Property.Value);
                    State.Nodes.Add(NewId, Copy); Map.Add(OldId, Copy);
                }
                foreach (ulong OldId in SourceIds) foreach (ulong OldChild in State.Nodes[OldId].Children) {
                    GuiRetainedNode Parent = Map[OldId], Child = Map[OldChild]; Parent.Children.Add(Child.Identity.GuiObjectId); Child.ParentId = Parent.Identity.GuiObjectId;
                }
                State.Screens += ScreenCopies;
                if (ScreenCopies != 0) ScreenCreated(Map[Source.Identity.GuiObjectId].Identity.GuiObjectId);
                return Map[Source.Identity.GuiObjectId];
            }
            protected string[] Destroy(ulong ObjectId)
            {
                GuiRetainedNode Root;
                if (!State.Nodes.TryGetValue(ObjectId, out Root)) {
                    if (ObjectId > 0 && ObjectId < NextObjectId) return new string[0];
                    throw new FacadeException("unknown GUI object identity");
                }
                var RemovedIds = new List<ulong>(); Collect(Root, RemovedIds);
                string[] RemovedConnections = BeforeDestroy(Root, RemovedIds);
                GuiRetainedNode AffectedScreen = Root.ClassId == GuiClassId.ScreenGui ? Root : RootScreen(Root);
                if (Root.ParentId.HasValue) State.Nodes[Root.ParentId.Value].Children.Remove(ObjectId);
                foreach (ulong RemovedId in RemovedIds) {
                    GuiRetainedNode Removed = State.Nodes[RemovedId];
                    if (Removed.ClassId == GuiClassId.ScreenGui) State.Screens--;
                    State.Nodes.Remove(RemovedId);
                }
                if (Root.ClassId == GuiClassId.ScreenGui) ScreenDestroyed(Root.Identity.GuiObjectId);
                AdjustObjectCount(-RemovedIds.Count); if (AffectedScreen != null && Root.ClassId != GuiClassId.ScreenGui)
                    TreeChanged(AffectedScreen, true);
                return RemovedConnections;
            }
            protected void SetProperty(GuiRetainedNode Node, string Name, string[] Fields, int KindIndex)
            {
                GuiPropertyUse Use;
                if (!GuiSchema.TryGetProperty(Node.ClassId, Name, out Use) || !Use.Writable) throw new FacadeException("GUI property is unknown or read-only");
                if (Use.Descriptor.Id == GuiPropertyId.Parent) { SetParent(Node, Fields, KindIndex); return; }
                GuiStoredValue Value = ParseValue(Use.Descriptor, Fields, KindIndex);
                GuiStoredValue Existing = Node.Properties[Use.Descriptor.Id]; if (Existing.SameAs(Value)) return;
                if (Use.Descriptor.Id == GuiPropertyId.Text) {
                    GuiRetainedNode Screen = RootScreen(Node); if (Screen != null) {
                        int Current = ScreenTextBytes(Screen); int Old = GuiTextPolicy.Utf8.GetByteCount(Node.Properties[GuiPropertyId.Text].Text);
                        int Next = GuiTextPolicy.Utf8.GetByteCount(Value.Text);
                        if (Current - Old + Next > Limits.MaxTextUtf8BytesPerScreen) throw new FacadeException("ScreenGui aggregate text limit reached");
                    }
                }
                if (Use.Descriptor.Id == GuiPropertyId.ClipsDescendants)
                    ValidateClippingMutation(Node, Value.Boolean);
                Node.Properties[Use.Descriptor.Id] = Value;
                PropertyChanged(Node, Use);
            }
            protected void SetParent(GuiRetainedNode Child, string[] Fields, int KindIndex)
            {
                if (Child.ClassId == GuiClassId.ScreenGui) throw new FacadeException("ScreenGui.Parent is read-only");
                if (KindIndex >= Fields.Length) throw new FacadeException("invalid Parent value");
                GuiRetainedNode Parent = null;
                if (Fields[KindIndex] == "object") {
                    if (Fields.Length != KindIndex + 2) throw new FacadeException("invalid Parent value"); Parent = Node(Id(Fields[KindIndex + 1]));
                } else if (Fields[KindIndex] == "nil") {
                    if (Fields.Length != KindIndex + 1) throw new FacadeException("invalid Parent value");
                } else throw new FacadeException("Parent expects a same-domain GUI object or nil");
                if (Parent != null && Child.ParentId == Parent.Identity.GuiObjectId) return;
                int Objects = SubtreeCount(Child), Depth = SubtreeDepth(Child), Buttons = SubtreeButtons(Child), TextBytes = SubtreeTextBytes(Child);
                if (Parent != null) ValidateAttachment(Child, Child.ClassId, Parent, Objects, Depth, Buttons, TextBytes, SubtreeProjectionCost(Child));
                GuiRetainedNode OldScreen = RootScreen(Child), NewScreen = Parent == null ? null : RootScreen(Parent);
                if (OldScreen != NewScreen) { var MovingIds = new List<ulong>(); Collect(Child, MovingIds); BeforeDetach(MovingIds); }
                if (Child.ParentId.HasValue) State.Nodes[Child.ParentId.Value].Children.Remove(Child.Identity.GuiObjectId);
                Child.ParentId = null; if (Parent != null) Attach(Child, Parent);
                TreeChanged(OldScreen, true);
                if (NewScreen != OldScreen) TreeChanged(NewScreen, true);
            }
            protected void ValidateAttachment(GuiRetainedNode Child, GuiClassId ChildClass, GuiRetainedNode Parent, int Objects, int Depth,
                int Buttons, int TextBytes, int ProjectedElements)
            {
                if (!GuiSchema.GetClass(Parent.ClassId).CanHaveChildren) throw new FacadeException("GUI parent cannot have children");
                if (IsLayoutHelper(ChildClass) && !GuiSchema.IsA(Parent.ClassId, "GuiObject"))
                    throw new FacadeException("GUI layout helpers require a GuiObject parent");
                if (IsLayoutHelper(ChildClass)) {
                    foreach (ulong SiblingId in Parent.Children) {
                        if (Child != null && SiblingId == Child.Identity.GuiObjectId) continue;
                        GuiClassId SiblingClass = State.Nodes[SiblingId].ClassId;
                        if ((IsLayoutManager(ChildClass) && IsLayoutManager(SiblingClass)) ||
                            (ChildClass == GuiClassId.UIPadding && SiblingClass == GuiClassId.UIPadding))
                            throw new FacadeException("GUI layout helper cardinality limit reached");
                    }
                }
                int RenderableChildren = 0;
                foreach (ulong ChildId in Parent.Children) if (GuiSchema.IsA(State.Nodes[ChildId].ClassId, "GuiObject")) RenderableChildren++;
                if (GuiSchema.IsA(ChildClass, "GuiObject") && RenderableChildren >= Limits.MaxChildrenPerObject)
                    throw new FacadeException("GUI child limit reached");
                for (GuiRetainedNode Cursor = Parent; Cursor != null; Cursor = Cursor.ParentId.HasValue ? State.Nodes[Cursor.ParentId.Value] : null)
                    if (Child != null && Cursor.Identity.GuiObjectId == Child.Identity.GuiObjectId) throw new FacadeException("GUI parent cycle rejected");
                int ClipDepth = ClipDepthToRoot(Parent) + (Child == null ? ClipContribution(ChildClass, false) : SubtreeClipDepth(Child));
                if (ClipDepth > Limits.MaxEffectiveClipDepth) throw new FacadeException("GUI effective clipping depth limit reached");
                int ParentDepth = DepthFromRoot(Parent);
                if (ParentDepth + Depth > Limits.MaxTreeDepth) throw new FacadeException("GUI tree depth limit reached");
                GuiRetainedNode Screen = RootScreen(Parent);
                if (Screen != null) {
                    int ExistingObjects = SubtreeCount(Screen); int ExistingButtons = SubtreeButtons(Screen); int ExistingText = SubtreeTextBytes(Screen);
                    int ExistingProjection = SubtreeProjectionCost(Screen);
                    GuiRetainedNode OldScreen = Child == null ? null : RootScreen(Child);
                    bool SameScreen = OldScreen != null && OldScreen.Identity.GuiObjectId == Screen.Identity.GuiObjectId;
                    if (!SameScreen && ExistingObjects + Objects > Limits.MaxObjectsPerScreen) throw new FacadeException("ScreenGui object limit reached");
                    if (!SameScreen && ExistingButtons + Buttons > Limits.MaxButtonsPerScreen) throw new FacadeException("ScreenGui button limit reached");
                    if (!SameScreen && ExistingText + TextBytes > Limits.MaxTextUtf8BytesPerScreen) throw new FacadeException("ScreenGui aggregate text limit reached");
                    if (!SameScreen && ExistingProjection + ProjectedElements > Limits.MaxProjectedElementsPerScreen)
                        throw new FacadeException("ScreenGui projection element limit reached");
                    if (Child != null && ContainsClippingFrame(Child)) ValidateClippingAttachmentCandidate(Child, Parent, Screen);
                }
            }
            protected void ValidateClippingAttachmentCandidate(GuiRetainedNode Child, GuiRetainedNode Parent, GuiRetainedNode Screen)
            {
                GuiTreeState Candidate = State.CopyTree();
                GuiRetainedNode CandidateChild = Candidate.Nodes[Child.Identity.GuiObjectId];
                if (CandidateChild.ParentId.HasValue)
                    Candidate.Nodes[CandidateChild.ParentId.Value].Children.Remove(CandidateChild.Identity.GuiObjectId);
                CandidateChild.ParentId = Parent.Identity.GuiObjectId;
                Candidate.Nodes[Parent.Identity.GuiObjectId].Children.Add(CandidateChild.Identity.GuiObjectId);
                ValidateClippingProjectionCandidate(Candidate, Screen.Identity.GuiObjectId);
            }
            protected static void Attach(GuiRetainedNode Child, GuiRetainedNode Parent)
            { Child.ParentId = Parent.Identity.GuiObjectId; Parent.Children.Add(Child.Identity.GuiObjectId); }
            protected string[] ReadProperty(GuiRetainedNode Node, string Name)
            {
                GuiPropertyUse Use; if (!GuiSchema.TryGetProperty(Node.ClassId, Name, out Use)) throw new FacadeException("unknown GUI property");
                if (Use.Descriptor.Id == GuiPropertyId.ClassName) return new[] {"string", GuiSchema.GetClass(Node.ClassId).Name};
                if (Use.Descriptor.Id == GuiPropertyId.Parent) return Node.ParentId.HasValue ? ObjectValue(State.Nodes[Node.ParentId.Value]) : new[] {"nil"};
                return Node.Properties[Use.Descriptor.Id].Encode();
            }
            protected GuiStoredValue ParseValue(GuiPropertyDescriptor Descriptor, string[] Fields, int KindIndex)
            {
                if (KindIndex >= Fields.Length) throw new FacadeException("missing GUI property value"); string Kind = Fields[KindIndex];
                switch (Descriptor.ValueKind) {
                    case GuiValueKind.String: {
                        if (Kind != "string" || Fields.Length != KindIndex + 2) throw new FacadeException("GUI property expects string");
                        int Limit = Descriptor.Utf8Limit == GuiLimitId.NameUtf8Bytes ? Limits.MaxNameUtf8Bytes :
                            Descriptor.Utf8Limit == GuiLimitId.TextUtf8Bytes ? Limits.MaxTextUtf8Bytes : Limits.MaxTextUtf8Bytes;
                        ValidateText(Fields[KindIndex + 1], Limit, Descriptor.Name);
                        string[] Allowed = Descriptor.AllowedValues;
                        if (Allowed.Length != 0 && Array.IndexOf(Allowed, Fields[KindIndex + 1]) < 0) throw new FacadeException("GUI property value is not allowed");
                        return GuiStoredValue.String(Fields[KindIndex + 1]);
                    }
                    case GuiValueKind.Boolean:
                        if (Kind != "boolean" || Fields.Length != KindIndex + 2 || (Fields[KindIndex + 1] != "0" && Fields[KindIndex + 1] != "1")) throw new FacadeException("GUI property expects boolean");
                        return GuiStoredValue.Bool(Fields[KindIndex + 1] == "1");
                    case GuiValueKind.Integer: {
                        int Value; if (Kind != "integer" || Fields.Length != KindIndex + 2 || !Int32.TryParse(Fields[KindIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out Value))
                            throw new FacadeException("GUI property expects integer");
                        ValidateRange(Value, Descriptor.Minimum, Descriptor.Maximum, Descriptor.Name); return GuiStoredValue.Int(Value);
                    }
                    case GuiValueKind.Number: {
                        double Value = ParseNumber(Fields, KindIndex, "number", 1)[0]; ValidateRange(Value, Descriptor.Minimum, Descriptor.Maximum, Descriptor.Name); return GuiStoredValue.Number(Value);
                    }
                    case GuiValueKind.UDim: {
                        double[] Value = ParseNumber(Fields, KindIndex, "udim", 2);
                        ValidateRange(Value[0], Descriptor.Minimum ?? -8, Descriptor.Maximum ?? 8, Descriptor.Name);
                        bool NonNegative = Descriptor.Id == GuiPropertyId.PaddingTop || Descriptor.Id == GuiPropertyId.PaddingBottom ||
                            Descriptor.Id == GuiPropertyId.PaddingLeft || Descriptor.Id == GuiPropertyId.PaddingRight;
                        ValidateRange(Value[1], NonNegative ? 0 : -32768, 32768, Descriptor.Name);
                        return GuiStoredValue.UDim(Value[0], Value[1]);
                    }
                    case GuiValueKind.UDim2: {
                        double[] Value = ParseNumber(Fields, KindIndex, "udim2", 4);
                        bool NonNegative = Descriptor.Id == GuiPropertyId.CellSize || Descriptor.Id == GuiPropertyId.CellPadding;
                        ValidateRange(Value[0], NonNegative ? 0 : -8, 8, Descriptor.Name); ValidateRange(Value[2], NonNegative ? 0 : -8, 8, Descriptor.Name);
                        ValidateRange(Value[1], NonNegative ? 0 : -32768, 32768, Descriptor.Name); ValidateRange(Value[3], NonNegative ? 0 : -32768, 32768, Descriptor.Name);
                        return GuiStoredValue.UDim2(Value[0], Value[1], Value[2], Value[3]);
                    }
                    case GuiValueKind.Vector2: {
                        double[] Value = ParseNumber(Fields, KindIndex, "vector2", 2); double Minimum = Descriptor.Minimum ?? -32768, Maximum = Descriptor.Maximum ?? 32768;
                        ValidateRange(Value[0], Minimum, Maximum, Descriptor.Name); ValidateRange(Value[1], Minimum, Maximum, Descriptor.Name); return GuiStoredValue.Vector2(Value[0], Value[1]);
                    }
                    case GuiValueKind.Color3: {
                        double[] Value = ParseNumber(Fields, KindIndex, "color3", 3); foreach (double Component in Value) ValidateRange(Component, 0, 1, Descriptor.Name);
                        return GuiStoredValue.Color3(Value[0], Value[1], Value[2]);
                    }
                    case GuiValueKind.ImageSource:
                        return GuiStoredValue.Image(GuiImageSourceValue.Parse(Fields, KindIndex));
                    case GuiValueKind.GuiFont: {
                        if (Kind != "guifont" || Fields.Length != KindIndex + 2) throw new FacadeException("GUI property expects GuiFont");
                        GuiFontIdentity Value;
                        switch (Fields[KindIndex + 1]) {
                            case "RobotoCondensedRegular": Value = GuiFontIdentity.RobotoCondensedRegular; break;
                            case "RobotoCondensedBold": Value = GuiFontIdentity.RobotoCondensedBold; break;
                            case "DroidSansMono": Value = GuiFontIdentity.DroidSansMono; break;
                            case "PermanentMarker": Value = GuiFontIdentity.PermanentMarker; break;
                            default: throw new FacadeException("GUI font identity is not allowed");
                        }
                        return GuiStoredValue.Font(Value);
                    }
                    default: throw new FacadeException("unsupported GUI property type");
                }
            }
            protected static double[] ParseNumber(string[] Fields, int KindIndex, string Kind, int Count)
            {
                if (Fields[KindIndex] != Kind || Fields.Length != KindIndex + Count + 1) throw new FacadeException("GUI property value has the wrong type");
                var Result = new double[Count];
                for (int Index = 0; Index < Count; ++Index) {
                    if (!Double.TryParse(Fields[KindIndex + Index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out Result[Index]) || Double.IsNaN(Result[Index]) || Double.IsInfinity(Result[Index]))
                        throw new FacadeException("GUI numeric value must be finite");
                    if (Result[Index] == 0) Result[Index] = 0;
                }
                return Result;
            }
            protected static void ValidateRange(double Value, double? Minimum, double? Maximum, string Name)
            { if ((Minimum.HasValue && Value < Minimum.Value) || (Maximum.HasValue && Value > Maximum.Value)) throw new FacadeException(Name + " is outside its allowed range"); }
            protected static void ValidateText(string Value, int Limit, string Name) { GuiTextPolicy.Text(Value, Limit, Name); }
            protected void ApplyDefaults(GuiRetainedNode Node)
            {
                string ClassName = GuiSchema.GetClass(Node.ClassId).Name; Node.Properties[GuiPropertyId.Name] = GuiStoredValue.String(ClassName);
                if (Node.ClassId == GuiClassId.ScreenGui) return;
                if (Node.ClassId == GuiClassId.UIListLayout) {
                    Node.Properties[GuiPropertyId.Padding] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.FillDirection] = GuiStoredValue.String("Vertical");
                    Node.Properties[GuiPropertyId.HorizontalAlignment] = GuiStoredValue.String("Left");
                    Node.Properties[GuiPropertyId.VerticalAlignment] = GuiStoredValue.String("Top");
                    return;
                }
                if (Node.ClassId == GuiClassId.UIGridLayout) {
                    Node.Properties[GuiPropertyId.CellSize] = GuiStoredValue.UDim2(0, 100, 0, 100);
                    Node.Properties[GuiPropertyId.CellPadding] = GuiStoredValue.UDim2(0, 0, 0, 0);
                    Node.Properties[GuiPropertyId.FillDirection] = GuiStoredValue.String("Horizontal");
                    Node.Properties[GuiPropertyId.FillDirectionMaxCells] = GuiStoredValue.Int(1);
                    Node.Properties[GuiPropertyId.HorizontalAlignment] = GuiStoredValue.String("Left");
                    Node.Properties[GuiPropertyId.VerticalAlignment] = GuiStoredValue.String("Top");
                    return;
                }
                if (Node.ClassId == GuiClassId.UIPadding) {
                    Node.Properties[GuiPropertyId.PaddingTop] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.PaddingBottom] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.PaddingLeft] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.PaddingRight] = GuiStoredValue.UDim(0, 0);
                    return;
                }
                Node.Properties[GuiPropertyId.Position] = GuiStoredValue.UDim2(0, 0, 0, 0);
                int Height = Node.ClassId == GuiClassId.Frame || Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton ||
                    Node.ClassId == GuiClassId.ScrollingFrame
                    ? 100 : Node.ClassId == GuiClassId.TextLabel ? 30 : 36;
                Node.Properties[GuiPropertyId.Size] = GuiStoredValue.UDim2(0, 100, 0, Height);
                Node.Properties[GuiPropertyId.AnchorPoint] = GuiStoredValue.Vector2(0, 0);
                Node.Properties[GuiPropertyId.Visible] = GuiStoredValue.Bool(true);
                Node.Properties[GuiPropertyId.BackgroundColor3] = GuiStoredValue.Color3(1, 1, 1);
                Node.Properties[GuiPropertyId.BackgroundTransparency] = GuiStoredValue.Number(
                    Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton ? 1 : 0);
                Node.Properties[GuiPropertyId.ZIndex] = GuiStoredValue.Int(1);
                Node.Properties[GuiPropertyId.LayoutOrder] = GuiStoredValue.Int(0);
                if (Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.TextButton) {
                    Node.Properties[GuiPropertyId.Text] = GuiStoredValue.String(""); Node.Properties[GuiPropertyId.TextColor3] = GuiStoredValue.Color3(0, 0, 0);
                    Node.Properties[GuiPropertyId.TextTransparency] = GuiStoredValue.Number(0); Node.Properties[GuiPropertyId.TextSize] = GuiStoredValue.Int(14);
                    Node.Properties[GuiPropertyId.TextXAlignment] = GuiStoredValue.String("Center"); Node.Properties[GuiPropertyId.TextYAlignment] = GuiStoredValue.String("Center");
                    Node.Properties[GuiPropertyId.Font] = GuiStoredValue.Font(GuiFontIdentity.RobotoCondensedRegular);
                }
                if (Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton) {
                    Node.Properties[GuiPropertyId.Image] = GuiStoredValue.Image(GuiImageSourceValue.Parse(new[] {"imagesource", "None"}, 0));
                    Node.Properties[GuiPropertyId.ImageColor3] = GuiStoredValue.Color3(1, 1, 1);
                    Node.Properties[GuiPropertyId.ImageTransparency] = GuiStoredValue.Number(0);
                }
                if (Node.ClassId == GuiClassId.ScrollingFrame) {
                    Node.Properties[GuiPropertyId.CanvasSize] = GuiStoredValue.UDim2(1, 0, 1, 0);
                    Node.Properties[GuiPropertyId.ScrollingDirection] = GuiStoredValue.String("Y");
                    Node.Properties[GuiPropertyId.ScrollingEnabled] = GuiStoredValue.Bool(true);
                }
                if (Node.ClassId == GuiClassId.Frame)
                    Node.Properties[GuiPropertyId.ClipsDescendants] = GuiStoredValue.Bool(false);
            }
            protected GuiRetainedNode Node(ulong Id)
            {
                GuiRetainedNode Result; if (State.Nodes.TryGetValue(Id, out Result)) return Result;
                if (Id > 0 && Id < NextObjectId) throw new FacadeException("GUI object is destroyed");
                throw new FacadeException("unknown GUI object identity");
            }
            protected static ulong Id(string Value) { ulong Result; if (!UInt64.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out Result) || Result == 0) throw new FacadeException("invalid GUI object identity"); return Result; }
            protected static void RequireFields(string[] Fields, int Count) { if (Fields.Length != Count) throw new FacadeException("invalid GUI operation arguments"); }
            protected static string[] ObjectResult(GuiRetainedNode Node) { return new[] {Node.Identity.GuiObjectId.ToString(CultureInfo.InvariantCulture), GuiSchema.GetClass(Node.ClassId).Name}; }
            protected static string[] ObjectValue(GuiRetainedNode Node) { return new[] {"object", Node.Identity.GuiObjectId.ToString(CultureInfo.InvariantCulture), GuiSchema.GetClass(Node.ClassId).Name}; }
            protected void Collect(GuiRetainedNode Root, List<ulong> Result) { Result.Add(Root.Identity.GuiObjectId); foreach (ulong Child in Root.Children) Collect(State.Nodes[Child], Result); }
            protected int SubtreeCount(GuiRetainedNode Root) { int Result = 1; foreach (ulong Child in Root.Children) Result += SubtreeCount(State.Nodes[Child]); return Result; }
            protected int SubtreeDepth(GuiRetainedNode Root) { int Result = 1; foreach (ulong Child in Root.Children) Result = Math.Max(Result, 1 + SubtreeDepth(State.Nodes[Child])); return Result; }
            protected int SubtreeButtons(GuiRetainedNode Root) { int Result = IsActivatedClass(Root.ClassId) ? 1 : 0; foreach (ulong Child in Root.Children) Result += SubtreeButtons(State.Nodes[Child]); return Result; }
            protected int SubtreeProjectionCost(GuiRetainedNode Root)
            { int Result = ProjectionCost(Root.ClassId) + (IsClippingFrame(Root) ? 1 : 0); foreach (ulong Child in Root.Children) Result += SubtreeProjectionCost(State.Nodes[Child]); return Result; }
            protected static int ProjectionCost(GuiClassId ClassId)
            {
                if (ClassId == GuiClassId.ScreenGui || ClassId == GuiClassId.Frame) return 1;
                if (ClassId == GuiClassId.TextLabel || ClassId == GuiClassId.TextButton || ClassId == GuiClassId.ImageLabel) return 2;
                if (ClassId == GuiClassId.ImageButton) return 3;
                if (ClassId == GuiClassId.ScrollingFrame) return 7;
                return 0;
            }
            protected static bool IsClippingFrame(GuiRetainedNode Node)
            {
                GuiStoredValue Value;
                return Node != null && Node.ClassId == GuiClassId.Frame &&
                    Node.Properties.TryGetValue(GuiPropertyId.ClipsDescendants, out Value) && Value.Boolean;
            }
            protected static int ClipContribution(GuiClassId ClassId, bool FrameClips)
            { return ClassId == GuiClassId.ScrollingFrame || (ClassId == GuiClassId.Frame && FrameClips) ? 1 : 0; }
            protected int ClipDepthToRoot(GuiRetainedNode Node)
            {
                int Result = 0;
                for (GuiRetainedNode Cursor = Node; Cursor != null;
                    Cursor = Cursor.ParentId.HasValue ? State.Nodes[Cursor.ParentId.Value] : null)
                    Result += ClipContribution(Cursor.ClassId, IsClippingFrame(Cursor));
                return Result;
            }
            protected int SubtreeClipDepth(GuiRetainedNode Root)
            { return SubtreeClipDepth(Root, false); }
            protected int SubtreeClipDepth(GuiRetainedNode Root, bool ForceRootClip)
            {
                int Own = ClipContribution(Root.ClassId, ForceRootClip || IsClippingFrame(Root)), Maximum = 0;
                foreach (ulong Child in Root.Children) Maximum = Math.Max(Maximum, SubtreeClipDepth(State.Nodes[Child], false));
                return Own + Maximum;
            }
            protected void ValidateClippingMutation(GuiRetainedNode Node, bool Enabled)
            {
                if (Node.ClassId != GuiClassId.Frame) throw new FacadeException("ClipsDescendants is only available on Frame");
                if (!Enabled) return;
                int Ancestors = Node.ParentId.HasValue ? ClipDepthToRoot(State.Nodes[Node.ParentId.Value]) : 0;
                if (Ancestors + SubtreeClipDepth(Node, true) > Limits.MaxEffectiveClipDepth)
                    throw new FacadeException("GUI effective clipping depth limit reached");
                GuiRetainedNode Screen = RootScreen(Node);
                if (Screen != null && SubtreeProjectionCost(Screen) + 1 > Limits.MaxProjectedElementsPerScreen)
                    throw new FacadeException("ScreenGui projection element limit reached");
                ValidateClippingProjectionCandidate(Node, Screen);
            }
            protected static bool IsActivatedClass(GuiClassId ClassId)
            { return ClassId == GuiClassId.TextButton || ClassId == GuiClassId.ImageButton; }
            protected int SubtreeTextBytes(GuiRetainedNode Root)
            {
                int Result = 0; GuiStoredValue Text; if (Root.Properties.TryGetValue(GuiPropertyId.Text, out Text)) Result = GuiTextPolicy.Utf8.GetByteCount(Text.Text);
                foreach (ulong Child in Root.Children) Result += SubtreeTextBytes(State.Nodes[Child]); return Result;
            }
            protected int ScreenTextBytes(GuiRetainedNode Screen) { return SubtreeTextBytes(Screen); }
            protected int TextBytes(GuiClassId ClassId) { return 0; }
            protected bool HasListParent(GuiRetainedNode Node)
            {
                if (!Node.ParentId.HasValue) return false;
                foreach (ulong ChildId in State.Nodes[Node.ParentId.Value].Children)
                    if (State.Nodes[ChildId].ClassId == GuiClassId.UIListLayout) return true;
                return false;
            }
            protected bool HasLayoutParent(GuiRetainedNode Node) { return HasListParent(Node) || HasGridParent(Node); }
            protected bool HasGridParent(GuiRetainedNode Node)
            {
                if (!Node.ParentId.HasValue) return false;
                foreach (ulong ChildId in State.Nodes[Node.ParentId.Value].Children)
                    if (State.Nodes[ChildId].ClassId == GuiClassId.UIGridLayout) return true;
                return false;
            }
            protected static bool IsLayoutManager(GuiClassId ClassId)
            { return ClassId == GuiClassId.UIListLayout || ClassId == GuiClassId.UIGridLayout; }
            protected static bool IsLayoutHelper(GuiClassId ClassId)
            { return IsLayoutManager(ClassId) || ClassId == GuiClassId.UIPadding; }
            protected int DepthFromRoot(GuiRetainedNode Node) { int Result = 1; while (Node.ParentId.HasValue) { Result++; Node = State.Nodes[Node.ParentId.Value]; } return Result; }
            protected GuiRetainedNode RootScreen(GuiRetainedNode Node)
            {
                while (Node.ParentId.HasValue) Node = State.Nodes[Node.ParentId.Value]; return Node.ClassId == GuiClassId.ScreenGui ? Node : null;
            }
            protected void RequireLive() { if (Disposed) throw new FacadeException("stale GUI domain lifetime"); }
            protected bool ContainsClippingFrame(GuiRetainedNode Root)
            {
                if (IsClippingFrame(Root)) return true;
                foreach (ulong Child in Root.Children) if (ContainsClippingFrame(State.Nodes[Child])) return true;
                return false;
            }
            protected void ValidateClippingProjectionCandidate(GuiRetainedNode Node, GuiRetainedNode Screen)
            {
                if (Screen == null) return;
                GuiTreeState Candidate = State.CopyTree();
                Candidate.Nodes[Node.Identity.GuiObjectId].Properties[GuiPropertyId.ClipsDescendants] = GuiStoredValue.Bool(true);
                ValidateClippingProjectionCandidate(Candidate, Screen.Identity.GuiObjectId);
            }
        }
    }
}
