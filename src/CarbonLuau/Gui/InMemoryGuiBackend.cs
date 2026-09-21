using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class InMemoryGuiBackend : IGuiBackend
        {
            internal sealed class Call
            {
                internal readonly long Sequence; internal readonly GuiBackendOperationKind Kind;
                internal readonly GuiBackendTarget Target; internal readonly GuiRenderPlan Plan;
                internal readonly GuiRenderPatch Patch; internal readonly GuiScrollEffect ScrollEffect; internal readonly GuiBackendResult Result;
                internal Call(long Sequence, GuiBackendOperationKind Kind, GuiBackendTarget Target,
                    GuiRenderPlan Plan, GuiRenderPatch Patch, GuiScrollEffect ScrollEffect, GuiBackendResult Result)
                { this.Sequence = Sequence; this.Kind = Kind; this.Target = Target; this.Plan = Plan; this.Patch = Patch; this.ScrollEffect = ScrollEffect; this.Result = Result; }
            }

            private sealed class State
            { internal GuiRenderPlan Plan; internal GuiRenderPatch LastPatch; }
            private sealed class InjectedFailure
            { internal readonly GuiBackendResultCode Code; internal readonly string Diagnostic; internal InjectedFailure(GuiBackendResultCode Code, string Diagnostic) { this.Code = Code; this.Diagnostic = Diagnostic; } }

            private readonly Dictionary<string, State> States = new Dictionary<string, State>(StringComparer.Ordinal);
            private readonly Dictionary<GuiBackendOperationKind, Queue<InjectedFailure>> Failures = new Dictionary<GuiBackendOperationKind, Queue<InjectedFailure>>();
            private readonly List<Call> CallValues = new List<Call>();
            private long NextSequence = 1;

            public int MeasureReplace(GuiBackendTarget Target, GuiRenderPlan Plan)
            { Required(Target, Plan); return Plan.EstimatedSerializedBytes; }
            public int MeasureUpdate(GuiBackendTarget Target, GuiRenderPatch Patch)
            { Required(Target, Patch); return Patch.EstimatedSerializedBytes; }
            public int MeasureDestroy(GuiBackendTarget Target)
            { if (Target == null) throw new InvalidOperationException("backend target is required"); return GuiRenderValue.Utf8Bytes(Target.ClientRootId); }
            public int MeasureScroll(GuiBackendTarget Target, GuiScrollEffect Effect)
            { Required(Target, Effect); return GuiRenderValue.Utf8Bytes(Effect.Describe()); }

            internal void FailNext(GuiBackendOperationKind Kind, GuiBackendResultCode Code, string Diagnostic)
            {
                if (Code == GuiBackendResultCode.Accepted || Code == GuiBackendResultCode.TargetUnavailable)
                    throw new InvalidOperationException("injected backend failure must represent a send failure");
                GuiBackendResult.Failure(Code, Diagnostic);
                Queue<InjectedFailure> Queue;
                if (!Failures.TryGetValue(Kind, out Queue)) { Queue = new Queue<InjectedFailure>(); Failures.Add(Kind, Queue); }
                Queue.Enqueue(new InjectedFailure(Code, Diagnostic));
            }

            public GuiBackendResult Replace(GuiBackendTarget Target, GuiRenderPlan Plan)
            {
                Required(Target, Plan); GuiBackendResult Result = ResultFor(GuiBackendOperationKind.Replace);
                if (Result.Accepted) States[Target.Key] = new State { Plan = Plan };
                Record(GuiBackendOperationKind.Replace, Target, Plan, null, null, Result); return Result;
            }

            public GuiBackendResult Update(GuiBackendTarget Target, GuiRenderPatch Patch)
            {
                Required(Target, Patch); State Current;
                GuiBackendResult Result = !States.TryGetValue(Target.Key, out Current)
                    ? GuiBackendResult.Failure(GuiBackendResultCode.TargetUnavailable, "presentation target is not live")
                    : ResultFor(GuiBackendOperationKind.Update);
                if (Result.Accepted) Current.LastPatch = Patch;
                Record(GuiBackendOperationKind.Update, Target, null, Patch, null, Result); return Result;
            }

            public GuiBackendResult Destroy(GuiBackendTarget Target)
            {
                if (Target == null) throw new InvalidOperationException("backend target is required");
                GuiBackendResult Result = ResultFor(GuiBackendOperationKind.Destroy);
                if (Result.Accepted) States.Remove(Target.Key);
                Record(GuiBackendOperationKind.Destroy, Target, null, null, null, Result); return Result;
            }

            public GuiBackendResult Scroll(GuiBackendTarget Target, GuiScrollEffect Effect)
            {
                Required(Target, Effect); State Current;
                GuiBackendResult Result = !States.TryGetValue(Target.Key, out Current)
                    ? GuiBackendResult.Failure(GuiBackendResultCode.TargetUnavailable, "presentation target is not live")
                    : ResultFor(GuiBackendOperationKind.Scroll);
                Record(GuiBackendOperationKind.Scroll, Target, null, null, Effect, Result); return Result;
            }

            internal bool IsLive(GuiBackendTarget Target) { return Target != null && States.ContainsKey(Target.Key); }
            internal GuiRenderPlan CurrentPlan(GuiBackendTarget Target)
            { State Value; return Target != null && States.TryGetValue(Target.Key, out Value) ? Value.Plan : null; }
            internal GuiRenderPatch LastPatch(GuiBackendTarget Target)
            { State Value; return Target != null && States.TryGetValue(Target.Key, out Value) ? Value.LastPatch : null; }
            internal Call[] Calls() { return CallValues.ToArray(); }

            private GuiBackendResult ResultFor(GuiBackendOperationKind Kind)
            {
                Queue<InjectedFailure> Queue;
                if (Failures.TryGetValue(Kind, out Queue) && Queue.Count != 0) {
                    InjectedFailure Failure = Queue.Dequeue(); return GuiBackendResult.Failure(Failure.Code, Failure.Diagnostic);
                }
                return GuiBackendResult.Success();
            }
            private void Record(GuiBackendOperationKind Kind, GuiBackendTarget Target, GuiRenderPlan Plan, GuiRenderPatch Patch, GuiBackendResult Result)
            { Record(Kind, Target, Plan, Patch, null, Result); }
            private void Record(GuiBackendOperationKind Kind, GuiBackendTarget Target, GuiRenderPlan Plan, GuiRenderPatch Patch,
                GuiScrollEffect ScrollEffect, GuiBackendResult Result)
            { CallValues.Add(new Call(NextSequence++, Kind, Target, Plan, Patch, ScrollEffect, Result)); }
            private static void Required(object First, object Second)
            { if (First == null || Second == null) throw new InvalidOperationException("backend target and request are required"); }
        }
    }
}
