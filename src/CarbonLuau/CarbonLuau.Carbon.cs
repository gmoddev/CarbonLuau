using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private FacadeWorld Gameplay;
        private CarbonCommandRegistrar CommandRegistrar;
        private readonly HashSet<string> RegisteredPermissions = new HashSet<string>(StringComparer.Ordinal);
        private long PermissionGeneration;
        private bool RegisteringPermissions;
        private PlayerView ReadPlayer(string UserId)
        {
            // Check population before bounded host lookup. No world/entity scan.
            if (BasePlayer.activePlayerList.Count > FacadePolicy.Players) throw new InvalidOperationException("player population exceeds supported bound");
            ulong Id;
            if (!ulong.TryParse(UserId, out Id)) return null;
            return ViewPlayer(BasePlayer.FindByID(Id));
        }
        private PlayerView ViewPlayer(BasePlayer Player)
        {
            if (Player == null || !Player.IsConnected || Player.Connection == null) return null;
            if (!Player.Connection.connected || !Player.Connection.active ||
                Player.Connection.userid.ToString(System.Globalization.CultureInfo.InvariantCulture) != Player.UserIDString) return null;
            return new PlayerView {
                Identity = Player, Connection = Player.Connection, UserId = Player.UserIDString, Name = Player.displayName,
                Connected = Player.IsConnected,
                Send = Message => Player.ChatMessage(Message),
                Permission = Permission => permission.UserHasPermission(Player.UserIDString, Permission)
            };
        }
        private void InitializeGameplay()
        {
            CommandRegistrar = new CarbonCommandRegistrar(this);
            Gameplay = new FacadeWorld(new PlayerDirectory(ReadPlayer), CommandRegistrar);
        }
        private void SeedPlayers()
        {
            Gameplay.Players.CheckOwner();
            if (BasePlayer.activePlayerList.Count > FacadePolicy.Players) throw new InvalidOperationException("player population exceeds supported bound");
            foreach (var Player in BasePlayer.activePlayerList) {
                var View = ViewPlayer(Player);
                if (View != null) Gameplay.Players.Connect(View);
            }
        }
        private void OnPlayerConnected(BasePlayer Player)
        {
            if (Stopping || Gameplay == null || !Initialized) return;
            try {
                Gameplay.Players.CheckOwner();
                var View = ViewPlayer(Player);
                if (View == null) return;
                var Lifetime = Gameplay.Players.Connect(View);
                Gameplay.Event("added", Lifetime);
                if (Host != null && !Host.Busy) RequestDrain();
            } catch (Exception) { PrintWarning("[CarbonLuau:Players] Connection event rejected (context or population/identity bound)."); }
        }
        private void OnPlayerDisconnected(BasePlayer Player, string Reason)
        {
            if (Stopping || Gameplay == null) return;
            try {
                Gameplay.Players.CheckOwner();
                if (Player == null) return;
                var Lifetime = Gameplay.Players.Disconnect(Player.UserIDString, Player);
                Gameplay.Event("removing", Lifetime);
                if (Host != null && !Host.Busy) RequestDrain();
            } catch (Exception) { PrintWarning("[CarbonLuau:Players] Disconnection event rejected (host context)."); }
        }
        private void RegisterActivePermissions()
        {
            // Permissions are metadata published only after successful commit.
            // Failure does not undo a committed generation or bypass checks.
            if (Stopping || Gameplay == null || Gameplay.Active == null || PermissionGeneration == Gameplay.Active.Generation || RegisteringPermissions) return;
            PermissionGeneration = Gameplay.Active.Generation;
            RegisteringPermissions = true;
            try {
                foreach (var Command in Gameplay.Active.Commands.Values) if (Command.Permission.Length != 0) {
                    try {
                        if (!permission.PermissionExists(Command.Permission)) {
                            if (RegisteredPermissions.Count >= 256) throw new InvalidOperationException("permission metadata bound");
                            RegisteredPermissions.Add(Command.Permission);
                            permission.RegisterPermission(Command.Permission, this);
                        }
                    } catch (Exception) { PrintWarning("[CarbonLuau:Permissions] Could not register active permission (host failure or 256-name metadata bound); authorization remains host-enforced."); }
                }
            } finally { RegisteringPermissions = false; }
        }
        // Carbon's convenience registration returns void and cannot atomically
        // replace several names. Its SDK exposes the chat list. Prepare a fresh
        // list and command objects, then publish once on the server thread. No
        // callbacks, permission hooks, allocations or external calls follow that
        // assignment in Publish. Late delegates close over the old session.
        private sealed class CarbonCommandRegistrar : ICommandRegistrar
        {
            private readonly CarbonLuau Plugin;
            private readonly object Ownership = new object();
            public CarbonCommandRegistrar(CarbonLuau Plugin) { this.Plugin = Plugin; }
            public void Publish(FacadeSession Previous, FacadeSession Next)
            {
                Plugin.Gameplay.Players.CheckOwner();
                var Manager = Carbon.Community.Runtime.CommandManager;
                if (Next != null && Manager.Chat.Count + Manager.ClientConsole.Count + Manager.RCon.Count > 16384)
                    throw new InvalidOperationException("host command registry exceeds inspection bound");
                var Chat = new List<API.Commands.Command>(Manager.Chat.Count + (Next == null ? 0 : Next.Commands.Count));
                foreach (var Existing in Manager.Chat) if (!Object.ReferenceEquals(Existing.Token, Ownership)) Chat.Add(Existing);
                if (Next != null) foreach (var Definition in Next.Commands.Values) {
                    foreach (var Factory in new[] {Manager.Chat, Manager.ClientConsole, Manager.RCon})
                        foreach (var Existing in Factory)
                            if (String.Equals(Existing.Name, Definition.Name, StringComparison.OrdinalIgnoreCase) && !Object.ReferenceEquals(Existing.Token, Ownership))
                                throw new InvalidOperationException("command name conflicts with an existing host command");
                    string Name = Definition.Name;
                    var Command = new API.Commands.Command.Chat {Name = Name, Help = Definition.Description, Reference = Plugin, Token = Ownership};
                    Command.Callback = Args => {
                        try {
                            Plugin.Gameplay.Players.CheckOwner();
                            if (Plugin.Stopping || Args.IsServer || Args.IsRCon) return;
                            var PlayerArgs = Args as API.Commands.PlayerArgs;
                            var Player = PlayerArgs == null ? null : PlayerArgs.Player as BasePlayer;
                            if (Player == null || !Player.IsConnected) return;
                            object[] Raw = Args.Arguments ?? new object[0];
                            if (Raw.Length > FacadePolicy.Arguments) return;
                            // Bind invocation to this exact current connection,
                            // not merely to the account named by a stale caller.
                            var Lifetime = Plugin.Gameplay.Players.Find(Player.UserIDString);
                            if (Lifetime == null || !Object.ReferenceEquals(Lifetime.Identity, Player) || !Object.ReferenceEquals(Lifetime.Connection, Player.Connection)) return;
                            var Arguments = new string[Raw.Length];
                            for (int Index = 0; Index < Arguments.Length; ++Index) {
                                if (!(Raw[Index] is string)) return;
                                Arguments[Index] = (string)Raw[Index];
                            }
                            Next.Invoke(Name, Player.UserIDString, Arguments);
                            if (Plugin.Host != null && !Plugin.Host.Busy) Plugin.RequestDrain();
                        } catch (Exception) { /* Reject malformed/off-thread host calls without exposing internals. */ }
                    };
                    Command.Fetch();
                    Chat.Add(Command);
                }
                Manager.Chat = Chat;
            }
        }
    }
}
