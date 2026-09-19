using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class FoundationETests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { }
    }

    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Foundation E: " + Message); }
    private static byte[] Bytes(string Text) { return Utf8.GetBytes(Text); }
    private static void DrainRegistry(Runtime.AddonRegistry Registry, int Maximum = 256)
    {
        for (int Count = 0; Count < Maximum && Registry.HasPending; ++Count)
            Check(Registry.ProcessOne(), "pending registration did not make progress");
    }
    private static void IsState(string[] Response, string State, string Message)
    { Check(Response[0] == "OK" && Response[2] == State, Message + ": " + String.Join("|", Response)); }

    public static void Run(Runtime.NativeRuntime Native)
    {
        foreach (int Count in new[] {0, 1, 10, 50, 100}) RunScale(Native, Count);
        RunSaturationFairness(Native);
        RunSharedHeapExhaustion(Native);
        RunPackageBoundaries(Native);
        Console.WriteLine("[CarbonLuau:FoundationE] PASS scale/resource, scheduler fairness, shared heap, parser and aggregate limits");
    }

    private static void RunScale(Runtime.NativeRuntime Native, int Count)
    {
        var Views = new Dictionary<string, Runtime.PlayerView>();
        const string UserId = "76561198000000010";
        var View = new Runtime.PlayerView {Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Foundation E", Connected = true, Send = Message => { }, Permission = Permission => true};
        Views.Add(UserId, View);
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => Views.ContainsKey(Id) ? Views[Id] : null), new Registrar());
        var Config = new Runtime.RuntimeConfig {MaxVmMemoryMiB = 64, MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object[] Providers = {new object(), new object(), new object(), new object()};
        int Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2);
        long ManagedBefore = GC.GetTotalMemory(true);
        Process CurrentProcess = Process.GetCurrentProcess(); CurrentProcess.Refresh();
        long PeakBefore = CurrentProcess.PeakWorkingSet64;
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "scale root bootstrap " + Count);
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                var Tokens = new List<string>(); int ExpectedSnapshotBytes = 0;
                var Activation = Stopwatch.StartNew();
                for (int Index = 0; Index < Count; ++Index) {
                    string Id = "scale" + Index.ToString("D3", CultureInfo.InvariantCulture);
                    string Source = "local Marker='" + Id + "'; local State={Marker=Marker,Payload=string.rep('x',1024)}; " +
                        "game:GetService('Players').PlayerAdded:Connect(function() print(Marker) end); " +
                        "task.delay(86400,function() return State end); return true";
                    byte[] SourceBytes = Bytes(Source); ExpectedSnapshotBytes += SourceBytes.Length;
                    string[] Registration = Registry.RegisterSource(Providers[Index / Runtime.AddonPolicy.MaxRegistrationsPerProvider],
                        Id, "1.0.0", SourceBytes);
                    Check(Registration[0] == "OK", "scale registration " + Count + "/" + Index); Tokens.Add(Registration[1]);
                    Check(Registry.ProcessOne(), "scale activation " + Count + "/" + Index);
                    IsState(Registry.Status(Providers[Index / Runtime.AddonPolicy.MaxRegistrationsPerProvider], Registration[1]),
                        "Active", "scale active " + Count + "/" + Index);
                }
                Activation.Stop();
                Check(Host.DomainCount == (ulong)Count + 1, "root plus representative addon domains " + Count);
                Check(Registry.SnapshotBytes == ExpectedSnapshotBytes, "immutable snapshot byte accounting " + Count);

                double ReplacementMilliseconds = 0;
                if (Count != 0) {
                    int Index = Count - 1; string Id = "scale" + Index.ToString("D3", CultureInfo.InvariantCulture);
                    string Source = "local Marker='" + Id + "'; game:GetService('Players').PlayerAdded:Connect(function() print(Marker) end); " +
                        "task.delay(86400,function() end); return true";
                    var Replacement = Stopwatch.StartNew();
                    string[] Result = Registry.ReplaceSource(Providers[Index / Runtime.AddonPolicy.MaxRegistrationsPerProvider], Tokens[Index], "2.0.0", Bytes(Source));
                    Check(Result[0] == "OK" && Registry.ProcessOne(), "representative replacement " + Count);
                    Replacement.Stop(); ReplacementMilliseconds = Replacement.Elapsed.TotalMilliseconds;
                    IsState(Registry.Status(Providers[Index / Runtime.AddonPolicy.MaxRegistrationsPerProvider], Tokens[Index]), "Active", "replacement active " + Count);
                }

                int P50 = 0, P95 = 0, P99 = 0, Maximum = 0;
                if (Count != 0) {
                    Runtime.PlayerLifetime Player = World.Players.Connect(View);
                    World.Event("added", Player);
                    int Pending = 0;
                    foreach (Runtime.FacadeSession Session in World.Sessions()) Pending += Session.PendingCount;
                    Check(Pending == Count, "event fanout is one bounded delivery per listening addon " + Count);
                    List<Runtime.ExecutionResult> Results = Host.Drain();
                    var Ordinals = new List<int>();
                    for (int Index = 0; Index < Results.Count; ++Index)
                        if (Results[Index].Logs.StartsWith("scale", StringComparison.Ordinal)) Ordinals.Add(Index + 1);
                    Check(Ordinals.Count == Count, "all representative addon callbacks progress in one bounded drain " + Count);
                    Ordinals.Sort(); P50 = Percentile(Ordinals, 50); P95 = Percentile(Ordinals, 95);
                    P99 = Percentile(Ordinals, 99); Maximum = Ordinals[Ordinals.Count - 1];
                    Check(Maximum <= Count + 1 && Results.Count <= 256, "global callback work is bounded and fair " + Count);
                    Check(Host.HasWork && !Host.HasReadyWork, "future-only tasks do not request persistent frame drains " + Count);
                    ulong Due; double Delay; Check(Host.TryGetNextDue(out Due, out Delay) && Delay > 86000,
                        "future callback exposes one delayed wakeup " + Count);
                }

                var Idle = Stopwatch.StartNew();
                for (int Sample = 0; Sample < 10000; ++Sample) Check(!Host.HasReadyWork, "idle readiness remains false " + Count);
                Idle.Stop();
                ulong VmBytes = Host.VmMemoryBytes;
                Check(VmBytes < Host.VmMemoryLimitBytes, "representative configuration remains below 64 MiB " + Count);
                long ManagedAfter = GC.GetTotalMemory(true); CurrentProcess.Refresh();
                Console.WriteLine("[CarbonLuau:FoundationE] SCALE addons=" + Count +
                    " vmBytes=" + VmBytes + " snapshotBytes=" + Registry.SnapshotBytes +
                    " managedBytes=" + ManagedAfter + " managedDelta=" + (ManagedAfter - ManagedBefore) +
                    " rssBytes=" + CurrentProcess.WorkingSet64 + " peakDeltaBytes=" + Math.Max(0, CurrentProcess.PeakWorkingSet64 - PeakBefore) +
                    " gc=" + (GC.CollectionCount(0) - Gen0) + "/" + (GC.CollectionCount(1) - Gen1) + "/" + (GC.CollectionCount(2) - Gen2) +
                    " activationMs=" + Activation.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                    " replacementMs=" + ReplacementMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                    " serviceOrdinalP50/P95/P99/max=" + P50 + "/" + P95 + "/" + P99 + "/" + Maximum +
                    " idleChecksMs=" + Idle.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            }
            Check(Host.DomainCount == 1, "scale registry teardown returns to root " + Count);
        }
    }

    private static void RunSaturationFairness(Runtime.NativeRuntime Native)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        var Config = new Runtime.RuntimeConfig {MaxVmMemoryMiB = 64, MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 5};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object Provider = new object();
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "fairness root bootstrap");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                string[] Saturated = Registry.RegisterSource(Provider, "fair00", "1.0.0", Bytes(
                    "for I=1,256 do task.defer(function() local X=0 for J=1,100000 do X+=J end end) end"));
                Check(Saturated[0] == "OK" && Registry.ProcessOne(), "saturated addon activates");
                for (int Index = 1; Index <= 10; ++Index) {
                    string Id = "fair" + Index.ToString("D2", CultureInfo.InvariantCulture);
                    string[] Result = Registry.RegisterSource(Provider, Id, "1.0.0", Bytes("task.defer(function() print('" + Id + "') end)"));
                    Check(Result[0] == "OK" && Registry.ProcessOne(), "unrelated fairness addon " + Index);
                }
                var Seen = new HashSet<string>(StringComparer.Ordinal); int Frames = 0;
                while (Frames < 10 && Seen.Count < 10) {
                    Frames++;
                    foreach (Runtime.ExecutionResult Result in Host.Drain()) {
                        string Marker = Result.Logs.Trim(); if (Marker.StartsWith("fair", StringComparison.Ordinal) && Marker != "fair00") Seen.Add(Marker);
                    }
                }
                Check(Seen.Count == 10, "one saturated addon does not starve ten unrelated addons");
                Check(Host.SchedulerSnapshot.Queued <= 256, "saturated domain queue remains bounded");
                Console.WriteLine("[CarbonLuau:FoundationE] FAIR saturatedQueued=256 unrelated=10 maxServiceFrames=" + Frames +
                    " remaining=" + Host.SchedulerSnapshot.Queued);
            }
        }
    }

    private static void RunSharedHeapExhaustion(Runtime.NativeRuntime Native)
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        var Config = new Runtime.RuntimeConfig {MaxVmMemoryMiB = 64, MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        object Provider = new object();
        using (var Host = new Runtime.ScriptHost(Native, Config, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "shared heap root bootstrap");
            long VmGeneration = Host.VmGenerationId; int Active = 0; bool Rejected = false;
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                for (int Index = 0; Index < Runtime.AddonPolicy.MaxRegistrationsPerProvider; ++Index) {
                    string Id = "heap" + Index.ToString("D2", CultureInfo.InvariantCulture);
                    string[] Registration = Registry.RegisterSource(Provider, Id, "1.0.0", Bytes(
                        "local B=buffer.create(4194304); task.delay(86400,function() return B end); return true"));
                    Check(Registration[0] == "OK" && Registry.ProcessOne(), "heap pressure registration " + Index);
                    string[] Status = Registry.Status(Provider, Registration[1]);
                    if (Status[2] == "Failed") { Check(Status[5].Contains("MEMORY_LIMIT"), "heap pressure fails with controlled memory status"); Rejected = true; break; }
                    IsState(Status, "Active", "heap pressure active " + Index); Active++;
                }
                Check(Rejected && Active >= 8, "64 MiB shared heap reaches a controlled global limit after representative retained allocations");
                Check(Host.Ready && Host.VmGenerationId == VmGeneration, "ordinary shared heap exhaustion preserves healthy VM generation");
                Check(Host.Execute("heap-survival", "return 7").Status == Runtime.RuntimeStatus.OK,
                    "unrelated root operation survives addon allocation rejection");
                Console.WriteLine("[CarbonLuau:FoundationE] HEAP limitBytes=" + Host.VmMemoryLimitBytes +
                    " usedBytes=" + Host.VmMemoryBytes + " retained4MiBAddons=" + Active + " controlledReject=true");
            }
        }
    }

    private static void RunPackageBoundaries(Runtime.NativeRuntime Native)
    {
        byte[] ExactSource = new byte[Runtime.AddonPolicy.MaxSourceBytes];
        for (int Index = 0; Index < ExactSource.Length; ++Index) ExactSource[Index] = (byte)' ';
        Check(Runtime.AddonPackageSnapshot.FromSource("sourceexact", "1.0.0", ExactSource).SourceBytes ==
            Runtime.AddonPolicy.MaxSourceBytes, "exact single-source boundary accepted");
        bool SourceOverflow = false;
        try { Runtime.AddonPackageSnapshot.FromSource("sourceoverflow", "1.0.0", new byte[Runtime.AddonPolicy.MaxSourceBytes + 1]); }
        catch (InvalidOperationException) { SourceOverflow = true; }
        Check(SourceOverflow, "single-source boundary plus one rejected");

        byte[] Bomb = Archive("actualbytes", Runtime.AddonPolicy.MaxSourceBytes + 1, true);
        MutateUncompressedSize(Bomb, Runtime.AddonPolicy.MaxSourceBytes);
        bool ActualRejected = false;
        try { Runtime.AddonPackageSnapshot.FromArchive(Bomb); }
        catch (InvalidOperationException) { ActualRejected = true; }
        Check(ActualRejected, "streamed decompressed bytes are enforced independently of forged ZIP size metadata");

        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        Func<Runtime.ScriptSnapshot> Root = () => new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = "return true"};
        using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100}, Root, World)) {
            Check(Host.Reload().Status == Runtime.RuntimeStatus.OK, "snapshot aggregate root bootstrap");
            using (var Registry = new Runtime.AddonRegistry(Host, Native.HostLifetimeId)) {
                object Provider = new object();
                for (int Index = 0; Index < 8; ++Index) {
                    string[] Result = Registry.RegisterArchive(Provider, Archive("aggregate" + Index, Runtime.AddonPolicy.MaxAggregateSourceBytes, false));
                    Check(Result[0] == "OK", "aggregate snapshot package " + Index);
                }
                Check(Registry.SnapshotBytes == Runtime.AddonPolicy.MaxAggregateSnapshotBytes, "exact 32 MiB aggregate snapshot accepted");
                Check(Registry.RegisterArchive(Provider, Archive("aggregateoverflow", Runtime.AddonPolicy.MaxAggregateSourceBytes, false))[0] == "ERROR",
                    "aggregate snapshot boundary plus one package rejected");
                Console.WriteLine("[CarbonLuau:FoundationE] PACKAGE aggregateSnapshotBytes=" + Registry.SnapshotBytes +
                    " exactLimitAccepted=true overflowRejected=true actualDecompressedBytesRejected=true");
            }
        }
    }

    private static int Percentile(List<int> Values, int Percent)
    {
        if (Values.Count == 0) return 0;
        int Index = (int)Math.Ceiling(Values.Count * Percent / 100.0) - 1;
        return Values[Math.Max(0, Math.Min(Values.Count - 1, Index))];
    }

    private static byte[] Archive(string Id, int SourceBytes, bool OneSource)
    {
        using (var Output = new MemoryStream()) {
            using (var Zip = new ZipArchive(Output, ZipArchiveMode.Create, true)) {
                Write(Zip, "addon.json", Bytes("{\"schema\":1,\"id\":\"" + Id + "\",\"version\":\"1.0.0\"}"));
                int Remaining = SourceBytes, Index = 0;
                while (Remaining != 0) {
                    int Length = OneSource ? Remaining : Math.Min(Runtime.AddonPolicy.MaxSourceBytes, Remaining);
                    string Name = Index == 0 ? "init.luau" : "module" + Index.ToString("D3", CultureInfo.InvariantCulture) + ".luau";
                    byte[] Source = new byte[Length]; for (int Byte = 0; Byte < Source.Length; ++Byte) Source[Byte] = (byte)' ';
                    Write(Zip, Name, Source); Remaining -= Length; Index++;
                }
            }
            return Output.ToArray();
        }
    }

    private static void Write(ZipArchive Zip, string Name, byte[] Value)
    {
        ZipArchiveEntry Entry = Zip.CreateEntry(Name, CompressionLevel.Optimal);
        using (Stream Stream = Entry.Open()) Stream.Write(Value, 0, Value.Length);
    }

    private static void MutateUncompressedSize(byte[] Archive, int Size)
    {
        byte[] Value = BitConverter.GetBytes(Size);
        for (int Offset = 0; Offset + 46 <= Archive.Length; ++Offset) {
            uint Signature = BitConverter.ToUInt32(Archive, Offset);
            int Field = Signature == 0x04034b50 ? Offset + 22 : Signature == 0x02014b50 ? Offset + 24 : -1;
            if (Field < 0) continue;
            Buffer.BlockCopy(Value, 0, Archive, Field, Value.Length);
        }
    }
}
