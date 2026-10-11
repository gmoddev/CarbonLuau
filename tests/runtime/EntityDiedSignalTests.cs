using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;
using Model = Carbon.Plugins.EntityLifetimeModel;

// Real compiler/VM, scheduler, publication and D20/D11 models with deliberately
// synthetic completion certificates. These fixtures do not qualify Rust Die,
// its terminal marker, effective virtual coverage or attribution provenance.
internal static class EntityDiedSignalTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private sealed class Proof
    {
        internal readonly Model.MembershipCandidate EpochRoot;
        internal bool Completed = true, Alive = true, Throws;
        internal int Checks;
        internal Proof(Model.MembershipCandidate EpochRoot = null) { this.EpochRoot = EpochRoot; }
        internal bool Validate()
        { Checks++; if (Throws) throw new InvalidOperationException("synthetic completion failure"); return Completed; }
    }
    private sealed class Delivery
    {
        internal Runtime.FacadeSession Owner;
        internal string[] Fields;
        internal byte[] Payload;
        internal ulong Nonce;
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime.FacadeWorld World;
        internal readonly List<Runtime.FacadeSession> Owners = new List<Runtime.FacadeSession>();
        private readonly List<Delivery> Taken = new List<Delivery>();
        internal long Frame, NextDomain = 1;
        internal bool Ready = true;
        internal int PlayerLookups;
        internal Runtime.GameplayEventBudget Budget { get { return World.GameplayEvents; } }
        internal Fixture()
        {
            // No Entity adapter, and a Player resolver that returns no live
            // player: snapshot admission must never require either lookup.
            World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => { PlayerLookups++; return null; }), new Registrar());
            Budget.FrameClock = () => Frame;
            World.GameplayAvailable = Kind => Kind != "entitydied" || Ready;
        }
        internal Runtime.FacadeSession Candidate(int Listeners = 0, int Capacity = 256)
        {
            var Owner = new Runtime.FacadeSession(World, 10, NextDomain++, Capacity); Owners.Add(Owner);
            for (int Index = 0; Index < Listeners; Index++) Operation(Owner, 6, "entitydied");
            return Owner;
        }
        internal Runtime.FacadeSession Add(int Listeners = 1, bool Root = false, int Capacity = 256)
        {
            var Owner = Candidate(Listeners, Capacity);
            if (Root) World.Commit(Owner); else World.CommitAddon(null, Owner);
            return Owner;
        }
        internal void Emit(Runtime.GameplayEntityDiedObservation Observation = null)
        {
            Check(Budget.Capture("entitydied"), "explicit shared producer capture");
            World.EntityDied(Observation ?? Snapshot());
        }
        internal List<Delivery> Take(Runtime.FacadeSession Owner, bool Transfer = true)
        {
            var Result = new List<Delivery>();
            object Queue = typeof(Runtime.FacadeSession).GetField("Pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Owner);
            MethodInfo Dequeue = Queue.GetType().GetMethod("Dequeue");
            while (Owner.PendingCount != 0) {
                object Item = Dequeue.Invoke(Queue, null);
                byte[] Payload = (byte[])Item.GetType().GetField("Payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Item);
                string[] Fields = Runtime.FacadePolicy.Unpack(Payload);
                var Value = new Delivery { Owner = Owner, Fields = Fields, Payload = Payload,
                    Nonce = UInt64.Parse(Fields[5], CultureInfo.InvariantCulture) };
                if (Transfer) Check(Budget.ToNative(Owner, Value.Nonce), "modeled native transfer");
                Result.Add(Value); Taken.Add(Value);
            }
            return Result;
        }
        public void Dispose()
        {
            foreach (var Owner in Owners) World.Retire(Owner);
            foreach (var Item in Taken) Budget.Release(Item.Owner, Item.Nonce);
            Check(Budget.PendingCount == 0 && Budget.RetainedBytes == 0, "exact modeled teardown refunds");
            Check(PlayerLookups == 0, "historical Entity snapshots never resolve a Player");
        }
    }
    private static void Check(bool Value, string Message)
    { if (!Value) throw new Exception("EntityDied: " + Message); }
    private static string[] Operation(Runtime.FacadeSession Owner, uint Code, params string[] Fields)
    {
        try { return (string[])typeof(Runtime.FacadeSession).GetMethod("Operation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Owner, new object[] { Code, Fields }); }
        catch (TargetInvocationException Error) { throw Error.InnerException; }
    }
    private static bool Host(Runtime.FacadeSession Owner, uint Code, string[] Fields, long? ExpectedDomain = null)
    {
        byte[] Bytes = Runtime.FacadePolicy.Pack(Fields);
        IntPtr Input = Marshal.AllocHGlobal(Math.Max(1, Bytes.Length)), Output = Marshal.AllocHGlobal(262144);
        try {
            if (Bytes.Length != 0) Marshal.Copy(Bytes, 0, Input, Bytes.Length);
            uint Written;
            return Owner.Callback((ulong)(ExpectedDomain ?? Owner.DomainLifetimeId), Code, Input, (uint)Bytes.Length,
                Output, 262144, out Written) == 0;
        } finally { Marshal.FreeHGlobal(Input); Marshal.FreeHGlobal(Output); }
    }
    private static void Reject(Action Action, string Message)
    {
        bool Rejected = false;
        try { Action(); } catch (Runtime.FacadeException) { Rejected = true; }
        catch (EncoderFallbackException) { Rejected = true; }
        Check(Rejected, Message);
    }
    private static Runtime.GameplayEntityDiedObservation Snapshot(Proof Proof = null, string Id = "50", ulong Epoch = 50,
        bool Position = true, bool Attribution = true)
    {
        Proof = Proof ?? new Proof();
        return new Runtime.GameplayEntityDiedObservation(Id, "assets/fixture.prefab", Epoch,
            Position ? (Runtime.PlayerPosition?)new Runtime.PlayerPosition(1, 2, 3) : null, Proof.Validate,
            Attribution ? "7" : null, Attribution ? "00076561190000999889" : null, Attribution ? "Captured killer" : null,
            Attribution ? "Bullet" : null, Attribution ? "assets/weapon.prefab" : null);
    }
    internal static void Run()
    {
        ObservationValidation(); SnapshotAdmission(); SameBirthEpisodes(); Publication(); RetentionAndCancellation(); PositionAttempts(); FrameLimits(); Fairness();
        Console.WriteLine("[CarbonLuau:EntityDiedModel] PASS synthetic certificate, exact fourteen-field transport, publication, retention and shared fairness; no Rust/VM dispatch qualification");
    }
    private static void ObservationValidation()
    {
        foreach (string Id in new[] { null, "", "0", "01", "+1", "-1", "1.0", "18446744073709551616" })
            Reject(() => Snapshot(Id: Id), "canonical Entity ID required");
        Reject(() => Snapshot(Epoch: 0), "nonzero source certificate identity required");
        Reject(() => new Runtime.GameplayEntityDiedObservation("1", "assets/fixture.prefab", 1, null, null), "completion predicate required");
        foreach (string Prefab in new[] { null, "", new string('x', 513), new string('\u00e9', 257), "nul\0prefab", "\ud800" })
            Reject(() => new Runtime.GameplayEntityDiedObservation("1", Prefab, 1, null, () => true), "bounded well-formed prefab required");
        foreach (float Invalid in new[] { Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity }) {
            foreach (var Position in new[] { new Runtime.PlayerPosition(Invalid, 0, 0), new Runtime.PlayerPosition(0, Invalid, 0), new Runtime.PlayerPosition(0, 0, Invalid) })
                Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, Position, () => true), "finite optional coordinates required");
        }
        foreach (string Token in new[] { "", "0", "01", "+7", "18446744073709551616" })
            Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, Token, "123", "killer"), "canonical D11 killer token required");
        foreach (string Id in new[] { null, "", "invalid", new string('1', 21) })
            Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, "7", Id, "killer"), "legacy bounded digit-only UserId required");
        foreach (string Name in new[] { null, new string('x', 129), new string('\u00e9', 65), "killer\0", "\ud800" })
            Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, "7", "123", Name), "bounded complete killer snapshot required");
        Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, null, "123"), "partial killer Id rejected");
        Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, KillerName: "killer"), "partial killer name rejected");
        foreach (string Label in new[] { new string('x', 33), new string('\u00e9', 17), "Bullet\0", "\ud800" })
            Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, DamageType: Label), "bounded damage label required");
        foreach (string Weapon in new[] { new string('x', 513), new string('\u00e9', 257), "weapon\0", "\ud800" })
            Reject(() => new Runtime.GameplayEntityDiedObservation("1", "p", 1, null, () => true, Weapon: Weapon), "bounded weapon snapshot required");
        var Boundary = new Runtime.GameplayEntityDiedObservation("18446744073709551615", new string('\u00e9', 256), UInt64.MaxValue,
            new Runtime.PlayerPosition(Single.MaxValue, -Single.MaxValue, -0.0f), () => true, "18446744073709551615", "000", new string('\u00e9', 64),
            new string('\u00e9', 16), new string('\u00e9', 256));
        Check(Boundary.KillerId == "000" && Boundary.Z == "0" &&
            Double.Parse(Boundary.X, CultureInfo.InvariantCulture) == (double)Single.MaxValue,
            "legacy UserId grammar and exact binary32-as-binary64 coordinates preserved");
        foreach (var Field in typeof(Runtime.GameplayEntityDiedObservation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            Check(Field.IsInitOnly, "all captured observation fields are immutable");
    }
    private static void SnapshotAdmission()
    {
        using (var Value = new Fixture()) {
            var Owner = Value.Add(Root: true); var Proof = new Proof();
            var Observation = Snapshot(Proof); Value.Emit(Observation);
            Check(Proof.Checks == 0, "capture/fanout never invokes fresh-callback proof");
            var Item = Value.Take(Owner, false)[0];
            Check(Item.Fields.Length == 14 && Item.Fields[0] == "entitydied" && Item.Fields[2] == "50" && Item.Fields[3] == "50" &&
                Item.Fields[4] == Observation.Prefab && Item.Fields[6] == "1" && Item.Fields[7] == "2" && Item.Fields[8] == "3" &&
                Item.Fields[9] == "7" && Item.Fields[10] == Observation.KillerId && Item.Fields[11] == Observation.KillerName &&
                Item.Fields[12] == "Bullet" && Item.Fields[13] == Observation.Weapon, "fixed typed private snapshot layout");
            Check(!Host(Owner, 9, Item.Fields), "managed pending work cannot enter before native transfer");
            Check(Value.Budget.ToNative(Owner, Item.Nonce) && !Value.Budget.ToNative(Owner, Item.Nonce), "one native transfer only");
            Proof.Alive = false;
            Check(Host(Owner, 9, Item.Fields) && Proof.Checks == 1, "qualified historical completion survives original retirement");
            foreach (int Index in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }) {
                var Tampered = (string[])Item.Fields.Clone(); Tampered[Index] = "tampered";
                Check(!Host(Owner, 9, Tampered) && Proof.Checks == 1, "all snapshot/routing fields match before completion validation");
            }
            foreach (int Count in new[] { 12, 13, 15 }) {
                var WrongShape = new string[Count]; Array.Copy(Item.Fields, WrongShape, Math.Min(Count, Item.Fields.Length));
                if (Count > 14) WrongShape[14] = "";
                Check(!Host(Owner, 9, WrongShape), "entitydied alone requires exactly fourteen fields");
            }
            Check(!Value.Budget.Validate(Owner, null) && !Value.Budget.Validate(Owner, new string[0]), "empty validation input fails closed");
            Proof.Completed = false; Check(!Host(Owner, 9, Item.Fields), "unproven completion rejected");
            Proof.Completed = true; Proof.Throws = true; Check(!Host(Owner, 9, Item.Fields), "throwing certificate contained");
            Proof.Throws = false; Value.Ready = false;
            int Before = Proof.Checks; Check(!Host(Owner, 9, Item.Fields) && Proof.Checks == Before, "source drift suppresses queued admission before proof");
            Reject(() => Operation(Owner, 6, "entitydied"), "unavailable internal registration rejected");
            Value.Ready = true;
            Check(Host(Owner, 39, new[] { Item.Fields[5] }) && !Host(Owner, 39, new[] { Item.Fields[5] }), "exact private acknowledgement once");
            Value.Frame++; Value.Emit(Snapshot(Position: false, Attribution: false)); Item = Value.Take(Owner)[0];
            for (int Index = 6; Index < 14; Index++) Check(Item.Fields[Index] == "", "absent optional fields have empty private slots");
            Check(Host(Owner, 9, Item.Fields), "nil optional values do not invent attribution or position");
            Value.Frame++; Value.Emit(Snapshot(Id: "50", Epoch: 51));
            var Reused = Value.Take(Owner)[0];
            Check(Host(Owner, 9, Reused.Fields) && Item.Fields[2] != Reused.Fields[2] && Item.Fields[3] == Reused.Fields[3],
                "same network Id never substitutes a different original certificate");
            byte[] Payload; var MissingProof = (string[])Item.Fields.Clone();
            Check(Value.Budget.Reserve(Owner, "entitydied", MissingProof[1], MissingProof, out Payload) == 0 && Payload == null,
                "new kind cannot reserve without its typed observation");
            var Mismatch = (string[])Item.Fields.Clone(); Mismatch[12] = "forged";
            Check(Value.Budget.Reserve(Owner, "entitydied", Mismatch[1], Mismatch, out Payload, Died: Snapshot(Position: false, Attribution: false)) == 0,
                "typed reservation rejects mismatched snapshot before packing");
            Check(Value.Budget.Reserve(Owner, "entitydestroyed", Item.Fields[1], Item.Fields, out Payload,
                Died: Snapshot(Position: false, Attribution: false)) == 0, "typed death observation belongs exclusively to the new kind");
            var Oversized = (string[])Item.Fields.Clone(); Oversized[1] = new string('x', 2049);
            int Held = Value.Budget.PendingCount, Charge = Value.Budget.RetainedBytes;
            Check(Value.Budget.Reserve(Owner, "entitydied", Oversized[1], Oversized, out Payload,
                Died: Snapshot(Position: false, Attribution: false)) == 0 && Payload == null &&
                Value.Budget.PendingCount == Held && Value.Budget.RetainedBytes == Charge,
                "existing payload ceiling rejects oversized private transport without holding a reservation");
        }
    }
    // Two synthetic full death certificates, separated by an alive reset, belong
    // to one completed-Spawn witness. No Rust/NPC reset qualification is implied.
    private sealed class SameBirthSource : IDisposable
    {
        internal readonly EntityProbe Entities = new EntityProbe();
        internal readonly EntitySpawnSignalTests.ObjectState Original = new EntitySpawnSignalTests.ObjectState {
            Id = 61, Prefab = "assets/original.prefab"
        };
        internal readonly Model.MembershipCandidate EpochRoot;
        internal readonly Proof FirstProof, SecondProof;
        internal readonly Runtime.GameplayEntityDiedObservation First, Second;
        internal SameBirthSource()
        {
            Check(Entities.Lifetimes.CompleteSpawn(Entities.Lifetimes.BeginSpawn(Original), true, true), "same-birth completed original Spawn");
            EpochRoot = Entities.Lifetimes.CaptureCompletedWitness(Original);
            Check(EpochRoot != null, "same-birth original completed witness");
            FirstProof = new Proof(EpochRoot) { Completed = false };
            SecondProof = new Proof(EpochRoot) { Completed = false };
            var Position = new Runtime.PlayerPosition(1, 2, 3); // Capture once before either delivery.
            First = new Runtime.GameplayEntityDiedObservation("61", Original.Prefab, EpochRoot.Birth, Position,
                () => FirstProof.Validate() && Entities.Lifetimes.IsHistoricalObservationCurrent(FirstProof.EpochRoot));
            Second = new Runtime.GameplayEntityDiedObservation("61", Original.Prefab, EpochRoot.Birth, Position,
                () => SecondProof.Validate() && Entities.Lifetimes.IsHistoricalObservationCurrent(SecondProof.EpochRoot));
        }
        internal void Stage(Action<Runtime.GameplayEntityDiedObservation> Emit)
        {
            Original.Alive = false; FirstProof.Completed = true; Emit(First);
            Original.Alive = true; // Synthetic qualified episode reset, not a new Spawn.
            Check(Entities.Lifetimes.IsWitnessEpochUnchanged(EpochRoot) &&
                ReferenceEquals(EpochRoot, Entities.Lifetimes.CaptureCompletedWitness(Original)), "alive reset preserves exact original epoch/birth");
            Original.Alive = false; SecondProof.Completed = true; Emit(Second);
            Check(FirstProof.Checks == 0 && SecondProof.Checks == 0 && !ReferenceEquals(FirstProof, SecondProof),
                "independent completion certificates remain unevaluated before delivery");
        }
        internal void Supersede()
        {
            Original.Alive = true; Original.Prefab = "assets/replacement.prefab";
            Check(Entities.Lifetimes.CompleteSpawn(Entities.Lifetimes.BeginSpawn(Original), true, true), "same-object later Spawn completes");
            var Replacement = Entities.Lifetimes.CaptureCompletedWitness(Original);
            Check(Replacement != null && Replacement.Birth != EpochRoot.Birth && Replacement.Epoch != EpochRoot.Epoch &&
                !Entities.Lifetimes.IsWitnessEpochUnchanged(EpochRoot) && Entities.Lifetimes.IsHistoricalObservationCurrent(EpochRoot),
                "new incarnation supersedes live epoch without retargeting historical certificates");
        }
        public void Dispose()
        { Check(Entities.Lookups == 0 && Entities.Reads == 0, "same-birth historical certificates never resolve/read live Entity"); Entities.Dispose(); }
    }
    private static void CheckSameBirthNonces(List<string[]> Fields, SameBirthSource Source)
    {
        Check(Fields.Count == 2 && Fields[0][5] != Fields[1][5] &&
            Fields[0][2] == Source.EpochRoot.Birth.ToString(CultureInfo.InvariantCulture) && Fields[1][2] == Fields[0][2] &&
            Fields[0][3] == "61" && Fields[1][3] == "61" && Fields[0][4] == "assets/original.prefab" && Fields[1][4] == Fields[0][4],
            "same birth/Id creates two distinct transport nonces with original scalar snapshots");
    }
    private static void SameBirthEpisodes()
    {
        using (var Source = new SameBirthSource()) using (var Value = new Fixture()) {
            var Owner = Value.Add(Root: true); Value.World.Entities = Source.Entities;
            ulong Accepted = Value.Budget.Accepted, Released = Value.Budget.Released;
            Source.Stage(Observation => Value.Emit(Observation));
            var Deliveries = Value.Take(Owner);
            var Fields = new List<string[]>(); foreach (var Item in Deliveries) Fields.Add(Item.Fields);
            CheckSameBirthNonces(Fields, Source); Source.Supersede();
            foreach (var Item in Deliveries) {
                Check(Host(Owner, 9, Item.Fields), "each independently certified old-epoch episode passes fresh admission");
                Check(Host(Owner, 39, new[] { Item.Fields[5] }), "each distinct episode nonce releases once");
                Check(!Host(Owner, 9, Item.Fields), "consumed episode nonce cannot replay");
            }
            Check(Source.FirstProof.Checks > 0 && Source.SecondProof.Checks > 0 && Value.Budget.Accepted == Accepted + 2 &&
                Value.Budget.Released == Released + 2 && Value.Budget.PendingCount == 0 && Value.Budget.RetainedBytes == 0,
                "same-birth modeled episodes return all transport resources to baseline");
        }
    }
    private static void Publication()
    {
        using (var Value = new Fixture()) {
            var Owner = Value.Add(0, Root: true);
            Operation(Owner, 10); Operation(Owner, 6, "entitydied");
            Check(Owner.GameplayEntityDiedListeners == 0 && !Value.Budget.Capture("entitydied"), "provisional subscription has no demand or backlog");
            Operation(Owner, 10); Operation(Owner, 6, "entitydied"); Operation(Owner, 11);
            Check(!Value.Budget.Capture("entitydied"), "nested commit remains behind outer publication");
            Operation(Owner, 12);
            Check(Owner.ListenerCount == 0 && Owner.GameplayEntityDiedListeners == 0, "failed cold/nested publication clears only staged listeners");
            string Published = Operation(Owner, 6, "entitydied")[0];
            Operation(Owner, 10); Operation(Owner, 7, Published); Operation(Owner, 6, "entitydied");
            Value.Emit(); var Item = Value.Take(Owner)[0];
            Check(Item.Fields[1] == Published && Owner.GameplayEntityDiedListeners == 1, "fanout uses committed listener during foreign publication");
            Operation(Owner, 12); Check(Host(Owner, 9, Item.Fields), "failed disconnect restores queued original listener");
            Check(Host(Owner, 39, new[] { Item.Fields[5] }), "restored publication refund");
            Value.Emit(); Item = Value.Take(Owner)[0];
            Operation(Owner, 10); Operation(Owner, 7, Published); Operation(Owner, 11);
            Check(!Host(Owner, 9, Item.Fields) && Owner.GameplayEntityDiedListeners == 0 && !Value.Budget.Capture("entitydied"),
                "committed disconnect invalidates callback and demand");
            Check(Value.Budget.PendingCount == 1 && Host(Owner, 39, new[] { Item.Fields[5] }), "cancelled callback remains charged until acknowledgement");
            string Destroyed = Operation(Owner, 6, "entitydestroyed")[0]; Operation(Owner, 6, "entitydied");
            Check(Owner.GameplayEntityDestroyedListeners == 1 && Owner.GameplayEntityDiedListeners == 1, "isolated B3 and B2 counters");
            var Removal = new Runtime.GameplayEntityDestroyedObservation("1", "p", 1, null, () => true, 0);
            Value.World.EntityDestroyed(Removal);
            Check(Owner.PendingCount == 0, "B3 historical publication cutoff still suppresses newer listeners");
            Value.Emit(); Check(Value.Take(Owner).Count == 1, "B3 cutoff does not apply to independently captured entity death");
            Operation(Owner, 7, Destroyed);
            Check(Owner.GameplayEntityDestroyedListeners == 0 && Owner.GameplayEntityDiedListeners == 1, "B3 disconnect does not clear B2 demand");
            var Candidate = Value.Candidate(1); Value.World.Retire(Candidate);
            Check(Owner.GameplayEntityDiedListeners == 1 && Candidate.GameplayEntityDiedListeners == 0, "failed root candidate leaves committed demand intact");
            Value.World.Retire(Owner);
            Check(Owner.GameplayEntityDiedListeners == 0 && !Value.Budget.Capture("entitydied"), "clear resets separate entity death demand");
        }
    }
    private static void RetentionAndCancellation()
    {
        using (var Value = new Fixture()) {
            var Root = Value.Add(Root: true); var Addon = Value.Add(); Value.Emit();
            var Held = Value.Take(Root)[0]; int Charge = Value.Budget.RetainedBytes;
            var Replacement = Value.Candidate(1); Value.World.CommitAddon(Addon, Replacement);
            Check(Addon.PendingCount == 0 && Addon.GameplayEntityDiedListeners == 0 && Value.Budget.PendingCount == 1 &&
                Value.Budget.RetainedBytes == Charge - Held.Payload.Length * 2, "pending addon cancellation refunds without touching native-held root");
            Check(!Host(Replacement, 9, Held.Fields) && !Host(Replacement, 39, new[] { Held.Fields[5] }) &&
                !Host(Root, 39, new[] { Held.Fields[5] }, Root.DomainLifetimeId + 1), "wrong domain cannot validate or refund old reservation");
            var NewRoot = Value.Candidate(1); Value.World.Commit(NewRoot); Value.World.Retire(Root);
            Check(!Host(Root, 9, Held.Fields) && !Host(NewRoot, 9, Held.Fields) && Value.Budget.PendingCount == 1,
                "root replacement cancels native-held work without early refund or retargeting");
            Check(Host(Root, 39, new[] { Held.Fields[5] }) && Value.Budget.PendingCount == 0, "retired exact owner retains release authority");
            Check(NewRoot.PendingCount == 0 && Replacement.PendingCount == 0, "replacement does not replay historical observation");
            Value.Frame++; Value.Emit();
            Check(NewRoot.PendingCount == 1 && Replacement.PendingCount == 1, "new domains receive only future capture");
        }
        using (var Value = new Fixture()) {
            var Owner = Value.Add(32, Root: true);
            for (int Frame = 0; Frame < 16; Frame++) {
                Value.Frame++; Value.Emit();
                foreach (var Item in Value.Take(Owner)) Check(Item.Payload.Length <= 2048, "per-reservation payload bound");
            }
            Check(Value.Budget.PendingCount == 512 && Value.Budget.RetainedBytes > 0 && Value.Budget.RetainedBytes <= 2 * 1024 * 1024,
                "native-held reservations stay charged across frames within existing global bounds");
            Value.Frame++; Value.Emit();
            Check(Owner.PendingCount == 0 && Value.Budget.PendingCount == 512 && Value.Budget.QueueRejected != 0,
                "global held-reservation ceiling rejects excess death without retry");
            int Before = Value.Budget.RetainedBytes; Value.World.Retire(Owner);
            Check(Value.Budget.PendingCount == 512 && Value.Budget.RetainedBytes == Before, "retirement never refunds native-held ownership prematurely");
        }
        using (var Value = new Fixture()) {
            var Owner = Value.Add(2, Root: true, Capacity: 1); Value.Emit();
            Check(Owner.PendingCount == 1 && Value.Budget.PendingCount == 1 && Value.Budget.QueueRejected == 1,
                "shared per-domain pending queue remains bounded");
        }
    }
    private static void PositionAttempts()
    {
        using (var Value = new Fixture()) {
            for (int Index = 0; Index < 512; Index++) {
                Check(!Value.Budget.DeathPositionSnapshot() && !Value.Budget.DestroyPositionSnapshot(), "no-demand attempts perform no host position work");
            }
            Check(Value.Budget.DeathPositionAccepted == 0 && Value.Budget.DeathPositionRejected == 0 &&
                Value.Budget.DestroyPositionAccepted == 0 && Value.Budget.DestroyPositionRejected == 0, "no-demand probes consume no position allowance");
            var Owner = Value.Add(Root: true);
            for (int Index = 0; Index < 512; Index++)
                Check(Value.Budget.DeathPositionSnapshot() == (Index < 128), "pre-Die attempts bounded even without successful death/capture");
            Check(Value.Budget.DeathPositionAccepted == 128 && Value.Budget.DeathPositionRejected == 384, "death-only attempt diagnostics");
            Check(!Value.Budget.DestroyPositionSnapshot() && Value.Budget.DestroyPositionAccepted == 0 && Value.Budget.DestroyPositionRejected == 0,
                "entity death demand cannot authorize destruction-position work");
            for (int Index = 0; Index < 128; Index++) Check(Value.Budget.Capture("entitydied"), "optional attempts leave producer capture allowance intact");
            Check(Value.Budget.PendingCount == 0 && Value.Budget.RetainedBytes == 0 && Owner.PendingCount == 0, "position attempts create no reserved queue work");
            Value.Frame++;
            Check(Value.Budget.DeathPositionSnapshot(), "new frame resets shared optional position allowance");
            string Destroyed = Operation(Owner, 6, "entitydestroyed")[0];
            for (int Index = 0; Index < 127; Index++) Check(Value.Budget.DestroyPositionSnapshot(), "destruction consumes remainder after a death attempt");
            Check(!Value.Budget.DestroyPositionSnapshot() && !Value.Budget.DeathPositionSnapshot(), "both kinds exhaust the same 128-attempt allowance");
            Check(Value.Budget.DestroyPositionAccepted == 127 && Value.Budget.DestroyPositionRejected == 1 &&
                Value.Budget.DeathPositionAccepted == 129 && Value.Budget.DeathPositionRejected == 385, "shared quota retains separate kind diagnostics");
            Value.Frame++;
            for (int Index = 0; Index < 512; Index++) {
                bool Accepted = (Index & 1) == 0 ? Value.Budget.DeathPositionSnapshot() : Value.Budget.DestroyPositionSnapshot();
                Check(Accepted == (Index < 128), "mixed veto attempts cannot multiply per-frame host position work");
            }
            Check(Value.Budget.DeathPositionAccepted == 193 && Value.Budget.DestroyPositionAccepted == 191,
                "mixed 128-attempt frame grants sixty-four reads per kind");
            Operation(Owner, 7, "1"); Value.Frame++;
            for (int Index = 0; Index < 512; Index++) Check(!Value.Budget.DeathPositionSnapshot(), "destruction-only demand cannot authorize death-position work");
            for (int Index = 0; Index < 128; Index++) Check(Value.Budget.DestroyPositionSnapshot(), "no-demand death attempts do not consume destruction allowance");
            Check(!Value.Budget.DestroyPositionSnapshot(), "destruction-only meaning stays bounded at 128");
            Operation(Owner, 7, Destroyed); Value.Frame++;
            Check(!Value.Budget.DeathPositionSnapshot() && !Value.Budget.DestroyPositionSnapshot(), "disconnect removes both kinds of position demand");
        }
    }
    private static void FrameLimits()
    {
        using (var Value = new Fixture()) {
            var Owner = Value.Add(Root: true);
            foreach (string Kind in new[] { "died", "spawned", "entityspawned", "entitydestroyed" }) Operation(Owner, 6, Kind);
            for (int Index = 0; Index < 128; Index++) Value.Emit();
            Check(Owner.PendingCount == 32 && Value.Budget.PendingCount == 32 && Value.Budget.FanoutRejected != 0,
                "entity death shares 32/domain/frame delivery quota");
            foreach (string Kind in new[] { "died", "spawned", "entityspawned", "entitydestroyed", "entitydied" })
                Check(!Value.Budget.Capture(Kind), "all five kinds share 128/frame captures");
            Value.Frame++;
            foreach (string Kind in new[] { "died", "spawned", "entityspawned", "entitydestroyed", "entitydied" })
                Check(Value.Budget.Capture(Kind), "new frame restores shared capture allowance");
            Value.Take(Owner);
            Value.Frame++;
            for (int Index = 0; Index < 4096; Index++) Check(Value.Budget.Visit(), "bounded shared visit allowance");
            Check(!Value.Budget.Visit(), "4096/frame shared visits enforced");
            Value.Emit(); Check(Owner.PendingCount == 0 && Value.Budget.ScanRejected != 0, "visit saturation drops fanout without retained retry");
            Value.Frame++; Value.Emit(); Check(Value.Take(Owner).Count == 1, "new frame resumes bounded progress");
            // Legacy kinds must still carry twelve fields, even with B2 demand.
            var Observation = new Runtime.GameplayEntityDestroyedObservation("9", "p", 9, null, () => true);
            Value.World.EntityDestroyed(Observation); var Legacy = Value.Take(Owner)[0];
            Check(Legacy.Fields.Length == 12 && Host(Owner, 9, Legacy.Fields), "B3 payload/gate remain twelve fields");
            var Extended = new List<string>(Legacy.Fields); Extended.Add(""); Extended.Add("");
            Check(!Host(Owner, 9, Extended.ToArray()), "fourteen-field shape exclusive to entitydied");
        }
    }
    [DllImport("libc", SetLastError = true)] private static extern int chmod(string Path, int Mode);
    private const string State = "local S=require('state'); ";
    private const string NativeEntry = State + "S.Count=0; S.History={}; local W=game:GetService('Workspace'); " +
        "S.Connection=W.EntityDied:Connect(function(...) assert(select('#',...)==1); local C=...; " +
        "assert(type(C)=='table' and table.isfrozen(C)); assert(type(C.Id)=='string' and type(C.Prefab)=='string'); " +
        "local Keys={Id=true,Prefab=true,Position=true,Killer=true,KillerId=true,DamageType=true,Weapon=true}; " +
        "for Key in pairs(C) do assert(Keys[Key]) end; " +
        "assert(C.Entity==nil and C.Token==nil and C.EpochIdentity==nil and C.Cause==nil); " +
        "assert(C.Killer==nil or (table.isfrozen(C.Killer) and type(C.KillerId)=='string')); " +
        "assert(not pcall(function() C.Id='replacement' end)); " +
        "S.Count+=1; S.Last=C; table.insert(S.History,C); print('entity died '..C.Id) end)";
    private sealed class EntityProbe : Carbon.Plugins.IEntityFacadeHost, IDisposable
    {
        internal readonly Model Lifetimes = new Model(32);
        internal int Lookups, Reads;
        internal EntityProbe()
        { Lifetimes.BeginQualifiedObservation(); Check(Lifetimes.QualifyStartupCompletion(), "synthetic D20 startup qualified"); }
        public string[] Lookup(Runtime.FacadeSession Session, string Id)
        { Lookups++; throw new Runtime.FacadeException("historical snapshot must never reacquire an Entity by Id"); }
        public string[] Read(Runtime.FacadeSession Session, string Token, string Publication, string Property)
        { Reads++; throw new Runtime.FacadeException("historical snapshot must never read a live Entity"); }
        public void Dispose() { Lifetimes.Dispose(); }
    }
    private sealed class NativeFixture : IDisposable
    {
        internal readonly EntityProbe Entities = new EntityProbe();
        internal readonly Dictionary<string, Runtime.PlayerView> Players = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Runtime.FacadeWorld World;
        internal readonly Runtime.ScriptHost Host;
        internal string Source = NativeEntry;
        internal long Frame;
        internal bool Ready = true;
        internal int PlayerLookups, Errors, Timeouts, Messages;
        internal Runtime.GameplayEventBudget Budget { get { return World.GameplayEvents; } }
        internal Runtime.RuntimeDomain Root {
            get { return (Runtime.RuntimeDomain)typeof(Runtime.ScriptHost).GetField("Current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Host); }
        }
        internal NativeFixture(Runtime.NativeRuntime Native)
        {
            World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => {
                PlayerLookups++; Runtime.PlayerView View; return Players.TryGetValue(Id, out View) ? View : null;
            }), new Registrar());
            World.Entities = Entities;
            Budget.FrameClock = () => Frame;
            World.GameplayAvailable = Kind => Kind != "entitydied" || Ready;
            Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {
                MaxCallbackMilliseconds = 20, FrameDrainBudgetMilliseconds = 20
            }, Sources, World);
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "initial real-VM EntityDied subscription");
        }
        private Runtime.ScriptSnapshot Sources()
        {
            var Value = new Runtime.ScriptSnapshot { EntryName = "init.luau", EntrySource = Source };
            Value.Modules.Add("state", "return {}");
            Value.Modules.Add("connectfail", "game:GetService('Workspace').EntityDied:Connect(function() error('leak') end); error('failed connect module')");
            Value.Modules.Add("disconnectfail", State + "S.Connection:Disconnect(); error('failed disconnect module')");
            Value.Modules.Add("nestedfail", "assert(require('connectfail')); return true");
            return Value;
        }
        internal Runtime.PlayerLifetime Connect(string UserId, string Name)
        {
            var View = new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = UserId,
                Name = Name, Connected = true, Send = Text => Messages++, Permission = NameValue => true };
            Players[UserId] = View; return World.Players.Connect(View);
        }
        internal void Disconnect(string UserId)
        { World.Players.Disconnect(UserId, Players[UserId].Identity); Players.Remove(UserId); }
        internal bool Emit(Runtime.GameplayEntityDiedObservation Observation = null)
        {
            if (!Budget.Capture("entitydied")) return false;
            World.EntityDied(Observation ?? Snapshot()); return true;
        }
        internal void Execute(string Source)
        { var Result = Host.Execute("entitydied.fixture", Source); Check(Result.Status == Runtime.RuntimeStatus.OK, Result.Error); }
        internal string Drain(bool AllowError = false)
        {
            var Text = new StringBuilder();
            for (int Turn = 0; Turn < 256 && Host.HasWork; Turn++) foreach (var Result in Host.Drain()) {
                if (Result.Status != Runtime.RuntimeStatus.OK) Errors++;
                if (Result.Status == Runtime.RuntimeStatus.TIMEOUT) Timeouts++;
                Check(AllowError || Result.Status == Runtime.RuntimeStatus.OK,
                    "callback status=" + Result.Status + "; error=" + Result.Error + "; " + Budget.Status);
                Text.Append(Result.Logs);
            }
            Check(!Host.HasWork && Budget.PendingCount == 0 && Budget.RetainedBytes == 0, "real scheduler consumption/refund converges");
            Check(Entities.Lookups == 0 && Entities.Reads == 0, "delivery never reacquires or reads a live Entity");
            return Text.ToString();
        }
        public void Dispose()
        {
            try { Host.Dispose(); } finally { Entities.Dispose(); }
            Check(World.Active == null && Budget.PendingCount == 0 && Budget.RetainedBytes == 0, "real VM/managed transport teardown");
            Check(Entities.Lookups == 0 && Entities.Reads == 0, "no hidden Entity adapter call");
        }
    }
    internal static void Run(string Library, string Compiler)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauEntityDied-" + Guid.NewGuid().ToString("N"));
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(Library, Target);
            string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, Worker);
            if (Rid == "linux-x64") Check(chmod(Worker, 493) == 0, "compiler executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) RunNative(Native);
        } finally { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    internal static void RunNative(Runtime.NativeRuntime Native)
    {
        Run();
        NativeHistorical(Native); NativeSameBirthEpisodes(Native); NativeOptionalFields(Native); NativeKillerLifetime(Native); NativePublication(Native);
        NativeFaults(Native); DataScalar_InvalidBlob14(Native); NativeDependencies(Native); NativeSharedValues(Native);
        NativeMixed(Native); NativeScale(Native);
        Check(Native.LiveVmCount == 0, "all EntityDied fixture VMs released");
        Console.WriteLine("[CarbonLuau:EntityDied] PASS real VM frozen snapshots, historical certificates, exact D11 killer, fourteen-field codec, publication/lifecycle/dependencies, faults and shared scale; synthetic host proof only");
    }
    private static void FlushNative(NativeFixture Value)
    {
        var Domains = new List<Runtime.RuntimeDomain> { Value.Root };
        Domains.AddRange(((SortedDictionary<long, Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost)
            .GetField("AddonDomains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Value.Host)).Values);
        foreach (var Domain in Domains) Domain.FacadeSession.Flush(Domain, Stopwatch.StartNew(), 20, 256);
        Check(Value.Host.SchedulerSnapshot.Queued == (ulong)Value.Budget.PendingCount, "managed reservations survive actual native transfer");
    }
    private static void NativeHistorical(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) {
            Check(Value.Drain() == "", "real registration never replays history");
            var Original = new EntitySpawnSignalTests.ObjectState { Id = 1 };
            var Attempt = Value.Entities.Lifetimes.BeginSpawn(Original);
            Check(Value.Entities.Lifetimes.CompleteSpawn(Attempt, true, true), "actual D20 model completed-Spawn epoch");
            var EpochRoot = Value.Entities.Lifetimes.CaptureCompletedWitness(Original);
            Check(EpochRoot != null, "capture the exact completed epoch root before synthetic Die");
            var Publication = Value.World.Active.CaptureCommittedEntityWitness();
            var Authority = new Model.Authority((ulong)Value.World.Active.VmGenerationId, (ulong)Value.World.Active.DomainLifetimeId, Publication.Token);
            Model.Binding Binding;
            Func<Model.Authority, bool> Current = Item => Value.World.Active.IsGameplayPublicationCurrent(Publication);
            Func<object, Model.HostEvidence> Evidence = Identity => {
                var Object = (EntitySpawnSignalTests.ObjectState)Identity;
                return new Model.HostEvidence(Object.Alive, true, true, Object.Id, Object.Prefab, Object);
            };
            Check(Value.Entities.Lifetimes.TryAdmit(Original, Authority, Current, Evidence, out Binding), "exact original D20 incarnation admitted in the model");
            var Proof = new Proof(EpochRoot);
            var Observation = Snapshot(Proof, Id: "1", Epoch: EpochRoot.Birth);
            ulong Before = Value.Host.Attempted;
            Check(Value.Emit(Observation) && Proof.Checks == 0 && Value.Host.Attempted == Before, "source only queues; no recursive callback entry");
            Original.Alive = false; Proof.Alive = false;
            Check(!Value.Entities.Lifetimes.Validate(Binding, Current, Evidence) && Binding.Record.Retired, "original model lifetime really retires before delivery");
            string Actual = Value.Drain();
            Check(Actual == "entity died 1\n" && Proof.Checks > 0,
                "historical certificate survives retirement without later host access; actual='" +
                Actual.Replace("\r", "\\r").Replace("\n", "\\n") + "'; proof_checks=" + Proof.Checks +
                "; callback_errors=" + Value.Errors + "; " + Value.Budget.Status);
            Value.Execute(State + "assert(S.Count==1 and S.Last.Id=='1' and S.Last.Prefab=='assets/fixture.prefab'); " +
                "assert(S.Last.Position==Vector3.new(1,2,3) and S.Last.KillerId=='00076561190000999889'); " +
                "assert(S.Last.Killer.Name=='Captured killer' and not S.Last.Killer.IsConnected); " +
                "assert(S.Last.DamageType=='Bullet' and S.Last.Weapon=='assets/weapon.prefab'); local Count=0; for _ in pairs(S.Last) do Count+=1 end; assert(Count==7)");
            Value.Frame++; var FalseHistorical = new Proof { Completed = false, Alive = false };
            Check(Value.Emit(Snapshot(FalseHistorical, Id: "2")), "false historical certificate queued for pre-entry rejection");
            Check(Value.Drain() == "" && FalseHistorical.Checks > 0, "retirement alone never manufactures completed death");
            Value.Frame++; Value.Emit(Snapshot(new Proof { Throws = true }, Id: "3"));
            Check(Value.Drain() == "", "throwing certificate fails closed without callback");
            Value.Frame++; Value.Emit(Snapshot(Id: "4")); Value.Ready = false;
            Check(Value.Drain() == "", "qualified source loss suppresses already queued snapshots");
            Value.Execute("assert(not pcall(function() game:GetService('Workspace').EntityDied:Connect(function() end) end))");
            Value.Ready = true;
            Value.Frame++; Value.Emit(Snapshot(Id: "5", Epoch: 5)); Value.Emit(Snapshot(Id: "5", Epoch: 6));
            Check(Value.Drain() == "entity died 5\nentity died 5\n", "distinct captured incarnations can share a network Id without retargeting");
            Value.Execute(State + "assert(S.History[1].Id=='1' and S.History[1].DamageType=='Bullet')");
        }
    }
    private static void NativeSameBirthEpisodes(Runtime.NativeRuntime Native)
    {
        int BaselineVms = Native.LiveVmCount;
        using (var Source = new SameBirthSource()) using (var Value = new NativeFixture(Native)) {
            ulong Accepted = Value.Budget.Accepted, Released = Value.Budget.Released, Attempted = Value.Host.Attempted;
            Source.Stage(Observation => Check(Value.Emit(Observation), "same-frame independently certified episode capture"));
            var Fields = new List<string[]>();
            object Queue = typeof(Runtime.FacadeSession).GetField("Pending", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(Value.World.Active);
            foreach (object Item in (System.Collections.IEnumerable)Queue) {
                byte[] Payload = (byte[])Item.GetType().GetField("Payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Item);
                Fields.Add(Runtime.FacadePolicy.Unpack(Payload));
            }
            CheckSameBirthNonces(Fields, Source);
            Check(Value.Host.Attempted == Attempted && Value.Budget.PendingCount == 2, "both same-birth episodes queue before any Lua entry");
            Source.Supersede();
            Check(Value.Drain() == "entity died 61\nentity died 61\n", "both original same-birth episodes dispatch after a later Spawn");
            Value.Execute(State + "assert(S.Count==2 and #S.History==2); local A,B=S.History[1],S.History[2]; " +
                "assert(not rawequal(A,B) and table.isfrozen(A) and table.isfrozen(B)); " +
                "assert(A.Id=='61' and B.Id=='61' and A.Prefab=='assets/original.prefab' and B.Prefab==A.Prefab); " +
                "assert(A.Position==Vector3.new(1,2,3) and B.Position==A.Position); " +
                "assert(A.Entity==nil and B.Entity==nil and A.EpochIdentity==nil and B.EpochIdentity==nil)");
            Check(Source.FirstProof.Checks > 0 && Source.SecondProof.Checks > 0 && Value.Budget.Accepted == Accepted + 2 &&
                Value.Budget.Released == Released + 2 && Value.Budget.PendingCount == 0 && Value.Budget.RetainedBytes == 0 &&
                Value.Errors == 0 && Value.Timeouts == 0 && Value.Entities.Lookups == 0 && Value.Entities.Reads == 0,
                "two fresh same-birth contexts validate independent historical proofs and restore resource baseline");
        }
        Check(Native.LiveVmCount == BaselineVms, "same-birth real VM returns to baseline");
        Console.WriteLine("[CarbonLuau:EntityDiedSameBirth] PASS two independent historical certificates, same Id/birth, distinct nonces, fresh contexts and old-epoch no-retarget; pending=0 retained_bytes=0 vm_baseline=" + BaselineVms);
    }
    private static void NativeOptionalFields(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) {
            for (int Mask = 0; Mask < 16; Mask++) {
                Value.Frame++;
                bool Position = (Mask & 1) != 0, Killer = (Mask & 2) != 0, Damage = (Mask & 4) != 0, Weapon = (Mask & 8) != 0;
                var Observation = new Runtime.GameplayEntityDiedObservation((Mask + 100).ToString(CultureInfo.InvariantCulture), "assets/fixture.prefab", (ulong)(Mask + 100),
                    Position ? (Runtime.PlayerPosition?)new Runtime.PlayerPosition(1, 2, 3) : null, () => true,
                    Killer ? "7" : null, Killer ? "000" : null, Killer ? "Optional killer" : null,
                    Damage ? "Bullet" : null, Weapon ? "assets/weapon.prefab" : null);
                Check(Value.Emit(Observation) && Value.Drain() == "entity died " + Observation.Id + "\n", "all independent optional field combinations delivered");
                int Count = 2 + (Position ? 1 : 0) + (Killer ? 2 : 0) + (Damage ? 1 : 0) + (Weapon ? 1 : 0);
                Value.Execute(State + "local C=S.Last; local Count=0; for _ in pairs(C) do Count+=1 end; assert(Count==" + Count + "); " +
                    "assert((C.Position~=nil)==" + (Position ? "true" : "false") + "); assert((C.Killer~=nil)==" + (Killer ? "true" : "false") + "); " +
                    "assert((C.KillerId~=nil)==" + (Killer ? "true" : "false") + "); assert((C.DamageType~=nil)==" + (Damage ? "true" : "false") + "); " +
                    "assert((C.Weapon~=nil)==" + (Weapon ? "true" : "false") + "); " +
                    "if C.Position then assert(C.Position==Vector3.new(1,2,3)) end; " +
                    "if C.Killer then assert(C.KillerId=='000' and C.Killer.Name=='Optional killer') end; " +
                    "if C.DamageType then assert(C.DamageType=='Bullet') end; if C.Weapon then assert(C.Weapon=='assets/weapon.prefab') end");
            }
            Value.Frame++;
            var Boundary = new Runtime.GameplayEntityDiedObservation("18446744073709551615", new string('\u00e9', 256), UInt64.MaxValue,
                new Runtime.PlayerPosition(Single.MaxValue, -Single.MaxValue, Single.Epsilon), () => true,
                "18446744073709551615", "000", new string('\u00e9', 64), new string('\u00e9', 16), new string('\u00e9', 256));
            Check(Value.Emit(Boundary) && Value.Drain() == "entity died 18446744073709551615\n", "UTF-8 and exact numeric boundary snapshots traverse real codec");
            Value.Execute(State + "local C=S.Last; assert(#C.Prefab==512 and #C.Killer.Name==128 and #C.DamageType==32 and #C.Weapon==512); " +
                "assert(C.KillerId=='000' and C.Position.X>0 and C.Position.Y<0 and C.Position.Z>0)");
        }
    }
    private static void NativeKillerLifetime(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) {
            const string UserId = "76561190000999889";
            var Killer = Value.Connect(UserId, "Original killer");
            Value.Execute(State + "S.OriginalKiller=game:GetService('Players'):GetPlayerByUserId('" + UserId + "')");
            var Observation = new Runtime.GameplayEntityDiedObservation("200", "assets/fixture.prefab", 200, null, () => true,
                Killer.Token, Killer.UserId, Killer.Name, "Bullet", "assets/weapon.prefab");
            Check(Value.Emit(Observation), "captured original tracked killer snapshot");
            Value.Disconnect(UserId); var Reconnected = Value.Connect(UserId, "New killer");
            Check(Reconnected.Token != Killer.Token, "real D11 model creates a fresh connection token");
            int Before = Value.PlayerLookups;
            Check(Value.Drain() == "entity died 200\n" && Value.PlayerLookups == Before, "delivery does not re-resolve captured killer by account Id");
            Value.Execute(State + "assert(S.Last.Killer==S.OriginalKiller and S.Last.KillerId=='" + UserId + "'); " +
                "assert(S.Last.Killer.Name=='Original killer' and not S.Last.Killer.IsConnected); " +
                "assert(S.Last.Killer~=game:GetService('Players'):GetPlayerByUserId('" + UserId + "')); " +
                "assert(not pcall(function() S.Last.Killer:SendMessage('stale') end))");
            Check(Value.Messages == 0, "old killer facade cannot mutate reconnected Player");
            Value.Frame++;
            Value.Emit(new Runtime.GameplayEntityDiedObservation("201", "assets/fixture.prefab", 201, null, () => true,
                Reconnected.Token, Reconnected.UserId, Reconnected.Name));
            Check(Value.Drain() == "entity died 201\n", "future event uses separately captured fresh connection");
            Value.Execute(State + "assert(S.Last.Killer.IsConnected and S.Last.Killer.Name=='New killer'); S.Last.Killer:SendMessage('current')");
            Check(Value.Messages == 1, "fresh defining-domain killer facade keeps ordinary authority");
        }
    }
    private static void NativePublication(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) {
            Value.Execute("assert(not pcall(require,'connectfail')); assert(not pcall(require,'disconnectfail')); assert(not pcall(require,'nestedfail'))");
            Check(Value.World.Active.ListenerCount == 1 && Value.World.Active.GameplayEntityDiedListeners == 1, "cold/nested failure restores real Lua roots and committed demand");
            Value.Emit(Snapshot(Id: "300")); Check(Value.Drain() == "entity died 300\n", "restored Lua callback remains usable");
            var Previous = Value.World.Active;
            Value.Source = "game:GetService('Workspace').EntityDied:Connect(function() error('candidate leak') end); error('failed root')";
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Value.World.Active == Previous, "failed root preserves old publication");
            Value.Frame++; Value.Emit(Snapshot(Id: "301")); Check(Value.Drain() == "entity died 301\n", "failed candidate owns no callback or backlog");
            Value.Frame++; Value.Emit(Snapshot(Id: "302")); FlushNative(Value);
            Check(Value.Budget.PendingCount == 1, "old root has actual native-held snapshot");
            Value.Source = NativeEntry;
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.OK && Previous.Disposed, "successful root replacement retires exact owner");
            Check(Value.Drain() == "", "replacement cancels native-held callback without replay");
            Value.Frame++; Value.Emit(Snapshot(Id: "303")); Check(Value.Drain() == "entity died 303\n", "new root receives only future observation");
            Value.Frame++; Value.Emit(Snapshot(Id: "304")); FlushNative(Value);
            Value.Execute(State + "S.Connection:Disconnect(); S.Connection:Disconnect()");
            Check(Value.Drain() == "" && !Value.Budget.Capture("entitydied"), "idempotent real connection disconnect cancels native-held work and demand");
        }
    }
    private static void NativeFaults(Runtime.NativeRuntime Native)
    {
        foreach (string Body in new[] { "error('ordinary callback')", "coroutine.yield()", "while true do end" }) using (var Value = new NativeFixture(Native)) {
            Value.Execute("game:GetService('Workspace').EntityDied:Connect(function() " + Body + " end)");
            long Generation = Value.Host.VmGenerationId;
            Value.Emit(Snapshot(Id: "400"));
            Check(Value.Drain(true).Contains("entity died 400\n"), "independent callback progresses beside throwing/yielding/runaway listener");
            if (Body == "while true do end") {
                Check(Value.Timeouts > 0 && Value.Host.VmGenerationId != Generation && Value.Host.Recoveries == 1, "callback deadline retires the whole VM and uses one recovery");
                Check(Value.Drain() == "", "reconstruction never resumes or replays failed captured work");
                Value.Frame++; Value.Emit(Snapshot(Id: "401")); Check(Value.Drain() == "entity died 401\n", "reconstructed root sees future snapshots only");
            } else Check(Value.Host.VmGenerationId == Generation && Value.Host.Recoveries == 0, "ordinary callback faults leave healthy VM usable");
        }
    }
    private static byte[] TakeNativePayload(NativeFixture Value)
    {
        object Queue = typeof(Runtime.FacadeSession).GetField("Pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Value.World.Active);
        object Item = Queue.GetType().GetMethod("Dequeue").Invoke(Queue, null);
        return (byte[])Item.GetType().GetField("Payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Item);
    }
    private static void DataScalar_InvalidBlob14(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) {
            Value.Emit(Snapshot(Id: "500")); byte[] Payload = TakeNativePayload(Value);
            string[] Fields = Runtime.FacadePolicy.Unpack(Payload);
            ulong Nonce = UInt64.Parse(Fields[5], CultureInfo.InvariantCulture);
            var Invalid = new List<byte[]>();
            var Labels = new List<string>();
            Action<int, string> Bad = (Index, Text) => {
                var Copy = (string[])Fields.Clone(); Copy[Index] = Text; Invalid.Add(Runtime.FacadePolicy.Pack(Copy));
                Labels.Add("field=" + Index + "; value=" + (Text.Length <= 32 ? Text : "length " + Text.Length));
            };
            foreach (int Index in new[] { 1, 2, 3, 5, 9 })
                foreach (string Text in new[] { "0", "01", "+1", "18446744073709551616" }) Bad(Index, Text);
            Bad(0, "unknown"); Bad(4, ""); Bad(4, new string('x', 513));
            foreach (int Index in new[] { 6, 7, 8 })
                foreach (string Text in new[] { "", "NaN", "Infinity", "+1", "-0", "01", "1e999", "3.4028235E38" }) Bad(Index, Text);
            Bad(9, ""); Bad(10, ""); Bad(10, "account"); Bad(10, new string('1', 21));
            Bad(11, new string('x', 129)); Bad(12, new string('x', 33)); Bad(13, new string('x', 513));
            foreach (int Count in new[] { 12, 13, 15 }) {
                var Copy = new string[Count]; Array.Copy(Fields, Copy, Math.Min(Count, Fields.Length));
                if (Count > 14) Copy[14] = "";
                Invalid.Add(Runtime.FacadePolicy.Pack(Copy));
                Labels.Add("shape=" + Count);
            }
            var Truncated = new byte[Payload.Length - 1]; Array.Copy(Payload, Truncated, Truncated.Length); Invalid.Add(Truncated); Labels.Add("missing terminal NUL");
            var MalformedUtf8 = (byte[])Payload.Clone();
            int Offset = 0; for (int Index = 0; Index < 4; Index++) Offset += Encoding.UTF8.GetByteCount(Fields[Index]) + 1;
            MalformedUtf8[Offset] = 0xff; Invalid.Add(MalformedUtf8); Labels.Add("malformed UTF-8 prefab");
            var Oversized = new byte[2049]; for (int Index = 0; Index < Oversized.Length - 1; Index++) Oversized[Index] = (byte)'x';
            Invalid.Add(Oversized); Labels.Add("2049-byte payload");
            bool NativeHeld = false;
            try {
                ulong Before = Value.Host.SchedulerSnapshot.Queued;
                for (int Index = 0; Index < Invalid.Count; Index++) {
                    Runtime.RuntimeStatus Status = Value.Root.Event(Invalid[Index]);
                    if (Status == Runtime.RuntimeStatus.OK) NativeHeld = true;
                    Check(Status == Runtime.RuntimeStatus.INVALID_ARGUMENT,
                        "native fourteen-field codec rejects malformed blob before queue admission: " + Labels[Index] +
                        "; actual_status=" + Status + "; native_queued=" + Value.Host.SchedulerSnapshot.Queued + "; " + Value.Budget.Status);
                }
                Check(Value.Host.SchedulerSnapshot.Queued == Before && Value.Budget.PendingCount == 1,
                    "malformed native admission never creates or refunds unrelated managed ownership");
                Check(Value.Budget.ToNative(Value.World.Active, Nonce), "original payload transfers managed ownership once");
                Runtime.RuntimeStatus Accepted = Value.Root.Event(Payload);
                NativeHeld = Accepted == Runtime.RuntimeStatus.OK;
                Check(NativeHeld, "original fourteen-field payload accepted through actual native admission; actual_status=" + Accepted);
                Check(Value.Root.Event(Payload) == Runtime.RuntimeStatus.INVALID_ARGUMENT, "duplicate native reservation rejected without replacing original");
                Check(Value.Drain() == "entity died 500\n" && Value.Budget.Released == 1, "accepted payload consumes/refunds exactly once after malformed attempts");
            } catch (Exception Error) {
                Console.WriteLine("[CarbonLuau:EntityDiedCodec] FIRST_FAILURE " + Error.Message); throw;
            } finally {
                // Native owns any admitted nonce until callback consumption or
                // domain destruction. An assertion failure must not refund it
                // early and hide the original failure behind a stale teardown ack.
                if (!NativeHeld) Value.Budget.Release(Value.World.Active, Nonce);
            }
            foreach (int Index in new[] { 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13 }) {
                Value.Frame++; var Proof = new Proof(); Value.Emit(Snapshot(Proof, Id: "501"));
                Payload = TakeNativePayload(Value); Fields = Runtime.FacadePolicy.Unpack(Payload);
                Nonce = UInt64.Parse(Fields[5], CultureInfo.InvariantCulture);
                string Changed = Index == 4 || Index == 13 ? "assets/replacement.prefab" : Index == 11 ? "Replacement killer" : Index == 12 ? "Fire" : "99";
                Fields[Index] = Changed; ulong Before = Value.Budget.StaleRejected;
                NativeHeld = false;
                try {
                    Check(Value.Budget.ToNative(Value.World.Active, Nonce), "changed snapshot transfers managed ownership once");
                    Runtime.RuntimeStatus Status = Value.Root.Event(Runtime.FacadePolicy.Pack(Fields));
                    NativeHeld = Status == Runtime.RuntimeStatus.OK;
                    Check(NativeHeld, "syntactically valid changed snapshot may enter native queue; field=" + Index + "; actual_status=" + Status);
                    string Actual = Value.Drain();
                    Check(Actual == "" && Value.Budget.StaleRejected > Before && Proof.Checks == 0,
                        "fresh callback matches immutable snapshot before certificate; field=" + Index +
                        "; actual='" + Actual.Replace("\r", "\\r").Replace("\n", "\\n") + "'; stale_before=" + Before +
                        "; proof_checks=" + Proof.Checks + "; " + Value.Budget.Status);
                } catch (Exception Error) {
                    Console.WriteLine("[CarbonLuau:EntityDiedCodec] FIRST_FAILURE " + Error.Message); throw;
                } finally { if (!NativeHeld) Value.Budget.Release(Value.World.Active, Nonce); }
            }
        }
        Console.WriteLine("[CarbonLuau:EntityDiedCodec] PASS malformed fourteen-field blobs, duplicate native ownership and immutable scalar matching");
    }
    private static byte[] Package(string Id, string Source = null, string ManifestSuffix = "", string Api = null,
        string Cold = null, string Nested = null)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                var Files = new Dictionary<string, string> {
                    { "addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" + ManifestSuffix + "}" },
                    { "init.luau", Source ?? "game:GetService('Workspace').EntityDied:Connect(function(C) assert(table.isfrozen(C)); print('B2 " + Id + "') end)" }
                };
                if (Api != null) Files.Add("api.luau", Api);
                if (Cold != null) Files.Add("cold.luau", Cold);
                if (Nested != null) Files.Add("nested.luau", Nested);
                foreach (var Pair in Files) using (var Stream = Zip.CreateEntry(Pair.Key).Open()) {
                    byte[] Bytes = Encoding.UTF8.GetBytes(Pair.Value); Stream.Write(Bytes, 0, Bytes.Length);
                }
            }
            return Output.ToArray();
        }
    }
    private static void Process(Runtime.AddonRegistry Registry)
    {
        int Guard = 32;
        while (Registry.HasPending && Guard-- > 0) Registry.ProcessOne();
        Check(!Registry.HasPending, "bounded addon dependency processing converges");
    }
    private static string Register(Runtime.AddonRegistry Registry, object Owner, byte[] Package, string Expected = "Active")
    {
        string[] Result = Registry.RegisterArchive(Owner, Package);
        Check(Result[0] == "OK", "archive registration accepted: " + String.Join("|", Result)); Process(Registry);
        Check(Registry.Status(Owner, Result[1])[2] == Expected, "addon activation reaches " + Expected); return Result[1];
    }
    private static Runtime.FacadeSession Session(Runtime.FacadeWorld World, string Command)
    {
        foreach (var Owner in World.Sessions()) if (Owner.Commands.ContainsKey(Command)) return Owner;
        throw new InvalidOperationException("EntityDied: missing fixture addon command " + Command);
    }
    private static void NativeDependencies(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            object Provider = new object(), Consumers = new object(), FailedOwner = new object(), ForeignOwner = new object();
            Func<string, byte[]> Dependency = Marker => Package("b2provider",
                "local Api=require('api'); game:GetService('Workspace').EntityDied:Connect(function(C) assert(table.isfrozen(C)); print('B2 provider " + Marker + "') end); " +
                "game:GetService('Commands'):Register('b2providerstate',{},function() end)",
                ",\"main\":\"api\",\"publicModules\":[\"cold\",\"nested\"]",
                "local Api={Marker='" + Marker + "',W=game:GetService('Workspace')}; function Api.ConnectForeign() " +
                "Api.W.EntityDied:Connect(function(C) assert(table.isfrozen(C)); print('B2 foreign '..Api.Marker) end) end; return Api",
                "require('api').ConnectForeign(); error('failed foreign cold publication')",
                "assert(not pcall(require,'cold')); require('api').ConnectForeign(); error('failed foreign outer publication')");
            Func<string, bool, bool, byte[]> Consumer = (Name, Optional, Foreign) => Package(Name,
                "local Api=require('@b2provider'); " + (Foreign ? "Api.ConnectForeign(); " :
                "game:GetService('Workspace').EntityDied:Connect(function(C) assert(C.Id~=nil); print('B2 " + Name + " '..Api.Marker) end); ") +
                "game:GetService('Commands'):Register('" + Name + "state',{},function() end)",
                ",\"dependencies\":{\"required\":[" + (Optional ? "" : "\"b2provider\"") + "],\"optional\":[" + (Optional ? "\"b2provider\"" : "") + "]}");
            string Token = Register(Registry, Provider, Dependency("A1"));
            string Required = Register(Registry, Consumers, Consumer("b2required", false, false));
            string OptionalToken = Register(Registry, Consumers, Consumer("b2optional", true, false));
            var OptionalOwner = Session(Value.World, "b2optionalstate");
            string Failed = Register(Registry, FailedOwner, Package("b2failed",
                "assert(not pcall(require,'@b2provider/nested')); game:GetService('Workspace').EntityDied:Connect(function() error('failed candidate leak') end); error('failed addon')",
                ",\"dependencies\":{\"required\":[\"b2provider\"],\"optional\":[]}"), "Failed");
            Check(Session(Value.World, "b2providerstate").GameplayEntityDiedListeners == 1, "failed foreign cold/nested publication restores provider callback roots");
            string ForeignToken = Register(Registry, ForeignOwner, Consumer("b2foreign", true, true));
            Check(Session(Value.World, "b2providerstate").GameplayEntityDiedListeners == 2 &&
                Session(Value.World, "b2foreignstate").GameplayEntityDiedListeners == 0, "captured foreign facade keeps provider ResourceOwner");
            Value.Emit(Snapshot(Id: "600")); FlushNative(Value);
            var RequiredOld = Session(Value.World, "b2requiredstate");
            Check(Registry.ReplaceArchive(Provider, Token, Dependency("A2"))[0] == "OK", "provider replacement admitted"); Process(Registry);
            string Queued = Value.Drain();
            Check(!Queued.Contains("B2 provider A1") && !Queued.Contains("B2 b2required A1") && !Queued.Contains("B2 foreign A1") &&
                Queued.Contains("B2 b2optional A1"), "actual native-held provider/required/foreign cancellation preserves optional domain work");
            Check(RequiredOld.Disposed && Object.ReferenceEquals(OptionalOwner, Session(Value.World, "b2optionalstate")), "required reconstructs and optional retains exact old domain");
            Value.Frame++; Value.Emit(Snapshot(Id: "601")); string Fresh = Value.Drain();
            Check(Fresh.Contains("B2 provider A2") && Fresh.Contains("B2 b2required A2") && Fresh.Contains("B2 b2optional A1") &&
                !Fresh.Contains("B2 foreign"), "provider replacement does not move foreign listeners or hot-rebind optional values");
            Value.Frame++; Value.Emit(Snapshot(Id: "602")); FlushNative(Value);
            Registry.UnloadProvider(Provider); Process(Registry);
            Check(Registry.Status(Consumers, Required)[2] == "Blocked", "required dependency loss retires consumer");
            string Lost = Value.Drain();
            Check(!Lost.Contains("B2 provider") && !Lost.Contains("B2 b2required") && Lost.Contains("B2 b2optional A1"), "provider loss suppresses queued exact old owners");
            Register(Registry, Provider, Dependency("A3"));
            Value.Frame++; Value.Emit(Snapshot(Id: "603")); string Restored = Value.Drain();
            Check(Restored.Contains("B2 provider A3") && Restored.Contains("B2 b2required A3") && Restored.Contains("B2 b2optional A1"), "restored provider only receives future death");
            Value.Frame++; Value.Emit(Snapshot(Id: "604")); FlushNative(Value);
            long Generation = Value.Host.VmGenerationId;
            Check(Value.Host.Execute("entitydied.dependencies.timeout", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT &&
                Value.Host.Ready && Value.Host.VmGenerationId != Generation && Value.Host.Recoveries == 1, "VM-fatal operation reconstructs shared dependency graph");
            Process(Registry);
            Check(Value.Drain() == "" && OptionalOwner.Disposed && Registry.Status(Consumers, Required)[2] == "Active" &&
                Registry.Status(FailedOwner, Failed)[2] == "Failed", "fresh VM retires captured work and does not activate failed addon");
            Value.Frame++; Value.Emit(Snapshot(Id: "605")); string Recovered = Value.Drain();
            // Optional edges do not order reconstruction. These IDs sort before
            // b2provider, so their unconditional require fails in the fresh VM.
            // Later provider activation cannot hot-bind or retry those domains.
            string[] OptionalStatus = Registry.Status(Consumers, OptionalToken), ForeignStatus = Registry.Status(ForeignOwner, ForeignToken);
            Check(Recovered.Contains("B2 provider A3") && Recovered.Contains("B2 b2required A3") &&
                !Recovered.Contains("B2 b2optional") && !Recovered.Contains("B2 foreign") &&
                OptionalStatus[2] == "Failed" && OptionalStatus[5].Contains("DEPENDENCY_UNAVAILABLE") &&
                ForeignStatus[2] == "Failed" && ForeignStatus[5].Contains("DEPENDENCY_UNAVAILABLE"),
                "optional consumers requiring an unavailable dependency fail without implicit reorder/rebind/retry; actual='" +
                Recovered.Replace("\r", "\\r").Replace("\n", "\\n") + "'; optional=" + String.Join("|", Registry.Status(Consumers, OptionalToken)) +
                "; foreign=" + String.Join("|", Registry.Status(ForeignOwner, ForeignToken)));
            Check(Registry.ReplaceArchive(Consumers, OptionalToken, Consumer("b2optional", true, false))[0] == "OK",
                "explicit optional consumer replacement admitted after provider activation"); Process(Registry);
            Check(Registry.ReplaceArchive(ForeignOwner, ForeignToken, Consumer("b2foreign", true, true))[0] == "OK",
                "explicit foreign consumer replacement admitted after provider activation"); Process(Registry);
            Check(Registry.Status(Consumers, OptionalToken)[2] == "Active" && Registry.Status(ForeignOwner, ForeignToken)[2] == "Active" &&
                Session(Value.World, "b2providerstate").GameplayEntityDiedListeners == 2 &&
                Session(Value.World, "b2foreignstate").GameplayEntityDiedListeners == 0 && Value.Drain() == "",
                "explicit fresh consumers bind available A3 while foreign subscriptions remain provider-owned and do not replay");
            Value.Frame++; Value.Emit(Snapshot(Id: "606")); string Replaced = Value.Drain();
            Check(Replaced.Contains("B2 provider A3") && Replaced.Contains("B2 b2required A3") &&
                Replaced.Contains("B2 b2optional A3") && Replaced.Contains("B2 foreign A3"),
                "explicitly replaced optional/foreign consumers receive future observations in the recovered VM; actual='" +
                Replaced.Replace("\r", "\\r").Replace("\n", "\\n") + "'");
            foreach (var Owner in new[] { Provider, Consumers, FailedOwner, ForeignOwner }) Registry.UnloadProvider(Owner);
            Check(Registry.Count == 0 && Value.Budget.PendingCount == 0, "dependency/provider teardown releases every owned callback");
        }
    }
    private static void NativeSharedValues(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            const string UserId = "76561190000999555";
            var Killer = Value.Connect(UserId, "Shared killer");
            object Producer = new object(), Holder = new object();
            Register(Registry, Producer, Package("b2snapshot",
                "local Api=require('api'); game:GetService('Workspace').EntityDied:Connect(function(C) Api.Last=C end)",
                ",\"main\":\"api\"", "return {}"));
            Register(Registry, Holder, Package("b2holder",
                "local Api=require('@b2snapshot'); local Saved=nil; game:GetService('Commands'):Register('b2shared',{},function() " +
                "if Saved==nil then Saved=Api.Last; assert(Saved.Id=='700' and Saved.KillerId=='" + UserId + "'); " +
                "assert(Saved.Killer.IsConnected); Saved.Killer:SendMessage('before owner retirement') else " +
                "assert(table.isfrozen(Saved) and Saved.Id=='700' and Saved.Prefab=='assets/fixture.prefab'); " +
                "assert(Saved.Position==Vector3.new(1,2,3) and Saved.DamageType=='Bullet' and Saved.Weapon=='assets/weapon.prefab'); " +
                "assert(not pcall(function() Saved.Killer:SendMessage('after owner retirement') end)) end; print('B2 shared') end)",
                ",\"dependencies\":{\"required\":[],\"optional\":[\"b2snapshot\"]}"));
            Value.Emit(new Runtime.GameplayEntityDiedObservation("700", "assets/fixture.prefab", 700,
                new Runtime.PlayerPosition(1, 2, 3), () => true, Killer.Token, Killer.UserId, Killer.Name, "Bullet", "assets/weapon.prefab"));
            Value.Drain(); var Consumer = Session(Value.World, "b2shared");
            Check(Consumer.Invoke("b2shared", UserId, new string[0]) && Value.Drain() == "B2 shared\n" && Value.Messages == 1,
                "frozen context crosses domain as ordinary value; killer retains defining provider authority");
            Registry.UnloadProvider(Producer); Process(Registry);
            Check(Object.ReferenceEquals(Consumer, Session(Value.World, "b2shared")) && Consumer.Invoke("b2shared", UserId, new string[0]) &&
                Value.Drain() == "B2 shared\n" && Value.Messages == 1, "retained scalar/Vector3 context survives producer retirement while old Player mutation stales");
            Registry.UnloadProvider(Holder); Check(Registry.Count == 0, "shared value provider teardown");
        }
    }
    private static void NativeMixed(Runtime.NativeRuntime Native)
    {
        using (var Value = new EntitySpawnSignalTests.Fixture(Native)) {
            const string UserId = "76561190000999333";
            var View = new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = UserId, Name = "B2 mixed", Connected = true };
            Value.Players.Add(UserId, View); var Player = Value.World.Players.Connect(View);
            Value.Source = "local W=game:GetService('Workspace'); local P=game:GetService('Players'); " +
                "W.EntityDied:Connect(function(C) assert(table.isfrozen(C)); print('B2 mixed death') end); " +
                "W.EntitySpawned:Connect(function() print('B2 mixed spawn') end); W.EntityDestroyed:Connect(function() print('B2 mixed removal') end); " +
                "P.PlayerDied:Connect(function() print('B2 mixed player death') end); P.PlayerSpawned:Connect(function() print('B2 mixed player spawn') end); " +
                "game:GetService('Commands'):Register('b2fair',{},function() print('B2 mixed command') end); task.defer(function() print('B2 mixed deferred') end)";
            Check(Value.Host.Reload().Status == Runtime.RuntimeStatus.OK, "five-stream mixed source activation");
            var Observation = Snapshot(Id: "800");
            for (int Index = 0; Index < 128; Index++) {
                Check(Value.World.GameplayEvents.Capture("entitydied"), "mixed producer bound"); Value.World.EntityDied(Observation);
            }
            foreach (string Kind in new[] { "died", "spawned", "entityspawned", "entitydestroyed" })
                Check(!Value.World.GameplayEvents.Capture(Kind), "all streams share entity death producer ceiling");
            Check(Value.World.Active.Invoke("b2fair", UserId, new string[0]), "command admitted alongside saturated death");
            string Saturated = Value.Drain();
            Check(Saturated.Contains("B2 mixed command\n") && Saturated.Contains("B2 mixed deferred\n"), "commands/deferred scheduler work progresses beside entity death saturation");
            Value.Frame++;
            foreach (string Kind in new[] { "died", "spawned" }) {
                Check(Value.World.GameplayEvents.Capture(Kind), "fresh mixed Player capture"); Value.World.GameplayEvent(Kind, Player.Token, Player.UserId, Player.Name, null);
            }
            Value.Spawn(801);
            Check(Value.World.GameplayEvents.Capture("entitydestroyed"), "fresh mixed removal capture");
            Value.World.EntityDestroyed(new Runtime.GameplayEntityDestroyedObservation("802", "assets/fixture.prefab", 802, null, () => true));
            Check(Value.World.GameplayEvents.Capture("entitydied"), "fresh mixed Entity death capture"); Value.World.EntityDied(Observation);
            string Mixed = Value.Drain();
            foreach (string Marker in new[] { "death", "spawn", "removal", "player death", "player spawn" })
                Check(Mixed.Contains("B2 mixed " + Marker + "\n"), "all five typed streams coexist with unchanged old transport");
            Check(Mixed.IndexOf("B2 mixed removal\n", StringComparison.Ordinal) < Mixed.IndexOf("B2 mixed death\n", StringComparison.Ordinal),
                "snapshot-only death can arrive after removal without a live Entity binding");
        }
    }
    private static void NativeScale(Runtime.NativeRuntime Native)
    {
        using (var Value = new NativeFixture(Native)) using (var Registry = new Runtime.AddonRegistry(Value.Host, Native.HostLifetimeId)) {
            Value.Execute("for Index=1,127 do game:GetService('Workspace').EntityDied:Connect(function(C) assert(C.Id~=nil) end) end");
            var Providers = new[] { new object(), new object(), new object(), new object() };
            var Names = new HashSet<string>(); var Tokens = new List<string>();
            for (int Index = 0; Index < 100; Index++) {
                string Name = "b2a" + Index.ToString("D3", CultureInfo.InvariantCulture); Names.Add(Name);
                Tokens.Add(Register(Registry, Providers[Index / 25], Package(Name)));
            }
            Check(Native.LiveVmCount == 1 && Value.Host.DomainCount == 101, "100 addons share one real VM with saturated root");
            var Progress = new HashSet<string>(); ulong Peak = Value.Host.VmMemoryBytes;
            for (int Frame = 0; Frame < 25; Frame++) {
                Value.Frame++; Value.Emit(Snapshot(Id: (900 + Frame).ToString(CultureInfo.InvariantCulture), Epoch: (ulong)(900 + Frame)));
                Check(Value.World.Active.PendingCount <= 32 && Value.Budget.PendingCount <= 128 && Value.Budget.RetainedBytes <= 2 * 1024 * 1024,
                    "root/domain/global event bounds never multiply by addon count");
                FlushNative(Value); Check(Value.Host.SchedulerSnapshot.Queued <= 128, "native admission retains shared global ceiling");
                foreach (string Line in Value.Drain().Split('\n')) if (Line.StartsWith("B2 ", StringComparison.Ordinal)) Progress.Add(Line.Substring(3));
                Peak = Math.Max(Peak, Value.Host.VmMemoryBytes);
            }
            Check(Progress.SetEquals(Names), "all 100 single-listener addons progress beside saturated 128-listener root");
            Value.Frame++; Value.Emit(); FlushNative(Value); Registry.UnloadProvider(Providers[0]); string Remaining = Value.Drain();
            for (int Index = 0; Index < 25; Index++) Check(!Remaining.Contains("B2 b2a" + Index.ToString("D3", CultureInfo.InvariantCulture) + "\n"),
                "provider unload cancels its exact native-held domain snapshots");
            for (int Turn = 0; Turn < 10; Turn++) {
                Value.Frame++; Value.Emit(); FlushNative(Value);
                Check(Registry.ReplaceArchive(Providers[1], Tokens[25], Package("b2a025"))[0] == "OK", "repeated addon replacement admitted"); Process(Registry);
                Check(Registry.Status(Providers[1], Tokens[25])[2] == "Active" && !Value.Drain().Contains("B2 b2a025\n"),
                    "replacement never inherits already queued old snapshot");
            }
            Value.Frame++;
            for (int Index = 0; Index < 512; Index++) Check(Value.Emit() == (Index < 128), "512-attempt producer burst admits only 128 captures");
            Check(Value.Budget.PendingCount <= 128 && Value.Budget.ProducerRejected > 0 && Value.Budget.FanoutRejected > 0, "overload drops are counted without retry queue");
            Value.Drain(); foreach (var Provider in Providers) Registry.UnloadProvider(Provider);
            Check(Registry.Count == 0 && Value.Budget.PendingCount == 0 && Value.Budget.RetainedBytes == 0, "scale/provider teardown converges all transport accounting");
            Console.WriteLine("[CarbonLuau:EntityDiedScale] PASS 100 addons, saturated root,25 frames,512 producer burst,10 replacements and native cancellation; peak_vm=" + Peak);
        }
    }
    private static void Fairness()
    {
        using (var Value = new Fixture()) {
            var Progress = new HashSet<long>();
            for (int Domain = 0; Domain < 100; Domain++) Value.Add(32);
            for (int Frame = 0; Frame < 25; Frame++) {
                Value.Frame++; Value.Emit(); int Delivered = 0;
                foreach (var Owner in Value.Owners) {
                    var Items = Value.Take(Owner); Delivered += Items.Count;
                    if (Items.Count != 0) {
                        Check(Items.Count == 32, "per-domain saturated fanout bound"); Progress.Add(Owner.DomainLifetimeId);
                    }
                    foreach (var Item in Items) Check(Host(Owner, 9, Item.Fields) && Value.Budget.Release(Owner, Item.Nonce), "fair-domain admission and consumption");
                }
                Check(Delivered == 128 && Value.Budget.PendingCount == 0 && Value.Budget.RetainedBytes == 0,
                    "100 domains share existing global delivery and exact release accounting");
            }
            Check(Progress.Count == 100 && Value.Budget.FanoutRejected != 0, "existing round-robin cursor progresses every saturated domain");
        }
    }
}
