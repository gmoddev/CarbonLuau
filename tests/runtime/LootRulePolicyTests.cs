using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class LootRulePolicyTests
{
    private const string Barrel2 = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab";
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        internal bool Fail;
        internal int Calls;
        internal Action ObservePublication;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        {
            Calls++; if (ObservePublication != null) ObservePublication();
            if (Fail) throw new InvalidOperationException("fixture publication failure");
        }
    }
    private sealed class Fixture
    {
        internal readonly Registrar Registrar = new Registrar();
        internal readonly Runtime.FacadeWorld World;
        internal bool Source = true, MissingItem;
        internal int Lookups;
        private long Next = 1;
        internal Fixture()
        {
            World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), Registrar,
                new Runtime.ItemDirectory(Name => { Lookups++; return MissingItem ? null : new object(); }));
            World.LootAvailable = () => Source;
        }
        internal Runtime.FacadeSession Session()
        { return new Runtime.FacadeSession(World, 7, Next++, 32); }
        internal Runtime.LootDropRule Selected(Runtime.FacadeSession Expected)
        { return Selected(Runtime.LootRulePolicy.Prefab, Expected); }
        internal Runtime.LootDropRule Selected(string Prefab, Runtime.FacadeSession Expected)
        {
            Runtime.FacadeSession Owner; Runtime.LootDropRule Rule;
            Check(World.TryLootRule(Prefab, out Owner, out Rule) &&
                Object.ReferenceEquals(Owner, Expected) && Rule != null, "exact committed rule owner");
            Check(String.Equals(Rule.Prefab, Prefab, StringComparison.Ordinal), "descriptor retains declared prefab");
            return Rule;
        }
        internal void None()
        {
            Runtime.FacadeSession Owner; Runtime.LootDropRule Rule;
            Check(!World.TryLootRule(Runtime.LootRulePolicy.Prefab, out Owner, out Rule) && Owner == null && Rule == null,
                "no eligible committed rule or retained out parameters");
            Check(!World.TryLootRule(Barrel2, out Owner, out Rule) && Owner == null && Rule == null,
                "no eligible barrel-2 rule or retained out parameters");
        }
    }
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception(Message); }
    private static void Reject(Action Action, string Message)
    {
        try { Action(); }
        catch (Runtime.FacadeException) { return; }
        throw new Exception("expected controlled rejection: " + Message);
    }
    private static void Publication(Runtime.FacadeSession Session, uint Code)
    {
        try {
            typeof(Runtime.FacadeSession).GetMethod("Operation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Session, new object[] { Code, new string[0] });
        } catch (TargetInvocationException Error) { throw Error.InnerException; }
    }
    private static Runtime.LootDropEntry[] Drops(int Amount = 2)
    { return new[] { new Runtime.LootDropEntry("scrap", Amount, Amount) }; }
    private static void Stage(Runtime.FacadeSession Session, Runtime.LootDropEntry[] Entries = null)
    {
        // Actual root candidate ownership is the inactive session, without an
        // artificial managed checkpoint for native PublicationScope creation.
        Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Entries ?? Drops());
    }
    private static void Invalid(Fixture Value, string Prefab, Runtime.LootDropEntry[] Entries)
    {
        var Session = Value.Session();
        Reject(() => Session.StageLootRule(Prefab, Entries), "invalid descriptor");
        Check(!Session.HasLootRule, "invalid input stages no descriptor");
        Value.World.Retire(Session);
    }
    private static string Host(Runtime.FacadeSession Session, uint Code, byte[] Bytes, bool Success,
        long? ExpectedDomain = null)
    {
        IntPtr Input = Marshal.AllocHGlobal(Math.Max(1, Bytes.Length)), Output = Marshal.AllocHGlobal(262144);
        try {
            if (Bytes.Length != 0) Marshal.Copy(Bytes, 0, Input, Bytes.Length);
            uint Written;
            uint Status = Session.Callback((ulong)(ExpectedDomain ?? Session.DomainLifetimeId), Code,
                Input, (uint)Bytes.Length, Output, 262144, out Written);
            Check(Status == (Success ? 0u : 1u), "managed host boundary status for operation " + Code);
            Check(Success ? Written == 0 : Written <= 200, "bounded declaration response");
            var Response = new byte[Written];
            if (Written != 0) Marshal.Copy(Output, Response, 0, (int)Written);
            return Runtime.FacadePolicy.Utf8.GetString(Response);
        } finally { Marshal.FreeHGlobal(Input); Marshal.FreeHGlobal(Output); }
    }
    private static string Declare(Runtime.FacadeSession Session, string Descriptor, bool Success = true)
    { return Host(Session, 42, Runtime.FacadePolicy.Pack(Runtime.LootRulePolicy.Prefab, Descriptor), Success); }
    private static void HostPublication(Runtime.FacadeSession Session, uint Code)
    { Host(Session, Code, new byte[0], true); }
    private static void InvalidHost(Fixture Value, params string[] Fields)
    {
        var Session = Value.Session(); int Lookups = Value.Lookups, Calls = Value.Registrar.Calls;
        string Error = Host(Session, 42, Runtime.FacadePolicy.Pack(Fields), false);
        Check(Error.Length != 0 && Error != "host operation failed", "curated managed declaration error");
        Check(!Session.HasLootRule && Value.Lookups == Lookups && Value.Registrar.Calls == Calls,
            "malformed host declaration stages nothing and performs no lookup/publication");
        // A catchable invalid declaration must leave the candidate usable.
        Declare(Session, "scrap\t0\t1"); Check(Session.HasLootRule, "valid declaration after controlled rejection");
        Value.World.Retire(Session);
    }

    private static void ManagedBoundaryValidation()
    {
        var Value = new Fixture(); string Prefab = Runtime.LootRulePolicy.Prefab;
        Check(Runtime.LootRulePolicy.DescriptorFields == 24 && Runtime.LootRulePolicy.DescriptorBytes == 1024,
            "public marshalling bounds preserve D2A entry limits");
        InvalidHost(Value); InvalidHost(Value, Prefab); InvalidHost(Value, Prefab, "", "");
        InvalidHost(Value, Prefab, "scrap\t0\t1", "Replace");
        foreach (string Name in new[] { "", "oil_barrel", Prefab + "extra", Prefab.ToUpperInvariant() })
            InvalidHost(Value, Name, "scrap\t0\t1");
        foreach (string Descriptor in new[] {
            "\t", "scrap", "scrap\t0", "scrap\t0\t1\t", "scrap\t0\t1\tmetal.fragments",
            "scrap\t0\t1\tmetal.fragments\t0", "\t0\t1", "scrap\t\t1", "scrap\t0\t",
            "scrap\t1\t0", "stones\t0\t1", "Wood\t0\t1", "Scrap\t0\t1", " scrap\t0\t1", "scráp\t0\t1",
            "metal.fragments.extra\t0\t1", "scrap\n\t0\t1", "scrap\0\t0\t1", "scrap\t0\t1\tstones\t0\t1",
            new string('x', 1025), new string('x', 1024), new string('é', 513),
            new string('é', 512), new string('\t', 24), new string('\t', 1024) })
            InvalidHost(Value, Prefab, Descriptor);
        foreach (string Amount in new[] {
            "-1", "+1", "-0", "+0", "00", "01", "0100", " 1", "1 ", "1.0", "1e0",
            "0x1", "1,000", "١", "１", "NaN", "Infinity", "1001", "9999", "2147483648",
            "18446744073709551616", new string('9', 1000) }) {
            InvalidHost(Value, Prefab, "scrap\t" + Amount + "\t1000");
            InvalidHost(Value, Prefab, "scrap\t0\t" + Amount);
        }
        var Triples = new string[9];
        for (int Index = 0; Index < Triples.Length; Index++) Triples[Index] = "scrap\t0\t1000";
        InvalidHost(Value, Prefab, String.Join("\t", Triples));
        Check(Runtime.LootRulePolicy.Parse(new string('x', 1020) + "\t0\t0").Length == 1 &&
            Runtime.LootRulePolicy.Parse(new string('é', 510) + "\t0\t0").Length == 1,
            "exact 1024 UTF8 bytes accepted by bounded codec; item qualification belongs to immutable validation");
        Reject(() => Runtime.LootRulePolicy.Parse(new string('é', 511) + "\t0\t0"), "UTF8 byte bound independent of character count");
        Reject(() => Runtime.LootRulePolicy.Parse(null), "null descriptor");
        Reject(() => Runtime.LootRulePolicy.Parse("\ud800"), "invalid managed UTF16 descriptor");
        var Wire = Value.Session();
        byte[] Payload = Runtime.FacadePolicy.Pack(Prefab, "scrap\t0\t1");
        Array.Resize(ref Payload, Payload.Length - 1);
        Check(Host(Wire, 42, Payload, false) == "invalid host payload", "unterminated NUL fields rejected");
        Host(Wire, 42, new byte[] { 0xff, 0 }, false);
        Host(Wire, 42, new byte[16385], false);
        Check(!Wire.HasLootRule, "invalid UTF8 stages nothing");
        Declare(Wire, ""); Value.World.Commit(Wire);
        Check(Value.Selected(Wire).Count == 0, "two NUL fields with empty descriptor commit zero entries");
        Value.World.Retire(Wire);
        var Maximum = Value.Session();
        Array.Resize(ref Triples, 8);
        for (int Index = 0; Index < Triples.Length; Index++)
            Triples[Index] = (Index % 2 == 0 ? "scrap" : "metal.fragments") + "\t1000\t1000";
        InvalidHost(Value, Prefab, String.Join("\t", Triples) + "\t");
        Declare(Maximum, String.Join("\t", Triples)); Value.World.Commit(Maximum);
        var Rule = Value.Selected(Maximum);
        Check(Rule.Count == 8 && Rule.MaximumTotal == 8000 && Rule.Entry(7).Min == 1000 &&
            Rule.Entry(7).Item == "metal.fragments", "24 fields and inclusive maximum retain immutable qualified entries");
        Value.World.Retire(Maximum);
        var Zero = Value.Session(); Declare(Zero, "scrap\t0\t0\tmetal.fragments\t0\t1"); Value.World.Commit(Zero);
        Check(Value.Selected(Zero).Entry(0).Max == 0 && Value.Selected(Zero).MaximumTotal == 1,
            "canonical zero and ordered entries preserved"); Value.World.Retire(Zero);
    }

    private static void ManagedBoundaryOwnership()
    {
        var Value = new Fixture(); var Session = Value.Session();
        byte[] Payload = Runtime.FacadePolicy.Pack(Runtime.LootRulePolicy.Prefab, "scrap\t0\t1");
        Host(Session, 42, Payload, false, Session.DomainLifetimeId + 1);
        Check(!Session.HasLootRule && Value.Lookups == 0, "outer host boundary rejects stale domain before resolution");
        Value.Source = false; Declare(Session, "scrap\t0\t1", false);
        Check(!Session.HasLootRule && Value.Lookups == 0, "unavailable source stages nothing");
        Value.Source = true; Value.MissingItem = true; Declare(Session, "scrap\t0\t1", false);
        Check(!Session.HasLootRule, "missing definition stages nothing"); Value.MissingItem = false;
        Declare(Session, "scrap\t0\t1"); int Lookups = Value.Lookups;
        Declare(Session, "scrap\t0\t1", false);
        Check(Value.Lookups == Lookups, "domain duplicate rejected before item resolution");
        Value.None(); Value.World.Commit(Session); var Snapshot = Value.Selected(Session);
        Declare(Session, "", false); HostPublication(Session, 10); Declare(Session, "", false);
        Check(Object.ReferenceEquals(Value.Selected(Session), Snapshot), "active captured facade cannot declare in provisional scope");
        HostPublication(Session, 12); Value.World.Retire(Session);
        Declare(Session, "", false); Value.None();
        var Cleared = Value.Session(); Cleared.Clear(); Declare(Cleared, "", false);
        Check(!Cleared.HasLootRule, "cleared candidate rejected through managed boundary"); Value.World.Retire(Cleared);

        var Nested = Value.Session(); HostPublication(Nested, 10); HostPublication(Nested, 10);
        Declare(Nested, "scrap\t1\t2"); HostPublication(Nested, 11);
        Check(Nested.HasLootRule, "inner success merges declaration"); Value.None();
        HostPublication(Nested, 12); Check(!Nested.HasLootRule, "outer rollback discards inner success");
        HostPublication(Nested, 10); HostPublication(Nested, 10);
        Declare(Nested, "scrap\t3\t4"); HostPublication(Nested, 12);
        Check(!Nested.HasLootRule, "caught cold-module failure discards declaration");
        Declare(Nested, "metal.fragments\t5\t6"); HostPublication(Nested, 10);
        Declare(Nested, "", false); HostPublication(Nested, 12); HostPublication(Nested, 11);
        Value.World.Commit(Nested);
        Check(Value.Selected(Nested).Entry(0).Item == "metal.fragments", "parent declaration survives inner rollback");
        var Conflict = Value.Session(); Declare(Conflict, ""); int Calls = Value.Registrar.Calls;
        Reject(() => Value.World.PreflightLootRules(null, Conflict), "public declaration conflicts before activation");
        Check(Value.Registrar.Calls == Calls, "public conflict has no registrar effect"); Value.Selected(Nested);
        Value.World.PreflightLootRules(Nested, Conflict); Value.World.Commit(Conflict); Value.World.Retire(Nested);
        Check(Value.Selected(Conflict).Count == 0, "exact outgoing-owner replacement accepts empty rule");
        Value.World.Retire(Conflict); Value.None();

        Runtime.FacadeSession ChangedSession = null;
        var ChangedWorld = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar(),
            new Runtime.ItemDirectory(Name => { ChangedSession.Clear(); return new object(); }));
        ChangedWorld.LootAvailable = () => true; ChangedSession = new Runtime.FacadeSession(ChangedWorld, 7, 99, 32);
        Declare(ChangedSession, "scrap\t0\t1", false);
        Check(!ChangedSession.HasLootRule, "resolver cannot retain stale staging authority after clear");
        ChangedWorld.Retire(ChangedSession);
    }

    internal static void Run()
    {
        Validation(); PublicationAndOwnership(); NoRulePreflight(); ConflictsAndReplacement(); SourceAndRegistrar(); BoundsAndTeardown();
        ManagedBoundaryValidation(); ManagedBoundaryOwnership();
        ProfileValidation(); ProfilePublication(); ProfileReplacement();
        ItemValidation();
        Console.WriteLine("[CarbonLuau:LootPolicy] PASS immutable descriptor, managed declaration boundary, publication, conflicts, source gating and teardown model; no host mutation");
    }
    private static void Nonmatching(Fixture Value, string Prefab, Runtime.FacadeSession Incumbent, Runtime.LootDropRule Snapshot)
    {
        Runtime.FacadeSession Owner = Incumbent; Runtime.LootDropRule Rule = Snapshot;
        Check(!Value.World.TryLootRule(Prefab, out Owner, out Rule) && Owner == null && Rule == null,
            "nonmatching profile clears both selection outputs");
    }
    private static void ProfileValidation()
    {
        Check(Runtime.LootRulePolicy.GlobalRules == 1 && Runtime.LootRulePolicy.DomainRules == 1,
            "additional prefab preserves one global rule and one rule per domain");
        Check(Runtime.LootRulePolicy.Prefab == "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
            "backward-compatible first prefab constant remains unchanged");
        var Value = new Fixture();
        foreach (string Prefab in new[] {
            "loot-barrel-2", "loot-barrel-2.prefab", Barrel2.ToUpperInvariant(), " " + Barrel2, Barrel2 + " ",
            Barrel2 + "extra", Barrel2 + "\0", Barrel2.Replace('/', '\\'), Barrel2.Replace("/loot/", "/loot/../loot/"),
            "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
            "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
            "assets/bundled/prefabs/radtown/oil_barrel.prefab",
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-3.prefab" }) {
            int Lookups = Value.Lookups;
            Invalid(Value, Prefab, Drops());
            Check(Value.Lookups == Lookups, "unsupported prefab rejected before definition resolver");
            // Embedded NUL is checked above as a managed string; it is a wire delimiter here.
            InvalidHost(Value, Prefab, "scrap\t0\t1");
        }
        foreach (string Item in new[] { "stones", "Wood", "Scrap", " scrap", "scrap\0", "scráp", "", null }) {
            int Lookups = Value.Lookups;
            Invalid(Value, Barrel2, new[] { new Runtime.LootDropEntry("scrap", 0, 1), new Runtime.LootDropEntry(Item, 0, 1) });
            Check(Value.Lookups == Lookups, "late unsupported item rejected before any definition resolver");
        }
        foreach (string Descriptor in new[] { "scrap\t0\t1\tstones\t0\t1", "scrap\t0\t1\tScrap\t0\t1",
            "scrap\t0\t1\tmetal.fragments\t2\t1", "scrap\t0\t1\tmetal.fragments\t0\t1001" })
            InvalidHost(Value, Barrel2, Descriptor);
        var Input = Drops(4); var Session = Value.Session();
        Session.StageLootRule(Barrel2, Input); Input[0] = new Runtime.LootDropEntry("wood", 99, 99);
        int Before = Value.Lookups;
        Reject(() => Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "different prefab still consumes same domain slot");
        Check(Value.Lookups == Before, "different-prefab duplicate rejected before definition resolver");
        Value.None(); Value.World.Commit(Session);
        var Snapshot = Value.Selected(Barrel2, Session);
        Check(Snapshot.Entry(0).Item == "scrap" && Snapshot.Entry(0).Max == 4 && Snapshot.MaximumTotal == 4,
            "barrel-2 owns copied immutable entries and actual prefab");
        foreach (string Prefab in new[] { Runtime.LootRulePolicy.Prefab, Barrel2.ToUpperInvariant(), Barrel2 + "extra", null })
            Nonmatching(Value, Prefab, Session, Snapshot);
        Value.World.Retire(Session); Value.None();
        Check(Snapshot.Prefab == Barrel2 && Snapshot.Entry(0).Max == 4, "retired snapshot retains actual prefab and entries");
    }
    private static void ProfilePublication()
    {
        var Value = new Fixture(); var Session = Value.Session();
        HostPublication(Session, 10); HostPublication(Session, 10);
        Host(Session, 42, Runtime.FacadePolicy.Pack(Barrel2, "scrap\t1\t2"), true);
        HostPublication(Session, 11); Value.None(); HostPublication(Session, 12);
        Check(!Session.HasLootRule, "outer rollback discards nested barrel-2 descriptor");
        HostPublication(Session, 10);
        Host(Session, 42, Runtime.FacadePolicy.Pack(Barrel2, "scrap\t3\t4"), true);
        HostPublication(Session, 12);
        Declare(Session, "metal.fragments\t5\t6"); Value.World.Commit(Session);
        var Snapshot = Value.Selected(Session);
        Nonmatching(Value, Barrel2, Session, Snapshot);
        HostPublication(Session, 10);
        Host(Session, 42, Runtime.FacadePolicy.Pack(Barrel2, ""), false);
        Check(Object.ReferenceEquals(Value.Selected(Session), Snapshot), "active provisional declaration cannot switch profiles");
        HostPublication(Session, 12); Value.World.Retire(Session); Value.None();
    }
    private static void ProfileReplacement()
    {
        var Value = new Fixture(); var Root = Value.Session(); Stage(Root); Value.World.Commit(Root);
        var Original = Value.Selected(Root); Nonmatching(Value, Barrel2, Root, Original);
        var Candidate = Value.Session(); Candidate.StageLootRule(Barrel2, Drops(7));
        Value.World.PreflightLootRules(Root, Candidate);
        Check(Object.ReferenceEquals(Value.Selected(Root), Original), "preflight preserves outgoing profile");
        Nonmatching(Value, Barrel2, Root, Original);
        Value.Registrar.ObservePublication = () => {
            Check(Object.ReferenceEquals(Value.Selected(Root), Original), "registrar observes old profile until publication completes");
            Nonmatching(Value, Barrel2, Root, Original);
        };
        Value.Registrar.Fail = true;
        try { Value.World.Commit(Candidate); throw new Exception("registrar must fail"); }
        catch (InvalidOperationException) { }
        Check(Value.World.Active == Root && Root.Active && !Candidate.Active &&
            Object.ReferenceEquals(Value.Selected(Root), Original), "failed cross-profile publication preserves old selection");
        Nonmatching(Value, Barrel2, Root, Original); Value.Registrar.Fail = false;
        Value.World.Commit(Candidate); Value.Registrar.ObservePublication = null; Value.World.Retire(Root);
        var Second = Value.Selected(Barrel2, Candidate); Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Candidate, Second);
        var Foreign = Value.Session(); Stage(Foreign); int Calls = Value.Registrar.Calls;
        Reject(() => Value.World.PreflightLootRules(null, Foreign), "different prefab foreign owner exceeds global capacity");
        Reject(() => Value.World.CommitAddon(null, Foreign), "different prefab root/addon collision");
        Check(Value.Registrar.Calls == Calls && Object.ReferenceEquals(Value.Selected(Barrel2, Candidate), Second),
            "foreign profile conflicts before registrar publication");
        Value.World.Retire(Candidate); Value.World.CommitAddon(null, Foreign);
        var Next = Value.Session(); Next.StageLootRule(Barrel2, Drops(8));
        Value.World.PreflightLootRules(Foreign, Next); Value.World.CommitAddon(Foreign, Next);
        Check(Foreign.Disposed && !Foreign.HasLootRule && Value.Selected(Barrel2, Next).Entry(0).Max == 8,
            "exact outgoing addon may switch profiles atomically");
        Nonmatching(Value, Runtime.LootRulePolicy.Prefab, Next, Value.Selected(Barrel2, Next));
        var Incoming = Value.Session(); Stage(Incoming); Calls = Value.Registrar.Calls;
        Reject(() => Value.World.Commit(Incoming), "different prefab addon/root collision");
        Reject(() => Value.World.CommitAddon(null, Incoming), "different prefab addon/addon collision");
        Check(Value.Registrar.Calls == Calls, "cross-profile addon conflicts have no registrar effect");
        Value.World.Retire(Incoming); Value.World.Retire(Next); Value.None();
        Check(Original.Prefab == Runtime.LootRulePolicy.Prefab && Second.Prefab == Barrel2 && Second.Entry(0).Max == 7,
            "captured outgoing descriptors never retarget between profiles");
    }
    private static void ItemValidation()
    {
        foreach (string Prefab in new[] { Runtime.LootRulePolicy.Prefab, Barrel2 }) {
            var Value = new Fixture(); var Session = Value.Session();
            var Input = new[] { new Runtime.LootDropEntry("wood", 50, 100),
                new Runtime.LootDropEntry("scrap", 5, 12), new Runtime.LootDropEntry("metal.fragments", 10, 25) };
            Session.StageLootRule(Prefab, Input); Input[0] = new Runtime.LootDropEntry("stones", 0, 0);
            Value.World.Commit(Session); var Rule = Value.Selected(Prefab, Session);
            Check(Rule.Count == 3 && Rule.MaximumTotal == 137 && Rule.Entry(0).Item == "wood" &&
                Rule.Entry(0).Min == 50 && Rule.Entry(0).Max == 100 && Value.Lookups == 3,
                "mixed wood descriptor preserves copied amounts and exact profile");
            var Maximum = new Runtime.LootDropEntry[8];
            for (int Index = 0; Index < Maximum.Length; Index++) Maximum[Index] = new Runtime.LootDropEntry("wood", 0, 1000);
            var Next = Value.Session(); Next.StageLootRule(Prefab, Maximum); Value.World.Commit(Next); Value.World.Retire(Session);
            Check(Value.Selected(Prefab, Next).MaximumTotal == 8000 && Session.Disposed,
                "wood maximum retains existing cardinality and outgoing ownership");
            Value.World.Retire(Next); Value.None();
        }
    }

    private static void Validation()
    {
        Check(Runtime.LootRulePolicy.Entries == 8 && Runtime.LootRulePolicy.PerFrame == 8 &&
            Runtime.LootRulePolicy.MaximumAmount == 1000 && Runtime.LootRulePolicy.MaximumTotal == 8000 &&
            Runtime.LootRulePolicy.MaximumCreatedWorlds == 8 && Runtime.LootRulePolicy.MaximumSessions == 129,
            "canonical private work and selection limits");
        var Value = new Fixture(); string Prefab = Runtime.LootRulePolicy.Prefab;
        Invalid(Value, Prefab + "extra", Drops()); Invalid(Value, Prefab.ToUpperInvariant(), Drops());
        Invalid(Value, null, Drops()); Invalid(Value, Prefab, null);
        Invalid(Value, Prefab, new Runtime.LootDropEntry[9]);
        foreach (string Item in new[] { "Scrap", "Wood", "stones", "wood.extra", "wood\0", "metal.fragments.extra", " scrap", "scrap\0", "scráp", "" , null })
            Invalid(Value, Prefab, new[] { new Runtime.LootDropEntry(Item, 0, 1) });
        foreach (var Entry in new[] {
            new Runtime.LootDropEntry("scrap", -1, 0), new Runtime.LootDropEntry("scrap", 3, 2),
            new Runtime.LootDropEntry("scrap", 0, 1001), new Runtime.LootDropEntry("scrap", Int32.MinValue, Int32.MaxValue) })
            Invalid(Value, Prefab, new[] { Entry });
        Check(Value.Lookups == 0 && Value.Registrar.Calls == 0, "malformed descriptors rejected before item lookup or publication");
        Value.MissingItem = true; Invalid(Value, Prefab, Drops()); Value.MissingItem = false;
        var Maximum = new Runtime.LootDropEntry[8];
        for (int Index = 0; Index < Maximum.Length; Index++)
            Maximum[Index] = new Runtime.LootDropEntry(Index % 2 == 0 ? "scrap" : "metal.fragments", 0, 1000);
        var Session = Value.Session(); Stage(Session, Maximum); Value.World.Commit(Session);
        var Rule = Value.Selected(Session);
        Check(Rule.Count == 8 && Rule.MaximumTotal == 8000 && Rule.Entry(7).Item == "metal.fragments" && Rule.Entry(7).Max == 1000,
            "maximum inclusive amounts and repeated qualified item names");
        Maximum[7] = new Runtime.LootDropEntry("wood", 0, 0);
        Check(Rule.Entry(7).Item == "metal.fragments", "caller array never mutates descriptor");
        var Copy = new[] { new Runtime.LootDropEntry("scrap", 1, 2) };
        var Immutable = new Runtime.LootDropRule(Runtime.LootRulePolicy.Prefab, Copy, 2); Copy[0] = new Runtime.LootDropEntry("wood", 7, 7);
        Check(Immutable.Entry(0).Item == "scrap", "descriptor constructor owns its array too");
        Value.World.Retire(Session); Value.None();
        var Empty = Value.Session(); Stage(Empty, new Runtime.LootDropEntry[0]); Value.World.Commit(Empty);
        Check(Value.Selected(Empty).Count == 0, "empty replacement is a committed rule, not absent demand");
        var Zero = Value.Session(); Stage(Zero, Drops(0)); Value.World.Commit(Zero); Value.World.Retire(Empty);
        Check(Value.Selected(Zero).Entry(0).Min == 0 && Value.Selected(Zero).MaximumTotal == 0, "zero endpoints accepted");
        Value.World.Retire(Zero);
    }
    private static void PublicationAndOwnership()
    {
        var Value = new Fixture(); var Session = Value.Session();
        var FailedRoot = Value.Session(); Stage(FailedRoot); Value.None();
        Value.World.Retire(FailedRoot);
        Check(!FailedRoot.HasLootRule, "failed root candidate clears its directly staged rule without control 10/12");
        // These controls model enlisted nested scopes, not entry-scope creation.
        Publication(Session, 10); Publication(Session, 10);
        Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()); Publication(Session, 11);
        Check(Session.HasLootRule, "inner success merged into outer staging"); Value.None();
        Reject(() => Value.World.Commit(Session), "outer publication still open");
        Publication(Session, 12); Check(!Session.HasLootRule, "outer failure discards successful inner descriptor");
        Publication(Session, 10); Publication(Session, 10);
        Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()); Publication(Session, 12);
        Check(!Session.HasLootRule, "failed cold module discarded even when parent catches failure");
        var Input = Drops(4); Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Input);
        Input[0] = new Runtime.LootDropEntry("wood", 99, 99);
        Publication(Session, 10);
        Reject(() => Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "one staged rule per domain");
        Publication(Session, 12); Publication(Session, 11);
        Value.None(); Value.World.PreflightLootRules(null, Session); Value.World.Commit(Session);
        var Selected = Value.Selected(Session); Check(Selected.Entry(0).Max == 4, "staged array copied immediately");
        Publication(Session, 10);
        Reject(() => Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "active foreign facade cannot launder provisional registration");
        Check(Object.ReferenceEquals(Value.Selected(Session), Selected), "open active publication uses original committed snapshot");
        Publication(Session, 12);
        Value.World.Retire(Session); Value.None();
        Reject(() => Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "retired owner");
        Value.World.PreflightLootRules(null, Session); // Cleared rule-free sessions add no new preflight gate.
        var Cleared = Value.Session(); Publication(Cleared, 10); Cleared.Clear();
        Reject(() => Cleared.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "cleared uncommitted session");
        Value.World.PreflightLootRules(null, Cleared);
        var Foreign = new Fixture(); var ForeignSession = Foreign.Session(); Stage(ForeignSession);
        Reject(() => Value.World.Commit(ForeignSession), "cross-world session"); Foreign.World.Retire(ForeignSession);
    }
    private static void NoRulePreflight()
    {
        var Value = new Fixture(); int SourceCalls = 0;
        Value.World.LootAvailable = () => { SourceCalls++; throw new InvalidOperationException("must not inspect source"); };
        var Empty = Value.Session(); Publication(Empty, 10);
        Value.World.PreflightLootRules(null, Empty); // No new open-publication failure for rule-free loads.
        Publication(Empty, 12); Value.World.Commit(Empty);
        Value.World.PreflightLootRules(null, Empty); // Existing active session is likewise not a loot gate.
        var Foreign = new Fixture(); var ForeignEmpty = Foreign.Session();
        Value.World.PreflightLootRules(null, ForeignEmpty);
        Foreign.World.Retire(ForeignEmpty); Value.World.PreflightLootRules(null, ForeignEmpty);
        Value.World.Retire(Empty); Value.World.PreflightLootRules(null, Empty);
        Check(SourceCalls == 0, "rule-free preflight does not add lifetime, owner, source or publication checks");
        Value.None();
    }
    private static void ConflictsAndReplacement()
    {
        var Value = new Fixture(); var Root = Value.Session(); Stage(Root); Value.World.Commit(Root);
        var FailedRoot = Value.Session(); Stage(FailedRoot, Drops(7)); Value.Selected(Root);
        Value.World.Retire(FailedRoot); Value.Selected(Root);
        Check(!FailedRoot.HasLootRule, "ordinary failed root candidate preserves incumbent and clears direct staging");
        var Addon = Value.Session(); Stage(Addon, Drops(3)); int Calls = Value.Registrar.Calls;
        Reject(() => Value.World.PreflightLootRules(null, Addon), "root/addon collision before native activation");
        Reject(() => Value.World.CommitAddon(null, Addon), "root/addon collision repeated at facade commit");
        Check(Value.Registrar.Calls == Calls, "conflict before registrar effects"); Value.Selected(Root);
        var Replacement = Value.Session(); Stage(Replacement, Drops(5));
        Value.World.PreflightLootRules(Root, Replacement); Value.World.Commit(Replacement); Value.World.Retire(Root);
        Check(Value.Selected(Replacement).Entry(0).Max == 5, "root exact outgoing-owner replacement");
        Value.World.Retire(Replacement); Value.World.CommitAddon(null, Addon); Value.Selected(Addon);
        var OtherAddon = Value.Session(); Stage(OtherAddon);
        Reject(() => Value.World.CommitAddon(null, OtherAddon), "addon/addon collision");
        var IncomingRoot = Value.Session(); Stage(IncomingRoot);
        Reject(() => Value.World.Commit(IncomingRoot), "addon/root collision");
        var NextAddon = Value.Session(); Stage(NextAddon, Drops(6));
        var Impostor = new Runtime.FacadeSession(Value.World, Addon.VmGenerationId, Addon.DomainLifetimeId, 32);
        Reject(() => Value.World.PreflightLootRules(Impostor, NextAddon), "numeric domain identity cannot exclude exact owner");
        Reject(() => Value.World.CommitAddon(Impostor, NextAddon), "impostor outgoing addon");
        Value.World.PreflightLootRules(Addon, NextAddon); Value.World.CommitAddon(Addon, NextAddon);
        Check(Addon.Disposed && !Addon.HasLootRule && Value.Selected(NextAddon).Entry(0).Max == 6, "addon replacement clears previous mapping");
        Value.World.Retire(NextAddon); Value.None();
        Value.World.PreflightLootRules(null, IncomingRoot);
        Value.World.CommitAddon(null, OtherAddon);
        Reject(() => Value.World.Commit(IncomingRoot), "commit rechecks state changed since preflight");
        Value.Selected(OtherAddon); Value.World.Retire(OtherAddon); Value.World.Retire(IncomingRoot);
    }
    private static void SourceAndRegistrar()
    {
        var Value = new Fixture(); var Session = Value.Session();
        Value.World.LootAvailable = null; Publication(Session, 10);
        Reject(() => Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "source unavailable by default");
        Check(Value.Lookups == 0, "unavailable source does not resolve items"); Publication(Session, 12);
        Value.World.LootAvailable = () => { throw new InvalidOperationException("source fault"); }; Value.None();
        Publication(Session, 10); Reject(() => Session.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "throwing source"); Publication(Session, 12);
        Value.World.LootAvailable = () => Value.Source; Stage(Session); Value.Source = false;
        Reject(() => Value.World.PreflightLootRules(null, Session), "source lost before commit");
        Reject(() => Value.World.Commit(Session), "source loss repeated before registrar"); Value.None();
        Value.Source = true; Value.World.Commit(Session); var Snapshot = Value.Selected(Session);
        Value.Source = false; Value.None(); Value.Source = true; Value.Selected(Session);
        Runtime.FacadeSession Owner; Runtime.LootDropRule Rule;
        Check(!Value.World.TryLootRule("oil_barrel", out Owner, out Rule) && Owner == null && Rule == null, "exact prefab lookup only");
        var Candidate = Value.Session(); Stage(Candidate, Drops(8)); Value.Registrar.Fail = true;
        try { Value.World.Commit(Candidate); throw new Exception("registrar must fail"); }
        catch (InvalidOperationException) { }
        Check(Object.ReferenceEquals(Value.Selected(Session), Snapshot) && Session.Active && !Candidate.Active, "failed registrar preserves previous root mapping");
        Value.Registrar.Fail = false; Value.World.Retire(Candidate); Value.World.Retire(Session);
        var Addon = Value.Session(); Stage(Addon); Value.World.CommitAddon(null, Addon);
        var Next = Value.Session(); Stage(Next); Value.Registrar.Fail = true;
        try { Value.World.CommitAddon(Addon, Next); throw new Exception("registrar must fail"); }
        catch (InvalidOperationException) { }
        Check(!Addon.Disposed && Addon.Active && !Next.Active, "failed registrar preserves previous addon lifetime"); Value.Selected(Addon);
        Value.Registrar.Fail = false; Value.World.Retire(Addon); Value.World.Retire(Next); Value.None();
        // A trusted resolver changing the source cannot publish under its earlier gate result.
        Runtime.FacadeWorld Changed = null;
        Changed = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar(),
            new Runtime.ItemDirectory(Name => { Changed.LootAvailable = () => false; return new object(); }));
        Changed.LootAvailable = () => true;
        var ChangedSession = new Runtime.FacadeSession(Changed, 9, 10, 32); Publication(ChangedSession, 10);
        Reject(() => ChangedSession.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops()), "source changed during validation");
        Check(!ChangedSession.HasLootRule, "failed source revalidation stages nothing"); Publication(ChangedSession, 12); Changed.Retire(ChangedSession);
    }
    private static void BoundsAndTeardown()
    {
        var Value = new Fixture(); var Root = Value.Session(); Value.World.Commit(Root);
        var Addons = new List<Runtime.FacadeSession>();
        for (int Index = 0; Index < 100; Index++) {
            var Session = Value.Session(); Value.World.CommitAddon(null, Session); Addons.Add(Session);
        }
        var Policy = Value.Session(); Stage(Policy); Value.World.CommitAddon(null, Policy);
        var Snapshot = Value.Selected(Policy);
        for (int Turn = 0; Turn < 100; Turn++) Check(Object.ReferenceEquals(Value.Selected(Policy), Snapshot), "bounded selection returns same immutable rule");
        Value.World.Retire(Policy); Value.None(); // Same session path as provider retirement.
        Check(!Policy.HasLootRule, "provider retirement clears descriptors");
        foreach (var Session in Addons) Value.World.Retire(Session);
        Value.World.Retire(Root); Value.None();
        var Reconstructed = Value.Session(); Stage(Reconstructed); Value.World.Commit(Reconstructed);
        var OldSnapshot = Value.Selected(Reconstructed); Reconstructed.Clear(); Value.None();
        Check(OldSnapshot.Entry(0).Item == "scrap" && !Reconstructed.HasLootRule,
            "already selected immutable snapshot survives clear, future selections do not");
        Value.World.Retire(Reconstructed);
        var Deep = Value.Session();
        for (int Index = 0; Index < 64; Index++) Publication(Deep, 10);
        Deep.StageLootRule(Runtime.LootRulePolicy.Prefab, Drops());
        Reject(() => Publication(Deep, 10), "existing GUI publication depth bound also bounds rule checkpoint copies");
        for (int Index = 0; Index < 64; Index++) Publication(Deep, 12);
        Check(!Deep.HasLootRule, "maximum-depth rollback restores empty state"); Value.World.Retire(Deep);
        var Bounded = new Fixture(); var BoundRoot = Bounded.Session(); Stage(BoundRoot); Bounded.World.Commit(BoundRoot);
        var BoundSessions = new List<Runtime.FacadeSession>();
        for (int Index = 0; Index < Runtime.LootRulePolicy.MaximumSessions - 1; Index++) {
            var Session = Bounded.Session(); Bounded.World.CommitAddon(null, Session); BoundSessions.Add(Session);
        }
        Bounded.Selected(BoundRoot);
        var Beyond = Bounded.Session(); Bounded.World.CommitAddon(null, Beyond); Bounded.None();
        var OversizedCandidate = Bounded.Session(); Stage(OversizedCandidate);
        Reject(() => Bounded.World.PreflightLootRules(BoundRoot, OversizedCandidate), "rule commit rejects an oversized session set before inspection");
        Bounded.World.Retire(OversizedCandidate);
        Bounded.World.Retire(Beyond); Bounded.Selected(BoundRoot);
        foreach (var Session in BoundSessions) Bounded.World.Retire(Session);
        Bounded.World.Retire(BoundRoot); Bounded.None();
    }
}
