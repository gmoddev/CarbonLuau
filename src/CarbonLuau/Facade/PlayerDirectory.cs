using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public struct PlayerPosition
        {
            public readonly float X, Y, Z;
            public PlayerPosition(float X, float Y, float Z) { this.X = X; this.Y = Y; this.Z = Z; }
        }
        public sealed class PlayerView
        {
            public object Identity, Connection;
            public string UserId, Name;
            public bool Connected;
            public Action<string> Send;
            public Func<string, bool> Permission;
            public Func<PlayerPosition> Position;
        }
        public sealed class PlayerLifetime
        {
            public string Token, UserId, Name;
            internal object Identity, Connection;
            internal bool Invalid;
        }
        public sealed class PlayerDirectory
        {
            private readonly Func<string, PlayerView> Lookup;
            private readonly Dictionary<string, PlayerLifetime> Live = new Dictionary<string, PlayerLifetime>(StringComparer.Ordinal);
            private ulong NextToken;
            private readonly int Owner = Thread.CurrentThread.ManagedThreadId;
            public PlayerDirectory(Func<string, PlayerView> Lookup) { this.Lookup = Lookup; }
            public void CheckOwner() { if (Thread.CurrentThread.ManagedThreadId != Owner) throw new FacadeException("facade owner-thread required"); }
            public PlayerLifetime Connect(PlayerView View)
            {
                CheckOwner();
                if (View == null || View.Identity == null || View.Connection == null || !View.Connected) throw new FacadeException("invalid player connection");
                FacadePolicy.UserId(View.UserId); FacadePolicy.Text(View.Name, 128, "player name");
                if (!Live.ContainsKey(View.UserId) && Live.Count >= FacadePolicy.Players) throw new FacadeException("player population exceeds supported bound");
                if (NextToken == ulong.MaxValue) throw new FacadeException("connection lifetime exhausted");
                var Value = new PlayerLifetime { Token = (++NextToken).ToString(CultureInfo.InvariantCulture), UserId = View.UserId,
                    Name = View.Name, Identity = View.Identity, Connection = View.Connection };
                Live[Value.UserId] = Value;
                return Value;
            }
            public PlayerLifetime Disconnect(string UserId, object Identity)
            {
                CheckOwner(); PlayerLifetime Value;
                if (!Live.TryGetValue(UserId, out Value) || !Object.ReferenceEquals(Value.Identity, Identity)) return null;
                Live.Remove(UserId); return Value;
            }
            public PlayerView Resolve(string Token, string UserId)
            {
                CheckOwner(); PlayerLifetime Value;
                if (!Live.TryGetValue(UserId, out Value) || Value.Token != Token || Value.Invalid) return null;
                var View = Lookup(UserId);
                if (View == null || !View.Connected || View.UserId != UserId || !Object.ReferenceEquals(View.Identity, Value.Identity) ||
                    !Object.ReferenceEquals(View.Connection, Value.Connection)) { Value.Invalid = true; return null; }
                FacadePolicy.Text(View.Name, 128, "player name");
                return View;
            }
            public PlayerLifetime Find(string UserId)
            {
                CheckOwner(); FacadePolicy.UserId(UserId); PlayerLifetime Value;
                return Live.TryGetValue(UserId, out Value) && Resolve(Value.Token, UserId) != null ? Value : null;
            }
            public List<PlayerLifetime> Snapshot()
            {
                CheckOwner(); var Result = new List<PlayerLifetime>();
                foreach (var Value in Live.Values) if (Resolve(Value.Token, Value.UserId) != null) Result.Add(Value);
                Result.Sort((A, B) => StringComparer.Ordinal.Compare(A.UserId, B.UserId));
                return Result;
            }
        }
    }
}
