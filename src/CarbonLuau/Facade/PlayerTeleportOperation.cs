using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public struct PlayerTeleportState
        {
            public bool Current, Alive, Spectating, Wounded, Incapacitated, Sleeping, Mounted, Parented;
        }

        // Owns the D18 eligibility and failure boundary independently of the
        // Rust adapter. Mutation may be partially visible after Mutate starts;
        // CarbonLuau deliberately promises no rollback of host world state.
        public sealed class PlayerTeleportOperation
        {
            private readonly Func<PlayerTeleportState> Inspect;
            private readonly Action<PlayerPosition, PlayerTeleportState> Mutate;
            private readonly Func<PlayerPosition, PlayerTeleportState, bool> Verify;

            public PlayerTeleportOperation(Func<PlayerTeleportState> Inspect,
                Action<PlayerPosition, PlayerTeleportState> Mutate,
                Func<PlayerPosition, PlayerTeleportState, bool> Verify)
            {
                this.Inspect = Inspect ?? throw new ArgumentNullException("Inspect");
                this.Mutate = Mutate ?? throw new ArgumentNullException("Mutate");
                this.Verify = Verify ?? throw new ArgumentNullException("Verify");
            }

            public void Execute(PlayerPosition Destination)
            {
                PlayerTeleportState Before;
                try { Before = Inspect(); }
                catch { throw new FacadeException("Teleport host inspection failed before mutation"); }
                if (!Before.Current) throw new FacadeException("Player is no longer connected");
                if (!Before.Alive) throw new FacadeException("Teleport requires an alive Player");
                if (Before.Spectating) throw new FacadeException("Teleport is unavailable while spectating");
                if (Before.Wounded || Before.Incapacitated)
                    throw new FacadeException("Teleport is unavailable while wounded or incapacitated");

                try {
                    Mutate(Destination, Before);
                    if (!Verify(Destination, Before))
                        throw new InvalidOperationException("verification rejected");
                } catch {
                    throw new FacadeException("Teleport failed after host mutation began; Player state may have changed");
                }
            }
        }
    }
}
