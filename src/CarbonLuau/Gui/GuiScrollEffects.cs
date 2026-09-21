using System;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal enum GuiScrollIntentKind { Position = 1, Top = 2, Bottom = 3 }

        internal sealed class GuiScrollIntent
        {
            internal readonly ulong ScreenId, ScrollFrameId;
            internal readonly string PlayerToken, PlayerUserId;
            internal readonly object PlayerIdentity, PlayerConnection;
            internal readonly GuiScrollIntentKind Kind;
            internal readonly double X, Y;

            internal GuiScrollIntent(ulong ScreenId, ulong ScrollFrameId, PlayerLifetime Player,
                GuiScrollIntentKind Kind, double X, double Y)
            {
                if (ScreenId == 0 || ScrollFrameId == 0 || Player == null)
                    throw new InvalidOperationException("scroll intent identity is invalid");
                ValidateCoordinate(X); ValidateCoordinate(Y);
                this.ScreenId = ScreenId; this.ScrollFrameId = ScrollFrameId;
                PlayerToken = Player.Token; PlayerUserId = Player.UserId;
                PlayerIdentity = Player.Identity; PlayerConnection = Player.Connection;
                this.Kind = Kind; this.X = Normalize(X); this.Y = Normalize(Y);
            }

            internal string Key
            {
                get {
                    return ScreenId.ToString(CultureInfo.InvariantCulture) + ":" + PlayerToken.Length.ToString(CultureInfo.InvariantCulture) +
                        ":" + PlayerToken + ":" + ScrollFrameId.ToString(CultureInfo.InvariantCulture);
                }
            }

            internal static void ValidateCoordinate(double Value)
            {
                if (Double.IsNaN(Value) || Double.IsInfinity(Value) || Value < 0 || Value > 1)
                    throw new FacadeException("scroll coordinates must be finite and within 0..1");
            }

            private static double Normalize(double Value) { return Value == 0 ? 0 : Value; }
        }

        internal sealed class GuiScrollEffect
        {
            internal readonly string ClientId;
            internal readonly ulong PresentationEpoch, ScrollFrameId;
            internal readonly double? Horizontal, Vertical;

            internal GuiScrollEffect(string ClientId, ulong PresentationEpoch, ulong ScrollFrameId,
                double? Horizontal, double? Vertical)
            {
                if (String.IsNullOrEmpty(ClientId) || ClientId.Length > 256 || ClientId.IndexOf('\0') >= 0 ||
                    PresentationEpoch == 0 || ScrollFrameId == 0 || (!Horizontal.HasValue && !Vertical.HasValue))
                    throw new InvalidOperationException("scroll effect is invalid");
                if (Horizontal.HasValue) GuiScrollIntent.ValidateCoordinate(Horizontal.Value);
                if (Vertical.HasValue) GuiScrollIntent.ValidateCoordinate(Vertical.Value);
                this.ClientId = ClientId; this.PresentationEpoch = PresentationEpoch; this.ScrollFrameId = ScrollFrameId;
                this.Horizontal = Horizontal; this.Vertical = Vertical;
            }

            internal string Describe()
            {
                return ClientId.Length.ToString(CultureInfo.InvariantCulture) + ":" + ClientId + "|" +
                    PresentationEpoch.ToString(CultureInfo.InvariantCulture) + "|" + ScrollFrameId.ToString(CultureInfo.InvariantCulture) + "|" +
                    (Horizontal.HasValue ? Horizontal.Value.ToString("R", CultureInfo.InvariantCulture) : "-") + "|" +
                    (Vertical.HasValue ? Vertical.Value.ToString("R", CultureInfo.InvariantCulture) : "-");
            }
        }

        internal sealed class GuiScrollDiagnostics
        {
            internal ulong Accepted, Coalesced, BoundRejected, PresentationDiscarded, PlayerDiscarded, BackendFailures, Sent;

            internal void Accept(bool Replaced) { Increment(ref Accepted); if (Replaced) Increment(ref Coalesced); }
            internal void RejectBound() { Increment(ref BoundRejected); }
            internal void DiscardPresentation(int Count = 1) { Add(ref PresentationDiscarded, Count); }
            internal void DiscardPlayer(int Count = 1) { Add(ref PlayerDiscarded, Count); }
            internal void BackendFailed() { Increment(ref BackendFailures); }
            internal void SendAccepted() { Increment(ref Sent); }
            internal string Status()
            {
                return "GUI scroll accepted/coalesced/bound/presentation/player/backend/sent: " + Accepted + "/" + Coalesced + "/" +
                    BoundRejected + "/" + PresentationDiscarded + "/" + PlayerDiscarded + "/" + BackendFailures + "/" + Sent;
            }
            private static void Increment(ref ulong Value) { if (Value != ulong.MaxValue) Value++; }
            private static void Add(ref ulong Value, int Count)
            {
                if (Count <= 0 || Value == ulong.MaxValue) return;
                ulong Amount = checked((ulong)Count); Value = ulong.MaxValue - Value < Amount ? ulong.MaxValue : Value + Amount;
            }
        }
    }
}
