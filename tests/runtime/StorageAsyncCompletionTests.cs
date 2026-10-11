using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class StorageAsyncCompletionTests
{
    private sealed class Completion : IAsyncResult, IDisposable
    {
        internal readonly ManualResetEvent Signal = new ManualResetEvent(false);
        internal Action<Completion> Borrowing;
        internal int Reads, HandleReads;
        private int Completed;
        internal void Finish() { Signal.Set(); Volatile.Write(ref Completed, 1); }
        public bool IsCompleted { get { Reads++; return Volatile.Read(ref Completed) != 0; } }
        public bool CompletedSynchronously { get { return false; } }
        public object AsyncState { get { return null; } }
        public WaitHandle AsyncWaitHandle {
            get { HandleReads++; if (Borrowing != null) Borrowing(this); return Signal; }
        }
        public void Dispose() { Signal.Dispose(); }
    }
    private sealed class CompletionTask : Task, IAsyncResult
    {
        internal int HandleReads;
        internal CompletionTask(Action Body) : base(Body) { }
        WaitHandle IAsyncResult.AsyncWaitHandle {
            get { HandleReads++; throw new Exception("Task kernel completion handle must not be requested"); }
        }
        bool IAsyncResult.IsCompleted { get { return IsCompleted; } }
        bool IAsyncResult.CompletedSynchronously { get { return false; } }
        object IAsyncResult.AsyncState { get { return AsyncState; } }
    }
    private static readonly MethodInfo CompletionWait = typeof(Runtime.StorageProcess).GetMethod("WaitCompletion", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo Await = typeof(Runtime.StorageProcess).GetMethod("Await", BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception(Message); }
    private static bool WaitCompletion(IAsyncResult Operation, ulong End, Func<bool> Stopping = null)
    {
        try { return (bool)CompletionWait.Invoke(null, new object[] { Operation, End, Stopping }); }
        catch (TargetInvocationException Error) { throw Error.InnerException; }
    }
    private static void WaitUntil(Runtime.StorageProcess Worker, IAsyncResult Operation, ulong End)
    {
        try { Await.Invoke(Worker, new object[] { Operation, End }); }
        catch (TargetInvocationException Error) { throw Error.InnerException; }
    }
    private static void Timeout(Action Operation)
    {
        try { Operation(); }
        catch (TimeoutException) { return; }
        throw new Exception("expected transport timeout");
    }
    internal static void Run()
    {
        Check(CompletionWait != null && Await != null, "private finite transport wait paths");
        BorrowedApm(); TaskCompletion();
        Console.WriteLine("[CarbonLuau:StorageAsync] PASS finite framework waits, borrowed APM ownership, Task handle avoidance, failure observation, deadlines and stopping");
    }
    private static void BorrowedApm()
    {
        using (var Worker = new Runtime.StorageProcess(() => false))
        using (var Ready = new Completion())
        using (var Delayed = new Completion { Borrowing = Value => Value.Finish() })
        using (var Pending = new Completion()) {
            Ready.Finish(); WaitUntil(Worker, Ready, Runtime.StorageProcess.Now + 5000);
            Check(Ready.Reads == 1 && Ready.HandleReads == 0, "completed APM result requires no wait handle");
            // The fixture signals before publishing IsCompleted; no background
            // callback or sleep-based completion assumption is involved.
            WaitUntil(Worker, Delayed, Runtime.StorageProcess.Now + 5000);
            Check(Delayed.HandleReads == 1 && Delayed.Signal.WaitOne(0) && !Delayed.Signal.SafeWaitHandle.IsClosed,
                "non-Task handle wakes completion and stays owned by its APM result");
            ulong End = Runtime.StorageProcess.Now;
            Timeout(() => WaitUntil(Worker, Ready, End));
            Timeout(() => WaitUntil(Worker, Pending, End));
            Check(Pending.HandleReads == 0, "absolute expiry refuses pending work before obtaining a handle");
            End = Runtime.StorageProcess.Now + 25;
            Check(!WaitCompletion(Pending, End) && Runtime.StorageProcess.Now >= End,
                "unfinished APM operation expires under the original monotonic deadline");
            Check(!Pending.Signal.SafeWaitHandle.IsClosed && !Pending.Signal.WaitOne(0), "timeout never closes the borrowed handle");
            int Previous = Pending.HandleReads;
            Check(!WaitCompletion(Pending, End) && Pending.HandleReads == Previous, "expired deadline cannot restart a wait budget");
            Check(WaitCompletion(Delayed, Runtime.StorageProcess.Now + 1000), "cleanup accepts completed I/O without a stopping predicate");
        }
        using (var Pending = new Completion()) {
            using (var Worker = new Runtime.StorageProcess(() => true))
                Timeout(() => WaitUntil(Worker, Pending, Runtime.StorageProcess.Now + 5000));
            Check(Pending.HandleReads == 0, "stopping refuses pending requests before waiting");
            Check(!WaitCompletion(Pending, Runtime.StorageProcess.Now + 5000, () => Pending.Reads >= 3) &&
                Pending.HandleReads == 1 && !Pending.Signal.SafeWaitHandle.IsClosed,
                "stopping is checked between finite waits without closing the borrowed handle");
        }
    }
    private static void TaskCompletion()
    {
        int StopCalls = 0;
        using (var Ready = new CompletionTask(() => { })) {
            Ready.RunSynchronously(TaskScheduler.Default);
            using (var Worker = new Runtime.StorageProcess(() => { StopCalls++; return true; })) {
                WaitUntil(Worker, Ready, Runtime.StorageProcess.Now + 5000);
                Timeout(() => WaitUntil(Worker, Ready, Runtime.StorageProcess.Now));
            }
            Check(StopCalls == 0 && Ready.HandleReads == 0, "completed Task preserves original stopping/deadline semantics without a kernel handle");
        }
        foreach (bool Fail in new[] { false, true })
        using (var Release = new ManualResetEvent(false)) {
            var Failure = new IOException("fixture I/O failure");
            using (var Work = new CompletionTask(() => { Release.WaitOne(); if (Fail) throw Failure; })) {
                Work.Start(TaskScheduler.Default);
                try {
                    using (var Worker = new Runtime.StorageProcess(() => { Release.Set(); return false; }))
                        WaitUntil(Worker, Work, Runtime.StorageProcess.Now + 5000);
                    Check(Work.IsCompleted && Work.HandleReads == 0, "finite Task wait never forces the exposed kernel handle");
                    try {
                        Work.GetAwaiter().GetResult();
                        Check(!Fail, "fault must remain observable by the terminal result consumer");
                    } catch (IOException Error) {
                        Check(Fail && Object.ReferenceEquals(Error, Failure), "wait suppresses only the completion wrapper, not the original operation failure");
                    }
                } finally {
                    Release.Set();
                    try { Check(Work.Wait(5000), "fixture Task completes before owned test resources are disposed"); }
                    catch (AggregateException) when (Work.IsCompleted) { }
                }
            }
        }
        using (var Release = new ManualResetEvent(false))
        using (var Work = new CompletionTask(() => Release.WaitOne())) {
            Work.Start(TaskScheduler.Default);
            try {
                using (var Worker = new Runtime.StorageProcess(() => false))
                    Timeout(() => WaitUntil(Worker, Work, Runtime.StorageProcess.Now + 25));
                Check(!Work.IsCompleted && Work.HandleReads == 0, "pending Task remains pending on bounded deadline expiry");
                using (var Worker = new Runtime.StorageProcess(() => true))
                    Timeout(() => WaitUntil(Worker, Work, Runtime.StorageProcess.Now + 5000));
                Check(Work.HandleReads == 0, "pending Task stopping never requests a kernel handle");
            } finally {
                Release.Set(); Check(Work.Wait(5000), "pending fixture Task drained before test resource disposal");
            }
        }
    }
}
