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
        public sealed partial class AddonRegistry
        {
            private void RefreshGraph()
            {
                bool Changed; int Passes = 0;
                do {
                    if (++Passes > AddonPolicy.MaxRegistrations + 1) throw new InvalidOperationException("dependency loss propagation bound exceeded");
                    Changed = false;
                    foreach (Registration Value in OrderedRegistrations()) {
                        if (Value.State != AddonRegistrationState.Active) continue;
                        string Lost = null;
                        if (Value.Domain == null || !Value.Domain.Alive) Lost = "VM generation retired";
                        else foreach (string Id in Value.Snapshot.Dependencies(false)) {
                            DependencyBinding Binding;
                            if (!Value.Bindings.TryGetValue(Id, out Binding) || !BindingCurrent(Binding)) {
                                Lost = "required dependency binding lost or stale: " + Id; break;
                            }
                        }
                        if (Lost == null) {
                            if (Value.PendingSnapshot != null) {
                                string PendingMissing = MissingRequired(Value.PendingSnapshot);
                                if (HasRequiredCycle(Value, Value.PendingSnapshot)) Value.Failure = "replacement blocked: required dependency cycle/SCC";
                                else if (PendingMissing != null) Value.Failure = "replacement blocked: required dependency unavailable: " + PendingMissing;
                                else if (String.IsNullOrEmpty(Value.Failure) || Value.Failure.StartsWith("replacement blocked", StringComparison.Ordinal))
                                    Value.Failure = "replacement dependencies ready; activation queued";
                            } else if (String.IsNullOrEmpty(Value.Failure) || Value.Failure.StartsWith("optional dependency", StringComparison.Ordinal))
                                Value.Failure = ActiveBindingDiagnostic(Value);
                            continue;
                        }
                        if (Value.Domain != null && Value.Domain.Alive) Host.RetireAddon(Value.Domain);
                        Value.Domain = null; Value.State = AddonRegistrationState.Blocked; Value.RestorationPending = true;
                        Value.Failure = Lost + "; deterministic restoration pending"; Changed = true;
                    }
                } while (Changed);
                foreach (Registration Value in OrderedRegistrations()) {
                    if (Value.State == AddonRegistrationState.Active || Value.State == AddonRegistrationState.Failed ||
                        Value.State == AddonRegistrationState.Stopping || Value.State == AddonRegistrationState.Initializing) continue;
                    AddonPackageSnapshot Candidate = Value.PendingSnapshot ?? Value.Snapshot;
                    if (HasRequiredCycle(Value, Candidate)) {
                        Value.State = AddonRegistrationState.Blocked; Value.Failure = "required dependency cycle/SCC blocks activation"; continue;
                    }
                    string Missing = MissingRequired(Candidate);
                    if (Missing != null) {
                        Value.State = AddonRegistrationState.Blocked;
                        Value.Failure = "required dependency unavailable: " + Missing; continue;
                    }
                    if (Value.State == AddonRegistrationState.Blocked) Value.RestorationPending = true;
                    Value.State = AddonRegistrationState.Registered;
                    Value.Failure = Value.RestorationPending ? "required dependencies restored; one activation attempt queued" : "";
                }
            }
            private List<Registration> OrderedRegistrations()
            {
                var Values = new List<Registration>(Registrations.Values);
                Values.Sort((Left, Right) => StringComparer.Ordinal.Compare(Left.Snapshot.Id, Right.Snapshot.Id)); return Values;
            }
            private Registration FindNext()
            {
                foreach (Registration Value in OrderedRegistrations()) {
                    if (Value.State == AddonRegistrationState.Registered) return Value;
                    if (Value.PendingSnapshot != null && Value.State == AddonRegistrationState.Active &&
                        (HasRequiredCycle(Value, Value.PendingSnapshot) || MissingRequired(Value.PendingSnapshot) == null)) return Value;
                }
                return null;
            }
            private string MissingRequired(AddonPackageSnapshot Snapshot)
            {
                foreach (string Id in Snapshot.Dependencies(false)) {
                    Registration Target;
                    if (!Ids.TryGetValue(Id, out Target) || Target.State != AddonRegistrationState.Active ||
                        Target.Domain == null || !Target.Domain.Alive) return Id;
                }
                return null;
            }
            private Dictionary<string, DependencyBinding> BuildBindings(AddonPackageSnapshot Snapshot)
            {
                var Result = new Dictionary<string, DependencyBinding>(StringComparer.Ordinal);
                foreach (string Id in Snapshot.Dependencies(false)) Result.Add(Id, CreateBinding(Id, false, true));
                foreach (string Id in Snapshot.Dependencies(true)) Result.Add(Id, CreateBinding(Id, true, false));
                return Result;
            }
            private DependencyBinding CreateBinding(string Id, bool Optional, bool Required)
            {
                Registration Target;
                bool Available = Ids.TryGetValue(Id, out Target) && Target.State == AddonRegistrationState.Active &&
                    Target.Domain != null && Target.Domain.Alive;
                if (Required && !Available) throw new InvalidOperationException("required dependency changed before activation: " + Id);
                return new DependencyBinding {Id = Id, Optional = Optional, Target = Available ? Target : null,
                    VmGenerationId = Available ? Target.Domain.VmGenerationId : 0,
                    DomainLifetimeId = Available ? Target.Domain.DomainLifetimeId : 0};
            }
            private static bool BindingCurrent(DependencyBinding Binding)
            {
                return Binding.Target != null && Binding.Target.State == AddonRegistrationState.Active &&
                    Binding.Target.Domain != null && Binding.Target.Domain.Alive &&
                    Binding.Target.Domain.VmGenerationId == Binding.VmGenerationId &&
                    Binding.Target.Domain.DomainLifetimeId == Binding.DomainLifetimeId;
            }
            private static bool BindingsCurrent(Dictionary<string, DependencyBinding> Bindings)
            {
                foreach (DependencyBinding Binding in Bindings.Values) if (!Binding.Optional && !BindingCurrent(Binding)) return false;
                return true;
            }
            private static AddonDomainBinding[] RuntimeBindings(Dictionary<string, DependencyBinding> Bindings)
            {
                var Ordered = new List<DependencyBinding>(Bindings.Values);
                Ordered.Sort((Left, Right) => StringComparer.Ordinal.Compare(Left.Id, Right.Id));
                var Result = new AddonDomainBinding[Ordered.Count];
                for (int Index = 0; Index < Ordered.Count; ++Index)
                    Result[Index] = new AddonDomainBinding {Id = Ordered[Index].Id,
                        Target = BindingCurrent(Ordered[Index]) ? Ordered[Index].Target.Domain : null};
                return Result;
            }
            private static string ActiveBindingDiagnostic(Registration Value)
            {
                foreach (string Id in Value.Snapshot.Dependencies(true)) {
                    DependencyBinding Binding; if (!Value.Bindings.TryGetValue(Id, out Binding)) continue;
                    if (Binding.Target == null) return "optional dependency absent for this domain: " + Binding.Id;
                    if (!BindingCurrent(Binding)) return "optional dependency binding unavailable/stale: " + Binding.Id;
                }
                return "";
            }
            private bool HasRequiredCycle(Registration Start, AddonPackageSnapshot Candidate)
            {
                var Visiting = new HashSet<Registration>(); var Visited = new HashSet<Registration>(); int Edges = 0;
                return VisitRequired(Start, Start, Candidate, Visiting, Visited, ref Edges);
            }
            private bool VisitRequired(Registration Current, Registration Start, AddonPackageSnapshot Candidate,
                HashSet<Registration> Visiting, HashSet<Registration> Visited, ref int Edges)
            {
                if (!Visiting.Add(Current)) return Object.ReferenceEquals(Current, Start);
                AddonPackageSnapshot Snapshot = Object.ReferenceEquals(Current, Start) ? Candidate :
                    Current.State == AddonRegistrationState.Active ? Current.Snapshot : Current.PendingSnapshot ?? Current.Snapshot;
                foreach (string Id in Snapshot.Dependencies(false)) {
                    if (++Edges > AddonPolicy.MaxGraphEdges) throw new InvalidOperationException("dependency graph edge bound exceeded");
                    Registration Target; if (!Ids.TryGetValue(Id, out Target)) continue;
                    if (Object.ReferenceEquals(Target, Start)) return true;
                    if (!Visited.Contains(Target) && VisitRequired(Target, Start, Candidate, Visiting, Visited, ref Edges)) return true;
                }
                Visiting.Remove(Current); Visited.Add(Current); return false;
            }
            internal string[] BindingStatus(object Provider, string Token, string DependencyId)
            {
                CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token); RefreshGraph();
                DependencyBinding Binding;
                if (!Value.Bindings.TryGetValue(DependencyId ?? "", out Binding)) return new[] {"undeclared", "", "", ""};
                return new[] {Binding.Optional ? "optional" : "required",
                    Binding.Target == null ? "absent" : BindingCurrent(Binding) ? "available" : "stale",
                    Binding.VmGenerationId.ToString(CultureInfo.InvariantCulture), Binding.DomainLifetimeId.ToString(CultureInfo.InvariantCulture)};
            }
            internal bool ValidateBinding(object Provider, string Token, string DependencyId, long VmGenerationId, long DomainLifetimeId)
            {
                string[] Status = BindingStatus(Provider, Token, DependencyId);
                return Status[1] == "available" && Status[2] == VmGenerationId.ToString(CultureInfo.InvariantCulture) &&
                    Status[3] == DomainLifetimeId.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}

