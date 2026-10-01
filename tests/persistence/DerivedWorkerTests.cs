using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Host=Carbon.Plugins.CarbonLuau;
using Queue=Carbon.Plugins.CarbonLuau.StorageQueue;
partial class ManagedTests
{
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
