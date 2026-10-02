using System;
using System.Text;
using Host=Carbon.Plugins.CarbonLuau;
using Queue=Carbon.Plugins.CarbonLuau.StorageQueue;

partial class ManagedTests
{
    static byte[] QueryDescriptor()
    {
        var Result=new byte[40];
        Encoding.ASCII.GetBytes("CLQD").CopyTo(Result,0);
        Host.StorageProcess.Put32(Result,4,1);
        Host.StorageProcess.Put32(Result,20,50);
        return Result;
    }
    static byte[] QueryReply(Queue.Request Request,uint Code=0)
    {
        var Result=new byte[Code==0 ? 76 : 60];
        Encoding.ASCII.GetBytes("CLPS").CopyTo(Result,0);
        Host.StorageProcess.Put32(Result,4,1);
        Host.StorageProcess.Put32(Result,8,Code);
        for(int Index=0; Index<8; ++Index) Result[12+Index]=(byte)(Request.WireId>>(8*Index));
        foreach(var Part in new[] {new {Offset=20,Value=Request.Owner.Host},new {Offset=28,Value=Request.Owner.Vm},
            new {Offset=36,Value=Request.Owner.Domain},new {Offset=44,Value=Request.Route}})
            for(int Index=0; Index<8; ++Index) Result[Part.Offset+Index]=(byte)(Part.Value>>(8*Index));
        if(Code==0) {
            Host.StorageProcess.Put32(Result,56,16);
            Encoding.ASCII.GetBytes("CLQR").CopyTo(Result,60);
            Host.StorageProcess.Put32(Result,64,1);
        }
        return Result;
    }
    static void QueryQueueTests()
    {
        ulong Now=1000;
        var Ledger=new Queue(71,()=>Now) {Ready=true}; Ledger.WorkerStarted();
        var Owner=Ledger.Bind(11,12,"query");
        var Accepted=Ledger.SubmitQuery(Owner,11,12,true,"State","a/b:c",QueryDescriptor(),1);
        Ledger.Submit(Owner,11,12,true,Queue.Operation.Get,"State","Key",new byte[0],2);
        Check(Ledger.PendingCount==2 && Ledger.WaiterCount==1 && Accepted.End==6000);
        var Demand=Ledger.Dispatch(); Check(Demand!=null && Demand.InternalDemand && Demand.Field=="a/b:c");
        byte[] Ready=Reply(Demand); Host.StorageProcess.Put32(Ready,52,1);
        Queue.Complete(Demand,Ready,Now);
        var Query=Ledger.Dispatch(); Check(Query==Accepted && Query.Op==Queue.Operation.Query && Ledger.WaiterCount==0);
        Check(Host.StorageProcess.U32(Query.Frame,8)==6 && Query.End==6000);
        Queue.Complete(Query,QueryReply(Query),Now);
        var Later=Ledger.Dispatch(); Check(Later!=null && Later.Op==Queue.Operation.Get);
        var Completed=Ledger.TakeCompletion(); Check(Completed==Query && Completed.Envelope.Length==16);
        Ledger.Release(Completed);
        Queue.Complete(Later,Reply(Later),Now); Ledger.Dispatch(); Ledger.Release(Ledger.TakeCompletion());
        Check(Ledger.PendingCount==0 && Ledger.WaiterCount==0 && Ledger.DemandCount==0 && Ledger.QueryCompleted==1);

        Ledger=new Queue(71,()=>Now) {Ready=true}; Ledger.WorkerStarted(); Owner=Ledger.Bind(11,12,"timeout");
        Accepted=Ledger.SubmitQuery(Owner,11,12,true,"State","Coins",QueryDescriptor(),1);
        Demand=Ledger.Dispatch(); Queue.Complete(Demand,Reply(Demand),Now); Ledger.Dispatch();
        Now+=5000; Ledger.Dispatch(); Completed=Ledger.TakeCompletion();
        if(Completed!=Accepted || Completed.Error!=Queue.Error.IndexPreparing || Ledger.WaiterCount!=0)
            throw new Exception("Query deadline outcome="+(Completed==null ? "none" : Completed.Error.ToString())+
                " same="+(Completed==Accepted)+" waiters="+Ledger.WaiterCount);
        Ledger.Release(Completed); Check(Ledger.PendingCount==0 && Ledger.WaiterCount==0 && Ledger.DemandCount==0);

        Ledger=new Queue(71,()=>Now) {Ready=true}; Ledger.WorkerStarted(); Owner=Ledger.Bind(11,12,"unavailable");
        Accepted=Ledger.SubmitQuery(Owner,11,12,true,"State","Coins",QueryDescriptor(),1);
        Demand=Ledger.Dispatch(); var Unavailable=Reply(Demand); Host.StorageProcess.Put32(Unavailable,52,2);
        Queue.Complete(Demand,Unavailable,Now); Ledger.Dispatch(); Completed=Ledger.TakeCompletion();
        Check(Completed==Accepted && Completed.Error==Queue.Error.QueryUnavailable);
        Ledger.Release(Completed); Check(Ledger.PendingCount==0 && Ledger.DemandCount==0);

        // The exact 66-KiB result reservation is legal, while one extra byte
        // is rejected before it can be copied into the preallocated slot.
        Ledger=new Queue(71,()=>Now) {Ready=true}; Ledger.WorkerStarted(); Owner=Ledger.Bind(11,12,"resultbound");
        Ledger.Hint(Owner,"State","Coins"); Demand=Ledger.Dispatch();
        Ready=Reply(Demand); Host.StorageProcess.Put32(Ready,52,1); Queue.Complete(Demand,Ready,Now); Ledger.Dispatch();
        Accepted=Ledger.SubmitQuery(Owner,11,12,true,"State","Coins",QueryDescriptor(),1);
        Query=Ledger.Dispatch(); Check(Query==Accepted && Ledger.WaiterCount==0);
        var Large=new byte[60+67584]; QueryReply(Query).CopyTo(Large,0);
        Host.StorageProcess.Put32(Large,56,67584);
        var Excess=new byte[Large.Length+1]; Large.CopyTo(Excess,0);
        Host.StorageProcess.Put32(Excess,56,67585);
        Invalid(()=>Queue.Complete(Query,Excess,Now)); Check(Query.State==1);
        Queue.Complete(Query,Large,Now); Ledger.Dispatch(); Completed=Ledger.TakeCompletion();
        Check(Completed==Query && Completed.Envelope.Length==67584);
        Ledger.Release(Completed); Check(Ledger.PendingCount==0 && Ledger.WaiterCount==0);

        Ledger=new Queue(71,()=>Now) {Ready=true}; Ledger.WorkerStarted(); Owner=Ledger.Bind(11,12,"retired");
        Accepted=Ledger.SubmitQuery(Owner,11,12,true,"State","Coins",QueryDescriptor(),1);
        Ledger.Retire(Owner); Ledger.Dispatch();
        Check(Ledger.TakeCompletion()==null && Ledger.PendingCount==0 && Ledger.WaiterCount==0);

        Ledger=new Queue(71,()=>Now) {Ready=true}; Ledger.WorkerStarted(); Owner=Ledger.Bind(11,12,"inflight");
        Ledger.Hint(Owner,"State","Coins"); Demand=Ledger.Dispatch(); Ready=Reply(Demand);
        Host.StorageProcess.Put32(Ready,52,1); Queue.Complete(Demand,Ready,Now); Ledger.Dispatch();
        Accepted=Ledger.SubmitQuery(Owner,11,12,true,"State","Coins",QueryDescriptor(),1);
        Query=Ledger.Dispatch(); Check(Query==Accepted && Query.State==1);
        Ledger.Retire(Owner);
        Queue.Complete(Query,QueryReply(Query),Now); Ledger.Dispatch();
        Check(Ledger.TakeCompletion()==null && Ledger.PendingCount==0 && Ledger.QueryCompleted==0);
        Console.WriteLine("[CarbonLuau:Persistence] Query FIFO/deadline/ready/unavailable, result bound and stale-retirement cleanup PASS");
    }
}
