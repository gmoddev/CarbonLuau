using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class ScriptTests
{
    private static void Check(bool Value, string Message) { if (!Value) throw new Exception("Phase 2: " + Message); }
    private static string Root, Scripts, Entry;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    private static void Source(string Text) { File.WriteAllText(Entry, Text, Utf8); }
    private static void Reject(Action Action, string Message) { bool Failed = false; try { Action(); } catch (InvalidOperationException) { Failed = true; } Check(Failed, Message); }
    private static string Drain(Runtime.ScriptHost Host) {
        string Logs = "";
        for (int Frame = 0; Frame < 100 && Host.HasWork; ++Frame)
            foreach (var Result in Host.Drain()) Logs += Result.Logs;
        return Logs;
    }
    [DllImport("libc", SetLastError = true)] private static extern int symlink(string Target, string Link);
    public static void Run(Runtime.NativeRuntime Native, string DataRoot)
    {
        Root = DataRoot; Scripts = Path.Combine(Root, "CarbonLuau", "scripts"); Entry = Path.Combine(Scripts, "init.luau");
        Directory.CreateDirectory(Path.Combine(Scripts, "modules", "util"));
        File.WriteAllText(Path.Combine(Scripts, "modules", "util", "value.luau"), "return {Value=42}", Utf8);
        var Config = new Runtime.RuntimeConfig { MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 1 };
        Source("local V=require('util/value'); assert(V.Value==42); print('loaded'); task.defer(function() print('later') end)");
        var Snapshot = Runtime.ScriptSnapshot.Load(Root, Config);
        Check(Snapshot.Modules.Count == 1 && Snapshot.EntryName == "init.luau", "snapshot");
        foreach (string PathValue in new[] {"../x", "a/../b", "a//b", "a\\..\\b", "/a", "C:/a", "C:a", "\\\\host\\share", "", "a/", "a/./b", "A", "nul", "com1"})
            Reject(() => Runtime.ScriptSnapshot.ValidatePath(PathValue, false), "path rejected: " + PathValue);
        Reject(() => Runtime.ScriptSnapshot.Load(Root, new Runtime.RuntimeConfig { EntryScript = "missing.luau" }), "missing entry");
        File.WriteAllBytes(Entry, new byte[] {239,187,191,112,114,105,110,116,40,49,41});
        Check(Runtime.ScriptSnapshot.Load(Root, Config).EntrySource == "print(1)", "BOM stripped");
        File.WriteAllBytes(Entry, new byte[] {0xC3,0x28}); Reject(() => Runtime.ScriptSnapshot.Load(Root,Config),"invalid UTF8");
        File.WriteAllBytes(Entry, new byte[] {65,0,66}); Reject(() => Runtime.ScriptSnapshot.Load(Root,Config),"NUL rejected");
        File.WriteAllBytes(Entry, new byte[65537]); Reject(() => Runtime.ScriptSnapshot.Load(Root,Config),"oversized source");
        Source("return");
        string LimitDirectory = Path.Combine(Scripts, "modules", "limits");
        Directory.CreateDirectory(LimitDirectory);
        string MaximumSource = new string('a', 65536);
        for (int Index = 0; Index < 65; ++Index)
            File.WriteAllText(Path.Combine(LimitDirectory, "source" + Index.ToString("D3") + ".luau"), MaximumSource, Utf8);
        Reject(() => Runtime.ScriptSnapshot.Load(Root, Config), "aggregate source exceeds 4 MiB");
        Directory.Delete(LimitDirectory, true);
        Directory.CreateDirectory(LimitDirectory);
        for (int Index = 0; Index < 256; ++Index)
            File.WriteAllText(Path.Combine(LimitDirectory, "module" + Index.ToString("D3") + ".luau"), "return true", Utf8);
        Reject(() => Runtime.ScriptSnapshot.Load(Root, Config), "module count exceeds 256");
        Directory.Delete(LimitDirectory, true);
        Check(Marshal.SizeOf(typeof(Runtime.SchedulerInfo))==56,"scheduler ABI layout");
        string Outside = Path.Combine(Root, "outside"); Directory.CreateDirectory(Outside);
        File.WriteAllText(Path.Combine(Outside,"escape.luau"),"return 99",Utf8);
        string Link = Path.Combine(Scripts,"modules","linked");
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
            using (var Process = System.Diagnostics.Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + Link + "\" \"" + Outside + "\"") {
                UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true })) {
                Process.WaitForExit(); Check(Process.ExitCode==0,"junction fixture creation");
            }
        } else Check(symlink(Outside,Link)==0,"symlink fixture creation");
        Reject(() => Runtime.ScriptSnapshot.Load(Root,Config),"reparse escape rejected");
        // Nonrecursive removal deletes only this fixture link, not its target.
        Directory.Delete(Link);

        using (var Host = new Runtime.ScriptHost(Native, Config, () => Runtime.ScriptSnapshot.Load(Root,Config))) {
            Source("local V=require('util/value'); print(V.Value); task.defer(function() print('old callback') end)");
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK && Host.Ready,"valid entry");
            long Old = Host.Generation, SharedVm = Host.VmGenerationId;
            Check(SharedVm != 0 && Host.Status().Contains("VM generation: "+SharedVm),"distinct VM/domain identity reported");
            Source("print('discard'); task.defer(function() print('candidate leak') end); error('bad entry')");
            var Result=Host.Reload(); Check(Result.Status==Runtime.RuntimeStatus.RUNTIME_ERROR && Result.Logs=="" && Host.Generation==Old && Host.VmGenerationId==SharedVm,"runtime candidate rollback in same VM");
            Source("local ="); Check(Host.Reload().Status==Runtime.RuntimeStatus.COMPILE_ERROR && Host.Generation==Old && Host.VmGenerationId==SharedVm,"compile rollback in same VM");
            Check(Drain(Host)=="old callback\n","old queue preserved, candidate discarded");
            Source("assert(require('util/value').Value==42)"); Check(Host.Reload().Status==Runtime.RuntimeStatus.OK && Host.Generation!=Old && Host.VmGenerationId==SharedVm,"healthy root domain replacement keeps VM generation");
            File.WriteAllText(Path.Combine(Scripts,"modules","util","value.luau"),"return {Value=43}",Utf8);
            Check(Host.Execute("cached","return require('util/value').Value").Number==42,"active snapshot unaffected by disk change");
            Source("assert(require('util/value').Value==43)"); Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"new generation fresh module value");
            File.WriteAllText(Path.Combine(Scripts,"modules","util","value.luau"),"return {Value=42}",Utf8);
            Source("task.defer(function() error('isolated') end); task.defer(function() print('unrelated') end)");
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"failure fixture init");
            Check(Drain(Host)=="unrelated\n" && Host.Failed==1 && Host.Ready,"callback error isolation");
            Source("task.spawn(function() print('one'); task.defer(function() print('two') end) end)");
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"recursive queue");
            string First=""; foreach(var Item in Host.Drain()) First+=Item.Logs;
            Check(First=="one\n" && Host.HasWork,"drain captured boundary"); Check(Drain(Host)=="two\n","later drain");
            Source("for I=1,10 do task.defer(function() local X=0; for J=1,500000 do X+=J end; assert(X>0) end) end");
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"budget fixture");
            ulong Before=Host.Attempted; Host.Drain();
            Check(Host.Attempted-Before==1 && Host.HasWork && Host.BudgetOverruns>0 && Host.Timeouts==0 && Host.Failed==1,"frame budget stops after successful callback overrun, not timeout recovery");
            Source("return"); Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"clear budget queue");
            var Watch=Stopwatch.StartNew();
            ulong MinimumMemory=ulong.MaxValue, MaximumMemory=0;
            for(int Cycle=0; Cycle<100; ++Cycle) {
                Source("local V=require('util/value'); assert(V.Value==42); for I=1,1000 do task.delay(100,function() error('stale') end) end");
                Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"stress scheduled generation");
                Old=Host.Generation; Source("require('missing')");
                Check(Host.Reload().Status!=Runtime.RuntimeStatus.OK && Host.Generation==Old && Host.Status().Contains("Queued: 1000"),"stress rejected candidate queue intact");
                Source("assert(require('util/value').Value==42)"); var Replacement=Host.Reload(); Check(Replacement.Status==Runtime.RuntimeStatus.OK,"stress replacement: "+Replacement.Status+" "+Replacement.Error);
                Check(!Host.HasWork && Host.Status().Contains("modules: 1"),"fresh cache and cancelled callbacks");
                ulong Memory=ulong.Parse(Regex.Match(Host.Status(), @"VM bytes: (\d+)").Groups[1].Value);
                MinimumMemory=Math.Min(MinimumMemory,Memory); MaximumMemory=Math.Max(MaximumMemory,Memory);
            }
            Check(MaximumMemory-MinimumMemory<65536,"memory stable across reload samples");
            Console.WriteLine("[CarbonLuau:ScriptTest] VM bytes across 100 reload samples: "+MinimumMemory+".."+MaximumMemory);
            Console.WriteLine("[CarbonLuau:ScriptTest] 100 reload/cancel cycles (1000 callbacks each): "+Watch.Elapsed.TotalMilliseconds.ToString("F2")+" ms; "+Host.Status());
            Check(Host.Invalidated>=100000 && Native.LiveVmCount==1,"stale resources released");
        }
        // Recovery must reconstruct scripts once, then latch unavailable on repeat.
        var Tight = new Runtime.RuntimeConfig { MaxCallbackMilliseconds=3 };
        using(var Host=new Runtime.ScriptHost(Native,Tight,()=>Runtime.ScriptSnapshot.Load(Root,Tight))) {
            Source("print('reconstructed entry'); task.defer(function() while true do end end); task.delay(100,function() error('stale') end)");
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"runaway init");
            OldGeneration(Host);
            Check(Host.Recoveries==1 && Host.Timeouts==1 && Host.Ready && Host.HasWork,"first timeout reconstructs queued script state");
            Source("local ="); long Before=Host.Generation;
            Check(Host.Reload().Status==Runtime.RuntimeStatus.COMPILE_ERROR && Host.Generation==Before,"failed operator reload does not rearm");
            Host.Drain(); Check(!Host.Ready && Host.Recoveries==1 && Host.Timeouts==2 && Native.LiveVmCount==0,"second timeout latches unavailable");
            Check(Host.Drain().Count==0,"unavailable never retries");
            Source("print('operator restored')"); Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"operator rearm");
            Source("local ="); Host.Execute("failure","while true do end");
            Check(!Host.Ready && Host.Recoveries==2 && Native.LiveVmCount==0,"failed reconstruction leaves unavailable");
            Source("task.delay(100,function() error('after unload') end)"); Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"queued unload init");
            Host.Dispose(); Host.Dispose(); Check(!Host.HasWork && Native.LiveVmCount==0,"unload queued resources");
        }
        Console.WriteLine("[CarbonLuau:ScriptTest] PASS: real ABI; UTF8/path/reparse confinement; atomic queues; bounded drains; 100 x 1000 cancellation cycles; one-attempt recovery");
    }
    private static void OldGeneration(Runtime.ScriptHost Host) {
        long Before=Host.Generation; string Logs="";
        foreach(var Item in Host.Drain()) Logs+=Item.Logs;
        Check(Host.Generation>Before && Logs.Contains("reconstructed entry"),"recovery actually executes entrypoint");
    }
}
