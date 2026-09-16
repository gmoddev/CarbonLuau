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
    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("Addon foundations: " + Message); }
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
    private static byte[] Package(string Id, string[] Required, string[] Optional, string Source = "return true", string Version = "1.0.0")
    {
        var Text = new StringBuilder("{\"required\":[");
        for (int Index = 0; Index < Required.Length; ++Index) Text.Append(Index == 0 ? "\"" : ",\"").Append(Required[Index]).Append('"');
        Text.Append("],\"optional\":[");
        for (int Index = 0; Index < Optional.Length; ++Index) Text.Append(Index == 0 ? "\"" : ",\"").Append(Optional[Index]).Append('"');
        Text.Append("]}");
        return Archive(Manifest(Id, Version, Text.ToString()), new EntrySpec("init.luau", Source));
    }
    private static string[] RegisterPackage(Runtime.AddonRegistry Registry, object Provider, string Id,
        string[] Required, string[] Optional, string Source = "return true", string Version = "1.0.0")
    { return Registry.RegisterArchive(Provider, Package(Id, Required, Optional, Source, Version)); }

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
        RunDependencyLifecycle(Native);

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
        Console.WriteLine("[CarbonLuau:AddonTest] PASS package/parser bounds; ownership; dependency lifecycle; Active/Blocked/Failed; replacement; unload; graph bounds and stress");
    }

    private static void RunDependencyLifecycle(Runtime.NativeRuntime Native)
    {
        var Registrar = new Registrar();
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), Registrar);
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object DependencyProvider = new object(), ConsumerProvider = new object(), OtherProvider = new object();
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "Foundation C root baseline");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                string[] Free = Registry.RegisterSource(OtherProvider, "free", "1.0.0", Bytes("return true"));
                Process(Registry); IsState(Registry.Status(OtherProvider, Free[1]), "Active", "dependency-free activation regression");

                string[] Missing = RegisterPackage(Registry, ConsumerProvider, "requiredconsumer", new[] {"requireddep"}, new string[0]);
                IsState(Missing, "Blocked", "required missing blocks before execution");
                Check(Missing[5].Contains("requireddep"), "required missing diagnostic names dependency");
                string[] RequiredDependency = Registry.RegisterSource(DependencyProvider, "requireddep", "1.0.0", Bytes("return true"));
                Check(Registry.ProcessOne(), "required dependency activation scheduled");
                IsState(Registry.Status(DependencyProvider, RequiredDependency[1]), "Active", "required dependency activates first");
                IsState(Registry.Status(ConsumerProvider, Missing[1]), "Registered", "required consumer becomes eligible after dependency");
                Check(Registry.ProcessOne(), "required consumer activation scheduled");
                string[] RequiredActive = Registry.Status(ConsumerProvider, Missing[1]);
                IsState(RequiredActive, "Active", "required present activates consumer deterministically");
                string[] RequiredBinding = Registry.BindingStatus(ConsumerProvider, Missing[1], "requireddep");
                Check(RequiredBinding[0] == "required" && RequiredBinding[1] == "available", "required exact lifetime binding published");
                long RequiredVm = Int64.Parse(RequiredBinding[2]), RequiredLifetime = Int64.Parse(RequiredBinding[3]);
                Check(Registry.ValidateBinding(ConsumerProvider, Missing[1], "requireddep", RequiredVm, RequiredLifetime), "required binding validates exact lifetime");

                string[] OptionalMissing = RegisterPackage(Registry, ConsumerProvider, "optionalmissing", new string[0], new[] {"lateroptional"});
                IsState(OptionalMissing, "Registered", "optional missing does not block registration"); Process(Registry);
                string OptionalDomain = Registry.Status(ConsumerProvider, OptionalMissing[1])[7];
                string[] OptionalAbsent = Registry.BindingStatus(ConsumerProvider, OptionalMissing[1], "lateroptional");
                Check(OptionalAbsent[0] == "optional" && OptionalAbsent[1] == "absent", "optional absence fixed for consumer lifetime");
                string[] LaterOptional = Registry.RegisterSource(DependencyProvider, "lateroptional", "1.0.0", Bytes("return true")); Process(Registry);
                Check(Registry.Status(ConsumerProvider, OptionalMissing[1])[7] == OptionalDomain &&
                    Registry.BindingStatus(ConsumerProvider, OptionalMissing[1], "lateroptional")[1] == "absent",
                    "optional package appearing later does not hot-bind");

                string[] OptionalTarget = Registry.RegisterSource(DependencyProvider, "optionaltarget", "1.0.0", Bytes("return true")); Process(Registry);
                string[] OptionalConsumer = RegisterPackage(Registry, ConsumerProvider, "optionalconsumer", new string[0], new[] {"optionaltarget"}); Process(Registry);
                string[] OptionalBinding = Registry.BindingStatus(ConsumerProvider, OptionalConsumer[1], "optionaltarget");
                string OptionalConsumerDomain = Registry.Status(ConsumerProvider, OptionalConsumer[1])[7];
                Check(OptionalBinding[1] == "available", "optional present binds exact lifetime");
                long OptionalVm = Int64.Parse(OptionalBinding[2]), OptionalLifetime = Int64.Parse(OptionalBinding[3]);
                Registry.Unregister(DependencyProvider, OptionalTarget[1]);
                IsState(Registry.Status(ConsumerProvider, OptionalConsumer[1]), "Active", "optional loss does not retire consumer");
                Check(Registry.Status(ConsumerProvider, OptionalConsumer[1])[7] == OptionalConsumerDomain &&
                    Registry.BindingStatus(ConsumerProvider, OptionalConsumer[1], "optionaltarget")[1] == "stale" &&
                    !Registry.ValidateBinding(ConsumerProvider, OptionalConsumer[1], "optionaltarget", OptionalVm, OptionalLifetime),
                    "optional loss preserves domain and rejects stale exact binding");
                string[] OptionalReload = Registry.RegisterSource(OtherProvider, "optionaltarget", "2.0.0", Bytes("return true")); Process(Registry);
                Check(Registry.BindingStatus(ConsumerProvider, OptionalConsumer[1], "optionaltarget")[1] == "stale",
                    "provider reload with fresh target lifetime does not retarget optional binding");

                string[] ChainBase = Registry.RegisterSource(DependencyProvider, "chainbase", "1.0.0", Bytes("return true")); Process(Registry);
                string[] ChainMid = RegisterPackage(Registry, ConsumerProvider, "chainmid", new[] {"chainbase"}, new string[0]); Process(Registry);
                string[] ChainTop = RegisterPackage(Registry, ConsumerProvider, "chaintop", new[] {"chainmid"}, new string[0]); Process(Registry);
                string[] Unrelated = Registry.RegisterSource(OtherProvider, "unrelated", "1.0.0", Bytes("return true")); Process(Registry);
                string OldMid = Registry.Status(ConsumerProvider, ChainMid[1])[7], OldTop = Registry.Status(ConsumerProvider, ChainTop[1])[7];
                Registry.Unregister(DependencyProvider, ChainBase[1]);
                IsState(Registry.Status(ConsumerProvider, ChainMid[1]), "Blocked", "required dependency loss blocks direct consumer");
                IsState(Registry.Status(ConsumerProvider, ChainTop[1]), "Blocked", "required dependency loss blocks transitive consumer");
                IsState(Registry.Status(OtherProvider, Unrelated[1]), "Active", "unrelated addon survives dependency loss");
                Check(Registry.Status(ConsumerProvider, ChainMid[1])[7] == "" && Registry.Status(ConsumerProvider, ChainTop[1])[7] == "",
                    "affected subgraph domains retired");
                string[] RestoredBase = Registry.RegisterSource(OtherProvider, "chainbase", "2.0.0", Bytes("return true"));
                Check(Registry.ProcessOne(), "restored dependency activates");
                IsState(Registry.Status(ConsumerProvider, ChainMid[1]), "Registered", "direct dependent queued after restoration");
                IsState(Registry.Status(ConsumerProvider, ChainTop[1]), "Blocked", "transitive dependent waits for topology");
                Check(Registry.ProcessOne(), "direct dependent restores");
                IsState(Registry.Status(ConsumerProvider, ChainTop[1]), "Registered", "transitive dependent queued after direct restoration");
                Check(Registry.ProcessOne(), "transitive dependent restores");
                Check(Registry.Status(ConsumerProvider, ChainMid[1])[7] != OldMid && Registry.Status(ConsumerProvider, ChainTop[1])[7] != OldTop,
                    "deterministic topological restoration creates fresh lifetimes");

                string[] ReplaceDependency = Registry.RegisterSource(DependencyProvider, "replaceddep", "1.0.0", Bytes("return true")); Process(Registry);
                string[] ReplaceRequired = RegisterPackage(Registry, ConsumerProvider, "replacerequired", new[] {"replaceddep"}, new string[0]); Process(Registry);
                string[] ReplaceOptional = RegisterPackage(Registry, ConsumerProvider, "replaceoptional", new string[0], new[] {"replaceddep"}); Process(Registry);
                string OldDependency = Registry.Status(DependencyProvider, ReplaceDependency[1])[7];
                string OldRequired = Registry.Status(ConsumerProvider, ReplaceRequired[1])[7];
                string OldOptional = Registry.Status(ConsumerProvider, ReplaceOptional[1])[7];
                Registry.ReplaceSource(DependencyProvider, ReplaceDependency[1], "1.1.0", Bytes("error('replacement failure')")); Process(Registry);
                Check(Registry.Status(DependencyProvider, ReplaceDependency[1])[7] == OldDependency &&
                    Registry.Status(ConsumerProvider, ReplaceRequired[1])[7] == OldRequired &&
                    Registry.BindingStatus(ConsumerProvider, ReplaceRequired[1], "replaceddep")[1] == "available",
                    "failed dependency replacement preserves old dependency and binding");
                Registry.ReplaceSource(DependencyProvider, ReplaceDependency[1], "2.0.0", Bytes("return true"));
                Check(Registry.ProcessOne(), "successful dependency replacement processed");
                string NewDependency = Registry.Status(DependencyProvider, ReplaceDependency[1])[7];
                Check(NewDependency != OldDependency, "successful dependency replacement publishes fresh lifetime");
                IsState(Registry.Status(ConsumerProvider, ReplaceRequired[1]), "Registered", "required consumer queued after replacement");
                Check(Registry.BindingStatus(ConsumerProvider, ReplaceRequired[1], "replaceddep")[1] == "stale" &&
                    !Registry.ValidateBinding(ConsumerProvider, ReplaceRequired[1], "replaceddep", RequiredVm, Int64.Parse(OldDependency)),
                    "successful replacement stales old required binding before restoration");
                IsState(Registry.Status(ConsumerProvider, ReplaceOptional[1]), "Active", "optional consumer remains active across replacement");
                Check(Registry.Status(ConsumerProvider, ReplaceOptional[1])[7] == OldOptional &&
                    Registry.BindingStatus(ConsumerProvider, ReplaceOptional[1], "replaceddep")[1] == "stale",
                    "optional consumer does not silently rebind on replacement");
                Check(Registry.ProcessOne(), "required consumer reinitializes after replacement");
                Check(Registry.Status(ConsumerProvider, ReplaceRequired[1])[7] != OldRequired &&
                    Registry.BindingStatus(ConsumerProvider, ReplaceRequired[1], "replaceddep")[3] == NewDependency,
                    "required consumer binds replacement lifetime");
                string RepeatedRequiredDomain = Registry.Status(ConsumerProvider, ReplaceRequired[1])[7];
                for (int Cycle = 1; Cycle <= 50; ++Cycle) {
                    Registry.ReplaceSource(DependencyProvider, ReplaceDependency[1], "2.0." + Cycle, Bytes("return " + Cycle)); Process(Registry);
                    string CurrentRequiredDomain = Registry.Status(ConsumerProvider, ReplaceRequired[1])[7];
                    Check(CurrentRequiredDomain != RepeatedRequiredDomain &&
                        Registry.BindingStatus(ConsumerProvider, ReplaceRequired[1], "replaceddep")[3] == Registry.Status(DependencyProvider, ReplaceDependency[1])[7],
                        "repeated dependency replacement cycle " + Cycle);
                    RepeatedRequiredDomain = CurrentRequiredDomain;
                }

                string[] Changing = Registry.RegisterSource(OtherProvider, "changingdeps", "1.0.0", Bytes("return true")); Process(Registry);
                string ChangingOld = Registry.Status(OtherProvider, Changing[1])[7];
                string[] ChangingPending = Registry.ReplaceArchive(OtherProvider, Changing[1],
                    Package("changingdeps", new[] {"newrequirement"}, new string[0], "return true", "2.0.0"));
                IsState(ChangingPending, "Active", "dependency-aware replacement preserves old active candidate while blocked");
                Check(ChangingPending[7] == ChangingOld && ChangingPending[8] == "2.0.0" && ChangingPending[5].Contains("newrequirement") && !Registry.HasPending,
                    "blocked replacement reports missing requirement without frame retry-loop");
                string[] NewRequirement = Registry.RegisterSource(DependencyProvider, "newrequirement", "1.0.0", Bytes("return true")); Process(Registry);
                Check(Registry.Status(OtherProvider, Changing[1])[7] != ChangingOld &&
                    Registry.BindingStatus(OtherProvider, Changing[1], "newrequirement")[1] == "available",
                    "dependency-aware replacement commits only after exact required binding exists");
                string[] CycleAnchor = RegisterPackage(Registry, ConsumerProvider, "cycleanchor", new[] {"changingdeps"}, new string[0]); Process(Registry);
                string ChangingCommitted = Registry.Status(OtherProvider, Changing[1])[7];
                string CycleAnchorDomain = Registry.Status(ConsumerProvider, CycleAnchor[1])[7];
                Registry.ReplaceArchive(OtherProvider, Changing[1],
                    Package("changingdeps", new[] {"cycleanchor"}, new string[0], "return true", "3.0.0"));
                Check(Registry.ProcessOne(), "cyclic replacement candidate is processed as bounded failure");
                Check(Registry.Status(OtherProvider, Changing[1])[7] == ChangingCommitted && Registry.Status(OtherProvider, Changing[1])[8] == "" &&
                    Registry.Status(OtherProvider, Changing[1])[5].Contains("cycle/SCC") &&
                    Registry.Status(ConsumerProvider, CycleAnchor[1])[7] == CycleAnchorDomain,
                    "cyclic replacement preserves old dependency graph and active exact bindings");

                string[] FailureDependency = Registry.RegisterSource(DependencyProvider, "failuredep", "1.0.0", Bytes("return true")); Process(Registry);
                string[] FailureConsumer = RegisterPackage(Registry, ConsumerProvider, "failureconsumer", new[] {"failuredep"}, new string[0]); Process(Registry);
                Registry.Unregister(DependencyProvider, FailureDependency[1]);
                IsState(Registry.Status(ConsumerProvider, FailureConsumer[1]), "Blocked", "restoration fixture blocks on loss");
                string[] FailureReplacement = Registry.ReplaceArchive(ConsumerProvider, FailureConsumer[1],
                    Package("failureconsumer", new[] {"failuredep"}, new string[0], "error('restoration failure')", "2.0.0"));
                Check(FailureReplacement[0] == "OK", "blocked consumer accepts explicit replacement candidate");
                string[] FailureRestored = Registry.RegisterSource(OtherProvider, "failuredep", "2.0.0", Bytes("return true")); Process(Registry);
                IsState(Registry.Status(ConsumerProvider, FailureConsumer[1]), "Failed", "failed restoration becomes Failed");
                Check(Registry.Status(ConsumerProvider, FailureConsumer[1])[5].Contains("restoration failed") && !Registry.ProcessOne(),
                    "failed restoration receives one bounded attempt and does not retry-loop");

                string[] RequiredA = RegisterPackage(Registry, DependencyProvider, "requireda", new[] {"requiredb"}, new string[0]);
                string[] RequiredB = RegisterPackage(Registry, OtherProvider, "requiredb", new[] {"requireda"}, new string[0]);
                IsState(Registry.Status(DependencyProvider, RequiredA[1]), "Blocked", "required cycle first SCC member blocked");
                IsState(Registry.Status(OtherProvider, RequiredB[1]), "Blocked", "required cycle second SCC member blocked");
                Check(Registry.Status(DependencyProvider, RequiredA[1])[5].Contains("cycle/SCC") && !Registry.HasPending,
                    "required SCC diagnostic and no activation spin");
                string[] OptionalA = RegisterPackage(Registry, DependencyProvider, "optionala", new string[0], new[] {"optionalb"});
                string[] OptionalB = RegisterPackage(Registry, OtherProvider, "optionalb", new string[0], new[] {"optionala"}); Process(Registry);
                IsState(Registry.Status(DependencyProvider, OptionalA[1]), "Active", "optional cycle first member active");
                IsState(Registry.Status(OtherProvider, OptionalB[1]), "Active", "optional cycle second member active");
                string[] MixedA = RegisterPackage(Registry, DependencyProvider, "mixeda", new[] {"mixedb"}, new string[0]);
                string[] MixedB = RegisterPackage(Registry, OtherProvider, "mixedb", new string[0], new[] {"mixeda"}); Process(Registry);
                IsState(Registry.Status(DependencyProvider, MixedA[1]), "Active", "mixed graph required consumer active");
                IsState(Registry.Status(OtherProvider, MixedB[1]), "Active", "optional back-edge does not create required cycle");

                string[] ProviderBase = Registry.RegisterSource(DependencyProvider, "providerbase", "1.0.0", Bytes("return true")); Process(Registry);
                string[] ProviderConsumer = RegisterPackage(Registry, ConsumerProvider, "providerconsumer", new[] {"providerbase"}, new string[0]); Process(Registry);
                string ProviderConsumerOld = Registry.Status(ConsumerProvider, ProviderConsumer[1])[7];
                int Removed = Registry.UnloadProvider(DependencyProvider);
                Check(Removed != 0, "provider unload retires owned dependency registrations");
                IsState(Registry.Status(ConsumerProvider, ProviderConsumer[1]), "Blocked", "provider unload propagates required dependency loss");
                IsState(Registry.Status(OtherProvider, Unrelated[1]), "Active", "provider unload leaves unrelated addon active");
                object ReloadedProvider = new object();
                string[] ReloadedBase = Registry.RegisterSource(ReloadedProvider, "providerbase", "2.0.0", Bytes("return true")); Process(Registry);
                IsState(Registry.Status(ConsumerProvider, ProviderConsumer[1]), "Active", "provider reload restores required dependent");
                Check(Registry.Status(ConsumerProvider, ProviderConsumer[1])[7] != ProviderConsumerOld &&
                    Registry.BindingStatus(ConsumerProvider, ProviderConsumer[1], "providerbase")[3] == Registry.Status(ReloadedProvider, ReloadedBase[1])[7],
                    "provider reload binds fresh dependency and consumer lifetimes");
            }
            Check(Host.DomainCount == 1, "Foundation C lifecycle registry teardown returns graph/resources to root baseline");
        }
        Check(Native.LiveVmCount == 0, "Foundation C lifecycle host teardown returns VM baseline");
        RunGraphBounds(Native, Config, Root);
    }

    private static void RunGraphBounds(Runtime.NativeRuntime Native, Runtime.RuntimeConfig Config, Func<Runtime.ScriptSnapshot> Root)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "graph-bound root baseline");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                object[] Providers = {new object(), new object(), new object(), new object()};
                var Tokens = new string[Runtime.AddonPolicy.MaxRegistrations];
                for (int Index = Runtime.AddonPolicy.MaxRegistrations - 1; Index >= 0; --Index) {
                    string Id = "graph" + Index.ToString("D3");
                    string[] Required;
                    if (Index == 0) Required = new string[0];
                    else if (Index == Runtime.AddonPolicy.MaxRegistrations - 1) {
                        Required = new string[Runtime.AddonPolicy.MaxDependencies];
                        for (int Dependency = 0; Dependency < Required.Length; ++Dependency)
                            Required[Dependency] = "graph" + (Index - Required.Length + Dependency).ToString("D3");
                    } else Required = new[] {"graph" + (Index - 1).ToString("D3")};
                    string[] Result = RegisterPackage(Registry, Providers[Index / Runtime.AddonPolicy.MaxRegistrationsPerProvider], Id, Required, new string[0]);
                    Check(Result[0] == "OK", "near-bound graph registration " + Index); Tokens[Index] = Result[1];
                }
                Check(Registry.Count == Runtime.AddonPolicy.MaxRegistrations, "registration graph reaches canonical 128-node bound");
                Check(Registry.RegisterSource(new object(), "graphoverflow", "1.0.0", Bytes("return true"))[0] == "ERROR",
                    "registration graph rejects node beyond bound");
                Process(Registry, Runtime.AddonPolicy.MaxRegistrations + 1);
                IsState(Registry.Status(Providers[3], Tokens[127]), "Active", "128-node reverse-registered chain restores topologically");
                Check(Registry.BindingStatus(Providers[3], Tokens[127], "graph095")[1] == "available" &&
                    Registry.BindingStatus(Providers[3], Tokens[127], "graph126")[1] == "available",
                    "operational fan-in reaches the canonical 32-dependency bound");
                Check(Host.DomainCount == 129, "128 addon domains plus root are live at graph bound");
                Check(Registry.UnloadProvider(Providers[0]) == Runtime.AddonPolicy.MaxRegistrationsPerProvider,
                    "bounded provider removal mutates graph deterministically");
                IsState(Registry.Status(Providers[3], Tokens[127]), "Blocked", "near-bound transitive subgraph retires after dependency loss");
                for (int Index = 1; Index < Providers.Length; ++Index)
                    Check(Registry.UnloadProvider(Providers[Index]) == Runtime.AddonPolicy.MaxRegistrationsPerProvider,
                        "remaining graph provider teardown " + Index);
                Check(Registry.Count == 0 && Host.DomainCount == 1, "near-bound graph/resource state returns to baseline");
            }
        }
        Check(Native.LiveVmCount == 0, "graph-bound VM returns to baseline");
    }

    private static void Fill(byte[] Values, byte Value)
    {
        for (int Index = 0; Index < Values.Length; ++Index) Values[Index] = Value;
    }
}
