using System;
using System.Collections.Generic;
using System.Linq;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Included only in the isolated worker test package, never production.
        [ConsoleCommand("carbonluau.phase3fixture"), AuthLevel(2)]
        private void Phase3Fixture(ConsoleSystem.Arg Arg)
        {
            if (BasePlayer.activePlayerList.Count != 0 || Host == null || !Host.Ready) { Arg.ReplyWith("Fixture requires healthy isolated server with zero clients"); return; }
            Arg.ReplyWith("CarbonLuau Phase3 fixture scheduled (controlled host objects, not a client connection)");
            NextFrame(RunPhase3Fixture);
        }
        private void RunPhase3Fixture()
        {
            const ulong Id = 76561198000000001;
            string UserId = Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            BasePlayer Player = null;
            BaseEntity Parent = null;
            BaseMountable Mount = null;
            API.Commands.Command Foreign = null;
            string Prefix = "[CarbonLuau:HostFixture] ";
            Action<bool, string> Check = (Value, Message) => { if (!Value) throw new InvalidOperationException(Message); };
            Func<string> Drain = () => {
                string Logs = "";
                for (int Frame = 0; Frame < 200 && Host.HasWork; ++Frame)
                    foreach (var Result in Host.Drain()) {
                        Logs += Result.Logs;
                        if (Result.Status != RuntimeStatus.OK) Puts(Prefix + "callback diagnostic: " + Result.Status + "; " + Result.Error);
                    }
                RegisterActivePermissions(); return Logs;
            };
            Action<string> Load = Source => {
                var Result = Host.Reload(Source);
                Check(Result.Status == RuntimeStatus.OK, "load: " + Result.Status + " " + Result.Error);
                RegisterActivePermissions();
            };
            try {
                Check(BasePlayer.activePlayerLookup.Count == 0, "fixture requires empty player lookup");
                Player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new UnityEngine.Vector3(0, -100, 0)) as BasePlayer;
                Check(Player != null, "create real BasePlayer entity");
                Player.userID = Id; Player.UserIDString = UserId; Player.displayName = "CarbonLuau controlled fixture";
                Player.Spawn(); Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.net.connection = new Network.Connection { userid = Id, username = Player.displayName, connected = true, active = true, player = Player };
                BasePlayer.activePlayerLookup[Id] = Player;
                BasePlayer.activePlayerList.Add(Player);
                Player.OverrideMaxHealth(250f, false, false); Player.health = 190.25f;
                Player.OverrideMaxHealth(145.5f, false, false);
                Item ScrapMainA = ItemManager.CreateByName("scrap", 40), ScrapMainB = ItemManager.CreateByName("scrap", 60), ScrapBelt = ItemManager.CreateByName("scrap", 20);
                Item HoodieMain = ItemManager.CreateByName("hoodie", 1), HoodieBelt = ItemManager.CreateByName("hoodie", 1), HoodieWear = ItemManager.CreateByName("hoodie", 1);
                Check(ScrapMainA != null && ScrapMainB != null && ScrapBelt != null && HoodieMain != null && HoodieBelt != null && HoodieWear != null,
                    "create exact-build inventory fixture items");
                Check(ScrapMainA.MoveToContainer(Player.inventory.containerMain, 0, false) && ScrapMainB.MoveToContainer(Player.inventory.containerMain, 1, false) &&
                    ScrapBelt.MoveToContainer(Player.inventory.containerBelt, 0, false) && HoodieMain.MoveToContainer(Player.inventory.containerMain, 2, false) &&
                    HoodieBelt.MoveToContainer(Player.inventory.containerBelt, 1, false) && HoodieWear.MoveToContainer(Player.inventory.containerWear, 0, false),
                    "place physical main/belt/wear fixture stacks");
                string Script = "local P=game:GetService('Players'); local I=game:GetService('Items'); local C=game:GetService('Commands'); assert(I:Exists('scrap') and I:Exists('hoodie') and not I:Exists('carbonluau.unknown')); P.PlayerAdded:Connect(function(V) assert(V.UserId=='"+UserId+"' and V.IsConnected and V.Health==190.25 and V.MaxHealth==145.5 and V:CountItem('scrap')==120 and V:CountItem('hoodie')==3 and V:HasItem('scrap',120)); V:SendMessage('welcome fixture'); print('joined') end); P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected); assert(not pcall(function() return V.Health end)); assert(not pcall(function() return V.MaxHealth end)); assert(not pcall(function() return V:CountItem('scrap') end)); assert(not pcall(function() return V:HasItem('scrap') end)); assert(not pcall(function() V:SendMessage('stale') end)); print('removed') end); C:Register('hello',{permission='carbonluau.example.hello'},function(Ctx) assert(Ctx.Player.UserId=='"+UserId+"'); Ctx.Player:SendMessage('hello fixture'); print('A '..(Ctx.Arguments[1] or 'empty')) end)";
                Load(Script);
                OnPlayerConnected(Player);
                Check(Drain() == "joined\n", "real host hook/proxy/ChatMessage path");
                Puts(Prefix + "PASS real BasePlayer + Network.Connection; future join; bounded ChatMessage path returned (no connected client or delivery claim)");
                var InventoryRead = Host.Execute("player1c.live", "local P=game:GetService('Players'):GetPlayers()[1]; local I=game:GetService('Items'); assert(I:Exists('wood') and I:Exists('rifle.ak') and not I:Exists('carbonluau.unknown')); assert(P:CountItem('scrap')==120 and P:CountItem('hoodie')==3 and P:CountItem('carbonluau.unknown')==0); assert(P:HasItem('scrap') and P:HasItem('scrap',120) and not P:HasItem('scrap',121))");
                Check(InventoryRead.Status == RuntimeStatus.OK, "live main/belt/wear physical inventory read: " + InventoryRead.Error);
                ScrapMainA.amount = 41;
                InventoryRead = Host.Execute("player1c.changed", "local P=game:GetService('Players'):GetPlayers()[1]; assert(P:CountItem('scrap')==121 and P:HasItem('scrap',121))");
                Check(InventoryRead.Status == RuntimeStatus.OK, "live inventory change between reads: " + InventoryRead.Error);
                Puts(Prefix + "PASS live Items lookup and bounded physical main/belt/wear inventory reads, multiple stacks and changing quantity");
                Player.OverrideMaxHealth(212.75f, false, false); Player.health = 0.125f;
                var Vitals = Host.Execute("player1b.live", "local P=game:GetService('Players'):GetPlayers()[1]; assert(P.Health==0.125 and P.MaxHealth==212.75)");
                Check(Vitals.Status == RuntimeStatus.OK, "dynamic live Health/MaxHealth read: " + Vitals.Error);
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
                Vitals = Host.Execute("player1b.states", "local P=game:GetService('Players'):GetPlayers()[1]; assert(P.Health==0.125 and P.MaxHealth==212.75 and P:CountItem('scrap')==121)");
                Check(Vitals.Status == RuntimeStatus.OK, "sleeping/wounded live Health/MaxHealth read: " + Vitals.Error);
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, false);
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.health = 190.25f; Player.OverrideMaxHealth(145.5f, false, false);
                Check(Player.IsAlive(), "controlled Player is alive before Teleport; state=" + Player.lifestate);
                var Teleport = Host.Execute("player1d.live.first", "local P=game:GetService('Players'):GetPlayers()[1]; assert(select('#',P:Teleport(Vector3.new(25.5,500.25,40.125)))==0); assert(P.Position==Vector3.new(25.5,500.25,40.125))");
                Check(Teleport.Status == RuntimeStatus.OK && Player.transform.position == new UnityEngine.Vector3(25.5f, 500.25f, 40.125f),
                    "live first exact-coordinate teleport: " + Teleport.Error);
                Check(Player.IsAlive(), "controlled Player remains alive after first Teleport");
                Teleport = Host.Execute("player1d.live.far", "local P=game:GetService('Players'):GetPlayers()[1]; P:Teleport(Vector3.new(-400.5,500.5,400.75)); assert(P.Position==Vector3.new(-400.5,500.5,400.75))");
                Check(Teleport.Status == RuntimeStatus.OK && Player.transform.position == new UnityEngine.Vector3(-400.5f, 500.5f, 400.75f),
                    "live long-distance exact-coordinate teleport: " + Teleport.Error);
                Check(Player.IsAlive(), "controlled Player remains alive after long-distance Teleport");
                Teleport = Host.Execute("player1d.live.repeat", "local P=game:GetService('Players'):GetPlayers()[1]; P:Teleport(Vector3.new(26,500,41)); assert(P.Position==Vector3.new(26,500,41))");
                Check(Teleport.Status == RuntimeStatus.OK && Player.transform.position == new UnityEngine.Vector3(26f, 500f, 41f),
                    "live repeated exact-coordinate teleport: " + Teleport.Error);
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                Teleport = Host.Execute("player1d.sleeping", "local P=game:GetService('Players'):GetPlayers()[1]; P:Teleport(Vector3.new(27,501,42)); assert(P.Position==Vector3.new(27,501,42))");
                Check(Teleport.Status == RuntimeStatus.OK && Player.IsSleeping(), "sleeping teleport preserves sleeping state");
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, false);
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
                Teleport = Host.Execute("player1d.wounded", "local P=game:GetService('Players'):GetPlayers()[1]; assert(not pcall(function() P:Teleport(Vector3.new(1,2,3)) end))");
                Check(Teleport.Status == RuntimeStatus.OK && Player.transform.position == new UnityEngine.Vector3(27f, 501f, 42f),
                    "wounded teleport rejects before positional mutation");
                Player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                Parent = GameManager.server.CreateEntity("assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab", Player.transform.position);
                Check(Parent != null, "create controlled parent fixture"); Parent.Spawn(); Player.SetParent(Parent, true, true);
                Check(Player.HasParent(), "controlled player parent fixture attached");
                Teleport = Host.Execute("player1d.parented", "local P=game:GetService('Players'):GetPlayers()[1]; P:Teleport(Vector3.new(28,502,43))");
                Check(Teleport.Status == RuntimeStatus.OK && !Player.HasParent() && Player.transform.position == new UnityEngine.Vector3(28f, 502f, 43f),
                    "parented teleport detaches and relocates");
                Mount = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", Player.transform.position) as BaseMountable;
                if (Mount != null) {
                    Mount.Spawn(); Mount.MountPlayer(Player);
                    if (Player.GetMounted() != null) {
                        Teleport = Host.Execute("player1d.mounted", "local P=game:GetService('Players'):GetPlayers()[1]; P:Teleport(Vector3.new(29,503,44))");
                        Check(Teleport.Status == RuntimeStatus.OK && Player.GetMounted() == null && !Player.HasParent() &&
                            Player.transform.position == new UnityEngine.Vector3(29f, 503f, 44f), "mounted teleport uses bounded dismount and relocation");
                    } else Puts(Prefix + "SKIP controlled chair did not accept synthetic mount");
                } else Puts(Prefix + "SKIP target build chair prefab unavailable");
                Puts(Prefix + "PASS live server exact/repeated/sleeping/parented Teleport adapter; no authenticated-client convergence claim");
                Player.health = 0f; Player.lifestate = BaseCombatEntity.LifeState.Dead;
                Check(Player.IsDead(), "controlled Player entered dead-but-host-valid state");
                Vitals = Host.Execute("player1b.dead", "local P=game:GetService('Players'):GetPlayers()[1]; assert(P.Health==0 and P.MaxHealth==145.5 and P:CountItem('scrap')==121 and P:HasItem('hoodie',3)); assert(not pcall(function() P:Teleport(Vector3.new(1,2,3)) end))");
                Check(Vitals.Status == RuntimeStatus.OK, "dead-but-host-valid live reads and Teleport rejection: " + Vitals.Error);
                Player.lifestate = BaseCombatEntity.LifeState.Alive;
                Player.OverrideMaxHealth(212.75f, false, false); Player.health = 190.25f;
                Puts(Prefix + "PASS live Health/MaxHealth direct reads, dynamic maximum, no clamp and sleeping/wounded/dead host-valid states");
                var Take = Host.Execute("player1fa.live", "local P=game:GetService('Players'):GetPlayers()[1]; assert(P:CountItem('scrap')==121 and P:CountItem('hoodie')==3); assert(P:TakeItem('scrap',45) and P:CountItem('scrap')==76); assert(P:TakeItem('hoodie',3) and P:CountItem('hoodie')==0); assert(P:TakeItem('scrap',76) and P:CountItem('scrap')==0); assert(not P:TakeItem('scrap',1)); assert(not P:TakeItem('carbonluau.unknown',1))");
                Check(Take.Status == RuntimeStatus.OK, "live TakeItem partial/full/multiple/main-belt-wear verification: " + Take.Error);
                Puts(Prefix + "PASS live TakeItem PREPARE/COMMIT/VERIFY across partial/full main-belt-wear stacks and insufficient/unknown rejection");
                var Manager = Carbon.Community.Runtime.CommandManager;
                Func<string, string[], string> Invoke = (Name, Arguments) => {
                    var Command = Manager.Find(Name); Check(Command != null, "host command exists");
                    Check(Manager.Execute(Command, new API.Commands.PlayerArgs {Player = Player, Arguments = Arguments.Cast<object>().ToArray()}), "Carbon command dispatch");
                    return Drain();
                };
                permission.RevokeUserPermission(UserId, "carbonluau.example.hello");
                Check(Invoke("hello", new[]{"denied"}) == "", "host permission denial");
                permission.GrantUserPermission(UserId, "carbonluau.example.hello", this);
                Check(Invoke("hello", new[]{"allowed"}) == "A allowed\n", "host permission grant permits script callback");
                Puts(Prefix + "PASS actual Carbon command dispatch and permission denial/grant");
                var A = Manager.Find("hello"); var Late = A.Callback;
                var Failed = Host.Reload(Script.Replace("'A '","'B '") + "; error('B fails')");
                Check(Failed.Status != RuntimeStatus.OK && Object.ReferenceEquals(Manager.Find("hello"), A), "B never externally published");
                Check(Invoke("hello", new[]{"retained"}) == "A retained\n", "A retained after B");
                Load(Script.Replace("'A '","'C '"));
                Late(new API.Commands.PlayerArgs {Player = Player, Arguments = new object[]{"late"}});
                Check(Drain() == "", "late selected A callback rejected");
                Check(Invoke("hello", new[]{"active"}) == "C active\n", "C active after commit");
                Check(!Host.HasWork, "reload does not synthesize joined events");
                Puts(Prefix + "PASS A -> rejected B -> committed C; late selected A rejected; no synthetic joins");
                Foreign = new API.Commands.Command.Chat {Name = "clforeign", Reference = this, Token = new object(), Callback = Args => { }};
                string Reason; Check(Manager.RegisterCommand(Foreign, out Reason), "install owned collision fixture");
                var BeforeCollision = Manager.Find("hello");
                Check(Host.Reload("game:GetService('Commands'):Register('clforeign',{},function() end)").Status != RuntimeStatus.OK && Object.ReferenceEquals(Manager.Find("hello"), BeforeCollision), "foreign collision preserves active state");
                // Return the synthetic no-client fixture to its original stable
                // host position before exercising unrelated lifecycle checks.
                Player.SetServerFall(false); Player.MovePosition(new UnityEngine.Vector3(0f, -100f, 0f));
                Player.lifestate = BaseCombatEntity.LifeState.Alive; Player.OverrideMaxHealth(212.75f, false, false); Player.health = 190.25f;
                Load("local P=game:GetService('Players'); local Old=P:GetPlayers()[1]; assert(Old.IsConnected,'connected'); assert(Old.Health==190.25,'health '..tostring(Old.Health)); assert(Old.MaxHealth==212.75,'max '..tostring(Old.MaxHealth)); assert(Old:CountItem('scrap')==0,'items'); assert(not pcall(function() Old:SendMessage('provisional') end),'send provisional'); assert(not pcall(function() Old:Teleport(Vector3.new(1,2,3)) end),'teleport provisional'); assert(not pcall(function() Old:TakeItem('scrap',1) end),'take provisional'); P.PlayerRemoving:Connect(function(V) assert(not Old.IsConnected and not V.IsConnected); assert(not pcall(function() return Old.Health end)); assert(not pcall(function() return Old.MaxHealth end)); assert(not pcall(function() return Old:CountItem('scrap') end)); assert(not pcall(function() Old:TakeItem('scrap',1) end)); assert(not pcall(function() Old:Teleport(Vector3.new(1,2,3)) end)); print('old invalid') end); P.PlayerAdded:Connect(function(V) assert(not Old.IsConnected and V.IsConnected and Old~=V and V.Health==190.25 and V.MaxHealth==212.75 and V:CountItem('scrap')==0 and not V:TakeItem('scrap',1)); print('new lifetime') end)");
                OnPlayerDisconnected(Player, "controlled fixture");
                Player.net.connection = null;
                Check(Drain() == "old invalid\n", "disconnect invalidates retained proxy");
                Player.net.connection = new Network.Connection {userid = Id, connected = true, active = true, player = Player};
                OnPlayerConnected(Player);
                Check(Drain() == "new lifetime\n", "same BasePlayer and account, new connection lifetime");
                Load("local P=game:GetService('Players'); P.PlayerAdded:Connect(function() error('controlled listener failure') end); P.PlayerAdded:Connect(function() print('unrelated survived') end)");
                Gameplay.Event("added", Gameplay.Players.Find(UserId));
                Check(Drain() == "unrelated survived\n", "listener error isolation");
                Load("game:GetService('Commands'):Register('hello',{},function() while true do end end)");
                string EntryPath = System.IO.Path.Combine(Oxide.Core.Interface.Oxide.DataDirectory, "CarbonLuau", Settings.ScriptRoot, Settings.EntryScript);
                string OriginalSource = System.IO.File.ReadAllText(EntryPath);
                try {
                    System.IO.File.WriteAllText(EntryPath, "game:GetService('Commands'):Register('hello',{},function() while true do end end)", new System.Text.UTF8Encoding(false));
                    ulong RecoveriesBefore=Host.Recoveries, TimeoutsBefore=Host.Timeouts;
                    Invoke("hello", new string[0]);
                    Check(Host.Ready && Host.Recoveries == RecoveriesBefore+1 && Host.Timeouts == TimeoutsBefore+1, "command timeout reconstructs once");
                    Invoke("hello", new string[0]);
                    Check(!Host.Ready && Manager.Find("hello")==null, "second timeout unavailable and commands removed");
                } finally { System.IO.File.WriteAllText(EntryPath, OriginalSource, new System.Text.UTF8Encoding(false)); }
                Load(Script);
                for (int Cycle=0; Cycle<100; ++Cycle) Load(Script);
                Check(Invoke("hello",new[]{"stress"})=="A stress\n", "100 live command replacements");
                Puts(Prefix + "PASS reconnect, D10, listener errors, command timeout/recovery, 100 live registration replacements");
                Load("game:GetService('Players').PlayerAdded:Connect(function(P) print('production NextFrame event') end); game:GetService('Commands'):Register('hello',{},function() end)");
                Gameplay.Event("added",Gameplay.Players.Find(UserId));
                RequestDrain();
                Puts(Prefix + "PASS controlled integration; awaiting production NextFrame marker and unload checks");
                AwaitPhase3Teardown(Host, 60);
            } catch (Exception Error) { PrintError(Prefix + "FAIL " + Error); }
            finally {
                if (Foreign != null) { string Reason; Carbon.Community.Runtime.CommandManager.UnregisterCommand(Foreign,out Reason); }
                permission.RevokeUserPermission(UserId,"carbonluau.example.hello");
                if (Player != null) {
                    Gameplay.Players.Disconnect(UserId,Player);
                    Player.net.connection = null;
                    BasePlayer.activePlayerLookup.Remove(Id); BasePlayer.activePlayerList.Remove(Player);
                    Player.Kill();
                }
                if (Mount != null && !Mount.IsDestroyed) Mount.Kill();
                if (Parent != null && !Parent.IsDestroyed) Parent.Kill();
            }
        }
        private void AwaitPhase3Teardown(ScriptHost Expected, int Frames)
        {
            NextFrame(() => {
                if (Host != Expected || !Host.Ready) { PrintError("[CarbonLuau:HostFixture] FAIL unexpected runtime before teardown"); return; }
                if (Host.HasWork) {
                    if (Frames == 0) PrintError("[CarbonLuau:HostFixture] FAIL production event did not drain");
                    else AwaitPhase3Teardown(Expected, Frames - 1);
                    return;
                }
                ReleaseNative();
                if (Host != null || Native != null || Carbon.Community.Runtime.CommandManager.Chat.Any(Command => Object.ReferenceEquals(Command.Reference, this)))
                    PrintError("[CarbonLuau:HostFixture] FAIL remaining host/native resources after teardown");
                else Puts("[CarbonLuau:HostFixture] PASS teardown: owned chat registrations removed and native library released");
            });
        }
    }
}
