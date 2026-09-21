using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal enum GuiActionRejection
        {
            None = 0,
            Malformed = 1,
            Unknown = 2,
            Stale = 3,
            CrossPlayer = 4,
            TargetUnavailable = 5,
            RateLimited = 6,
            QueueFull = 7,
            PreEntryStale = 8
        }

        internal sealed class GuiActionDiagnostics
        {
            internal ulong Accepted, Malformed, Unknown, Stale, CrossPlayer, TargetUnavailable, RateLimited, QueueFull, PreEntryStale;

            internal void Reject(GuiActionRejection Reason)
            {
                switch (Reason) {
                    case GuiActionRejection.Malformed: Increment(ref Malformed); break;
                    case GuiActionRejection.Unknown: Increment(ref Unknown); break;
                    case GuiActionRejection.Stale: Increment(ref Stale); break;
                    case GuiActionRejection.CrossPlayer: Increment(ref CrossPlayer); break;
                    case GuiActionRejection.TargetUnavailable: Increment(ref TargetUnavailable); break;
                    case GuiActionRejection.RateLimited: Increment(ref RateLimited); break;
                    case GuiActionRejection.QueueFull: Increment(ref QueueFull); break;
                    case GuiActionRejection.PreEntryStale: Increment(ref PreEntryStale); break;
                }
            }

            internal void Accept() { Increment(ref Accepted); }
            private static void Increment(ref ulong Value) { if (Value != ulong.MaxValue) Value++; }
            internal string Status()
            {
                return "GUI actions accepted/malformed/unknown/stale/cross-player/target/rate/queue/pre-entry: " +
                    Accepted + "/" + Malformed + "/" + Unknown + "/" + Stale + "/" + CrossPlayer + "/" +
                    TargetUnavailable + "/" + RateLimited + "/" + QueueFull + "/" + PreEntryStale;
            }
        }

        internal sealed class GuiActionRate
        {
            private readonly int Rate, Burst;
            private double Tokens;
            private long Last;

            internal GuiActionRate(int Rate, int Burst, long Now)
            { this.Rate = Rate; this.Burst = Burst; Tokens = Burst; Last = Now; }

            internal bool Allow(long Now)
            {
                if (Now > Last) Tokens = Math.Min(Burst, Tokens + (Now - Last) * Rate / (double)Stopwatch.Frequency);
                Last = Now;
                if (Tokens < 1) return false;
                Tokens -= 1; return true;
            }
        }

        internal sealed class GuiActionRecord
        {
            internal readonly string Token, PlayerToken, PlayerUserId;
            internal readonly ulong VmGenerationId, DomainLifetimeId, ScreenId, PresentationEpoch, ButtonId;
            internal readonly object PlayerIdentity, PlayerConnection;
            internal readonly GuiRetainedRegistry Registry;
            internal readonly GuiActionRate Rate;

            internal GuiActionRecord(string Token, GuiRetainedRegistry Registry, GuiPresentation Presentation,
                GuiRetainedNode Button, PlayerLifetime Player, GuiLimits Limits, long Now)
            {
                this.Token = Token; this.Registry = Registry;
                VmGenerationId = Button.Identity.VmGenerationId; DomainLifetimeId = Button.Identity.DomainLifetimeId;
                ScreenId = Presentation.ScreenId; PresentationEpoch = Presentation.Epoch; ButtonId = Button.Identity.GuiObjectId;
                PlayerToken = Player.Token; PlayerUserId = Player.UserId; PlayerIdentity = Player.Identity; PlayerConnection = Player.Connection;
                Rate = new GuiActionRate(Limits.MaxActionInteractionsPerSecond, Limits.MaxActionInteractionBurst, Now);
            }
        }

        internal sealed class GuiActionAdmission
        {
            internal readonly GuiActionRecord Record;
            internal readonly string[] Registrations;
            internal GuiActionAdmission(GuiActionRecord Record, string[] Registrations)
            { this.Record = Record; this.Registrations = Registrations; }
        }

        internal sealed class GuiPlayerActionRate
        {
            internal readonly GuiActionRate Rate;
            internal int References;
            internal GuiPlayerActionRate(GuiLimits Limits, long Now)
            { Rate = new GuiActionRate(Limits.MaxPlayerInteractionsPerSecond, Limits.MaxPlayerInteractionBurst, Now); }
        }

        internal sealed class GuiRetiredAction
        {
            internal readonly GuiActionRejection Reason;
            internal GuiRetiredAction(GuiActionRejection Reason) { this.Reason = Reason; }
        }
    }
}
