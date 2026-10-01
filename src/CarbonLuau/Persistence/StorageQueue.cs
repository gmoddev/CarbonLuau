using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Pure owner-thread namespace/admission and completion substrate. No Luau
        // API, callback invocation, disk access, wait, or process launch lives here.
        internal sealed class StorageQueue
        {
            internal enum Operation : uint { Get=1, Set=2, Remove=3, Demand=4, Await=5 }
            internal enum Preparation { Unknown, Preparing, Ready, Unavailable }
            internal enum AwaitOutcome { None, Ready, IndexPreparing, QueryUnavailable }
            internal enum Error : uint { None, InvalidArgument, QuotaExceeded, StorageUnavailable, StorageBusy, StorageFull,
                StorageCorrupt, FormatUnsupported, DeadlineExceeded, StorageError, Indeterminate }
            internal sealed class Rejection : InvalidOperationException
            {
                internal readonly uint Status;
                internal Rejection(uint Status) : base("storage submission rejected") { this.Status=Status; }
            }
            internal sealed class Binding
            {
                internal readonly ulong Host, Vm, Domain;
                internal readonly string Package;
                internal readonly string Namespace;
                internal bool Alive=true;
                internal Binding(ulong Host,ulong Vm,ulong Domain,string Package)
                { this.Host=Host; this.Vm=Vm; this.Domain=Domain; this.Package=Package; Namespace=Package==null ? "\0" : "\u0001"+Package; }
            }
            internal sealed class Request
            {
                internal Binding Owner;
                internal ulong Id, WireId, Route, End;
                internal Operation Op;
                internal byte[] Frame;
                internal byte[] Envelope;
                internal Error Error;
                internal bool Found, NamespacePresent, Sent;
                internal bool Released, HandedOff, CallbackDiscarded;
                internal bool InternalDemand, Waiting;
                internal string Store, Field;
                internal Preparation Preparation;
                internal AwaitOutcome AwaitResult;
                internal int Reservation=-1;
                internal int State; // owner: 0 queued; supervisor: 1 in flight, 2 complete
            }
            private sealed class Bucket
            {
                internal readonly string Namespace;
                internal readonly Queue<Request> Pending=new Queue<Request>(8);
                internal int Count, Bindings;
                internal int Waiters;
                internal bool MayHaveData;
                internal double Requests=32, Mutations=8;
                internal ulong Updated;
                internal Bucket(string Namespace,ulong Now) { this.Namespace=Namespace; Updated=Now; }
            }
            private readonly int OwnerThread=Thread.CurrentThread.ManagedThreadId;
            private readonly Dictionary<string,Bucket> Namespaces=new Dictionary<string,Bucket>(256+258,StringComparer.Ordinal);
            private readonly List<Binding> Bindings=new List<Binding>(258);
            private readonly List<Bucket> Rotation=new List<Bucket>(256+258);
            private readonly Queue<Request> Completions=new Queue<Request>(128);
            private readonly Request[] Reservations=new Request[128];
            private sealed class DemandIntent
            {
                internal Binding Owner;
                internal string Store, Field;
                internal Preparation State;
                internal ulong Due;
                internal int AttemptedEpoch;
                internal bool Proactive;
                internal int Users;
            }
            private readonly Dictionary<string,DemandIntent> Demands=new Dictionary<string,DemandIntent>(StringComparer.Ordinal);
            private readonly Queue<string> ProactiveDemandKeys=new Queue<string>();
            private readonly HashSet<string> QueuedProactiveDemandKeys=new HashSet<string>(StringComparer.Ordinal);
            private readonly Func<ulong> Clock;
            private readonly ulong Host;
            private ulong NextId, NextWireId, NextInternalRoute, Updated;
            private double Requests=256, Mutations=64;
            private int Cursor, Pending, Waiters, IntakeRemaining=8, WorkerEpoch;
            private Request Active;
            internal long QueueRejected, RateRejected, Expired, Discarded, QuotaRejected, BackendFailures;
            internal readonly long[] Completed=new long[3];
            internal bool Ready;
            internal StorageQueue(ulong Host,Func<ulong> Clock) { if (Host==0 || Clock==null) throw new ArgumentException(); this.Host=Host; this.Clock=Clock; Updated=Clock(); }
            private void Owner() { if (Thread.CurrentThread.ManagedThreadId!=OwnerThread) throw new InvalidOperationException("storage owner thread required"); }
            internal static void Count(ref long Value) { if (Value!=long.MaxValue) ++Value; }
            internal int PendingCount { get { Owner(); return Pending; } }
            internal int WaiterCount { get { Owner(); return Waiters; } }
            internal int DemandCount { get { Owner(); return Demands.Count; } }
            internal bool HasCompletions { get { Owner(); return Completions.Count!=0 && IntakeRemaining>0; } }
            internal Binding Bind(ulong Vm,ulong Domain,string Package)
            {
                Owner(); if (Vm==0 || Domain==0 || Bindings.Count>=258) throw new InvalidOperationException("storage domain bound");
                if (Package!=null) global::CarbonLuau.Core.AddonPolicy.ValidateId(Package);
                foreach (var Existing in Bindings) if (Existing.Vm==Vm && Existing.Domain==Domain) throw new InvalidOperationException("duplicate storage domain");
                ulong Now=Clock();
                for (int Index=Rotation.Count-1; Index>=0; --Index) {
                    Bucket Empty=Rotation[Index]; double Age=(Now-Empty.Updated)/1000.0;
                    if (Empty.Count==0 && Empty.Bindings==0 && !Empty.MayHaveData && Empty.Requests+Age*20>=32 && Empty.Mutations+Age*5>=8) {
                        Namespaces.Remove(Empty.Namespace); Rotation.RemoveAt(Index);
                        if (Index<Cursor) --Cursor;
                    }
                }
                var Result=new Binding(Host,Vm,Domain,Package); Bucket Bucket;
                if (!Namespaces.TryGetValue(Result.Namespace,out Bucket)) {
                    // Durable namespace buckets remain for the loaded host. Never
                    // reset a namespace's rate allowance during domain replacement.
                    if (Namespaces.Count>=256+258) throw new InvalidOperationException("storage namespace bound");
                    Bucket=new Bucket(Result.Namespace,Clock()); Namespaces.Add(Result.Namespace,Bucket); Rotation.Add(Bucket);
                }
                ++Bucket.Bindings; Bindings.Add(Result); return Result;
            }
            internal void Retire(Binding Binding)
            {
                Owner(); if (!Bindings.Remove(Binding)) return; Binding.Alive=false;
                --Namespaces[Binding.Namespace].Bindings;
                var Remove=new List<string>();
                foreach (var Pair in Demands) if (Pair.Value.Owner==Binding) Remove.Add(Pair.Key);
                foreach (string Key in Remove) Demands.Remove(Key);
                foreach (Request Request in Reservations)
                    if (Request!=null && Request.Owner==Binding && Request.HandedOff) { Release(Request); Count(ref Discarded); }
            }
            internal void RetireVm(ulong Vm)
            { Owner(); for (int Index=Bindings.Count-1; Index>=0; --Index) if (Bindings[Index].Vm==Vm) Retire(Bindings[Index]); }
            internal static void Name(string Value,int Maximum)
            {
                if (Value==null || Value.Length>Maximum || Value.Length==0 || Value=="." || Value=="..") throw new ArgumentException("storage name bound");
                byte[] Bytes=StorageProcess.Utf8.GetBytes(Value); if (Bytes.Length>Maximum) throw new ArgumentException("storage name bound");
                foreach (char C in Value) if (C<32 || C==127 || C=='/' || C=='\\' || C==':') throw new ArgumentException("storage name invalid");
            }
            internal static void ValidateField(string Value)
            {
                if (String.IsNullOrEmpty(Value) || Value.IndexOf('\0')>=0) throw new ArgumentException("derived field invalid");
                byte[] Bytes=StorageProcess.Utf8.GetBytes(Value);
                if (Bytes.Length>64) throw new ArgumentException("derived field bound");
            }
            private static string DemandKey(Binding Binding,string Store,string Field)
            { return Binding.Namespace+"\0"+Store+"\0"+Field; }
            private void QueueProactive(string Key)
            {
                if (!QueuedProactiveDemandKeys.Add(Key)) return;
                // Retired generations may leave inert keys in this FIFO. Keep
                // its retained memory bounded even under repeated replacement.
                if (ProactiveDemandKeys.Count>=132096) {
                    var Retained=new Queue<string>(Demands.Count);
                    QueuedProactiveDemandKeys.Clear();
                    while (ProactiveDemandKeys.Count!=0) {
                        string Existing=ProactiveDemandKeys.Dequeue(); DemandIntent Intent;
                        if (Demands.TryGetValue(Existing,out Intent) && Intent.Proactive && Intent.Owner.Alive &&
                            QueuedProactiveDemandKeys.Add(Existing)) Retained.Enqueue(Existing);
                    }
                    while (Retained.Count!=0) ProactiveDemandKeys.Enqueue(Retained.Dequeue());
                    if (!QueuedProactiveDemandKeys.Add(Key)) return;
                }
                ProactiveDemandKeys.Enqueue(Key);
            }
            internal void WorkerStarted()
            {
                Owner(); ++WorkerEpoch;
                ProactiveDemandKeys.Clear(); QueuedProactiveDemandKeys.Clear();
                foreach (var Pair in Demands) {
                    DemandIntent Intent=Pair.Value;
                    Intent.State=Preparation.Unknown; Intent.AttemptedEpoch=0; Intent.Due=0;
                    if (Intent.Proactive) QueueProactive(Pair.Key);
                }
            }
            internal void Hint(Binding Binding,string Store,string Field)
            {
                Owner(); if (Binding==null || !Binding.Alive || Binding.Host!=Host || !Bindings.Contains(Binding)) throw new Rejection(5);
                Name(Store,64); ValidateField(Field);
                string Key=DemandKey(Binding,Store,Field); DemandIntent Existing;
                if (Demands.TryGetValue(Key,out Existing)) {
                    if (Existing.Owner!=Binding) { Existing.State=Preparation.Unknown; Existing.AttemptedEpoch=0; Existing.Due=0; }
                    if (!Existing.Proactive) QueueProactive(Key);
                    Existing.Owner=Binding; Existing.Proactive=true; return;
                }
                if (Demands.Count>=132096) throw new Rejection(3);
                Demands.Add(Key,new DemandIntent { Owner=Binding,Store=Store,Field=Field,Proactive=true });
                QueueProactive(Key);
            }
            internal Request Submit(Binding Binding,ulong AdmittedVm,ulong AdmittedDomain,bool CommittedPublication,
                Operation Op,string Store,string Key,byte[] Envelope,ulong Route)
            {
                Owner();
                // CommittedPublication is supplied only by the native authority
                // boundary in 1B, using CanMutateHost; never a script argument.
                if (!Ready) throw new Rejection(2);
                if (Binding==null || !Binding.Alive || Binding.Host!=Host || !Bindings.Contains(Binding) ||
                    Binding.Vm!=AdmittedVm || Binding.Domain!=AdmittedDomain || !CommittedPublication || Route==0)
                    throw new Rejection(5);
                if (Op<Operation.Get || Op>Operation.Remove) throw new ArgumentException("storage operation");
                Name(Store,64); Name(Key,128);
                if (Envelope==null || Envelope.Length>65536 || (Op==Operation.Set ? Envelope.Length<45 : Envelope.Length!=0)) throw new ArgumentException("storage envelope bound");
                Bucket Bucket=Namespaces[Binding.Namespace]; ulong Now=Clock();
                if (Bucket.Count>=8 || Pending>=128) { Count(ref QueueRejected); throw new Rejection(3); }
                int Reservation=-1;
                for (int Index=0; Index<Reservations.Length; ++Index) {
                    Request Existing=Reservations[Index];
                    if (Existing==null) { if (Reservation<0) Reservation=Index; }
                    else if (Existing.Owner==Binding && Existing.Route==Route) throw new Rejection(5);
                }
                if (Reservation<0) throw new Rejection(3);
                double Elapsed=(Now-Updated)/1000.0; Requests=Math.Min(256,Requests+Elapsed*200); Mutations=Math.Min(64,Mutations+Elapsed*50); Updated=Now;
                Elapsed=(Now-Bucket.Updated)/1000.0; Bucket.Requests=Math.Min(32,Bucket.Requests+Elapsed*20); Bucket.Mutations=Math.Min(8,Bucket.Mutations+Elapsed*5); Bucket.Updated=Now;
                bool Mutation=Op!=Operation.Get;
                if (Requests<1 || Bucket.Requests<1 || (Mutation && (Mutations<1 || Bucket.Mutations<1))) { Count(ref RateRejected); throw new Rejection(4); }
                var Result=new Request { Owner=Binding,Id=checked(NextId+1),Route=Route,Op=Op,End=checked(Now+5000),Reservation=Reservation };
                using (var Buffer=new MemoryStream()) using (var Writer=new BinaryWriter(Buffer)) {
                    Writer.Write(new byte[] {67,76,80,81}); Writer.Write(1u); Writer.Write((uint)Op); Writer.Write(0ul);
                    Writer.Write(Host); Writer.Write(Binding.Vm); Writer.Write(Binding.Domain); Writer.Write(Route); Writer.Write(Result.End);
                    Writer.Write(Binding.Package==null ? 0u : 1u);
                    foreach (string Value in new[] {Binding.Package??"",Store,Key}) { byte[] Bytes=StorageProcess.Utf8.GetBytes(Value); Writer.Write((uint)Bytes.Length); Writer.Write(Bytes); }
                    Writer.Write((uint)Envelope.Length); Writer.Write(Envelope); Result.Frame=Buffer.ToArray();
                }
                if (Result.Frame.Length>StorageProcess.MaximumFrame-4) throw new ArgumentException("storage frame bound");
                // Pre-reserve the full result capacity before acceptance.
                Result.Envelope=new byte[65536];
                // The namespace queue and global ledger have fixed reserved capacity.
                // Nothing after this enqueue may allocate or fail before acceptance.
                Bucket.Pending.Enqueue(Result); Reservations[Reservation]=Result;
                NextId=Result.Id; --Requests; --Bucket.Requests; if (Mutation) { --Mutations; --Bucket.Mutations; }
                ++Pending; ++Bucket.Count; return Result;
            }
            // Private 2B seam for a future accepted Query. It owns one ordinary
            // D21 reservation and deadline; no public Luau method reaches it yet.
            internal Request SubmitAwait(Binding Binding,string Store,string Field,ulong Route,bool AssumedReady=false)
            {
                Owner(); if (Binding==null || !Binding.Alive || !Bindings.Contains(Binding)) throw new Rejection(5);
                Name(Store,64); ValidateField(Field);
                Bucket Bucket=Namespaces[Binding.Namespace];
                if (!AssumedReady && (Waiters>=32 || Bucket.Waiters>=8)) throw new Rejection(3);
                string Key=DemandKey(Binding,Store,Field); DemandIntent Intent;
                bool Created=!Demands.TryGetValue(Key,out Intent);
                if (Created) {
                    if (Demands.Count>=132096) throw new Rejection(3);
                    Intent=new DemandIntent {Owner=Binding,Store=Store,Field=Field};
                    Demands.Add(Key,Intent);
                }
                try {
                    // Submit owns the canonical D21 reservation/rate/deadline.
                    // Its wire key is discarded: a field is an exact top-level
                    // name and may contain punctuation prohibited in data keys.
                    Request Result=Submit(Binding,Binding.Vm,Binding.Domain,true,Operation.Get,Store,"_await",new byte[0],Route);
                    Result.Op=Operation.Await; Result.Store=Store; Result.Field=Field; Result.Waiting=!AssumedReady;
                    Result.Preparation=AssumedReady ? Preparation.Ready : Preparation.Unknown;
                    Result.Frame=null;
                    ++Intent.Users;
                    if (Result.Waiting) { ++Bucket.Waiters; ++Waiters; }
                    return Result;
                } catch { if (Created) Demands.Remove(Key); throw; }
            }
            private void EndWaiting(Request Request,Bucket Bucket)
            {
                if (!Request.Waiting) return;
                Request.Waiting=false; --Bucket.Waiters; --Waiters;
            }
            private void FinishAwait(Request Request,Bucket Bucket,AwaitOutcome Outcome)
            {
                EndWaiting(Request,Bucket); Bucket.Pending.Dequeue();
                Request.AwaitResult=Outcome; Request.State=2; Completions.Enqueue(Request);
            }
            private Request NextDemand(ulong Now,DemandIntent Waiting)
            {
                DemandIntent Selected=Waiting;
                if (Selected==null) for (int Visited=0; Visited<32 && ProactiveDemandKeys.Count!=0; ++Visited) {
                    string Key=ProactiveDemandKeys.Dequeue(); QueuedProactiveDemandKeys.Remove(Key); DemandIntent Intent;
                    if (!Demands.TryGetValue(Key,out Intent) || !Intent.Proactive || !Intent.Owner.Alive ||
                        Intent.AttemptedEpoch==WorkerEpoch || Intent.State==Preparation.Ready || Intent.State==Preparation.Unavailable) continue;
                    if (Intent.Due>Now) { ProactiveDemandKeys.Enqueue(Key); QueuedProactiveDemandKeys.Add(Key); continue; }
                    Selected=Intent; break;
                }
                if (Selected==null) return null;
                Selected.AttemptedEpoch=WorkerEpoch; Selected.Due=checked(Now+50);
                var Result=new Request {Owner=Selected.Owner,Id=checked(++NextId),Route=checked(++NextInternalRoute),
                    Op=Operation.Demand,InternalDemand=true,Store=Selected.Store,Field=Selected.Field,
                    End=checked(Now+5000),Envelope=new byte[0]};
                using (var Buffer=new MemoryStream()) using (var Writer=new BinaryWriter(Buffer)) {
                    Writer.Write(new byte[] {67,76,80,81}); Writer.Write(1u); Writer.Write((uint)Result.Op); Writer.Write(0ul);
                    Writer.Write(Host); Writer.Write(Result.Owner.Vm); Writer.Write(Result.Owner.Domain); Writer.Write(Result.Route); Writer.Write(Result.End);
                    Writer.Write(Result.Owner.Package==null ? 0u : 1u);
                    foreach (string Value in new[] {Result.Owner.Package??"",Result.Store,Result.Field}) {
                        byte[] Bytes=StorageProcess.Utf8.GetBytes(Value); Writer.Write((uint)Bytes.Length); Writer.Write(Bytes);
                    }
                    Writer.Write(0u); Result.Frame=Buffer.ToArray();
                }
                return Result;
            }
            private Request DispatchDemand(ulong Now,DemandIntent Waiting=null)
            {
                Request Result=NextDemand(Now,Waiting);
                if (Result==null) return null;
                Result.WireId=++NextWireId;
                for (int Index=0; Index<8; ++Index) Result.Frame[12+Index]=(byte)(Result.WireId>>(Index*8));
                Active=Result; Volatile.Write(ref Result.State,1); return Result;
            }
            internal Request Dispatch()
            {
                Owner(); IntakeRemaining=8; int Bookkeeping=8;
                if (Active!=null) {
                    if (Volatile.Read(ref Active.State)!=2) return null;
                    if (Active.InternalDemand) {
                        DemandIntent Intent;
                        if (Demands.TryGetValue(DemandKey(Active.Owner,Active.Store,Active.Field),out Intent))
                            Intent.State=Active.Error==Error.None ? Active.Preparation : Preparation.Unavailable;
                        Active=null;
                    } else {
                    // Keep the pre-dispatch presence baseline on definite failure:
                    // failure neither creates data nor proves existing data absent.
                    if (Active.Error==Error.None) Namespaces[Active.Owner.Namespace].MayHaveData=Active.NamespacePresent;
                    else if (Active.Error==Error.Indeterminate && Active.Op!=Operation.Get)
                        Namespaces[Active.Owner.Namespace].MayHaveData=true;
                    Completions.Enqueue(Active); Active=null;
                    }
                }
                if (Pending==0) return Ready ? DispatchDemand(Clock()) : null;
                ulong Now=Clock();
                DemandIntent WaitingDemand=null;
                int ScanStart=Cursor;
                // Scan at most the fixed namespace bound. One namespace gets one
                // turn, then the persistent cursor advances to the next.
                for (int Visited=0; Visited<Rotation.Count; ++Visited) {
                    if (Cursor>=Rotation.Count) Cursor=0; Bucket Bucket=Rotation[Cursor++];
                    while (Bucket.Pending.Count!=0) {
                        Request Next=Bucket.Pending.Peek();
                        if (!Next.Owner.Alive || Next.CallbackDiscarded || Now>=Next.End) {
                            if (Bookkeeping--==0) return null;
                            Bucket.Pending.Dequeue();
                            EndWaiting(Next,Bucket);
                            if (!Next.Owner.Alive || Next.CallbackDiscarded) { Release(Next); Count(ref Discarded); }
                            else { if (Next.Op==Operation.Await) Next.AwaitResult=AwaitOutcome.IndexPreparing;
                                else Next.Error=Error.DeadlineExceeded; Next.State=2; Completions.Enqueue(Next); }
                            continue;
                        }
                        if (!Ready) return null;
                        if (Next.Op==Operation.Await) {
                            DemandIntent Intent;
                            Preparation State=Demands.TryGetValue(DemandKey(Next.Owner,Next.Store,Next.Field),out Intent)
                                ? Intent.State : Preparation.Unavailable;
                            if (State==Preparation.Ready) { FinishAwait(Next,Bucket,AwaitOutcome.Ready); continue; }
                            if (State==Preparation.Unavailable) { FinishAwait(Next,Bucket,AwaitOutcome.QueryUnavailable); continue; }
                            if (!Next.Waiting) {
                                if (Waiters>=32 || Bucket.Waiters>=8) { FinishAwait(Next,Bucket,AwaitOutcome.QueryUnavailable); continue; }
                                Next.Waiting=true; ++Waiters; ++Bucket.Waiters;
                            }
                            if (WaitingDemand==null && Intent!=null && Intent.Due<=Now) WaitingDemand=Intent;
                            break; // preserve this namespace's FIFO head
                        }
                        // Count retains this bucket through dispatch and delivery;
                        // only the result can change its durable-presence baseline.
                        // Fair namespace rotation may reorder admission IDs. The
                        // worker's strictly increasing nonce follows dispatch,
                        // not global submission order. Each accepted request is
                        // dispatched at most once, so NextWireId <= NextId and
                        // admission's checked ID bound also prevents overflow.
                        Next.WireId=++NextWireId;
                        for (int Index=0; Index<8; ++Index) Next.Frame[12+Index]=(byte)(Next.WireId>>(Index*8));
                        Bucket.Pending.Dequeue(); Active=Next; Volatile.Write(ref Next.State,1); return Next;
                    }
                }
                // A full scan otherwise returns Cursor to its starting point.
                // Rotate the next first waiter so one preparing field cannot
                // monopolize private demand polls across namespaces.
                if (Rotation.Count!=0) Cursor=(ScanStart+1)%Rotation.Count;
                return DispatchDemand(Now,WaitingDemand);
            }
            internal void BeginTick() { Owner(); IntakeRemaining=8; }
            internal Request TakeCompletion()
            {
                Owner();
                while (Completions.Count!=0 && IntakeRemaining>0) {
                    --IntakeRemaining;
                    Request Value=Completions.Dequeue();
                    if (!Value.Owner.Alive || Value.CallbackDiscarded) { Release(Value); Count(ref Discarded); continue; }
                    if (Value.Op==Operation.Await) return Value;
                    if (Value.Error==Error.None) Count(ref Completed[(int)Value.Op-1]);
                    else if (Value.Error==Error.QuotaExceeded) Count(ref QuotaRejected);
                    else if (Value.Error==Error.DeadlineExceeded) Count(ref Expired);
                    else Count(ref BackendFailures);
                    return Value; // reservation retained until explicit delivery/discard
                }
                return null;
            }
            internal void Release(Request Request)
            {
                Owner(); if (Request.Released) return;
                Request.Released=true;
                if (Request.Op==Operation.Await) {
                    DemandIntent Intent;
                    string Key=DemandKey(Request.Owner,Request.Store,Request.Field);
                    if (Demands.TryGetValue(Key,out Intent) && Intent.Owner==Request.Owner && Intent.Users>0) {
                        --Intent.Users;
                        if (Intent.Users==0 && !Intent.Proactive) Demands.Remove(Key);
                    }
                }
                if (Request.Reservation>=0 && Reservations[Request.Reservation]==Request) Reservations[Request.Reservation]=null;
                --Pending; --Namespaces[Request.Owner.Namespace].Count; Request.Frame=null; Request.Envelope=null;
            }
            internal void Handoff(Request Request)
            {
                Owner(); if (Request.Released) return;
                Request.HandedOff=true;
                // Native now owns the result bytes. Keep only the counted route.
                Request.Frame=null; Request.Envelope=null;
            }
            internal void ReleaseCallback(Binding Binding,ulong Route)
            {
                Owner();
                foreach (Request Request in Reservations) if (Request!=null && Request.Owner==Binding && Request.Route==Route) {
                    Request.CallbackDiscarded=true;
                    if (Request.HandedOff) Release(Request);
                    // An undispatched/in-flight record remains queue/supervisor-owned
                    // until it is dropped/settled; never clear its worker buffers here.
                    return;
                }
            }
            internal void RetireAll()
            {
                Owner(); Ready=false;
                foreach (var Binding in Bindings) Binding.Alive=false;
                Bindings.Clear();
                foreach (var Bucket in Rotation) {
                    Bucket.Bindings=0;
                    while (Bucket.Pending.Count!=0) {
                        Request Request=Bucket.Pending.Dequeue(); EndWaiting(Request,Bucket);
                        Release(Request); Count(ref Discarded);
                    }
                }
                while (Completions.Count!=0) { Release(Completions.Dequeue()); Count(ref Discarded); }
                foreach (Request Request in Reservations)
                    if (Request!=null && Request.HandedOff) { Release(Request); Count(ref Discarded); }
                Demands.Clear(); ProactiveDemandKeys.Clear(); QueuedProactiveDemandKeys.Clear();
                // The sole in-flight reservation remains supervisor-owned until
                // it publishes a terminal response or detached teardown ends.
            }
            internal static void Complete(Request Request,byte[] Reply,ulong Now)
            {
                // Supervisor thread touches only its one exclusively handed-off slot.
                if (Volatile.Read(ref Request.State)!=1) throw new IOException("storage response already completed");
                if (Now>=Request.End) throw new TimeoutException("storage completion deadline");
                if (Reply.Length<60 || System.Text.Encoding.ASCII.GetString(Reply,0,4)!="CLPS" || StorageProcess.U32(Reply,4)!=1 ||
                    StorageProcess.U32(Reply,8)>10 || StorageProcess.U64(Reply,12)!=Request.WireId || StorageProcess.U64(Reply,20)!=Request.Owner.Host ||
                    StorageProcess.U64(Reply,28)!=Request.Owner.Vm || StorageProcess.U64(Reply,36)!=Request.Owner.Domain || StorageProcess.U64(Reply,44)!=Request.Route)
                    throw new IOException("storage response identity mismatch");
                uint Flags=StorageProcess.U32(Reply,52), Found=Flags&1, Size=StorageProcess.U32(Reply,56), Code=StorageProcess.U32(Reply,8);
                if (Request.InternalDemand) {
                    if (Request.Op!=Operation.Demand || Reply.Length!=60 || Size!=0 || (Code==0 ? Flags>2 : Flags!=0))
                        throw new IOException("storage demand response shape mismatch");
                    Request.Preparation=Code==0 ? (Flags==1 ? Preparation.Ready : Flags==2 ? Preparation.Unavailable : Preparation.Preparing)
                        : Preparation.Unavailable;
                    Request.Error=(Error)Code; Volatile.Write(ref Request.State,2); return;
                }
                if (Flags>3 || Size>65536 || Reply.Length!=60+Size || (Code!=0 && (Flags!=0 || Size!=0)) ||
                    (Request.Op!=Operation.Get && Size!=0) || (Code==0 && Request.Op==Operation.Set && Flags!=3) ||
                    (Request.Op==Operation.Get && Code==0 && (((Found==0)!=(Size==0)) || (Found!=0 && ((Flags&2)==0 || Size<45)))))
                    throw new IOException("storage response shape mismatch");
                Buffer.BlockCopy(Reply,60,Request.Envelope,0,(int)Size);
                Array.Resize(ref Request.Envelope,(int)Size); Request.Found=Found!=0; Request.NamespacePresent=(Flags&2)!=0; Request.Error=(Error)Code;
                Volatile.Write(ref Request.State,2);
            }
            internal static void Fail(Request Request)
            { Request.Error=Request.Op==Operation.Get || !Request.Sent ? Error.StorageUnavailable : Error.Indeterminate; Volatile.Write(ref Request.State,2); }
        }
    }
}
