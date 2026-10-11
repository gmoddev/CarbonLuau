// Qualification package only. Entry is explicitly coordinated by the main B2
// runner after startup/Host readiness; no authenticated client or policy bypass.
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private bool EntityMeleeFixtureRunning;
        private bool EntityMeleeFixturePassed;
        private string EntityMeleeFixtureFailure;
        private BaseEntity EntityMeleeFixtureVictim;
        private Item EntityMeleeFixtureTool;
        private Network.Connection EntityMeleeFixtureReconnect;
        private int EntityMeleeFixtureSnapshots;
        private string EntityMeleeFixtureToken, EntityMeleeFixtureId;
        private string EntityMeleeFixtureVictimId, EntityMeleeFixtureExpectedToken;
        private int EntityMeleeFixtureTerminals;
        partial void ObserveEntityMeleeSnapshotFixture(BaseEntity Victim, string Token, string Id)
        {
            if(!EntityMeleeFixtureRunning)return;
            if(!ReferenceEquals(Victim,EntityMeleeFixtureVictim)){EntityMeleeFixtureFailure="candidate receipt victim mismatch";return;}
            EntityMeleeFixtureSnapshots++;EntityMeleeFixtureToken=Token;EntityMeleeFixtureId=Id;
        }
        partial void ObserveEntityMeleeTerminalFixture(GameplayEntityDiedObservation Observation)
        {
            if(!EntityMeleeFixtureRunning||Observation.Id!=EntityMeleeFixtureVictimId)return;
            EntityMeleeFixtureTerminals++;
            string Expected=EntityMeleeDeploymentQualified?EntityMeleeFixtureExpectedToken:null;
            if(Observation.KillerToken!=Expected||
                !(Expected==null?Observation.KillerId==null&&Observation.KillerName==null:Observation.KillerId==BarrelResearchActor.UserIDString))
                EntityMeleeFixtureFailure="immutable terminal actor metadata mismatch";
        }
        // Main must invoke this at an isolated fixture stage and wire Snapshot's
        // TryEntityMeleeKiller call. Missing integration fails rather than faking PASS.
        private void RunEntityMeleeQualification()
        {
            Network.Connection Connection=null; BaseMelee Weapon=null; Item Tool=null;
            if(EntityMeleeFixtureRunning){Puts("[CarbonLuau:EntityMeleeFixture] FAIL duplicate fixture entry");return;}
            EntityMeleeFixturePassed=false;EntityMeleeFixtureFailure=null;
            EntityMeleeFixtureReconnect=null;
            bool Scheduled=false,Started=false;
            try {
                BarrelResearchCheck(!EntityMeleeFixtureRunning && Host!=null&&Host.Ready&&!Host.Busy&&EntityDiedAvailable() &&
                    (BasePlayer.activePlayerList.Count==0 || BasePlayer.activePlayerList.Count==1&&ReferenceEquals(BasePlayer.activePlayerList[0],BarrelResearchActor)),"isolated melee fixture ready; no foreign actor");
                // D3's final case may still occupy slot zero. Reset its owned scene
                // once, before our actor/tool exist; never repeat this mid-matrix.
                var ExistingHost=Host;bool ExistingLootArmed=LootArmed;
                BarrelResearchCleanup();
                BarrelResearchCheck(BarrelResearchLedgerCount==0&&BarrelResearchActor==null&&BasePlayer.activePlayerList.Count==0&&
                    ReferenceEquals(Host,ExistingHost)&&LootArmed==ExistingLootArmed,"clean owned melee scene preserves Host/Loot");
                EntityMeleeFixtureRunning=true;Started=true;
                InitializeEntityMeleeSource();
                for(int Index=0;Index<2;Index++) {
                    var Method=Index==0?EntityMeleeRpcMethod:EntityMeleeSharedMethod;
                    byte[] Bytes=Method.GetMethodBody().GetILAsByteArray(); string Digest;
                    using(var Hash=SHA256.Create())Digest=BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant();
                    Puts("[CarbonLuau:EntityMeleeFixture] PIN method="+Method.Name+" token="+Method.MetadataToken+" bytes="+Bytes.Length+" hash="+Digest+" matches="+EntityMeleePinned(Method,Index));
                }
                BarrelResearchCheck(EntityMeleeCandidateAvailable()&&EntityMeleeAvailable()==EntityMeleeDeploymentQualified,"both live pins/topology qualified; deployment approval controls actor availability");
                BarrelResearchCreateActor(); BasePlayer Actor=BarrelResearchActor;
                // Bind a real server-side Connection to the owned actor; no Steam
                // authentication, network send, forged Player facade or API bypass.
                Connection=new Network.Connection{userid=Actor.userID,username=Actor.displayName,connected=true,active=true,state=(Network.Connection.State)4,canReserveSlot=false};
                Connection.player=Actor; EntityMeleeFixtureSetConnection(Actor,Connection);
                Actor.SetPlayerFlag(BasePlayer.PlayerFlags.Connected,true);
                Actor.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping,false);
                Actor.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded,false);
                if(!BasePlayer.activePlayerList.Contains(Actor))BasePlayer.activePlayerList.Add(Actor);
                BasePlayer.activePlayerLookup[Actor.userID]=Actor;
                OnPlayerConnected(Actor);
                var Lifetime=CurrentGameplayPlayer(Actor);
                BarrelResearchCheck(Lifetime!=null&&ReferenceEquals(Lifetime.Connection,Connection),"original constructed human D11 connection");
                Tool=BarrelResearchNewItem("machete",1);
                EntityMeleeFixtureTool=Tool;
                BarrelResearchCheck(Tool.MoveToContainer(Actor.inventory.containerBelt),"owned melee tool in belt");
                Actor.UpdateActiveItem(Tool.uid); Weapon=Tool.GetHeldEntity() as BaseMelee;
                BarrelResearchCheck(Weapon!=null&&EntityMeleeTypes.Contains(Weapon.GetType()),"ordinary base-dispatch melee component");
                BarrelResearchHold(Weapon); Weapon.SetHeld(true);

                // A candidate receipt comes only from main's real terminal capture,
                // not a hand-built source frame or a direct helper invocation.
                EntityMeleeFixtureCase(Actor,Connection,Weapon,Lifetime,"candidate_rpc");
                BarrelResearchCheck(EntityMeleeAvailable()==EntityMeleeDeploymentQualified,"fixture cannot toggle deployment approval");
                EntityMeleeFixtureNegative(Actor,Weapon,"direct_hurt",false);
                EntityMeleeFixtureNegative(Actor,Weapon,"human_owner_lookup",true);
                EntityMeleeFixtureUnstamped(Actor,Connection,Weapon);

                // Explicitly test the bound through original RPC entry, even when
                // cooldown refuses later damage. Time.frameCount is fixed in loop.
                int BudgetStart=BarrelResearchLedgerCount;
                var BudgetCase=BarrelResearchCreate("melee_budget",BarrelResearchMode.Vanilla,Actor.transform.position+Vector3.forward,true);
                try {
                BudgetCase.Source.health=10000f; EntityMeleeFixtureVictim=BudgetCase.Source;
                EntityMeleeFixtureVictimId=BudgetCase.Source.net.ID.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                EntityMeleeFixtureExpectedToken=null;EntityMeleeFixtureTerminals=0;
                for(int Index=0;Index<129;Index++)EntityMeleeFixturePacket(Actor,Connection,Weapon,BudgetCase.Source);
                BarrelResearchCheck(EntityMeleeCaptures==128&&EntityMeleeFrames.Count==0,"128 captures/actual frame; all RPC borrowed frames released");
                BarrelResearchCheck(EntityMeleeFixtureSnapshots==0,"nonterminal budget RPCs cannot fabricate death snapshots");
                } finally { ReleaseEntityMeleeFixtureCase(BudgetCase,BudgetStart,Weapon); }
                // Move the positive rerun to a fresh actual Unity frame. Do not
                // reset the production counter or weaken its policy for this test.
                NextTick(()=>CompleteEntityMeleeQualification(Actor,Connection,Weapon,Lifetime));
                Scheduled=true;
                Puts("[CarbonLuau:EntityMeleeFixture] CANDIDATE PASS real protobuf NetRead -> original PlayerAttack -> armed OnAttacked -> terminal death; direct/owner-resolved/unstamped negatives nil");
            } catch(Exception Error) {
                EntityMeleeFixtureFailure=Error.GetType().Name+": "+Error.Message;
                Puts("[CarbonLuau:EntityMeleeFixture] FAIL "+Error.GetType().Name+": "+Error.Message);
            } finally {
                if(!Scheduled&&Started)CleanupEntityMeleeQualification(Connection);
            }
        }
        private void CompleteEntityMeleeQualification(BasePlayer Actor,Network.Connection Connection,BaseMelee Weapon,PlayerLifetime Lifetime)
        {
            bool Passed=false;
            string Token=Lifetime==null?null:Lifetime.Token,Id=Lifetime==null?null:Lifetime.UserId;
            try {
                BarrelResearchCheck(EntityMeleeCandidateAvailable()&&CurrentGameplayPlayer(Actor,Lifetime)!=null,"qualified source and original actor survived next frame");
                EntityMeleeFixtureCase(Actor,Connection,Weapon,Lifetime,"next_frame_rpc");
                BarrelResearchCheck(EntityMeleeAvailable()==EntityMeleeDeploymentQualified&&EntityMeleeFrames.Count==0,"deployment approval unchanged and no retained source frames");
                EntityMeleeFixtureCase(Actor,Connection,Weapon,Lifetime,"queued_before_disconnect",true);
                BarrelResearchCheck(EntityMeleeFixtureFailure==null,"no swallowed observation receipt failure");
                Passed=true;
            } catch(Exception Error) {
                EntityMeleeInitialized=false;
                EntityMeleeFixtureFailure=Error.GetType().Name+": "+Error.Message;
                Puts("[CarbonLuau:EntityMeleeFixture] FAIL final "+Error.GetType().Name+": "+Error.Message);
            } finally {
                bool Clean=CleanupEntityMeleeQualification(Connection);
                EntityMeleeFixturePassed=Passed&&Clean&&EntityMeleeFixtureFailure==null;
                if(EntityMeleeFixturePassed)Puts("[CarbonLuau:EntityMeleeFixture] PASS actual RPC source/terminal proof and cleanup; original token="+Token+" id="+Id+"; deployment_approved="+EntityMeleeDeploymentQualified+"; client authentication UNQUALIFIED");
            }
        }
        private void EntityMeleeFixtureCase(BasePlayer Actor,Network.Connection Connection,BaseMelee Weapon,PlayerLifetime Lifetime,string Label,bool DisconnectBeforeDrain=false)
        {
            int LedgerStart=BarrelResearchLedgerCount;
            var Case=BarrelResearchCreate(Label,BarrelResearchMode.Vanilla,Actor.transform.position+Vector3.forward,true);
            try {
            Case.Source.health=0.01f; EntityMeleeFixtureVictim=Case.Source;
            EntityMeleeFixtureVictimId=Case.Source.net.ID.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EntityMeleeFixtureExpectedToken=Lifetime.Token;EntityMeleeFixtureTerminals=0;
            string VictimId=EntityMeleeFixtureVictimId;
            string WeaponLabel=EntityDeathPrefab.GetValue(Weapon) as string;
            BarrelResearchCheck(WeaponLabel!=null&&WeaponLabel.Length<=512&&WeaponLabel.StartsWith("assets/",StringComparison.Ordinal)&&
                WeaponLabel.EndsWith(".prefab",StringComparison.Ordinal)&&FacadePolicy.Utf8.GetByteCount(WeaponLabel)<=512,"original cached canonical weapon label");
            EntityMeleeFixtureSnapshots=0;EntityMeleeFixtureToken=EntityMeleeFixtureId=null;
            long Before=EntityDeathCompleted;
            Weapon.ResetAttackCooldown();
            EntityMeleeFixturePacket(Actor,Connection,Weapon,Case.Source);
            BarrelResearchCheck(EntityMeleeFixtureFailure==null,"candidate/terminal receipt failures were not swallowed");
            BarrelResearchCheck(EntityDeathCompleted==Before+1&&EntityMeleeFixtureSnapshots==1&&EntityMeleeFixtureTerminals==1&&
                EntityMeleeFixtureToken==Lifetime.Token&&EntityMeleeFixtureId==Lifetime.UserId,"original RPC reached actual armed death capture with exact human connection");
            BarrelResearchCheck(Case.Source.IsDestroyed||Case.Source.lifestate==BaseCombatEntity.LifeState.Dead,"physical terminal death completed");
            BarrelResearchCheck(EntityMeleeFrames.Count==0,"RPC finalizer released every borrowed source reference");
            if(DisconnectBeforeDrain) {
                // The real RPC and completed death precede retirement. Preserve
                // the original snapshot; never construct a source stamp or rebind.
                OnPlayerDisconnected(Actor,"melee queued original-connection fixture");
                Connection.connected=Connection.active=false;
                Actor.SetPlayerFlag(BasePlayer.PlayerFlags.Connected,false);
                BarrelResearchCheck(CurrentGameplayPlayer(Actor)==null,"original actor connection retired before delivery");
                // Reuse only the owned actor/account, with a genuinely NEW server
                // connection. The already queued death keeps its old RPC witness.
                EntityMeleeFixtureReconnect=new Network.Connection{userid=Actor.userID,username=Actor.displayName,
                    connected=true,active=true,state=(Network.Connection.State)4,canReserveSlot=false};
                EntityMeleeFixtureReconnect.player=Actor;
                EntityMeleeFixtureSetConnection(Actor,EntityMeleeFixtureReconnect);
                Actor.SetPlayerFlag(BasePlayer.PlayerFlags.Connected,true);
                OnPlayerConnected(Actor);
                var Fresh=CurrentGameplayPlayer(Actor);
                BarrelResearchCheck(Fresh!=null&&Fresh.Token!=Lifetime.Token&&Fresh.UserId==Lifetime.UserId&&
                    ReferenceEquals(Fresh.Connection,EntityMeleeFixtureReconnect)&&!ReferenceEquals(Fresh.Connection,Connection),
                    "same account now has a distinct actual D11 connection/token before old callback delivery");
            }
            EntityMeleeFixturePublicContext(VictimId,WeaponLabel,Lifetime,DisconnectBeforeDrain);
            // Qualification fixture observation must inspect main's delivered
            // actor fields separately; source witness alone is not API delivery.
            Puts("[CarbonLuau:EntityMeleeFixture] TERMINAL label="+Label+" deployment_approved="+EntityMeleeDeploymentQualified+" token="+EntityMeleeFixtureToken+" id="+EntityMeleeFixtureId);
            EntityMeleeFixtureSnapshots=0;
            } finally { ReleaseEntityMeleeFixtureCase(Case,LedgerStart,Weapon); }
        }
        private static string EntityMeleeFixtureLuaText(string Value)
        {
            if(Value==null||Value.Length>512)throw new InvalidOperationException("fixture Lua scalar bound");
            byte[] Bytes=FacadePolicy.Utf8.GetBytes(Value);if(Bytes.Length>512)throw new InvalidOperationException("fixture Lua UTF-8 bound");
            var Result=new StringBuilder("\"");
            foreach(byte ValueByte in Bytes)Result.Append('\\').Append(ValueByte.ToString("D3",System.Globalization.CultureInfo.InvariantCulture));
            return Result.Append('"').ToString();
        }
        private void EntityMeleeFixturePublicContext(string VictimId,string WeaponLabel,PlayerLifetime Lifetime,bool Disconnected)
        {
            foreach(var Result in Host.Drain())BarrelResearchCheck(Result.Status==RuntimeStatus.OK,"actual deferred melee callback status: "+Result.Error);
            string Source="local C=require('state').Deaths["+EntityMeleeFixtureLuaText(VictimId)+
                "]; assert(C and table.isfrozen(C)); assert(C.Id=="+EntityMeleeFixtureLuaText(VictimId)+
                " and C.DamageType=='Slash'); assert(type(C.Weapon)=='string' and #C.Weapon<=512 and C.Weapon=="+
                EntityMeleeFixtureLuaText(WeaponLabel)+"); ";
            if(EntityMeleeDeploymentQualified) {
                string Id=EntityMeleeFixtureLuaText(Lifetime.UserId),Name=EntityMeleeFixtureLuaText(Lifetime.Name);
                Source+="assert(C.Killer and C.KillerId=="+Id+" and C.Killer.UserId=="+Id+" and C.Killer.Name=="+Name+"); ";
                if(Disconnected)Source+="assert(C.Killer.IsConnected==false); ";
            } else Source+="assert(C.Killer==nil and C.KillerId==nil); ";
            var Check=Host.Execute("melee.public.context",Source);
            BarrelResearchCheck(Check.Status==RuntimeStatus.OK,"frozen public melee context and original actor snapshot: "+Check.Error);
            Puts("[CarbonLuau:EntityMeleeFixture] PUBLIC PASS id="+VictimId+" slash/canonical_weapon/frozen deployment_approved="+
                EntityMeleeDeploymentQualified+" original_actor_disconnected="+Disconnected);
        }
        private void EntityMeleeFixtureNegative(BasePlayer Actor,BaseMelee Weapon,string Label,bool Lookup)
        {
            int LedgerStart=BarrelResearchLedgerCount;
            var Case=BarrelResearchCreate(Label,BarrelResearchMode.Vanilla,Actor.transform.position+Vector3.right,true);
            try {
            EntityMeleeFixtureVictim=Case.Source;EntityMeleeFixtureSnapshots=0;
            EntityMeleeFixtureVictimId=Case.Source.net.ID.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EntityMeleeFixtureExpectedToken=null;EntityMeleeFixtureTerminals=0;
            BasePlayer Initiator=Lookup?BasePlayer.FindByID(Actor.userID):Actor;
            BarrelResearchCheck(ReferenceEquals(Initiator,Actor),"negative still has a valid current human field");
            long Before=EntityDeathCompleted;Case.Source.Hurt(10000f,Rust.DamageType.Explosion,Initiator,false);
            BarrelResearchCheck(EntityMeleeFixtureFailure==null,"unqualified source terminal receipt has nil actor fields");
            BarrelResearchCheck(EntityDeathCompleted==Before+1&&EntityMeleeFixtureSnapshots==0&&EntityMeleeFixtureTerminals==1,"unqualified human source produces death but no melee actor certificate");
            } finally { ReleaseEntityMeleeFixtureCase(Case,LedgerStart,Weapon); }
        }
        private void EntityMeleeFixtureUnstamped(BasePlayer Actor,Network.Connection Connection,BaseMelee Weapon)
        {
            int LedgerStart=BarrelResearchLedgerCount;
            var Case=BarrelResearchCreate("melee_fingerprint_without_rpc",BarrelResearchMode.Vanilla,Actor.transform.position+Vector3.forward,true);
            HitInfo Hit=null;
            try {
            Case.Source.health=0.01f;EntityMeleeFixtureVictim=Case.Source;EntityMeleeFixtureSnapshots=0;
            EntityMeleeFixtureVictimId=Case.Source.net.ID.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EntityMeleeFixtureExpectedToken=null;EntityMeleeFixtureTerminals=0;
            Hit=new HitInfo{Initiator=Actor,Predicted=Connection,Weapon=Weapon,WeaponPrefab=Weapon,HitEntity=Case.Source,DidHit=true};
            long Before=EntityDeathCompleted;Weapon.DoAttackShared(Hit);
            BarrelResearchCheck(EntityMeleeFixtureFailure==null,"unstamped source terminal receipt has nil actor fields");
            BarrelResearchCheck(EntityDeathCompleted==Before+1&&EntityMeleeFixtureSnapshots==0&&EntityMeleeFixtureTerminals==1,"matching human/connection/weapon fields without original RPC are nil");
            } finally { if(Hit!=null)Hit.Clear();ReleaseEntityMeleeFixtureCase(Case,LedgerStart,Weapon); }
        }
        private void ReleaseEntityMeleeFixtureCase(BarrelResearchCase Case,int Start,BaseMelee Weapon)
        {
            // Same exact-owned Kill/Item cleanup premises as the shared fixture,
            // applied only to the trailing records acquired by this one case.
            BarrelResearchCheck(System.Threading.Thread.CurrentThread.ManagedThreadId==BarrelResearchThread&&Host!=null&&!Host.Busy&&
                !BarrelResearchCleaning&&BarrelResearchDropDepth==0&&BarrelResearchBonusDepth==0&&
                Case!=null&&ReferenceEquals(BarrelResearchCases[0],Case)&&Start>=0&&Start<BarrelResearchLedgerCount&&
                BarrelResearchLedgerCount<=BarrelResearchLimit&&BarrelResearchTraceCount<=BarrelResearchTrace.Length,"bounded exact case cleanup scope");
            Item Tool=EntityMeleeFixtureTool;int End=BarrelResearchLedgerCount;
            BarrelResearchCheck(Tool!=null,"preserve explicitly acquired original fixture tool");
            for(int Index=Start;Index<End;Index++) {
                var Resource=BarrelResearchLedger[Index];
                BarrelResearchCheck(!ReferenceEquals(Resource.Entity,BarrelResearchActor)&&!ReferenceEquals(Resource.Entity,Weapon)&&
                    (Resource.Item==null||!ReferenceEquals(Resource.Item,Tool)),"case cleanup excludes original actor/weapon/tool");
            }
            BarrelResearchCleaning=true;bool Failed=false;
            try {
                for(int Index=End-1;Index>=Start;Index--) {
                    var Resource=BarrelResearchLedger[Index];if(ReferenceEquals(Resource.Entity,null))continue;
                    try {
                        if(Resource.Entity!=null&&!Resource.Entity.IsDestroyed)Resource.Entity.Kill();
                        BarrelResearchCheck(Resource.Id==0||!ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Resource.Id)),Resource.Entity),"case world removed with original identity");
                        BarrelResearchCheck(Resource.Entity==null||Resource.Entity.net==null,"case world network authority released");
                    } catch(Exception Error){Failed=true;EntityMeleeFixtureFailure="case world cleanup: "+Error.Message;}
                }
                for(int Index=Start;Index<End;Index++) {
                    var Resource=BarrelResearchLedger[Index];Item Value=Resource.Item;if(Value==null)continue;
                    try {
                        BarrelResearchCheck(!Resource.OwnershipUnknown,"case Item ownership must be known");
                        if(Value.uid.Value==0)continue; // Never free a pooled zero-UID record twice.
                        BarrelResearchCheck(Value.uid.Value==Resource.ItemUid&&Value.info!=null,"case cleanup original Item UID/definition");
                        if(Value.IsValid()) {
                            BarrelResearchCheck(Value.parent==null&&ReferenceEquals(Value.GetWorldEntity(),null),"case cleanup only unowned Item");
                            Value.Remove(0);BarrelResearchCheck(Value.IsRemoved(),"case Item removal accepted");
                        }
                    } catch(Exception Error){Failed=true;EntityMeleeFixtureFailure="case Item cleanup: "+Error.Message;}
                }
                try{ItemManager.DoRemoves();}catch(Exception Error){Failed=true;EntityMeleeFixtureFailure="case removal drain: "+Error.Message;}
                BarrelResearchCheck(!Failed&&BarrelResearchLedgerCount==End,"case cleanup complete; actor/weapon ledger untouched");
                Array.Clear(BarrelResearchLedger,Start,End-Start);BarrelResearchLedgerCount=Start;
                int Kept=0;
                for(int Index=0;Index<BarrelResearchTraceCount;Index++)
                    if(!ReferenceEquals(BarrelResearchTrace[Index].Source,Case))BarrelResearchTrace[Kept++]=BarrelResearchTrace[Index];
                Array.Clear(BarrelResearchTrace,Kept,BarrelResearchTraceCount-Kept);BarrelResearchTraceCount=Kept;
                BarrelResearchCases[0]=null;if(ReferenceEquals(BarrelResearchCurrent,Case))BarrelResearchCurrent=null;
                Array.Clear(Case.Stored,0,Case.Stored.Length);Array.Clear(Case.Replacement,0,Case.Replacement.Length);
                Case.Source=null;Case.Container=null;Case.Binding=null;
                EntityMeleeFixtureVictim=null;EntityMeleeFixtureVictimId=EntityMeleeFixtureExpectedToken=null;
                BarrelResearchCheck(BarrelResearchActor!=null&&!BarrelResearchActor.IsDestroyed&&Weapon!=null&&!Weapon.IsDestroyed,
                    "original actor/weapon remain live after case cleanup");
            } finally {BarrelResearchCleaning=false;}
        }
        private void EntityMeleeFixturePacket(BasePlayer Actor,Network.Connection Connection,BaseMelee Weapon,BaseEntity Victim)
        {
            var Write=new Network.NetWrite();var Read=new Network.NetRead();
            try {
                Vector3 Start=Actor.eyes.position,Point=Victim.ClosestPoint(Start);
                var Attack=new ProtoBuf.Attack{ShouldPool=false,hitID=Victim.net.ID,pointStart=Start,pointEnd=Point,
                    hitPositionWorld=Point,hitPositionLocal=Victim.transform.InverseTransformPoint(Point),hitNormalWorld=Vector3.up,
                    hitNormalLocal=Victim.transform.InverseTransformDirection(Vector3.up),hitMaterialID=StringPool.Get("Metal")};
                var Packet=new ProtoBuf.PlayerAttack{ShouldPool=false,attack=Attack,projectileID=0};
                BarrelResearchCheck(Write.Start(null),"actual NetWrite buffer start");Write.Proto(Packet);
                var Buffer=Write.GetBuffer();BarrelResearchCheck(Buffer.Item2>0&&Buffer.Item2<=4096,"bounded actual protobuf packet");
                byte[] Bytes=new byte[Buffer.Item2];Array.Copy(Buffer.Item1,Bytes,Bytes.Length);
                BarrelResearchCheck(Read.Start(Connection,new Span<byte>(Bytes)),"actual NetRead carries original connection");
                Weapon.PlayerAttack(new BaseEntity.RPCMessage{player=Actor,connection=Connection,read=Read});
            } finally{Read.EnterPool();Write.EnterPool();}
        }
        private bool CleanupEntityMeleeQualification(Network.Connection Connection)
        {
            bool Clean=true;
            try {
                EntityMeleeFixtureVictim=null;
                EntityMeleeFixtureVictimId=EntityMeleeFixtureExpectedToken=EntityMeleeFixtureToken=EntityMeleeFixtureId=null;
                if(BarrelResearchActor!=null) {
                    OnPlayerDisconnected(BarrelResearchActor,"owned melee fixture cleanup");
                    BarrelResearchActor.SetPlayerFlag(BasePlayer.PlayerFlags.Connected,false);
                    if(BarrelResearchActor.net!=null)EntityMeleeFixtureSetConnection(BarrelResearchActor,null);
                }
            } catch(Exception Error){Clean=false;EntityMeleeFixtureFailure="connection cleanup: "+Error.Message;Puts("[CarbonLuau:EntityMeleeFixture] CLEANUP FAIL "+Error.GetType().Name+": "+Error.Message);}
            finally {
                if(Connection!=null){Connection.connected=Connection.active=false;Connection.player=null;}
                if(EntityMeleeFixtureReconnect!=null){EntityMeleeFixtureReconnect.connected=EntityMeleeFixtureReconnect.active=false;EntityMeleeFixtureReconnect.player=null;EntityMeleeFixtureReconnect=null;}
                try {
                    // OnPlayerDisconnected retires the facade, not Rust's explicit
                    // fixture indexes. Remove only this exact original wrapper,
                    // before Kill/ResetState can reset its account identity.
                    BasePlayer OwnedActor=BarrelResearchActor;
                    if(!ReferenceEquals(OwnedActor,null)) {
                        int Count=BasePlayer.activePlayerList.Count;
                        BarrelResearchCheck(Count<=1,"isolated owned actor index cleanup bound");
                        for(int Index=Count-1;Index>=0;Index--)
                            if(ReferenceEquals(BasePlayer.activePlayerList[Index],OwnedActor))BasePlayer.activePlayerList.RemoveAt(Index);
                        BasePlayer Found;
                        if(BasePlayer.activePlayerLookup.TryGetValue(OwnedActor.userID,out Found)&&ReferenceEquals(Found,OwnedActor))
                            BasePlayer.activePlayerLookup.Remove(OwnedActor.userID);
                    }
                    BarrelResearchCleanup();
                    BarrelResearchCheck(BarrelResearchLedgerCount==0&&BarrelResearchActor==null&&EntityMeleeFrames.Count==0,"all owned resources and source references released");
                    EntityMeleeFixtureTool=null;
                } catch(Exception Error){Clean=false;EntityMeleeFixtureFailure="scene cleanup: "+Error.Message;Puts("[CarbonLuau:EntityMeleeFixture] CLEANUP FAIL "+Error.GetType().Name+": "+Error.Message);}
                if(!Clean){EntityMeleeInitialized=false;EntityMeleeFixturePassed=false;}
                EntityMeleeFixtureRunning=false;
            }
            return Clean;
        }
        private static void EntityMeleeFixtureSetConnection(BasePlayer Actor,Network.Connection Connection)
        {
            // The exact vendor property setter is nonpublic before Carbon's
            // publicization. Invoke that setter, not an invented backing field.
            var Property=typeof(Network.Networkable).GetProperty("connection",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            var Setter=Property==null?null:Property.GetSetMethod(true);
            if(Setter==null||Property.PropertyType!=typeof(Network.Connection))throw new InvalidOperationException("fixture connection setter shape drift");
            Setter.Invoke(Actor.net,new object[]{Connection});
        }
    }
}
