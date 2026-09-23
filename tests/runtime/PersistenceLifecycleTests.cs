using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Runtime = Carbon.Plugins.CarbonLuau;

// Uses real native domains/ScriptHost/AddonRegistry. Queue completions are bounded
// synthetic transport here; actual worker fencing/crashes live in ManagedTests.
internal static class PersistenceLifecycleTests
{
    private static void Check(bool Value,string Message) { if(!Value) throw new Exception("Persistence lifecycle: "+Message); }
    private static Runtime.RuntimeDomain Root(Runtime.ScriptHost Host)
    { return (Runtime.RuntimeDomain)typeof(Runtime.ScriptHost).GetField("Current",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Host); }
    private static Runtime.RuntimeDomain Addon(Runtime.ScriptHost Host)
    {
        var Domains=(SortedDictionary<long,Runtime.RuntimeDomain>)typeof(Runtime.ScriptHost).GetField("AddonDomains",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Host);
        Check(Domains.Count==1,"one addon domain"); foreach(var Domain in Domains.Values) return Domain; throw new Exception();
    }
    private static byte[] Reply(Runtime.StorageQueue.Request Request)
    {
        using(var Buffer=new MemoryStream()) using(var Writer=new BinaryWriter(Buffer)) {
            Writer.Write(Encoding.ASCII.GetBytes("CLPS")); Writer.Write(1u); Writer.Write(0u); Writer.Write(Request.Id);
            Writer.Write(Request.Owner.Host); Writer.Write(Request.Owner.Vm); Writer.Write(Request.Owner.Domain); Writer.Write(Request.Route);
            Writer.Write(0u); Writer.Write(0u); return Buffer.ToArray();
        }
    }
    internal static void Run(Runtime.NativeRuntime Native)
    {
        Check(Native.Storage==null,"isolated fixture storage"); ulong Now=1;
        var Storage=new Runtime.StorageQueue((ulong)Native.HostLifetimeId,()=>Now) { Ready=true }; Native.Storage=Storage;
        var Config=new Runtime.RuntimeConfig {MaxVmMemoryMiB=32,MaxCallbackMilliseconds=20};
        Func<Runtime.ScriptSnapshot> Snapshot=()=>new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource="return true"};
        try {
            using(var Host=new Runtime.ScriptHost(Native,Config,Snapshot)) using(var Registry=new Runtime.AddonRegistry(Host,Native.HostLifetimeId)) {
                Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"initial root");
                var Initial=Root(Host).StorageBinding; Check(Initial!=null && Initial.Package==null,"root namespace binding");
                Storage.Submit(Initial,Initial.Vm,Initial.Domain,true,Runtime.StorageQueue.Operation.Get,"S","K",new byte[0],1);
                var Active=Storage.Dispatch(); Check(Active!=null,"root request in flight");
                Check(Host.Reload("error('candidate rejected')").Status==Runtime.RuntimeStatus.RUNTIME_ERROR,"failed root candidate");
                Check(ReferenceEquals(Initial,Root(Host).StorageBinding) && Initial.Alive,"failed root preserves exact binding");
                Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"root replacement");
                var Replacement=Root(Host).StorageBinding;
                Check(!Initial.Alive && Replacement.Alive && Initial.Namespace==Replacement.Namespace && Initial.Domain!=Replacement.Domain,"root stable namespace/fresh lifetime");
                Storage.Submit(Replacement,Replacement.Vm,Replacement.Domain,true,Runtime.StorageQueue.Operation.Get,"S","K",new byte[0],2);
                Check(Storage.Dispatch()==null,"replacement fenced behind old active request");
                Runtime.StorageQueue.Complete(Active,Reply(Active),Now); var Next=Storage.Dispatch();
                Check(Next!=null && ReferenceEquals(Next.Owner,Replacement),"replacement dispatch after terminal old reply");
                Check(Storage.TakeCompletion()==null,"old root completion suppressed");
                Runtime.StorageQueue.Complete(Next,Reply(Next),Now); Storage.Dispatch(); Storage.Release(Storage.TakeCompletion());
                object Provider=new object(); string[] Registration=Registry.RegisterSource(Provider,"persist.fixture","1.0.0",Encoding.UTF8.GetBytes("return true"));
                Check(Registration[0]=="OK" && Registry.ProcessOne(),"addon activation");
                var First=Addon(Host).StorageBinding; Check(First.Package=="persist.fixture" && First.Namespace!=Replacement.Namespace,"stable tagged addon namespace");
                Check(Registry.ReplaceSource(Provider,Registration[1],"2.0.0",Encoding.UTF8.GetBytes("error('candidate rejected')"))[0]=="OK" && Registry.ProcessOne(),"failed addon candidate evaluated");
                Check(ReferenceEquals(First,Addon(Host).StorageBinding) && First.Alive,"failed addon preserves binding");
                Check(Registry.ReplaceSource(Provider,Registration[1],"2.0.0",Encoding.UTF8.GetBytes("return true"))[0]=="OK" && Registry.ProcessOne(),"addon replacement");
                var Second=Addon(Host).StorageBinding; Check(!First.Alive && Second.Alive && First.Namespace==Second.Namespace,"addon replacement same data/fresh authority");
                Check(Registry.UnloadProvider(Provider)==1 && !Second.Alive,"provider unload retires addon authority");
                Provider=new object(); Registration=Registry.RegisterSource(Provider,"persist.fixture","3.0.0",Encoding.UTF8.GetBytes("return true"));
                Check(Registration[0]=="OK" && Registry.ProcessOne(),"new provider same package registration");
                var Reassigned=Addon(Host).StorageBinding;
                Check(Reassigned.Alive && Reassigned.Namespace==Second.Namespace && Reassigned.Domain!=Second.Domain,"provider reassignment preserves namespace, not lifetime");
                Second=Reassigned;
                Storage.Submit(Second,Second.Vm,Second.Domain,true,Runtime.StorageQueue.Operation.Get,"S","K",new byte[0],3);
                Active=Storage.Dispatch();
                var Timeout=Host.Execute("timeout","while true do end");
                Check(Timeout.Status==Runtime.RuntimeStatus.TIMEOUT && Host.Ready && Host.Recoveries==1,"actual fatal timeout/recovery");
                Check(!Second.Alive && !Replacement.Alive && Root(Host).StorageBinding.Alive && Root(Host).StorageBinding.Vm!=Second.Vm,"all old VM bindings retired");
                Runtime.StorageQueue.Complete(Active,Reply(Active),Now); Storage.Dispatch(); Check(Storage.TakeCompletion()==null && Storage.PendingCount==0,"retired addon late completion suppressed");
                for(int Index=0; Index<100; ++Index) { var Previous=Root(Host).StorageBinding; Check(Host.Reload().Status==Runtime.RuntimeStatus.OK && !Previous.Alive,"repeated root retirement"); }
            }
            Check(Native.LiveVmCount==0,"all native VMs released"); Storage.Dispatch(); Check(Storage.PendingCount==0,"all reservations released");
            Console.WriteLine("[CarbonLuau:Persistence] Actual root/addon candidate, replacement, timeout/recovery, stale completion and 100 reload lifecycle PASS");
        } finally { Storage.RetireAll(); Native.Storage=null; }
    }
}
