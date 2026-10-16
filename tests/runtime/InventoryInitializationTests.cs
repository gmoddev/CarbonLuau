using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class InventoryNetObservationTests
{
    // Separate fresh process: never warms up or retries the original --inventory-net
    // assertion. The hang executable is the existing private compiler fixture.
    public static void RunInitialization(string NativePath, string CompilerPath, string HangPath)
    {
        foreach (string Mode in new[] { "bootstrap-timeout", "entry-compile", "module-compile", "runtime", "entry-deadline" }) {
            string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CarbonLuauInventoryInit-" + Guid.NewGuid().ToString("N")));
            Exception Primary = null;
            try {
                string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
                string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
                Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(NativePath, Target);
                string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
                File.Copy(Mode == "bootstrap-timeout" ? HangPath : CompilerPath, Worker);
                if (Rid == "linux-x64") Check(chmod(Worker, 493) == 0, "initialization compiler executable mode");
                using (var Native = new Runtime.NativeRuntime(Root))
                using (var Value = new Fixture()) {
                    Player Player = Value.Connect(); object Scrap = Value.Item("scrap"); Player.Set(Scrap, 3);
                    string Acquisition = "local S=require('state'); S.P=game:GetService('Players'):GetPlayers()[1]; S.I=S.P:GetInventory(); assert(S.I==S.P:GetInventory())";
                    string Source = Mode == "entry-compile" ? "local =" : Acquisition;
                    if (Mode == "runtime") Source += "; error('controlled-init-failure')";
                    if (Mode == "entry-deadline") Source += "; while true do end";
                    string Module = Mode == "module-compile" ? "local =" : "return {}";
                    Func<Runtime.ScriptSnapshot> Snapshot = () => {
                        var Result = new Runtime.ScriptSnapshot { EntryName = "init.luau", EntrySource = Source };
                        Result.Modules.Add("state", Module); return Result;
                    };
                    using (var Host = new Runtime.ScriptHost(Native, new Runtime.RuntimeConfig { MaxCallbackMilliseconds = 100 }, Snapshot, Value.World)) {
                        var Clock = System.Diagnostics.Stopwatch.StartNew();
                        var Failed = Host.Reload(); Clock.Stop();
                        TestDiagnostics.Observe("init.status", (long)Failed.Status, 0, (long)Failed.Status);
                        TestDiagnostics.Observe("init.scans", Player.Scans, 0);
                        TestDiagnostics.Observe("init.watches", Value.Samples.WatchCount, 0);
                        TestDiagnostics.Observe("init.demand", Value.Samples.DemandCount, 0);
                        TestDiagnostics.Observe("init.pending", Value.Samples.PendingCount, 0);
                        TestDiagnostics.Observe("init.liveVms", Native.LiveVmCount, 0);
                        Console.WriteLine("[CarbonLuau:InventoryInit] INJECTED mode=" + Mode + " status=" + Failed.Status +
                            " retired=" + Failed.Retired + " scans=" + Player.Scans + " watches=" + Value.Samples.WatchCount +
                            " liveVms=" + Native.LiveVmCount + " elapsedMs=" + Clock.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                            " error=" + Failed.Error);
                        Check(Player.Scans == 0 && Value.Samples.WatchCount == 0 && Value.Samples.DemandCount == 0 &&
                            Value.Samples.PendingCount == 0, "rejected initialization never scans or stages inventory demand");
                        Check(!Host.Ready && Value.World.Active == null && Native.LiveVmCount == 0,
                            "rejected first initialization releases VM and facade authority");
                        Runtime.RuntimeStatus Expected = Mode == "bootstrap-timeout" || Mode == "entry-deadline" ? Runtime.RuntimeStatus.TIMEOUT :
                            Mode == "entry-compile" ? Runtime.RuntimeStatus.COMPILE_ERROR : Runtime.RuntimeStatus.RUNTIME_ERROR;
                        Check(Failed.Status == Expected, "initialization failure category preserved: " + Mode + " got " + Failed.Status + ": " + Failed.Error);
                        Check(Failed.Retired == (Mode == "entry-deadline"), "compiler failure and execution retirement stay distinct");
                        if (Mode == "bootstrap-timeout") {
                            Check(Failed.Error.Contains("domain facade") && Failed.Error.Contains("TIMEOUT"), "bootstrap phase diagnostic");
                            // Explicit operator recovery after removing a known injected fault.
                            // No failed user operation is replayed or accepted by retry.
                            File.Copy(CompilerPath, Worker, true);
                            if (Rid == "linux-x64") Check(chmod(Worker, 493) == 0, "restored compiler executable mode");
                        }
                        Source = Acquisition; Module = "return {}";
                        var Recovered = Host.Reload();
                        Check(Recovered.Status == Runtime.RuntimeStatus.OK && Host.Ready && Player.Scans == 0 &&
                            Value.Samples.WatchCount == 0, "explicit corrected-source recovery: " + Recovered.Status + ": " + Recovered.Error);
                        var Owner = Value.World.Active;
                        ulong Demand = Value.Stage(Owner, Player); Value.Step();
                        Player.Set(Scrap, 4); Value.Step(); Change(Value.Samples, Demand, "scrap", 3, 4);
                        long VmId = Host.VmGenerationId;
                        Source = "error('controlled replacement rejection')";
                        Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Value.World.Active, Owner) &&
                            Host.VmGenerationId == VmId && !Owner.Disposed, "failed replacement preserves published inventory owner");
                        Source = Acquisition;
                        Native.RefuseNextFacadeInitialization(Runtime.RuntimeStatus.TIMEOUT);
                        var SetupFailure = Host.Reload();
                        Check(SetupFailure.Status == Runtime.RuntimeStatus.TIMEOUT && !SetupFailure.Retired &&
                            ReferenceEquals(Value.World.Active, Owner) && !Owner.Disposed && Host.VmGenerationId == VmId,
                            "native setup timeout preserves category and published root");
                        Player.Set(Scrap, 5); Value.Step(); Change(Value.Samples, Demand, "scrap", 4, 5);
                        Native.RefuseNextFacadeInitialization(Runtime.RuntimeStatus.MEMORY_LIMIT);
                        var AddonFailure = Host.ActivateAddon(Runtime.AddonPackageSnapshot.FromSource("initfault", "1.0.0", System.Text.Encoding.UTF8.GetBytes("return")),
                            null, new Runtime.AddonDomainBinding[0]);
                        Check(AddonFailure.Result.Status == Runtime.RuntimeStatus.MEMORY_LIMIT && AddonFailure.Domain == null &&
                            Host.Ready && ReferenceEquals(Value.World.Active, Owner) && Value.Samples.DemandCount == 1 &&
                            Host.DomainCount == 1, "addon setup memory failure preserves category, root and observer ownership");
                        Check(Host.Reload().Status == Runtime.RuntimeStatus.OK && Owner.Disposed && Host.VmGenerationId == VmId &&
                            Value.Samples.WatchCount == 0 && Value.Samples.PendingCount == 0, "successful replacement retires old demand without replay");
                    }
                    Check(Native.LiveVmCount == 0 && Value.Samples.WatchCount == 0, "initialization case teardown");
                    using (var Host = new Runtime.RuntimeHost(Native, new Runtime.RuntimeConfig { MaxCallbackMilliseconds = 100 })) {
                        Native.RefuseNextVmInitialization();
                        Check(Host.Reload().Status == Runtime.RuntimeStatus.MEMORY_LIMIT && Native.LiveVmCount == 0,
                            "shared runtime initializer preserves checked VM allocation failure");
                    }
                }
            } catch (Exception Error) { Primary = Error; TestDiagnostics.ExceptionFailure(Error); throw; }
            finally { TestDiagnostics.Cleanup(() => {
                string Temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Check(Root.StartsWith(Temporary, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(Root).StartsWith("CarbonLuauInventoryInit-", StringComparison.Ordinal), "confined initialization fixture cleanup");
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }, Primary); }
        }
        Console.WriteLine("[CarbonLuau:InventoryInit] PASS five independent injected failures, status/retirement, explicit recovery, replacement, observer ownership and cleanup; not a historical-failure attribution");
    }
}

// Test assembly only. One checked ABI return is refused before any facade
// installation; the real delegate is restored immediately, with no retry.
namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class NativeRuntime
        {
            internal void RefuseNextVmInitialization()
            {
                CheckOwner();
                CreateDelegate Original = CreateVm;
                CreateVm = (ref VmConfig Config, out ulong Vm) => {
                    CreateVm = Original; Vm = 0; return RuntimeStatus.MEMORY_LIMIT;
                };
            }

            internal void RefuseNextFacadeInitialization(RuntimeStatus Status)
            {
                CheckOwner();
                if (InstallDomainFacade == null) {
                    InstallDomainFacade = Loader.Bind<DomainFacadeDelegate>("cl_domain_facade");
                    AdmitDomainEvent = Loader.Bind<DomainEventDelegate>("cl_domain_event");
                }
                DomainFacadeDelegate Original = InstallDomainFacade;
                InstallDomainFacade = (Vm, Domain, Host) => { InstallDomainFacade = Original; return Status; };
            }
        }
    }
}
