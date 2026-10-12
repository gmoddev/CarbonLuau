using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Private D1 limits, not a scripting API or a general loot registry.
        internal static class LootRulePolicy
        {
            internal const string Prefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab";
            internal const uint PrefabId = 966676416;
            internal const string SecondPrefab = "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab";
            internal const uint SecondPrefabId = 555882409;
            internal const int GlobalRules = 1, DomainRules = 1, Entries = 8;
            internal const int MaximumAmount = 1000, MaximumTotal = 8000, MaximumCreatedWorlds = 8;
            internal const int MaximumSessions = AddonPolicy.MaxRegistrations + 1;
            internal const int DescriptorFields = Entries * 3, DescriptorBytes = 1024;
            // The future adapter must qualify and enforce this before producer work.
            internal const int PerFrame = 8;

            internal static bool TryPrefabId(string Name, out uint Id)
            {
                if (String.Equals(Name, Prefab, StringComparison.Ordinal)) { Id = PrefabId; return true; }
                if (String.Equals(Name, SecondPrefab, StringComparison.Ordinal)) { Id = SecondPrefabId; return true; }
                Id = 0; return false;
            }

            internal static LootDropEntry[] Parse(string Descriptor)
            {
                if (Descriptor == null || Descriptor.Length > DescriptorBytes)
                    throw new FacadeException("loot rule descriptor exceeds bound");
                try {
                    if (FacadePolicy.Utf8.GetByteCount(Descriptor) > DescriptorBytes)
                        throw new FacadeException("loot rule descriptor exceeds bound");
                } catch (System.Text.EncoderFallbackException) {
                    throw new FacadeException("invalid loot rule descriptor encoding");
                }
                if (Descriptor.Length == 0) return new LootDropEntry[0];
                int Count = 1;
                foreach (char Value in Descriptor)
                    if (Value == '\t' && ++Count > DescriptorFields)
                        throw new FacadeException("loot rule descriptor field limit exceeded");
                if (Count % 3 != 0) throw new FacadeException("loot rule descriptor requires Item, Min, Max triples");
                // Both text and field counts are bounded before splitting or allocating entries.
                string[] Fields = Descriptor.Split('\t');
                var Result = new LootDropEntry[Count / 3];
                for (int Index = 0; Index < Result.Length; Index++)
                    Result[Index] = new LootDropEntry(Fields[Index * 3],
                        Amount(Fields[Index * 3 + 1]), Amount(Fields[Index * 3 + 2]));
                return Result;
            }
            private static int Amount(string Text)
            {
                if (Text.Length == 0 || Text.Length > 4 || (Text.Length > 1 && Text[0] == '0'))
                    throw new FacadeException("loot rule amount must be canonical decimal from 0 through 1000");
                int Result = 0;
                foreach (char Value in Text) {
                    if (Value < '0' || Value > '9')
                        throw new FacadeException("loot rule amount must be canonical decimal from 0 through 1000");
                    Result = Result * 10 + Value - '0';
                }
                if (Result > MaximumAmount)
                    throw new FacadeException("loot rule amount must be canonical decimal from 0 through 1000");
                return Result;
            }

            internal static LootDropRule Validate(string PrefabName, LootDropEntry[] Drops, ItemDirectory Items)
            {
                uint Id;
                if (!TryPrefabId(PrefabName, out Id))
                    throw new FacadeException("unsupported loot rule prefab");
                if (Drops == null || Drops.Length > Entries)
                    throw new FacadeException("loot rule entry limit exceeded");
                // Copy before any definition resolver can call trusted host code.
                var Input = (LootDropEntry[])Drops.Clone();
                int Total = 0;
                for (int Index = 0; Index < Input.Length; Index++) {
                    LootDropEntry Value = Input[Index];
                    FacadePolicy.ItemShortName(Value.Item);
                    if (Value.Item != "scrap" && Value.Item != "metal.fragments" && Value.Item != "wood")
                        throw new FacadeException("unsupported loot rule item");
                    if (Value.Min < 0 || Value.Max < Value.Min || Value.Max > MaximumAmount)
                        throw new FacadeException("loot rule amounts must be integers from 0 through 1000 with Min <= Max");
                    Total = checked(Total + Value.Max);
                    if (Total > MaximumTotal) throw new FacadeException("loot rule total limit exceeded");
                }
                foreach (var Entry in Input)
                    if (Items.Resolve(Entry.Item) == null) throw new FacadeException("loot rule item definition unavailable");
                // Definitions are deliberately neither copied nor retained.
                return new LootDropRule(PrefabName, Input, Total);
            }
        }

        internal readonly struct LootDropEntry
        {
            internal readonly string Item;
            internal readonly int Min, Max;
            internal LootDropEntry(string Item, int Min, int Max)
            { this.Item = Item; this.Min = Min; this.Max = Max; }
        }

        internal sealed class LootDropRule
        {
            private readonly LootDropEntry[] Entries;
            internal readonly int MaximumTotal;
            internal readonly string Prefab;
            internal int Count { get { return Entries.Length; } }
            internal LootDropEntry Entry(int Index) { return Entries[Index]; }
            internal LootDropRule(string Prefab, LootDropEntry[] Entries, int MaximumTotal)
            { this.Prefab = Prefab; this.Entries = (LootDropEntry[])Entries.Clone(); this.MaximumTotal = MaximumTotal; }
        }

        public sealed partial class FacadeSession
        {
            private SortedDictionary<string, LootDropRule> LootRules =
                new SortedDictionary<string, LootDropRule>(StringComparer.Ordinal);

            internal void StageLootRule(string Prefab, LootDropEntry[] Entries)
            {
                RequireLootStaging();
                if (!World.IsLootAvailable()) throw new FacadeException("loot rule source unavailable");
                LootDropRule Rule = LootRulePolicy.Validate(Prefab, Entries, World.Items);
                // Trusted definition/source callbacks cannot grant stale staging authority.
                RequireLootStaging();
                if (!World.IsLootAvailable()) throw new FacadeException("loot rule source unavailable");
                RequireLootStaging();
                LootRules.Add(Rule.Prefab, Rule);
            }
            private void RequireLootStaging()
            {
                World.Players.CheckOwner();
                // Root initialization is owned by the whole inactive session;
                // native PublicationScope construction does not send control 10.
                // Nested enlisted scopes use the existing checkpoints when present.
                if (Active || Disposed || RootPublication.Retired)
                    throw new FacadeException("loot rule declaration is initialization-only");
                if (LootRules.Count >= LootRulePolicy.DomainRules)
                    throw new FacadeException("loot rule domain limit reached");
            }
            internal LootDropRule PublishedLootRule()
            {
                if (!Active || Disposed || RootPublication.Retired) return null;
                var Published = FirstPublication == null ? LootRules : FirstPublication.LootRules;
                if (Published.Count != 1) return null;
                // A session still owns at most one rule, not one per prefab.
                foreach (var Entry in Published) {
                    uint Id;
                    if (Entry.Value != null && String.Equals(Entry.Key, Entry.Value.Prefab, StringComparison.Ordinal) &&
                        LootRulePolicy.TryPrefabId(Entry.Key, out Id)) return Entry.Value;
                }
                return null;
            }
            internal void CheckLootCommit(FacadeWorld ExpectedWorld)
            {
                if (!Object.ReferenceEquals(World, ExpectedWorld) || Active || Disposed || RootPublication.Retired || Publications.Count != 0)
                    throw new FacadeException("stale or incomplete loot rule candidate");
            }
            internal bool HasLootRule { get { return LootRules.Count != 0; } }
        }

        public sealed partial class FacadeWorld
        {
            // The adapter owns current exact-host qualification. Absent/throwing gate is unavailable.
            internal Func<bool> LootAvailable;
            internal bool IsLootAvailable()
            {
                Players.CheckOwner();
                try { return LootAvailable != null && LootAvailable(); }
                catch (Exception) { return false; }
            }
            internal void PreflightLootRules(FacadeSession Previous, FacadeSession Next)
            {
                Players.CheckOwner();
                // Do not add loot-specific lifetime/source/publication gates to
                // unrelated root/addon loads or removal of an outgoing rule.
                if (Next == null || !Next.HasLootRule) return;
                Next.CheckLootCommit(this);
                if (!IsLootAvailable()) throw new FacadeException("loot rule source unavailable");
                Next.CheckLootCommit(this);
                if (Previous != null && !IsActive(Previous)) throw new FacadeException("stale outgoing loot rule owner");
                int ExistingSessions = Addons.Count + (Active == null ? 0 : 1);
                if (ExistingSessions + (Previous == null ? 1 : 0) > LootRulePolicy.MaximumSessions)
                    throw new FacadeException("loot rule session inspection limit exceeded");
                int Count = 0;
                if (!Object.ReferenceEquals(Active, Previous) && Active != null && Active.PublishedLootRule() != null) Count++;
                foreach (FacadeSession Session in Addons.Values)
                    if (!Object.ReferenceEquals(Session, Previous) && Session.PublishedLootRule() != null) Count++;
                if (Count >= LootRulePolicy.GlobalRules) throw new FacadeException("loot rule prefab conflicts with a committed owner");
            }
            internal bool TryLootRule(string Prefab, out FacadeSession Owner, out LootDropRule Rule)
            {
                Owner = null; Rule = null;
                Players.CheckOwner();
                uint Id;
                if (!LootRulePolicy.TryPrefabId(Prefab, out Id) || !IsLootAvailable() ||
                    Addons.Count + (Active == null ? 0 : 1) > LootRulePolicy.MaximumSessions) return false;
                FacadeSession SelectedOwner = null;
                LootDropRule Selected = Active == null ? null : Active.PublishedLootRule();
                if (Selected != null) SelectedOwner = Active;
                foreach (FacadeSession Session in Addons.Values) {
                    LootDropRule Candidate = Session.PublishedLootRule();
                    if (Candidate == null) continue;
                    if (Selected != null) return false; // Corrupt/equivocal authority never chooses a winner.
                    Selected = Candidate; SelectedOwner = Session;
                }
                if (Selected == null || !String.Equals(Prefab, Selected.Prefab, StringComparison.Ordinal)) return false;
                Owner = SelectedOwner; Rule = Selected;
                return true;
            }
        }
    }
}
