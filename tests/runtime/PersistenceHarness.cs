using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    // Reflection is confined to observing existing private queue state; no test
    // hook changes production behavior or treats a synthetic reply as durability.
    private static Runtime.StorageQueue.Request[] Reservations(Runtime.StorageQueue Queue)
    {
        return (Runtime.StorageQueue.Request[])typeof(Runtime.StorageQueue)
            .GetField("Reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Queue);
    }
    private static Runtime.StorageQueue.Request OnlyRequest(Fixture F)
    {
        Runtime.StorageQueue.Request Result = null;
        foreach (var Request in Reservations(F.Queue)) if (Request != null) {
            Check(Result == null, "expected exactly one reservation"); Result = Request;
        }
        Check(Result != null, "expected a reservation"); return Result;
    }
    private static void WaitFor(Func<bool> Done, Action Advance, string Message, int Milliseconds = 12000)
    {
        var Watch = Stopwatch.StartNew();
        while (!Done() && Watch.ElapsedMilliseconds < Milliseconds) { Advance(); Thread.Sleep(5); }
        Check(Done(), Message);
    }
    private static void RemoveOwnedDirectory(string Directory)
    {
        // Only paths returned by NewDirectory below reach this method. Never
        // remove the caller's fixture directory or native-data installation.
        for (int Attempt = 0; Attempt < 3; ++Attempt) {
            try { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); return; }
            catch (IOException) { if (Attempt == 2) throw; }
            catch (UnauthorizedAccessException) { if (Attempt == 2) throw; }
            Thread.Sleep(100);
        }
    }
    private static string NewDirectory(string Parent)
    {
        string Result = Path.Combine(Path.GetFullPath(Parent), "Persistence1B-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Result);
        return Result;
    }
    internal static void RunStandalone(string Library, string Worker, string FixtureDirectory, bool Closure = false)
    {
        Library = Path.GetFullPath(Library); Worker = Path.GetFullPath(Worker);
        string Root = NewDirectory(FixtureDirectory);
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.Copy(Library, Target);
            string CompilerName = Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler";
            string Compiler = Path.Combine(Path.GetDirectoryName(Target), CompilerName);
            File.Copy(Path.Combine(Path.GetDirectoryName(Library), CompilerName), Compiler);
            if (Rid == "linux-x64") Check(PersistenceChmod(Compiler, 493) == 0, "staged compiler executable mode");
            using (var Native = new Runtime.NativeRuntime(Root)) {
                if (Closure) RunClosure(Native, Worker, Root);
                else Run(Native, Worker, Root);
            }
        } finally { RemoveOwnedDirectory(Root); }
    }
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int PersistenceChmod(string Path, uint Mode);

    internal static void Run(Runtime.NativeRuntime Native, string Worker, string FixtureDirectory)
    {
        Worker = Path.GetFullPath(Worker);
        Check(File.Exists(Worker), "production storage worker required: " + Worker);
        Check(Native.AbiVersion == 0x00010005, "Persistence-1B ABI 1.5");
        string Root = NewDirectory(FixtureDirectory);
        try {
            RoundTrips(Native, Worker, Path.Combine(Root, "roundtrips"));
            AttachedStorageRequiresAbi15(Native);
            DeterministicAdmission(Native);
            GlobalCapacity(Native);
            using (var F = new Fixture(Native, Worker, Path.Combine(Root, "observations"))) PerformanceObservations(F);
            using (var F = new Fixture(Native, Worker, Path.Combine(Root, "stress"))) Stress(F);
            using (var F = new Fixture(Native, Worker, Path.Combine(Root, "quota"))) StoreQuota(F);
            string Suffix = Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : "";
            string WorkerDirectory = Path.GetDirectoryName(Worker);
            // Existing CMake fault fixtures are deliberately required, never
            // silently skipped. CI builds them alongside the production worker.
            foreach (bool Committed in new[] { true, false }) {
                string FaultWorker = Path.Combine(WorkerDirectory, (Committed ? "StorageFixtureLostAck" : "StorageFixtureHang") + Suffix);
                Check(File.Exists(FaultWorker), "existing fault worker required: " + FaultWorker);
                WorkerFailure(Native, FaultWorker, Path.Combine(Root, Committed ? "lost-ack" : "hang"), Committed);
            }
            Check(Native.Storage == null && Native.LiveVmCount == 0, "suite leaves no queue or VM");
            Console.WriteLine("[CarbonLuau:Persistence1B] ALL PASS; real worker cases and explicitly synthetic admission cases; no 1C platform/power-loss qualification claim");
        } finally { RemoveOwnedDirectory(Root); }
    }
}
