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
            internal const int GlobalRules = 2, DomainRules = 2, Entries = 8;
            // Two committed owners plus the serialized pending candidate.
            // This bounds retained descriptors even before conflict preflight.
            internal const int RetainedRuleSessions = GlobalRules + 1;
            internal const int RetainedRules = RetainedRuleSessions * DomainRules;
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
                    // BEGIN GENERATED LOOT ITEMS
                    // Manifest SHA256: 37688a539718d8f80879b66bc88e9594bf90c37cf4d23f7425ac6813f45a47e8
                    if (Value.Item != "2module.car" &&
                        Value.Item != "3module.car" &&
                        Value.Item != "4module.car" &&
                        Value.Item != "ammo.paintball" &&
                        Value.Item != "ammo.pistol" &&
                        Value.Item != "ammo.pistol.hv" &&
                        Value.Item != "ammo.rifle" &&
                        Value.Item != "ammo.rifle.hv" &&
                        Value.Item != "ammo.rocket.hv" &&
                        Value.Item != "ammo.rocket.sam" &&
                        Value.Item != "ammo.shotgun" &&
                        Value.Item != "antiradpills" &&
                        Value.Item != "apple.spoiled" &&
                        Value.Item != "arrow.bone" &&
                        Value.Item != "arrow.fire" &&
                        Value.Item != "arrow.hv" &&
                        Value.Item != "arrow.wooden" &&
                        Value.Item != "attire.nesthat" &&
                        Value.Item != "ball" &&
                        Value.Item != "barrelcostume" &&
                        Value.Item != "battery.small" &&
                        Value.Item != "bdu.pants" &&
                        Value.Item != "bdu.shirt" &&
                        Value.Item != "bearmeat.burned" &&
                        Value.Item != "bicycle" &&
                        Value.Item != "bleach" &&
                        Value.Item != "blood" &&
                        Value.Item != "bluedogtags" &&
                        Value.Item != "blueidtag" &&
                        Value.Item != "blueprintbase" &&
                        Value.Item != "bone.fragments" &&
                        Value.Item != "boots.frog" &&
                        Value.Item != "bottle.vodka" &&
                        Value.Item != "bread.loaf" &&
                        Value.Item != "burlap.gloves" &&
                        Value.Item != "burlap.headwrap" &&
                        Value.Item != "burlap.shirt" &&
                        Value.Item != "burlap.shoes" &&
                        Value.Item != "burlap.trousers" &&
                        Value.Item != "caboose" &&
                        Value.Item != "can.beans" &&
                        Value.Item != "can.beans.empty" &&
                        Value.Item != "can.tuna" &&
                        Value.Item != "can.tuna.empty" &&
                        Value.Item != "candycane" &&
                        Value.Item != "cannonball" &&
                        Value.Item != "captainslog" &&
                        Value.Item != "charcoal" &&
                        Value.Item != "chicken.burned" &&
                        Value.Item != "chicken.spoiled" &&
                        Value.Item != "chocolate" &&
                        Value.Item != "clatter.helmet" &&
                        Value.Item != "cloth" &&
                        Value.Item != "coal" &&
                        Value.Item != "coconut" &&
                        Value.Item != "coolingtea" &&
                        Value.Item != "cratecostume" &&
                        Value.Item != "crude.oil" &&
                        Value.Item != "dart.radiation" &&
                        Value.Item != "dart.scatter" &&
                        Value.Item != "dart.wood" &&
                        Value.Item != "deermeat.burned" &&
                        Value.Item != "diesel_barrel" &&
                        Value.Item != "diving.fins" &&
                        Value.Item != "diving.mask" &&
                        Value.Item != "diving.wetsuit" &&
                        Value.Item != "documents" &&
                        Value.Item != "dogtagneutral" &&
                        Value.Item != "door.key" &&
                        Value.Item != "draculacape" &&
                        Value.Item != "draculamask" &&
                        Value.Item != "ducttape" &&
                        Value.Item != "easter.goldegg" &&
                        Value.Item != "explosives" &&
                        Value.Item != "fat.animal" &&
                        Value.Item != "fertilizer" &&
                        Value.Item != "fish.anchovy" &&
                        Value.Item != "fish.catfish" &&
                        Value.Item != "fish.herring" &&
                        Value.Item != "fish.minnows" &&
                        Value.Item != "fish.salmon" &&
                        Value.Item != "fish.sardine" &&
                        Value.Item != "fish.smallshark" &&
                        Value.Item != "fish.spoiled" &&
                        Value.Item != "fish.troutsmall" &&
                        Value.Item != "fishing.tackle" &&
                        Value.Item != "gears" &&
                        Value.Item != "ghostsheet" &&
                        Value.Item != "gingerbreadsuit" &&
                        Value.Item != "glue" &&
                        Value.Item != "granolabar" &&
                        Value.Item != "grayidtag" &&
                        Value.Item != "greenidtag" &&
                        Value.Item != "grub" &&
                        Value.Item != "gunpowder" &&
                        Value.Item != "hab" &&
                        Value.Item != "halloween.candy" &&
                        Value.Item != "harvestingtea" &&
                        Value.Item != "hat.beenie" &&
                        Value.Item != "hat.boonie" &&
                        Value.Item != "hat.bunnyhat" &&
                        Value.Item != "hat.cap" &&
                        Value.Item != "hat.dragonmask" &&
                        Value.Item != "hat.horsemask" &&
                        Value.Item != "hat.oxmask" &&
                        Value.Item != "hat.rabbitmask" &&
                        Value.Item != "hat.ratmask" &&
                        Value.Item != "hat.snakemask" &&
                        Value.Item != "hat.tigermask" &&
                        Value.Item != "hat.wellipets" &&
                        Value.Item != "hat.wolf" &&
                        Value.Item != "healingtea" &&
                        Value.Item != "healingtea.pure" &&
                        Value.Item != "honey" &&
                        Value.Item != "honeycomb" &&
                        Value.Item != "hoodie" &&
                        Value.Item != "horse" &&
                        Value.Item != "horsedung" &&
                        Value.Item != "hq.metal.ore" &&
                        Value.Item != "jacket.snow" &&
                        Value.Item != "jar.pickle" &&
                        Value.Item != "kickgems" &&
                        Value.Item != "largemedkit" &&
                        Value.Item != "lavenderidtag" &&
                        Value.Item != "leather" &&
                        Value.Item != "locomotive" &&
                        Value.Item != "lowgradefuel" &&
                        Value.Item != "mask.balaclava" &&
                        Value.Item != "mask.bandana" &&
                        Value.Item != "maxhealthtea" &&
                        Value.Item != "metal.fragments" &&
                        Value.Item != "metal.ore" &&
                        Value.Item != "metal.refined" &&
                        Value.Item != "metalblade" &&
                        Value.Item != "metalpipe" &&
                        Value.Item != "metalspring" &&
                        Value.Item != "minicopter" &&
                        Value.Item != "mintidtag" &&
                        Value.Item != "mlrs" &&
                        Value.Item != "motorbike" &&
                        Value.Item != "mrspice.can" &&
                        Value.Item != "mummymask" &&
                        Value.Item != "note" &&
                        Value.Item != "nucleus" &&
                        Value.Item != "orangeidtag" &&
                        Value.Item != "orchid" &&
                        Value.Item != "oretea" &&
                        Value.Item != "oretea.advanced" &&
                        Value.Item != "oretea.pure" &&
                        Value.Item != "pants" &&
                        Value.Item != "pants.shorts" &&
                        Value.Item != "paper" &&
                        Value.Item != "partyhat" &&
                        Value.Item != "pinkidtag" &&
                        Value.Item != "plantfiber" &&
                        Value.Item != "prisonerhood" &&
                        Value.Item != "propanetank" &&
                        Value.Item != "ptboat" &&
                        Value.Item != "purecoolingtea" &&
                        Value.Item != "purewarmingtea" &&
                        Value.Item != "purpleidtag" &&
                        Value.Item != "reddogtags" &&
                        Value.Item != "redidtag" &&
                        Value.Item != "researchpaper" &&
                        Value.Item != "rhib" &&
                        Value.Item != "riflebody" &&
                        Value.Item != "roadsign.gloves" &&
                        Value.Item != "roadsigns" &&
                        Value.Item != "rope" &&
                        Value.Item != "rose" &&
                        Value.Item != "santabeard" &&
                        Value.Item != "santahat" &&
                        Value.Item != "scarecrow.suit" &&
                        Value.Item != "scarecrowhead" &&
                        Value.Item != "scrap" &&
                        Value.Item != "scraptea" &&
                        Value.Item != "scraptea.pure" &&
                        Value.Item != "sedan" &&
                        Value.Item != "semibody" &&
                        Value.Item != "sewingkit" &&
                        Value.Item != "sheetmetal" &&
                        Value.Item != "shirt.collared" &&
                        Value.Item != "shirt.tanktop" &&
                        Value.Item != "shoes.boots" &&
                        Value.Item != "skull.human" &&
                        Value.Item != "skull.wolf" &&
                        Value.Item != "smgbody" &&
                        Value.Item != "snowmobile" &&
                        Value.Item != "speargun.spear" &&
                        Value.Item != "spoiled.produce" &&
                        Value.Item != "spraycandecal" &&
                        Value.Item != "sticks" &&
                        Value.Item != "stones" &&
                        Value.Item != "submarineduo" &&
                        Value.Item != "submarinesolo" &&
                        Value.Item != "sulfur" &&
                        Value.Item != "sulfur.ore" &&
                        Value.Item != "sunflower" &&
                        Value.Item != "supertea" &&
                        Value.Item != "tactical.gloves" &&
                        Value.Item != "tarp" &&
                        Value.Item != "techparts" &&
                        Value.Item != "trike" &&
                        Value.Item != "tshirt" &&
                        Value.Item != "tshirt.long" &&
                        Value.Item != "twitch.headset" &&
                        Value.Item != "venom.snake" &&
                        Value.Item != "wagon" &&
                        Value.Item != "walkietalkie" &&
                        Value.Item != "wallpaper.wall" &&
                        Value.Item != "warmingtea" &&
                        Value.Item != "wheat" &&
                        Value.Item != "whiteidtag" &&
                        Value.Item != "wolfmeat.burned" &&
                        Value.Item != "wood" &&
                        Value.Item != "woodtea" &&
                        Value.Item != "woodtea.pure" &&
                        Value.Item != "wool" &&
                        Value.Item != "workcart" &&
                        Value.Item != "worm" &&
                        Value.Item != "yellowidtag")
                        throw new FacadeException("unsupported loot rule item");
                    // END GENERATED LOOT ITEMS
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
                RequireLootStaging(Prefab);
                if (!World.IsLootAvailable()) throw new FacadeException("loot rule source unavailable");
                World.ReserveLootSession(this);
                try {
                    LootDropRule Rule = LootRulePolicy.Validate(Prefab, Entries, World.Items);
                    // Trusted callbacks cannot grant stale or duplicate authority.
                    RequireLootStaging(Prefab);
                    if (!World.IsLootAvailable()) throw new FacadeException("loot rule source unavailable");
                    RequireLootStaging(Prefab);
                    World.ReserveLootSession(this);
                    LootRules.Add(Rule.Prefab, Rule);
                } finally {
                    if (!HasLootRule) World.ReleaseLootSession(this);
                }
            }
            private void RequireLootStaging(string Prefab)
            {
                World.Players.CheckOwner();
                // Root initialization is owned by the whole inactive session;
                // native PublicationScope construction does not send control 10.
                // Nested enlisted scopes use the existing checkpoints when present.
                if (Active || Disposed || RootPublication.Retired)
                    throw new FacadeException("loot rule declaration is initialization-only");
                if (Prefab != null && LootRules.ContainsKey(Prefab))
                    throw new FacadeException("loot rule prefab already declared by this domain");
                if (LootRules.Count >= LootRulePolicy.DomainRules)
                    throw new FacadeException("loot rule domain limit reached");
            }
            private static int LootRuleMask(SortedDictionary<string, LootDropRule> Rules)
            {
                if (Rules.Count > LootRulePolicy.DomainRules) return -1;
                int Mask = 0;
                foreach (var Entry in Rules) {
                    uint Id;
                    if (Entry.Value == null || !String.Equals(Entry.Key, Entry.Value.Prefab, StringComparison.Ordinal) ||
                        !LootRulePolicy.TryPrefabId(Entry.Key, out Id)) return -1;
                    Mask |= Id == LootRulePolicy.PrefabId ? 1 : 2;
                }
                return Mask;
            }
            internal int PublishedLootMask()
            {
                if (!Active || Disposed || RootPublication.Retired) return 0;
                var Published = FirstPublication == null ? LootRules : FirstPublication.LootRules;
                return LootRuleMask(Published);
            }
            internal LootDropRule PublishedLootRule(string Prefab)
            {
                if (!Active || Disposed || RootPublication.Retired) return null;
                var Published = FirstPublication == null ? LootRules : FirstPublication.LootRules;
                LootDropRule Rule;
                return LootRuleMask(Published) >= 0 && Prefab != null && Published.TryGetValue(Prefab, out Rule) ? Rule : null;
            }
            internal int CandidateLootMask()
            {
                int Mask = LootRuleMask(LootRules);
                if (Mask < 0) throw new FacadeException("invalid loot rule candidate set");
                return Mask;
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
            private readonly HashSet<FacadeSession> LootSessions = new HashSet<FacadeSession>();
            internal int LootRetainedSessionCount { get { return LootSessions.Count; } }
            internal void ReserveLootSession(FacadeSession Session)
            {
                Players.CheckOwner();
                if (LootSessions.Contains(Session)) return;
                if (LootSessions.Count >= LootRulePolicy.RetainedRuleSessions)
                    throw new FacadeException("loot retained candidate limit reached");
                LootSessions.Add(Session);
            }
            internal void ReleaseLootSession(FacadeSession Session)
            { Players.CheckOwner(); LootSessions.Remove(Session); }
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
                int Mask = Next.CandidateLootMask();
                if (!Object.ReferenceEquals(Active, Previous) && Active != null)
                    MergeLootMask(ref Mask, Active.PublishedLootMask());
                foreach (FacadeSession Session in Addons.Values)
                    if (!Object.ReferenceEquals(Session, Previous)) MergeLootMask(ref Mask, Session.PublishedLootMask());
            }
            private static void MergeLootMask(ref int Mask, int Other)
            {
                if (Other < 0 || (Mask & Other) != 0)
                    throw new FacadeException("loot rule prefab conflicts with a committed owner");
                Mask |= Other;
            }
            internal bool TryLootRule(string Prefab, out FacadeSession Owner, out LootDropRule Rule)
            {
                Owner = null; Rule = null;
                Players.CheckOwner();
                uint Id;
                if (!LootRulePolicy.TryPrefabId(Prefab, out Id) || !IsLootAvailable() ||
                    Addons.Count + (Active == null ? 0 : 1) > LootRulePolicy.MaximumSessions) return false;
                int Mask = 0, Current = Active == null ? 0 : Active.PublishedLootMask();
                if (Current < 0) return false;
                Mask |= Current;
                FacadeSession SelectedOwner = null;
                LootDropRule Selected = Active == null ? null : Active.PublishedLootRule(Prefab);
                if (Selected != null) SelectedOwner = Active;
                foreach (FacadeSession Session in Addons.Values) {
                    Current = Session.PublishedLootMask();
                    if (Current < 0 || (Mask & Current) != 0) return false;
                    Mask |= Current;
                    LootDropRule Candidate = Session.PublishedLootRule(Prefab);
                    if (Candidate == null) continue;
                    if (Selected != null) return false; // Corrupt/equivocal authority never chooses a winner.
                    Selected = Candidate; SelectedOwner = Session;
                }
                if (Selected == null) return false;
                Owner = SelectedOwner; Rule = Selected;
                return true;
            }
        }
    }
}
