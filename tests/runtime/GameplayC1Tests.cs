using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class GameplayC1Tests
{
    [DllImport("libc")] private static extern int chmod(string Path, int Mode);
    private sealed class Registrar : Runtime.ICommandRegistrar { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }
    private static void Check(bool Value, string Reason, [System.Runtime.CompilerServices.CallerArgumentExpression("Value")] string Expression = "", [System.Runtime.CompilerServices.CallerFilePath] string File = "", [System.Runtime.CompilerServices.CallerLineNumber] int Line = 0) {
        if (!Value) TestDiagnostics.Pass(Value, Expression, File, Line); if (!Value) throw new Exception("C1: " + Reason); }
    private static byte[] Package(string Id, string Init, string Api = null, string Required = null)
    {
        using (var Bytes = new MemoryStream()) {
            using (var Zip = new ZipArchive(Bytes, ZipArchiveMode.Create, true)) {
                Action<string,string> Add = (Name, Source) => { using (var Writer = new StreamWriter(Zip.CreateEntry(Name).Open())) Writer.Write(Source); };
                Add("addon.json", "{\"schema\":1,\"id\":\""+Id+"\",\"version\":\"1.0.0\"" +
                    (Api == null ? "" : ",\"publicModules\":[\"api\"]") +
                    (Required == null ? "" : ",\"dependencies\":{\"required\":[\""+Required+"\"],\"optional\":[]}") + "}");
                Add("init.luau", Init); if (Api != null) Add("api.luau", Api);
            } return Bytes.ToArray();
        }
    }
    internal static void Run(string Library, string Compiler)
    {
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauC1-"+Guid.NewGuid().ToString("N"));
        Exception DiagnosticPrimary = null;
        try {
            string Rid = Runtime.NativeLibraryLoader.GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
            string Target = Runtime.NativeLibraryLoader.GetLibraryPath(Root, Rid);
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.Copy(Library, Target);
            string Worker = Path.Combine(Path.GetDirectoryName(Target), Rid == "win-x64" ? "carbonluau_compiler.exe" : "carbonluau_compiler");
            File.Copy(Compiler, Worker); if (Rid == "linux-x64") Check(chmod(Worker,493)==0,"compiler mode");
            using (var Native = new Runtime.NativeRuntime(Root)) {
                int Scans=0; object Definition=new object(), MainId=new object();
                int StackCount=0; long Amount=3;
                var View=new Runtime.PlayerView {Identity=new object(),Connection=new object(),UserId="42",Name="Fixture",Connected=true};
                View.Inventory=()=>{ Scans++; return new Runtime.PhysicalInventorySource {
                    Main=new Runtime.PhysicalInventoryContainer {Identity=MainId,StackCount=StackCount,Read=Index=>new Runtime.PhysicalInventoryStack(MainId,Definition,Amount,true)},
                    Belt=new Runtime.PhysicalInventoryContainer {Identity=new object(),StackCount=0,Read=Index=>default(Runtime.PhysicalInventoryStack)},
                    Wear=new Runtime.PhysicalInventoryContainer {Identity=new object(),StackCount=0,Read=Index=>default(Runtime.PhysicalInventoryStack)}}; };
                var Players=new Runtime.PlayerDirectory(Id=>View);
                Players.Connect(View);
                var World=new Runtime.FacadeWorld(Players,new Registrar(),new Runtime.ItemDirectory(Name=>Name=="scrap" ? Definition : null));
                string Source="local S=require('state'); S.P=game:GetService('Players'):GetPlayers()[1]; S.I=S.P:GetInventory(); assert(S.I==S.P:GetInventory())";
                Func<Runtime.ScriptSnapshot> Snapshot=()=>{
                    var Value=new Runtime.ScriptSnapshot {EntryName="init.luau",EntrySource=Source};
                    Value.Modules.Add("state","return {}");
                    Value.Modules.Add("bad","local S=require('state'); S.Leak=S.P:GetInventory(); assert(S.Leak:CountItem('scrap')>=0); error('failed publication')");
                    Value.Modules.Add("nested","return require('bad')"); return Value; };
                using (var Host=new Runtime.ScriptHost(Native,new Runtime.RuntimeConfig {MaxCallbackMilliseconds=100},Snapshot,World)) {
                    Action<string> Run=Code=>{var Result=Host.Execute("c1",Code); Check(Result.Status==Runtime.RuntimeStatus.OK,Code+": "+Result.Error);};
                    Check(Host.Reload().Status==Runtime.RuntimeStatus.OK && Scans==0,"disk/scan-free acquisition and same-scope cache");
                    Run("local S=require('state'); assert(game.ApiVersion=='0.7.5-experimental'); for _,K in {'CountItem','HasItem','GiveItem','TakeItem'} do assert(S.P[K]==nil) end; assert(type(S.I.GiveItem)=='function' and type(S.I.TakeItem)=='function')");
                    Run("local S=require('state'); assert(S.I:CountItem('scrap')==0 and not S.I:HasItem('scrap')); assert(S.I:CountItem('unknown')==0 and not S.I:HasItem('unknown')); assert(not pcall(function() S.I.X=1 end)); for _,K in {'ItemAdded','ItemRemoved','Unknown'} do assert(not pcall(function() return S.I[K] end)) end");
                    StackCount=128;
                    Run("local S=require('state'); assert(S.I:CountItem(Item.Scrap)==S.I:CountItem('scrap')); assert(S.I:HasItem(Item.Scrap)==S.I:HasItem('scrap'))");
                    Check(Runtime.PhysicalInventoryObservation.Count(View.Inventory(),Definition)==384 &&
                        Runtime.PhysicalInventoryObservation.Has(View.Inventory(),Definition,384) &&
                        !Runtime.PhysicalInventoryObservation.Has(View.Inventory(),Definition,385),"qualified scanner parity at exact bound");
                    Run("local S=require('state'); assert(S.I:CountItem('scrap')==384); assert(S.I:HasItem('scrap',384) and not S.I:HasItem('scrap',385)); assert(S.I:HasItem('scrap'))");
                    StackCount=129;
                    Run("local S=require('state'); for _,O in {S.I,S.P:GetInventory()} do assert(not pcall(function() O:CountItem('scrap') end)); assert(not pcall(function() O:HasItem('scrap') end)) end");
                    StackCount=2; Amount=Runtime.FacadePolicy.MaxExactLuauInteger;
                    Run("assert(not pcall(function() require('state').I:CountItem('scrap') end))");
                    StackCount=1; Amount=3;
                    Run("local S=require('state'); for _,N in {'','Scrap',' scrap','scrap ',string.rep('x',129),'s\\0x','é'} do assert(not pcall(function() S.I:CountItem(N) end)) end; for _,N in {0,-1,1.5,math.huge,0/0,9007199254740992,'1'} do assert(not pcall(function() S.I:HasItem('scrap',N) end)) end; assert(not pcall(function() S.I.CountItem({},'scrap') end)); assert(not pcall(function() S.P.GetInventory({}) end)); assert(not pcall(function() S.I:HasItem(1) end))");
                    Run("local S=require('state'); assert(not pcall(function() require('nested') end)); assert(not pcall(function() S.Leak:CountItem('scrap') end)); assert(S.I:CountItem('scrap')==3)");
                    Run("local S=require('state'); local Current=S.P:GetInventory(); for Index=1,1000 do assert(S.P:GetInventory()==Current); assert(Current:CountItem('scrap')==3 and S.I:CountItem('scrap')==3) end");
                    Check(World.Active.ListenerCount==0,"acquisition/read stress creates no subscription");
                    Source="local P=game:GetService('Players'):GetPlayers()[1]; P:GetInventory(); error('bad candidate')";
                    Check(Host.Reload().Status==Runtime.RuntimeStatus.RUNTIME_ERROR,"failed replacement"); Run("assert(require('state').I:CountItem('scrap')==3)");
                    using (var Registry=new Runtime.AddonRegistry(Host,Native.HostLifetimeId)) {
                        object Provider=new object();
                        string[] Owner=Registry.RegisterArchive(Provider,Package("inventoryowner","return true","return game:GetService('Players'):GetPlayers()[1]:GetInventory()"));
                        Check(Owner[0]=="OK" && Registry.ProcessOne(),"owner activation");
                        string[] Consumer=Registry.RegisterArchive(Provider,Package("inventoryconsumer","local I=require('@inventoryowner/api'); assert(I:CountItem('scrap')==3); return I",null,"inventoryowner"));
                        Check(Consumer[0]=="OK","consumer registration: "+String.Join("|",Consumer));
                        bool Processed=Registry.ProcessOne();
                        Check(Processed && Registry.Status(Provider,Consumer[1])[2]=="Active","shared defining-domain inventory in consumer candidate: "+String.Join("|",Registry.Status(Provider,Consumer[1])));
                        Check(Registry.UnloadProvider(Provider)==2,"provider retirement");
                    }
                    View.Connected=false;
                    Run("local S=require('state'); assert(not pcall(function() S.P:GetInventory() end)); assert(not pcall(function() S.I:CountItem('scrap') end))");
                    View=new Runtime.PlayerView {Identity=new object(),Connection=new object(),UserId="42",Name="Reconnect",Connected=true,Inventory=View.Inventory}; Players.Connect(View);
                    Run("local S=require('state'); assert(not pcall(function() S.I:CountItem('scrap') end)); local P=game:GetService('Players'):GetPlayers()[1]; assert(P~=S.P); assert(P:GetInventory():CountItem('scrap')==3)");
                    Source="local S=require('state'); S.P=game:GetService('Players'):GetPlayers()[1]; S.I=S.P:GetInventory()";
                    Runtime.FacadeSession Previous=World.Active;
                    Check(Host.Reload().Status==Runtime.RuntimeStatus.OK && Previous.Disposed,"successful root replacement retires old authority");
                    Check(Host.Execute("fatal","while true do end").Status==Runtime.RuntimeStatus.TIMEOUT && Host.Ready,"VM recovery");
                    Run("assert(require('state').I:CountItem('scrap')==3)");
                }
                Check(Native.LiveVmCount==0,"native VM cleanup");
            }
            Console.WriteLine("[CarbonLuau:GameplayC1] PASS real VM, shared scanner, acquisition, validation, publication, sharing, connection and recovery");
        } catch (Exception Error) { DiagnosticPrimary = Error; TestDiagnostics.ExceptionFailure(Error); throw; }
        finally { TestDiagnostics.Cleanup(() => { if (Directory.Exists(Root)) Directory.Delete(Root,true); }, DiagnosticPrimary); }
    }
}
