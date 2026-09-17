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
        public sealed class FacadeWorld
        {
            public readonly PlayerDirectory Players;
            public readonly ICommandRegistrar Registrar;
            public FacadeSession Active { get; private set; }
            private readonly SortedDictionary<long, FacadeSession> Addons = new SortedDictionary<long, FacadeSession>();
            public long PublicationVersion { get; private set; }
            public bool HasWork {
                get {
                    if (Active != null && Active.HasWork) return true;
                    foreach (FacadeSession Session in Addons.Values) if (Session.HasWork) return true;
                    return false;
                }
            }
            public FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar) { this.Players = Players; this.Registrar = Registrar; }
            public void Commit(FacadeSession Next)
            {
                Players.CheckOwner();
                Registrar.Publish(Active, Next);
                if (Active != null) Active.Active = false;
                Active = Next;
                if (Next != null) Next.Active = true;
                PublicationVersion++;
            }
            public void CommitAddon(FacadeSession Previous, FacadeSession Next)
            {
                Players.CheckOwner(); Registrar.Publish(Previous, Next);
                if (Previous != null) {
                    Addons.Remove(Previous.DomainLifetimeId); Previous.Active = false; Previous.Disposed = true; Previous.Clear();
                }
                if (Next != null) {
                    if (Addons.ContainsKey(Next.DomainLifetimeId)) throw new FacadeException("duplicate active addon domain");
                    Addons.Add(Next.DomainLifetimeId, Next); Next.Active = true;
                }
                PublicationVersion++;
            }
            public void Retire(FacadeSession Value)
            {
                Players.CheckOwner();
                if (Active == Value) Commit(null);
                else if (Value != null && Addons.ContainsKey(Value.DomainLifetimeId)) CommitAddon(Value, null);
                if (Value != null) { Value.Active = false; Value.Disposed = true; Value.Clear(); }
            }
            public bool IsActive(FacadeSession Value)
            { return Value != null && (Active == Value || (Addons.ContainsKey(Value.DomainLifetimeId) && Addons[Value.DomainLifetimeId] == Value)); }
            public List<FacadeSession> Sessions()
            {
                var Result = new List<FacadeSession>(); if (Active != null) Result.Add(Active);
                Result.AddRange(Addons.Values); return Result;
            }
            public void Event(string Kind, PlayerLifetime Player)
            {
                Players.CheckOwner();
                if (Player == null) return;
                Exception Failure = null;
                if (Active != null) try { Active.Event(Kind, Player); } catch (Exception Error) { Failure = Error; }
                foreach (FacadeSession Session in Addons.Values) try { Session.Event(Kind, Player); } catch (Exception Error) { if (Failure == null) Failure = Error; }
                if (Failure != null) throw Failure;
            }
        }
        public sealed class FacadeSession
        {
            public readonly long VmGenerationId, DomainLifetimeId;
            public long Generation { get { return DomainLifetimeId; } }
            public readonly SortedDictionary<string, ScriptCommand> Commands = new SortedDictionary<string, ScriptCommand>(StringComparer.Ordinal);
            private readonly SortedDictionary<ulong, string> Listeners = new SortedDictionary<ulong, string>();
            private readonly Queue<byte[]> Pending = new Queue<byte[]>();
            private sealed class PublicationCheckpoint
            {
                public readonly SortedDictionary<ulong, string> Listeners;
                public readonly SortedDictionary<string, ScriptCommand> Commands;
                public PublicationCheckpoint(SortedDictionary<ulong, string> Listeners, SortedDictionary<string, ScriptCommand> Commands)
                {
                    this.Listeners = new SortedDictionary<ulong, string>(Listeners);
                    this.Commands = new SortedDictionary<string, ScriptCommand>(Commands, StringComparer.Ordinal);
                }
            }
            private readonly Stack<PublicationCheckpoint> Publications = new Stack<PublicationCheckpoint>();
            private readonly FacadeWorld World;
            private readonly int Capacity;
            internal readonly object CommandOwnership = new object();
            private ulong NextRegistration;
            public bool Active, Disposed;
            public ulong Rejected;
            public bool HasWork { get { return Pending.Count != 0; } }
            public int PendingCount { get { return Pending.Count; } }
            public int ListenerCount { get { return Listeners.Count; } }
            public readonly NativeRuntime.HostDelegate Callback;
            public FacadeSession(FacadeWorld World, long Generation, int Capacity)
                : this(World, Generation, Generation, Capacity) { }
            public FacadeSession(FacadeWorld World, long VmGenerationId, long DomainLifetimeId, int Capacity)
            { this.World = World; this.VmGenerationId = VmGenerationId; this.DomainLifetimeId = DomainLifetimeId;
                this.Capacity = Math.Min(Capacity, FacadePolicy.PendingEvents); Callback = HostCall; }
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
                if (!Active || Disposed || !World.IsActive(this) || !Commands.TryGetValue(Name, out Command)) return false;
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
            public void Flush(RuntimeDomain Runtime, System.Diagnostics.Stopwatch Watch, int Milliseconds)
            { Flush(Runtime, Watch, Milliseconds, 64); }
            internal void Flush(RuntimeDomain Runtime, System.Diagnostics.Stopwatch Watch, int Milliseconds, int Maximum)
            {
                World.Players.CheckOwner();
                for (int Count = 0; Count < Maximum && Pending.Count != 0 && Watch.Elapsed.TotalMilliseconds < Milliseconds; ++Count) {
                    if (Runtime.Event(Pending.Dequeue()) != RuntimeStatus.OK) Rejected++;
                    if (Runtime.Info.Ready == 0) { Pending.Clear(); break; }
                }
            }
            public void Clear() { Pending.Clear(); Listeners.Clear(); Commands.Clear(); Publications.Clear(); }
            private bool Gate(string[] Fields)
            {
                if (!Active || !World.IsActive(this) || Disposed || Fields.Length < 5) return false;
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
                if (Code == 10) {
                    if (Fields.Length != 0) throw new FacadeException("invalid publication begin");
                    Publications.Push(new PublicationCheckpoint(Listeners, Commands));
                    return new string[0];
                }
                if (Code == 11) {
                    if (Fields.Length != 0 || Publications.Count == 0) throw new FacadeException("invalid publication commit");
                    Publications.Pop(); return new string[0];
                }
                if (Code == 12) {
                    if (Fields.Length != 0 || Publications.Count == 0) throw new FacadeException("invalid publication rollback");
                    PublicationCheckpoint Checkpoint = Publications.Pop();
                    Listeners.Clear(); foreach (var Item in Checkpoint.Listeners) Listeners.Add(Item.Key, Item.Value);
                    Commands.Clear(); foreach (var Item in Checkpoint.Commands) Commands.Add(Item.Key, Item.Value);
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
                        if (Code == 4 && (!Active || !World.IsActive(this))) throw new FacadeException("SendMessage requires a committed generation; use task.defer for startup delivery");
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
            private uint HostCall(ulong ExpectedDomainLifetime, uint Code, IntPtr Request, uint Length, IntPtr Response, uint Capacity, out uint Written)
            {
                Written = 0;
                try {
                    World.Players.CheckOwner();
                    if (Disposed || ExpectedDomainLifetime != (ulong)DomainLifetimeId || Length > 16384 || Capacity != 262144) return 1;
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
    }
}

