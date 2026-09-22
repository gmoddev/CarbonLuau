using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiPresentation : GuiProjectionNames
        {
            internal readonly ulong ScreenId;
            internal ulong Epoch;
            internal readonly string PlayerToken, PlayerUserId;
            internal bool WasSent, NeedsFullResync, SynchronizationUncertain, LastNeedsCursor;
            internal bool ActionInvalidationPending;
            internal ulong SentRevision, ProjectionBlockedRevision, LastAttemptCycle;
            internal ulong ScrollEffectFlushCursor;
            internal int SuccessfulPatchBatches;
            internal readonly Dictionary<ulong, string> ActionTokens = new Dictionary<ulong, string>();
            internal readonly SortedDictionary<ulong, GuiScrollIntent> PendingScrollEffects = new SortedDictionary<ulong, GuiScrollIntent>();
            internal GuiPresentation(ulong ScreenId, ulong Epoch, string PlayerToken, string PlayerUserId, string ClientPrefix) : base(ClientPrefix)
            {
                this.ScreenId = ScreenId; this.Epoch = Epoch; this.PlayerToken = PlayerToken;
                this.PlayerUserId = PlayerUserId; NeedsFullResync = true;
            }
            internal GuiBackendTarget Target { get { return new GuiBackendTarget(PlayerToken, PlayerUserId, RootClientId); } }
            internal GuiPresentation Copy()
            {
                var Result = new GuiPresentation(ScreenId, Epoch, PlayerToken, PlayerUserId, ClientPrefix) {
                    WasSent = WasSent, NeedsFullResync = NeedsFullResync, SynchronizationUncertain = SynchronizationUncertain,
                    LastNeedsCursor = LastNeedsCursor, SentRevision = SentRevision, ProjectionBlockedRevision = ProjectionBlockedRevision,
                    LastAttemptCycle = LastAttemptCycle, ScrollEffectFlushCursor = ScrollEffectFlushCursor,
                    SuccessfulPatchBatches = SuccessfulPatchBatches,
                    ActionInvalidationPending = ActionInvalidationPending
                };
                foreach (var Value in ActionTokens) Result.ActionTokens.Add(Value.Key, Value.Value);
                foreach (var Value in PendingScrollEffects) Result.PendingScrollEffects.Add(Value.Key, Value.Value);
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

        internal static partial class GuiRenderCompiler
        {
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
                    bool Font = Dirty.Value.Contains(GuiPropertyId.Font);
                    bool TextAlignment = HasAny(Dirty.Value, GuiPropertyId.TextXAlignment, GuiPropertyId.TextYAlignment);
                    var TextProperties = new List<GuiRenderProperty>();
                    if (Dirty.Value.Contains(GuiPropertyId.ContentProjection)) AddContentLayout(State, Node, TextProperties);
                    if (Text) TextProperties.Add(String(GuiRenderPropertyId.Text, TextValue(Node, GuiPropertyId.Text)));
                    if (TextColor) {
                        double[] ColorValue = Numbers(Node, GuiPropertyId.TextColor3);
                        TextProperties.Add(Color(GuiRenderPropertyId.TextColor, ColorValue, 1 - Number(Node, GuiPropertyId.TextTransparency)));
                    }
                    if (TextSize) TextProperties.Add(Integer(GuiRenderPropertyId.FontSize, Integer(Node, GuiPropertyId.TextSize)));
                    if (Font) TextProperties.Add(FontValue(GuiRenderPropertyId.Font, Property(Node, GuiPropertyId.Font).FontIdentity));
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
        }

        internal sealed class GuiFullRebuildRequiredException : Exception
        { internal GuiFullRebuildRequiredException(string Message) : base(Message) { } }
    }
}
