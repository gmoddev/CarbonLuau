using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    private static double ElapsedMilliseconds(long Start, long End)
    { return (End - Start) * 1000.0 / Stopwatch.Frequency; }

    private static void PerformanceObservations(Fixture F)
    {
        // Six operation/payload combinations, three samples each. Startup and
        // pacing are outside the measurements. No latency pass/fail threshold:
        // the existing finite fixture waits still catch stuck work.
        Console.WriteLine("[CarbonLuau:Persistence1BPerf] observations only; samples_per_case=3; " +
            "Host.Execute includes compiler IPC, source execution, value construction and submission; " +
            "dispatch_to_native includes worker IPC/storage plus owner polling; " +
            "observed_reply_to_native starts when State=2 is observed (not the worker's exact completion instant); " +
            "callback_drain includes admission, decoding and callback validation; " +
            "poll_ms=5; no SLA, percentile or isolated API/SQL latency claim");
        F.Execute("require('state')");
        for (int Sample = 1; Sample <= 3; ++Sample) {
            foreach (bool NearLimit in new[] { false, true }) {
                string Key = NearLimit ? "near64k" : "small";
                string Value = NearLimit
                    ? "{string.rep('x',16300),string.rep('y',16300),string.rep('z',16300),string.rep('w',16300)}"
                    : "{Count=7,Enabled=true}";
                string Validate = NearLimit
                    ? "assert(type(V)=='table' and #V==4); for I,Letter in {'x','y','z','w'} do assert(V[I]==string.rep(Letter,16300)) end"
                    : "assert(type(V)=='table' and V.Count==7 and V.Enabled==true)";
                foreach (string Operation in new[] { "Set", "Get", "Remove" }) {
                    // 12 mutations in total; space submissions to respect the
                    // namespace 5/s rate even on a very fast storage stack.
                    Thread.Sleep(220);
                    string Marker = "perf-" + Sample + "-" + Key + "-" + Operation;
                    string Body = Operation == "Get" ? Validate : "assert(V==true)";
                    string Source = Store + "S:" + Operation + "Async('" + Key + "'," +
                        (Operation == "Set" ? Value + "," : "") +
                        "function(V,E) assert(E==nil); " + Body + "; print('" + Marker + "') end)";
                    ObserveOperation(F, Source, Marker, Operation, Key, Sample, NearLimit && Operation == "Set" ? 65269 : 0);
                }
            }
        }
        Check(F.Worker.RequestsSent == 18 && F.Queue.Completed[0] == 6 && F.Queue.Completed[1] == 6 &&
            F.Queue.Completed[2] == 6 && F.Queue.PendingCount == 0 && F.Queue.RateRejected == 0,
            "bounded performance fixture completes exactly six Set/Get/Remove groups");
    }

    private static void ObserveOperation(Fixture F, string Source, string Marker, string Operation, string Payload,
        int Sample, int ExpectedEnvelopeBytes)
    {
        Check(F.Queue.PendingCount == 0, "performance sample starts idle");
        long Before = F.Worker.RequestsSent;
        long SubmissionStart = Stopwatch.GetTimestamp();
        var Submitted = F.Host.Execute("persistence.performance", Source);
        long SubmissionEnd = Stopwatch.GetTimestamp();
        Check(Submitted.Status == Runtime.RuntimeStatus.OK && Submitted.Logs == "", "performance submission: " + Submitted.Error);
        var Request = OnlyRequest(F);
        if (ExpectedEnvelopeBytes != 0) {
            // The worker request frame ends with the snapshotted envelope;
            // read its explicit length rather than assuming buffer capacity.
            int Offset = 64;
            for (int Index = 0; Index < 3; ++Index) {
                uint Size = Runtime.StorageProcess.U32(Request.Frame, Offset);
                Offset = checked(Offset + 4 + (int)Size);
            }
            Check(Runtime.StorageProcess.U32(Request.Frame, Offset) == (uint)ExpectedEnvelopeBytes, "near-limit envelope is 65269 bytes");
        }

        long DispatchStart = Stopwatch.GetTimestamp();
        F.Worker.Tick();
        WaitFor(() => Volatile.Read(ref Request.State) == 2, () => { }, "performance worker reply");
        long ReplyObserved = Stopwatch.GetTimestamp();
        Check(Request.Sent && Request.Error == Runtime.StorageQueue.Error.None &&
            F.Worker.RequestsSent == Before + 1, "performance operation received a real successful worker reply");
        var IntakeWait = Stopwatch.StartNew();
        while (!Request.HandedOff && IntakeWait.ElapsedMilliseconds < 12000) {
            F.Tick(false);
            // Unlike the general fixture waiter, do not sleep after observing
            // success: that would add an artificial 5-ms floor to this phase.
            if (!Request.HandedOff) Thread.Sleep(5);
        }
        long NativeHandoff = Stopwatch.GetTimestamp();
        Check(Request.HandedOff, "performance native completion handoff");
        Check(F.Queue.PendingCount == 1 && !Request.Released, "handoff defers callback and retains its reservation");

        long DrainStart = Stopwatch.GetTimestamp();
        var Results = F.Host.Drain();
        long DrainEnd = Stopwatch.GetTimestamp();
        Check(Results.Count == 1 && Results[0].Status == Runtime.RuntimeStatus.OK &&
            Results[0].Logs == Marker + "\n" && Request.Released && F.Queue.PendingCount == 0,
            "performance callback delivered once on separate admission");
        F.Logs.Append(Results[0].Logs);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "[CarbonLuau:Persistence1BPerf] operation={0}; payload={1}; sample={2}; host_execute_ms={3:F3}; " +
            "dispatch_to_native_handoff_ms={4:F3}; observed_reply_to_native_ms={5:F3}; " +
            "handoff_to_drain_start_ms={6:F3}; deferred_callback_drain_ms={7:F3}",
            Operation, Payload, Sample, ElapsedMilliseconds(SubmissionStart, SubmissionEnd),
            ElapsedMilliseconds(DispatchStart, NativeHandoff), ElapsedMilliseconds(ReplyObserved, NativeHandoff),
            ElapsedMilliseconds(NativeHandoff, DrainStart), ElapsedMilliseconds(DrainStart, DrainEnd)));
    }
}
