using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Runtime=Carbon.Plugins.CarbonLuau;

internal static class Program
{
    private const string Prefix="[CarbonLuau:PersistenceAdapter] ";
    private const string Store="local S=game:GetService('DataStoreService'):GetDataStore('Adapter'); ";
    private const string Stale="ADAPTER_STALE_CALLBACK";
    private static bool Windows { get { return Environment.OSVersion.Platform==PlatformID.Win32NT; } }
    private static void Check(bool Value,string Message) { if (!Value) throw new InvalidOperationException(Prefix+Message); }
    private static object Field(object Owner,string Name)
    { return Owner.GetType().GetField(Name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Owner); }
    private static Runtime.StorageQueue.Request[] Reservations(Runtime.StorageQueue Queue)
    { return (Runtime.StorageQueue.Request[])Field(Queue,"Reservations"); }
    private static void WaitFor(Func<bool> Done,Action Advance,string Message,int Milliseconds=12000)
    {
        var Watch=Stopwatch.StartNew();
        while (!Done() && Watch.ElapsedMilliseconds<Milliseconds) { Advance(); Thread.Sleep(5); }
        Check(Done(),Message);
    }
    private static bool Logged(Runtime Plugin,string Text)
    { foreach (string Line in Plugin.TestLogs) if (Line.Contains(Text)) return true; return false; }
    private static void StopWorker(Runtime.StorageSupervisor Worker)
    {
        if (Worker==null) return;
        Worker.Stop();
        WaitFor(()=>Worker.IsFinished,()=>{},"supervisor finished",12000);
        Check(((Thread)Field(Worker,"Thread")).Join(5000),"supervisor thread exited");
    }
    private static bool Mapped(string Library)
    {
        if (Windows) return GetModuleHandleW(Library)!=IntPtr.Zero;
        string Escaped=Library.Replace("\\","\\134").Replace(" ","\\040").Replace("\t","\\011").Replace("\n","\\012");
        foreach (string Line in File.ReadLines("/proc/self/maps"))
            if (Line.EndsWith(" "+Escaped,StringComparison.Ordinal)) return true;
        return false;
    }
    private static string Stage(string NativeLibrary,string Worker,string Root)
    {
        string Rid=Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform,RuntimeInformation.ProcessArchitecture);
        string Library=Runtime.NativeLibraryLoader.GetLibraryPath(Root,Rid);
        string Target=Path.GetDirectoryName(Library); Directory.CreateDirectory(Target);
        File.Copy(NativeLibrary,Library);
        string Suffix=Windows ? ".exe" : "";
        foreach (string Name in new[] {"carbonluau_compiler","carbonluau_storage"}) {
            string Source=Name=="carbonluau_storage" ? Worker : Path.Combine(Path.GetDirectoryName(NativeLibrary),Name+Suffix);
            string Destination=Path.Combine(Target,Name+Suffix); File.Copy(Source,Destination);
            if (!Windows) Check(chmod(Destination,493)==0,"staged helper executable permissions");
        }
        return Library;
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Runtime Plugin=new Runtime();
        internal Runtime.NativeRuntime Native;
        internal Runtime.ScriptHost Host;
        internal Runtime.StorageQueue Queue;
        internal Runtime.StorageSupervisor Worker;
        internal Runtime.FacadeSession Session;
        private bool Disposed;
        internal Fixture(string Root)
        {
            try {
                Plugin.TestInitialize(Root);
                Native=Plugin.TestNative; Host=Plugin.TestHost; Queue=Native.Storage; Worker=Plugin.TestWorker;
                Check(Worker!=null,"production supervisor initialized");
                Check(Native.AbiVersion==0x00010005,"ABI 1.5");
                WaitFor(()=>Queue.Ready,()=>Worker.Tick(),"production worker ready",35000);
                var Result=Host.Reload(); Check(Result.Status==Runtime.RuntimeStatus.OK,"real VM root activation: "+Result.Error);
                Session=Plugin.TestWorld.Active;
            } catch { Dispose(); throw; }
        }
        internal Runtime.StorageQueue.Request Prepare(bool ScheduleCallbackFault)
        {
            string Callback=ScheduleCallbackFault
                ? "assert(V==nil and E==nil); print('ADAPTER_FIRST_CALLBACK'); S:GetAsync('later',function() print('"+Stale+"') end)"
                : "print('"+Stale+"')";
            var Result=Host.Execute("adapter.fixture",Store+"S:GetAsync('first',function(V,E) "+Callback+" end)");
            Check(Result.Status==Runtime.RuntimeStatus.OK,"public Get acceptance: "+Result.Error);
            Check(Queue.PendingCount==1,"one accepted public request");
            Runtime.StorageQueue.Request Request=null;
            foreach (var Slot in Reservations(Queue)) if (Slot!=null) Request=Slot;
            Check(Request!=null,"accepted route recorded");
            WaitFor(()=>Request.HandedOff,()=>{ Worker.Tick(); Native.PumpStorage(); },"real worker completion reaches reserved native slot");
            Check(Worker.RequestsSent==1 && Queue.Completed[0]==1 && Request.Error==Runtime.StorageQueue.Error.None,
                "real production worker completed missing Get");
            Check(Queue.PendingCount==1 && Request.Frame==null && Request.Envelope==null && !Request.Released,
                "native-ready callback retains quota but releases managed transport");
            Check(!Logged(Plugin,Stale),"no callback before fault");
            return Request;
        }
        public void Dispose()
        {
            if (Disposed) return; Disposed=true;
            // Save a partially constructed supervisor too. Cleanup is owner-thread
            // production unload plus bounded test-only waiting for detached I/O.
            if (Worker==null) Worker=Plugin.TestWorker;
            try { Plugin.TestUnload(); }
            finally {
                StopWorker(Worker);
                if (Queue!=null) {
                    Queue.Dispatch();
                    Runtime.StorageQueue.Request Completion;
                    while ((Completion=Queue.TakeCompletion())!=null) Queue.Release(Completion);
                }
            }
        }
    }
    private static void VerifyCleanup(Fixture F,Runtime.StorageQueue.Request First,
        Runtime.StorageQueue.Request Later,string Library,string Diagnostic)
    {
        Check(F.Plugin.TestStopped && F.Plugin.TestReleased,"production catch fully released plugin fields");
        Check(F.Host.Recoveries==0,"unexpected adapter failure uses teardown, not automatic recovery");
        Check(F.Native.LiveVmCount==0 && F.Native.DestroyedVmCount==1,"zero VM resources after production teardown");
        Check(F.Session.Disposed && !F.Session.Active && !F.Session.StorageBinding.Alive,"old facade and namespace authority retired");
        Check(First.Released && First.Frame==null && First.Envelope==null,"first route and transport released");
        if (Later!=null) Check(Later.Released && Later.Frame==null && Later.Envelope==null,"undispatched route released");
        Check(F.Queue.PendingCount==0 && !F.Queue.Ready,"zero managed reservations and closed intake");
        foreach (var Slot in Reservations(F.Queue)) Check(Slot==null,"fixed reservation ledger empty");
        Check(((IDictionary)Field(F.Native,"DomainFacadeRoots")).Count==0 &&
            ((IDictionary)Field(F.Native,"FacadeRoots")).Count==0,"no managed native-callback roots");
        Check(!((Runtime.NativeLibraryLoader)Field(F.Native,"Loader")).Available && F.Native.UnloadError==null,
            "native loader released without error");
        Check(!Mapped(Library),"owned native library unmapped");
        Check(Logged(F.Plugin,Diagnostic) && Logged(F.Plugin,"reload") && !Logged(F.Plugin,Stale),
            "controlled expected diagnostic without stale callback execution");
        Check(!Logged(F.Plugin,"fixture NextFrame failure") && !Logged(F.Plugin,"fixture permission failure"),"exception details not leaked");
        StopWorker(F.Worker);
        Check(F.Worker.RequestsSent==1,"no replay or undispatched write after teardown");
        F.Plugin.TestTick(); F.Plugin.TestUnload();
        Check(F.Plugin.TestReleased && F.Native.DestroyedVmCount==1,"late tick and repeated unload are inert");
    }
    private static void VerifyOwnershipReleased(string Root,string Library)
    {
        // Reacquire the exact database with a new real supervisor. This observes
        // release of process ownership and the worker's authoritative file lock.
        var Queue=new Runtime.StorageQueue(9001,()=>Runtime.StorageProcess.Now);
        string Worker=Path.Combine(Path.GetDirectoryName(Library),Windows ? "carbonluau_storage.exe" : "carbonluau_storage");
        var Supervisor=new Runtime.StorageSupervisor(Queue,Worker,Path.Combine(Root,"CarbonLuau","persistence"));
        Supervisor.Start();
        try { WaitFor(()=>Queue.Ready,()=>Supervisor.Tick(),"same directory ownership reacquired",35000); }
        finally { Queue.RetireAll(); StopWorker(Supervisor); }
        Check(Queue.PendingCount==0,"reacquisition leaves zero reservations");
    }
    private static void RunCase(string NativeLibrary,string Worker,string Parent,bool CallbackFault)
    {
        string Root=Path.GetFullPath(Path.Combine(Parent,"PersistenceAdapter-"+Guid.NewGuid().ToString("N")));
        Check(!Directory.Exists(Root),"fresh fixture directory"); Directory.CreateDirectory(Root);
        bool Passed=false;
        try {
            string Library=Stage(NativeLibrary,Worker,Root);
            using (var F=new Fixture(Root)) {
                Check(Mapped(Library),"owned native library mapped before test");
                Runtime.StorageQueue.Request First=F.Prepare(CallbackFault), Later=null;
                if (!CallbackFault) {
                    F.Plugin.ThrowNextFrame=true;
                    F.Plugin.TestTick(); // Actual OnTick -> RequestDrain -> failing host stub.
                    Check(F.Plugin.SchedulingFaults==1 && F.Plugin.PendingFrame==null,"NextFrame admission fault actually injected");
                    VerifyCleanup(F,First,null,Library,"[CarbonLuau:Persistence] Scripting stopped");
                } else {
                    int Faults=0;
                    F.Plugin.PermissionAction=()=>{
                        Check(Logged(F.Plugin,"ADAPTER_FIRST_CALLBACK"),"first completion executed before callback-host failure");
                        Check(F.Queue.PendingCount==1,"completion submitted another accepted request");
                        foreach (var Slot in Reservations(F.Queue)) if (Slot!=null) Later=Slot;
                        Check(Later!=null && Later.State==0 && !Later.HandedOff && !Later.Released,"later callback reserved before dispatch");
                        ++Faults;
                        throw new InvalidOperationException("fixture permission failure");
                    };
                    F.Plugin.TestTick();
                    Check(F.Plugin.PendingFrame!=null && !F.Plugin.TestStopped,"actual NextFrame callback scheduled");
                    F.Plugin.RunFrame(); // Actual RequestDrain closure and its catch.
                    Check(Faults==1 && F.Plugin.SchedulingFaults==0,"scheduled callback-host fault actually injected");
                    VerifyCleanup(F,First,Later,Library,"[CarbonLuau:Scheduler] Drain stopped");
                }
            }
            VerifyOwnershipReleased(Root,Library);
            Passed=true;
            Console.WriteLine(Prefix+"PASS "+(CallbackFault ? "scheduled callback catch" : "OnTick scheduling catch")+
                "; real VM/worker; zero reservations, VM/callback roots, supervisor thread and native mapping; ownership reacquired");
        } finally {
            // Only this exact, newly created GUID child is eligible for removal.
            if (Passed) Directory.Delete(Root,true);
            else Console.Error.WriteLine(Prefix+"preserved failed fixture: "+Root);
        }
    }
    private static int Main(string[] Args)
    {
        if (Windows) SetErrorMode(0x0001|0x0002|0x8000);
        try {
            Check(Args.Length==3,"usage: PersistenceAdapterTests <native-library> <storage-worker> <fixture-parent> (absolute paths)");
            foreach (string Argument in Args) Check(Path.IsPathRooted(Argument),"CLI paths must be absolute");
            string Library=Path.GetFullPath(Args[0]), Worker=Path.GetFullPath(Args[1]), Parent=Path.GetFullPath(Args[2]);
            Check(File.Exists(Library) && File.Exists(Worker),"native library and storage worker required");
            Directory.CreateDirectory(Parent);
            RunCase(Library,Worker,Parent,false); RunCase(Library,Worker,Parent,true);
            Console.WriteLine(Prefix+"ALL PASS: Persistence-1B adapter teardown gate; host stubs, not actual Carbon scheduling or 1C qualification");
            return 0;
        } catch (Exception Error) { Console.Error.WriteLine(Prefix+"FAIL "+Error); return 1; }
    }
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint Mode);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern IntPtr GetModuleHandleW(string Name);
    [DllImport("libc",SetLastError=true)] private static extern int chmod(string Path,uint Mode);
}
