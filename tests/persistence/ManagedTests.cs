using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using Host=Carbon.Plugins.CarbonLuau;
using Queue=Carbon.Plugins.CarbonLuau.StorageQueue;
class ManagedTests
{
    static void Check(bool Good, [System.Runtime.CompilerServices.CallerLineNumber] int Line=0)
    { if (!Good) throw new InvalidOperationException("persistence assertion at source line "+Line); }
    static void Rejected(Action Action) { try { Action(); } catch (InvalidOperationException) { return; } throw new Exception("expected rejection"); }
    static void Rejected(Action Action,uint Status)
    { try { Action(); } catch (Queue.Rejection Error) { Check(Error.Status==Status); return; } throw new Exception("expected storage rejection"); }
    static void Invalid(Action Action) { try { Action(); } catch (ArgumentException) { return; } catch (IOException) { return; } throw new Exception("expected invalid data rejection"); }
    static void Finish(Queue Queue,ulong Now)
    {
        Queue.BeginTick(); var Request=Queue.Dispatch(); Check(Request!=null);
        Host.StorageQueue.Complete(Request,Reply(Request),Now); Check(Queue.Dispatch()==null);
        Queue.Release(Queue.TakeCompletion());
    }
    static System.Collections.IDictionary NamespaceBuckets(Queue Queue)
    { return (System.Collections.IDictionary)typeof(Queue).GetField("Namespaces",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Queue); }
    static void FinishResult(Queue Queue,ulong Now,Host.StorageQueue.Error Error,uint Flags=0,bool TransportFailure=false,bool Sent=true)
    {
        var Request=Queue.Dispatch(); Check(Request!=null); Request.Sent=Sent;
        if (TransportFailure) Host.StorageQueue.Fail(Request);
        else {
            byte[] Result=Reply(Request); Host.StorageProcess.Put32(Result,8,(uint)Error); Host.StorageProcess.Put32(Result,52,Flags);
            Host.StorageQueue.Complete(Request,Result,Now);
        }
        Check(Queue.Dispatch()==null);
        var Done=Queue.TakeCompletion(); Check(Done==Request && Done.Error==Error);
        Queue.Release(Done); Check(Queue.PendingCount==0);
    }
    static void NamespaceRetentionTests()
    {
        ulong Now=1; var Queue=new Queue(1,()=>Now) { Ready=true };
        // More distinct failed namespaces than the entire 514-bucket ceiling.
        // These worker replies prove no mutation committed; the clock refills
        // both token buckets before the next inactive namespace is reclaimed.
        for (int Index=0; Index<1024; ++Index) {
            Now+=2000; var Owner=Queue.Bind(1,(ulong)Index+1,"churn"+Index);
            var Op=Index%3==0 ? Host.StorageQueue.Operation.Get : Index%3==1 ? Host.StorageQueue.Operation.Set : Host.StorageQueue.Operation.Remove;
            var Error=Op==Host.StorageQueue.Operation.Set ? Host.StorageQueue.Error.QuotaExceeded : Host.StorageQueue.Error.StorageBusy;
            Queue.Submit(Owner,1,Owner.Domain,true,Op,"Store","Key",Op==Host.StorageQueue.Operation.Set ? Envelope() : new byte[0],1);
            FinishResult(Queue,Now,Error); Queue.Retire(Owner);
        }
        Now+=2000; Queue.Bind(1,2000,"sentinel"); Check(NamespaceBuckets(Queue).Count==1);

        // A definite failure must not erase a successful Set's presence baseline.
        foreach (var Error in new[] {Host.StorageQueue.Error.InvalidArgument,Host.StorageQueue.Error.QuotaExceeded,
            Host.StorageQueue.Error.StorageUnavailable,Host.StorageQueue.Error.StorageBusy,Host.StorageQueue.Error.StorageFull,
            Host.StorageQueue.Error.StorageCorrupt,Host.StorageQueue.Error.FormatUnsupported,Host.StorageQueue.Error.DeadlineExceeded,
            Host.StorageQueue.Error.StorageError}) {
            Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(1,1,"known");
            Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
            FinishResult(Queue,Now,Host.StorageQueue.Error.None,3);
            object Baseline=NamespaceBuckets(Queue)[Owner.Namespace];
            Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),2);
            FinishResult(Queue,Now,Error); Queue.Retire(Owner); Now+=2000;
            Queue.Bind(2,2,"cleanup"); Check(Object.ReferenceEquals(Baseline,NamespaceBuckets(Queue)[Owner.Namespace]));
        }

        // Both worker-reported uncertainty and a lost reply after dispatch retain
        // the namespace. A later definite failure must preserve that baseline too.
        foreach (bool LostReply in new[] {false,true}) {
            Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(1,1,"uncertain");
            Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
            FinishResult(Queue,Now,Host.StorageQueue.Error.Indeterminate,0,LostReply);
            object Baseline=NamespaceBuckets(Queue)[Owner.Namespace];
            Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),2);
            FinishResult(Queue,Now,Host.StorageQueue.Error.StorageBusy); Queue.Retire(Owner); Now+=2000;
            Queue.Bind(2,2,"cleanup"); Check(Object.ReferenceEquals(Baseline,NamespaceBuckets(Queue)[Owner.Namespace]));
            Owner=Queue.Bind(3,3,"uncertain");
            Queue.Submit(Owner,3,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],3);
            FinishResult(Queue,Now,Host.StorageQueue.Error.None); Queue.Retire(Owner); Now+=2000;
            Queue.Bind(4,4,"cleanup2"); Check(!NamespaceBuckets(Queue).Contains(Owner.Namespace));
        }

        // Failed reads and unsent mutations cannot create uncertain durable data.
        foreach (bool Read in new[] {false,true}) {
            Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(1,1,"unsent");
            Queue.Submit(Owner,1,1,true,Read ? Host.StorageQueue.Operation.Get : Host.StorageQueue.Operation.Set,
                "Store","Key",Read ? new byte[0] : Envelope(),1);
            FinishResult(Queue,Now,Host.StorageQueue.Error.StorageUnavailable,0,true,Read);
            Queue.Retire(Owner); Now+=2000; Queue.Bind(2,2,"cleanup");
            Check(!NamespaceBuckets(Queue).Contains(Owner.Namespace));
        }

        // Failure does not refund requests/mutations or reset rates on replacement.
        // Even an empty retired namespace remains until BOTH buckets fully refill.
        foreach (bool Mutation in new[] {false,true}) {
            Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(1,1,"rates");
            var Op=Mutation ? Host.StorageQueue.Operation.Remove : Host.StorageQueue.Operation.Get;
            int Burst=Mutation ? 8 : 32;
            for (int Index=0; Index<Burst; ++Index) {
                Queue.Submit(Owner,1,1,true,Op,"Store","Key",new byte[0],(ulong)Index+1);
                FinishResult(Queue,Now,Host.StorageQueue.Error.StorageBusy);
            }
            object Baseline=NamespaceBuckets(Queue)[Owner.Namespace];
            Queue.Retire(Owner); Owner=Queue.Bind(2,2,"rates");
            Check(Object.ReferenceEquals(Baseline,NamespaceBuckets(Queue)[Owner.Namespace]));
            Rejected(()=>Queue.Submit(Owner,2,2,true,Op,"Store","Key",new byte[0],1));
            Queue.Retire(Owner); Now+=1599; var Cleanup=Queue.Bind(3,3,"cleanup");
            Check(Object.ReferenceEquals(Baseline,NamespaceBuckets(Queue)[Owner.Namespace]));
            Queue.Retire(Cleanup); ++Now; Owner=Queue.Bind(4,4,"rates");
            Check(!Object.ReferenceEquals(Baseline,NamespaceBuckets(Queue)[Owner.Namespace]));
            for (int Index=0; Index<Burst; ++Index) {
                Queue.Submit(Owner,4,4,true,Op,"Store","Key",new byte[0],(ulong)Index+1);
                FinishResult(Queue,Now,Host.StorageQueue.Error.StorageBusy);
            }
            Rejected(()=>Queue.Submit(Owner,4,4,true,Op,"Store","Key",new byte[0],(ulong)Burst+1));
        }
        Console.WriteLine("[CarbonLuau:Persistence] Failed-namespace churn, known/uncertain data retention, unsent/read failures and failure-rate refill PASS");
    }
    static void BoundsTests()
    {
        ulong Now=1000; var Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(1,1,null);
        foreach (string Name in new[] {"", ".", "..", "a/b", "a\\b", "a:b", "\0", "\u007f", "\ud800", new string('a',65),new string('\u00e9',33)})
            Invalid(()=>Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Get,Name,"K",new byte[0],1));
        Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Get,new string('\u00e9',32),new string('\u00e9',64),new byte[0],1); Finish(Queue,Now);
        Queue=new Queue(1,()=>Now) { Ready=true }; Owner=Queue.Bind(1,1,null);
        for (int Index=0; Index<8; ++Index) { Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Remove,"S","K",new byte[0],(ulong)Index+1); Finish(Queue,Now); }
        Rejected(()=>Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Remove,"S","K",new byte[0],9));
        Queue.Retire(Owner); Owner=Queue.Bind(2,2,null);
        Rejected(()=>Queue.Submit(Owner,2,2,true,Host.StorageQueue.Operation.Remove,"S","K",new byte[0],1));
        Now+=199; Rejected(()=>Queue.Submit(Owner,2,2,true,Host.StorageQueue.Operation.Remove,"S","K",new byte[0],1));
        ++Now; Queue.Submit(Owner,2,2,true,Host.StorageQueue.Operation.Remove,"S","K",new byte[0],1); Finish(Queue,Now);
        foreach (bool Mutations in new[] {false,true}) {
            Queue=new Queue(1,()=>Now) { Ready=true }; int Limit=Mutations ? 64 : 256;
            for (int Index=0; Index<Limit; ++Index) {
                Owner=Queue.Bind(1,(ulong)Index+1,"addon"+Index);
                Queue.Submit(Owner,1,(ulong)Index+1,true,Mutations ? Host.StorageQueue.Operation.Remove : Host.StorageQueue.Operation.Get,"S","K",new byte[0],1); Finish(Queue,Now); Queue.Retire(Owner);
            }
            Owner=Queue.Bind(1,999,"next");
            Rejected(()=>Queue.Submit(Owner,1,999,true,Mutations ? Host.StorageQueue.Operation.Remove : Host.StorageQueue.Operation.Get,"S","K",new byte[0],1));
            Now+=(ulong)(Mutations ? 20 : 5);
            Queue.Submit(Owner,1,999,true,Mutations ? Host.StorageQueue.Operation.Remove : Host.StorageQueue.Operation.Get,"S","K",new byte[0],1); Finish(Queue,Now);
        }
        // Retained completions continue charging capacity; eight is a tick-wide intake cap.
        Queue=new Queue(1,()=>Now) { Ready=true };
        for (int Index=0; Index<16; ++Index) {
            Owner=Queue.Bind(1,(ulong)Index+1,"addon"+Index);
            Queue.Submit(Owner,1,(ulong)Index+1,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],1);
            var Request=Queue.Dispatch(); Host.StorageQueue.Complete(Request,Reply(Request),Now); Queue.Dispatch();
        }
        Check(Queue.PendingCount==16); Queue.BeginTick();
        for (int Index=0; Index<8; ++Index) { var Done=Queue.TakeCompletion(); Check(Done!=null); Queue.Release(Done); }
        Check(Queue.TakeCompletion()==null && Queue.PendingCount==8); Queue.BeginTick();
        for (int Index=0; Index<8; ++Index) Queue.Release(Queue.TakeCompletion());
        Check(Queue.PendingCount==0);
        Exception WrongThread=null; var Thread=new Thread(()=> { try { Queue.Bind(1,999,null); } catch(Exception Error) { WrongThread=Error; } }); Thread.Start(); Thread.Join(); Check(WrongThread is InvalidOperationException);
        Console.WriteLine("[CarbonLuau:Persistence] Names, UTF-8, mutation/global rates, retained completion bounds, owner-thread rejection PASS");
    }
    static void ProtocolTests()
    {
        var Parse=(Action<byte[]>)Delegate.CreateDelegate(typeof(Action<byte[]>),typeof(Host.StorageProcess).GetMethod("ValidateReady",BindingFlags.Static|BindingFlags.NonPublic));
        byte[] Ready=new byte[12]; Buffer.BlockCopy(Encoding.ASCII.GetBytes("CLPR"),0,Ready,0,4); Host.StorageProcess.Put32(Ready,4,1);
        for(uint Code=0; Code<=10; ++Code) {
            Host.StorageProcess.Put32(Ready,8,Code);
            try { Parse(Ready); Check(Code==0); }
            catch(Host.StorageProcess.StartupFailure Failure) { Check(Code!=0 && Failure.Code==Code); }
        }
        foreach(uint Code in new[] {11u,0x80000000u,uint.MaxValue}) {
            Host.StorageProcess.Put32(Ready,8,Code);
            try { Parse(Ready); throw new Exception("unknown startup code accepted"); }
            catch(IOException Error) { Check(Error.GetType()==typeof(IOException)); }
        }
        Host.StorageProcess.Put32(Ready,8,0);
        foreach(int Length in new[] {0,1,8,11,13}) Invalid(()=>Parse(new byte[Length]));
        foreach(int Offset in new[] {0,4}) { byte[] InvalidReady=(byte[])Ready.Clone(); InvalidReady[Offset]^=0x20; Invalid(()=>Parse(InvalidReady)); }
        Console.WriteLine("[CarbonLuau:Persistence] Private CLPR parser: known codes 0..10, unknown-code fail-closed, magic/version/length rejection PASS");
        ulong Now=1; var Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(2,3,null);
        Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],4); var Request=Queue.Dispatch();
        foreach (int Offset in new[] {0,4,12,20,28,36,44}) { byte[] Bytes=Reply(Request); Bytes[Offset]^=0x20; Invalid(()=>Host.StorageQueue.Complete(Request,Bytes,Now)); Check(Request.State==1); }
        foreach (int Length in new[] {0,1,59,61,69632}) Invalid(()=>Host.StorageQueue.Complete(Request,new byte[Length],Now));
        foreach (int Offset in new[] {8,52,56}) { byte[] Bytes=Reply(Request); Host.StorageProcess.Put32(Bytes,Offset,uint.MaxValue); Invalid(()=>Host.StorageQueue.Complete(Request,Bytes,Now)); }
        try { Host.StorageQueue.Complete(Request,Reply(Request),Request.End); throw new Exception("late reply accepted"); } catch(TimeoutException) { }
        Host.StorageQueue.Complete(Request,Reply(Request),Now);
        Invalid(()=>Host.StorageQueue.Complete(Request,Reply(Request),Now)); Queue.Dispatch(); Queue.Release(Queue.TakeCompletion());
        Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),5); Request=Queue.Dispatch();
        Invalid(()=>Host.StorageQueue.Complete(Request,Reply(Request),Now));
        byte[] Valid=Reply(Request); Host.StorageProcess.Put32(Valid,52,3); Host.StorageQueue.Complete(Request,Valid,Now);
        Queue.Dispatch(); Queue.Release(Queue.TakeCompletion());
        for (int Cycle=0; Cycle<1000; ++Cycle) {
            Now+=200; Owner=Queue.Bind((ulong)Cycle+10,10,null); Queue.Submit(Owner,Owner.Vm,10,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],1);
            var Active=Queue.Dispatch(); Queue.RetireVm(Owner.Vm); Check(!Owner.Alive);
            Host.StorageQueue.Complete(Active,Reply(Active),Now); Queue.Dispatch(); Check(Queue.TakeCompletion()==null && Queue.PendingCount==0);
        }
        Console.WriteLine("[CarbonLuau:Persistence] Protocol identity/shape/late rejection; 1000 VM-retirement stale-completion cycles PASS");
    }
    static Process Child(string Arguments)
    {
        string Assembly=typeof(ManagedTests).Assembly.Location;
        bool Mono=Type.GetType("Mono.Runtime")!=null;
        return Process.Start(new ProcessStartInfo(Mono ? "/usr/bin/mono" : Assembly,Mono ? "\""+Assembly+"\" "+Arguments : Arguments) {
            UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true });
    }
    static void OwnershipTests()
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"owner-"+Guid.NewGuid().ToString("N"));
        using (var Guard=new Host.StorageOwnership(Directory)) {
            using(var Other=Child("--ownership \""+Directory+"\"")) { Check(Other.WaitForExit(5000)); Check(Other.ExitCode==23); }
            Check(!System.IO.Directory.Exists(Directory));
        }
        using(var Other=Child("--ownership \""+Directory+"\"")) { Check(Other.WaitForExit(5000)); Check(Other.ExitCode==0); }
        Console.WriteLine("[CarbonLuau:Persistence] Cross-process duplicate guard, release/reacquire and no directory I/O PASS");
    }
    static byte[] Reply(Queue.Request Request)
    {
        using (var Stream=new MemoryStream()) using (var Writer=new BinaryWriter(Stream)) {
            Writer.Write(Encoding.ASCII.GetBytes("CLPS")); Writer.Write(1u); Writer.Write(0u); Writer.Write(Request.WireId);
            Writer.Write(Request.Owner.Host); Writer.Write(Request.Owner.Vm); Writer.Write(Request.Owner.Domain); Writer.Write(Request.Route);
            Writer.Write(0u); Writer.Write(0u); return Stream.ToArray();
        }
    }
    static void QueueTests()
    {
        ulong Now=1;
        var Queue=new Queue(1,()=>Now) { Ready=true }; var Owner=Queue.Bind(2,3,null);
        Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],1);
        // Test duplicate authority before capacity is full; it must reserve nothing.
        Rejected(()=>Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],1),5);
        Check(Queue.PendingCount==1 && Queue.QueueRejected==0 && Queue.RateRejected==0);
        for (int Index=1; Index<8; ++Index) Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],(ulong)Index+1);
        Rejected(()=>Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],9),3);
        Check(Queue.PendingCount==8); Queue.Retire(Owner); Check(Queue.Dispatch()==null && Queue.PendingCount==0);
        Owner=Queue.Bind(2,4,null);
        Rejected(()=>Queue.Submit(Owner,2,4,false,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],1));
        Rejected(()=>Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],1));
        Queue.Submit(Owner,2,4,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],1);
        Now+=5000; Check(Queue.Dispatch()==null); var Expired=Queue.TakeCompletion(); Check(Expired.Error==Host.StorageQueue.Error.DeadlineExceeded); Queue.Release(Expired);
        Queue=new Queue(1,()=>Now) { Ready=true };
        for (uint Namespace=0; Namespace<16; ++Namespace) {
            Owner=Queue.Bind(2,Namespace+1,"addon"+Namespace);
            for (int Index=0; Index<8; ++Index) Queue.Submit(Owner,2,Namespace+1,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],(ulong)Index+1);
        }
        Owner=Queue.Bind(2,99,"extra");
        Rejected(()=>Queue.Submit(Owner,2,99,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],1),3);
        for (uint Index=0; Index<128; ++Index) {
            var Request=Queue.Dispatch(); Check(Request!=null && Request.Owner.Package=="addon"+(Index%16));
            Host.StorageQueue.Complete(Request,Reply(Request),Now);
            // Dispatch observes completion on the next owner turn. Do not release
            // in-flight slots until that turn has moved them to the completion queue.
            if (Index>0) { var Done=Queue.TakeCompletion(); Check(Done!=null); Queue.Release(Done); }
        }
        Check(Queue.Dispatch()==null); var Last=Queue.TakeCompletion(); Check(Last!=null); Queue.Release(Last); Check(Queue.PendingCount==0);
        Queue=new Queue(1,()=>Now) { Ready=true }; Owner=Queue.Bind(2,1,null);
        for (int Index=0; Index<32; ++Index) {
            Queue.Submit(Owner,2,1,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],(ulong)Index+1);
            var Request=Queue.Dispatch(); Host.StorageQueue.Complete(Request,Reply(Request),Now); Queue.Dispatch(); Queue.Release(Queue.TakeCompletion());
        }
        Rejected(()=>Queue.Submit(Owner,2,1,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],33),4);
        Queue.Retire(Owner); Owner=Queue.Bind(2,2,null);
        Rejected(()=>Queue.Submit(Owner,2,2,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],1));
        Now+=1600;
        Queue.Submit(Owner,2,2,true,Host.StorageQueue.Operation.Get,"S","K",new byte[0],1);
        var Active=Queue.Dispatch(); Queue.Retire(Owner); Host.StorageQueue.Complete(Active,Reply(Active),Now); Queue.Dispatch();
        Check(Queue.TakeCompletion()==null && Queue.PendingCount==0);
        Console.WriteLine("[CarbonLuau:Persistence] Queue/name/lifetime model PASS (not native publication proof)");
    }
    static byte[] Envelope()
    {
        byte[] Value;
        using (var Stream=new MemoryStream()) using (var Writer=new BinaryWriter(Stream)) {
            Writer.Write(Encoding.ASCII.GetBytes("CLPV")); Writer.Write(1u); Writer.Write(2u); Writer.Write((byte)1); Writer.Write((byte)0); Value=Stream.ToArray();
        }
        byte[] Digest;
        using (var Stream=new MemoryStream()) using (var Writer=new BinaryWriter(Stream)) {
            Writer.Write((byte)0);
            foreach (string Name in new[] {"","Store","Key"}) { byte[] Text=Encoding.UTF8.GetBytes(Name); Writer.Write((uint)Text.Length); Writer.Write(Text); }
            Writer.Write(Value); using (var Hash=SHA256.Create()) Digest=Hash.ComputeHash(Stream.ToArray());
        }
        byte[] Result=new byte[Value.Length+Digest.Length]; Buffer.BlockCopy(Value,0,Result,0,Value.Length); Buffer.BlockCopy(Digest,0,Result,Value.Length,Digest.Length); return Result;
    }
    static Queue.Request Wait(Host.StorageSupervisor Supervisor,Queue Queue)
    {
        ulong End=Host.StorageProcess.Now+8000;
        while (Host.StorageProcess.Now<End) { Supervisor.Tick(); var Value=Queue.TakeCompletion(); if (Value!=null) return Value; Thread.Sleep(5); }
        throw new TimeoutException("completion missing");
    }
    static void ProcessTests(string Executable)
    {
        if (Environment.OSVersion.Platform==PlatformID.Win32NT) {
            var Drive=new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
            Console.WriteLine("[CarbonLuau:Persistence] Fixture volume: "+Drive.DriveFormat+"; "+Drive.DriveType);
            for (var Parent=new DirectoryInfo(Environment.CurrentDirectory); Parent!=null; Parent=Parent.Parent)
                Console.WriteLine("[CarbonLuau:Persistence] Fixture ancestor: "+Parent.FullName+"; "+Parent.Attributes);
        }
        string Directory=Path.Combine(Environment.CurrentDirectory,"managed-storage-"+Guid.NewGuid().ToString("N"));
        using (var Probe=new Host.StorageProcess(()=>false)) {
            try { Probe.Start(Executable,Directory); }
            catch (Exception Failure) {
                var Child=(Process)typeof(Host.StorageProcess).GetField("Child",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(Probe);
                if (Child!=null && Child.WaitForExit(1000)) {
                    char[] Text=new char[1024]; int Count=Child.StandardError.Read(Text,0,Text.Length);
                    Console.WriteLine("[CarbonLuau:Persistence] Startup diagnostic: "+new string(Text,0,Count));
                }
                var Rejected=Failure as Host.StorageProcess.StartupFailure;
                throw new Exception("direct startup failure"+(Rejected==null ? "" : " code="+Rejected.Code),Failure);
            }
            finally { Check(Probe.Stop()); }
        }
        for (int Cycle=0; Cycle<3; ++Cycle) {
            var Queue=new Queue((ulong)Cycle+1,()=>Host.StorageProcess.Now);
            var Supervisor=new Host.StorageSupervisor(Queue,Executable,Directory);
            var Owner=Queue.Bind(2,3,null); Supervisor.Start();
            try {
                Ready(Supervisor,Queue);
                if (Cycle==0) {
                    Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
                    var Saved=Wait(Supervisor,Queue); Check(Saved.Error==Host.StorageQueue.Error.None && Saved.Found); Queue.Release(Saved);
                }
                Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],2);
                var Loaded=Wait(Supervisor,Queue); Check(Loaded.Error==Host.StorageQueue.Error.None && Loaded.Found && Convert.ToBase64String(Loaded.Envelope)==Convert.ToBase64String(Envelope())); Queue.Release(Loaded);
                Check(Queue.PendingCount==0);
                string Status=Supervisor.Status;
                foreach(string Field in new[] {"completed_get=1", "completed_set=", "completed_remove=", "queue_rejected=", "rate_rejected=", "quota_rejected=", "expired=", "backend_failures=", "corruptions=", "discarded="}) Check(Status.Contains(Field));
                Check(!Status.Contains(Directory) && Status.Length<1024);
            } finally {
                Queue.Retire(Owner); Supervisor.Stop(); ulong End=Host.StorageProcess.Now+5000;
                while (!Supervisor.IsFinished && Host.StorageProcess.Now<End) Thread.Sleep(5);
                Check(Supervisor.IsFinished);
            }
        }
        Console.WriteLine("[CarbonLuau:Persistence] Managed supervised-worker/restart PASS");
    }
    static void Ready(Host.StorageSupervisor Supervisor,Queue Queue)
    {
        ulong End=Host.StorageProcess.Now+32000;
        do { Supervisor.Tick(); if (Queue.Ready) return; Thread.Sleep(5); }
        while (!Supervisor.IsFinished && Host.StorageProcess.Now<End);
        throw new Exception("worker unavailable: "+Supervisor.Status);
    }
    static void ProtocolInputTests(string Executable)
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"protocol-storage-"+Guid.NewGuid().ToString("N"));
        using (var Probe=new Host.StorageProcess(()=>false)) {
            try {
                Probe.Start(Executable,Directory);
                var Flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var Child=(Process)typeof(Host.StorageProcess).GetField("Child",Flags).GetValue(Probe);
                var Input=Child.StandardInput.BaseStream;
                byte[] LatePreamble={0xef,0xbb,0xbf,8};
                Input.Write(LatePreamble,0,LatePreamble.Length); Input.Flush();
                var Reply=(byte[])typeof(Host.StorageProcess).GetMethod("Read",Flags).Invoke(Probe,new object[] {Host.StorageProcess.Now+5000});
                Check(Reply.Length==12 && Encoding.ASCII.GetString(Reply,0,4)=="CLPR" && Host.StorageProcess.U32(Reply,8)==1);
            } finally { Check(Probe.Stop()); }
        }
        Console.WriteLine("[CarbonLuau:Persistence] UTF-8 preamble accepted only before initialization; later framing remains strict PASS");
    }
    static void FaultTests(string Executable,bool Committed)
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"fault-storage-"+Guid.NewGuid().ToString("N"));
        var Queue=new Queue(1,()=>Host.StorageProcess.Now);
        var Supervisor=new Host.StorageSupervisor(Queue,Executable,Directory); var Owner=Queue.Bind(2,3,null);
        Supervisor.Start();
        try {
            Ready(Supervisor,Queue);
            Queue.Submit(Owner,2,3,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
            var Failed=Wait(Supervisor,Queue); Check(Failed.Error==Host.StorageQueue.Error.Indeterminate); Queue.Release(Failed);
            Ready(Supervisor,Queue);
            Check(Supervisor.WorkerStarts==2 && Supervisor.RequestsSent==1); // no automatic replay
            Queue.Retire(Owner); Owner=Queue.Bind(22,33,null); // stable namespace, new completion lifetime
            Queue.Submit(Owner,22,33,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],2);
            var Value=Wait(Supervisor,Queue); Check(Value.Error==Host.StorageQueue.Error.None && Value.Found==Committed); Queue.Release(Value);
            Check(Supervisor.RequestsSent==2 && Queue.PendingCount==0);
        } finally {
            Queue.RetireAll(); Supervisor.Stop(); ulong End=Host.StorageProcess.Now+5000;
            while (!Supervisor.IsFinished && Host.StorageProcess.Now<End) Thread.Sleep(5);
            Check(Supervisor.IsFinished);
        }
        Console.WriteLine("[CarbonLuau:Persistence] Actual IPC "+(Committed ? "lost committed acknowledgement" : "deadline kill/reap")+" PASS; no replay");
    }
    struct Resources
    {
        internal long ManagedBytes;
        internal int Handles,Threads;
    }
    static Resources MeasureResources()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var Result=new Resources { ManagedBytes=GC.GetTotalMemory(true) };
        using(var Process=System.Diagnostics.Process.GetCurrentProcess()) {
            Process.Refresh(); Result.Threads=Process.Threads.Count;
            Result.Handles=Environment.OSVersion.Platform==PlatformID.Win32NT ? Process.HandleCount : System.IO.Directory.GetFiles("/proc/self/fd").Length;
        }
        return Result;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static long ShutdownCycle(string Executable)
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"shutdown-"+Guid.NewGuid().ToString("N"));
        var Queue=new Queue(1,()=>Host.StorageProcess.Now); var Owner=Queue.Bind(1,1,null);
        var Supervisor=new Host.StorageSupervisor(Queue,Executable,Directory); Supervisor.Start();
        Ready(Supervisor,Queue);
        Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
        var Saved=Wait(Supervisor,Queue); Check(Saved.Error==Host.StorageQueue.Error.None && Saved.Found); Queue.Release(Saved);
        Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Remove,"Store","Key",new byte[0],2);
        var Removed=Wait(Supervisor,Queue); Check(Removed.Error==Host.StorageQueue.Error.None && Removed.Found); Queue.Release(Removed);
        Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),3);
        Supervisor.Tick(); Queue.RetireAll();
        long Before=Stopwatch.GetTimestamp(); Supervisor.Stop(); long StopTicks=Stopwatch.GetTimestamp()-Before;
        ulong End=Host.StorageProcess.Now+5000;
        while(!Supervisor.IsFinished && Host.StorageProcess.Now<End) Thread.Sleep(1);
        Check(Supervisor.IsFinished); Supervisor.Tick(); Check(Queue.TakeCompletion()==null && Queue.PendingCount==0);
        using(var Guard=new Host.StorageOwnership(Directory)) { }
        return StopTicks;
    }
    static void ShutdownTests(string Executable)
    {
        var Timer=Stopwatch.StartNew(); long MaximumStop=0;
        // Warm JIT, ThreadPool, pipes and the measurement itself before sampling.
        MeasureResources(); for(int Cycle=0; Cycle<4; ++Cycle) ShutdownCycle(Executable);
        Resources Before=MeasureResources();
        for(int Cycle=0; Cycle<32; ++Cycle) MaximumStop=Math.Max(MaximumStop,ShutdownCycle(Executable));
        Resources After=MeasureResources();
        long ManagedDelta=After.ManagedBytes-Before.ManagedBytes;
        int HandleDelta=After.Handles-Before.Handles, ThreadDelta=After.Threads-Before.Threads;
        Console.WriteLine("[CarbonLuau:Persistence] Warmed 32-cycle resources: managed_bytes="+Before.ManagedBytes+"->"+After.ManagedBytes+
            " delta="+ManagedDelta+"; "+(Environment.OSVersion.Platform==PlatformID.Win32NT ? "handles=" : "fds=")+Before.Handles+"->"+After.Handles+
            " delta="+HandleDelta+"; threads="+Before.Threads+"->"+After.Threads+" delta="+ThreadDelta+
            "; tolerances=2097152_bytes/16_handles_or_fds/8_threads");
        // Conservative process-wide noise allowance: runtime caches/thread-pool
        // scheduling are not exact-zero resources. This is a finite churn gate,
        // not a proof about arbitrary-duration workloads or whole-process RSS.
        Check(ManagedDelta<=2*1024*1024 && HandleDelta<=16 && ThreadDelta<=8);
        // Deterministic close-before-handoff interleaving, using private test reflection.
        var ClosedQueue=new Queue(1,()=>Host.StorageProcess.Now) { Ready=true }; var Binding=ClosedQueue.Bind(1,1,null);
        var Closed=new Host.StorageSupervisor(ClosedQueue,Executable,"unused");
        typeof(Host.StorageSupervisor).GetField("Ready",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Closed,1);
        typeof(Host.StorageSupervisor).GetField("Closed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Closed,1);
        var Lost=ClosedQueue.Submit(Binding,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
        Closed.Tick(); Check(Lost.State==2 && Lost.Error==Host.StorageQueue.Error.StorageUnavailable && !Lost.Sent);
        Closed.Tick(); ClosedQueue.Release(ClosedQueue.TakeCompletion()); Check(ClosedQueue.PendingCount==0);
        ((AutoResetEvent)typeof(Host.StorageSupervisor).GetField("Wake",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Closed)).Dispose();
        Console.WriteLine("[CarbonLuau:Persistence] 4 warmup + 32 Set/Remove/active handoff/retire/stop/reacquire cycles + deterministic closed-handoff PASS; elapsed_ms="+Timer.ElapsedMilliseconds+" max_owner_stop_ms="+(MaximumStop*1000.0/Stopwatch.Frequency));
    }
    static void PreflightDisableTests(string Executable)
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"preflight-"+Guid.NewGuid().ToString("N"));
        var Queue=new Queue(1,()=>Host.StorageProcess.Now); var Owner=Queue.Bind(1,1,null);
        var Supervisor=new Host.StorageSupervisor(Queue,Executable,Directory); Supervisor.Start();
        try {
            Ready(Supervisor,Queue);
            Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Set,"Store","Key",Envelope(),1);
            var Saved=Wait(Supervisor,Queue); Check(Saved.Error==Host.StorageQueue.Error.None && Saved.Found); Queue.Release(Saved);
            string Database=Path.Combine(Directory,"store.sqlite3"), Unexpected=Path.Combine(Directory,"unexpected.fixture");
            byte[] Before;
            // No request is active, but SQLite legitimately keeps its writable
            // handle open. The test reader must share that handle on Windows.
            using(var Input=new FileStream(Database,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                Check(Input.Length<=65536);
                using(var Copy=new MemoryStream()) { Input.CopyTo(Copy); Before=Copy.ToArray(); }
            }
            byte[] Marker=Encoding.ASCII.GetBytes("preserve-preflight-fixture");
            // Deliberate external interference in this unique disposable fixture,
            // after Ready: this must disable, not restart or silently clean up.
            File.WriteAllBytes(Unexpected,Marker);
            Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],2);
            var Failed=Wait(Supervisor,Queue); Check(Failed.Error==Host.StorageQueue.Error.StorageUnavailable); Queue.Release(Failed);
            ulong End=Host.StorageProcess.Now+5000;
            while (!Supervisor.IsFinished && Host.StorageProcess.Now<End) { Supervisor.Tick(); Thread.Sleep(5); }
            Check(Supervisor.IsFinished);
            for (int Tick=0; Tick<8; ++Tick) {
                Supervisor.Tick();
                Check(!Queue.Ready && Supervisor.WorkerStarts==1 && Supervisor.RequestsSent==2 && Queue.PendingCount==0);
            }
            Check(Supervisor.LastFailure==Host.StorageQueue.Error.StorageUnavailable);
            foreach (var Op in new[] {Host.StorageQueue.Operation.Get,Host.StorageQueue.Operation.Set,Host.StorageQueue.Operation.Remove})
                Rejected(()=>Queue.Submit(Owner,1,1,true,Op,"Store","Key",Op==Host.StorageQueue.Operation.Set ? Envelope() : new byte[0],3));
            Check(Convert.ToBase64String(Marker)==Convert.ToBase64String(File.ReadAllBytes(Unexpected)));
            Check(Convert.ToBase64String(Before)==Convert.ToBase64String(File.ReadAllBytes(Database)));
        } finally {
            Queue.RetireAll(); Supervisor.Stop(); ulong End=Host.StorageProcess.Now+5000;
            while (!Supervisor.IsFinished && Host.StorageProcess.Now<End) Thread.Sleep(5);
            Check(Supervisor.IsFinished);
        }
        Console.WriteLine("[CarbonLuau:Persistence] Actual IPC post-Ready preflight failure disables without restart, rejects admission and preserves files PASS");
    }
    static void DuplicateWorkerTests(string Executable)
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"duplicate-"+Guid.NewGuid().ToString("N"));
        using(var First=new Host.StorageProcess(()=>false)) {
            First.Start(Executable,Directory);
            using(var Other=new Host.StorageProcess(()=>false)) {
                bool Failed=false;
                try { Other.Start(Executable,Directory); } catch(Host.StorageProcess.StartupFailure Error) { Check(Error.Code==4); Failed=true; }
                finally { Check(Other.Stop()); }
                Check(Failed);
            }
            Check(First.Stop());
        }
        using(var Next=new Host.StorageProcess(()=>false)) { Next.Start(Executable,Directory); Check(Next.Stop()); }
        byte[] Before=File.ReadAllBytes(Path.Combine(Directory,"store.sqlite3")); Before[0]^=1;
        File.WriteAllBytes(Path.Combine(Directory,"store.sqlite3"),Before);
        var Queue=new Queue(1,()=>Host.StorageProcess.Now); var Supervisor=new Host.StorageSupervisor(Queue,Executable,Directory); Supervisor.Start();
        ulong End=Host.StorageProcess.Now+32000;
        while(!Supervisor.IsFinished && Host.StorageProcess.Now<End) { Supervisor.Tick(); Thread.Sleep(5); }
        Check(Supervisor.IsFinished && !Queue.Ready && Supervisor.WorkerStarts==1 && Supervisor.LastFailure==Host.StorageQueue.Error.StorageCorrupt);
        Check(Supervisor.Status.Contains("corruptions=1"));
        Check(Convert.ToBase64String(Before)==Convert.ToBase64String(File.ReadAllBytes(Path.Combine(Directory,"store.sqlite3"))));
        Console.WriteLine("[CarbonLuau:Persistence] Actual worker duplicate-writer fencing; corrupt startup disables without restart/preserves database PASS");
    }
    static int ParentFixture(string Executable,string Directory)
    {
        using (var Worker=new Host.StorageProcess(()=>false)) {
            Worker.Start(Executable,Directory);
            var Child=(Process)typeof(Host.StorageProcess).GetField("Child",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Worker);
            Console.WriteLine(Child.Id); Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite); return 0;
        }
    }
    static void ParentDeathTests(string Executable)
    {
        string Directory=Path.Combine(Environment.CurrentDirectory,"parent-"+Guid.NewGuid().ToString("N"));
        using(var Parent=Child("--parent \""+Executable+"\" \""+Directory+"\"")) {
            var Line=Parent.StandardOutput.ReadLineAsync(); Check(Line.Wait(32000)); int Id; Check(int.TryParse(Line.Result,out Id));
            using(var Worker=Process.GetProcessById(Id)) {
                Parent.Kill(); Check(Parent.WaitForExit(5000));
                ulong End=Host.StorageProcess.Now+5000;
                while(!Worker.HasExited && Host.StorageProcess.Now<End) Thread.Sleep(5);
                Check(Worker.HasExited);
            }
        }
        // Reopening the exact directory proves the dead worker released writer ownership.
        using(var Worker=new Host.StorageProcess(()=>false)) { Worker.Start(Executable,Directory); Check(Worker.Stop()); }
        Console.WriteLine("[CarbonLuau:Persistence] Abrupt parent death kills child and writer lock is reusable PASS (not power-loss evidence)");
    }
    static int Main(string[] Args)
    {
        try {
            if (Args.Length>0 && Args[0]=="--utf8-input") {
                Console.InputEncoding=new UTF8Encoding(true);
                var Rest=new string[Args.Length-1]; Array.Copy(Args,1,Rest,0,Rest.Length); Args=Rest;
                Console.WriteLine("[CarbonLuau:Persistence] Test-process UTF-8 input preamble enabled");
            }
            if (Args.Length==2 && Args[0]=="--ownership") {
                try { using(var Guard=new Host.StorageOwnership(Args[1])) { return 0; } } catch(IOException) { return 23; }
            }
            if (Args.Length==3 && Args[0]=="--parent") return ParentFixture(Args[1],Args[2]);
            QueueTests(); BoundsTests(); NamespaceRetentionTests(); ProtocolTests(); OwnershipTests();
            if (Args.Length>=1) { ProcessTests(Path.GetFullPath(Args[0])); ProtocolInputTests(Path.GetFullPath(Args[0])); ShutdownTests(Path.GetFullPath(Args[0])); ParentDeathTests(Path.GetFullPath(Args[0])); DuplicateWorkerTests(Path.GetFullPath(Args[0])); PreflightDisableTests(Path.GetFullPath(Args[0])); }
            if (Args.Length==3) { FaultTests(Path.GetFullPath(Args[1]),true); FaultTests(Path.GetFullPath(Args[2]),false); }
            return 0;
        }
        catch (Exception Error) { Console.Error.WriteLine("[CarbonLuau:Persistence] Test failure: "+Error); return 1; }
    }
}
