using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Host=Carbon.Plugins.CarbonLuau;
using Queue=Carbon.Plugins.CarbonLuau.StorageQueue;
partial class ManagedTests
{
    static void DemandString(BinaryWriter Writer,byte[] Value)
    { Writer.Write((uint)Value.Length);Writer.Write(Value); }
    static uint DemandExchange(Host.StorageProcess Worker,ulong Nonce,byte[] Field,byte[] Envelope,
        uint ExpectedCode)
    {
        ulong End=Host.StorageProcess.Now+5000;
        byte[] Request;
        using(var Stream=new MemoryStream())using(var Writer=new BinaryWriter(Stream)){
            Writer.Write(Encoding.ASCII.GetBytes("CLPQ"));Writer.Write(1u);Writer.Write(4u);
            Writer.Write(Nonce);
            for(ulong Route=1;Route<=4;++Route)Writer.Write(Route);
            Writer.Write(End);Writer.Write(0u);
            DemandString(Writer,new byte[0]);
            DemandString(Writer,Encoding.UTF8.GetBytes("Store"));
            DemandString(Writer,Field);
            DemandString(Writer,Envelope);
            Request=Stream.ToArray();
        }
        var Reply=Worker.Exchange(Request,End);
        Check(Reply.Length==60 && Encoding.ASCII.GetString(Reply,0,4)=="CLPS" &&
            Host.StorageProcess.U32(Reply,4)==1 && Host.StorageProcess.U32(Reply,8)==ExpectedCode &&
            Host.StorageProcess.U64(Reply,12)==Nonce && Host.StorageProcess.U32(Reply,56)==0);
        for(int Route=0;Route<4;++Route)
            Check(Host.StorageProcess.U64(Reply,20+Route*8)==(ulong)Route+1);
        return Host.StorageProcess.U32(Reply,52);
    }
    static void DemandWorkerIngress(string Executable)
    {
        var Directory=Path.Combine(Environment.CurrentDirectory,"demand-worker-"+Guid.NewGuid().ToString("N"));
        ulong Nonce=0;
        using(var Worker=new Host.StorageProcess(()=>false)){
            Worker.Start(Executable,Directory);
            var Field=Encoding.UTF8.GetBytes("/literal.field");
            Check(DemandExchange(Worker,++Nonce,Field,new byte[0],0)==0);
            uint Flags=0;
            ulong End=Host.StorageProcess.Now+5000;
            while(Host.StorageProcess.Now<End){
                Flags=DemandExchange(Worker,++Nonce,Field,new byte[0],0);
                if(Flags==1)break;
                Check(Flags==0);
            }
            Check(Flags==1);
            Check(DemandExchange(Worker,++Nonce,new byte[0],new byte[0],1)==0);
            Check(DemandExchange(Worker,++Nonce,new byte[]{0xc0,0xaf},new byte[0],1)==0);
            Check(DemandExchange(Worker,++Nonce,Field,new byte[]{1},1)==0);
            for(int Index=1;Index<8;++Index)
                Check(DemandExchange(Worker,++Nonce,Encoding.UTF8.GetBytes("Field"+Index),new byte[0],0)<=1);
            Check(DemandExchange(Worker,++Nonce,Encoding.UTF8.GetBytes("Ninth"),new byte[0],0)==2);
        }
        Console.WriteLine("[CarbonLuau:Persistence] CLPQ Op=4 demand framing, ready, invalid input and field ceiling PASS");
    }
    static void DerivedFixture(string Executable,string Mode,string Directory)
    {
        using(var Child=Process.Start(new ProcessStartInfo(Executable,Mode+" \""+Directory+"\"") {
            UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
            RedirectStandardOutput=true,RedirectStandardError=true })) {
            var Output=Child.StandardOutput.ReadToEndAsync();var Error=Child.StandardError.ReadToEndAsync();
            if(!Child.WaitForExit(60000)){Child.Kill();Child.WaitForExit(5000);throw new Exception("derived fixture timeout");}
            Console.Write(Output.Result);Check(Child.ExitCode==0);
        }
    }
    static void DerivedWorkerTests(string Executable,string Fixture)
    {
        DemandWorkerIngress(Executable);
        var Directory=Path.Combine(Environment.CurrentDirectory,"derived-worker-"+Guid.NewGuid().ToString("N"));
        DerivedFixture(Fixture,"seed",Directory);
        var Queue=new Queue(1,()=>Host.StorageProcess.Now);var Owner=Queue.Bind(1,1,null);
        var Supervisor=new Host.StorageSupervisor(Queue,Executable,Directory);Supervisor.Start();
        ulong MaxLatency=0;
        try{
            Ready(Supervisor,Queue);
            for(ulong I=0;I<64;++I){
                var Started=Host.StorageProcess.Now;
                Queue.Submit(Owner,1,1,true,Host.StorageQueue.Operation.Get,"Store","Key",new byte[0],I+1);
                var Done=Wait(Supervisor,Queue);Check(Done.Error==Host.StorageQueue.Error.None && Done.Found);Queue.Release(Done);
                MaxLatency=Math.Max(MaxLatency,Host.StorageProcess.Now-Started);Thread.Sleep(50);
            }
            foreach(var Op in new[]{Host.StorageQueue.Operation.Set,Host.StorageQueue.Operation.Remove,Host.StorageQueue.Operation.Set}){
                Queue.Submit(Owner,1,1,true,Op,"Store","Key",Op==Host.StorageQueue.Operation.Set?Envelope():new byte[0],100);
                var Done=Wait(Supervisor,Queue);Check(Done.Error==Host.StorageQueue.Error.None);Queue.Release(Done);
            }
            var Until=Host.StorageProcess.Now+5000;
            while(Host.StorageProcess.Now<Until){Supervisor.Tick();Thread.Sleep(5);}
            Check(Queue.PendingCount==0 && Queue.Ready && Supervisor.WorkerStarts==1 && Supervisor.RequestsSent==67);
        }finally{
            Queue.RetireAll();Supervisor.Stop();var Until=Host.StorageProcess.Now+5000;
            while(!Supervisor.IsFinished && Host.StorageProcess.Now<Until)Thread.Sleep(5);
            Check(Supervisor.IsFinished);
        }
        DerivedFixture(Fixture,"verify",Directory);
        Console.WriteLine("[CarbonLuau:Persistence] Real worker resumed 8 builds/512 records; foreground Get=64 Set=2 Remove=1; no replay/restart; max_get_ms="+MaxLatency+" PASS");
    }
}
