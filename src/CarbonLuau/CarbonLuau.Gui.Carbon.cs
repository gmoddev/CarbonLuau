using System;
using Oxide.Game.Rust.Cui;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private sealed class CarbonRustCuiTransport : IRustCuiTransport
        {
            private readonly PlayerDirectory Players;
            internal CarbonRustCuiTransport(PlayerDirectory Players) { this.Players = Players ?? throw new ArgumentNullException("Players"); }
            public GuiBackendResult Replace(GuiBackendTarget Target, string Payload)
            {
                BasePlayer Player = Resolve(Target);
                if (Player == null) return GuiBackendResult.Failure(GuiBackendResultCode.TargetUnavailable, "exact Player connection is unavailable");
                return CuiHelper.AddUi(Player, Payload) ? GuiBackendResult.Success() :
                    GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI AddUI was rejected locally");
            }
            public GuiBackendResult Update(GuiBackendTarget Target, string Payload) { return Replace(Target, Payload); }
            public GuiBackendResult Destroy(GuiBackendTarget Target)
            {
                BasePlayer Player = Resolve(Target);
                if (Player == null) return GuiBackendResult.Failure(GuiBackendResultCode.TargetUnavailable, "exact Player connection is unavailable");
                return CuiHelper.DestroyUi(Player, Target.ClientRootId) ? GuiBackendResult.Success() :
                    GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "Rust CUI DestroyUI was rejected locally");
            }
            private BasePlayer Resolve(GuiBackendTarget Target)
            {
                if (Target == null) return null;
                PlayerView View = Players.Resolve(Target.ExactPlayerConnectionToken, Target.ExactPlayerUserId);
                BasePlayer Player = View == null ? null : View.Identity as BasePlayer;
                if (Player == null || !Player.IsConnected || Player.Connection == null ||
                    !Object.ReferenceEquals(Player.Connection, View.Connection)) return null;
                return Player;
            }
        }
    }
}
