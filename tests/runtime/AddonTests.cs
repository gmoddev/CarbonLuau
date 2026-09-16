using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class AddonTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        private readonly List<Runtime.FacadeSession> Sessions = new List<Runtime.FacadeSession>();
        public bool Has(string Name)
        {
            foreach (var Session in Sessions) if (Session.Commands.ContainsKey(Name)) return true;
            return false;
        }
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        {
            if (Previous != null) Sessions.Remove(Previous);
            if (Next == null) return;
            foreach (var Candidate in Next.Commands.Keys)
                foreach (var Existing in Sessions)
                    if (Existing.Commands.ContainsKey(Candidate)) throw new InvalidOperationException("fixture command collision");
            Sessions.Add(Next);
        }
    }
    private sealed class EntrySpec
    {
        public string Name, Text;
        public byte[] RawBytes;
        public int? ExternalAttributes;
        public EntrySpec(string Name, string Text) { this.Name = Name; this.Text = Text; }
        public EntrySpec(string Name, byte[] RawBytes) { this.Name = Name; this.RawBytes = RawBytes; }
    }
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("Foundation B: " + Message); }
    private static byte[] Bytes(string Text) { return Utf8.GetBytes(Text); }
    private static string Manifest(string Id, string Version = "1.0.0", string Dependencies = null)
    {
        return "{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"" + Version + "\"" +
            (Dependencies == null ? "" : ",\"dependencies\":" + Dependencies) + "}";
    }
    private static byte[] Archive(string ManifestText, params EntrySpec[] Sources)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Write(Zip, new EntrySpec("addon.json", ManifestText));
                foreach (EntrySpec Source in Sources) Write(Zip, Source);
            }
            return Output.ToArray();
        }
    }
    private static void Write(ZipArchive Zip, EntrySpec Spec)
    {
        ZipArchiveEntry Entry = Zip.CreateEntry(Spec.Name, CompressionLevel.Optimal);
        if (Spec.ExternalAttributes.HasValue) Entry.ExternalAttributes = Spec.ExternalAttributes.Value;
        using (Stream Stream = Entry.Open()) {
            byte[] Value = Spec.RawBytes == null ? Bytes(Spec.Text) : Spec.RawBytes;
            Stream.Write(Value, 0, Value.Length);
        }
    }
    private static byte[] MutateZipFlags(byte[] Input, bool Encryption, bool Compression)
    {
        byte[] Result = (byte[])Input.Clone();
        for (int Index = 0; Index + 12 < Result.Length; ++Index) {
            uint Signature = BitConverter.ToUInt32(Result, Index);
            if (Signature == 0x04034b50) {
                if (Encryption) Result[Index + 6] |= 1;
                if (Compression) { Result[Index + 8] = 99; Result[Index + 9] = 0; }
            } else if (Signature == 0x02014b50) {
                if (Encryption) Result[Index + 8] |= 1;
                if (Compression) { Result[Index + 10] = 99; Result[Index + 11] = 0; }
            }
        }
        return Result;
    }
    private static void InvalidArchive(byte[] ArchiveBytes, string Message)
    {
        bool Rejected = false;
        try { Runtime.AddonPackageSnapshot.FromArchive(ArchiveBytes); }
        catch (InvalidOperationException) { Rejected = true; }
        Check(Rejected, Message);
    }
    private static void Process(Runtime.AddonRegistry Registry, int Maximum = 32)
    {
        for (int Count = 0; Count < Maximum && Registry.HasPending; ++Count) Registry.ProcessOne();
    }
    private static void IsState(string[] Response, string State, string Message)
    { Check(Response[0] == "OK" && Response[2] == State, Message + ": " + String.Join("|", Response)); }

    public static void Run(Runtime.NativeRuntime Native)
    {
        var Views = new Dictionary<string, Runtime.PlayerView>();
        const string UserId = "76561198000000009";
        Views[UserId] = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Addon Fixture", Connected = true, Send = Message => { }, Permission = Permission => true};
        var Registrar = new Registrar();
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => Views.ContainsKey(Id) ? Views[Id] : null), Registrar);
        World.Players.Connect(Views[UserId]);
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object Provider = new object(), OtherProvider = new object();
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Host.DomainCount == 1, "root baseline");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                byte[] ValidArchive = Archive(Manifest("creator.archive"), new EntrySpec("init.luau", "return true"));
                string[] Registered = Registry.RegisterArchive(Provider, ValidArchive); IsState(Registered, "Registered", "archive accepted");
                string ArchiveToken = Registered[1]; Fill(ValidArchive, 255);
                Process(Registry); IsState(Registry.Status(Provider, ArchiveToken), "Active", "archive activates from copied snapshot");
                Check(Host.DomainCount == 2, "archive adds one domain");

                string[] Duplicate = Registry.RegisterSource(Provider, "creator.archive", "1.0.0", Bytes("return true"));
                Check(Duplicate[0] == "ERROR", "duplicate stable id rejected");
                foreach (string Id in new[] {"", "Upper", "a..b", ".a", "a.", "a_b.", "carbonluau", "carbonluau.core", "three.part.id", new string('a', 33)})
                    Check(Registry.RegisterSource(Provider, Id, "1.0.0", Bytes("return true"))[0] == "ERROR", "invalid id rejected: " + Id);

                string[] Ownership = Registry.Status(OtherProvider, ArchiveToken);
                Check(Ownership[0] == "ERROR" && Ownership[5].Contains("owner"), "same-provider ownership enforced");
                Check(Registry.Unregister(OtherProvider, ArchiveToken)[0] == "ERROR", "foreign unregister rejected");

                byte[] SourceBytes = Bytes("return true");
                string[] SourceRegistration = Registry.RegisterSource(Provider, "sourceaddon", "1.0.0", SourceBytes);
                string SourceToken = SourceRegistration[1]; Fill(SourceBytes, 255); Process(Registry);
                IsState(Registry.Status(Provider, SourceToken), "Active", "single source activates from copied snapshot");

                string[] Failed = Registry.RegisterSource(Provider, "failedaddon", "1.0.0",
                    Bytes("game:GetService('Commands'):Register('failedleak',{},function() end); task.defer(function() error('leak') end); error('init failed')"));
                Process(Registry); IsState(Registry.Status(Provider, Failed[1]), "Failed", "init failure becomes Failed");
                Check(!Registrar.Has("failedleak") && !Host.HasWork, "failed init publishes no command or task");
                string[] CompileFailed = Registry.RegisterSource(Provider, "compilefailed", "1.0.0", Bytes("local ="));
                Process(Registry); IsState(Registry.Status(Provider, CompileFailed[1]), "Failed", "compile failure becomes Failed");

                byte[] CaughtArchive = Archive(Manifest("caughtmodule"),
                    new EntrySpec("init.luau", "assert(not pcall(require,'bad')); assert(not pcall(require,'bad')); return true"),
                    new EntrySpec("bad.luau", "game:GetService('Commands'):Register('moduleleak',{},function() end); task.defer(function() error('leak') end); error('module failed')"));
                string[] Caught = Registry.RegisterArchive(Provider, CaughtArchive); Process(Registry);
                IsState(Registry.Status(Provider, Caught[1]), "Active", "caught ordinary module failures preserve candidate");
                Check(!Registrar.Has("moduleleak") && !Host.HasWork, "caught failed module publishes no resources");

                string[] Replace = Registry.RegisterSource(Provider, "replaceable", "1.0.0",
                    Bytes("game:GetService('Commands'):Register('oldaddoncmd',{},function() end)"));
                Process(Registry); string ReplaceToken = Replace[1]; string OldDomain = Registry.Status(Provider, ReplaceToken)[7];
                Check(Registrar.Has("oldaddoncmd"), "initial replacement fixture published");
                Registry.ReplaceSource(Provider, ReplaceToken, "1.1.0", Bytes("game:GetService('Commands'):Register('newaddoncmd',{},function() end)"));
                Check(Registrar.Has("oldaddoncmd") && !Registrar.Has("newaddoncmd"), "old addon remains during replacement preparation");
                Process(Registry); string[] Replaced = Registry.Status(Provider, ReplaceToken);
                IsState(Replaced, "Active", "replacement succeeds");
                Check(Replaced[4] == "1.1.0" && Replaced[7] != OldDomain && !Registrar.Has("oldaddoncmd") && Registrar.Has("newaddoncmd"), "replacement atomically changes lifetime/resources");
                Runtime.FacadeSession AddonSession = null;
                foreach (Runtime.FacadeSession Session in World.Sessions())
                    if (Session.DomainLifetimeId.ToString() == Replaced[7]) AddonSession = Session;
                Check(AddonSession != null && AddonSession.Invoke("newaddoncmd", UserId, new string[0]), "active addon command enters its owning domain");
                Check(Host.HasWork && Host.Drain().Count == 1 && !Host.HasWork, "addon command callback drains through shared VM");
                string CommittedDomain = Replaced[7];
                Registry.ReplaceSource(Provider, ReplaceToken, "1.2.0",
                    Bytes("game:GetService('Commands'):Register('replacementleak',{},function() end); error('replacement rejected')"));
                Process(Registry); string[] Preserved = Registry.Status(Provider, ReplaceToken);
                IsState(Preserved, "Active", "failed replacement preserves active state");
                Check(Preserved[4] == "1.1.0" && Preserved[7] == CommittedDomain && Registrar.Has("newaddoncmd") && !Registrar.Has("replacementleak"), "failed replacement preserves old lifetime/publication");

                string[] Blocked = Registry.RegisterArchive(Provider, Archive(Manifest("blockedaddon", "1.0.0", "{\"required\":[\"missing\"],\"optional\":[]}"),
                    new EntrySpec("init.luau", "error('must not run')")));
                IsState(Blocked, "Blocked", "dependency-bearing addon remains Blocked");
                Check(!Registry.ProcessOne() || Registry.Status(Provider, Blocked[1])[2] == "Blocked", "blocked addon never executes");

                string[] Pending = Registry.RegisterSource(Provider, "pendingunload", "1.0.0", Bytes("return true"));
                int Reachable = Registry.Count;
                Check(Registry.UnloadProvider(Provider) == Reachable, "provider unload retires Registered, Blocked, Active, and Failed registrations");
                Check(Registry.Count == 0 && Host.DomainCount == 1 && !Registrar.Has("newaddoncmd"), "provider unload returns domains/resources to baseline");
                Check(Registry.Status(Provider, Pending[1])[0] == "ERROR", "unloaded registration token is stale");
                Check(Registry.Unregister(Provider, ArchiveToken)[0] == "OK", "unregister is idempotent for bounded same-provider tombstone");

                for (int Cycle = 0; Cycle < 100; ++Cycle) {
                    string Id = "cycle" + Cycle;
                    string[] CycleRegistration = Registry.RegisterSource(Provider, Id, "1.0.0", Bytes("return true"));
                    Process(Registry); IsState(Registry.Status(Provider, CycleRegistration[1]), "Active", "cycle activation");
                    Check(Registry.Unregister(Provider, CycleRegistration[1])[0] == "OK", "cycle unregister");
                }
                Check(Registry.Count == 0 && Host.DomainCount == 1, "100 lifecycle cycles return to baseline");

                string[] Repeated = Registry.RegisterSource(Provider, "repeated", "1.0.0", Bytes("return true")); Process(Registry);
                for (int Cycle = 1; Cycle <= 100; ++Cycle) {
                    Registry.ReplaceSource(Provider, Repeated[1], "1.0." + Cycle, Bytes("return " + Cycle)); Process(Registry);
                    IsState(Registry.Status(Provider, Repeated[1]), "Active", "repeated replacement");
                    Check(Host.DomainCount == 2, "replacement retains one addon domain");
                }
                Registry.Unregister(Provider, Repeated[1]); Check(Host.DomainCount == 1, "repeated replacements clean up");

                string[] Stale = Registry.RegisterSource(Provider, "stale", "1.0.0", Bytes("return true")); Process(Registry);
                string StaleToken = Stale[1]; Registry.Unregister(Provider, StaleToken);
                Check(Registry.Status(Provider, StaleToken)[0] == "ERROR", "status rejects stale token");
            }
            Check(Host.DomainCount == 1, "registry disposal preserves root only");
        }
        Check(Native.LiveVmCount == 0, "Foundation B host teardown returns VM count to baseline");

        InvalidArchive(Archive("{", new EntrySpec("init.luau", "return true")), "malformed JSON rejected");
        InvalidArchive(Archive("{\"schema\":1,\"id\":\"dup\",\"id\":\"dup\",\"version\":\"1.0.0\"}", new EntrySpec("init.luau", "return true")), "duplicate JSON key rejected");
        InvalidArchive(Archive("{\"schema\":1,\"id\":\"unknown\",\"version\":\"1.0.0\",\"main\":\"api\"}", new EntrySpec("init.luau", "return true")), "out-of-scope manifest field rejected");
        InvalidArchive(Archive(Manifest("badpath"), new EntrySpec("init.luau", "return true"), new EntrySpec("../bad.luau", "return true")), "traversal path rejected");
        InvalidArchive(Archive(Manifest("abspath"), new EntrySpec("init.luau", "return true"), new EntrySpec("/bad.luau", "return true")), "absolute path rejected");
        InvalidArchive(Archive(Manifest("backslash"), new EntrySpec("init.luau", "return true"), new EntrySpec("bad\\name.luau", "return true")), "backslash path rejected");
        InvalidArchive(Archive(Manifest("dotpath"), new EntrySpec("init.luau", "return true"), new EntrySpec("a/./bad.luau", "return true")), "dot path rejected");
        InvalidArchive(Archive(Manifest("duplicatepath"), new EntrySpec("init.luau", "return true"), new EntrySpec("same.luau", "return 1"), new EntrySpec("same.luau", "return 2")), "duplicate normalized path rejected");
        InvalidArchive(Archive(Manifest("nestedarchive"), new EntrySpec("init.luau", "return true"), new EntrySpec("nested.zip", "x")), "nested archive rejected");
        InvalidArchive(Archive(Manifest("symlink"), new EntrySpec("init.luau", "return true"), new EntrySpec("link.luau", "return true") {ExternalAttributes = unchecked((int)0xA1FF0000)}), "symlink entry rejected");
        byte[] PlainZip = Archive(Manifest("zipflags"), new EntrySpec("init.luau", "return true"));
        InvalidArchive(MutateZipFlags(PlainZip, true, false), "encrypted entry rejected");
        InvalidArchive(MutateZipFlags(PlainZip, false, true), "unsupported compression rejected");
        InvalidArchive(Archive(Manifest("utf8"), new EntrySpec("init.luau", new byte[] {255})), "malformed UTF-8 source rejected");
        bool OversizeSource = false;
        try { Runtime.AddonPackageSnapshot.FromSource("large", "1.0.0", new byte[Runtime.AddonPolicy.MaxSourceBytes + 1]); }
        catch (InvalidOperationException) { OversizeSource = true; }
        Check(OversizeSource, "single source limit rejected");
        InvalidArchive(new byte[Runtime.AddonPolicy.MaxArchiveBytes + 1], "archive byte limit rejected");
        var LargeSources = new List<EntrySpec>(); LargeSources.Add(new EntrySpec("init.luau", new string(' ', Runtime.AddonPolicy.MaxSourceBytes)));
        for (int Index = 0; Index < 64; ++Index) LargeSources.Add(new EntrySpec("m" + Index + ".luau", new string(' ', Runtime.AddonPolicy.MaxSourceBytes)));
        InvalidArchive(Archive(Manifest("aggregate"), LargeSources.ToArray()), "cumulative decompressed/source limit rejected");
        string ManyDependencies = "{\"required\":["; for (int Index = 0; Index < 33; ++Index) ManyDependencies += (Index == 0 ? "" : ",") + "\"d" + Index + "\"";
        ManyDependencies += "],\"optional\":[]}";
        InvalidArchive(Archive(Manifest("manydeps", "1.0.0", ManyDependencies), new EntrySpec("init.luau", "return true")), "dependency limit rejected");
        InvalidArchive(Archive(Manifest("selfdep", "1.0.0", "{\"required\":[\"selfdep\"],\"optional\":[]}"),
            new EntrySpec("init.luau", "return true")), "self dependency rejected");
        Console.WriteLine("[CarbonLuau:AddonTest] PASS package/parser bounds; ownership; Active/Blocked/Failed; replacement; unload; 100 cycles and replacements");
    }

    private static void Fill(byte[] Values, byte Value)
    {
        for (int Index = 0; Index < Values.Length; ++Index) Values[Index] = Value;
    }
}
