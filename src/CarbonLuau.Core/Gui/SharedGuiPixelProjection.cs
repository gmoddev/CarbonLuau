using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiPixelRect
        {
            internal readonly double X, Y, Width, Height;
            internal GuiPixelRect(double X, double Y, double Width, double Height)
            {
                foreach (double Value in new[] { X, Y, Width, Height })
                    if (Double.IsNaN(Value) || Double.IsInfinity(Value)) throw new InvalidOperationException("nonfinite projected geometry");
                this.X = X == 0 ? 0 : X; this.Y = Y == 0 ? 0 : Y;
                this.Width = Width == 0 ? 0 : Width; this.Height = Height == 0 ? 0 : Height;
            }
            internal static GuiPixelRect Intersect(GuiPixelRect Left, GuiPixelRect Right)
            {
                double X = Math.Max(Left.X, Right.X), Y = Math.Max(Left.Y, Right.Y);
                return new GuiPixelRect(X, Y, Math.Max(0, Math.Min(Left.X + Math.Max(0, Left.Width), Right.X + Math.Max(0, Right.Width)) - X),
                    Math.Max(0, Math.Min(Left.Y + Math.Max(0, Left.Height), Right.Y + Math.Max(0, Right.Height)) - Y));
            }
        }

        internal sealed class GuiPixelElement
        {
            internal readonly GuiPixelRect Rect, Clip, ChildClip;
            internal readonly bool Visible;
            internal readonly int PaintOrder, ClipDepth;
            internal readonly string[] ClipOwners;
            internal GuiPixelElement(GuiPixelRect Rect, GuiPixelRect Clip, GuiPixelRect ChildClip, bool Visible,
                int PaintOrder, int ClipDepth, string[] ClipOwners)
            {
                this.Rect = Rect; this.Clip = Clip; this.ChildClip = ChildClip; this.Visible = Visible;
                this.PaintOrder = PaintOrder; this.ClipDepth = ClipDepth; this.ClipOwners = ClipOwners;
            }
        }

        // Resolves the canonical compiler's affine output at a supplied viewport.
        // List/grid/padding/anchor decisions have already been made by GuiRenderCompiler.
        internal static class GuiPixelProjection
        {
            internal static Dictionary<string, GuiPixelElement> Resolve(GuiRenderPlan Plan, double Width, double Height)
            {
                if (Width < 1 || Height < 1 || Width > 8192 || Height > 8192 ||
                    Double.IsNaN(Width) || Double.IsNaN(Height)) throw new InvalidOperationException("preview viewport must be finite and within 1..8192 pixels");
                var Viewport = new GuiPixelRect(0, 0, Width, Height);
                var Result = new Dictionary<string, GuiPixelElement>(StringComparer.Ordinal);
                int Order = 0;
                foreach (GuiRenderElement Element in Plan.Elements) {
                    GuiPixelElement Parent = Element.ParentClientId == null
                        ? new GuiPixelElement(Viewport, Viewport, Viewport, true, -1, 0, new string[0]) : Result[Element.ParentClientId];
                    GuiPixelRect Rect = ResolveRect(Element, Parent.Rect, false);
                    bool Visible = Parent.Visible && Boolean(Element, GuiRenderPropertyId.Visible, true);
                    bool CreatesClip = Element.Kind == GuiRenderNodeKind.Clip || Element.Kind == GuiRenderNodeKind.ScrollView;
                    GuiPixelRect Clip = Parent.ChildClip;
                    GuiPixelRect ChildClip = CreatesClip ? GuiPixelRect.Intersect(Clip, Rect) : Clip;
                    var Owners = new List<string>(Parent.ClipOwners);
                    if (CreatesClip) Owners.Add(Element.ClientId);
                    int Depth = Parent.ClipDepth + (CreatesClip ? 1 : 0);
                    Result.Add(Element.ClientId, new GuiPixelElement(Rect, Clip, ChildClip, Visible, Order++, Depth, Owners.ToArray()));
                    if (Element.Kind == GuiRenderNodeKind.ScrollView) {
                        // No client-local scroll offset is inferred. The initial content origin is top-left.
                        GuiPixelRect Content = ResolveRect(Element, Rect, true);
                        Result.Add(Element.PrivateChildRootId, new GuiPixelElement(Content, ChildClip, ChildClip, Visible,
                            Order - 1, Depth, Owners.ToArray()));
                    }
                }
                return Result;
            }
            private static GuiPixelRect ResolveRect(GuiRenderElement Element, GuiPixelRect Parent, bool Content)
            {
                GuiRenderVector2 Min = Vector(Element, Content ? GuiRenderPropertyId.ScrollContentAnchorMin : GuiRenderPropertyId.AnchorMin);
                GuiRenderVector2 Max = Vector(Element, Content ? GuiRenderPropertyId.ScrollContentAnchorMax : GuiRenderPropertyId.AnchorMax);
                GuiRenderVector2 OffsetMin = Vector(Element, Content ? GuiRenderPropertyId.ScrollContentOffsetMin : GuiRenderPropertyId.OffsetMin);
                GuiRenderVector2 OffsetMax = Vector(Element, Content ? GuiRenderPropertyId.ScrollContentOffsetMax : GuiRenderPropertyId.OffsetMax);
                double Left = Parent.Width * Min.X + OffsetMin.X, Right = Parent.Width * Max.X + OffsetMax.X;
                double Bottom = Parent.Height * Min.Y + OffsetMin.Y, Top = Parent.Height * Max.Y + OffsetMax.Y;
                return new GuiPixelRect(Parent.X + Left, Parent.Y + Parent.Height - Top, Right - Left, Top - Bottom);
            }
            private static GuiRenderVector2 Vector(GuiRenderElement Element, GuiRenderPropertyId Id)
            {
                foreach (GuiRenderProperty Property in Element.Properties) if (Property.Id == Id) return Property.Value.Vector;
                throw new InvalidOperationException("canonical projection lacks required geometry");
            }
            private static bool Boolean(GuiRenderElement Element, GuiRenderPropertyId Id, bool Default)
            {
                foreach (GuiRenderProperty Property in Element.Properties) if (Property.Id == Id) return Property.Value.Boolean;
                return Default;
            }
        }
    }
}
