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
        private sealed class FacadeException : InvalidOperationException
        { public FacadeException(string Message) : base(Message) { } }
        public static class FacadePolicy
        {
            public const string ApiName = "CarbonLuau", ApiVersion = "0.3.0-experimental";
            public const int Players = 1024, ListenersPerSignal = 128, Listeners = 256, Commands = 64, PendingEvents = 256;
            public const int CommandBytes = 32, PermissionBytes = 128, DescriptionBytes = 256;
            public const int MessageBytes = 1024, Arguments = 16, ArgumentBytes = 512, TotalArgumentBytes = 4096;
            public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            public static void Text(string Value, int Bytes, string Label)
            {
                if (Value == null || Value.Length > Bytes || Value.IndexOf('\0') >= 0 || Utf8.GetByteCount(Value) > Bytes)
                    throw new FacadeException(Label + " exceeds limit or contains NUL");
            }
            public static void Identifier(string Value, bool Permission)
            {
                Text(Value, Permission ? PermissionBytes : CommandBytes, Permission ? "permission name" : "command name");
                if (Value.Length == 0 || Value[0] < 'a' || Value[0] > 'z') throw new FacadeException("name is invalid");
                foreach (char C in Value)
                    if (!(C >= 'a' && C <= 'z') && !(C >= '0' && C <= '9') && C != '_' && C != '-' && !(Permission && C == '.'))
                        throw new FacadeException("name is invalid");
                if (Permission && (Value.EndsWith(".", StringComparison.Ordinal) || Value.Contains("..") || !Value.Contains(".")))
                    throw new FacadeException("permission requires nonempty dotted namespace");
                if (!Permission && (Value.StartsWith("carbon", StringComparison.Ordinal) || Value.StartsWith("oxide", StringComparison.Ordinal) ||
                    Value.StartsWith("rcon", StringComparison.Ordinal) || Value == "c" || Value == "quit" || Value == "restart" || Value == "server"))
                    throw new FacadeException("protected command name");
            }
            public static void UserId(string Value)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > 20) throw new FacadeException("invalid user ID string");
                foreach (char C in Value) if (C < '0' || C > '9') throw new FacadeException("invalid user ID string");
            }
            public static byte[] Pack(params string[] Fields)
            { return Utf8.GetBytes(String.Join("\0", Fields) + (Fields.Length == 0 ? "" : "\0")); }
            public static string[] Unpack(byte[] Bytes)
            {
                if (Bytes.Length == 0) return new string[0];
                if (Bytes[Bytes.Length - 1] != 0) throw new FacadeException("invalid host payload");
                return Utf8.GetString(Bytes, 0, Bytes.Length - 1).Split('\0');
            }
        }

        // These managed-only views are never serialized to Lua. Fresh views must
        // be obtained from the actual host on every resolution.
        public sealed class PlayerView
        {
            public object Identity, Connection;
            public string UserId, Name;
            public bool Connected;
            public Action<string> Send;
            public Func<string, bool> Permission;
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
        public sealed class ScriptCommand
        { public string Id, Name, Permission, Description; }
        // Publish must prepare without effects, then atomically replace only
        // CarbonLuau's chat entries. A thrown failure must leave Previous intact.
        public interface ICommandRegistrar
        {
            void Publish(FacadeSession Previous, FacadeSession Next);
        }
        public sealed class FacadeWorld
        {
            public readonly PlayerDirectory Players;
            public readonly ICommandRegistrar Registrar;
            public FacadeSession Active { get; private set; }
            public FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar) { this.Players = Players; this.Registrar = Registrar; }
            public void Commit(FacadeSession Next)
            {
                Players.CheckOwner();
                Registrar.Publish(Active, Next);
                if (Active != null) Active.Active = false;
                Active = Next;
                if (Next != null) Next.Active = true;
            }
            public void Retire(FacadeSession Value)
            { Players.CheckOwner(); if (Active == Value) Commit(null); Value.Active = false; Value.Disposed = true; Value.Clear(); }
            public void Event(string Kind, PlayerLifetime Player)
            {
                Players.CheckOwner();
                if (Active == null || Player == null) return;
                Active.Event(Kind, Player);
            }
        }
        public sealed class FacadeSession
        {
            public readonly long Generation;
            public readonly SortedDictionary<string, ScriptCommand> Commands = new SortedDictionary<string, ScriptCommand>(StringComparer.Ordinal);
            private readonly SortedDictionary<ulong, string> Listeners = new SortedDictionary<ulong, string>();
            private readonly Queue<byte[]> Pending = new Queue<byte[]>();
            private readonly FacadeWorld World;
            private readonly int Capacity;
            private ulong NextRegistration;
            public bool Active, Disposed;
            public ulong Rejected;
            public bool HasWork { get { return Pending.Count != 0; } }
            public int PendingCount { get { return Pending.Count; } }
            public int ListenerCount { get { return Listeners.Count; } }
            public readonly NativeRuntime.HostDelegate Callback;
            public FacadeSession(FacadeWorld World, long Generation, int Capacity)
            { this.World = World; this.Generation = Generation; this.Capacity = Math.Min(Capacity, FacadePolicy.PendingEvents); Callback = HostCall; }
            private string Id()
            {
                if (NextRegistration == ulong.MaxValue) throw new FacadeException("registration identity exhausted");
                return (++NextRegistration).ToString(CultureInfo.InvariantCulture);
            }
            private void Enqueue(string[] Fields)
            {
                if (!Active || Disposed || Pending.Count >= Capacity) { Rejected++; return; }
                byte[] Bytes = FacadePolicy.Pack(Fields);
                if (Bytes.Length > 16384) { Rejected++; return; }
                Pending.Enqueue(Bytes);
            }
            public void Event(string Kind, PlayerLifetime Player)
            {
                World.Players.CheckOwner();
                foreach (var Listener in Listeners) if (Listener.Value == Kind)
                    Enqueue(new[] {Kind, Listener.Key.ToString(CultureInfo.InvariantCulture), Player.Token, Player.UserId, Player.Name});
            }
            public bool Invoke(string Name, string UserId, string[] Arguments)
            {
                World.Players.CheckOwner(); ScriptCommand Command;
                if (!Active || Disposed || World.Active != this || !Commands.TryGetValue(Name, out Command)) return false;
                PlayerLifetime Player = World.Players.Find(UserId);
                if (Player == null || Arguments == null || Arguments.Length > FacadePolicy.Arguments) { Rejected++; return false; }
                try {
                    int Total = 0;
                    foreach (string Argument in Arguments) { FacadePolicy.Text(Argument, FacadePolicy.ArgumentBytes, "command argument"); Total += FacadePolicy.Utf8.GetByteCount(Argument); }
                    if (Total > FacadePolicy.TotalArgumentBytes) throw new FacadeException("command arguments exceed total limit");
                    var View = World.Players.Resolve(Player.Token, Player.UserId);
                    if (View == null || (Command.Permission.Length != 0 && !View.Permission(Command.Permission))) { Rejected++; return false; }
                    var Fields = new List<string> {"command", Command.Id, Player.Token, Player.UserId, View.Name, Name};
                    Fields.AddRange(Arguments); // Pack immediately: never retain host array.
                    if (Pending.Count >= Capacity) { Rejected++; return false; }
                    Enqueue(Fields.ToArray()); return true;
                } catch (Exception) { Rejected++; return false; }
            }
            public void Flush(RuntimeGeneration Runtime, System.Diagnostics.Stopwatch Watch, int Milliseconds)
            {
                World.Players.CheckOwner();
                for (int Count = 0; Count < 64 && Pending.Count != 0 && Watch.Elapsed.TotalMilliseconds < Milliseconds; ++Count) {
                    if (Runtime.Event(Pending.Dequeue()) != RuntimeStatus.OK) Rejected++;
                    if (Runtime.Info.Ready == 0) { Pending.Clear(); break; }
                }
            }
            public void Clear() { Pending.Clear(); Listeners.Clear(); Commands.Clear(); }
            private bool Gate(string[] Fields)
            {
                if (!Active || World.Active != this || Disposed || Fields.Length < 5) return false;
                if (Fields[0] == "command") {
                    ScriptCommand Command;
                    if (Fields.Length < 6 || !Commands.TryGetValue(Fields[5], out Command) || Command.Id != Fields[1]) return false;
                    var View = World.Players.Resolve(Fields[2], Fields[3]);
                    return View != null && (Command.Permission.Length == 0 || View.Permission(Command.Permission));
                }
                ulong IdValue; string Kind;
                return ulong.TryParse(Fields[1], out IdValue) && Listeners.TryGetValue(IdValue, out Kind) && Kind == Fields[0];
            }
            private string[] Operation(uint Code, string[] Fields)
            {
                if (Code == 0) {
                    if (Active || Fields.Length != 0 || Commands.Count != 0 || Listeners.Count != 0) throw new FacadeException("invalid facade preparation");
                    // Exercise bounded metadata/codec paths before the first
                    // script deadline. Never publish a command or send a message.
                    FacadePolicy.Unpack(FacadePolicy.Pack("prepare"));
                    Operation(1, new string[0]);
                    string Listener = Operation(6, new[]{"added"})[0];
                    Operation(7, new[]{Listener});
                    Operation(8, new[]{"clprepare", "carbonluau.prepare", ""});
                    Commands.Clear();
                    return new string[0];
                }
                if (Code == 9) { if (!Gate(Fields)) throw new FacadeException("stale or unauthorized callback"); return new string[0]; }
                int Expected = Code == 1 ? 0 : Code == 3 ? 2 : (Code == 4 || Code == 5 || Code == 8) ? 3 : 1;
                if (Fields.Length != Expected) throw new FacadeException("invalid host arguments");
                switch (Code) {
                    case 1: {
                        var Result = new List<string>();
                        foreach (var Player in World.Players.Snapshot()) { Result.Add(Player.Token); Result.Add(Player.UserId); Result.Add(World.Players.Resolve(Player.Token, Player.UserId).Name); }
                        return Result.ToArray();
                    }
                    case 2: {
                        var Player = World.Players.Find(Fields[0]);
                        return Player == null ? new string[0] : new[] {Player.Token, Player.UserId, World.Players.Resolve(Player.Token, Player.UserId).Name};
                    }
                    case 3: {
                        var View = World.Players.Resolve(Fields[0], Fields[1]);
                        return View == null ? new[] {"0", ""} : new[] {"1", View.Name};
                    }
                    case 4: case 5: {
                        if (Code == 4 && (!Active || World.Active != this)) throw new FacadeException("SendMessage requires a committed generation; use task.defer for startup delivery");
                        var View = World.Players.Resolve(Fields[0], Fields[1]);
                        if (View == null) throw new FacadeException("Player is no longer connected");
                        if (Code == 4) {
                            FacadePolicy.Text(Fields[2], FacadePolicy.MessageBytes, "message");
                            View.Send(Fields[2]); return new string[0];
                        }
                        FacadePolicy.Identifier(Fields[2], true);
                        return new[] {View.Permission(Fields[2]) ? "1" : "0"};
                    }
                    case 6: {
                        if (Fields[0] != "added" && Fields[0] != "removing") throw new FacadeException("unknown signal");
                        int Count = 0; foreach (string Kind in Listeners.Values) if (Kind == Fields[0]) Count++;
                        if (Count >= FacadePolicy.ListenersPerSignal || Listeners.Count >= FacadePolicy.Listeners) throw new FacadeException("signal listener limit reached");
                        string Value = Id(); Listeners.Add(ulong.Parse(Value, CultureInfo.InvariantCulture), Fields[0]); return new[] {Value};
                    }
                    case 7: {
                        ulong Value; if (!ulong.TryParse(Fields[0], out Value)) throw new FacadeException("invalid Connection");
                        Listeners.Remove(Value); return new string[0];
                    }
                    case 8: {
                        if (Active) throw new FacadeException("Commands:Register is initialization-only");
                        FacadePolicy.Identifier(Fields[0], false);
                        if (Fields[1].Length != 0) FacadePolicy.Identifier(Fields[1], true);
                        FacadePolicy.Text(Fields[2], FacadePolicy.DescriptionBytes, "description");
                        if (Commands.Count >= FacadePolicy.Commands) throw new FacadeException("command registration limit reached");
                        if (Commands.ContainsKey(Fields[0])) throw new FacadeException("duplicate command name");
                        var Command = new ScriptCommand {Id = Id(), Name = Fields[0], Permission = Fields[1], Description = Fields[2]};
                        Commands.Add(Command.Name, Command); return new[] {Command.Id};
                    }
                    default: throw new FacadeException("invalid host operation");
                }
            }
            private uint HostCall(ulong ExpectedGeneration, uint Code, IntPtr Request, uint Length, IntPtr Response, uint Capacity, out uint Written)
            {
                Written = 0;
                try {
                    World.Players.CheckOwner();
                    if (Disposed || ExpectedGeneration != (ulong)Generation || Length > 16384 || Capacity != 262144) return 1;
                    var Bytes = new byte[Length]; if (Length != 0) Marshal.Copy(Request, Bytes, 0, (int)Length);
                    var Result = FacadePolicy.Pack(Operation(Code, FacadePolicy.Unpack(Bytes)));
                    if (Result.Length > Capacity) throw new FacadeException("host response exceeds bound");
                    Marshal.Copy(Result, 0, Response, Result.Length); Written = (uint)Result.Length; return 0;
                } catch (Exception Error) {
                    // No exception crosses the reverse P/Invoke boundary. Only
                    // curated validation errors are script-visible.
                    try {
                        string Message = Error is FacadeException ? Error.Message : "host operation failed";
                        if (Message.Length > 200) Message = "host operation failed";
                        byte[] Bytes = FacadePolicy.Utf8.GetBytes(Message);
                        if (Bytes.Length <= Capacity) { Marshal.Copy(Bytes, 0, Response, Bytes.Length); Written = (uint)Bytes.Length; }
                    } catch { Written = 0; }
                    return 1;
                }
            }
        }

        public sealed partial class NativeRuntime
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate uint HostDelegate(ulong Generation, uint Operation, IntPtr Request, uint Length, IntPtr Response, uint Capacity, out uint Written);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus FacadeDelegate(ulong Vm, ulong Generation, HostDelegate Host);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate RuntimeStatus EventDelegate(ulong Vm, byte[] Payload, uint Length);
            private FacadeDelegate InstallFacade;
            private EventDelegate AdmitEvent;
            private readonly Dictionary<ulong, FacadeSession> FacadeRoots = new Dictionary<ulong, FacadeSession>();
            public void Facade(ulong Handle, FacadeSession Session)
            {
                CheckOwner();
                if ((AbiVersion & 65535) < 2) throw new FacadeException("Phase 3 requires native ABI 1.2 or later");
                if (InstallFacade == null) { InstallFacade = Loader.Bind<FacadeDelegate>("cl_vm_facade"); AdmitEvent = Loader.Bind<EventDelegate>("cl_vm_event"); }
                FacadeRoots.Add(Handle, Session); // Root until native destruction succeeds, including failed installation.
                InsideNative = true;
                try { Require(InstallFacade(Handle, (ulong)Session.Generation, Session.Callback)); }
                finally { InsideNative = false; }
            }
            public RuntimeStatus Event(ulong Handle, byte[] Payload) { CheckOwner(); return AdmitEvent(Handle, Payload, (uint)Payload.Length); }
        }
        public sealed partial class RuntimeGeneration
        {
            public FacadeSession FacadeSession { get; private set; }
            public void Facade(FacadeSession Session) { FacadeSession = Session; Native.Facade(Handle, Session); }
            public RuntimeStatus Event(byte[] Payload) { return Native.Event(Handle, Payload); }
        }
    }
}
