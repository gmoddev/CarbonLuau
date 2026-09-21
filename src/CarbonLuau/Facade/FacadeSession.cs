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
            public readonly ItemDirectory Items;
            public readonly ICommandRegistrar Registrar;
            internal readonly PlayerTakeItemOperation TakeItems = new PlayerTakeItemOperation();
            internal readonly GuiRetainedWorld Gui;
            public FacadeSession Active { get; private set; }
            private readonly SortedDictionary<long, FacadeSession> Addons = new SortedDictionary<long, FacadeSession>();
            private long GuiFlushCursor;
            private ulong GuiFlushCycle;
            public long PublicationVersion { get; private set; }
            public bool HasWork {
                get {
                    if (Active != null && Active.HasWork) return true;
                    foreach (FacadeSession Session in Addons.Values) if (Session.HasWork) return true;
                    return false;
                }
            }
            public FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar)
                : this(Players, Registrar, new ItemDirectory(Name => null), new GuiConfig().Validate(), null) { }
            internal FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar, IGuiBackend Backend)
                : this(Players, Registrar, new ItemDirectory(Name => null), new GuiConfig().Validate(), Backend) { }
            public FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar, ItemDirectory Items)
                : this(Players, Registrar, Items, new GuiConfig().Validate(), null) { }
            internal FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar, GuiLimits Limits, IGuiBackend Backend)
                : this(Players, Registrar, new ItemDirectory(Name => null), Limits, Backend) { }
            internal FacadeWorld(PlayerDirectory Players, ICommandRegistrar Registrar, ItemDirectory Items, GuiLimits Limits, IGuiBackend Backend)
            {
                this.Players = Players ?? throw new ArgumentNullException("Players"); this.Registrar = Registrar;
                this.Items = Items ?? throw new ArgumentNullException("Items");
                Gui = new GuiRetainedWorld(Limits ?? throw new ArgumentNullException("Limits"), Players, Backend ?? new InMemoryGuiBackend());
            }
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
            internal void DisconnectGui(PlayerLifetime Player)
            {
                Players.CheckOwner(); if (Player == null) return;
                if (Active != null) Active.Gui.Disconnect(Player);
                foreach (FacadeSession Session in Addons.Values) Session.Gui.Disconnect(Player);
            }
            internal bool AdmitGuiAction(PlayerLifetime Sender, string Token)
            {
                Players.CheckOwner(); GuiActionAdmission Admission;
                if (!Gui.TryAdmit(Sender, Token, out Admission)) return false;
                FacadeSession Session = null;
                if (Active != null && Active.DomainLifetimeId == checked((long)Admission.Record.DomainLifetimeId) &&
                    Active.VmGenerationId == checked((long)Admission.Record.VmGenerationId)) Session = Active;
                else {
                    FacadeSession Candidate;
                    if (Addons.TryGetValue(checked((long)Admission.Record.DomainLifetimeId), out Candidate) &&
                        Candidate.VmGenerationId == checked((long)Admission.Record.VmGenerationId)) Session = Candidate;
                }
                if (Session == null || !Session.TryEnqueueGuiAction(Admission, Sender)) {
                    Gui.QueueRejected(); return false;
                }
                Gui.Accepted(); return true;
            }
            internal string GuiStatus { get { return Gui.Status; } }
            internal void FlushGui(System.Diagnostics.Stopwatch Watch, int Milliseconds)
            {
                Players.CheckOwner(); int Sends = 0, Bytes = 0, WithoutProgress = 0;
                if (GuiFlushCycle == ulong.MaxValue) GuiFlushCycle = 0; GuiFlushCycle++;
                double DeadlineMilliseconds = Math.Min(Milliseconds,
                    Watch.Elapsed.TotalMilliseconds + Gui.Limits.GuiFlushBudgetMicroseconds / 1000.0);
                var SessionValues = Sessions();
                while (Sends < Gui.Limits.MaxPresentationSendsPerFlush && Bytes < Gui.Limits.MaxSerializedBytesPerFlush &&
                    Watch.Elapsed.TotalMilliseconds < DeadlineMilliseconds && SessionValues.Count != 0) {
                    FacadeSession Selected = null;
                    foreach (FacadeSession Session in SessionValues) if (Session.Gui.HasWork && Session.DomainLifetimeId > GuiFlushCursor &&
                        (Selected == null || Session.DomainLifetimeId < Selected.DomainLifetimeId)) Selected = Session;
                    if (Selected == null) foreach (FacadeSession Session in SessionValues) if (Session.Gui.HasWork &&
                        (Selected == null || Session.DomainLifetimeId < Selected.DomainLifetimeId)) Selected = Session;
                    if (Selected == null) break;
                    GuiFlushCursor = Selected.DomainLifetimeId;
                    int Used = Selected.Gui.FlushOne(Gui.Limits.MaxSerializedBytesPerFlush - Bytes, GuiFlushCycle);
                    if (Used == Int32.MinValue) { if (++WithoutProgress >= SessionValues.Count) break; continue; }
                    if (Used < 0) { if (++WithoutProgress >= SessionValues.Count) break; continue; }
                    WithoutProgress = 0; Sends++; Bytes += Used;
                }
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
            internal readonly GuiRetainedRegistry Gui;
            private readonly int Capacity;
            internal readonly object CommandOwnership = new object();
            private ulong NextRegistration;
            public bool Active, Disposed;
            public ulong Rejected;
            public bool HasWork { get { return Pending.Count != 0 || Gui.HasWork; } }
            public int PendingCount { get { return Pending.Count; } }
            public int ListenerCount { get { return Listeners.Count; } }
            public readonly NativeRuntime.HostDelegate Callback;
            public FacadeSession(FacadeWorld World, long Generation, int Capacity)
                : this(World, Generation, Generation, Capacity) { }
            public FacadeSession(FacadeWorld World, long VmGenerationId, long DomainLifetimeId, int Capacity)
            { this.World = World; this.VmGenerationId = VmGenerationId; this.DomainLifetimeId = DomainLifetimeId;
                this.Capacity = Math.Min(Capacity, FacadePolicy.PendingEvents);
                Gui = new GuiRetainedRegistry(World.Gui, checked((ulong)VmGenerationId), checked((ulong)DomainLifetimeId)); Callback = HostCall; }
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
            internal bool TryEnqueueGuiAction(GuiActionAdmission Admission, PlayerLifetime Player)
            {
                if (!Active || Disposed || !World.IsActive(this) || Admission == null || Player == null) return false;
                string[] Registrations = Admission.Registrations; GuiActionRecord Record = Admission.Record;
                if (Registrations == null || Registrations.Length == 0 || Pending.Count > Capacity - Registrations.Length) { Rejected++; return false; }
                var Payloads = new List<byte[]>(Registrations.Length);
                try {
                    foreach (string Registration in Registrations) {
                        byte[] Payload = FacadePolicy.Pack("activated", Registration, Player.Token, Player.UserId, Player.Name,
                            Record.Token, Record.ScreenId.ToString(CultureInfo.InvariantCulture),
                            Record.PresentationEpoch.ToString(CultureInfo.InvariantCulture), Record.ButtonId.ToString(CultureInfo.InvariantCulture));
                        if (Payload.Length > 16384) throw new FacadeException("GUI action payload exceeds limit");
                        Payloads.Add(Payload);
                    }
                } catch { Rejected++; return false; }
                foreach (byte[] Payload in Payloads) Pending.Enqueue(Payload);
                return true;
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
            public void Clear() { Pending.Clear(); Listeners.Clear(); Commands.Clear(); Publications.Clear(); Gui.Dispose(); }
            private bool Gate(string[] Fields)
            {
                if (!Active || !World.IsActive(this) || Disposed || Fields.Length < 5) return false;
                if (Fields[0] == "command") {
                    ScriptCommand Command;
                    if (Fields.Length < 6 || !Commands.TryGetValue(Fields[5], out Command) || Command.Id != Fields[1]) return false;
                    var View = World.Players.Resolve(Fields[2], Fields[3]);
                    return View != null && (Command.Permission.Length == 0 || View.Permission(Command.Permission));
                }
                if (Fields[0] == "activated") {
                    ulong ScreenId, Epoch, ButtonId;
                    return Fields.Length == 9 && UInt64.TryParse(Fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out ScreenId) &&
                        UInt64.TryParse(Fields[7], NumberStyles.None, CultureInfo.InvariantCulture, out Epoch) &&
                        UInt64.TryParse(Fields[8], NumberStyles.None, CultureInfo.InvariantCulture, out ButtonId) &&
                        World.Gui.ValidateQueued(Fields[5], Fields[1], Fields[2], Fields[3], ScreenId, Epoch, ButtonId);
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
                    PublicationCheckpoint Checkpoint = new PublicationCheckpoint(Listeners, Commands);
                    Gui.BeginPublication();
                    try { Publications.Push(Checkpoint); }
                    catch { Gui.RollbackPublication(); throw; }
                    return new string[0];
                }
                if (Code == 11) {
                    if (Fields.Length != 0 || Publications.Count == 0) throw new FacadeException("invalid publication commit");
                    Gui.CommitPublication(); Publications.Pop(); return new string[0];
                }
                if (Code == 12) {
                    if (Fields.Length != 0 || Publications.Count == 0) throw new FacadeException("invalid publication rollback");
                    Gui.RollbackPublication();
                    PublicationCheckpoint Checkpoint = Publications.Pop();
                    Listeners.Clear(); foreach (var Item in Checkpoint.Listeners) Listeners.Add(Item.Key, Item.Value);
                    Commands.Clear(); foreach (var Item in Checkpoint.Commands) Commands.Add(Item.Key, Item.Value);
                    return new string[0];
                }
                if (Code == 9) { if (!Gate(Fields)) throw new FacadeException("stale or unauthorized callback"); return new string[0]; }
                if (Code == 20) return Gui.Query(Fields);
                if (Code == 21) return Gui.Mutate(Fields, Id);
                int Expected = Code == 1 ? 0 : (Code == 3 || (Code >= 22 && Code <= 24)) ? 2 :
                    Code == 25 ? 1 : Code == 26 ? 3 : (Code == 27 || Code == 29) ? 4 : Code == 28 ? 5 :
                    (Code == 4 || Code == 5 || Code == 8) ? 3 : 1;
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
                    case 22: {
                        var View = World.Players.Resolve(Fields[0], Fields[1]);
                        if (View == null) throw new FacadeException("Player is no longer connected");
                        if (View.Position == null) throw new FacadeException("Player position is unavailable");
                        PlayerPosition Position = View.Position();
                        if (Single.IsNaN(Position.X) || Single.IsInfinity(Position.X) ||
                            Single.IsNaN(Position.Y) || Single.IsInfinity(Position.Y) ||
                            Single.IsNaN(Position.Z) || Single.IsInfinity(Position.Z))
                            throw new FacadeException("Player position is invalid");
                        return new[] {Position.X.ToString("R", CultureInfo.InvariantCulture),
                            Position.Y.ToString("R", CultureInfo.InvariantCulture), Position.Z.ToString("R", CultureInfo.InvariantCulture)};
                    }
                    case 23: case 24: {
                        var View = World.Players.Resolve(Fields[0], Fields[1]);
                        if (View == null) throw new FacadeException("Player is no longer connected");
                        Func<float> Read = Code == 23 ? View.Health : View.MaxHealth;
                        string Name = Code == 23 ? "health" : "maximum health";
                        if (Read == null) throw new FacadeException("Player " + Name + " is unavailable");
                        float Value = Read();
                        if (Single.IsNaN(Value) || Single.IsInfinity(Value)) throw new FacadeException("Player " + Name + " is invalid");
                        return new[] {Value.ToString("R", CultureInfo.InvariantCulture)};
                    }
                    case 25: {
                        return new[] {World.Items.Resolve(Fields[0]) == null ? "0" : "1"};
                    }
                    case 26: case 27: {
                        FacadePolicy.ItemShortName(Fields[2]);
                        long Amount = Code == 27 ? FacadePolicy.ExactPositiveInteger(Fields[3], "item amount") : 0;
                        var View = World.Players.Resolve(Fields[0], Fields[1]);
                        if (View == null) throw new FacadeException("Player is no longer connected");
                        object Definition = World.Items.Resolve(Fields[2]);
                        if (Definition == null) return new[] {"0"};
                        if (View.Inventory == null) throw new FacadeException("Player inventory is unavailable");
                        PhysicalInventorySource Source = View.Inventory();
                        if (Code == 27) return new[] {PhysicalInventoryObservation.Has(Source, Definition, Amount) ? "1" : "0"};
                        return new[] {PhysicalInventoryObservation.Count(Source, Definition).ToString(CultureInfo.InvariantCulture)};
                    }
                    case 28: {
                        if (!Active || Disposed || !World.IsActive(this))
                            throw new FacadeException("Teleport requires a committed domain; use task.defer for startup movement");
                        var View = World.Players.Resolve(Fields[0], Fields[1]);
                        if (View == null) throw new FacadeException("Player is no longer connected");
                        if (View.Teleport == null) throw new FacadeException("Player teleport is unavailable");
                        float X, Y, Z;
                        if (!Single.TryParse(Fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out X) ||
                            !Single.TryParse(Fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out Y) ||
                            !Single.TryParse(Fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out Z) ||
                            Single.IsNaN(X) || Single.IsInfinity(X) || Single.IsNaN(Y) || Single.IsInfinity(Y) ||
                            Single.IsNaN(Z) || Single.IsInfinity(Z)) throw new FacadeException("invalid Teleport position");
                        View.Teleport.Execute(new PlayerPosition(X, Y, Z));
                        return new string[0];
                    }
                    case 29: {
                        if (!Active || Disposed || !World.IsActive(this))
                            throw new FacadeException("TakeItem requires a committed domain; use task.defer for startup mutation");
                        FacadePolicy.ItemShortName(Fields[2]);
                        long Parsed = FacadePolicy.ExactPositiveInteger(Fields[3], "item amount");
                        if (Parsed > Int32.MaxValue) throw new FacadeException("item amount exceeds Int32.MaxValue");
                        object Definition = World.Items.Resolve(Fields[2]);
                        bool Result = World.TakeItems.Execute(Fields[0],
                            () => World.Players.Resolve(Fields[0], Fields[1]), Definition, (int)Parsed);
                        return new[] {Result ? "1" : "0"};
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
