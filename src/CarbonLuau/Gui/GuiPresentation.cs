using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiPresentation
        {
            internal readonly ulong ScreenId;
            internal ulong Epoch;
            internal readonly string PlayerToken, PlayerUserId, ClientPrefix;
            internal bool WasSent, NeedsFullResync, SynchronizationUncertain, LastNeedsCursor;
            internal bool ActionInvalidationPending;
            internal ulong SentRevision, ProjectionBlockedRevision, LastAttemptCycle;
            internal int SuccessfulPatchBatches;
            internal readonly Dictionary<ulong, string> ActionTokens = new Dictionary<ulong, string>();
            internal GuiPresentation(ulong ScreenId, ulong Epoch, string PlayerToken, string PlayerUserId, string ClientPrefix)
            {
                this.ScreenId = ScreenId; this.Epoch = Epoch; this.PlayerToken = PlayerToken;
                this.PlayerUserId = PlayerUserId; this.ClientPrefix = ClientPrefix; NeedsFullResync = true;
            }
            internal string RootClientId { get { return ClientPrefix + "r"; } }
            internal GuiBackendTarget Target { get { return new GuiBackendTarget(PlayerToken, PlayerUserId, RootClientId); } }
            internal string ObjectClientId(ulong ObjectId) { return ClientPrefix + "o" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string TextClientId(ulong ObjectId) { return ClientPrefix + "t" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ImageClientId(ulong ObjectId) { return ClientPrefix + "i" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ActionClientId(ulong ObjectId) { return ClientPrefix + "a" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ScrollContentClientId(ulong ObjectId) { return ObjectClientId(ObjectId) + "___Content"; }
            internal GuiPresentation Copy()
            {
                var Result = new GuiPresentation(ScreenId, Epoch, PlayerToken, PlayerUserId, ClientPrefix) {
                    WasSent = WasSent, NeedsFullResync = NeedsFullResync, SynchronizationUncertain = SynchronizationUncertain,
                    LastNeedsCursor = LastNeedsCursor, SentRevision = SentRevision, ProjectionBlockedRevision = ProjectionBlockedRevision,
                    LastAttemptCycle = LastAttemptCycle, SuccessfulPatchBatches = SuccessfulPatchBatches,
                    ActionInvalidationPending = ActionInvalidationPending
                };
                foreach (var Value in ActionTokens) Result.ActionTokens.Add(Value.Key, Value.Value);
                return Result;
            }
        }

        internal sealed class GuiScreenSynchronization
        {
            internal ulong Revision = 1;
            internal bool FullRebuildRequired;
            internal readonly SortedDictionary<ulong, HashSet<GuiPropertyId>> DirtyObjects =
                new SortedDictionary<ulong, HashSet<GuiPropertyId>>();
            internal GuiScreenSynchronization Copy()
            {
                var Result = new GuiScreenSynchronization {Revision = Revision, FullRebuildRequired = FullRebuildRequired};
                foreach (var Value in DirtyObjects) Result.DirtyObjects.Add(Value.Key, new HashSet<GuiPropertyId>(Value.Value));
                return Result;
            }
        }

        internal static class GuiRenderCompiler
        {
            internal static GuiRenderPlan Compile(GuiRetainedState State, GuiRetainedNode Screen, GuiPresentation Presentation, GuiLimits Limits)
            { return Compile(State, Screen, Presentation, Limits, null); }
            internal static GuiRenderPlan Compile(GuiRetainedState State, GuiRetainedNode Screen, GuiPresentation Presentation,
                GuiLimits Limits, Func<GuiRetainedNode, string> ActionCommand)
            {
                if (State == null || Screen == null || Presentation == null || Limits == null || Screen.ClassId != GuiClassId.ScreenGui)
                    throw new InvalidOperationException("GUI render compiler requires a live ScreenGui presentation");
                var Elements = new List<GuiRenderElement>();
                bool NeedsCursor = NeedsCursorFor(State, Screen);
                Elements.Add(new GuiRenderElement(Presentation.RootClientId, null, GuiRenderNodeKind.Container, Limits,
                    Vector(GuiRenderPropertyId.AnchorMin, 0, 0), Vector(GuiRenderPropertyId.AnchorMax, 1, 1),
                    Vector(GuiRenderPropertyId.OffsetMin, 0, 0), Vector(GuiRenderPropertyId.OffsetMax, 0, 0),
                    Vector(GuiRenderPropertyId.Pivot, 0.5, 0.5), Boolean(GuiRenderPropertyId.Visible, true),
                    Boolean(GuiRenderPropertyId.NeedsCursor, NeedsCursor)));
                AppendChildren(State, Screen, Presentation.RootClientId, Presentation, Limits, Elements, true, ActionCommand);
                int ProjectedElements = 0;
                foreach (GuiRenderElement Element in Elements) ProjectedElements = checked(ProjectedElements + Element.ProjectedElementCost);
                if (ProjectedElements > Limits.MaxProjectedElementsPerScreen)
                    throw new FacadeException("GUI full presentation exceeds the projection element bound");
                int Estimated = 16;
                foreach (GuiRenderElement Element in Elements) Estimated = checked(Estimated + Element.CanonicalUtf8Bytes + 64);
                if (Estimated > Limits.MaxSerializedOperationBytes) throw new FacadeException("GUI full presentation exceeds the serialized operation bound");
                return new GuiRenderPlan(Limits, Estimated, Elements.ToArray());
            }

            internal static GuiRenderPatch CompilePatch(GuiRetainedState State, GuiRetainedNode Screen,
                GuiPresentation Presentation, GuiScreenSynchronization Synchronization, GuiLimits Limits)
            {
                if (State == null || Screen == null || Presentation == null || Synchronization == null || Limits == null ||
                    Screen.ClassId != GuiClassId.ScreenGui || Synchronization.DirtyObjects.Count == 0)
                    throw new InvalidOperationException("GUI patch compiler requires bounded dirty ScreenGui state");
                if (NeedsCursorFor(State, Screen) != Presentation.LastNeedsCursor)
                    throw new GuiFullRebuildRequiredException("cursor requirement changed");
                var Elements = new List<GuiRenderElement>();
                var ProjectedByParent = new Dictionary<ulong, Dictionary<ulong, GuiProjectedRect>>();
                foreach (var Dirty in Synchronization.DirtyObjects) {
                    GuiRetainedNode Node;
                    if (!State.Nodes.TryGetValue(Dirty.Key, out Node)) throw new GuiFullRebuildRequiredException("dirty GUI object no longer exists");
                    bool Layout = HasAny(Dirty.Value, GuiPropertyId.Position, GuiPropertyId.Size, GuiPropertyId.AnchorPoint, GuiPropertyId.LayoutProjection);
                    bool Visible = Dirty.Value.Contains(GuiPropertyId.Visible);
                    bool Background = HasAny(Dirty.Value, GuiPropertyId.BackgroundColor3, GuiPropertyId.BackgroundTransparency);
                    var Main = new List<GuiRenderProperty>();
                    if (Layout) AddLayout(State, Node, Main, ProjectedByParent);
                    if (Visible) Main.Add(Boolean(GuiRenderPropertyId.Visible, Boolean(Node, GuiPropertyId.Visible)));
                    if (Background) {
                        double[] ColorValue = Numbers(Node, GuiPropertyId.BackgroundColor3);
                        Main.Add(Color(GuiRenderPropertyId.BackgroundColor, ColorValue, 1 - Number(Node, GuiPropertyId.BackgroundTransparency)));
                    }
                    string ActionToken;
                    if (Node.ClassId == GuiClassId.TextButton && Presentation.ActionTokens.TryGetValue(Dirty.Key, out ActionToken))
                        Main.Add(String(GuiRenderPropertyId.ActionCommand, GuiRetainedWorld.ActionCommand + " " + ActionToken));
                    if (Main.Count != 0) Elements.Add(new GuiRenderElement(Presentation.ObjectClientId(Dirty.Key), null,
                        RenderKind(Node.ClassId), Limits, Main.ToArray()));
                    bool Text = Dirty.Value.Contains(GuiPropertyId.Text);
                    bool TextColor = HasAny(Dirty.Value, GuiPropertyId.TextColor3, GuiPropertyId.TextTransparency);
                    bool TextSize = Dirty.Value.Contains(GuiPropertyId.TextSize);
                    bool TextAlignment = HasAny(Dirty.Value, GuiPropertyId.TextXAlignment, GuiPropertyId.TextYAlignment);
                    var TextProperties = new List<GuiRenderProperty>();
                    if (Dirty.Value.Contains(GuiPropertyId.ContentProjection)) AddContentLayout(State, Node, TextProperties);
                    if (Text) TextProperties.Add(String(GuiRenderPropertyId.Text, TextValue(Node, GuiPropertyId.Text)));
                    if (TextColor) {
                        double[] ColorValue = Numbers(Node, GuiPropertyId.TextColor3);
                        TextProperties.Add(Color(GuiRenderPropertyId.TextColor, ColorValue, 1 - Number(Node, GuiPropertyId.TextTransparency)));
                    }
                    if (TextSize) TextProperties.Add(Integer(GuiRenderPropertyId.FontSize, Integer(Node, GuiPropertyId.TextSize)));
                    if (TextAlignment) {
                        TextProperties.Add(String(GuiRenderPropertyId.TextXAlignment, TextValue(Node, GuiPropertyId.TextXAlignment)));
                        TextProperties.Add(String(GuiRenderPropertyId.TextYAlignment, TextValue(Node, GuiPropertyId.TextYAlignment)));
                    }
                    if (TextProperties.Count != 0) Elements.Add(new GuiRenderElement(Presentation.TextClientId(Dirty.Key), null,
                        GuiRenderNodeKind.Text, Limits, TextProperties.ToArray()));
                    bool ImageColor = HasAny(Dirty.Value, GuiPropertyId.ImageColor3, GuiPropertyId.ImageTransparency);
                    if (ImageColor) {
                        double[] ColorValue = Numbers(Node, GuiPropertyId.ImageColor3);
                        Elements.Add(new GuiRenderElement(Presentation.ImageClientId(Dirty.Key), null, GuiRenderNodeKind.Image, Limits,
                            Image(GuiRenderPropertyId.ImageSource, Property(Node, GuiPropertyId.Image).ImageSource),
                            Color(GuiRenderPropertyId.ImageColor, ColorValue, 1 - Number(Node, GuiPropertyId.ImageTransparency))));
                    }
                }
                if (Elements.Count == 0) throw new GuiFullRebuildRequiredException("dirty state produced no safe patch");
                int Estimated = 16;
                foreach (GuiRenderElement Element in Elements) Estimated = checked(Estimated + Element.CanonicalUtf8Bytes + 64);
                return new GuiRenderPatch(Limits, Estimated, Elements.ToArray());
            }

            private static void AppendChildren(GuiRetainedState State, GuiRetainedNode Parent, string ParentClientId,
                GuiPresentation Presentation, GuiLimits Limits, List<GuiRenderElement> Elements, bool AncestorsVisible,
                Func<GuiRetainedNode, string> ActionCommand)
            {
                var Ordered = new List<GuiRetainedNode>();
                foreach (ulong ChildId in Parent.Children) {
                    GuiRetainedNode Child = State.Nodes[ChildId];
                    if (GuiSchema.IsA(Child.ClassId, "GuiObject")) Ordered.Add(Child);
                }
                Dictionary<ulong, GuiProjectedRect> Rectangles = ProjectChildren(State, Parent);
                Ordered.Sort((Left, Right) => {
                    int Result = Integer(Left, GuiPropertyId.ZIndex).CompareTo(Integer(Right, GuiPropertyId.ZIndex));
                    if (Result != 0) return Result;
                    Result = Parent.Children.IndexOf(Left.Identity.GuiObjectId).CompareTo(Parent.Children.IndexOf(Right.Identity.GuiObjectId));
                    return Result != 0 ? Result : Left.Identity.GuiObjectId.CompareTo(Right.Identity.GuiObjectId);
                });
                foreach (GuiRetainedNode Node in Ordered) {
                    bool EffectiveVisible = AncestorsVisible && Boolean(Node, GuiPropertyId.Visible);
                    string ClientId = Presentation.ObjectClientId(Node.Identity.GuiObjectId);
                    GuiRenderNodeKind Kind = RenderKind(Node.ClassId);
                    var Properties = new List<GuiRenderProperty>();
                    AddLayout(Node, Rectangles[Node.Identity.GuiObjectId], Properties);
                    Properties.Add(Boolean(GuiRenderPropertyId.Visible, Boolean(Node, GuiPropertyId.Visible)));
                    double[] Background = Numbers(Node, GuiPropertyId.BackgroundColor3);
                    Properties.Add(Color(GuiRenderPropertyId.BackgroundColor, Background,
                        1 - Number(Node, GuiPropertyId.BackgroundTransparency)));
                    if (EffectiveVisible && Node.ClassId == GuiClassId.TextButton && ActionCommand != null) {
                        string Command = ActionCommand(Node);
                        if (Command != null) Properties.Add(String(GuiRenderPropertyId.ActionCommand, Command));
                    }
                    if (Node.ClassId == GuiClassId.ScrollingFrame) AddScrollProperties(Node, Properties);
                    Elements.Add(new GuiRenderElement(ClientId, ParentClientId, Kind, Limits, Properties.ToArray()));
                    if (Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.TextButton) {
                        double[] TextColor = Numbers(Node, GuiPropertyId.TextColor3);
                        Elements.Add(new GuiRenderElement(Presentation.TextClientId(Node.Identity.GuiObjectId), ClientId,
                            GuiRenderNodeKind.Text, Limits,
                            ContentProperty(State, Node, GuiRenderPropertyId.AnchorMin), ContentProperty(State, Node, GuiRenderPropertyId.AnchorMax),
                            ContentProperty(State, Node, GuiRenderPropertyId.OffsetMin), ContentProperty(State, Node, GuiRenderPropertyId.OffsetMax),
                            Vector(GuiRenderPropertyId.Pivot, 0.5, 0.5), Boolean(GuiRenderPropertyId.Visible, true),
                            String(GuiRenderPropertyId.Text, TextValue(Node, GuiPropertyId.Text)),
                            Color(GuiRenderPropertyId.TextColor, TextColor, 1 - Number(Node, GuiPropertyId.TextTransparency)),
                            Integer(GuiRenderPropertyId.FontSize, Integer(Node, GuiPropertyId.TextSize)),
                            String(GuiRenderPropertyId.TextXAlignment, TextValue(Node, GuiPropertyId.TextXAlignment)),
                            String(GuiRenderPropertyId.TextYAlignment, TextValue(Node, GuiPropertyId.TextYAlignment))));
                    }
                    if (Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton) {
                        double[] ImageColor = Numbers(Node, GuiPropertyId.ImageColor3);
                        Elements.Add(new GuiRenderElement(Presentation.ImageClientId(Node.Identity.GuiObjectId), ClientId,
                            GuiRenderNodeKind.Image, Limits,
                            Vector(GuiRenderPropertyId.AnchorMin, 0, 0), Vector(GuiRenderPropertyId.AnchorMax, 1, 1),
                            Vector(GuiRenderPropertyId.OffsetMin, 0, 0), Vector(GuiRenderPropertyId.OffsetMax, 0, 0),
                            Vector(GuiRenderPropertyId.Pivot, 0.5, 0.5), Boolean(GuiRenderPropertyId.Visible, true),
                            Image(GuiRenderPropertyId.ImageSource, Property(Node, GuiPropertyId.Image).ImageSource),
                            Color(GuiRenderPropertyId.ImageColor, ImageColor, 1 - Number(Node, GuiPropertyId.ImageTransparency))));
                    }
                    string ChildParentClientId = Node.ClassId == GuiClassId.ScrollingFrame
                        ? Presentation.ScrollContentClientId(Node.Identity.GuiObjectId) : ClientId;
                    AppendChildren(State, Node, ChildParentClientId, Presentation, Limits, Elements, EffectiveVisible, ActionCommand);
                    if (Node.ClassId == GuiClassId.ImageButton) {
                        var ActionProperties = new List<GuiRenderProperty> {
                            Vector(GuiRenderPropertyId.AnchorMin, 0, 0), Vector(GuiRenderPropertyId.AnchorMax, 1, 1),
                            Vector(GuiRenderPropertyId.OffsetMin, 0, 0), Vector(GuiRenderPropertyId.OffsetMax, 0, 0),
                            Vector(GuiRenderPropertyId.Pivot, 0.5, 0.5), Boolean(GuiRenderPropertyId.Visible, true),
                            Color(GuiRenderPropertyId.BackgroundColor, new[] {1.0, 1.0, 1.0}, 0)
                        };
                        if (EffectiveVisible && ActionCommand != null) {
                            string Command = ActionCommand(Node);
                            if (Command != null) ActionProperties.Add(String(GuiRenderPropertyId.ActionCommand, Command));
                        }
                        Elements.Add(new GuiRenderElement(Presentation.ActionClientId(Node.Identity.GuiObjectId), ClientId,
                            GuiRenderNodeKind.Button, Limits, ActionProperties.ToArray()));
                    }
                }
            }

            private static GuiRenderNodeKind RenderKind(GuiClassId ClassId)
            {
                if (ClassId == GuiClassId.TextButton) return GuiRenderNodeKind.Button;
                if (ClassId == GuiClassId.ScrollingFrame) return GuiRenderNodeKind.ScrollView;
                return GuiRenderNodeKind.Container;
            }

            private static void AddScrollProperties(GuiRetainedNode Node, List<GuiRenderProperty> Result)
            {
                double[] Canvas = Numbers(Node, GuiPropertyId.CanvasSize);
                Result.Add(Vector(GuiRenderPropertyId.ScrollContentAnchorMin, 0, 1 - Canvas[2]));
                Result.Add(Vector(GuiRenderPropertyId.ScrollContentAnchorMax, Canvas[0], 1));
                Result.Add(Vector(GuiRenderPropertyId.ScrollContentOffsetMin, 0, -Canvas[3]));
                Result.Add(Vector(GuiRenderPropertyId.ScrollContentOffsetMax, Canvas[1], 0));
                Result.Add(Vector(GuiRenderPropertyId.ScrollContentPivot, 0, 1));
                string Direction = TextValue(Node, GuiPropertyId.ScrollingDirection);
                Result.Add(Boolean(GuiRenderPropertyId.ScrollHorizontal, Direction == "X" || Direction == "XY"));
                Result.Add(Boolean(GuiRenderPropertyId.ScrollVertical, Direction == "Y" || Direction == "XY"));
                Result.Add(Boolean(GuiRenderPropertyId.ScrollEnabled, Boolean(Node, GuiPropertyId.ScrollingEnabled)));
            }

            private static void AddLayout(GuiRetainedState State, GuiRetainedNode Node, List<GuiRenderProperty> Result,
                Dictionary<ulong, Dictionary<ulong, GuiProjectedRect>> ProjectedByParent)
            {
                if (!Node.ParentId.HasValue) { AddLayout(Node, RetainedRect(Node), Result); return; }
                Dictionary<ulong, GuiProjectedRect> Rectangles;
                if (!ProjectedByParent.TryGetValue(Node.ParentId.Value, out Rectangles)) {
                    Rectangles = ProjectChildren(State, State.Nodes[Node.ParentId.Value]);
                    ProjectedByParent.Add(Node.ParentId.Value, Rectangles);
                }
                AddLayout(Node, Rectangles[Node.Identity.GuiObjectId], Result);
            }

            private static void AddLayout(GuiRetainedNode Node, GuiProjectedRect Rect, List<GuiRenderProperty> Result)
            {
                double[] Position = Rect.Position, Size = Rect.Size, Anchor = Numbers(Node, GuiPropertyId.AnchorPoint);
                double Ax = Anchor[0], Ay = Anchor[1];
                Result.Add(Vector(GuiRenderPropertyId.AnchorMin, Position[0] - Ax * Size[0], 1 - Position[2] - (1 - Ay) * Size[2]));
                Result.Add(Vector(GuiRenderPropertyId.AnchorMax, Position[0] + (1 - Ax) * Size[0], 1 - Position[2] + Ay * Size[2]));
                Result.Add(Vector(GuiRenderPropertyId.OffsetMin, Position[1] - Ax * Size[1], -Position[3] - (1 - Ay) * Size[3]));
                Result.Add(Vector(GuiRenderPropertyId.OffsetMax, Position[1] + (1 - Ax) * Size[1], -Position[3] + Ay * Size[3]));
                Result.Add(Vector(GuiRenderPropertyId.Pivot, Ax, 1 - Ay));
            }

            private static void AddContentLayout(GuiRetainedState State, GuiRetainedNode Node, List<GuiRenderProperty> Result)
            {
                GuiContentRect Content = ContentRect(State, Node);
                Result.Add(Vector(GuiRenderPropertyId.AnchorMin, Content.XStart.Scale, 1 - Content.YStart.Scale - Content.YSize.Scale));
                Result.Add(Vector(GuiRenderPropertyId.AnchorMax, Content.XStart.Scale + Content.XSize.Scale, 1 - Content.YStart.Scale));
                Result.Add(Vector(GuiRenderPropertyId.OffsetMin, Content.XStart.Offset, -Content.YStart.Offset - Content.YSize.Offset));
                Result.Add(Vector(GuiRenderPropertyId.OffsetMax, Content.XStart.Offset + Content.XSize.Offset, -Content.YStart.Offset));
            }

            private static GuiRenderProperty ContentProperty(GuiRetainedState State, GuiRetainedNode Node, GuiRenderPropertyId Id)
            {
                var Values = new List<GuiRenderProperty>(); AddContentLayout(State, Node, Values);
                foreach (GuiRenderProperty Value in Values) if (Value.Id == Id) return Value;
                throw new InvalidOperationException("GUI content projection is missing");
            }

            private sealed class GuiAffine
            {
                internal readonly double Scale, Offset;
                internal GuiAffine(double Scale, double Offset) { this.Scale = Scale; this.Offset = Offset; }
                internal static GuiAffine Add(GuiAffine Left, GuiAffine Right)
                { return new GuiAffine(Left.Scale + Right.Scale, Left.Offset + Right.Offset); }
                internal static GuiAffine Subtract(GuiAffine Left, GuiAffine Right)
                { return new GuiAffine(Left.Scale - Right.Scale, Left.Offset - Right.Offset); }
                internal static GuiAffine Multiply(GuiAffine Value, double Factor)
                { return new GuiAffine(Value.Scale * Factor, Value.Offset * Factor); }
            }

            private sealed class GuiContentRect
            {
                internal readonly GuiAffine XStart, YStart, XSize, YSize;
                internal GuiContentRect(GuiAffine XStart, GuiAffine YStart, GuiAffine XSize, GuiAffine YSize)
                { this.XStart = XStart; this.YStart = YStart; this.XSize = XSize; this.YSize = YSize; }
            }

            private sealed class GuiProjectedRect
            {
                internal readonly double[] Position, Size;
                internal GuiProjectedRect(GuiAffine XPosition, GuiAffine YPosition, GuiAffine XSize, GuiAffine YSize)
                { Position = new[] {XPosition.Scale, XPosition.Offset, YPosition.Scale, YPosition.Offset}; Size = new[] {XSize.Scale, XSize.Offset, YSize.Scale, YSize.Offset}; }
            }

            private static Dictionary<ulong, GuiProjectedRect> ProjectChildren(GuiRetainedState State, GuiRetainedNode Parent)
            {
                GuiContentRect Content = ContentRect(State, Parent);
                GuiRetainedNode Layout = null;
                var Children = new List<GuiRetainedNode>();
                foreach (ulong ChildId in Parent.Children) {
                    GuiRetainedNode Child = State.Nodes[ChildId];
                    if (Child.ClassId == GuiClassId.UIListLayout || Child.ClassId == GuiClassId.UIGridLayout) Layout = Child;
                    else if (GuiSchema.IsA(Child.ClassId, "GuiObject")) Children.Add(Child);
                }
                var Result = new Dictionary<ulong, GuiProjectedRect>();
                if (Layout == null) {
                    foreach (GuiRetainedNode Child in Children) Result.Add(Child.Identity.GuiObjectId, ProjectRetained(Content, Child));
                    return Result;
                }
                var Visible = new List<GuiRetainedNode>();
                foreach (GuiRetainedNode Child in Children) if (Boolean(Child, GuiPropertyId.Visible)) Visible.Add(Child);
                Visible.Sort((Left, Right) => {
                    int Order = Integer(Left, GuiPropertyId.LayoutOrder).CompareTo(Integer(Right, GuiPropertyId.LayoutOrder));
                    if (Order != 0) return Order;
                    Order = Parent.Children.IndexOf(Left.Identity.GuiObjectId).CompareTo(Parent.Children.IndexOf(Right.Identity.GuiObjectId));
                    return Order != 0 ? Order : Left.Identity.GuiObjectId.CompareTo(Right.Identity.GuiObjectId);
                });
                if (Layout.ClassId == GuiClassId.UIGridLayout) return ProjectGridChildren(Content, Layout, Children, Visible);
                bool Vertical = TextValue(Layout, GuiPropertyId.FillDirection) == "Vertical";
                GuiAffine MainExtent = new GuiAffine(0, 0);
                var Sizes = new Dictionary<ulong, GuiProjectedRect>();
                foreach (GuiRetainedNode Child in Children) {
                    GuiProjectedRect Rect = ProjectRetained(Content, Child); Sizes.Add(Child.Identity.GuiObjectId, Rect);
                    if (Boolean(Child, GuiPropertyId.Visible)) MainExtent = GuiAffine.Add(MainExtent, Affine(Vertical ? Rect.Size[2] : Rect.Size[0], Vertical ? Rect.Size[3] : Rect.Size[1]));
                }
                double[] Padding = Numbers(Layout, GuiPropertyId.Padding);
                GuiAffine ContentMain = Vertical ? Content.YSize : Content.XSize;
                GuiAffine Gap = GuiAffine.Add(GuiAffine.Multiply(ContentMain, Padding[0]), new GuiAffine(0, Padding[1]));
                if (Visible.Count > 1) MainExtent = GuiAffine.Add(MainExtent, GuiAffine.Multiply(Gap, Visible.Count - 1));
                double MainFactor = AlignmentFactor(TextValue(Layout, Vertical ? GuiPropertyId.VerticalAlignment : GuiPropertyId.HorizontalAlignment));
                GuiAffine Cursor = GuiAffine.Add(Vertical ? Content.YStart : Content.XStart,
                    GuiAffine.Multiply(GuiAffine.Subtract(ContentMain, MainExtent), MainFactor));
                foreach (GuiRetainedNode Child in Visible) {
                    GuiProjectedRect Size = Sizes[Child.Identity.GuiObjectId];
                    GuiAffine XSize = Affine(Size.Size[0], Size.Size[1]), YSize = Affine(Size.Size[2], Size.Size[3]);
                    GuiAffine CrossSize = Vertical ? XSize : YSize;
                    GuiAffine ContentCross = Vertical ? Content.XSize : Content.YSize;
                    string CrossName = TextValue(Layout, Vertical ? GuiPropertyId.HorizontalAlignment : GuiPropertyId.VerticalAlignment);
                    GuiAffine Cross = GuiAffine.Add(Vertical ? Content.XStart : Content.YStart,
                        GuiAffine.Multiply(GuiAffine.Subtract(ContentCross, CrossSize), AlignmentFactor(CrossName)));
                    double[] Anchor = Numbers(Child, GuiPropertyId.AnchorPoint);
                    GuiAffine XStart = Vertical ? Cross : Cursor, YStart = Vertical ? Cursor : Cross;
                    GuiAffine XPosition = GuiAffine.Add(XStart, GuiAffine.Multiply(XSize, Anchor[0]));
                    GuiAffine YPosition = GuiAffine.Add(YStart, GuiAffine.Multiply(YSize, Anchor[1]));
                    Result.Add(Child.Identity.GuiObjectId, new GuiProjectedRect(XPosition, YPosition, XSize, YSize));
                    Cursor = GuiAffine.Add(Cursor, GuiAffine.Add(Vertical ? YSize : XSize, Gap));
                }
                foreach (GuiRetainedNode Child in Children)
                    if (!Result.ContainsKey(Child.Identity.GuiObjectId)) Result.Add(Child.Identity.GuiObjectId, ProjectRetained(Content, Child));
                return Result;
            }

            private static Dictionary<ulong, GuiProjectedRect> ProjectGridChildren(GuiContentRect Content, GuiRetainedNode Layout,
                List<GuiRetainedNode> Children, List<GuiRetainedNode> Visible)
            {
                var Result = new Dictionary<ulong, GuiProjectedRect>();
                double[] CellValue = Numbers(Layout, GuiPropertyId.CellSize), PaddingValue = Numbers(Layout, GuiPropertyId.CellPadding);
                GuiAffine XCell = GuiAffine.Add(GuiAffine.Multiply(Content.XSize, CellValue[0]), new GuiAffine(0, CellValue[1]));
                GuiAffine YCell = GuiAffine.Add(GuiAffine.Multiply(Content.YSize, CellValue[2]), new GuiAffine(0, CellValue[3]));
                GuiAffine XGap = GuiAffine.Add(GuiAffine.Multiply(Content.XSize, PaddingValue[0]), new GuiAffine(0, PaddingValue[1]));
                GuiAffine YGap = GuiAffine.Add(GuiAffine.Multiply(Content.YSize, PaddingValue[2]), new GuiAffine(0, PaddingValue[3]));
                int Count = Visible.Count, Maximum = Integer(Layout, GuiPropertyId.FillDirectionMaxCells);
                bool Horizontal = TextValue(Layout, GuiPropertyId.FillDirection) == "Horizontal";
                int Columns = Count == 0 ? 0 : Horizontal ? Math.Min(Count, Maximum) : (Count + Maximum - 1) / Maximum;
                int Rows = Count == 0 ? 0 : Horizontal ? (Count + Maximum - 1) / Maximum : Math.Min(Count, Maximum);
                GuiAffine GridWidth = Columns == 0 ? Affine(0, 0) : GuiAffine.Add(GuiAffine.Multiply(XCell, Columns), GuiAffine.Multiply(XGap, Columns - 1));
                GuiAffine GridHeight = Rows == 0 ? Affine(0, 0) : GuiAffine.Add(GuiAffine.Multiply(YCell, Rows), GuiAffine.Multiply(YGap, Rows - 1));
                GuiAffine GridX = GuiAffine.Add(Content.XStart, GuiAffine.Multiply(GuiAffine.Subtract(Content.XSize, GridWidth),
                    AlignmentFactor(TextValue(Layout, GuiPropertyId.HorizontalAlignment))));
                GuiAffine GridY = GuiAffine.Add(Content.YStart, GuiAffine.Multiply(GuiAffine.Subtract(Content.YSize, GridHeight),
                    AlignmentFactor(TextValue(Layout, GuiPropertyId.VerticalAlignment))));
                for (int Index = 0; Index < Count; ++Index) {
                    int Column = Horizontal ? Index % Maximum : Index / Maximum;
                    int Row = Horizontal ? Index / Maximum : Index % Maximum;
                    GuiAffine XStart = GuiAffine.Add(GridX, GuiAffine.Multiply(GuiAffine.Add(XCell, XGap), Column));
                    GuiAffine YStart = GuiAffine.Add(GridY, GuiAffine.Multiply(GuiAffine.Add(YCell, YGap), Row));
                    GuiRetainedNode Child = Visible[Index]; double[] Anchor = Numbers(Child, GuiPropertyId.AnchorPoint);
                    GuiAffine XPosition = GuiAffine.Add(XStart, GuiAffine.Multiply(XCell, Anchor[0]));
                    GuiAffine YPosition = GuiAffine.Add(YStart, GuiAffine.Multiply(YCell, Anchor[1]));
                    Result.Add(Child.Identity.GuiObjectId, new GuiProjectedRect(XPosition, YPosition, XCell, YCell));
                }
                foreach (GuiRetainedNode Child in Children)
                    if (!Result.ContainsKey(Child.Identity.GuiObjectId)) Result.Add(Child.Identity.GuiObjectId, ProjectRetained(Content, Child));
                return Result;
            }

            private static GuiProjectedRect ProjectRetained(GuiContentRect Content, GuiRetainedNode Child)
            {
                double[] Position = Numbers(Child, GuiPropertyId.Position), Size = Numbers(Child, GuiPropertyId.Size);
                GuiAffine XSize = GuiAffine.Add(GuiAffine.Multiply(Content.XSize, Size[0]), new GuiAffine(0, Size[1]));
                GuiAffine YSize = GuiAffine.Add(GuiAffine.Multiply(Content.YSize, Size[2]), new GuiAffine(0, Size[3]));
                GuiAffine XPosition = GuiAffine.Add(Content.XStart, GuiAffine.Add(GuiAffine.Multiply(Content.XSize, Position[0]), new GuiAffine(0, Position[1])));
                GuiAffine YPosition = GuiAffine.Add(Content.YStart, GuiAffine.Add(GuiAffine.Multiply(Content.YSize, Position[2]), new GuiAffine(0, Position[3])));
                return new GuiProjectedRect(XPosition, YPosition, XSize, YSize);
            }

            private static GuiProjectedRect RetainedRect(GuiRetainedNode Node)
            {
                double[] Position = Numbers(Node, GuiPropertyId.Position), Size = Numbers(Node, GuiPropertyId.Size);
                return new GuiProjectedRect(Affine(Position[0], Position[1]), Affine(Position[2], Position[3]),
                    Affine(Size[0], Size[1]), Affine(Size[2], Size[3]));
            }

            private static GuiContentRect ContentRect(GuiRetainedState State, GuiRetainedNode Parent)
            {
                GuiRetainedNode Padding = null;
                foreach (ulong ChildId in Parent.Children) if (State.Nodes[ChildId].ClassId == GuiClassId.UIPadding) Padding = State.Nodes[ChildId];
                if (Padding == null) return new GuiContentRect(Affine(0, 0), Affine(0, 0), Affine(1, 0), Affine(1, 0));
                GuiAffine Left = StoredAffine(Padding, GuiPropertyId.PaddingLeft), Right = StoredAffine(Padding, GuiPropertyId.PaddingRight);
                GuiAffine Top = StoredAffine(Padding, GuiPropertyId.PaddingTop), Bottom = StoredAffine(Padding, GuiPropertyId.PaddingBottom);
                return new GuiContentRect(Left, Top, GuiAffine.Subtract(GuiAffine.Subtract(Affine(1, 0), Left), Right),
                    GuiAffine.Subtract(GuiAffine.Subtract(Affine(1, 0), Top), Bottom));
            }

            private static GuiAffine StoredAffine(GuiRetainedNode Node, GuiPropertyId Id)
            { double[] Value = Numbers(Node, Id); return Affine(Value[0], Value[1]); }
            private static GuiAffine Affine(double Scale, double Offset) { return new GuiAffine(Scale, Offset); }
            private static double AlignmentFactor(string Value)
            { return Value == "Center" ? 0.5 : Value == "Right" || Value == "Bottom" ? 1 : 0; }

            internal static bool NeedsCursorFor(GuiRetainedState State, GuiRetainedNode Screen)
            { return HasVisibleInteractiveControl(State, Screen, true); }
            private static bool HasVisibleInteractiveControl(GuiRetainedState State, GuiRetainedNode Node, bool AncestorsVisible)
            {
                bool Visible = AncestorsVisible && (!GuiSchema.IsA(Node.ClassId, "GuiObject") || Boolean(Node, GuiPropertyId.Visible));
                if (Visible && (Node.ClassId == GuiClassId.TextButton || Node.ClassId == GuiClassId.ImageButton)) return true;
                if (Visible && Node.ClassId == GuiClassId.ScrollingFrame && Boolean(Node, GuiPropertyId.ScrollingEnabled)) return true;
                foreach (ulong Child in Node.Children) if (HasVisibleInteractiveControl(State, State.Nodes[Child], Visible)) return true;
                return false;
            }
            private static GuiStoredValue Property(GuiRetainedNode Node, GuiPropertyId Id)
            { GuiStoredValue Value; if (!Node.Properties.TryGetValue(Id, out Value)) throw new InvalidOperationException("GUI render property is missing"); return Value; }
            private static bool Boolean(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Boolean; }
            private static int Integer(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Integer; }
            private static double Number(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Numbers[0]; }
            private static double[] Numbers(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Numbers; }
            private static string TextValue(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Text; }
            private static bool HasAny(HashSet<GuiPropertyId> Values, params GuiPropertyId[] Candidates)
            { foreach (GuiPropertyId Candidate in Candidates) if (Values.Contains(Candidate)) return true; return false; }
            private static GuiRenderProperty Boolean(GuiRenderPropertyId Id, bool Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromBoolean(Value)); }
            private static GuiRenderProperty Integer(GuiRenderPropertyId Id, int Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromInteger(Value)); }
            private static GuiRenderProperty String(GuiRenderPropertyId Id, string Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromString(Value)); }
            private static GuiRenderProperty Vector(GuiRenderPropertyId Id, double X, double Y) { return new GuiRenderProperty(Id, GuiRenderValue.FromVector(X, Y)); }
            private static GuiRenderProperty Color(GuiRenderPropertyId Id, double[] Value, double Alpha) { return new GuiRenderProperty(Id, GuiRenderValue.FromColor(Value[0], Value[1], Value[2], Alpha)); }
            private static GuiRenderProperty Image(GuiRenderPropertyId Id, GuiImageSourceValue Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromImageSource(Value)); }
        }

        internal sealed class GuiFullRebuildRequiredException : Exception
        { internal GuiFullRebuildRequiredException(string Message) : base(Message) { } }
    }
}
