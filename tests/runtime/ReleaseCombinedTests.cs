using System;
using System.Diagnostics;
using System.Threading;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    // Real compiler/VM, production SQLite worker and shared scheduler; Player
    // events are synthetic captures, not a Rust transition/client claim.
    internal static void PersistenceGameplaySaturation(Runtime.NativeRuntime Native, string Worker, string Directory)
    {
        var View = new Runtime.PlayerView { Identity = new object(), Connection = new object(),
            UserId = "76561190000999888", Name = "Release victim", Connected = true };
        using (var F = new Fixture(Native, Worker, Directory, PlayerLookup: Id => Id == View.UserId ? View : null)) {
            long Frame = 0;
            F.World.GameplayEvents.FrameClock = () => Frame;
            var Player = F.World.Players.Connect(View);
            F.Source = "local S=require('state'); S.Events=0; S.Completions=0; " +
                "local P=game:GetService('Players'); " +
                "for Index=1,128 do P.PlayerDied:Connect(function(V,C) " +
                "assert(V.UserId=='76561190000999888' and table.isfrozen(C)); S.Events+=1 end) end";
            F.Reload();
            for (int Cycle = 0; Cycle < 6; Cycle++) {
                if (Cycle != 0) F.Refill(); // Preserve canonical namespace submission rates.
                F.Execute("local S=require('state'); local Store=game:GetService('DataStoreService'):GetDataStore('ReleaseCombined'); " +
                    "Store:SetAsync('x'," + Cycle + ",function(OK,E) assert(OK and E==nil); " +
                    "Store:GetAsync('x',function(V,GE) assert(V==" + Cycle + " and GE==nil); " +
                    "Store:RemoveAsync('x',function(R,RE) assert(R==true and RE==nil); " +
                    "S.Completions+=1; print('combined-done-" + Cycle + "') end) end) end)");
                string Marker = "combined-done-" + Cycle;
                var Watch = Stopwatch.StartNew();
                while (!F.Logs.ToString().Contains(Marker) && Watch.ElapsedMilliseconds < 12000) {
                    Frame++;
                    for (int Capture = 0; Capture < 8; Capture++) {
                        if (F.World.GameplayEvents.Capture("died"))
                            F.World.GameplayEvent("died", Player.Token, Player.UserId, Player.Name, null);
                    }
                    F.Tick();
                    Check(F.World.GameplayEvents.PendingCount <= Runtime.GameplayEventPolicy.Reservations,
                        "release combined event retention bounded");
                    Thread.Sleep(5);
                }
                Check(F.Logs.ToString().Contains(Marker), "real durable completion progresses under event saturation");
            }
            F.Execute("local S=require('state'); assert(S.Completions==6 and S.Events>0)");
            Check(F.World.GameplayEvents.FanoutRejected > 0, "combined fixture actually saturates event admission");
            for (int Turn = 0; Turn < 128 && F.Host.HasWork; Turn++) { Frame++; F.Tick(); }
            Check(F.Queue.PendingCount == 0 && F.World.GameplayEvents.PendingCount == 0 &&
                F.World.GameplayEvents.RetainedBytes == 0, "combined callbacks and reservations converge");
            Console.WriteLine("[CarbonLuau:ReleaseCombined] PASS six real-worker Set/Get/Remove chains during saturated public PlayerDied; no replay, bounded shared scheduling and zero pending reservations");
        }
        Check(Native.LiveVmCount == 0 && Native.Storage == null, "combined full teardown");
    }
}
