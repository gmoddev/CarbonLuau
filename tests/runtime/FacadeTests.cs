using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class FacadeTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    {
        public Runtime.FacadeSession Active;
        public bool RejectNext;
        public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next)
        {
            if (RejectNext) { RejectNext = false; throw new InvalidOperationException("fixture command collision"); }
            Active = Next;
        }
    }
    private static void Check(bool Condition, string Message) { if (!Condition) throw new Exception("Phase 3: " + Message); }
    private static string Drain(Runtime.ScriptHost Host)
    {
        string Logs = "";
        for (int Frame = 0; Frame < 200 && Host.HasWork; ++Frame)
            foreach (var Result in Host.Drain()) Logs += Result.Logs;
        return Logs;
    }
    private static Runtime.PhysicalInventoryContainer InventoryContainer(object Identity, List<Runtime.PhysicalInventoryStack> Stacks, int Capacity)
    { return new Runtime.PhysicalInventoryContainer {Identity = Identity, StackCount = Stacks.Count, Capacity = Capacity, Read = Index => Stacks[Index]}; }
    private static int TakePhysical(List<Runtime.PhysicalInventoryStack> Stacks, object Parent, object Definition, int Amount)
    {
        int Removed = 0;
        for (int Index = 0; Index < Stacks.Count && Removed < Amount; ++Index) {
            Runtime.PhysicalInventoryStack Stack = Stacks[Index];
            if (!Stack.Valid || !Object.ReferenceEquals(Stack.Parent, Parent) ||
                !Object.ReferenceEquals(Stack.Definition, Definition) || Stack.Amount <= 0) continue;
            int Used = (int)Math.Min(Stack.Amount, Amount - Removed); Removed += Used;
            long Left = Stack.Amount - Used;
            Stacks[Index] = new Runtime.PhysicalInventoryStack(Parent, Definition, Left, Left > 0);
        }
        return Removed;
    }
    public static void Run(Runtime.NativeRuntime Native, string Repository = null)
    {
        const string UserId = "76561198000000001";
        var Views = new Dictionary<string, Runtime.PlayerView>();
        var Messages = new List<string>(); bool Allowed = false;
        var Position = new Runtime.PlayerPosition(10.5f, -20.25f, 30.75f);
        var TeleportState = new Runtime.PlayerTeleportState {Current = true, Alive = true};
        bool TeleportMutationFailure = false, TeleportVerification = true;
        int TeleportMutations = 0, TeleportVerifications = 0, TakeMutations = 0;
        float Health = 87.5f, MaxHealth = 137.25f;
        object Scrap = new object(), Wood = new object(), Rifle = new object();
        object MainId = new object(), BeltId = new object(), WearId = new object(), ExternalId = new object();
        var Main = new List<Runtime.PhysicalInventoryStack>(); var Belt = new List<Runtime.PhysicalInventoryStack>(); var Wear = new List<Runtime.PhysicalInventoryStack>();
        Func<Runtime.PhysicalInventorySource> Inventory = () => new Runtime.PhysicalInventorySource {
            Main = InventoryContainer(MainId, Main, 64), Belt = InventoryContainer(BeltId, Belt, 32), Wear = InventoryContainer(WearId, Wear, 32)};
        Func<Runtime.PlayerView> View = () => new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = UserId,
            Name = "Fixture Player", Connected = true, Send = Message => Messages.Add(Message), Permission = Permission => Allowed,
            Position = () => Position, Health = () => Health, MaxHealth = () => MaxHealth, Inventory = Inventory,
            TakeInventory = (Definition, Amount) => {
                TakeMutations++;
                int Removed = TakePhysical(Main, MainId, Definition, Amount);
                Removed += TakePhysical(Belt, BeltId, Definition, Amount - Removed);
                Removed += TakePhysical(Wear, WearId, Definition, Amount - Removed);
                return Removed;
            },
            Teleport = new Runtime.PlayerTeleportOperation(
                () => TeleportState,
                (Destination, Before) => {
                    TeleportMutations++; Position = Destination; TeleportState.Mounted = false; TeleportState.Parented = false;
                    if (TeleportMutationFailure) throw new InvalidOperationException("private teleport failure");
                },
                (Destination, Before) => {
                    TeleportVerifications++;
                    return TeleportVerification && TeleportState.Current && TeleportState.Sleeping == Before.Sleeping &&
                        !TeleportState.Mounted && !TeleportState.Parented && Position.X == Destination.X &&
                        Position.Y == Destination.Y && Position.Z == Destination.Z;
                }) };
        var Directory = new Runtime.PlayerDirectory(Id => Views.ContainsKey(Id) ? Views[Id] : null);
        var Definitions = new Dictionary<string, object>(StringComparer.Ordinal) {{"scrap", Scrap}, {"wood", Wood}, {"rifle.ak", Rifle}};
        var Registrar = new Registrar(); var World = new Runtime.FacadeWorld(Directory, Registrar,
            new Runtime.ItemDirectory(Name => Definitions.ContainsKey(Name) ? Definitions[Name] : null));
        string Source = "", FailureModule = null;
        var Config = new Runtime.RuntimeConfig {MaxCallbackMilliseconds = 100, FrameDrainBudgetMilliseconds = 20};
        Func<Runtime.ScriptSnapshot> SnapshotSource = () => {
            var Snapshot = new Runtime.ScriptSnapshot {EntryName = "init.luau", EntrySource = Source};
            Snapshot.Modules.Add("state", "return {}");
            if (FailureModule != null) Snapshot.Modules.Add("caughtresource", FailureModule);
            Snapshot.Modules.Add("positionread", "return game:GetService('Players'):GetPlayers()[1].Position");
            Snapshot.Modules.Add("healthread", "local P=game:GetService('Players'):GetPlayers()[1]; return {Health=P.Health,MaxHealth=P.MaxHealth}");
            Snapshot.Modules.Add("inventoryread", "local P=game:GetService('Players'):GetPlayers()[1]; local I=game:GetService('Items'); return {Exists=I:Exists('scrap'),Count=P:CountItem('scrap'),Has=P:HasItem('scrap')}");
            return Snapshot;
        };
        const string ReadState = "local State=require('state'); local P,Old,Snapshot,C=State.P,State.Old,State.Snapshot,State.C; ";
        using (var Host = new Runtime.ScriptHost(Native, Config, SnapshotSource, World)) {
            Action<string> Load = Text => { Source = ReadState + Text + "; State.P=P; State.Old=Old; State.Snapshot=Snapshot; State.C=C"; var Result = Host.Reload(); Check(Result.Status == Runtime.RuntimeStatus.OK, "load: " + Result.Error); };
            Action<string> Execute = Text => { var Result = Host.Execute("facade.test", ReadState + Text); Check(Result.Status == Runtime.RuntimeStatus.OK, "execute: " + Result.Error); };
            Load("local P=game:GetService('Players'); local I=game:GetService('Items'); assert(#P:GetPlayers()==0); assert(P==game:GetService('Players')); assert(I==game:GetService('Items')); assert(I:Exists('wood') and I:Exists('scrap') and I:Exists('rifle.ak') and not I:Exists('unknown.item')); assert(not pcall(function() I:Exists('Scrap') end)); assert(not pcall(function() I:Exists(' scrap') end)); assert(not pcall(function() I:Exists('scrap ') end)); assert(not pcall(function() I:Exists('') end)); assert(not pcall(function() I:Exists(string.rep('a',129)) end)); assert(not pcall(function() I:Exists('scrap\\0bad') end)); assert(not pcall(function() I:Exists('café') end)); assert(not pcall(function() I:Exists(1) end)); assert(not I:Exists('a') and not I:Exists(string.rep('a',128))); assert(game:GetService('Commands')==game:GetService('Commands')); assert(game.ApiVersion=='0.4.0-experimental'); assert(not pcall(function() game:GetService('X') end)); assert(not pcall(function() game:GetService(1) end)); assert(not pcall(function() P:GetPlayerByUserId(123) end)); assert(P:GetPlayerByUserId('123')==nil); assert(__hostcall==nil and debug==nil and getfenv==nil)");
            FailureModule = "print('module-attempt'); game:GetService('Commands'):Register('moduleleak',{},function() end); task.defer(function() error('module task leaked') end); error('caught resource failure')";
            Source = "assert(not pcall(require,'caughtresource')); assert(not pcall(require,'caughtresource'))";
            var FailedModule = Host.Reload();
            Check(FailedModule.Status == Runtime.RuntimeStatus.OK && FailedModule.Logs == "module-attempt\nmodule-attempt\n", "caught failed module retries inside successful candidate");
            Check(!Registrar.Active.Commands.ContainsKey("moduleleak") && !Host.HasWork && Host.Status().Contains("modules: 0"), "caught failed module publishes no cache, command or task");
            FailureModule = null;
            Views[UserId] = View(); var Lifetime = Directory.Connect(Views[UserId]);
            Load("P=game:GetService('Players'); Old=P:GetPlayers()[1]; Snapshot=P:GetPlayers(); assert(Old==P:GetPlayerByUserId('" + UserId + "')); assert(Old.UserId=='" + UserId + "' and type(Old.UserId)=='string'); assert(Old.Name=='Fixture Player' and Old.IsConnected); assert(not pcall(function() Old.Name='forged' end)); assert(not pcall(function() Old.Health=1 end)); assert(not pcall(function() Old.MaxHealth=1 end)); assert(not pcall(function() Old.SendMessage({},'forged') end)); assert(not pcall(function() Old:SendMessage('provisional') end)); local ProvisionalPosition=require('positionread'); assert(ProvisionalPosition==Vector3.new(10.5,-20.25,30.75)); local ProvisionalHealth=require('healthread'); assert(ProvisionalHealth.Health==87.5 and ProvisionalHealth.MaxHealth==137.25); local ProvisionalInventory=require('inventoryread'); assert(ProvisionalInventory.Exists and ProvisionalInventory.Count==0 and not ProvisionalInventory.Has); State.SavedPosition=Old.Position; State.SavedHealth=Old.Health; State.SavedMaxHealth=Old.MaxHealth; task.defer(function() Old:SendMessage('committed') end)");
            Check(Messages.Count == 0, "D10 caught provisional message has no effect"); Drain(Host); Check(Messages.Count == 1 && Messages[0] == "committed", "D10 deferred delivery after commit");
            Execute("local Zero=Vector3.new(0,0,0); local A=Vector3.new(10,20,30); local B=Vector3.new(5,0,-5); assert(Zero.X==0 and Zero.Y==0 and Zero.Z==0 and Zero.Magnitude==0); assert(Vector3.new(-1.5,2.25,-3.75).X==-1.5); assert(Vector3.new(3,4,12).Magnitude==13); assert(tostring(A)=='Vector3'); assert(A==Vector3.new(10,20,30) and A~=Vector3.new(11,20,30) and A~=Vector3.new(10,21,30) and A~=Vector3.new(10,20,31) and A~={}); assert(Vector3.new(0.1+0.2,0,0)~=Vector3.new(0.3,0,0)); assert(A+B==Vector3.new(15,20,25)); assert(A-B==Vector3.new(5,20,35)); assert(-A==Vector3.new(-10,-20,-30)); assert(A*2==Vector3.new(20,40,60)); assert(2*A==Vector3.new(20,40,60)); assert(A/2==Vector3.new(5,10,15)); local Maximum=Vector3.new(3.4028234663852886e38,-3.4028234663852886e38,0); assert(Maximum.X>0 and Maximum.Y<0); assert(not pcall(Vector3.new,0/0,0,0)); assert(not pcall(Vector3.new,1/0,0,0)); assert(not pcall(Vector3.new,3.4028236e38,0,0)); assert(not pcall(Vector3.new,'1',0,0)); assert(not pcall(function() A.X=1 end)); assert(not pcall(function() A.Unknown=1 end)); assert(not pcall(function() return A*B end)); assert(not pcall(function() return A/B end)); assert(not pcall(function() return A/0 end)); assert(not pcall(function() return Maximum+Maximum end)); assert(not pcall(function() return Maximum*2 end))");
            Execute("assert(select('#',Old:Teleport(Vector3.new(1.25,-2.5,3.75)))==0); assert(Old.Position==Vector3.new(1.25,-2.5,3.75)); assert(not pcall(function() Old:Teleport({X=1,Y=2,Z=3}) end)); assert(not pcall(function() Old:Teleport(1) end))");
            Check(TeleportMutations == 1 && TeleportVerifications == 1, "active Teleport returns no values and verifies exactly once");
            var BeforeProvisionalTeleport = Position;
            Load("P=game:GetService('Players'); Old=P:GetPlayers()[1]; Snapshot=P:GetPlayers(); State.SavedPosition=Vector3.new(10.5,-20.25,30.75); State.SavedHealth=87.5; State.SavedMaxHealth=137.25; assert(not pcall(function() Old:Teleport(Vector3.new(10,20,30)) end)); task.defer(function() Old:Teleport(Vector3.new(11,22,33)) end)");
            Check(Position.X == BeforeProvisionalTeleport.X && TeleportMutations == 1, "provisional Teleport is rejected before host mutation");
            Drain(Host); Check(Position.X == 11f && Position.Y == 22f && Position.Z == 33f && TeleportMutations == 2,
                "deferred Teleport runs exactly once after successful commit");
            var BeforeFailedCandidate = Position;
            Source = "local P=game:GetService('Players'):GetPlayers()[1]; task.defer(function() P:Teleport(Vector3.new(44,55,66)) end); error('reject')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && !Host.HasWork &&
                Position.X == BeforeFailedCandidate.X && TeleportMutations == 2, "failed candidate never publishes deferred Teleport");
            TeleportState = new Runtime.PlayerTeleportState {Current = true, Alive = true, Sleeping = true, Mounted = true, Parented = true};
            Execute("Old:Teleport(Vector3.new(-7,8,-9))");
            Check(TeleportState.Sleeping && !TeleportState.Mounted && !TeleportState.Parented,
                "sleeping Teleport preserves sleep and normalizes mounted/parented state");
            foreach (var Ineligible in new[] {
                new Runtime.PlayerTeleportState {Current = true, Alive = false},
                new Runtime.PlayerTeleportState {Current = true, Alive = true, Spectating = true},
                new Runtime.PlayerTeleportState {Current = true, Alive = true, Wounded = true},
                new Runtime.PlayerTeleportState {Current = true, Alive = true, Incapacitated = true}
            }) {
                TeleportState = Ineligible; int Before = TeleportMutations;
                Execute("assert(not pcall(function() Old:Teleport(Vector3.new(1,2,3)) end))");
                Check(TeleportMutations == Before, "ineligible Teleport fails before mutation");
            }
            TeleportState = new Runtime.PlayerTeleportState {Current = true, Alive = true};
            TeleportMutationFailure = true;
            Execute("local Ok,Error=pcall(function() Old:Teleport(Vector3.new(70,80,90)) end); assert(not Ok and string.find(Error,'after host mutation began',1,true) and not string.find(Error,'private teleport failure',1,true))");
            Check(Position.X == 70f, "failed post-boundary Teleport is not rolled back");
            TeleportMutationFailure = false; TeleportVerification = false; int BeforeMismatchVerify = TeleportVerifications;
            Execute("assert(not pcall(function() Old:Teleport(Vector3.new(71,81,91)) end))");
            Check(TeleportVerifications == BeforeMismatchVerify + 1 && Position.X == 71f,
                "verification mismatch performs one check and claims no rollback");
            TeleportVerification = true;
            Position = new Runtime.PlayerPosition(-4.5f, 6.25f, 8.75f);
            Execute("local First=Old.Position; assert(First==Vector3.new(-4.5,6.25,8.75)); assert(First~=require('state').SavedPosition); local Second=Old.Position; assert(First==Second and not rawequal(First,Second))");
            Position = new Runtime.PlayerPosition(101.5f, 202.25f, -303.75f);
            Execute("assert(Old.Position==Vector3.new(101.5,202.25,-303.75))");
            foreach (var Vitals in new[] {
                new[]{0f, 100f}, new[]{0.125f, 250.75f}, new[]{190.5f, 125.25f}, new[]{-2.5f, 0f}
            }) {
                Health = Vitals[0]; MaxHealth = Vitals[1];
                Execute("assert(Old.Health==" + Health.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " and Old.MaxHealth==" + MaxHealth.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")");
            }
            foreach (string StateName in new[]{"normal", "sleeping", "wounded", "dead-but-host-valid"})
                Execute("assert(Old.Health==-2.5 and Old.MaxHealth==0, '" + StateName + "')");
            Health = 53.375f; MaxHealth = 142.625f;
            Execute("assert(Old.Health==53.375 and Old.MaxHealth==142.625); local A=Old.Health; assert(A==53.375 and require('state').SavedHealth==87.5 and require('state').SavedMaxHealth==137.25)");
            Health = Single.NaN; Execute("assert(not pcall(function() return Old.Health end))");
            Health = 53.375f; MaxHealth = Single.PositiveInfinity; Execute("assert(not pcall(function() return Old.MaxHealth end))");
            MaxHealth = 142.625f;
            Execute("assert(Old:CountItem('scrap')==0 and not Old:HasItem('scrap') and Old:CountItem('unknown.item')==0 and not Old:HasItem('unknown.item',1))");
            Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 50, true));
            Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 25, true));
            Main.Add(new Runtime.PhysicalInventoryStack(MainId, Wood, 500, true));
            Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 0, true));
            Belt.Add(new Runtime.PhysicalInventoryStack(BeltId, Scrap, 20, true));
            Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Scrap, 5, true));
            Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Scrap, 999, false));
            Wear.Add(new Runtime.PhysicalInventoryStack(ExternalId, Scrap, 999, true));
            Execute("assert(Old:CountItem('scrap')==100 and Old:CountItem('wood')==500); assert(Old:HasItem('scrap') and Old:HasItem('scrap',1) and Old:HasItem('scrap',100) and not Old:HasItem('scrap',101)); assert(not pcall(function() Old:HasItem('scrap',0) end)); assert(not pcall(function() Old:HasItem('scrap',-1) end)); assert(not pcall(function() Old:HasItem('scrap',1.5) end)); assert(not pcall(function() Old:HasItem('scrap',9007199254740992) end)); assert(not pcall(function() Old:HasItem('scrap',0/0) end)); assert(not pcall(function() Old:HasItem('scrap',1/0) end)); assert(not pcall(function() Old:HasItem('scrap','1') end)); assert(not pcall(function() Old:CountItem('Scrap') end))");
            Main[0] = new Runtime.PhysicalInventoryStack(MainId, Scrap, 51, true);
            Execute("assert(Old:CountItem('scrap')==101 and Old:HasItem('scrap',101))");
            Execute("assert(Old:TakeItem('wood',1) and Old:CountItem('wood')==499); assert(not Old:TakeItem('wood',500)); assert(not Old:TakeItem('wood',2147483647)); assert(not Old:TakeItem('unknown.item',1)); assert(not pcall(function() Old:TakeItem('wood') end)); assert(not pcall(function() Old:TakeItem('wood',0) end)); assert(not pcall(function() Old:TakeItem('wood',-1) end)); assert(not pcall(function() Old:TakeItem('wood',1.5) end)); assert(not pcall(function() Old:TakeItem('wood',2147483648) end)); assert(not pcall(function() Old:TakeItem('Wood',1) end)); assert(not pcall(function() Old:TakeItem(1,1) end)); assert(not pcall(function() Old:TakeItem('wood','1') end))");
            Check(TakeMutations == 1, "TakeItem true verifies one commit while false and invalid inputs never commit");
            int BeforeProvisionalTake = TakeMutations;
            Load("P=game:GetService('Players'); Old=P:GetPlayers()[1]; Snapshot=P:GetPlayers(); State.SavedPosition=Vector3.new(10.5,-20.25,30.75); State.SavedHealth=87.5; State.SavedMaxHealth=137.25; assert(Old:CountItem('wood')==499); assert(not pcall(function() Old:TakeItem('wood',1) end)); task.defer(function() assert(Old:TakeItem('wood',1)); print('take-committed') end)");
            Check(TakeMutations == BeforeProvisionalTake, "provisional TakeItem rejects before host mutation");
            Check(Drain(Host) == "take-committed\n" && TakeMutations == BeforeProvisionalTake + 1,
                "deferred TakeItem executes exactly once after successful publication");
            Source = "local P=game:GetService('Players'):GetPlayers()[1]; task.defer(function() P:TakeItem('wood',1) end); error('reject take')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && !Host.HasWork &&
                TakeMutations == BeforeProvisionalTake + 1, "failed candidate never publishes deferred TakeItem");
            var SavedMain = new List<Runtime.PhysicalInventoryStack>(Main); var SavedBelt = new List<Runtime.PhysicalInventoryStack>(Belt); var SavedWear = new List<Runtime.PhysicalInventoryStack>(Wear);
            Main.Clear(); Belt.Clear(); Wear.Clear();
            for (int Index = 0; Index < Runtime.FacadePolicy.InventoryStacks; ++Index)
                Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 1, true));
            Execute("assert(Old:CountItem('scrap')==128 and Old:HasItem('scrap',128))");
            Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 1, true));
            Execute("assert(not pcall(function() return Old:CountItem('scrap') end)); assert(not pcall(function() return Old:HasItem('scrap',1) end))");
            Main.Clear(); Main.AddRange(SavedMain); Belt.AddRange(SavedBelt); Wear.AddRange(SavedWear);
            Position = new Runtime.PlayerPosition(Single.NaN, 0, 0);
            Execute("assert(not pcall(function() return Old.Position end))");
            Position = new Runtime.PlayerPosition(101.5f, 202.25f, -303.75f);
            var PositionPrevious = Registrar.Active; Source = "local Value=require('positionread'); assert(Value==Vector3.new(101.5,202.25,-303.75)); error('position candidate rejected')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Registrar.Active == PositionPrevious && !Host.HasWork,
                "failed provisional Position candidate preserves active domain and publishes no work");
            var HealthPrevious = Registrar.Active; Source = "local Value=require('healthread'); assert(Value.Health==53.375 and Value.MaxHealth==142.625); error('health candidate rejected')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Registrar.Active == HealthPrevious && !Host.HasWork,
                "failed provisional Health candidate preserves active domain and publishes no work");
            var InventoryPrevious = Registrar.Active; Source = "local Value=require('inventoryread'); assert(Value.Exists and Value.Count==101 and Value.Has); error('inventory candidate rejected')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Registrar.Active == InventoryPrevious && !Host.HasWork,
                "failed provisional inventory candidate preserves active domain and publishes no work");
            var Second = View(); Second.UserId = "76561198000000002"; Second.Name = "Second Player";
            Views[Second.UserId] = Second; Directory.Connect(Second);
            Execute("assert(#P:GetPlayers()==2 and #Snapshot==1); assert(P:GetPlayers()[2].Name=='Second Player')");
            Directory.Disconnect(Second.UserId,Second.Identity); Views.Remove(Second.UserId);
            var Previous = Registrar.Active;
            Source = "local P=game:GetService('Players'):GetPlayers()[1]; task.defer(function() P:SendMessage('leak') end); P:SendMessage('illegal')";
            Check(Host.Reload().Status == Runtime.RuntimeStatus.RUNTIME_ERROR && Registrar.Active == Previous, "D10 unhandled send rejects candidate");
            Drain(Host); Check(Messages.Count == 1, "failed candidate never sends deferred message");
            Execute("assert(not pcall(function() Old:SendMessage(string.rep('x',1025)) end)); assert(not pcall(function() Old:HasPermission('INVALID') end)); assert(not Old:HasPermission('fixture.allowed'))");
            var NormalSend = Views[UserId].Send;
            Views[UserId].Send = Message => { Host.Status(); };
            Execute("local Ok,Error=pcall(function() Old:SendMessage('reentrant') end); assert(not Ok and string.find(Error,'host operation failed',1,true))");
            Views[UserId].Send = Message => { throw new InvalidOperationException("private host detail must not leak"); };
            Execute("local Ok,Error=pcall(function() Old:SendMessage('host failure') end); assert(not Ok and not string.find(Error,'private host detail',1,true))");
            Views[UserId].Send = NormalSend;
            var NormalTeleport = Views[UserId].Teleport;
            Views[UserId].Teleport = new Runtime.PlayerTeleportOperation(
                () => new Runtime.PlayerTeleportState {Current = true, Alive = true},
                (Destination, Before) => { Host.Status(); },
                (Destination, Before) => true);
            Execute("local Ok,Error=pcall(function() Old:Teleport(Vector3.new(1,2,3)) end); assert(not Ok and string.find(Error,'after host mutation began',1,true))");
            Views[UserId].Teleport = NormalTeleport;
            Views[UserId].Connected = false;
            Execute("assert(not Old.IsConnected)");
            Views[UserId].Connected = true;
            Execute("assert(not Old.IsConnected)");
            Check(Directory.Resolve(Lifetime.Token,UserId)==null,"observed invalidation is permanent even if host object/connection is reused");
            var Removed = Directory.Disconnect(UserId, Views[UserId].Identity); Views.Remove(UserId);
            Execute("local Saved=require('state').SavedPosition; assert(Saved==Vector3.new(10.5,-20.25,30.75) and Saved*2==Vector3.new(21,-40.5,61.5),'saved vector'); assert(require('state').SavedHealth==87.5 and require('state').SavedMaxHealth==137.25,'saved vitals'); assert(not Old.IsConnected and Old.Name=='Fixture Player','stale snapshot'); assert(#Snapshot==1,'snapshot'); assert(not pcall(function() return Old.Position end),'stale position'); assert(not pcall(function() return Old.Health end),'stale health'); assert(not pcall(function() return Old.MaxHealth end),'stale max'); assert(not pcall(function() Old:CountItem('scrap') end),'stale count'); assert(not pcall(function() Old:HasItem('scrap') end),'stale has'); assert(not pcall(function() Old:TakeItem('scrap',1) end),'stale take'); assert(not pcall(function() Old:Teleport(Vector3.new(1,2,3)) end),'stale teleport'); assert(not pcall(function() Old:SendMessage('stale') end),'stale send'); assert(not pcall(function() Old:HasPermission('fixture.allowed') end),'stale permission')");
            Views[UserId] = View(); var Reconnected = Directory.Connect(Views[UserId]);
            Check(Reconnected.Token != Removed.Token && Directory.Resolve(Removed.Token, UserId) == null && Directory.Resolve("forged", UserId) == null, "reconnect/forged token never retargets");
            Execute("assert(not Old.IsConnected,'old connected'); assert(not pcall(function() return Old.Position end),'old position'); assert(not pcall(function() return Old.Health end),'old health'); assert(not pcall(function() return Old.MaxHealth end),'old max'); assert(not pcall(function() return Old:CountItem('scrap') end),'old count'); assert(not pcall(function() Old:TakeItem('scrap',1) end),'old take'); assert(not pcall(function() Old:Teleport(Vector3.new(1,2,3)) end),'old teleport'); local Fresh=P:GetPlayers()[1]; assert(Fresh~=Old,'fresh identity'); assert(Fresh.IsConnected,'fresh connected'); assert(Fresh.Position==Vector3.new(101.5,202.25,-303.75),'fresh position'); assert(Fresh.Health==53.375,'fresh health'); assert(Fresh.MaxHealth==142.625,'fresh max'); assert(Fresh:CountItem('scrap')==101,'fresh count '..Fresh:CountItem('scrap'))");

            Load("local P=game:GetService('Players'); P.PlayerAdded:Connect(function() print('first') end); C=P.PlayerAdded:Connect(function() print('second') end); P.PlayerAdded:Connect(function() error('listener error') end); P.PlayerAdded:Connect(function() print('last') end); P.PlayerRemoving:Connect(function(V) assert(not V.IsConnected); print(V.UserId) end)");
            Check(!Host.HasWork, "no synthetic joins for current players"); World.Event("added", Reconnected);
            Execute("C:Disconnect(); C:Disconnect()");
            Check(Drain(Host) == "first\nlast\n", "registration order, queued disconnect and listener error isolation");
            Source = "game:GetService('Players').PlayerAdded:Connect(function() print('leak') end); error('bad')";
            Previous = Registrar.Active; Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Registrar.Active == Previous, "failed candidate preserves listeners");
            World.Event("added", Reconnected); Check(Drain(Host) == "first\nlast\n", "preserved listeners actually execute");
            Removed = Directory.Disconnect(UserId, Views[UserId].Identity); Views.Remove(UserId); World.Event("removing", Removed);
            Check(Drain(Host) == UserId + "\n", "removing retains safe snapshot after host disappears");
            Views[UserId] = View(); Reconnected = Directory.Connect(Views[UserId]);

            string Command = "game:GetService('Commands'):Register('hello',{permission='carbonluau.example.hello',description='Hello'},function(C) assert(C.Name=='hello' and C.Player.UserId=='" + UserId + "'); print(C.Arguments[1]); C.Player:SendMessage('A') end)";
            Load(Command);
            Check(!Registrar.Active.Invoke("hello", UserId, new[] {"denied"}) && !Host.HasWork, "permission denial before native admission");
            Allowed = true; string[] Arguments = {"snapshot"}; Check(Registrar.Active.Invoke("hello", UserId, Arguments), "authorized command admitted"); Arguments[0] = "changed";
            Check(Drain(Host) == "snapshot\n" && Messages[Messages.Count - 1] == "A", "argument snapshot and messaging");
            Check(!Registrar.Active.Invoke("hello", UserId, new string[17]) && !Registrar.Active.Invoke("hello", UserId, new[] {new string('x',513)}), "argument bounds");
            var ExcessTotal=new string[9]; for(int Index=0; Index<ExcessTotal.Length; ++Index) ExcessTotal[Index]=new string('x',512);
            Check(!Registrar.Active.Invoke("hello", UserId, ExcessTotal) && !Registrar.Active.Invoke("hello",UserId,new[]{"embedded\0NUL"}),"total/NUL argument bounds");
            Check(Registrar.Active.Invoke("hello", UserId, new[] {"revoked"}), "queued authorization fixture"); Allowed = false;
            Check(Drain(Host) == "", "permission recheck immediately before Lua entry"); Allowed = true;
            Previous = Registrar.Active;
            Source = Command.Replace("'A'", "'B'") + "; error('candidate B')";
            Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Registrar.Active == Previous, "B never published");
            Check(Previous.Invoke("hello", UserId, new[] {"A retained"}) && Drain(Host) == "A retained\n", "A command survives B");
            Registrar.RejectNext = true; Source = Command; Check(Host.Reload().Status != Runtime.RuntimeStatus.OK && Registrar.Active == Previous, "host publication failure preserves A");
            Check(Previous.Invoke("hello", UserId, new[] {"old queued"}), "old work pending before C");
            Load(Command.Replace("'A'", "'C'"));
            Check(!Previous.Invoke("hello", UserId, new[] {"late selected A"}) && Drain(Host) == "", "selected late A and old queued work cannot enter C");
            Check(Registrar.Active.Invoke("hello", UserId, new[] {"C active"}) && Drain(Host) == "C active\n" && Messages[Messages.Count-1] == "C", "C atomically replaces A");
            Load("game:GetService('Commands'):Register('hello',{},function(C) assert(not pcall(function() C.Name='changed' end)); assert(not pcall(function() C.Arguments[1]='changed' end)); if C.Arguments[1]=='error' then error('command fixture error') end; print('command survived') end)");
            Check(Registrar.Active.Invoke("hello",UserId,new[]{"error"}) && Registrar.Active.Invoke("hello",UserId,new[]{"ok"}),"command error fixture admitted");
            Check(Drain(Host)=="command survived\n" && Host.Ready,"command error is isolated and context/arguments are immutable");
            Check(Registrar.Active.Invoke("hello",UserId,new[]{"disconnect queued"}),"command pending before disconnect");
            Directory.Disconnect(UserId,Views[UserId].Identity); Views.Remove(UserId);
            Check(!Registrar.Active.Invoke("hello",UserId,new[]{"disconnected"}) && Drain(Host)=="","disconnected caller and queued old connection never enter Lua");
            Views[UserId]=View(); Reconnected=Directory.Connect(Views[UserId]);
            bool WrongThreadRejected=false;
            var OtherThread=new System.Threading.Thread(()=>{ try { World.Event("added",Reconnected); } catch (InvalidOperationException) { WrongThreadRejected=true; } });
            OtherThread.Start(); OtherThread.Join(); Check(WrongThreadRejected,"facade rejects off-thread host intake");
            foreach (string Invalid in new[] {"", "Bad", "quit", "carbonluau", "a.b", new string('a',33)}) {
                Source = "game:GetService('Commands'):Register('" + Invalid + "',{},function() end)";
                Check(Host.Reload().Status != Runtime.RuntimeStatus.OK, "invalid/protected command name");
            }
            foreach (string Bad in new[] {
                "local C=game:GetService('Commands'); C:Register('hello',{},function() end); C:Register('hello',{},function() end)",
                "for I=1,65 do game:GetService('Commands'):Register('cmd'..I,{},function() end) end",
                "game:GetService('Commands'):Register('hello',{permission='bad'},function() end)",
                "game:GetService('Commands'):Register('hello',{permission=1},function() end)",
                "game:GetService('Commands'):Register('hello',{extra=true},function() end)",
                "for I=1,129 do game:GetService('Players').PlayerAdded:Connect(function() end) end"}) {
                Source = Bad; Check(Host.Reload().Status != Runtime.RuntimeStatus.OK, "registration/options bounds");
            }
            Load("local S=game:GetService('Players').PlayerAdded; for I=1,2000 do local C=S:Connect(function() end); C:Disconnect(); C:Disconnect() end");
            World.Event("added", Reconnected); Check(!Host.HasWork, "2000 disconnects release registrations");
            Load("game:GetService('Commands'):Register('hello',{},function() print('public') end)");
            Allowed=false; Check(Registrar.Active.Invoke("hello",UserId,new string[0]) && Drain(Host)=="public\n","no permission is explicitly public"); Allowed=true;
            Execute("assert(not pcall(function() game:GetService('Commands'):Register('later',{},function() end) end))");
            Load("game:GetService('Players').PlayerAdded:Connect(function() print('queued') end)");
            for(int Index=0; Index<1000; ++Index) World.Event("added",Reconnected);
            Check(Registrar.Active.PendingCount==256 && Registrar.Active.Rejected==744,"managed admission bound rejects excess events");
            Load(Command); Check(!Host.HasWork,"replacement cancels bounded intake");
            var Watch = Stopwatch.StartNew();
            for (int Cycle=0; Cycle<100; ++Cycle) {
                Previous = Registrar.Active; Load(Command);
                Check(!Previous.Invoke("hello",UserId,new[]{"late"}), "stress late generation");
                for (int Index=0; Index<20; ++Index) {
                    var Old = Reconnected;
                    Directory.Disconnect(UserId,Views[UserId].Identity); Views[UserId]=View(); Reconnected=Directory.Connect(Views[UserId]);
                    Check(Directory.Resolve(Old.Token,UserId)==null && Directory.Resolve(Reconnected.Token,UserId)!=null,"2000 reconnect checks");
                }
                Check(Registrar.Active.Invoke("hello",UserId,new[]{"stress"}),"stress command"); Drain(Host);
            }
            Console.WriteLine("[CarbonLuau:FacadeTest] 100 registration replacements + 2000 reconnects: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            foreach (int Count in new[]{1,10,100}) {
                Load("for I=1,"+Count+" do game:GetService('Players').PlayerAdded:Connect(function(P) assert(P.IsConnected) end) end");
                Watch.Restart(); World.Event("added",Reconnected); Drain(Host);
                Console.WriteLine("[CarbonLuau:FacadeTest] dispatch " + Count + " listeners: " + Watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
            }
            Watch.Restart(); Execute("local P=game:GetService('Players'); for I=1,2000 do assert(P:GetPlayerByUserId('"+UserId+"').IsConnected); assert(#P:GetPlayers()==1) end");
            Console.WriteLine("[CarbonLuau:FacadeTest] 2000 proxy lookup/snapshot pairs: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            Watch.Restart(); Execute("local Player=game:GetService('Players'):GetPlayerByUserId('"+UserId+"'); for I=1,2000 do local Value=Player.Position; assert(Value.X==101.5 and Value.Y==202.25 and Value.Z==-303.75) end");
            Console.WriteLine("[CarbonLuau:Player1A] 2000 live Position reads: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            Watch.Restart(); Execute("local Player=game:GetService('Players'):GetPlayerByUserId('"+UserId+"'); for I=1,2000 do assert(Player.Health==53.375 and Player.MaxHealth==142.625) end");
            Console.WriteLine("[CarbonLuau:Player1B] 2000 live Health/MaxHealth read pairs: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            Watch.Restart(); Execute("local Player=game:GetService('Players'):GetPlayerByUserId('"+UserId+"'); for I=1,2000 do assert(Player:CountItem('scrap')==101 and Player:HasItem('scrap',100)) end");
            Console.WriteLine("[CarbonLuau:Player1C] 2000 CountItem/HasItem pairs over 8 entries: " + Watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            Previous=Registrar.Active; Host.Dispose(); Check(Registrar.Active==null && !Previous.Invoke("hello",UserId,new[]{"unload"}) && !Host.HasWork,"unload removes all host registrations");
        }
        var Tight = new Runtime.RuntimeConfig {MaxCallbackMilliseconds=3}; string Runaway = "game:GetService('Players').PlayerAdded:Connect(function() while true do end end)";
        using (var Host = new Runtime.ScriptHost(Native,Tight,()=>new Runtime.ScriptSnapshot{EntryName="init.luau",EntrySource=Runaway},World)) {
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"runaway signal init");
            World.Event("added",Directory.Find(UserId)); Drain(Host);
            Check(Host.Ready && Host.Recoveries==1 && Host.Timeouts==1,"signal timeout reconstructs once");
            World.Event("added",Directory.Find(UserId)); Drain(Host);
            Check(!Host.Ready && Host.Timeouts==2 && World.Active==null,"second timeout retires signals and commands");
        }
        Check(Native.LiveVmCount==0,"all facade VMs destroyed");
        using (var Host = new Runtime.ScriptHost(Native,Config,()=>new Runtime.ScriptSnapshot{EntryName="init.luau",EntrySource="game:GetService('Players').PlayerAdded:Connect(function(P) P:SendMessage('stop'); print('returned') end); game:GetService('Players').PlayerAdded:Connect(function() print('must not run') end)"},World)) {
            Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"nested stop fixture init");
            var NormalSend=Views[UserId].Send;
            Views[UserId].Send=Message=>Host.RequestStop();
            try {
                World.Event("added",Directory.Find(UserId));
                Check(Drain(Host)=="returned\n" && !Host.Ready,"nested stop returns from current native call without admitting another callback");
                Host.Dispose(); Check(World.Active==null && Native.LiveVmCount==0,"outer teardown releases stopped generation");
            } finally { Views[UserId].Send=NormalSend; }
        }
        if (Repository != null) {
            foreach (string Example in new[]{"player-events", "player-position", "player-health", "player-teleport", "player-take-item", "hello-command", "gui/hello", "gui/shared-live", "gui/per-player", "gui/activated", "gui/images", "gui/scrolling"}) {
                string Text=File.ReadAllText(Path.Combine(Repository,"examples",Example,"init.luau"));
                using (var Host=new Runtime.ScriptHost(Native,Config,()=>new Runtime.ScriptSnapshot{EntryName="init.luau",EntrySource=Text},World))
                    Check(Host.Reload().Status==Runtime.RuntimeStatus.OK,"shipped example loads: "+Example);
            }
            Console.WriteLine("[CarbonLuau:FacadeTest] PASS twelve shipped root examples loaded through real compiler/VM");
        }
        Console.WriteLine("[CarbonLuau:FacadeTest] PASS services, proxies, Vector3, Position, Teleport, Health, MaxHealth, Items, physical inventory, TakeItem, lifetime, D10, signals, transactional commands, permissions, bounds, stress, recovery");
    }
}
