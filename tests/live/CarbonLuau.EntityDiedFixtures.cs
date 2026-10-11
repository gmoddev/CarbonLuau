// Qualification-only extension of D3's exact-owned scene/cleanup fixture.
using System;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private int EntityDiedFixtureObservations;
        private int EntityDiedFixtureWait;
        private bool EntityDiedFixtureBoundaryStarted;
        partial void ObserveEntityDeathCandidateFixture(BaseEntity Entity, MethodBase Method)
        {
            if (!EntityDeathBoundaryRunning || EntityDeathBoundaryStep < 10 || EntityDeathBoundaryStep > 11) return;
            DestroyWatch Watch; bool Has=DestroyWatches.TryGetValue(Entity,out Watch);
            var Combat=Entity as BaseCombatEntity; MethodInfo Effective;
            bool Known=EntityDeathEffective.TryGetValue(Entity.GetType(),out Effective);
            Puts("[CarbonLuau:EntityDiedFixture] CANDIDATE method="+Method.Name+" type="+Entity.GetType().Name+
                " effective="+(Known?Effective.Name:"unknown")+" matches="+(Known&&SameEntityMethod(Effective,Method))+
                " dead="+(!ReferenceEquals(Combat,null)&&Combat.lifestate==BaseCombatEntity.LifeState.Dead)+
                " watch="+Has+" closed="+(Has&&Watch.Closed)+" seen="+(Has&&Watch.DeathSeen)+
                " epoch="+(Has&&EntityLifetimes.IsWitnessEpochUnchanged(Watch.Witness))+" fully="+Entity.IsFullySpawned()+
                " net="+(Entity.net!=null)+" stack="+EntityDeathFrames.Count);
        }
        partial void ObserveEntityDeathAttemptFixture(EntityDeathFrame Frame, string Stage, Exception Error)
        {
            if (!EntityDeathBoundaryRunning || EntityDeathBoundaryStep < 10 || EntityDeathBoundaryStep > 11) return;
            var Combat=Frame.Entity as BaseCombatEntity;
            Puts("[CarbonLuau:EntityDiedFixture] ATTEMPT stage="+Stage+" id="+Frame.Watch.Id+
                " method="+Frame.Method.Name+" marked="+Frame.Marked+" postfix="+Frame.Postfix+" original="+Frame.Original+
                " poisoned="+Frame.Poisoned+" error="+(Error==null?"none":Error.GetType().Name)+
                " epoch="+EntityLifetimes.IsWitnessEpochUnchanged(Frame.Watch.Witness)+" source="+EntityDiedAvailable()+
                " stamps="+EntityDeathTopology(Frame.CriticalMethods,Frame)+" dead="+(!ReferenceEquals(Combat,null)&&Combat.lifestate==BaseCombatEntity.LifeState.Dead)+
                " closed="+Frame.Watch.Closed+" removal="+Frame.Watch.Certified+" stack="+EntityDeathFrames.Count);
        }
        partial void ContinueEntityDeathFixture(ref bool Deferred)
        {
            Deferred = true;
            foreach (var Result in Host.Drain()) BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "death callbacks before live actor fixture");
            var Public = Host.Execute("b2.public", "assert((require('state').Died or 0)>0)");
            BarrelResearchCheck(Public.Status == RuntimeStatus.OK, "actual public EntityDied callback enters Luau: " + Public.Error);
            Puts("[CarbonLuau:EntityDiedFixture] PASS public barrel callback; immutable historical context");
            RunEntityMeleeQualification();
            NextTick(FinishEntityDeathFixture);
        }
        private void FinishEntityDeathFixture()
        {
            try {
                if (EntityMeleeFixtureRunning) {
                    BarrelResearchCheck(++EntityDiedFixtureWait <= 10, "melee fixture bounded next-turn completion");
                    NextTick(FinishEntityDeathFixture); return;
                }
                BarrelResearchCheck(EntityMeleeFixturePassed, "actual RPC melee source qualification");
                if (!EntityDiedFixtureBoundaryStarted) {
                    EntityDiedFixtureBoundaryStarted=true; EntityDiedFixtureWait=0;
                    RunEntityDeathBoundaryQualification(); NextTick(FinishEntityDeathFixture); return;
                }
                if (EntityDeathBoundaryRunning) {
                    BarrelResearchCheck(++EntityDiedFixtureWait <= 32, "terminal boundary fixture bounded owner turns and actual-frame continuation");
                    NextTick(FinishEntityDeathFixture); return;
                }
                BarrelResearchCheck(EntityDeathBoundaryPassed, "actual terminal boundaries: " + EntityDeathBoundaryFailure);
                foreach (var Result in Host.Drain()) BarrelResearchCheck(Result.Status == RuntimeStatus.OK, "deferred death callback status");
                Puts("[CarbonLuau:EntityDiedFixture] PASS bounded public transport and actual RPC source; terminal boundaries qualified");
                EndLootQualification(null);
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        partial void PrepareEntityDeathFixture() { PrepareEntityDiedQualification(); }
        partial void ObserveEntityDeathQualification(GameplayEntityDiedObservation Observation)
        {
            EntityDiedFixtureObservations++;
            Puts("[CarbonLuau:EntityDiedFixture] OBSERVE id="+Observation.Id+" prefab="+Observation.Prefab+
                " killer="+(Observation.KillerId??"nil")+" type="+(Observation.DamageType??"nil")+" weapon="+(Observation.Weapon??"nil"));
        }
        private void PrepareEntityDiedQualification()
        {
            int Matched=0;
            foreach (string Row in EntityDeathPinRows) {
                string[] Fields=Row.Split('|'); int Token=Int32.Parse(Fields[0], Fields[0].StartsWith("06",StringComparison.Ordinal)?NumberStyles.HexNumber:NumberStyles.None,CultureInfo.InvariantCulture);
                var Method=typeof(BaseEntity).Module.ResolveMethod(Token) as MethodInfo;
                byte[] Bytes=Method.GetMethodBody().GetILAsByteArray(); string HashText;
                using(var Hash=SHA256.Create()) HashText=BitConverter.ToString(Hash.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant();
                Puts("[CarbonLuau:EntityDiedFixture] PIN token="+Token+" method="+Method.DeclaringType.FullName+"."+Method.Name+" bytes="+Bytes.Length+" hash="+HashText+" matches="+EntityDeathPinned(Method));
                if(EntityDeathPinned(Method))Matched++;
            }
            Puts("[CarbonLuau:EntityDiedFixture] READY "+EntityDiedStatus+" types="+EntityDeathTypeMethods.Count+" pins="+Matched);
            BarrelResearchCheck(Matched==67&&EntityDiedAvailable(),"exact loaded EntityDied body/topology qualification");
            int Covered=0;
            foreach (var Entry in EntityDeathTypeMethods) {
                bool Valid=EntityDeathTopology(Entry.Value);
                if (Valid) Covered++;
                else Puts("[CarbonLuau:EntityDiedFixture] TOPOLOGY_REJECT type="+Entry.Key.FullName);
            }
            Puts("[CarbonLuau:EntityDiedFixture] TYPE_TOPOLOGY covered="+Covered+" total="+EntityDeathTypeMethods.Count);
            BarrelResearchCheck(Covered==EntityDeathTypeMethods.Count,"all pinned Entity families have qualified loaded death topology");
            LootFixtureSource += "; W.EntityDied:Connect(function(C) assert(table.isfrozen(C) and C.Id and C.Prefab); S.Died=(S.Died or 0)+1; S.Deaths=S.Deaths or {}; S.Deaths[C.Id]=C end)";
        }
    }
}
