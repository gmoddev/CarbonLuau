using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiPresentation
        {
            internal readonly ulong ScreenId, Epoch;
            internal readonly string PlayerToken, PlayerUserId, ClientPrefix;
            internal bool WasSent, NeedsReplace, SynchronizationUncertain;
            internal GuiPresentation(ulong ScreenId, ulong Epoch, string PlayerToken, string PlayerUserId, string ClientPrefix)
            {
                this.ScreenId = ScreenId; this.Epoch = Epoch; this.PlayerToken = PlayerToken;
                this.PlayerUserId = PlayerUserId; this.ClientPrefix = ClientPrefix; NeedsReplace = true;
            }
            internal string RootClientId { get { return ClientPrefix + "r"; } }
            internal GuiBackendTarget Target { get { return new GuiBackendTarget(PlayerToken, PlayerUserId, RootClientId); } }
            internal string ObjectClientId(ulong ObjectId) { return ClientPrefix + "o" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string TextClientId(ulong ObjectId) { return ClientPrefix + "t" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal GuiPresentation Copy()
            {
                return new GuiPresentation(ScreenId, Epoch, PlayerToken, PlayerUserId, ClientPrefix) {
                    WasSent = WasSent, NeedsReplace = NeedsReplace, SynchronizationUncertain = SynchronizationUncertain
                };
            }
        }

        internal static class GuiRenderCompiler
        {
            internal static GuiRenderPlan Compile(GuiRetainedState State, GuiRetainedNode Screen, GuiPresentation Presentation, GuiLimits Limits)
            {
                if (State == null || Screen == null || Presentation == null || Limits == null || Screen.ClassId != GuiClassId.ScreenGui)
                    throw new InvalidOperationException("GUI render compiler requires a live ScreenGui presentation");
                var Elements = new List<GuiRenderElement>();
                bool NeedsCursor = HasVisibleButton(State, Screen, true);
                Elements.Add(new GuiRenderElement(Presentation.RootClientId, null, GuiRenderNodeKind.Container, Limits,
                    Vector(GuiRenderPropertyId.AnchorMin, 0, 0), Vector(GuiRenderPropertyId.AnchorMax, 1, 1),
                    Vector(GuiRenderPropertyId.OffsetMin, 0, 0), Vector(GuiRenderPropertyId.OffsetMax, 0, 0),
                    Vector(GuiRenderPropertyId.Pivot, 0.5, 0.5), Boolean(GuiRenderPropertyId.Visible, true),
                    Boolean(GuiRenderPropertyId.NeedsCursor, NeedsCursor)));
                AppendChildren(State, Screen, Presentation.RootClientId, Presentation, Limits, Elements);
                int Estimated = 16;
                foreach (GuiRenderElement Element in Elements) Estimated = checked(Estimated + Element.CanonicalUtf8Bytes + 64);
                if (Estimated > Limits.MaxSerializedOperationBytes) throw new FacadeException("GUI full presentation exceeds the serialized operation bound");
                return new GuiRenderPlan(Limits, Estimated, Elements.ToArray());
            }

            private static void AppendChildren(GuiRetainedState State, GuiRetainedNode Parent, string ParentClientId,
                GuiPresentation Presentation, GuiLimits Limits, List<GuiRenderElement> Elements)
            {
                var Ordered = new List<GuiRetainedNode>();
                foreach (ulong ChildId in Parent.Children) Ordered.Add(State.Nodes[ChildId]);
                Ordered.Sort((Left, Right) => {
                    int Result = Integer(Left, GuiPropertyId.ZIndex).CompareTo(Integer(Right, GuiPropertyId.ZIndex));
                    if (Result != 0) return Result;
                    Result = Parent.Children.IndexOf(Left.Identity.GuiObjectId).CompareTo(Parent.Children.IndexOf(Right.Identity.GuiObjectId));
                    return Result != 0 ? Result : Left.Identity.GuiObjectId.CompareTo(Right.Identity.GuiObjectId);
                });
                foreach (GuiRetainedNode Node in Ordered) {
                    string ClientId = Presentation.ObjectClientId(Node.Identity.GuiObjectId);
                    GuiRenderNodeKind Kind = Node.ClassId == GuiClassId.TextButton ? GuiRenderNodeKind.Button : GuiRenderNodeKind.Container;
                    var Properties = new List<GuiRenderProperty>();
                    AddLayout(Node, Properties);
                    Properties.Add(Boolean(GuiRenderPropertyId.Visible, Boolean(Node, GuiPropertyId.Visible)));
                    double[] Background = Numbers(Node, GuiPropertyId.BackgroundColor3);
                    Properties.Add(Color(GuiRenderPropertyId.BackgroundColor, Background,
                        1 - Number(Node, GuiPropertyId.BackgroundTransparency)));
                    Elements.Add(new GuiRenderElement(ClientId, ParentClientId, Kind, Limits, Properties.ToArray()));
                    if (Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.TextButton) {
                        double[] TextColor = Numbers(Node, GuiPropertyId.TextColor3);
                        Elements.Add(new GuiRenderElement(Presentation.TextClientId(Node.Identity.GuiObjectId), ClientId,
                            GuiRenderNodeKind.Text, Limits,
                            Vector(GuiRenderPropertyId.AnchorMin, 0, 0), Vector(GuiRenderPropertyId.AnchorMax, 1, 1),
                            Vector(GuiRenderPropertyId.OffsetMin, 0, 0), Vector(GuiRenderPropertyId.OffsetMax, 0, 0),
                            Vector(GuiRenderPropertyId.Pivot, 0.5, 0.5), Boolean(GuiRenderPropertyId.Visible, true),
                            String(GuiRenderPropertyId.Text, Text(Node, GuiPropertyId.Text)),
                            Color(GuiRenderPropertyId.TextColor, TextColor, 1 - Number(Node, GuiPropertyId.TextTransparency)),
                            Integer(GuiRenderPropertyId.FontSize, Integer(Node, GuiPropertyId.TextSize)),
                            String(GuiRenderPropertyId.TextXAlignment, Text(Node, GuiPropertyId.TextXAlignment)),
                            String(GuiRenderPropertyId.TextYAlignment, Text(Node, GuiPropertyId.TextYAlignment))));
                    }
                    AppendChildren(State, Node, ClientId, Presentation, Limits, Elements);
                }
            }

            private static void AddLayout(GuiRetainedNode Node, List<GuiRenderProperty> Result)
            {
                double[] Position = Numbers(Node, GuiPropertyId.Position), Size = Numbers(Node, GuiPropertyId.Size), Anchor = Numbers(Node, GuiPropertyId.AnchorPoint);
                double Ax = Anchor[0], Ay = Anchor[1];
                Result.Add(Vector(GuiRenderPropertyId.AnchorMin, Position[0] - Ax * Size[0], 1 - Position[2] - (1 - Ay) * Size[2]));
                Result.Add(Vector(GuiRenderPropertyId.AnchorMax, Position[0] + (1 - Ax) * Size[0], 1 - Position[2] + Ay * Size[2]));
                Result.Add(Vector(GuiRenderPropertyId.OffsetMin, Position[1] - Ax * Size[1], -Position[3] - (1 - Ay) * Size[3]));
                Result.Add(Vector(GuiRenderPropertyId.OffsetMax, Position[1] + (1 - Ax) * Size[1], -Position[3] + Ay * Size[3]));
                Result.Add(Vector(GuiRenderPropertyId.Pivot, Ax, 1 - Ay));
            }

            private static bool HasVisibleButton(GuiRetainedState State, GuiRetainedNode Node, bool AncestorsVisible)
            {
                bool Visible = AncestorsVisible && (Node.ClassId == GuiClassId.ScreenGui || Boolean(Node, GuiPropertyId.Visible));
                if (Visible && Node.ClassId == GuiClassId.TextButton) return true;
                foreach (ulong Child in Node.Children) if (HasVisibleButton(State, State.Nodes[Child], Visible)) return true;
                return false;
            }
            private static GuiStoredValue Property(GuiRetainedNode Node, GuiPropertyId Id)
            { GuiStoredValue Value; if (!Node.Properties.TryGetValue(Id, out Value)) throw new InvalidOperationException("GUI render property is missing"); return Value; }
            private static bool Boolean(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Boolean; }
            private static int Integer(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Integer; }
            private static double Number(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Numbers[0]; }
            private static double[] Numbers(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Numbers; }
            private static string Text(GuiRetainedNode Node, GuiPropertyId Id) { return Property(Node, Id).Text; }
            private static GuiRenderProperty Boolean(GuiRenderPropertyId Id, bool Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromBoolean(Value)); }
            private static GuiRenderProperty Integer(GuiRenderPropertyId Id, int Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromInteger(Value)); }
            private static GuiRenderProperty String(GuiRenderPropertyId Id, string Value) { return new GuiRenderProperty(Id, GuiRenderValue.FromString(Value)); }
            private static GuiRenderProperty Vector(GuiRenderPropertyId Id, double X, double Y) { return new GuiRenderProperty(Id, GuiRenderValue.FromVector(X, Y)); }
            private static GuiRenderProperty Color(GuiRenderPropertyId Id, double[] Value, double Alpha) { return new GuiRenderProperty(Id, GuiRenderValue.FromColor(Value[0], Value[1], Value[2], Alpha)); }
        }
    }
}
