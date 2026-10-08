// Test-package-only private Entity-1A lifetime exercise. Never ship this
// partial class in a production CarbonLuau package.
using System;
using System.Linq;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const string EntityFixturePrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        partial void RunEntityPrivateFixtures()
        {
            NextTick(() => {
                try { ExecuteEntityPrivateFixtures(); }
                catch (Exception Error) {
                    PrintError("[CarbonLuau:EntityPrivateFixture] FAIL " + Error.GetType().Name +
                        ": " + Error.Message);
                }
            });
        }

        private static void RequireEntityFixture(bool Condition, string Reason)
        {
            if (!Condition) throw new InvalidOperationException(Reason);
        }

        private void ExecuteEntityPrivateFixtures()
        {
            RequireEntityFixture(EntityStartupQualified && !EntityObserverBroken,
                "observer baseline unavailable");
            FacadeSession Session = Gameplay == null ? null : Gameplay.Active;
            RequireEntityFixture(Session != null && !Session.Disposed,
                "committed root facade session unavailable");
            BaseEntity Existing = BaseNetworkable.serverEntities.OfType<BaseEntity>()
                .FirstOrDefault(Value => Value != null && !Value.IsDestroyed && Value.net != null &&
                    Value.IsFullySpawned() && !String.IsNullOrEmpty(Value.PrefabName) &&
                    ReferenceEquals(BaseNetworkable.serverEntities.Find(Value.net.ID), Value));
            RequireEntityFixture(Existing != null, "no pre-existing fully spawned entity");
            EntityFacadeBinding ExistingBinding;
            RequireEntityFixture(TryAdmitEntity(Existing, Session, out ExistingBinding),
                "observed pre-existing entity did not admit");
            RequireEntityFixture(ValidateEntity(ExistingBinding),
                "pre-existing lifetime failed exact validation");
            EntityFacadeBinding ExistingAgain;
            RequireEntityFixture(TryAdmitEntity(Existing, Session, out ExistingAgain) &&
                EntityLifetimeModel.SameLifetime(ExistingBinding.Lifetime, ExistingAgain.Lifetime),
                "repeated admission did not retain exact lifetime");
            var Candidate = new FacadeSession(Gameplay, Session.VmGenerationId,
                checked(Session.DomainLifetimeId + 1000000), 32);
            EntityFacadeBinding CandidateBinding;
            RequireEntityFixture(TryAdmitEntity(Existing, Candidate, out CandidateBinding) &&
                ValidateEntity(CandidateBinding), "candidate facade binding did not admit");
            Gameplay.Retire(Candidate);
            RequireEntityFixture(!ValidateEntity(CandidateBinding) &&
                !ValidateEntity(CandidateBinding), "retired candidate facade binding revived");
            RequireEntityFixture(ValidateEntity(ExistingAgain),
                "retiring a foreign candidate must not retire the live root binding");

            BaseEntity Spawned = GameManager.server.CreateEntity(EntityFixturePrefab, new Vector3(0, 100, 0));
            RequireEntityFixture(Spawned != null, "fixture prefab unavailable");
            Spawned.enableSaving = false;
            try {
                Spawned.Spawn();
                EntityFacadeBinding First;
                RequireEntityFixture(TryAdmitEntity(Spawned, Session, out First) &&
                    ValidateEntity(First), "newly spawned entity did not admit");
                BaseNetworkable.serverEntities.UnregisterID(Spawned);
                BaseNetworkable.serverEntities.RegisterID(Spawned);
                RequireEntityFixture(ValidateEntity(First),
                    "unobserved same-object/same-epoch registry churn retargeted");
                BaseNetworkable.serverEntities.UnregisterID(Spawned);
                RequireEntityFixture(!ValidateEntity(First),
                    "observed occupancy loss did not retire token");
                BaseNetworkable.serverEntities.RegisterID(Spawned);
                RequireEntityFixture(!ValidateEntity(First),
                    "retired token revived after registry reinsertion");
            }
            finally { if (!Spawned.IsDestroyed) Spawned.Kill(); }

            BaseEntity Retry = GameManager.server.CreateEntity(EntityFixturePrefab, new Vector3(0, 100, 0));
            RequireEntityFixture(Retry != null, "retry prefab unavailable");
            Retry.enableSaving = false;
            try {
                Retry.Spawn();
                EntityFacadeBinding Old;
                RequireEntityFixture(TryAdmitEntity(Retry, Session, out Old),
                    "pre-retry entity did not admit");
                bool RetryThrew = false;
                try { Retry.Spawn(); }
                catch (Exception) { RetryThrew = true; }
                RequireEntityFixture(RetryThrew && !ValidateEntity(Old),
                    "failed same-object retry left old token valid");
                EntityFacadeBinding Unqualified;
                RequireEntityFixture(!TryAdmitEntity(Retry, Session, out Unqualified),
                    "failed retry admitted a new lifetime");
            }
            finally { if (!Retry.IsDestroyed) Retry.Kill(); }
            ExecutionResult FailedCandidate = Host.Reload("error('entity fixture candidate rejection')");
            RequireEntityFixture(FailedCandidate.Status == RuntimeStatus.RUNTIME_ERROR &&
                ValidateEntity(ExistingAgain), "failed root candidate changed entity authority");
            ExecutionResult Replaced = Host.Reload();
            RequireEntityFixture(Replaced.Status == RuntimeStatus.OK &&
                !ValidateEntity(ExistingAgain), "root replacement retained the old facade authority");
            FacadeSession FreshSession = Gameplay.Active;
            EntityFacadeBinding FreshBinding = null;
            RequireEntityFixture(FreshSession != null && !ReferenceEquals(FreshSession, Session) &&
                TryAdmitEntity(Existing, FreshSession, out FreshBinding) &&
                ValidateEntity(FreshBinding) &&
                EntityLifetimeModel.SameLifetime(ExistingAgain.Lifetime, FreshBinding.Lifetime),
                "new root domain did not reacquire the same host entity lifetime");
            ExecutionResult TimedOut = Host.Execute("entity.fixture.timeout", "while true do end");
            RequireEntityFixture(TimedOut.Status == RuntimeStatus.TIMEOUT &&
                !ValidateEntity(FreshBinding), "fatal VM timeout retained old entity facade authority");
            FacadeSession RecoveredSession = Gameplay.Active;
            EntityFacadeBinding RecoveredBinding;
            RequireEntityFixture(RecoveredSession != null &&
                !ReferenceEquals(RecoveredSession, FreshSession) &&
                TryAdmitEntity(Existing, RecoveredSession, out RecoveredBinding) &&
                ValidateEntity(RecoveredBinding) &&
                EntityLifetimeModel.SameLifetime(FreshBinding.Lifetime, RecoveredBinding.Lifetime),
                "fatal VM recovery did not preserve host lifetime with new facade authority");
            Puts("[CarbonLuau:EntityPrivateFixture] PASS pre-existing, facade authority, new Spawn, " +
                "registry churn, sticky retirement, failed same-object retry, root replacement, VM recovery");
        }
    }
}
