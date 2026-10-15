using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

// Qualification of the PRIVATE sampled candidate only. These fixtures do not
// prove Rust operation completion, host topology or public Signal delivery.
internal static class InventoryNetObservationTests
{
    [DllImport("libc")] private static extern int chmod(string Path, int Mode);

    private sealed class Definition
    {
        internal string Name;
        internal Definition(string Name) { this.Name = Name; }
    }

    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        internal Action<Runtime.FacadeSession, Runtime.FacadeSession> DuringPublish;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        { if (DuringPublish != null) DuringPublish(Previous, Next); }
    }

    private sealed class Player
    {
        internal readonly object[] Containers = { new object(), new object(), new object() };
        internal readonly List<Runtime.PhysicalInventoryStack>[] Stacks = {
            new List<Runtime.PhysicalInventoryStack>(), new List<Runtime.PhysicalInventoryStack>(),
            new List<Runtime.PhysicalInventoryStack>() };
        internal Runtime.PlayerView View;
        internal Runtime.PlayerLifetime Lifetime;
        internal int Scans, Reads;
        internal Action DuringRead;
        internal void Clear() { foreach (var Values in Stacks) Values.Clear(); }
        internal void Add(object Definition, long Amount, int Container = 0, bool Valid = true, object Parent = null)
        {
            Stacks[Container].Add(new Runtime.PhysicalInventoryStack(Parent ?? Containers[Container], Definition,
                Amount, Valid, new object(), Stacks[Container].Count));
        }
        internal void Set(object Definition, long Amount)
        { Clear(); if (Amount != 0) Add(Definition, Amount); }
        internal Runtime.PhysicalInventorySource Source()
        {
            Scans++;
            var Values = new Runtime.PhysicalInventoryContainer[3];
            for (int Index = 0; Index < Values.Length; Index++) {
                int Slot = Index;
                Values[Index] = new Runtime.PhysicalInventoryContainer {
                    Identity = Containers[Slot], Capacity = Slot == 0 ? 64 : 32, StackCount = Stacks[Slot].Count,
                    Read = Position => { Reads++; if (DuringRead != null) DuringRead(); return Stacks[Slot][Position]; } };
            }
            return new Runtime.PhysicalInventorySource { Main = Values[0], Belt = Values[1], Wear = Values[2] };
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Dictionary<string, Runtime.PlayerView> Views = new Dictionary<string, Runtime.PlayerView>(StringComparer.Ordinal);
        internal readonly Dictionary<string, object> Definitions = new Dictionary<string, object>(StringComparer.Ordinal);
        internal readonly Registrar Registrar = new Registrar();
        internal readonly Runtime.PlayerDirectory Players;
        internal readonly Runtime.FacadeWorld World;
        internal readonly List<Runtime.FacadeSession> Owners = new List<Runtime.FacadeSession>();
        internal long Frame, NextDomain = 10;
        internal int Lookups, Descriptions;
        internal Runtime.InventoryNetObservation Samples { get { return World.InventorySamples; } }
        internal Fixture()
        {
            Players = new Runtime.PlayerDirectory(Id => {
                Lookups++; Runtime.PlayerView View; return Views.TryGetValue(Id, out View) ? View : null;
            });
            World = new Runtime.FacadeWorld(Players, Registrar, new Runtime.ItemDirectory(Name => {
                object Value; return Definitions.TryGetValue(Name, out Value) ? Value : null;
            }, Value => {
                Descriptions++; var Label = Value as Definition;
                if (Label != null) return Label.Name;
                foreach (var Entry in Definitions) if (ReferenceEquals(Entry.Value, Value)) return Entry.Key;
                return null;
            }));
            World.GameplayEvents.FrameClock = () => Frame;
        }
        internal object Item(string Name)
        {
            object Value;
            if (!Definitions.TryGetValue(Name, out Value)) { Value = new Definition(Name); Definitions.Add(Name, Value); }
            return Value;
        }
        internal Player Connect(string Id = "42")
        {
            var Value = new Player();
            Value.View = new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = Id,
                Name = "Inventory fixture", Connected = true, Inventory = Value.Source };
            Views[Id] = Value.View; Value.Lifetime = Players.Connect(Value.View); return Value;
        }
        internal Runtime.FacadeSession Owner(bool Publish = true, bool Addon = false)
        {
            var Value = new Runtime.FacadeSession(World, 1, ++NextDomain, 256); Owners.Add(Value);
            if (Publish) {
                if (Addon) World.CommitAddon(null, Value);
                else { var Previous = World.Active; World.Commit(Value); World.Retire(Previous); }
            }
            return Value;
        }
        internal ulong Stage(Runtime.FacadeSession Owner, Player Player)
        { return Samples.Stage(Owner, Player.Lifetime.Token, Player.Lifetime.UserId); }
        internal void Step(Action BeforeTick = null)
        { Frame++; if (BeforeTick != null) BeforeTick(); Samples.Tick(Frame); }
        internal Runtime.InventoryQuantitySnapshot Snapshot(Player Player)
        { return Runtime.PhysicalInventoryObservation.Snapshot(Player.Source(), World.Items.Describe, World.Items.Resolve); }
        public void Dispose()
        {
            Registrar.DuringPublish = null;
            foreach (var Owner in World.Sessions()) World.Retire(Owner);
            foreach (var Owner in Owners) World.Retire(Owner);
            Samples.Dispose();
            Check(Samples.WatchCount == 0 && Samples.DemandCount == 0 && Samples.PendingCount == 0 &&
                Samples.AllocationAdmission == null, "fixture deterministic cleanup");
        }
    }

    private static void Check(bool Value, string Reason)
    { if (!Value) throw new Exception("[CarbonLuau:InventoryNetTest] " + Reason); }

    private static void Reject<T>(Action Action, string Reason) where T : Exception
    {
        try { Action(); }
        catch (T) { return; }
        throw new Exception("[CarbonLuau:InventoryNetTest] expected " + typeof(T).Name + ": " + Reason);
    }

    private static void Publication(Runtime.FacadeSession Owner, uint Code)
    {
        // Same dispatch used by the native publication callbacks; no new test API.
        typeof(Runtime.FacadeSession).GetMethod("Operation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Owner, new object[] { Code, new string[0] });
    }

    private static void Empty(Runtime.InventoryNetObservation Samples, ulong Demand, string Reason)
    { Runtime.InventoryQuantityChange Change; Check(!Samples.Consume(Demand, out Change), Reason); }

    private static void Change(Runtime.InventoryNetObservation Samples, ulong Demand, string Item, long Before, long After)
    {
        Runtime.InventoryQuantityChange Value;
        Check(Samples.Consume(Demand, out Value), "missing change " + Item);
        Check(Value.Item == Item && Value.Before == Before && Value.After == After && Value.Delta == After - Before,
            "scalar change " + Item + ": " + Before + " -> " + After);
    }

    private static void NoDifference(Runtime.InventoryQuantitySnapshot Before, Runtime.InventoryQuantitySnapshot After, string Reason)
    { Check(Runtime.InventoryQuantitySnapshot.Difference(Before, After).Length == 0, Reason); }

    private static void SnapshotRules()
    {
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); object Scrap = Value.Item("scrap"), Wood = Value.Item("wood");
            Check(ReferenceEquals(Value.World.Items.Resolve("scrap"), Scrap) && Value.World.Items.Resolve("missing") == null,
                "ItemDirectory lookup and Describe bound together");
            var EmptySnapshot = Value.Snapshot(Player);
            Check(EmptySnapshot.Length == 0 && EmptySnapshot.Count("scrap") == 0, "empty physical snapshot");
            NoDifference(EmptySnapshot, Value.Snapshot(Player), "empty difference");
            Reject<ArgumentNullException>(() => Runtime.InventoryQuantitySnapshot.Difference(null, EmptySnapshot), "null before");
            Reject<ArgumentNullException>(() => Runtime.InventoryQuantitySnapshot.Difference(EmptySnapshot, null), "null after");

            Player.Add(Scrap, 60); Player.Add(Scrap, 30, 1); Player.Add(Scrap, 10, 2);
            var Hundred = Value.Snapshot(Player);
            Check(Hundred.Length == 1 && Hundred.Count("scrap") == 100, "all three top-level containers");
            Player.Clear(); Player.Add(Scrap, 100, 1);
            NoDifference(Hundred, Value.Snapshot(Player), "same-player main/belt/wear transfer");
            Player.Clear(); Player.Add(Scrap, 40); Player.Add(Scrap, 30); Player.Add(Scrap, 30, 2);
            NoDifference(Hundred, Value.Snapshot(Player), "stack split");
            Player.Set(Scrap, 100);
            NoDifference(Hundred, Value.Snapshot(Player), "stack merge");
            Player.Set(Scrap, 110); Player.Set(Scrap, 100);
            NoDifference(Hundred, Value.Snapshot(Player), "+10/-10 between samples intentionally invisible");
            Player.Set(Scrap, 80);
            var Used = Runtime.InventoryQuantitySnapshot.Difference(Hundred, Value.Snapshot(Player));
            Check(Used.Length == 1 && Used[0].Item == "scrap" && Used[0].Before == 100 && Used[0].After == 80 &&
                Used[0].Delta == -20, "direct amount write/consumption sampled delta");
            Player.Set(Scrap, 50);
            var Dropped = Runtime.InventoryQuantitySnapshot.Difference(Hundred, Value.Snapshot(Player));
            Check(Dropped.Length == 1 && Dropped[0].Delta == -50, "drop/removal sampled delta");

            Player.Set(Scrap, 7); Player.Add(Scrap, 999, 0, false); Player.Add(Scrap, 999, 1, true, new object());
            Player.Add(Scrap, 0, 2); Player.Add(Scrap, -5); Player.Add(null, 900, 1, false);
            var Filtered = Value.Snapshot(Player);
            Check(Filtered.Count("scrap") == 7 && Runtime.PhysicalInventoryObservation.Count(Player.Source(), Scrap) == 7,
                "invalid/removal-pending/nonreciprocal/zero/negative same-item acceptance parity");
            Player.Add(null, 1);
            Reject<Runtime.FacadeException>(() => Value.Snapshot(Player), "accepted missing definition");
            Player.Set(Scrap, 1); Player.Add(new Definition("scrap"), 999, 1);
            Check(Value.Snapshot(Player).Count("scrap") == 1 && Runtime.PhysicalInventoryObservation.Count(Player.Source(), Scrap) == 1,
                "cloned alias label excluded by exact ItemDirectory definition, matching CountItem");
            Player.Set(new Definition("scrap"), 999);
            var CloneOnly = Value.Snapshot(Player);
            Check(CloneOnly.Length == 0 && CloneOnly.Count("scrap") == 0 && Runtime.PhysicalInventoryObservation.Count(Player.Source(), Scrap) == 0,
                "clone-only canonical label contributes no quantity");
            NoDifference(EmptySnapshot, CloneOnly, "cloned label never fabricates gain");
            Player.Set(new Definition("unknown"), 10);
            Check(Value.Snapshot(Player).Length == 0, "Describe label with missing resolver definition excluded");
            var Ambiguous = new Runtime.InventoryQuantitySnapshot(); var DefinitionSlots = new object[128];
            Ambiguous.Add("scrap", Scrap, 1, DefinitionSlots);
            Reject<Runtime.FacadeException>(() => Ambiguous.Add("scrap", new Definition("scrap"), 1, DefinitionSlots),
                "snapshot accumulator fails closed on inconsistent same-name definition");

            Player.Clear();
            for (int Index = 127; Index >= 0; Index--) Player.Add(Value.Item("a" + Index.ToString("D3", CultureInfo.InvariantCulture)), Index + 1);
            int Reads = Player.Reads;
            var Many = Value.Snapshot(Player);
            Check(Many.Length == 128 && Player.Reads - Reads == 128, "128-stack complete inspection");
            var Added = Runtime.InventoryQuantitySnapshot.Difference(EmptySnapshot, Many);
            Check(Added.Length == 128, "many keys");
            for (int Index = 0; Index < Added.Length; Index++) Check(Added[Index].Item == "a" + Index.ToString("D3", CultureInfo.InvariantCulture) &&
                Added[Index].Before == 0 && Added[Index].After == Index + 1, "ordinal order and exact many-key values");
            Player.Add(Scrap, 1, 2, false); Reads = Player.Reads; int Descriptions = Value.Descriptions;
            Reject<Runtime.FacadeException>(() => Value.Snapshot(Player), "129 preflight including invalid stacks");
            Check(Player.Reads == Reads && Value.Descriptions == Descriptions, "129 rejection before any item read/Describe");
            Player.Clear();
            for (int Index = 0; Index < 128; Index++) Player.Add(Value.Item("b" + Index.ToString("D3", CultureInfo.InvariantCulture)), Index + 2);
            var Union = Runtime.InventoryQuantitySnapshot.Difference(Many, Value.Snapshot(Player));
            Check(Union.Length == 256, "two disjoint 128-key snapshots bounded union");
            for (int Index = 0; Index < 128; Index++) Check(Union[Index].Delta == -(Index + 1) && Union[Index + 128].Delta == Index + 2,
                "all removals and additions in 256-key union");

            long Maximum = Runtime.FacadePolicy.MaxExactLuauInteger;
            Player.Set(Scrap, Maximum - 1); Player.Add(Scrap, 1, 1);
            var Exact = Value.Snapshot(Player);
            Check(Exact.Count("scrap") == Maximum, "exact integer inclusive maximum");
            Check(Runtime.InventoryQuantitySnapshot.Difference(EmptySnapshot, Exact)[0].Delta == Maximum &&
                Runtime.InventoryQuantitySnapshot.Difference(Exact, EmptySnapshot)[0].Delta == -Maximum, "signed exact integer extremes");
            Player.Add(Scrap, 1, 2);
            Reject<Runtime.FacadeException>(() => Value.Snapshot(Player), "aggregate exact integer overflow");
            foreach (long Amount in new[] { Maximum + 1, Int64.MaxValue }) {
                Player.Set(Scrap, Amount); Reject<Runtime.FacadeException>(() => Value.Snapshot(Player), "single oversized amount");
            }
            foreach (string Name in new[] { null, "", "Scrap", " scrap", "scrap ", "s\0x", "é", new string('x', 129) }) {
                Player.Set(new Definition(Name), 1);
                Reject<Runtime.FacadeException>(() => Value.Snapshot(Player), "malformed host label");
                Reject<Runtime.FacadeException>(() => Exact.Count(Name), "malformed count label");
            }
            Player.Set(Value.Item(new string('x', 128)), 1);
            Check(Value.Snapshot(Player).Length == 1, "128-byte label inclusive");

            Player.Set(Wood, 3); var Source = Player.Source(); Reads = Player.Reads;
            Source.Wear = null;
            Reject<Runtime.FacadeException>(() => Runtime.PhysicalInventoryObservation.Snapshot(Source, Value.World.Items.Describe, Value.World.Items.Resolve), "missing wear preflight");
            Check(Player.Reads == Reads, "all containers validated before first read");
            Source = Player.Source(); Source.Belt.StackCount = -1;
            Reject<Runtime.FacadeException>(() => Runtime.PhysicalInventoryObservation.Snapshot(Source, Value.World.Items.Describe, Value.World.Items.Resolve), "negative count");
            Source = Player.Source(); Source.Belt.StackCount = Int32.MaxValue;
            Reject<Runtime.FacadeException>(() => Runtime.PhysicalInventoryObservation.Snapshot(Source, Value.World.Items.Describe, Value.World.Items.Resolve), "inspection addition overflow/bound");
            Reject<Runtime.FacadeException>(() => Runtime.PhysicalInventoryObservation.Snapshot(null, Value.World.Items.Describe, Value.World.Items.Resolve), "missing source");
            Reject<Runtime.FacadeException>(() => Runtime.PhysicalInventoryObservation.Snapshot(Player.Source(), null, Value.World.Items.Resolve), "missing Describe");
            Reject<Runtime.FacadeException>(() => Runtime.PhysicalInventoryObservation.Snapshot(Player.Source(), Value.World.Items.Describe, null), "missing Resolve");
            Source = Player.Source(); Source.Main.Read = Index => { throw new InvalidOperationException("fixture read failure"); };
            Reject<InvalidOperationException>(() => Runtime.PhysicalInventoryObservation.Snapshot(Source, Value.World.Items.Describe, Value.World.Items.Resolve), "host read failure escapes snapshot only");
        }
    }

    private static void LifecycleAndPublication()
    {
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); object Scrap = Value.Item("scrap"); Player.Set(Scrap, 3);
            var Candidate = Value.Owner(false); ulong Staged = Value.Stage(Candidate, Player);
            Empty(Value.Samples, 0, "Consume zero with empty queue");
            Check(Value.Samples.PendingCount == 0, "Consume(0) cannot underflow pending accounting");
            Value.Step(); Check(Player.Scans == 0 && Value.Samples.Scans == 0, "staged candidate zero scans");
            Value.Samples.Cancel(Staged); Value.Samples.Cancel(Staged);
            Check(Value.Samples.DemandCount == 0 && Value.Samples.WatchCount == 0, "Cancel idempotent and releases last watch");
            Value.World.Commit(Candidate); ulong First = Value.Stage(Candidate, Player);
            Value.Step(); Empty(Value.Samples, First, "first committed sample has no history");
            Check(Value.Samples.Baselines == 1, "initial baseline counter");
            Player.Set(Scrap, 13); Value.Step();
            int Pending = Value.Samples.PendingCount; Empty(Value.Samples, 0, "Consume zero with live queue");
            Check(Value.Samples.PendingCount == Pending, "Consume(0) leaves live pending records unchanged");
            ulong Second = Value.Stage(Candidate, Player);
            Empty(Value.Samples, Second, "new subscriber receives no queued backlog");
            Change(Value.Samples, First, "scrap", 3, 13);
            Player.Set(Scrap, 23); Value.Step(); Change(Value.Samples, First, "scrap", 13, 23);
            Empty(Value.Samples, Second, "new subscriber skips pre-first-sample interval");
            Player.Set(Scrap, 24); Value.Step();
            Change(Value.Samples, First, "scrap", 23, 24); Change(Value.Samples, Second, "scrap", 23, 24);
            Value.Samples.Cancel(Second);
            Check(Value.Samples.WatchCount == 1 && Value.Samples.DemandCount == 1, "shared watch retained for original subscriber");

            Publication(Candidate, 10); ulong Outer = Value.Stage(Candidate, Player);
            Publication(Candidate, 10); ulong Inner = Value.Stage(Candidate, Player);
            Publication(Candidate, 11); Player.Set(Scrap, 25); Value.Step();
            Change(Value.Samples, First, "scrap", 24, 25);
            Empty(Value.Samples, Outer, "outer provisional receives no records");
            Empty(Value.Samples, Inner, "inner commit cannot escape open parent publication");
            Publication(Candidate, 12);
            Check(Value.Samples.DemandCount == 1, "rollback synchronously prunes nested demands before next Tick");
            Value.Step(); Check(Value.Samples.DemandCount == 1, "outer rollback preserves original");
            Publication(Candidate, 10); ulong Committed = Value.Stage(Candidate, Player);
            Value.Step(); Empty(Value.Samples, Committed, "open publication no sample admission");
            Publication(Candidate, 11); Value.Step();
            Player.Set(Scrap, 26); Value.Step();
            Change(Value.Samples, First, "scrap", 25, 26); Change(Value.Samples, Committed, "scrap", 25, 26);

            var Failed = Value.Owner(false); ulong FailedDemand = Value.Stage(Failed, Player);
            Value.World.Retire(Failed); Player.Set(Scrap, 27); Value.Step();
            Empty(Value.Samples, FailedDemand, "failed replacement cannot consume original demand");
            Change(Value.Samples, First, "scrap", 26, 27); Change(Value.Samples, Committed, "scrap", 26, 27);
            var Addon = Value.Owner(true, true); ulong AddonDemand = Value.Stage(Addon, Player); Value.Step();
            Player.Set(Scrap, 28); Value.Step();
            Value.World.Retire(Candidate);
            Empty(Value.Samples, First, "root retirement cancels queued root records");
            Check(Value.Samples.DemandCount == 1 && Value.Samples.WatchCount == 1, "addon retains independent exact watch");
            Change(Value.Samples, AddonDemand, "scrap", 27, 28);
            Value.World.Retire(Addon);
            Check(Value.Samples.PendingCount == 0 && Value.Samples.WatchCount == 0, "addon retirement cleanup");
            Reject<Runtime.FacadeException>(() => Value.Stage(Addon, Player), "disposed owner Stage");
            Reject<Runtime.FacadeException>(() => Value.Samples.Stage(null, Player.Lifetime.Token, "42"), "null owner");
            using (var Foreign = new Fixture()) {
                var ForeignOwner = Foreign.Owner();
                Reject<Runtime.FacadeException>(() => Value.Stage(ForeignOwner, Player), "foreign world owner");
            }
        }
    }

    private static void ExactLifetimesAndFailures()
    {
        using (var Value = new Fixture()) {
            Player Original = Value.Connect(); object Scrap = Value.Item("scrap"); Original.Set(Scrap, 1);
            var Owner = Value.Owner(); ulong Old = Value.Stage(Owner, Original); Value.Step();
            Original.Set(Scrap, 2); Value.Step(); Original.View.Connected = false;
            Empty(Value.Samples, Old, "Consume rechecks exact disconnected connection");
            Player Next = Value.Connect(); Next.Set(Scrap, 200); Value.Step();
            Check(Value.Samples.DemandCount == 0 && Value.Samples.WatchCount == 0, "reconnect retires stale original demand");
            Reject<Runtime.FacadeException>(() => Value.Stage(Owner, Original), "same UserId cannot retarget old token");
            ulong Current = Value.Stage(Owner, Next); Value.Step(); Empty(Value.Samples, Current, "reconnect has fresh baseline");
            Next.Set(Scrap, 201); Value.Step(); Change(Value.Samples, Current, "scrap", 200, 201);
            Next.View.Connection = new object(); Value.Step();
            Check(Value.Samples.DemandCount == 0, "same account/player with changed connection is stale");
            Next = Value.Connect(); Next.Set(Scrap, 4); Current = Value.Stage(Owner, Next); Value.Step();
            Next.View.Identity = new object(); Value.Step(); Check(Value.Samples.DemandCount == 0, "changed host identity is stale");
            Next = Value.Connect(); Next.Set(Scrap, 5); Current = Value.Stage(Owner, Next); Value.Step();
            Player Reading = Next;
            Reading.DuringRead = () => { Reading.View.Connected = false; Reading.DuringRead = null; };
            Reading.Set(Scrap, 6); ulong Invalid = Value.Samples.InvalidSnapshots; Value.Step();
            Check(Value.Samples.InvalidSnapshots == Invalid + 1, "disconnect during scan rejects sampled interval");
            Empty(Value.Samples, Current, "no record from invalid post-scan lifetime"); Value.Step();
            Check(Value.Samples.DemandCount == 0, "late stale scan cleans up on next tick");

            Next = Value.Connect(); Next.Set(Scrap, 10); Current = Value.Stage(Owner, Next); Value.Step();
            Next.Set(new Definition("Scrap"), 99); Value.Step();
            Empty(Value.Samples, Current, "malformed label invalidates interval");
            Next.Set(Scrap, 100); Value.Step(); Empty(Value.Samples, Current, "valid recovery rebaselines without fabricated gain");
            Next.Set(Scrap, 101); Value.Step(); Change(Value.Samples, Current, "scrap", 100, 101);
            Next.View.Inventory = () => { throw new InvalidOperationException("private fixture host failure"); };
            Value.Step(); Empty(Value.Samples, Current, "Tick contains host exception");
            Next.View.Inventory = Next.Source; Next.Set(Scrap, 1000); Value.Step();
            Empty(Value.Samples, Current, "host failure gap not bridged");
            Next.Set(Scrap, 1001); Value.Step(); Change(Value.Samples, Current, "scrap", 1000, 1001);

            int Scans = Next.Scans, Lookups = Value.Lookups; Exception Failure = null;
            var Thread = new Thread(() => { try { Value.Samples.Tick(99999); } catch (Exception Error) { Failure = Error; } });
            Thread.IsBackground = true; Thread.Start(); Check(Thread.Join(5000), "wrong-thread qualification bounded join");
            Check(Failure is Runtime.FacadeException && Next.Scans == Scans && Value.Lookups == Lookups,
                "wrong-thread rejected before host lookup or read");
            Value.Samples.Cancel(Current); Value.Samples.Dispose(); Value.Samples.Dispose();
            Reject<Runtime.FacadeException>(() => Value.Samples.Tick(Value.Frame + 1), "closed Tick");
            Reject<Runtime.FacadeException>(() => Value.Stage(Owner, Next), "closed Stage");
            Reject<Runtime.FacadeException>(() => Value.Samples.Cancel(Current), "closed Cancel");
            Reject<Runtime.FacadeException>(() => Empty(Value.Samples, Current, "closed"), "closed Consume");
        }
        // Lack of Describe must fail with a live, otherwise valid original Player.
        var Live = new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = "7", Name = "missing adapter", Connected = true };
        var Directory = new Runtime.PlayerDirectory(Id => Live); var Lifetime = Directory.Connect(Live);
        var World = new Runtime.FacadeWorld(Directory, new Registrar(), new Runtime.ItemDirectory(Name => null));
        var Session = new Runtime.FacadeSession(World, 1, 2, 256); World.Commit(Session);
        try {
            Reject<Runtime.FacadeException>(() => World.InventorySamples.Stage(Session, Lifetime.Token, "7"), "Describe required at Stage");
            Check(World.InventorySamples.WatchCount == 0 && World.InventorySamples.DemandCount == 0, "unavailable adapter leaves no admission");
        } finally { World.Retire(Session); World.InventorySamples.Dispose(); }
    }

    private static void BoundsAndFairness()
    {
        Check(Runtime.InventoryNetObservationPolicy.Players == 64 && Runtime.InventoryNetObservationPolicy.Demands == 128 &&
            Runtime.InventoryNetObservationPolicy.ScansPerFrame == 4 && Runtime.InventoryNetObservationPolicy.RecordsPerFrame == 32 &&
            Runtime.InventoryNetObservationPolicy.Pending == 256 && Runtime.GameplayEventPolicy.VisitsPerFrame == 4096,
            "qualified hard bounds");
        using (var Value = new Fixture()) {
            var Owner = Value.Owner(); var Players = new Player[65]; var Demands = new ulong[64]; object Scrap = Value.Item("scrap");
            for (int Index = 0; Index < Players.Length; Index++) {
                Players[Index] = Value.Connect((Index + 1).ToString(CultureInfo.InvariantCulture));
                Players[Index].Set(Scrap, 1);
                if (Index < Demands.Length) Demands[Index] = Value.Stage(Owner, Players[Index]);
            }
            Reject<Runtime.FacadeException>(() => Value.Stage(Owner, Players[64]), "65th distinct watch");
            var Duplicates = new ulong[64];
            for (int Index = 0; Index < Duplicates.Length; Index++) Duplicates[Index] = Value.Stage(Owner, Players[Index]);
            Reject<Runtime.FacadeException>(() => Value.Stage(Owner, Players[0]), "129th demand on existing watch");
            Check(Value.Samples.WatchCount == 64 && Value.Samples.DemandCount == 128, "deduplicated 64 watches/128 demands");
            for (int Frame = 0; Frame < 16; Frame++) {
                Value.Step(); Check(Value.Samples.LastScans == 4, "four scans per frame");
                ulong Scans = Value.Samples.Scans; int Lookups = Value.Lookups;
                Value.Samples.Tick(Value.Frame);
                Check(Value.Samples.Scans == Scans && Value.Lookups == Lookups, "same frame idempotent before host reads");
            }
            for (int Index = 0; Index < 64; Index++) Check(Players[Index].Scans == 1, "fair 64-watch first complete rotation");
            Check(Value.Samples.Scans == 64 && Value.Samples.PendingCount == 0 &&
                Value.Samples.ChargedBytes <= Runtime.InventoryNetObservationPolicy.RetainedChargeCeiling, "bounded retained charge at maximum demand");
            for (int Index = 0; Index < 64; Index++) Value.Samples.Cancel(Duplicates[Index]);
            Check(Value.Samples.WatchCount == 64 && Value.Samples.DemandCount == 64, "cancel duplicate preserves every original watch");
            for (int Index = 0; Index < 64; Index++) Value.Samples.Cancel(Demands[Index]);
            Check(Value.Samples.ChargedBytes == Runtime.InventoryNetObservationPolicy.SnapshotCharge, "all admission charges released except fixed scratch");
        }
        using (var Value = new Fixture()) {
            var Owner = Value.Owner(); var Players = new Player[5];
            for (int Index = 0; Index < Players.Length; Index++) { Players[Index] = Value.Connect((Index + 1).ToString()); Value.Stage(Owner, Players[Index]); }
            Value.Step(() => { for (int Index = 0; Index < 4096; Index++) Check(Value.World.GameplayEvents.Visit(), "shared event visits admitted"); });
            Check(Value.Samples.LastScans == 0, "shared 4096 visit exhaustion suppresses inventory scan");
            foreach (var Player in Players) Check(Player.Scans == 0, "exhaustion precedes physical access");
            Value.Step(() => { for (int Index = 0; Index < 4096 - 127; Index++) Value.World.GameplayEvents.Visit(); });
            Check(Value.Samples.LastScans == 0, "less than full 128 inspection charge cannot scan even empty inventory");
            Value.Step(() => { for (int Index = 0; Index < 4096 - 128; Index++) Value.World.GameplayEvents.Visit(); });
            Check(Value.Samples.LastScans == 1 && !Value.World.GameplayEvents.Visit(), "exact final 128 visits permit one scan only");
            Value.Step(); Check(Value.Samples.LastScans == 4, "new owner frame restores shared work budget");
        }
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); object Scrap = Value.Item("scrap"); Player.Set(Scrap, 1);
            var Owner = Value.Owner(); var Demands = new ulong[128]; var Served = new bool[128];
            for (int Index = 0; Index < Demands.Length; Index++) Demands[Index] = Value.Stage(Owner, Player);
            Value.Step();
            for (int Frame = 2; Frame <= 5; Frame++) {
                Player.Set(Scrap, Frame); Value.Step(); int Delivered = 0;
                for (int Index = 0; Index < Demands.Length; Index++) {
                    Runtime.InventoryQuantityChange Record;
                    if (!Value.Samples.Consume(Demands[Index], out Record)) continue;
                    Check(!Served[Index] && Record.Item == "scrap" && Record.Before == Frame - 1 && Record.After == Frame,
                        "fair bounded fanout never replays missed intervals");
                    Served[Index] = true; Delivered++;
                }
                Check(Delivered == 32, "shared per-frame record cap under 128-demand fanout");
            }
            foreach (bool ServedDemand in Served) Check(ServedDemand, "all 128 demands receive a fair fanout turn");
            Check(Value.Samples.Dropped == 384 && Value.Samples.PendingCount == 0, "fair fanout loss counted and consumed");
        }
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); var Owner = Value.Owner(); var Demands = new ulong[128];
            for (int Index = 0; Index < 128; Index++) {
                Player.Add(Value.Item("v" + Index.ToString("D3", CultureInfo.InvariantCulture)), 1);
                Demands[Index] = Value.Stage(Owner, Player);
            }
            Value.Step(); Player.Clear();
            for (int Index = 0; Index < 128; Index++) Player.Add(Value.Item("v" + Index.ToString("D3", CultureInfo.InvariantCulture)), 2);
            ulong Rejected = Value.World.GameplayEvents.ScanRejected;
            Value.Step(() => { for (int Index = 0; Index < 4096 - 128 - 3; Index++) Value.World.GameplayEvents.Visit(); });
            Check(Value.Samples.PendingCount == 3 && Value.Samples.Dropped == 128 * 128 - 3 && Value.World.GameplayEvents.ScanRejected == Rejected + 1,
                "visit exhaustion breaks 128-item by 128-demand fanout at first refused visit");
            int Consumed = 0;
            foreach (ulong Demand in Demands) {
                Runtime.InventoryQuantityChange Record;
                while (Value.Samples.Consume(Demand, out Record)) {
                    Check(Record.Before == 1 && Record.After == 2, "budget-limited record values"); Consumed++;
                }
            }
            Check(Consumed == 3, "exactly three remaining shared visits permit three records");
            Value.Step(); Check(Value.Samples.PendingCount == 0, "fanout exhaustion advances baseline without replay");
        }
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); var Owner = Value.Owner();
            ulong Original = Value.Stage(Owner, Player); // Default model pump availability is true.
            Value.Samples.PumpAvailable = () => false;
            int Charge = Value.Samples.ChargedBytes, Lookups = Value.Lookups;
            Reject<Runtime.FacadeException>(() => Value.Stage(Owner, Player), "unavailable actual-host pump admission");
            Check(Value.Samples.DemandCount == 1 && Value.Samples.WatchCount == 1 && Value.Samples.ChargedBytes == Charge &&
                Value.Lookups == Lookups, "absent pump refuses admission without damaging original demand or touching host");
            Value.Samples.PumpAvailable = () => true; ulong Again = Value.Stage(Owner, Player);
            Value.Step(); Value.Samples.Cancel(Original); Value.Samples.Cancel(Again);
            Check(Value.Samples.DemandCount == 0 && Value.Samples.WatchCount == 0, "available pump admission and cleanup");
        }
    }

    private static void QueueBoundsAndNoReplay()
    {
        foreach (int Keys in new[] {32, 64}) using (var Value = new Fixture()) {
            var Owner = Value.Owner(); var Players = new[] { Value.Connect("41"), Value.Connect("42") };
            var Handles = new ulong[2]; var Served = new int[2];
            for (int Index = 0; Index < 2; Index++) {
                for (int Key = 0; Key < Keys; Key++) Players[Index].Add(Value.Item("fair" + Key.ToString("D2")), 1);
                Handles[Index] = Value.Stage(Owner, Players[Index]);
            }
            Value.Step();
            for (int Frame = 2; Frame < 10; Frame++) {
                for (int Index = 0; Index < 2; Index++) {
                    Players[Index].Clear();
                    for (int Key = 0; Key < Keys; Key++) Players[Index].Add(Value.Item("fair" + Key.ToString("D2")), Frame);
                }
                Value.Step(); Check(Value.Samples.PendingCount == 32, "two busy Players share, never multiply record cap");
                int Expected = (Frame - 2) % 2;
                for (int Index = 0; Index < 2; Index++) {
                    Runtime.InventoryQuantityChange Record; int Records = 0;
                    while (Value.Samples.Consume(Handles[Index], out Record)) {
                        Check(Index == Expected && Record.After == Frame, "exhausted turn resumes at next Player, not first busy watch");
                        Records++;
                    }
                    Check(Records == (Index == Expected ? 32 : 0), "alternating bounded cross-Player service");
                    if (Records != 0) Served[Index]++;
                }
            }
            Check(Served[0] == 4 && Served[1] == 4, "continuous first-Player saturation cannot starve second Player");
        }
        using (var Value = new Fixture()) {
            var Owner = Value.Owner(); var Players = new[] { Value.Connect("41"), Value.Connect("42") };
            object Scrap = Value.Item("scrap");
            for (int Index = 0; Index < 2; Index++) { Players[Index].Set(Scrap, 1); Value.Stage(Owner, Players[Index]); }
            Value.Step(); int FirstScans = Players[0].Scans, SecondScans = Players[1].Scans;
            Players[0].Set(Scrap, 2); Players[1].Set(Scrap, 2);
            Value.Step(() => { for (int Visit = 0; Visit < 4096 - 129; Visit++) Value.World.GameplayEvents.Visit(); });
            Check(Players[0].Scans == FirstScans + 1 && Players[1].Scans == SecondScans, "exact inspection plus record budget serves first watch");
            Value.Step(() => { for (int Visit = 0; Visit < 4096 - 129; Visit++) Value.World.GameplayEvents.Visit(); });
            Check(Players[1].Scans == SecondScans + 1, "refused inspection leaves next watch first next frame");
        }
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); var Owner = Value.Owner(); ulong Demand = Value.Stage(Owner, Player);
            Value.Step();
            for (int Index = 127; Index >= 0; Index--) Player.Add(Value.Item("item" + Index.ToString("D3", CultureInfo.InvariantCulture)), 1);
            Value.Step(); Check(Value.Samples.PendingCount == 32 && Value.Samples.Dropped == 96, "128 deltas capped at 32 records/frame");
            for (int Index = 0; Index < 32; Index++) Change(Value.Samples, Demand, "item" + Index.ToString("D3", CultureInfo.InvariantCulture), 0, 1);
            Value.Step(); Empty(Value.Samples, Demand, "record cap advances complete snapshot baseline, dropped 96 never replay");
            Player.Set(Value.Item("item127"), 2); Value.Step();
            // 127 removals sort ahead of the last changed key; loss remains bounded.
            Check(Value.Samples.PendingCount == 32 && Value.Samples.Dropped == 192, "subsequent union also bounded");
            Value.Samples.Cancel(Demand); Check(Value.Samples.PendingCount == 0, "Cancel clears all private pending records");
        }
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); var Owner = Value.Owner(); ulong Demand = Value.Stage(Owner, Player);
            Action<int> State = Amount => {
                Player.Clear(); for (int Index = 0; Index < 32; Index++) Player.Add(Value.Item("q" + Index.ToString("D2", CultureInfo.InvariantCulture)), Amount);
            };
            State(1); Value.Step();
            for (int Frame = 2; Frame <= 9; Frame++) { State(Frame); Value.Step(); Check(Value.Samples.PendingCount == (Frame - 1) * 32, "pending grows to 256"); }
            Check(Value.Samples.PendingCount == 256 && Value.Samples.Dropped == 0 &&
                Value.Samples.ChargedBytes <= Runtime.InventoryNetObservationPolicy.RetainedChargeCeiling, "256 pending hard cap and charge");
            State(10); Value.Step(); Check(Value.Samples.PendingCount == 256 && Value.Samples.Dropped == 32, "full queue drops rather than expanding");
            for (int Frame = 2; Frame <= 9; Frame++) for (int Index = 0; Index < 32; Index++)
                Change(Value.Samples, Demand, "q" + Index.ToString("D2", CultureInfo.InvariantCulture), Frame - 1, Frame);
            Value.Step(); Empty(Value.Samples, Demand, "queue overflow never replays lost 9->10 interval");
            State(11); Value.Step();
            for (int Index = 0; Index < 32; Index++) Change(Value.Samples, Demand, "q" + Index.ToString("D2", CultureInfo.InvariantCulture), 10, 11);
            Empty(Value.Samples, Demand, "pending consumed exactly once");
        }
    }

    private static void AllocationFaults()
    {
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); var Owner = Value.Owner(); int Charge = Value.Samples.ChargedBytes;
            Value.Samples.AllocationAdmission = () => false;
            Reject<OutOfMemoryException>(() => Value.Stage(Owner, Player), "Stage allocation refusal");
            Check(Value.Samples.DemandCount == 0 && Value.Samples.WatchCount == 0 && Value.Samples.ChargedBytes == Charge,
                "failed Stage leaves all admission state unchanged");
            Value.Samples.AllocationAdmission = null; ulong Original = Value.Stage(Owner, Player);
            Check(Original == 1, "failed Stage does not burn demand identity");
            Value.Samples.AllocationAdmission = () => false;
            Reject<OutOfMemoryException>(() => Value.Stage(Owner, Player), "duplicate demand allocation refusal");
            Check(Value.Samples.DemandCount == 1 && Value.Samples.WatchCount == 1, "failed shared admission preserves original demand");
            Value.Samples.AllocationAdmission = null; Value.Step(); Value.Samples.Cancel(Original);
        }
        // Snapshot, difference, first enqueue and partial enqueue allocation positions.
        for (int Position = 1; Position <= 4; Position++) using (var Value = new Fixture()) {
            Player Player = Value.Connect(); var Owner = Value.Owner(); object Scrap = Value.Item("scrap"), Wood = Value.Item("wood");
            Player.Set(Scrap, 1); ulong Demand = Value.Stage(Owner, Player); Value.Step();
            Player.Set(Scrap, 2); Player.Add(Wood, 3); int Calls = 0;
            Value.Samples.AllocationAdmission = () => ++Calls != Position;
            Value.Step();
            Check(Calls == Position && Value.Samples.InvalidSnapshots == 1 && Value.Samples.DemandCount == 1,
                "allocation fault contained at bounded seam " + Position);
            if (Position == 4) Change(Value.Samples, Demand, "scrap", 1, 2);
            Empty(Value.Samples, Demand, "failed allocation does not manufacture further records");
            Value.Samples.AllocationAdmission = null;
            Player.Set(Scrap, 20); Player.Add(Wood, 30); Value.Step();
            Empty(Value.Samples, Demand, "allocation failure rebaseline suppresses gap");
            Player.Set(Scrap, 21); Player.Add(Wood, 31); Value.Step();
            Change(Value.Samples, Demand, "scrap", 20, 21); Change(Value.Samples, Demand, "wood", 30, 31);
            Check(Value.Samples.Baselines == 2, "one fresh baseline after fault");
            Value.World.Retire(Owner);
            Check(Value.Samples.WatchCount == 0 && Value.Samples.DemandCount == 0 && Value.Samples.PendingCount == 0 &&
                Value.Samples.ChargedBytes == Runtime.InventoryNetObservationPolicy.SnapshotCharge, "fault cleanup returns all variable charges");
        }
    }

    private static void MixedChurn()
    {
        using (var Value = new Fixture()) {
            object Scrap = Value.Item("scrap"); Player Player = Value.Connect(); var Root = Value.Owner();
            for (int Cycle = 0; Cycle < 10000; Cycle++) {
                Player.Set(Scrap, Cycle + 1); ulong Demand = Value.Stage(Root, Player); Value.Step();
                Player.Set(Scrap, Cycle + 2); Value.Step();
                switch (Cycle % 5) {
                    case 0:
                        Change(Value.Samples, Demand, "scrap", Cycle + 1, Cycle + 2); Value.Samples.Cancel(Demand); break;
                    case 1:
                        Publication(Root, 10); ulong Staged = Value.Stage(Root, Player);
                        Publication(Root, 12); Value.Step(); Empty(Value.Samples, Staged, "churn failed module");
                        Change(Value.Samples, Demand, "scrap", Cycle + 1, Cycle + 2); Value.Samples.Cancel(Demand); break;
                    case 2:
                        Player.View.Connected = false; Empty(Value.Samples, Demand, "churn stale queue");
                        Player = Value.Connect(); Value.Step(); break;
                    case 3:
                        var Addon = Value.Owner(true, true); Value.Stage(Addon, Player);
                        Value.World.Retire(Addon); Value.World.Retire(Root); Root = Value.Owner(); break;
                    default:
                        Value.Samples.AllocationAdmission = () => false; Value.Step();
                        Value.Samples.AllocationAdmission = null; Value.Step();
                        Change(Value.Samples, Demand, "scrap", Cycle + 1, Cycle + 2); Value.Samples.Cancel(Demand); break;
                }
                Check(Value.Samples.WatchCount == 0 && Value.Samples.DemandCount == 0 && Value.Samples.PendingCount == 0 &&
                    Value.Samples.ChargedBytes == Runtime.InventoryNetObservationPolicy.SnapshotCharge && Root.ListenerCount == 0,
                    "10000 mixed churn cleanup cycle " + Cycle);
            }
        }
    }

    private static byte[] Package(string Id, string Init, string Api = null, string Required = null)
    {
        using (var Bytes = new MemoryStream()) {
            using (var Zip = new ZipArchive(Bytes, ZipArchiveMode.Create, true)) {
                Action<string, string> Add = (Name, Source) => {
                    using (var Writer = new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Source);
                };
                Add("addon.json", "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"" +
                    (Api == null ? "" : ",\"publicModules\":[\"api\"]") +
                    (Required == null ? "" : ",\"dependencies\":{\"required\":[\"" + Required + "\"],\"optional\":[]}") + "}");
                Add("init.luau", Init); if (Api != null) Add("api.luau", Api);
            }
            return Bytes.ToArray();
        }
    }

    private static Runtime.FacadeSession AddonSession(Runtime.FacadeWorld World, string Domain)
    {
        foreach (var Session in World.Sessions()) if (Session.DomainLifetimeId.ToString(CultureInfo.InvariantCulture) == Domain) return Session;
        throw new Exception("[CarbonLuau:InventoryNetTest] missing real addon domain " + Domain);
    }

    private static void RealVm(Runtime.NativeRuntime Native)
    {
        using (var Value = new Fixture()) {
            Player Player = Value.Connect(); object Scrap = Value.Item("scrap"); Player.Set(Scrap, 3);
            string Source = "local S=require('state'); S.P=game:GetService('Players'):GetPlayers()[1]; S.I=S.P:GetInventory(); assert(S.I==S.P:GetInventory())";
            Func<Runtime.ScriptSnapshot> Snapshot = () => {
                var Result = new Runtime.ScriptSnapshot { EntryName = "init.luau", EntrySource = Source };
                Result.Modules.Add("state", "return {}");
                Result.Modules.Add("bad", "local S=require('state'); S.Leak=S.P:GetInventory(); error('failed publication')");
                Result.Modules.Add("nested", "return require('bad')"); return Result;
            };
            using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig { MaxCallbackMilliseconds = 100 }, Snapshot, Value.World)) {
                Action<string> Run = Code => {
                    var Result = Host.Execute("inventory-net", Code);
                    Check(Result.Status == Runtime.RuntimeStatus.OK, "real VM: " + Code + ": " + Result.Error);
                };
                var ReloadWatch = System.Diagnostics.Stopwatch.StartNew();
                var Reload = Host.Reload();
                ReloadWatch.Stop();
                Console.WriteLine("[CarbonLuau:InventoryNetTest] RELOAD_OBSERVATION status=" + Reload.Status +
                    " scans=" + Player.Scans + " watches=" + Value.Samples.WatchCount +
                    " elapsedMs=" + ReloadWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                    " error=" + Reload.Error);
                Check(Reload.Status == Runtime.RuntimeStatus.OK && Player.Scans == 0 && Value.Samples.WatchCount == 0,
                    "old PlayerInventory acquisition still scan/watch free: status=" + Reload.Status +
                    " scans=" + Player.Scans + " watches=" + Value.Samples.WatchCount + " error=" + Reload.Error);
                Check(Runtime.PhysicalInventoryObservation.Count(Player.Source(),Scrap)==3 &&
                    Runtime.PhysicalInventoryObservation.Has(Player.Source(),Scrap,3),"qualified backend read parity");
                Run("local S=require('state'); assert(game.ApiVersion=='0.7.5-experimental'); for _,K in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(S.P[K]==nil) end; assert(S.I:CountItem('scrap')==3 and S.I:HasItem('scrap',3)); assert(not S.I:HasItem('scrap',4)); assert(S.I:CountItem('missing')==0); assert(not S.I:HasItem('missing')); assert(game:GetService('Items'):Exists('scrap')); assert(not game:GetService('Items'):Exists('missing')); assert(not pcall(function() S.I.X=1 end)); for _,K in {'ItemAdded','ItemRemoved','Changed','Signal'} do assert(not pcall(function() return S.I[K] end)) end; assert(type(S.I.GiveItem)=='function' and type(S.I.TakeItem)=='function' and type(S.I.ItemChanged.Connect)=='function')");
                Run("local S=require('state'); assert(not pcall(function() require('nested') end)); assert(not pcall(function() S.Leak:CountItem('scrap') end)); assert(S.I:CountItem('scrap')==3)");
                Player.Clear(); for (int Index = 0; Index < 128; Index++) Player.Add(Scrap, 3);
                Run("local S=require('state'); assert(S.I:CountItem('scrap')==384 and S.P:GetInventory():CountItem('scrap')==384); assert(S.I:HasItem('scrap',384) and not S.I:HasItem('scrap',385))");
                Player.Add(Scrap, 1, 1, false);
                Run("local S=require('state'); for _,O in {S.I,S.P:GetInventory()} do assert(not pcall(function() O:CountItem('scrap') end)); assert(not pcall(function() O:HasItem('scrap') end)) end");
                Player.Set(Scrap, Runtime.FacadePolicy.MaxExactLuauInteger); Player.Add(Scrap, 1, 1);
                Run("local S=require('state'); assert(not pcall(function() S.I:CountItem('scrap') end)); assert(not pcall(function() S.P:GetInventory():CountItem('scrap') end))");
                Player.Set(Scrap, 3); Player.Add(new Definition("scrap"), 999, 1);
                Run("local S=require('state'); assert(S.I:CountItem('scrap')==3 and S.P:GetInventory():CountItem('scrap')==3)");
                Player.Set(Scrap, 3);
                var Root = Value.World.Active; ulong RootDemand = Value.Stage(Root, Player); Value.Step();
                Player.Set(Scrap, 4); Value.Step(); Change(Value.Samples, RootDemand, "scrap", 3, 4);
                Check(Root.ListenerCount == 0 && !Host.HasWork, "private sampling creates no native callback roots/work");

                Runtime.FacadeSession Failed = null; ulong FailedDemand = 0;
                Value.Registrar.DuringPublish = (Previous, Next) => {
                    if (Next == null) return;
                    Failed = Next; FailedDemand = Value.Stage(Next, Player);
                    int Scans = Player.Scans; Value.Step();
                    Check(Value.Samples.DemandCount == 2 && Player.Scans == Scans + 1, "only committed original scans while replacement is staged");
                    throw new InvalidOperationException("fixture rejects replacement publication");
                };
                Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Failed != null && Failed.Disposed &&
                    ReferenceEquals(Value.World.Active, Root) && Value.Samples.DemandCount == 1, "real failed replacement original demand preserved");
                Value.Registrar.DuringPublish = null;
                Empty(Value.Samples, FailedDemand, "failed real candidate records inaccessible");
                Run("assert(require('state').I:CountItem('scrap')==4)");
                Player.Set(Scrap, 5); Value.Step(); Change(Value.Samples, RootDemand, "scrap", 4, 5);

                using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                    object Provider = new object(), ConsumerProvider = new object();
                    string[] OwnerRegistration = Registry.RegisterArchive(Provider, Package("netowner", "return true",
                        "return game:GetService('Players'):GetPlayers()[1]:GetInventory()"));
                    Check(OwnerRegistration[0] == "OK" && Registry.ProcessOne(), "real addon owner activation");
                    var Addon = AddonSession(Value.World, Registry.Status(Provider, OwnerRegistration[1])[7]);
                    ulong AddonDemand = Value.Stage(Addon, Player); Value.Step();
                    string[] Consumer = Registry.RegisterArchive(ConsumerProvider, Package("netconsumer",
                        "local I=require('@netowner/api'); assert(I:CountItem('scrap')==5); for _,K in {'ItemAdded','ItemRemoved'} do assert(not pcall(function() return I[K] end)) end; assert(type(I.ItemChanged.Connect)=='function'); return I",
                        null, "netowner"));
                    Check(Consumer[0] == "OK" && Registry.ProcessOne() && Registry.Status(ConsumerProvider, Consumer[1])[2] == "Active",
                        "real shared PlayerInventory keeps defining addon ownership");
                    Player.Set(Scrap, 6); Value.Step();
                    Change(Value.Samples, RootDemand, "scrap", 5, 6); Change(Value.Samples, AddonDemand, "scrap", 5, 6);
                    string[] Replacement = Registry.ReplaceArchive(Provider, OwnerRegistration[1], Package("netowner", "error('failed candidate')",
                        "return game:GetService('Players'):GetPlayers()[1]:GetInventory()"));
                    Check(Replacement[0] == "OK" && Registry.ProcessOne() && !Addon.Disposed && Value.World.IsActive(Addon),
                        "failed real addon replacement retains original domain/watch");
                    Player.Set(Scrap, 7); Value.Step(); Change(Value.Samples, AddonDemand, "scrap", 6, 7);
                    Change(Value.Samples, RootDemand, "scrap", 6, 7);
                    Check(Registry.UnloadProvider(ConsumerProvider) == 1 && Registry.UnloadProvider(Provider) == 1 && Addon.Disposed,
                        "real provider unload retires addon demand");
                    Check(Value.Samples.DemandCount == 1, "provider unload leaves real root demand");
                    Empty(Value.Samples, AddonDemand, "no addon queue after provider retirement");
                }

                // Use the existing D13 grant fixture, preserving its PREPARE/COMMIT/VERIFY model.
                var Grant = new PlayerInteractionFoundation1FBTests.Fixture();
                Value.Definitions["scrap"] = Grant.Definition;
                Player.View.Inventory = Grant.Source; Player.View.GiveInventory = Grant; Player.View.TakeInventory = Grant.Take;
                // Lookup and Describe both resolve Grant.Definition as canonical scrap.
                Value.Samples.Cancel(RootDemand);
                RootDemand = Value.Stage(Root, Player); Value.Step();
                Run("local S=require('state'); assert(S.I:CountItem('scrap')==0); assert(S.P:GetInventory():GiveItem('scrap',5)); assert(S.I:CountItem('scrap')==5 and S.P:GetInventory():CountItem('scrap')==5); assert(S.P:GetInventory():TakeItem('scrap',2)); assert(S.I:CountItem('scrap')==3); assert(not S.P:GetInventory():TakeItem('scrap',4)); assert(not S.P:GetInventory():TakeItem('missing',1)); assert(not pcall(function() S.P:GetInventory():GiveItem('missing',1) end)); assert(not pcall(function() S.P:GetInventory():GiveItem('scrap',1,true) end))");
                Check(Grant.Creates == 1 && Grant.Transfers == 1 && Grant.Cleanups == 0 && Value.World.TakeItems.BusyCount == 0 &&
                    Value.World.GiveItems.BusyCount == 0 && Value.World.GiveItems.TrackedResources == 0, "existing mutation verification and cleanup unchanged");
                Value.Step(); Change(Value.Samples, RootDemand, "scrap", 0, 3);
                Grant.Failure = "reject";
                Run("local Ok,Message=pcall(function() require('state').P:GetInventory():GiveItem('scrap',1) end); assert(not Ok and string.find(Message,'may have changed')); assert(require('state').I:CountItem('scrap')==3)");
                Check(Grant.Cleanups == 1 && Value.World.GiveItems.TrackedResources == 0 && Value.World.GiveItems.BusyCount == 0,
                    "post-COMMIT grant rejection still indeterminate with temporary-resource cleanup");
                Grant.Failure = ""; Value.Step(); Empty(Value.Samples, RootDemand, "rejected physical grant has zero sampled delta");
                Run("local S=require('state'); for _,N in {'','Scrap',' scrap','scrap ',string.rep('x',129),'s\\0x','é'} do assert(not pcall(function() S.I:CountItem(N) end)) end; for _,N in {0,-1,1.5,math.huge,0/0,9007199254740992,'1'} do assert(not pcall(function() S.I:HasItem('scrap',N) end)) end; assert(not pcall(function() S.I.CountItem({},'scrap') end))");
                Player.View.Inventory = Player.Source; Player.View.GiveInventory = null; Player.View.TakeInventory = null;
                Value.Definitions["scrap"] = Scrap; Player.Set(Scrap, 8);
                Value.Samples.Cancel(RootDemand);
                RootDemand = Value.Stage(Root, Player); Value.Step();
                Player.View.Connected = false;
                Run("local S=require('state'); assert(not pcall(function() S.P:GetInventory() end)); assert(not pcall(function() S.I:CountItem('scrap') end))");
                Player = Value.Connect(); Player.Set(Scrap, 100); Value.Step();
                Empty(Value.Samples, RootDemand, "old real root demand cannot follow reconnect");
                Run("local S=require('state'); assert(not pcall(function() S.I:CountItem('scrap') end)); local P=game:GetService('Players'):GetPlayers()[1]; assert(P~=S.P and P:GetInventory():CountItem('scrap')==100)");

                RootDemand = Value.Stage(Root, Player); Value.Step(); Player.Set(Scrap, 101); Value.Step();
                Runtime.FacadeSession StagedOwner = null; ulong FreshDemand = 0;
                Value.Registrar.DuringPublish = (Previous, Next) => {
                    if (Next == null) return;
                    StagedOwner = Next; FreshDemand = Value.Stage(Next, Player);
                    Check(!Next.Active && !Value.World.IsActive(Next), "watch lifetime uses actual staged ScriptHost session");
                };
                Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Root.Disposed && ReferenceEquals(Value.World.Active, StagedOwner),
                    "real successful replacement retires original authority");
                Value.Registrar.DuringPublish = null;
                Empty(Value.Samples, RootDemand, "old root pending record not delivered to replacement");
                Value.Step(); Empty(Value.Samples, FreshDemand, "replacement demand has no backlog");
                Player.Set(Scrap, 102); Value.Step(); Change(Value.Samples, FreshDemand, "scrap", 101, 102);
                Player.Set(Scrap, 103); Value.Step();
                long OldGeneration = Host.VmGenerationId; var BeforeFatal = Value.World.Active;
                Check(Host.Execute("inventory-net-fatal", "while true do end").Status == Runtime.RuntimeStatus.TIMEOUT && Host.Ready,
                    "real fatal timeout and automatic reconstruction");
                Check(BeforeFatal.Disposed && Host.VmGenerationId != OldGeneration && Value.Samples.WatchCount == 0 &&
                    Value.Samples.DemandCount == 0 && Value.Samples.PendingCount == 0, "fatal timeout retires watches and old generation records");
                Empty(Value.Samples, FreshDemand, "old generation records unavailable after reconstruction");
                ulong Recovered = Value.Stage(Value.World.Active, Player); Value.Step();
                Empty(Value.Samples, Recovered, "new generation no replay baseline");
                Player.Set(Scrap, 104); Value.Step(); Change(Value.Samples, Recovered, "scrap", 103, 104);
                Run("assert(require('state').I:CountItem('scrap')==104)");
            }
            Check(Value.Samples.WatchCount == 0 && Value.Samples.DemandCount == 0 && Value.Samples.PendingCount == 0 &&
                Native.LiveVmCount == 0, "ScriptHost/native cleanup releases all real root watches");
        }
    }

    public static void Run(string NativePath, string CompilerPath)
    {
        SnapshotRules(); LifecycleAndPublication(); ExactLifetimesAndFailures();
        BoundsAndFairness(); QueueBoundsAndNoReplay(); AllocationFaults(); MixedChurn();
        // Observations from the deterministic managed source, not a Carbon
        // server SLA or a whole-process hard memory guarantee.
        using (var Value = new Fixture()) {
            var Owner = Value.Owner();
            var Labels = new object[128];
            for (int Index = 0; Index < Labels.Length; Index++)
                Labels[Index] = Value.Item(new string('a', 125) + Index.ToString("D3", CultureInfo.InvariantCulture));
            long BeforeBytes = GC.GetTotalMemory(true);
            for (int Index = 0; Index < 64; Index++) {
                Player Player = Value.Connect((Index + 1).ToString(CultureInfo.InvariantCulture));
                for (int Key = 127; Key >= 0; Key--) Player.Add(Labels[Key], 1);
                Value.Stage(Owner, Player); Value.Stage(Owner, Player);
            }
            var Watch = new System.Diagnostics.Stopwatch(); double Maximum = 0, Total = 0;
            for (int Frame = 0; Frame < 80; Frame++) {
                Watch.Restart(); Value.Step(); Watch.Stop();
                Maximum = Math.Max(Maximum, Watch.Elapsed.TotalMilliseconds); Total += Watch.Elapsed.TotalMilliseconds;
                Check(Value.Samples.LastScans == 4, "worst-label measurement stays within four scans");
            }
            Console.WriteLine("[CarbonLuau:InventoryNetTest] MODEL_OBSERVATION watches=64 demands=128 stacks=128 labelBytes=128 turns=80 scans=" +
                Value.Samples.Scans + " meanTurnMs=" + (Total / 80).ToString("F3", CultureInfo.InvariantCulture) +
                " maxTurnMs=" + Maximum.ToString("F3", CultureInfo.InvariantCulture) + " chargedBytes=" + Value.Samples.ChargedBytes +
                " reachableManagedDeltaBytes=" + (GC.GetTotalMemory(true) - BeforeBytes));
        }
        string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CarbonLuauInventoryNet-" + Guid.NewGuid().ToString("N")));
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(NativePath, Target);
            string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(CompilerPath, Worker);
            if (Rid == "linux-x64") Check(chmod(Worker, 493) == 0, "compiler executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) RealVm(Native);
            Console.WriteLine("[CarbonLuau:InventoryNetTest] PASS private sampled candidate: bounded snapshot/model, lifecycle, saturation, allocation faults, 10000 mixed churn cycles and real VM compatibility; completed-operation events and public Signals unqualified");
        } finally {
            // Only this invocation's generated fixture directory is eligible.
            string Temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Check(Root.StartsWith(Temporary, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(Root).StartsWith("CarbonLuauInventoryNet-", StringComparison.Ordinal), "confined fixture cleanup path");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
