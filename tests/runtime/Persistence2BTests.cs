using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    private static void HintPublicationIntegration(Runtime.NativeRuntime Native, string Worker, string Parent)
    {
        using (var F = new Fixture(Native, Worker, Path.Combine(Parent, "hints"), Initialize: false)) {
            F.Source = "game:GetService('DataStoreService'):GetDataStore('State',{Indexes={'Coins'}}); return true";
            F.Reload();
            Check(F.Worker.RequestsSent == 0 && F.Queue.PendingCount == 0,
                "candidate hint has no synchronous persistence request");
            var Watch = Stopwatch.StartNew();
            while (F.Worker.RequestsSent < 1 && Watch.ElapsedMilliseconds < 10000) {
                F.Tick(); Thread.Sleep(5);
            }
            Check(F.Worker.RequestsSent == 1 && F.Queue.PendingCount == 0,
                "published hint submits one private demand to the real worker");
            F.Source = "game:GetService('DataStoreService'):GetDataStore('Failed',{Indexes={'Never'}}); error('candidate')";
            var Failed = F.Host.Reload();
            Check(Failed.Status == Runtime.RuntimeStatus.RUNTIME_ERROR,
                "failed candidate rejected");
            for (int Index = 0; Index < 5; ++Index) F.Tick();
            Check(F.Worker.RequestsSent == 1,
                "failed candidate dispatches no private demand");
            F.Source = "game:GetService('DataStoreService'):GetDataStore('State',{Indexes={'Coins','Level'}}); return true";
            F.Reload();
            Watch.Restart();
            while (F.Worker.RequestsSent < 3 && Watch.ElapsedMilliseconds < 10000) {
                F.Tick(); Thread.Sleep(5);
            }
            Check(F.Worker.RequestsSent == 3,
                "replacement reasserts exact desired field union without replaying failed intent");
        }
        Console.WriteLine("[CarbonLuau:Persistence2B] real VM publication -> managed intent -> worker demand, failed replacement isolation PASS");
    }
}
