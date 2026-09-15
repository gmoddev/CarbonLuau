using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const ulong Phase5UserId = 76561198000000005;
        private const string Phase5CommandName = "clphase5";
        private static readonly UTF8Encoding Phase5Utf8 = new UTF8Encoding(false);

        [ConsoleCommand("carbonluau.phase5fixture"), AuthLevel(2)]
        private void Phase5Fixture(ConsoleSystem.Arg Arg)
        {
            if (Host == null || !Host.Ready || BasePlayer.activePlayerList.Count != 0)
            {
                Arg.ReplyWith("Fixture requires a healthy isolated server with zero clients");
                return;
            }
            Arg.ReplyWith("CarbonLuau Phase5 fixture scheduled (controlled host objects, not a client connection)");
            NextFrame(RunPhase5Fixture);
        }

        [ConsoleCommand("carbonluau.phase5arm"), AuthLevel(2)]
        private void Phase5Arm(ConsoleSystem.Arg Arg)
        {
            try
            {
                BasePlayer Player = EnsurePhase5Player();
                LoadPhase5("local M=require('message'); local P=game:GetService('Players'); " +
                    "assert(P:GetPlayerByUserId('" + Phase5UserId.ToString(CultureInfo.InvariantCulture) + "').IsConnected); " +
                    "P.PlayerAdded:Connect(function(V) assert(V.IsConnected) end); " +
                    "P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected) end); " +
                    "game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end); " +
                    "task.delay(86400,function() error('must be cancelled on unload') end)");
                CheckPhase5(Gameplay.Players.Find(Player.UserIDString) != null, "armed player directory");
                Arg.ReplyWith("CarbonLuau Phase5 armed: modules/tasks/signals/command/player directory active");
            }
            catch (Exception Error) { PrintError("[CarbonLuau:Phase5Fixture] FAIL arm " + Error); }
        }

        [ConsoleCommand("carbonluau.phase5pulse"), AuthLevel(2)]
        private void Phase5Pulse(ConsoleSystem.Arg Arg)
        {
            try
            {
                BasePlayer Player = EnsurePhase5Player();
                string UserId = Player.UserIDString;
                for (int Index = 0; Index < 10; ++Index)
                {
                    OnPlayerDisconnected(Player, "phase5 pulse");
                    Player.net.connection = null;
                    DrainPhase5();
                    Player.net.connection = NewPhase5Connection(Player);
                    OnPlayerConnected(Player);
                    DrainPhase5();
                }
                LoadPhase5("local M=require('message'); local P=game:GetService('Players'); assert(P:GetPlayerByUserId('" + UserId + "').IsConnected); " +
                    "P.PlayerAdded:Connect(function(V) assert(V.IsConnected) end); P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected) end); " +
                    "game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end); task.defer(function() assert(M=='CarbonLuau Phase 2 module loading works') end)");
                DrainPhase5();
                CheckPhase5(InvokePhase5(Player, Phase5CommandName, new[] { "phase5-profile" }) == "", "pulse command dispatch");
                Arg.ReplyWith("CarbonLuau Phase5 pulse PASS; generation=" + Host.Generation);
            }
            catch (Exception Error) { PrintError("[CarbonLuau:Phase5Fixture] FAIL pulse " + Error); }
        }

        [ConsoleCommand("carbonluau.phase5cleanup"), AuthLevel(2)]
        private void Phase5Cleanup(ConsoleSystem.Arg Arg)
        {
            RemovePhase5Player();
            Arg.ReplyWith("CarbonLuau Phase5 controlled player removed");
        }

        private void RunPhase5Fixture()
        {
            BasePlayer Player = null;
            string Prefix = "[CarbonLuau:Phase5Fixture] ";
            try
            {
                Player = EnsurePhase5Player();
                string UserId = Player.UserIDString;
                string Stable = "local M=require('message'); assert(M=='CarbonLuau Phase 2 module loading works'); local P=game:GetService('Players'); " +
                    "assert(P:GetPlayerByUserId('" + UserId + "').IsConnected); " +
                    "P.PlayerAdded:Connect(function(V) assert(V.IsConnected) end); " +
                    "P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected) end); " +
                    "game:GetService('Commands'):Register('" + Phase5CommandName + "',{permission='carbonluau.phase5'},function(C) print(C.Arguments[1] or 'empty') end); " +
                    "task.defer(function() assert(game.ApiVersion=='0.3.0-experimental') end)";

                RunSuccessfulReloadSoak(Stable, Prefix);
                RunConnectionSoak(Player, UserId, Prefix);
                RunFailedReloadSoak(Prefix);
                RunMemoryPressure(Prefix);
                RunTimeoutQualification(Player, Prefix);
                LoadPhase5(Stable);
                DrainPhase5();
                CheckPhase5(Host.Ready && Native.LiveVmCount == 1, "final healthy runtime");
                Puts(Prefix + "PASS complete Phase 5 controlled-host fixture; " + Phase5Metrics());
            }
            catch (Exception Error) { PrintError(Prefix + "FAIL " + Error); }
            finally { RemovePhase5Player(); }
        }

        private void RunSuccessfulReloadSoak(string Stable, string Prefix)
        {
            var Latencies = new List<double>();
            var VmSamples = new List<ulong>();
            long WorkingBefore = Process.GetCurrentProcess().WorkingSet64;
            long PrivateBefore = Process.GetCurrentProcess().PrivateMemorySize64;
            long ManagedBefore = GC.GetTotalMemory(false);
            ulong InvalidatedBefore = Host.Invalidated;
            for (int Cycle = 1; Cycle <= 100; ++Cycle)
            {
                var Watch = Stopwatch.StartNew();
                LoadPhase5(Stable);
                Watch.Stop();
                Latencies.Add(Watch.Elapsed.TotalMilliseconds);
                CheckPhase5(Native.LiveVmCount == 1, "one live VM after reload " + Cycle);
                CheckPhase5(Gameplay.Active != null && Gameplay.Active.ListenerCount == 2 && Gameplay.Active.Commands.Count == 1, "stable facade registrations " + Cycle);
                CheckPhase5(Carbon.Community.Runtime.CommandManager.Chat.Count(Command => Command.Name == Phase5CommandName && Object.ReferenceEquals(Command.Reference, this)) == 1, "one host command " + Cycle);
                VmSamples.Add(ReadPhase5VmBytes());
            }
            DrainPhase5();
            CheckPhase5(!Host.HasWork, "only final generation task drains");
            CheckPhase5(Host.Invalidated >= InvalidatedBefore + 99, "old generation tasks invalidated");
            permission.RevokeUserPermission(Phase5UserId.ToString(CultureInfo.InvariantCulture), "carbonluau.phase5");
            CheckPhase5(InvokePhase5(BasePlayer.FindByID(Phase5UserId), Phase5CommandName, new[] { "denied" }) == "", "permission denial blocks Lua command entry");
            permission.GrantUserPermission(Phase5UserId.ToString(CultureInfo.InvariantCulture), "carbonluau.phase5", this);
            CheckPhase5(InvokePhase5(BasePlayer.FindByID(Phase5UserId), Phase5CommandName, new[] { "allowed" }) == "allowed\n", "permission grant admits Lua command entry");
            permission.RevokeUserPermission(Phase5UserId.ToString(CultureInfo.InvariantCulture), "carbonluau.phase5");
            Latencies.Sort();
            Puts(Prefix + "PASS 100 successful facade reloads; latency ms min/median/p95/max=" +
                Latencies[0].ToString("F3", CultureInfo.InvariantCulture) + "/" + Latencies[50].ToString("F3", CultureInfo.InvariantCulture) + "/" +
                Latencies[94].ToString("F3", CultureInfo.InvariantCulture) + "/" + Latencies[99].ToString("F3", CultureInfo.InvariantCulture) +
                "; VM bytes min/max=" + VmSamples.Min() + "/" + VmSamples.Max() +
                "; working/private/managed delta=" + (Process.GetCurrentProcess().WorkingSet64 - WorkingBefore) + "/" +
                (Process.GetCurrentProcess().PrivateMemorySize64 - PrivateBefore) + "/" + (GC.GetTotalMemory(false) - ManagedBefore));
        }

        private void RunConnectionSoak(BasePlayer Player, string UserId, string Prefix)
        {
            string Source = "local P=game:GetService('Players'); local Last=P:GetPlayers()[1]; local Added,Removed=0,0; " +
                "P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected and not Last.IsConnected); Removed+=1 end); " +
                "P.PlayerAdded:Connect(function(V) assert(V.IsConnected and not Last.IsConnected and V~=Last); Last=V; Added+=1 end); " +
                "game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function(C) assert(C.Player.UserId=='" + UserId + "'); " +
                "assert(not C.Player:HasPermission('carbonluau.phase5')); C.Player:SendMessage('phase5 controlled-host message'); print(Added..'/'..Removed) end)";
            LoadPhase5(Source);
            for (int Cycle = 1; Cycle <= 1000; ++Cycle)
            {
                var Old = Gameplay.Players.Find(UserId);
                CheckPhase5(Old != null, "old connection exists " + Cycle);
                OnPlayerDisconnected(Player, "phase5 connection soak");
                Player.net.connection = null;
                CheckPhase5(DrainPhase5() == "", "removal callback " + Cycle);
                CheckPhase5(Gameplay.Players.Find(UserId) == null && Gameplay.Players.Resolve(Old.Token, UserId) == null, "old proxy latched invalid " + Cycle);
                Player.net.connection = NewPhase5Connection(Player);
                OnPlayerConnected(Player);
                CheckPhase5(DrainPhase5() == "", "addition callback " + Cycle);
                var Current = Gameplay.Players.Find(UserId);
                CheckPhase5(Current != null && Current.Token != Old.Token && Gameplay.Players.Resolve(Old.Token, UserId) == null, "new lifetime cannot retarget old " + Cycle);
            }
            CheckPhase5(InvokePhase5(Player, Phase5CommandName, new string[0]) == "1000/1000\n", "Lua observed every connection transition");
            CheckPhase5(BasePlayer.activePlayerList.Count == 1 && Gameplay.Players.Find(UserId) != null && !Host.HasWork, "bounded steady-state directory");
            Puts(Prefix + "PASS 1000 controlled-host disconnect/reconnect cycles; no authenticated client/session claim");
        }

        private void RunFailedReloadSoak(string Prefix)
        {
            string ModuleDirectory = Path.Combine(Oxide.Core.Interface.Oxide.DataDirectory, "CarbonLuau", Settings.ScriptRoot, Settings.ModuleRoot);
            var Files = new Dictionary<string, string> {
                {"phase5_badsyntax.luau", "local ="}, {"phase5_badruntime.luau", "error('phase5 module runtime')"},
                {"phase5_cycle_a.luau", "return require('phase5_cycle_b')"}, {"phase5_cycle_b.luau", "return require('phase5_cycle_a')"}
            };
            foreach (var FileValue in Files) File.WriteAllText(Path.Combine(ModuleDirectory, FileValue.Key), FileValue.Value, Phase5Utf8);
            try
            {
                string Healthy = "local P=game:GetService('Players'); P.PlayerAdded:Connect(function() end); " +
                    "game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end); task.defer(function() print('healthy queued') end)";
                LoadPhase5(Healthy);
                var Active = Gameplay.Active;
                var Command = Carbon.Community.Runtime.CommandManager.Find(Phase5CommandName);
                string[] Failures = {
                    "local =", "error('entry runtime')", "require('phase5_badsyntax')", "require('phase5_badruntime')", "require('phase5_cycle_a')",
                    "for I=1,129 do game:GetService('Players').PlayerAdded:Connect(function() end) end",
                    "game:GetService('Commands'):Register('Bad',{},function() end)",
                    "game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end); game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end)",
                    "local P=game:GetService('Players'):GetPlayers()[1]; assert(not pcall(function() P:SendMessage('provisional') end)); error('reject')"
                };
                int Attempts = 0;
                for (int Round = 0; Round < 12; ++Round) foreach (string Failure in Failures)
                {
                    long Generation = Host.Generation;
                    var Result = Host.Reload(Failure);
                    Attempts++;
                    CheckPhase5(Result.Status != RuntimeStatus.OK && Host.Generation == Generation, "failed candidate preserves generation");
                    CheckPhase5(Object.ReferenceEquals(Gameplay.Active, Active) && Object.ReferenceEquals(Carbon.Community.Runtime.CommandManager.Find(Phase5CommandName), Command), "failed candidate preserves facade publication");
                    CheckPhase5(Native.LiveVmCount == 1, "failed candidate VM retired");
                }
                CheckPhase5(DrainPhase5() == "healthy queued\n", "healthy queued work survives failed candidates");
                Puts(Prefix + "PASS " + Attempts + " failed reloads across entry/module/cycle/resource/facade failures; active generation preserved; " + Phase5Metrics());
            }
            finally { foreach (string Name in Files.Keys) File.Delete(Path.Combine(ModuleDirectory, Name)); }
        }

        private void RunMemoryPressure(string Prefix)
        {
            LoadPhase5("game:GetService('Players').PlayerAdded:Connect(function() end); game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end)");
            long Generation = Host.Generation;
            for (int Attempt = 0; Attempt < 16; ++Attempt)
            {
                var Result = Host.Execute("phase5.memory", "return buffer.create(67108865)");
                CheckPhase5(Result.Status == RuntimeStatus.MEMORY_LIMIT && Host.Ready && Host.Generation == Generation && Native.LiveVmCount == 1, "controlled repeated VM allocation failure " + Attempt);
            }
            var LogResult = Host.Execute("phase5.log", "for I=1,20 do print(string.rep('x',256)) end");
            CheckPhase5(LogResult.Status == RuntimeStatus.OK && LogResult.LogTruncated && LogResult.Logs.Length <= 4096, "bounded logging");
            ulong CancelledBefore = Host.Cancelled;
            double Accepted = 0;
            for (int Batch = 0; Batch < 10; ++Batch)
            {
                var Saturation = Host.Execute("phase5.scheduler", "local Accepted=0; for I=1,500 do if pcall(task.delay,86400,function() end) then Accepted+=1 end end; return Accepted");
                CheckPhase5(Saturation.Status == RuntimeStatus.OK && Saturation.HasNumber, "bounded scheduler saturation batch " + Batch);
                Accepted += Saturation.Number;
            }
            CheckPhase5(Accepted == Settings.MaxQueuedCallbacks, "scheduler saturation capacity");
            LoadPhase5("return");
            CheckPhase5(Host.Cancelled >= CancelledBefore + (ulong)Settings.MaxQueuedCallbacks && !Host.HasWork, "saturated queue cancelled on replacement");
            LoadPhase5("game:GetService('Players').PlayerAdded:Connect(function() end)");
            var Lifetime = Gameplay.Players.Find(Phase5UserId.ToString(CultureInfo.InvariantCulture));
            ulong RejectedBefore = Gameplay.Active.Rejected;
            for (int Index = 0; Index < 1000; ++Index) Gameplay.Event("added", Lifetime);
            CheckPhase5(Gameplay.Active.PendingCount == 256 && Gameplay.Active.Rejected == RejectedBefore + 744, "managed event intake bound");
            LoadPhase5("return");
            CheckPhase5(!Host.HasWork, "bounded facade intake cancelled on replacement");
            Generation = Host.Generation;
            CheckPhase5(Host.Reload("return buffer.create(67108865)").Status == RuntimeStatus.MEMORY_LIMIT && Host.Generation == Generation && Native.LiveVmCount == 1, "candidate memory failure preserves generation");
            Puts(Prefix + "PASS memory pressure: 16 repeated VM cap failures, bounded logs, scheduler/event saturation, candidate cleanup; " + Phase5Metrics());
        }

        private void RunTimeoutQualification(BasePlayer Player, string Prefix)
        {
            string UserId = Player.UserIDString;
            LoadPhase5("return");
            long Generation = Host.Generation;
            CheckPhase5(Host.Reload("while true do end").Status == RuntimeStatus.TIMEOUT && Host.Generation == Generation && Host.Ready, "candidate entry timeout preserves active generation");

            LoadPhase5("task.defer(function() while true do end end); task.defer(function() print('stale scheduled work') end)");
            ulong Recoveries = Host.Recoveries, Timeouts = Host.Timeouts;
            string Logs = DrainPhase5();
            CheckPhase5(Host.Ready && Host.Recoveries == Recoveries + 1 && Host.Timeouts == Timeouts + 1 && !Logs.Contains("stale scheduled work"), "scheduled timeout reconstructs once without queue transfer");
            Host.Execute("phase5.timeout.second", "while true do end");
            CheckPhase5(!Host.Ready && Host.Recoveries == Recoveries + 1 && Host.Timeouts == Timeouts + 2 && Gameplay.Active == null, "second timeout latches unavailable");
            CheckPhase5(Host.Reload().Status == RuntimeStatus.OK && Host.Ready, "operator reload rearms after scheduled timeout");

            LoadPhase5("game:GetService('Players').PlayerAdded:Connect(function() while true do end end); game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() end)");
            var OldSignal = Gameplay.Active;
            Gameplay.Event("added", Gameplay.Players.Find(UserId));
            Recoveries = Host.Recoveries; Timeouts = Host.Timeouts;
            DrainPhase5();
            CheckPhase5(Host.Ready && Host.Recoveries == Recoveries + 1 && Host.Timeouts == Timeouts + 1 && OldSignal.Disposed, "Signal timeout retires stale session and reconstructs once");
            Host.Execute("phase5.timeout.second", "while true do end");
            CheckPhase5(!Host.Ready && Gameplay.Active == null, "Signal second retirement removes facade");
            CheckPhase5(Host.Reload().Status == RuntimeStatus.OK, "operator reload after Signal timeout");

            LoadPhase5("game:GetService('Commands'):Register('" + Phase5CommandName + "',{},function() while true do end end)");
            var OldCommand = Gameplay.Active;
            Recoveries = Host.Recoveries; Timeouts = Host.Timeouts;
            CheckPhase5(OldCommand.Invoke(Phase5CommandName, UserId, new string[0]), "command timeout admitted");
            DrainPhase5();
            CheckPhase5(Host.Ready && Host.Recoveries == Recoveries + 1 && Host.Timeouts == Timeouts + 1 && !OldCommand.Invoke(Phase5CommandName, UserId, new string[0]), "command timeout retires stale command and reconstructs once");
            Host.Execute("phase5.timeout.second", "while true do end");
            CheckPhase5(!Host.Ready && Gameplay.Active == null, "command second retirement unavailable");
            CheckPhase5(Host.Reload().Status == RuntimeStatus.OK && Host.Ready, "operator reload restores valid runtime");
            Puts(Prefix + "PASS entry/scheduled/Signal/command timeout, one recovery, second retirement, stale rejection and operator rearm; " + Phase5Metrics());
        }

        private BasePlayer EnsurePhase5Player()
        {
            BasePlayer Player = BasePlayer.FindByID(Phase5UserId);
            if (Player == null)
            {
                Player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new UnityEngine.Vector3(0, -100, 0)) as BasePlayer;
                CheckPhase5(Player != null, "create controlled BasePlayer");
                Player.userID = Phase5UserId;
                Player.UserIDString = Phase5UserId.ToString(CultureInfo.InvariantCulture);
                Player.displayName = "CarbonLuau Phase5 controlled fixture";
                Player.Spawn();
                BasePlayer.activePlayerLookup[Phase5UserId] = Player;
                BasePlayer.activePlayerList.Add(Player);
            }
            if (Player.net.connection == null)
            {
                Player.net.connection = NewPhase5Connection(Player);
                OnPlayerConnected(Player);
            }
            return Player;
        }

        private Network.Connection NewPhase5Connection(BasePlayer Player)
        {
            return new Network.Connection { userid = Phase5UserId, username = Player.displayName, connected = true, active = true, player = Player };
        }

        private void RemovePhase5Player()
        {
            BasePlayer Player = BasePlayer.FindByID(Phase5UserId);
            if (Player == null) return;
            try { OnPlayerDisconnected(Player, "phase5 cleanup"); DrainPhase5(); } catch { }
            if (Gameplay != null) Gameplay.Players.Disconnect(Player.UserIDString, Player);
            Player.net.connection = null;
            BasePlayer.activePlayerLookup.Remove(Phase5UserId);
            BasePlayer.activePlayerList.Remove(Player);
            Player.Kill();
        }

        private void LoadPhase5(string Source)
        {
            var Result = Host.Reload(Source);
            CheckPhase5(Result.Status == RuntimeStatus.OK, "load: " + Result.Status + " " + Result.Error);
            RegisterActivePermissions();
        }

        private string DrainPhase5()
        {
            string Logs = "";
            for (int Frame = 0; Frame < 300; ++Frame)
            {
                var Results = Host.Drain();
                foreach (var Result in Results)
                {
                    Logs += Result.Logs;
                    CheckPhase5(Result.Status == RuntimeStatus.OK || Result.Status == RuntimeStatus.TIMEOUT, "unexpected callback status " + Result.Status + ": " + Result.Error);
                }
                if (Results.Count == 0 || !Host.HasWork) break;
            }
            RegisterActivePermissions();
            return Logs;
        }

        private string InvokePhase5(BasePlayer Player, string Name, string[] Arguments)
        {
            var Command = Carbon.Community.Runtime.CommandManager.Find(Name);
            CheckPhase5(Command != null, "host command exists: " + Name);
            CheckPhase5(Carbon.Community.Runtime.CommandManager.Execute(Command, new API.Commands.PlayerArgs { Player = Player, Arguments = Arguments.Cast<object>().ToArray() }), "host command dispatch: " + Name);
            return DrainPhase5();
        }

        private ulong ReadPhase5VmBytes()
        {
            Match Value = Regex.Match(Host.Status(), @"VM bytes: (\d+)");
            CheckPhase5(Value.Success, "status VM bytes");
            return ulong.Parse(Value.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private string Phase5Metrics()
        {
            var ProcessValue = Process.GetCurrentProcess();
            return "working/private/managed/VM=" + ProcessValue.WorkingSet64 + "/" + ProcessValue.PrivateMemorySize64 + "/" + GC.GetTotalMemory(false) + "/" + (Host != null && Host.Ready ? ReadPhase5VmBytes() : 0) +
                "; generation=" + (Host == null ? 0 : Host.Generation) + "; liveVMs=" + (Native == null ? 0 : Native.LiveVmCount);
        }

        private static void CheckPhase5(bool Value, string Message)
        {
            if (!Value) throw new InvalidOperationException(Message);
        }
    }
}
