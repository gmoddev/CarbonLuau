using System;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class StorageSupervisor
        {
            private readonly StorageQueue Queue;
            private readonly string Executable, Directory;
            private readonly AutoResetEvent Wake=new AutoResetEvent(false);
            private readonly Thread Thread;
            private StorageQueue.Request Slot;
            private int Stopping, Ready, Finished, Closed;
            private int Failure, Starts, Retries, Corruptions;
            private long Sent;
            internal bool IsFinished { get { return Volatile.Read(ref Finished)!=0; } }
            internal int WorkerStarts { get { return Volatile.Read(ref Starts); } }
            internal long RequestsSent { get { return Volatile.Read(ref Sent); } }
            internal StorageQueue.Error LastFailure { get { return (StorageQueue.Error)Volatile.Read(ref Failure); } }
            internal string Status { get { return "[CarbonLuau:Persistence] ready="+(Volatile.Read(ref Ready)!=0)+
                "; pending="+Queue.PendingCount+"; starts="+WorkerStarts+"; retries="+Volatile.Read(ref Retries)+
                "; sent="+RequestsSent+"; completed_get="+Queue.Completed[0]+"; completed_set="+Queue.Completed[1]+
                "; completed_remove="+Queue.Completed[2]+"; queue_rejected="+Queue.QueueRejected+"; rate_rejected="+Queue.RateRejected+
                "; quota_rejected="+Queue.QuotaRejected+"; expired="+Queue.Expired+"; backend_failures="+Queue.BackendFailures+
                "; corruptions="+Volatile.Read(ref Corruptions)+"; discarded="+Queue.Discarded+"; failure="+LastFailure; } }
            internal StorageSupervisor(StorageQueue Queue,string Executable,string Directory)
            {
                this.Queue=Queue; this.Executable=Executable; this.Directory=Directory;
                Thread=new Thread(Run) { IsBackground=true, Name="CarbonLuau.Persistence" };
            }
            internal void Start()
            {
                try { Thread.Start(); }
                catch { Volatile.Write(ref Closed,1); Wake.Dispose(); Volatile.Write(ref Finished,1); throw; }
            }
            internal void Tick()
            {
                Queue.BeginTick();
                Queue.Ready=Volatile.Read(ref Ready)!=0 && Volatile.Read(ref Stopping)==0;
                if (Volatile.Read(ref Slot)!=null) return;
                var Request=Queue.Dispatch();
                if (Request!=null) {
                    Volatile.Write(ref Slot,Request);
                    // Close-before-drain pairs with the finalizer: either it
                    // owns this request, or this owner turn returns its failure.
                    if (Volatile.Read(ref Closed)!=0 && Interlocked.CompareExchange(ref Slot,null,Request)==Request)
                        StorageQueue.Fail(Request);
                    Signal();
                }
            }
            internal void Stop()
            {
                Queue.Ready=false; Interlocked.Exchange(ref Stopping,1);
                Signal(); // no Join, native entry, process wait or disk I/O here
            }
            private void Signal() { try { Wake.Set(); } catch (ObjectDisposedException) { } }
            private void Run()
            {
                StorageOwnership Ownership=null;
                var Worker=new StorageProcess(() => Volatile.Read(ref Stopping)!=0);
                try {
                    Ownership=new StorageOwnership(Directory);
                    int Restarts=0;
                    while (Volatile.Read(ref Stopping)==0) {
                        try { Interlocked.Increment(ref Starts); Worker.Start(Executable,Directory); }
                        catch (Exception Error) {
                            if (!Worker.Stop()) { RetainUntilDead(Worker); break; }
                            var Rejected=Error as StorageProcess.StartupFailure;
                            Volatile.Write(ref Failure,Rejected==null ? (int)StorageQueue.Error.StorageUnavailable : (int)Rejected.Code);
                            if (Rejected!=null && (Rejected.Code==6 || Rejected.Code==7)) {
                                if (Rejected.Code==6) Interlocked.Increment(ref Corruptions);
                                break;
                            }
                            if (Restarts++==0 && Volatile.Read(ref Stopping)==0) { Volatile.Write(ref Retries,1); continue; }
                            break;
                        }
                        Volatile.Write(ref Ready,1);
                        bool Fault=false, Terminal=false;
                        while (Volatile.Read(ref Stopping)==0) {
                            var Request=Volatile.Read(ref Slot);
                            if (Request==null) { Wake.WaitOne(50); continue; }
                            try {
                                if (StorageProcess.Now>=Request.End) { Request.Error=StorageQueue.Error.DeadlineExceeded; Volatile.Write(ref Request.State,2); }
                                else {
                                    if (Sent!=long.MaxValue) Interlocked.Increment(ref Sent);
                                    Request.Sent=true;
                                    StorageQueue.Complete(Request,Worker.Exchange(Request.Frame,Request.End),StorageProcess.Now);
                                }
                                Terminal=Request.Error==StorageQueue.Error.StorageCorrupt || Request.Error==StorageQueue.Error.FormatUnsupported ||
                                    Request.Error==StorageQueue.Error.StorageUnavailable;
                                if (Request.Error==StorageQueue.Error.StorageCorrupt) Interlocked.Increment(ref Corruptions);
                                Fault=Request.Error==StorageQueue.Error.Indeterminate || Request.Error==StorageQueue.Error.StorageError;
                            } catch (Exception) { StorageQueue.Fail(Request); Fault=true; }
                            if (Request.Error!=StorageQueue.Error.None) Volatile.Write(ref Failure,(int)Request.Error);
                            Volatile.Write(ref Slot,null);
                            if (Fault || Terminal) break;
                        }
                        Volatile.Write(ref Ready,0);
                        if (!Worker.Stop()) { RetainUntilDead(Worker); break; }
                        if (Terminal || !Fault || Restarts++!=0) break;
                        Volatile.Write(ref Retries,1);
                    }
                } catch (Exception) { Volatile.Write(ref Failure,(int)StorageQueue.Error.StorageUnavailable); }
                finally {
                    Volatile.Write(ref Ready,0);
                    if (!Worker.Stop()) RetainUntilDead(Worker);
                    Volatile.Write(ref Closed,1);
                    var Request=Interlocked.Exchange(ref Slot,null);
                    if (Request!=null) StorageQueue.Fail(Request);
                    if (Ownership!=null) Ownership.Dispose();
                    Wake.Dispose();
                    Volatile.Write(ref Finished,1);
                }
            }
            private static void RetainUntilDead(StorageProcess Worker)
            {
                // Never release directory ownership or spawn replacements while an
                // OS-stuck writer is unconfirmed. Only this detached managed thread
                // remains; no native library/VM reference is reachable from it.
                while (!Worker.Stop()) System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
