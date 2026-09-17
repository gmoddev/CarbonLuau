// Reference: System.IO.Compression
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class AddonActivation
        {
            public ExecutionResult Result; public RuntimeDomain Domain;
        }

        public sealed partial class ScriptHost
        {
            private readonly SortedDictionary<long, RuntimeDomain> AddonDomains = new SortedDictionary<long, RuntimeDomain>();
            private long FacadeDrainCursor;
            public ulong DomainCount { get { return Vm == null || !Vm.Alive ? 0 : Native.GenerationInfo(Vm.Handle).Domains; } }
            internal AddonActivation ActivateAddon(AddonPackageSnapshot Package, RuntimeDomain Previous, AddonDomainBinding[] Bindings)
            {
                Native.CheckOwner(); var Activation = new AddonActivation {Result = new ExecutionResult {Status = RuntimeStatus.INVALID_ARGUMENT, Error = "runtime unavailable or busy"}};
                if (!Ready || Busy) return Activation;
                RuntimeDomain Candidate = null; Busy = true;
                try {
                    ScriptSnapshot Snapshot = Package.ToScriptSnapshot();
                    Candidate = new RuntimeDomain(Native, Vm, Settings, Snapshot);
                    Candidate.Addon(Package, Bindings);
                    if (Facade != null) Candidate.Facade(new FacadeSession(Facade, Candidate.VmGenerationId, Candidate.DomainLifetimeId, Settings.MaxQueuedCallbacks));
                    ExecutionResult Result = Candidate.Execute("addon." + Package.Id + ".init", Snapshot.EntrySource, Settings.MaxCallbackMilliseconds);
                    Activation.Result = Result;
                    if (Result.Status != RuntimeStatus.OK) {
                        if (Result.Retired || Vm.Info.Ready == 0) {
                            var Recovery = Recover();
                            Result.Error += "; root recovery=" + (Recovery == null ? "exhausted" : Recovery.Status.ToString());
                        }
                        return Activation;
                    }
                    Candidate.Commit();
                    if (Facade != null) Facade.CommitAddon(Previous == null ? null : Previous.FacadeSession, Candidate.FacadeSession);
                    AddonDomains.Add(Candidate.DomainLifetimeId, Candidate);
                    if (Previous != null) { AddonDomains.Remove(Previous.DomainLifetimeId); Previous.Dispose(); }
                    Activation.Domain = Candidate; Candidate = null; return Activation;
                } catch (Exception Error) {
                    Activation.Result = new ExecutionResult {Status = RuntimeStatus.INTERNAL_ERROR,
                        Error = AddonPolicy.Diagnostic(Error is InvalidOperationException ? Error.Message : "addon native initialization failed")};
                    return Activation;
                } finally {
                    if (Candidate != null) {
                        if (Candidate.FacadeSession != null && Facade != null) Facade.Retire(Candidate.FacadeSession);
                        Candidate.Dispose();
                    }
                    Busy = false;
                }
            }
            internal void RetireAddon(RuntimeDomain Domain)
            {
                Native.CheckOwner(); if (Domain == null) return;
                AddonDomains.Remove(Domain.DomainLifetimeId);
                if (Domain.FacadeSession != null && Facade != null) Facade.Retire(Domain.FacadeSession);
                Domain.Dispose();
            }
            private void ReleaseAddonDomains()
            {
                var Values = new List<RuntimeDomain>(AddonDomains.Values); AddonDomains.Clear();
                foreach (RuntimeDomain Domain in Values) {
                    if (Domain.FacadeSession != null && Facade != null) Facade.Retire(Domain.FacadeSession);
                    Domain.Dispose();
                }
            }
            private void FlushDomainFacades(Stopwatch Watch)
            {
                var Domains = new List<RuntimeDomain>();
                if (Current != null && Current.Alive && Current.FacadeSession != null) Domains.Add(Current);
                foreach (RuntimeDomain Domain in AddonDomains.Values)
                    if (Domain.Alive && Domain.FacadeSession != null) Domains.Add(Domain);
                for (int Admitted = 0; Admitted < 256 && Watch.Elapsed.TotalMilliseconds < Settings.FrameDrainBudgetMilliseconds; ++Admitted) {
                    RuntimeDomain Selected = null;
                    foreach (RuntimeDomain Domain in Domains) if (Domain.FacadeSession.PendingCount != 0 &&
                        Domain.DomainLifetimeId > FacadeDrainCursor && (Selected == null || Domain.DomainLifetimeId < Selected.DomainLifetimeId)) Selected = Domain;
                    if (Selected == null) foreach (RuntimeDomain Domain in Domains) if (Domain.FacadeSession.PendingCount != 0 &&
                        (Selected == null || Domain.DomainLifetimeId < Selected.DomainLifetimeId)) Selected = Domain;
                    if (Selected == null) break;
                    Selected.FacadeSession.Flush(Selected, Watch, Settings.FrameDrainBudgetMilliseconds, 1);
                    FacadeDrainCursor = Selected.DomainLifetimeId;
                }
            }
        }
    }
}

