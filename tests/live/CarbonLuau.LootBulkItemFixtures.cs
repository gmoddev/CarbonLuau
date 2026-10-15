// TEST ONLY: finite exact-registry discovery and individually isolated D8 probes.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        private const int LootBulkRegistryLimit = 4096, LootBulkRootLimit = 32, LootBulkModLimit = 32;
        private const string LootBulkQualifiedWorldPrefab = "assets/prefabs/misc/burlap sack/generic_world.prefab";
        private ItemDefinition[] LootBulkDefinitions;
        private int LootBulkIndex, LootBulkPass, LootBulkFail, LootBulkSkip;
        private bool LootBulkDiscoveryFinished;
        private MethodInfo LootBulkCycleMethod;
        private readonly Dictionary<Type, string> LootBulkModReasons = new Dictionary<Type, string>();
        private readonly Dictionary<Type, string> LootBulkMetadataReasons = new Dictionary<Type, string>();
        private readonly List<ItemDefinition> LootBulkCandidates = new List<ItemDefinition>();
        private readonly HashSet<string> LootBulkProbeSkips = new HashSet<string>(StringComparer.Ordinal);
        partial void PopulateLootBulkProbeSkips(HashSet<string> Names);
        private sealed class LootBulkItemSpec
        {
            internal readonly string Name, Family;
            internal readonly int Id, Stack;
            internal LootBulkItemSpec(string Name, int Id, int Stack, string Family)
            { this.Name = Name; this.Id = Id; this.Stack = Stack; this.Family = Family; }
        }
        private readonly List<LootBulkItemSpec> LootBulkApproved = new List<LootBulkItemSpec>();
        private int LootBulkBatchOffset, LootBulkBatchCount = 32, LootBulkPublicIndex, LootBulkPublicPhase;
        private int LootBulkPublicPass, LootBulkPublicFail, LootBulkCommonPhase;
        private bool LootBulkPublicStarted;
        private readonly object LootBulkProvider = new object(), LootBulkConflictProvider = new object();
        private string LootBulkToken;
        partial void PopulateLootBulkApproved(List<LootBulkItemSpec> Specs, ref int Offset, ref int Count);
        private readonly string[] LootBulkCallbacks = { "OnItemCreated", "OnRemove", "OnParentChanged", "OnMovedToWorld", "OnRemovedFromWorld", "OnChanged" };
        partial void SelectLootBulkItems(ref bool Items, ref bool DiscoveryOnly, ref bool ProbeOnly)
        { Items = true; SelectLootBulkMode(ref DiscoveryOnly, ref ProbeOnly); }
        partial void SelectLootBulkMode(ref bool DiscoveryOnly, ref bool ProbeOnly);
        partial void BeginLootBulkDiscovery(ref bool Deferred)
        {
            BarrelResearchCheck(BarrelResearchOnOwner() && LootCurrent == null && BarrelResearchLedgerCount == 0 &&
                ItemManager.itemList != null && ItemManager.itemList.Count <= LootBulkRegistryLimit,
                "D8 bounded exact loaded registry and isolated owner");
            LootBulkDefinitions = ItemManager.itemList.ToArray();
            PopulateLootBulkProbeSkips(LootBulkProbeSkips);
            BarrelResearchCheck(LootBulkProbeSkips.Count <= 512, "D8 bounded explicit previously-proved probe skips");
            Array.Sort(LootBulkDefinitions, (First, Second) => StringComparer.Ordinal.Compare(
                First == null ? "" : First.shortname, Second == null ? "" : Second.shortname));
            Puts("[CarbonLuau:LootFixture] D8_BATCH phase=discovery registry=" + LootBulkDefinitions.Length +
                " registry_cap=4096 definitions_per_turn=1 lifecycle_callbacks=6 public_declarations_executed=false previously_proved_skips=" + LootBulkProbeSkips.Count);
            Deferred = true; NextTick(LootBulkDiscoveryTurn);
        }
        private string LootBulkText(string Value)
        {
            if (Value == null) return "nil";
            BarrelResearchCheck(Value.Length <= 2048, "D8 bounded diagnostic scalar");
            return Value.Replace("%", "%25").Replace("\t", "%09").Replace("\n", "%0a").Replace("\r", "%0d").Replace("|", "%7c");
        }
        private string LootBulkFamily(ItemDefinition Definition)
        {
            string Category = Definition.category.ToString();
            if (Category == "Resources") return "RawResources";
            if (Category == "Component") return "Components";
            if (Category == "Food" || Category == "Medical") return "BasicConsumables";
            return "Equivalent:" + Category;
        }
        private string LootBulkFields(object Value)
        {
            var Result = new StringBuilder(); int Count = 0, Depth = 0;
            for (Type Current = Value.GetType(); Current != null && Current != typeof(UnityEngine.Object) && Current != typeof(object); Current = Current.BaseType) {
                BarrelResearchCheck(++Depth <= 8, "D8 bounded cold scalar type depth");
                var Fields = Current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                BarrelResearchCheck((Count += Fields.Length) <= 512, "D8 bounded cold scalar field count");
                Array.Sort(Fields, (First, Second) => StringComparer.Ordinal.Compare(First.Name, Second.Name));
                foreach (var Field in Fields) {
                    string Name = Field.Name;
                    bool Relevant = Name.IndexOf("repair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("condition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("held", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("redirect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("parent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("world", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("content", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        Name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!Relevant && !(Value is ItemDefinition)) Relevant = true;
                    if (!Relevant) continue;
                    object FieldValue = Field.GetValue(Value);
                    string Text = Field.FieldType.IsPrimitive || Field.FieldType.IsEnum || Field.FieldType == typeof(string)
                        ? Convert.ToString(FieldValue, System.Globalization.CultureInfo.InvariantCulture)
                        : FieldValue == null ? "nil" : "present:" + Field.FieldType.FullName +
                            (FieldValue is Array ? ":length=" + ((Array)FieldValue).Length : "");
                    if (Result.Length != 0) Result.Append(',');
                    Result.Append(LootBulkText(Current.Name + "." + Name)).Append('=').Append(LootBulkText(Text));
                    BarrelResearchCheck(Result.Length <= 8192, "D8 bounded cold scalar receipt");
                }
            }
            return Result.ToString();
        }
        private string LootBulkModReason(ItemMod Mod)
        {
            Type Type = Mod.GetType(); string Reason;
            if (LootBulkModReasons.TryGetValue(Type, out Reason)) return Reason;
            BarrelResearchCheck(LootBulkModReasons.Count < 256, "D8 finite distinct ItemMod inspection");
            Reason = ""; int Callbacks = 0;
            var Methods = typeof(ItemMod).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            BarrelResearchCheck(Methods.Length <= 128, "D8 bounded base lifecycle method set");
            foreach (var Base in Methods) {
                if (Array.IndexOf(LootBulkCallbacks, Base.Name) < 0) continue;
                var Parameters = Base.GetParameters();
                BarrelResearchCheck(Parameters.Length <= 8, "D8 bounded lifecycle signature");
                var Types = new Type[Parameters.Length];
                for (int Index = 0; Index < Types.Length; Index++) Types[Index] = Parameters[Index].ParameterType;
                var Actual = Type.GetMethod(Base.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Types, null);
                byte[] Body = Actual == null || Actual.GetMethodBody() == null ? null : Actual.GetMethodBody().GetILAsByteArray();
                bool Empty = Body != null && Body.Length == 1 && Body[0] == 0x2a;
                bool ScalarFuel = false;
                if (Type.Name == "ItemModBurnable" && Base.Name == "OnItemCreated" && Body != null && Body.Length == 13 &&
                    Body[0] == 0x03 && Body[1] == 0x02 && Body[2] == 0x7b && Body[7] == 0x7d && Body[12] == 0x2a) {
                    FieldInfo Source = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 3));
                    FieldInfo Target = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 8));
                    ScalarFuel = Source.DeclaringType == Type && Source.Name == "fuelAmount" && Source.FieldType == typeof(float) &&
                        Target.DeclaringType == typeof(Item) && Target.Name == "fuel" && Target.FieldType == typeof(float);
                }
                bool OwnedCycle = Base.Name == "OnItemCreated" && Actual != null &&
                    LootBulkCycleCreation(Type, Actual, Body);
                if ((!Empty && !ScalarFuel && !OwnedCycle) || Body == null || Body.Length > 16384 ||
                    Actual.GetMethodBody().ExceptionHandlingClauses.Count != 0) Reason = "special_lifecycle:" + Base.Name;
                string Hash = "none";
                if (Body != null && Body.Length <= 16384) using (var Digest = SHA256.Create())
                    Hash = BitConverter.ToString(Digest.ComputeHash(Body)).Replace("-", "").ToLowerInvariant();
                Puts("[CarbonLuau:LootFixture] D8_MOD_CALLBACK type=" + Type.FullName + " callback=" + Base.Name +
                    " owner=" + (Actual == null ? "nil" : Actual.DeclaringType.FullName) + " il_bytes=" + (Body == null ? -1 : Body.Length) +
                    " il_sha256=" + Hash + " ret_only=" + Empty + " scalar_fuel=" + ScalarFuel + " item_owned_static_cycle=" + OwnedCycle);
                Callbacks++;
            }
            if (Callbacks != 6) Reason = "unsupported_callback_shape";
            if (Type.Name == "ItemModFoodSpoiling") Reason = "nested_instance_data_and_global_spoil_queue";
            LootBulkModReasons.Add(Type, Reason); return Reason;
        }
        private bool LootBulkCycleCreation(Type Type, MethodInfo Actual, byte[] Body)
        {
            if (Body == null || Actual.DeclaringType != Type || (Type.Name != "ItemModCookable" && Type.Name != "ItemModCompostable")) return false;
            MethodInfo Subscribe = null;
            if (Type.Name == "ItemModCookable" && Body.Length == 19 && Body[0] == 0x03 && Body[1] == 0x02 &&
                Body[2] == 0x7b && Body[7] == 0x7d && Body[12] == 0x03 && Body[13] == 0x28 && Body[18] == 0x2a) {
                var Source = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 3));
                var Target = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 8));
                if (Source.DeclaringType != Type || Source.Name != "cookTime" || Source.FieldType != typeof(float) ||
                    Target.DeclaringType != typeof(Item) || Target.Name != "cookTimeLeft" || Target.FieldType != typeof(float)) return false;
                Subscribe = Actual.Module.ResolveMethod(BitConverter.ToInt32(Body, 14)) as MethodInfo;
            } else if (Type.Name == "ItemModCompostable" && Body.Length == 31 && Body[0] == 0x02 && Body[1] == 0x7b &&
                Body[6] == 0x22 && BitConverter.ToSingle(Body, 7) == 0 && Body[11] == 0x36 && Body[12] == 0x11 && Body[13] == 0x03 &&
                Body[14] == 0x7e && Body[19] == 0x7d && Body[24] == 0x03 && Body[25] == 0x28 && Body[30] == 0x2a) {
                var Amount = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 2));
                var Interval = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 15));
                var Target = Actual.Module.ResolveField(BitConverter.ToInt32(Body, 20));
                if (Amount.DeclaringType != Type || Amount.Name != "TotalFertilizerProduced" || Amount.FieldType != typeof(float) ||
                    Interval.DeclaringType.FullName != "ConVar.Server" || Interval.Name != "composterUpdateInterval" ||
                    !Interval.IsStatic || Interval.FieldType != typeof(float) || Target.DeclaringType != typeof(Item) ||
                    Target.Name != "cookTimeLeft" || Target.FieldType != typeof(float)) return false;
                Subscribe = Actual.Module.ResolveMethod(BitConverter.ToInt32(Body, 26)) as MethodInfo;
            }
            if (Subscribe == null || !Subscribe.IsStatic || Subscribe.DeclaringType.Name != "ItemModCookable" ||
                Subscribe.Name != "SubscribeCycleCooking" || Subscribe.ReturnType != typeof(void)) return false;
            var Parameters = Subscribe.GetParameters();
            if (Parameters.Length != 1 || Parameters[0].ParameterType != typeof(Item)) return false;
            byte[] Code = Subscribe.GetMethodBody() == null ? null : Subscribe.GetMethodBody().GetILAsByteArray();
            if (Code == null || Code.Length != 37 || Subscribe.GetMethodBody().ExceptionHandlingClauses.Count != 0 ||
                Code[0] != 0x02 || Code[1] != 0x14 || Code[2] != 0xfe || Code[3] != 0x06 || Code[8] != 0x73 ||
                (Code[13] != 0x6f && Code[13] != 0x28) || Code[18] != 0x02 || Code[19] != 0x14 || Code[20] != 0xfe ||
                Code[21] != 0x06 || Code[26] != 0x73 || (Code[31] != 0x6f && Code[31] != 0x28) || Code[36] != 0x2a) return false;
            var First = Subscribe.Module.ResolveMethod(BitConverter.ToInt32(Code, 4)) as MethodInfo;
            var Second = Subscribe.Module.ResolveMethod(BitConverter.ToInt32(Code, 22)) as MethodInfo;
            var Remove = Subscribe.Module.ResolveMethod(BitConverter.ToInt32(Code, 14));
            var Add = Subscribe.Module.ResolveMethod(BitConverter.ToInt32(Code, 32));
            var FirstCtor = Subscribe.Module.ResolveMethod(BitConverter.ToInt32(Code, 9));
            var SecondCtor = Subscribe.Module.ResolveMethod(BitConverter.ToInt32(Code, 27));
            if (First == null || Second == null || !First.Equals(Second) || !First.IsStatic || First.DeclaringType != Subscribe.DeclaringType ||
                First.Name != "CycleCooking" || First.ReturnType != typeof(void) || Remove.DeclaringType != typeof(Item) || Remove.Name != "remove_onCycle" ||
                Add.DeclaringType != typeof(Item) || Add.Name != "add_onCycle" || FirstCtor.DeclaringType != SecondCtor.DeclaringType ||
                !typeof(MulticastDelegate).IsAssignableFrom(FirstCtor.DeclaringType)) return false;
            var CycleParameters = First.GetParameters();
            if (CycleParameters.Length != 2 || CycleParameters[0].ParameterType != typeof(Item) || CycleParameters[1].ParameterType != typeof(float)) return false;
            if (LootBulkCycleMethod == null) {
                LootBulkCycleMethod = First;
                using (var Digest = SHA256.Create())
                    Puts("[CarbonLuau:LootFixture] D8_ITEM_CYCLE_PREMISE owner=original_Item.onCycle remove_then_add_same_static=true" +
                        " target=null method=" + First.DeclaringType.FullName + "." + First.Name + " subscribe_bytes=" + Code.Length +
                        " subscribe_sha256=" + BitConverter.ToString(Digest.ComputeHash(Code)).Replace("-", "").ToLowerInvariant() +
                        " mvid=" + Subscribe.Module.ModuleVersionId + " future_cooking_gameplay_unqualified=true");
            }
            return LootBulkCycleMethod.Equals(First);
        }
        private string LootBulkMetadataReason(Type Type)
        {
            string Reason;
            if (LootBulkMetadataReasons.TryGetValue(Type, out Reason)) return Reason;
            BarrelResearchCheck(LootBulkMetadataReasons.Count < 64, "D8 bounded metadata-root type inspection");
            bool Qualified = Array.IndexOf(new[] { "ItemBlueprint", "ItemModConsumable", "ItemModProjectile", "ItemModEntityReference",
                "ItemFootstepSounds", "ItemCorpseOverride", "ItemModProjectileSpawn", "ItemModProjectileRadialDamage", "ItemModProjectileDart" }, Type.Name) >= 0;
            Reason = Qualified ? "" : "unqualified_metadata_root:" + Type.FullName;
            if (Qualified) {
                string Parent = Type.Name == "ItemModProjectileSpawn" ? "ItemModProjectile" :
                    Type.Name == "ItemModProjectileRadialDamage" || Type.Name == "ItemModProjectileDart" ? "ItemModProjectileMod" : null;
                if (typeof(ItemMod).IsAssignableFrom(Type) || Type.BaseType == null ||
                    (Parent == null ? Type.BaseType != typeof(MonoBehaviour) : Type.BaseType.Name != Parent || Type.BaseType.BaseType != typeof(MonoBehaviour)))
                    Reason = "metadata_base_type_changed:" + Type.FullName;
                int Depth = 0, MethodsCount = 0;
                for (var Current = Type; Current != null && Current != typeof(MonoBehaviour); Current = Current.BaseType) {
                    BarrelResearchCheck(++Depth <= 8, "D8 bounded exact metadata inheritance path");
                    var Methods = Current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    BarrelResearchCheck(Methods.Length <= 128 && Current.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length <= 128, "D8 exact bounded metadata shape");
                    MethodsCount += Methods.Length;
                    foreach (var Method in Methods) if (Array.IndexOf(LootBulkCallbacks, Method.Name) >= 0 ||
                        Array.IndexOf(new[] { "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "Update", "FixedUpdate", "LateUpdate" }, Method.Name) >= 0)
                        Reason = "metadata_lifecycle_method:" + Current.FullName + "." + Method.Name;
                }
                Puts("[CarbonLuau:LootFixture] D8_METADATA_TYPE type=" + Type.FullName + " base=" + Type.BaseType.FullName +
                    " inspected_depth=" + Depth + " methods=" + MethodsCount + " item_mod=false create_drop_lifecycle=" + (Reason.Length == 0 ? "none" : Reason) +
                    " mvid=" + Type.Module.ModuleVersionId + " unrelated_use_gameplay_unqualified=true");
            }
            LootBulkMetadataReasons.Add(Type, Reason); return Reason;
        }
        private bool LootBulkNoDropping(ItemDefinition Definition)
        {
            Type Flags = Definition.flags.GetType();
            BarrelResearchCheck(Flags.IsEnum && Enum.IsDefined(Flags, "NoDropping"), "D8 exact loaded NoDropping flag identity");
            long Mask = Convert.ToInt64(Enum.Parse(Flags, "NoDropping", false));
            BarrelResearchCheck(Mask != 0, "D8 nonzero NoDropping flag mask");
            return (Convert.ToInt64(Definition.flags) & Mask) != 0;
        }
        private void LootBulkDiscoveryTurn()
        {
            try {
                BarrelResearchCheck(BarrelResearchOnOwner() && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D8 registry traversal retains no physical resources");
                if (LootBulkIndex >= LootBulkDefinitions.Length) {
                    LootBulkDiscoveryFinished = true; LootBulkIndex = 0;
                    Puts("[CarbonLuau:LootFixture] D8_DISCOVERY_COMPLETE definitions=" + LootBulkDefinitions.Length +
                        " simple_candidates=" + LootBulkCandidates.Count + " distinct_mod_types=" + LootBulkModReasons.Count);
                    if (LootFixtureBulkDiscoveryOnly) { LootBulkFinishProbe(); return; }
                    NextTick(LootBulkProbeTurn); return;
                }
                ItemDefinition Definition = LootBulkDefinitions[LootBulkIndex++];
                if (Definition == null) {
                    LootBulkSkip++;
                    Puts("[CarbonLuau:LootFixture] D8_DISCOVERY index=" + (LootBulkIndex - 1) + " result=SKIP reason=null_registry_definition");
                    NextTick(LootBulkDiscoveryTurn); return;
                }
                try {
                string Reason = ""; var Mods = new StringBuilder(); var Roots = new StringBuilder();
                var Components = Definition.GetComponents<Component>();
                if (Components == null || Components.Length > LootBulkRootLimit) Reason = "root_component_bound";
                else foreach (var Component in Components) {
                    if (Roots.Length != 0) Roots.Append(',');
                    Roots.Append(Component == null ? "nil" : Component.GetType().FullName);
                    if (Component == null) Reason = "missing_root_component";
                    else if (!(Component is Transform || ReferenceEquals(Component, Definition) || Component is ItemMod)) {
                        string MetadataReason = LootBulkMetadataReason(Component.GetType());
                        if (MetadataReason.Length != 0) Reason = MetadataReason;
                    }
                    if (Component is ItemMod && (Definition.itemMods == null || Array.IndexOf(Definition.itemMods, (ItemMod)Component) < 0)) Reason = "unlisted_root_mod";
                }
                if (Definition.itemMods == null || Definition.itemMods.Length > LootBulkModLimit) Reason = "item_mod_bound";
                else foreach (var Mod in Definition.itemMods) {
                    if (Mods.Length != 0) Mods.Append(',');
                    Mods.Append(Mod == null ? "nil" : Mod.GetType().FullName);
                    if (Mod == null) { Reason = "null_item_mod"; continue; }
                    string ModReason = LootBulkModReason(Mod); if (ModReason.Length != 0) Reason = ModReason;
                    Puts("[CarbonLuau:LootFixture] D8_MOD_PROPERTIES name=" + LootBulkText(Definition.shortname) +
                        " type=" + Mod.GetType().FullName + " scalar_state=" + LootBulkFields(Mod));
                }
                if (String.IsNullOrEmpty(Definition.shortname) || Definition.shortname.Length > 128 || Definition.itemid == 0 ||
                    !ReferenceEquals(ItemManager.FindItemDefinition(Definition.shortname), Definition) ||
                    !ReferenceEquals(ItemManager.FindItemDefinition(Definition.itemid), Definition)) Reason = "definition_identity";
                if (Definition.stackable < 1) Reason = "nonpositive_stack";
                if (Definition.condition.enabled) Reason = "condition_durability_or_repair";
                if (Definition.itemMods != null && Definition.itemMods.Length > 8) Reason = "adapter_item_mod_limit8";
                if (LootBulkNoDropping(Definition)) Reason = "definition_NoDropping_flag";
                // D6's retained shortname payload proof is preserved. Longer names
                // are discovered and reported, never silently charged as 15 bytes.
                if (Definition.shortname != null && Definition.shortname.Length > 15) Reason = "retained_shortname_budget15";
                if (Definition.shortname == "scrap" || Definition.shortname == "metal.fragments" || Definition.shortname == "wood" || Definition.shortname == "stones")
                    Reason = "previously_qualified_D2A_D7";
                if (Reason.Length == 0 && LootBulkProbeSkips.Contains(Definition.shortname)) Reason = "previously_proved_physical_both_hosts";
                Puts("[CarbonLuau:LootFixture] D8_DISCOVERY index=" + (LootBulkIndex - 1) + " name=" + LootBulkText(Definition.shortname) +
                    " item_id=" + Definition.itemid + " stackable=" + Definition.stackable + " category=" + Definition.category +
                    " family=" + LootBulkFamily(Definition) + " condition_enabled=" + Definition.condition.enabled +
                    " condition_state=" + LootBulkFields(Definition.condition) + " definition_state=" + LootBulkFields(Definition) +
                    " roots=" + Roots + " mods=" + Mods + " result=" + (Reason.Length == 0 ? "CANDIDATE" : "SKIP") +
                    " reason=" + (Reason.Length == 0 ? "simple_lifecycle_equivalent" : Reason));
                if (Reason.Length == 0) LootBulkCandidates.Add(Definition); else LootBulkSkip++;
                } catch (Exception Error) {
                    LootBulkFail++;
                    Puts("[CarbonLuau:LootFixture] D8_DISCOVERY index=" + (LootBulkIndex - 1) + " name=" + LootBulkText(Definition.shortname) +
                        " item_id=" + Definition.itemid + " stackable=" + Definition.stackable + " category=" + Definition.category +
                        " family=" + LootBulkFamily(Definition) + " result=FAIL reason=bounded_inspection:" + LootBulkText(Error.GetType().Name + ":" + Error.Message));
                }
                NextTick(LootBulkDiscoveryTurn);
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        private float LootBulkFuel(ItemDefinition Definition)
        {
            float Expected = 0; int Burnable = 0;
            foreach (var Mod in Definition.itemMods) if (Mod.GetType().Name == "ItemModBurnable") {
                FieldInfo Fuel = Mod.GetType().GetField("fuelAmount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                BarrelResearchCheck(Fuel != null && Fuel.FieldType == typeof(float), "D8 measured scalar fuel member");
                Expected = (float)Fuel.GetValue(Mod); Burnable++;
                BarrelResearchCheck(!Single.IsNaN(Expected) && !Single.IsInfinity(Expected) && Expected >= 0, "D8 finite scalar default fuel");
            }
            BarrelResearchCheck(Burnable <= 1, "D8 at most one measured scalar fuel initialization"); return Expected;
        }
        private float LootBulkCooking(ItemDefinition Definition, out bool Cycle)
        {
            float Expected = 0; Cycle = false;
            foreach (var Mod in Definition.itemMods) {
                if (Mod.GetType().Name == "ItemModCookable") {
                    var Field = Mod.GetType().GetField("cookTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    BarrelResearchCheck(Field != null && Field.FieldType == typeof(float), "D8 exact cookTime scalar source");
                    Expected = (float)Field.GetValue(Mod); Cycle = true;
                } else if (Mod.GetType().Name == "ItemModCompostable") {
                    var Field = Mod.GetType().GetField("TotalFertilizerProduced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    BarrelResearchCheck(Field != null && Field.FieldType == typeof(float), "D8 exact compostable branch scalar");
                    float Amount = (float)Field.GetValue(Mod);
                    if (!Single.IsNaN(Amount) && Amount > 0) {
                        var Server = typeof(Item).Assembly.GetType("ConVar.Server", true);
                        var Interval = Server.GetField("composterUpdateInterval", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        BarrelResearchCheck(Interval != null && Interval.FieldType == typeof(float), "D8 exact compostable interval source");
                        Expected = (float)Interval.GetValue(null); Cycle = true;
                    }
                }
            }
            BarrelResearchCheck(!Single.IsNaN(Expected) && !Single.IsInfinity(Expected) && Expected >= 0, "D8 finite nonnegative scalar cookTimeLeft default");
            return Expected;
        }
        private int LootBulkCycleCount(Item Value, bool Expected)
        {
            var Field = typeof(Item).GetField("onCycle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            BarrelResearchCheck(Field != null && typeof(Delegate).IsAssignableFrom(Field.FieldType), "D8 exact original Item cycle event backing field");
            var Callback = Field.GetValue(Value) as Delegate;
            var Calls = Callback == null ? new Delegate[0] : Callback.GetInvocationList();
            BarrelResearchCheck(Calls.Length <= 1, "D8 at most one Item-owned static cycle callback");
            if (Calls.Length == 1) BarrelResearchCheck(Calls[0].Target == null && LootBulkCycleMethod != null && Calls[0].Method.Equals(LootBulkCycleMethod),
                "D8 cycle callback has no extra managed/global owner and exact static method");
            BarrelResearchCheck(Calls.Length == (Expected ? 1 : 0), "D8 exact ordered definition cycle callback default"); return Calls.Length;
        }
        private void LootBulkProbeItem(ItemDefinition Definition)
        {
            BarrelResearchCheck(!LootBulkNoDropping(Definition) && Definition.itemMods != null && Definition.itemMods.Length <= 8,
                "D8 exact definition drop eligibility and unchanged production mod-count bound before Create/Drop");
            int Maximum = Math.Min(Definition.stackable, 1000);
            var Amounts = new SortedSet<int> { 1, Maximum, Math.Max(1, Maximum - 1) };
            float ExpectedFuel = LootBulkFuel(Definition); bool ExpectedCycle;
            float ExpectedCooking = LootBulkCooking(Definition, out ExpectedCycle); string WorldPrefab = null;
            float ExpectedCondition = Definition.condition.max;
            BarrelResearchCheck(!Definition.condition.enabled && !Single.IsNaN(ExpectedCondition) && !Single.IsInfinity(ExpectedCondition) && ExpectedCondition >= 0,
                "D8 exact inactive finite definition condition scalar premise");
            foreach (int Amount in Amounts) {
                Item Value = BarrelResearchNewItem(Definition.shortname, Amount); ulong Uid = Value.uid.Value;
                FieldInfo Fuel = typeof(Item).GetField("fuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                BarrelResearchCheck(Fuel != null && Fuel.FieldType == typeof(float), "D8 exact existing fuel scalar");
                BaseEntity Held = Value.GetHeldEntity(), ExistingWorld = Value.GetWorldEntity();
                var Cooking = typeof(Item).GetField("cookTimeLeft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                BarrelResearchCheck(Cooking != null && Cooking.FieldType == typeof(float), "D8 exact Item scalar cooking field");
                Puts("[CarbonLuau:LootFixture] D8_FRESH_STATE name=" + Definition.shortname + " wanted=" + Amount +
                    " actual_amount=" + Value.amount + " uid=" + Uid + " item_id=" + (Value.info == null ? 0 : Value.info.itemid) +
                    " definition_reference=" + ReferenceEquals(Value.info, Definition) + " skin=" + Value.skin + " has_condition=" + Value.hasCondition +
                    " condition=" + Value.condition + " max_condition=" + Value.maxCondition + " fuel=" + (float)Fuel.GetValue(Value) +
                    " expected_condition=" + ExpectedCondition + " expected_max_condition=" + ExpectedCondition +
                    " expected_fuel=" + ExpectedFuel + " parent=" + (Value.parent != null) + " contents=" + (Value.contents != null) +
                    " instance_data=" + (Value.instanceData != null) + " held=" + !ReferenceEquals(Held, null) +
                    " world=" + !ReferenceEquals(ExistingWorld, null) + " cook_time_left=" + (float)Cooking.GetValue(Value) +
                    " expected_cook_time_left=" + ExpectedCooking + " expected_item_cycle=" + ExpectedCycle);
                BarrelResearchCheck(Uid != 0 && ReferenceEquals(Value.info, Definition) && Value.skin == 0 && !Value.hasCondition &&
                    Value.condition == ExpectedCondition && Value.maxCondition == ExpectedCondition && (float)Fuel.GetValue(Value) == ExpectedFuel &&
                    Value.contents == null && Value.instanceData == null && ReferenceEquals(Held, null),
                    "D8 exact fresh defaults no nested/held/instance ownership");
                BarrelResearchCheck((float)Cooking.GetValue(Value) == ExpectedCooking, "D8 exact measured ordered cooking scalar initialization");
                int CycleCount = LootBulkCycleCount(Value, ExpectedCycle);
                BarrelResearchOwnership(Value, true);
                BaseEntity Entity = Value.Drop(BarrelResearchPosition, Vector3.zero); BarrelResearchHold(Entity);
                WorldItem World = Entity as WorldItem;
                BarrelResearchCheck(World != null && World.GetType() == typeof(DroppedItem) && !World.IsDestroyed && World.IsFullySpawned() &&
                    World.net != null && ReferenceEquals(BaseNetworkable.serverEntities.Find(World.net.ID), World) && Value.uid.Value == Uid &&
                    ReferenceEquals(World.item, Value) && ReferenceEquals(Value.GetWorldEntity(), World) && Value.parent == null &&
                    Value.amount == Amount && ReferenceEquals(Value.info, Definition),
                    "D8 exact ordinary reciprocal original ItemUID/world at supported quantity boundary");
                BarrelResearchOwnership(Value, false);
                Puts("[CarbonLuau:LootFixture] D8_WORLD_PROFILE name=" + Definition.shortname + " amount=" + Amount +
                    " world_type=" + World.GetType().FullName + " world_prefab=" + LootBulkText(World.PrefabName));
                BarrelResearchCheck(World.PrefabName == LootBulkQualifiedWorldPrefab && (WorldPrefab == null || World.PrefabName == WorldPrefab),
                    "D8 only previously qualified exact generic dropped-world asset; other assets require separate premise");
                WorldPrefab = World.PrefabName;
                var WorldRoots = World.GetComponents<Component>();
                BarrelResearchCheck(WorldRoots != null && WorldRoots.Length <= 16, "D8 bounded actual generic world root components");
                var WorldNames = new StringBuilder();
                foreach (var Component in WorldRoots) {
                    BarrelResearchCheck(Component != null, "D8 no missing actual world root component");
                    if (WorldNames.Length != 0) WorldNames.Append(','); WorldNames.Append(Component.GetType().FullName);
                }
                Puts("[CarbonLuau:LootFixture] D8_WORLD_ROOTS name=" + Definition.shortname + " amount=" + Amount + " roots=" + WorldNames);
                var Witness = EntityLifetimes.CaptureCompletedWitness(World);
                BarrelResearchCheck(Witness != null && ReferenceEquals(Witness.Target, World) && EntityLifetimes.IsWitnessEpochUnchanged(Witness),
                    "D8 original completed Spawn epoch authority");
                ulong Id = World.net.ID.Value;
                Puts("[CarbonLuau:LootFixture] D8_PHYSICAL name=" + Definition.shortname + " item_id=" + Definition.itemid +
                    " stackable=" + Definition.stackable + " amount=" + Amount + " uid=" + Uid + " world_id=" + Id +
                    " world_prefab=" + World.PrefabName + " epoch=" + Witness.Epoch + " reciprocal=true completed_spawn=true" +
                    " skin=0 condition=" + ExpectedCondition + " max_condition=" + ExpectedCondition + " has_condition=false fuel=" + ExpectedFuel +
                    " cook_time_left=" + ExpectedCooking + " item_cycle_callbacks=" + CycleCount +
                    " item_cycle_static_target_null=" + ExpectedCycle + " contents=false instance_data=false held=false");
                BarrelResearchCleanup();
                BarrelResearchCheck(BarrelResearchLedgerCount == 0 && !ReferenceEquals(BaseNetworkable.serverEntities.Find(new NetworkableId(Id)), World) &&
                    (Value.uid.Value == 0 || Value.uid.Value == Uid && Value.IsRemoved()), "D8 original world/Item exact cleanup converged");
                LootBulkCycleCount(Value, false);
                Puts("[CarbonLuau:LootFixture] D8_PHYSICAL_CLEAN name=" + Definition.shortname + " amount=" + Amount + " ledger=0 original_removed=true");
            }
        }
        private void LootBulkProbeTurn()
        {
            try {
                BarrelResearchCheck(LootBulkDiscoveryFinished && BarrelResearchOnOwner() && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D8 isolated candidate begins with zero owned resources");
                if (LootBulkIndex >= LootBulkCandidates.Count) { LootBulkFinishProbe(); return; }
                ItemDefinition Definition = LootBulkCandidates[LootBulkIndex++]; Exception Failure = null;
                try { LootBulkProbeItem(Definition); }
                catch (Exception Error) { Failure = Error; }
                try { BarrelResearchCleanup(); }
                catch (Exception Error) {
                    Puts("[CarbonLuau:LootFixture] D8_ITEM name=" + Definition.shortname + " item_id=" + Definition.itemid +
                        " stackable=" + Definition.stackable + " family=" + LootBulkFamily(Definition) +
                        " result=FAIL reason=exact_cleanup_blocker first=" + LootBulkText(Failure == null ? Error.Message : Failure.Message));
                    Puts("[CarbonLuau:LootFixture] D8_BATCH_STOP phase=probe definitions=" + LootBulkDefinitions.Length +
                        " candidates=" + LootBulkCandidates.Count + " attempted=" + LootBulkIndex + " pass=" + LootBulkPass +
                        " fail=" + (LootBulkFail + 1) + " skip=" + LootBulkSkip + " remaining=" + (LootBulkCandidates.Count - LootBulkIndex) +
                        " exact_cleanup_blocker=true restore_required=true complete=false");
                    throw;
                }
                if (Failure == null) LootBulkPass++; else LootBulkFail++;
                Puts("[CarbonLuau:LootFixture] D8_ITEM name=" + Definition.shortname + " item_id=" + Definition.itemid +
                    " stackable=" + Definition.stackable + " family=" + LootBulkFamily(Definition) + " result=" + (Failure == null ? "PASS" : "FAIL") +
                    " reason=" + (Failure == null ? "individual_identity_initialization_quantity_world_cleanup" : LootBulkText(Failure.GetType().Name + ":" + Failure.Message)));
                NextTick(LootBulkProbeTurn);
            } catch (Exception Error) { EndLootQualification(Error); }
        }
        private void LootBulkFinishProbe()
        {
            BarrelResearchCheck(LootBulkDiscoveryFinished && LootCurrent == null && BarrelResearchLedgerCount == 0,
                "D8 completed finite batch has zero physical resources");
            Puts("[CarbonLuau:LootFixture] PASS D8 " + (LootFixtureBulkDiscoveryOnly ? "registry discovery" : "bulk probe batch") +
                " definitions=" + LootBulkDefinitions.Length + " candidates=" + LootBulkCandidates.Count + " pass=" + LootBulkPass +
                " fail=" + LootBulkFail + " skip=" + LootBulkSkip + " per_item_receipts_authoritative=true public_declarations_executed=false ledger=0");
            EndLootQualification(null);
        }
        private void LootBulkStartPublic()
        {
            PopulateLootBulkApproved(LootBulkApproved, ref LootBulkBatchOffset, ref LootBulkBatchCount);
            BarrelResearchCheck(LootBulkApproved.Count > 0 && LootBulkApproved.Count <= 512 && LootBulkBatchOffset >= 0 &&
                LootBulkBatchOffset < LootBulkApproved.Count && LootBulkBatchCount > 0 && LootBulkBatchCount <= 32,
                "D8 exact finite pinned manifest and bounded public batch");
            string Previous = null; var Ids = new HashSet<int>();
            foreach (var Spec in LootBulkApproved) {
                BarrelResearchCheck(Spec != null && !String.IsNullOrEmpty(Spec.Name) && Spec.Name.Length <= 15 &&
                    (Previous == null || StringComparer.Ordinal.Compare(Previous, Spec.Name) < 0) && Spec.Id != 0 &&
                    Spec.Stack > 0 && Ids.Add(Spec.Id), "D8 copied unique ordinal manifest identities and retained-name bound");
                ItemDefinition Definition = ItemManager.FindItemDefinition(Spec.Name);
                BarrelResearchCheck(Definition != null && Definition.shortname == Spec.Name && Definition.itemid == Spec.Id &&
                    Definition.stackable == Spec.Stack && !LootBulkNoDropping(Definition) && Definition.itemMods != null &&
                    Definition.itemMods.Length <= 8 && ReferenceEquals(ItemManager.FindItemDefinition(Spec.Id), Definition),
                    "D8 public manifest matches exact current name/ID/stack definition");
                Previous = Spec.Name;
            }
            LootBulkBatchCount = Math.Min(LootBulkBatchCount, LootBulkApproved.Count - LootBulkBatchOffset);
            LootBulkPublicIndex = LootBulkBatchOffset; LootBulkPublicStarted = true;
            Puts("[CarbonLuau:LootFixture] D8_PUBLIC_BATCH manifest=" + LootBulkApproved.Count + " offset=" + LootBulkBatchOffset +
                " count=" + LootBulkBatchCount + " batch_cap=32 per_item_receipts_authoritative=true");
        }
        private LootDropEntry[] LootBulkEntries(LootBulkItemSpec Spec)
        { return new[] { new LootDropEntry(Spec.Name, 1, 1), new LootDropEntry(Spec.Name, Math.Min(Spec.Stack, 1000), Math.Min(Spec.Stack, 1000)) }; }
        private void LootBulkRoot(LootDropEntry[] First, LootDropEntry[] Second)
        {
            LootFixturePrefab = LootRulePolicy.Prefab; LootFixtureSecondPrefab = LootRulePolicy.SecondPrefab;
            BarrelResearchSelectedPrefab = LootFixturePrefab; LootFixtureEntries = First; LootFixtureSecondEntries = Second;
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 complete copied public root set publication");
            if (First == null) BarrelResearchCheck(Gameplay.Active.PublishedLootRule(LootRulePolicy.Prefab) == null,
                "D8 omitted first key is absent from exact root owner set");
            else LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active);
            if (Second == null) BarrelResearchCheck(Gameplay.Active.PublishedLootRule(LootRulePolicy.SecondPrefab) == null,
                "D8 omitted second key is absent from exact root owner set");
            else LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
        }
        private void LootBulkProfileRule(string Prefab, LootDropEntry[] Entries)
        {
            if (Prefab == LootRulePolicy.Prefab) LootFixtureEntries = Entries; else LootFixtureSecondEntries = Entries;
            BarrelResearchSelectedPrefab = Prefab;
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 public per-prefab amount descriptor publication");
            LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
        }
        private void LootBulkAmounts(LootBulkItemSpec Spec, string Prefab)
        {
            int Maximum = Math.Min(Spec.Stack, 1000);
            LootBulkRoot(LootBulkEntries(Spec), LootBulkEntries(Spec));
            foreach (int Amount in new[] { 0, 1, Maximum }) {
                LootBulkProfileRule(Prefab, new[] { new LootDropEntry(Spec.Name, Amount, Amount) });
                LootRegistryPayout("d8_amount_" + Spec.Name + "_" + Amount, Prefab, Amount == 0 ? 0 : 1, true);
            }
            var Repeated = new LootDropEntry[8];
            for (int Index = 0; Index < Repeated.Length; Index++) Repeated[Index] = new LootDropEntry(Spec.Name, Maximum, Maximum);
            LootBulkProfileRule(Prefab, Repeated); LootRegistryPayout("d8_repeated_eight_" + Spec.Name, Prefab, 8, true);
            Puts("[CarbonLuau:LootFixture] D8_PUBLIC_AMOUNTS name=" + Spec.Name + " prefab=" + Prefab +
                " zero=0 minimum=1 maximum=" + Maximum + " repeated=8 original_worlds_verified=true");
        }
        private LootDropEntry[] LootBulkMixed(LootBulkItemSpec Spec)
        {
            var Result = new List<LootDropEntry> { new LootDropEntry(Spec.Name, 1, 1) };
            foreach (string Family in new[] { "RawResource", "Component", "BasicMaterial", "Consumable" })
                foreach (var Other in LootBulkApproved) if (Other.Family == Family && Other.Name != Spec.Name) {
                    Result.Add(new LootDropEntry(Other.Name, 1, 1)); break;
                }
            if (Result.Count == 1) Result.Add(new LootDropEntry("scrap", 2, 2));
            Result.Add(new LootDropEntry(Spec.Name, 0, 0));
            BarrelResearchCheck(Result.Count <= 8, "D8 mixed families preserve unchanged entry budget"); return Result.ToArray();
        }
        private void LootBulkRangeMixed(LootBulkItemSpec Spec)
        {
            LootBulkRoot(LootBulkEntries(Spec), LootBulkEntries(Spec));
            foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                LootBulkProfileRule(Prefab, new[] { new LootDropEntry(Spec.Name, 1, Math.Min(Spec.Stack, 1000)) });
                LootRegistryPayout("public_range", Prefab, 1, true);
                var Mixed = LootBulkMixed(Spec); LootBulkProfileRule(Prefab, Mixed);
                LootRegistryPayout("d8_mixed_families_" + Spec.Name, Prefab, Mixed.Length - 1, true);
            }
        }
        private void LootBulkBoundary(LootBulkItemSpec Spec)
        {
            LootBulkRoot(LootBulkEntries(Spec), LootBulkEntries(Spec));
            LootRegistryPayout("d8_stack_actual_frame_prime_" + Spec.Name, LootRulePolicy.Prefab);
            BarrelResearchCheck(LootFrameNumber == Time.frameCount && LootFrameUsed > 0,
                "D8 stack controls begin after an eligible current-frame occurrence");
            int RejectedMaximum = Spec.Stack < 1000 ? Spec.Stack + 1 : 1000;
            var Entries = new[] { new LootDropEntry(Spec.Name, RejectedMaximum, RejectedMaximum) };
            LootBulkRoot(Entries, Entries);
            ItemDefinition Definition = ItemManager.FindItemDefinition(Spec.Name);
            BarrelResearchCheck(Definition != null && Definition.itemid == Spec.Id && Definition.stackable == Spec.Stack,
                "D8 exact original definition before bounded stack control");
            foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                var Case = BarrelResearchCreate("d8_stack_precommit_" + Spec.Name, BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Prefab);
                Exception FirstCause = null;
                try {
                    if (Spec.Stack >= 1000) Definition.stackable = 999;
                    Puts("[CarbonLuau:LootFixture] D8_STACK_CONTROL name=" + Spec.Name + " prefab=" + Prefab +
                        " original_stack=" + Spec.Stack + " actual_stack=" + Definition.stackable + " declared_max=" + RejectedMaximum +
                        " registration_succeeded=true expected_precommit_vanilla=true controlled_premise=" + (Spec.Stack >= 1000));
                    LootFixtureVanilla(Case);
                } catch (Exception Error) {
                    FirstCause = Error;
                    PrintError("[CarbonLuau:LootFixture] D8_FIRST_CAUSE name=" + Spec.Name + " phase=stack_control " + Error);
                } finally {
                    Definition.stackable = Spec.Stack;
                    try { if (LootFixtureOriginalSourceEpoch(Case)) LootFixtureReleaseStored(Case); }
                    catch (Exception Error) { if (FirstCause == null) FirstCause = Error; }
                    try { BarrelResearchCleanup(); }
                    catch (Exception Error) { if (FirstCause == null) FirstCause = Error; }
                }
                if (FirstCause != null) throw new InvalidOperationException("D8 exact stack control failed for " + Spec.Name, FirstCause);
            }
            LootBulkRoot(LootBulkEntries(Spec), LootBulkEntries(Spec));
        }
        private void LootBulkBag(LootBulkItemSpec Spec, string Prefab)
        {
            LootFixturePrefab = Prefab; BarrelResearchSelectedPrefab = Prefab;
            LootFixtureSecondPrefab = Prefab == LootRulePolicy.Prefab ? LootRulePolicy.SecondPrefab : LootRulePolicy.Prefab;
            LootFixtureEntries = LootFixtureSecondEntries = LootBulkEntries(Spec);
            BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 exact candidate two rules before controlled bag branch");
            LootFixtureControlledBag(LootBulkEntries(Spec));
            LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryRule(LootRulePolicy.SecondPrefab, Gameplay.Active);
            Puts("[CarbonLuau:LootFixture] D8_PUBLIC_SUPPRESSION name=" + Spec.Name + " prefab=" + Prefab +
                " stored=true bonus=true controlled_bag=true natural_bag_branch=false");
        }
        private void LootBulkPublicItem(LootBulkItemSpec Spec)
        {
            switch (LootBulkPublicPhase) {
            case 0: LootBulkRoot(LootBulkEntries(Spec), LootBulkEntries(Spec)); LootRegistryPair("d8_candidate_pair_" + Spec.Name); break;
            case 1: LootBulkAmounts(Spec, LootRulePolicy.Prefab); break;
            case 2: LootBulkAmounts(Spec, LootRulePolicy.SecondPrefab); break;
            case 3: LootBulkRangeMixed(Spec); break;
            case 4: LootBulkBoundary(Spec); break;
            case 5:
                LootBulkRoot(LootBulkEntries(Spec), LootBulkEntries(Spec));
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    BarrelResearchCreateActor(); BarrelResearchCheck(BarrelResearchBoost(), "D8 actual Scrap_Yield modifier");
                    LootRegistryPayout("d8_real_bonus_stored_suppressed_" + Spec.Name, Prefab);
                }
                break;
            case 6: LootBulkBag(Spec, LootRulePolicy.Prefab); break;
            case 7: LootBulkBag(Spec, LootRulePolicy.SecondPrefab); break;
            default: throw new InvalidOperationException("D8 unknown bounded per-item public phase");
            }
            LootRegistryRetention();
            BarrelResearchCheck(LootCurrent == null && BarrelResearchLedgerCount == 0, "D8 per-item phase exact physical convergence");
        }
        private LootDropEntry[] LootBulkCommonEntries()
        {
            var First = LootBulkApproved[LootBulkBatchOffset]; var Second = First;
            for (int Index = LootBulkBatchOffset; Index < LootBulkBatchOffset + LootBulkBatchCount; Index++)
                if (LootBulkApproved[Index].Family != First.Family) { Second = LootBulkApproved[Index]; break; }
            return new[] { new LootDropEntry(First.Name, 1, 1), new LootDropEntry(Second.Name, Math.Min(Second.Stack, 1000), Math.Min(Second.Stack, 1000)) };
        }
        private string LootBulkAddonSource()
        { return LootFixtureSingleDeclaration(LootRulePolicy.SecondPrefab, LootBulkCommonEntries()); }
        private void LootBulkAddonReplace(string Source, string Version)
        {
            string[] Result = Addons.ReplaceArchive(LootBulkProvider, LootBulkToken, LootFixturePackage("d8batchowner", Source, false, Version));
            BarrelResearchCheck(Result[0] == "OK" && Addons.ProcessOne() && Addons.Status(LootBulkProvider, LootBulkToken)[2] == "Active",
                "D8 exact per-batch addon candidate replacement");
        }
        private void LootBulkCommonStep()
        {
            switch (LootBulkCommonPhase) {
            case 0: LootBulkRoot(LootBulkCommonEntries(), LootBulkCommonEntries()); LootRegistryPair("d8_batch_common_two_rules"); break;
            case 1: LootRegistryVeto(LootRulePolicy.Prefab); LootRegistryVeto(LootRulePolicy.SecondPrefab); break;
            case 2:
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    long Before = LootCleanupFailed; LootFixtureInjectSpawn = true;
                    try { LootRegistryPayout("d8_batch_actual_incomplete_spawn", Prefab, 1, false); }
                    finally { LootFixtureInjectSpawn = false; }
                    BarrelResearchCheck(LootFixtureFailedWorld != null && LootFixtureFailedWorld.net == null &&
                        !EntityLifetimes.HasCompletedObservation(LootFixtureFailedWorld) && LootCleanupFailed == Before,
                        "D8 incomplete world has no admitted lifetime and exact original attempt cleanup");
                    LootFixtureFailedWorld = null;
                }
                LootRegistryPair("d8_batch_after_incomplete_spawn"); break;
            case 3:
            case 4:
                LootFixtureCreateFault = LootBulkCommonPhase == 3 ? 1 : 2;
                try {
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab })
                        LootRegistryPayout("d8_batch_actual_create_" + LootFixtureCreateFault, Prefab, 1, false);
                } finally { LootFixtureCreateFault = 0; }
                LootRegistryPair("d8_batch_after_actual_creation_fault"); break;
            case 5:
                LootFixtureMutateAmount = true;
                try {
                    foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab })
                        LootRegistryPayout("d8_batch_actual_amount_interference", Prefab, 2, false);
                } finally { LootFixtureMutateAmount = false; }
                LootRegistryPair("d8_batch_after_amount_interference"); break;
            case 6: {
                LootBulkRoot(LootBulkCommonEntries(), null);
                LootBulkToken = LootRegistryAdd(LootBulkProvider, "d8batchowner", LootBulkAddonSource(), "Active");
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                BarrelResearchCheck(!ReferenceEquals(Root, Addon), "D8 independent root/addon exact defining owners");
                LootRegistryAdd(LootBulkConflictProvider, "d8batchcollision", LootFixtureSingleDeclaration(LootRulePolicy.Prefab, LootBulkCommonEntries()), "Failed");
                LootFixtureSecondEntries = LootBulkCommonEntries();
                try {
                    BarrelResearchCheck(Host.Reload().Status != RuntimeStatus.OK && ReferenceEquals(Gameplay.Active, Root),
                        "D8 overlapping complete candidate set cannot partially publish");
                    LootRegistryRule(LootRulePolicy.Prefab, Root, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSecondEntries = null; }
                string Previous = LootFixtureSource;
                LootFixtureSecondEntries = LootBulkCommonEntries(); LootFixtureModuleRollback = true;
                LootFixtureSource = LootFixtureSingleDeclaration(LootRulePolicy.Prefab, LootFixtureEntries) +
                    "assert(not pcall(function() require('registrysecondfailed') end)); " + Previous;
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Root.Disposed, "D8 caught module failure rolls back its staged key");
                    LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSource = Previous; LootFixtureModuleRollback = false; LootFixtureSecondEntries = null; }
                LootRegistryPair("d8_batch_independent_publication"); break;
            }
            case 7: {
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                string Previous = LootFixtureSource; LootFixtureSource = "error('D8 failed root candidate')";
                try {
                    BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.RUNTIME_ERROR && ReferenceEquals(Gameplay.Active, Root),
                        "D8 failed root replacement preserves exact outgoing rule");
                    LootRegistryRule(LootRulePolicy.Prefab, Root, First); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                } finally { LootFixtureSource = Previous; }
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Root.Disposed, "D8 healthy first-owner replacement");
                var NextRoot = Gameplay.Active; var NextFirst = LootRegistryRule(LootRulePolicy.Prefab, NextRoot);
                LootBulkAddonReplace(LootBulkAddonSource() + "error('D8 failed addon replacement')", "2.0.0");
                BarrelResearchCheck(Addons.Status(LootBulkProvider, LootBulkToken)[5].Contains("replacement failed"),
                    "D8 failed addon replacement retains active source");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                LootBulkAddonReplace(LootBulkAddonSource(), "3.0.0");
                BarrelResearchCheck(Addon.Disposed && !ReferenceEquals(LootRegistryOwner(LootRulePolicy.SecondPrefab), Addon),
                    "D8 addon replacement retires exact outgoing owner");
                LootRegistryRule(LootRulePolicy.Prefab, NextRoot, NextFirst);
                LootRegistryPair("d8_batch_independent_replaced"); break;
            }
            case 8: {
                var Root = Gameplay.Active; var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab);
                long Vm = Host.VmGenerationId; int Receipts = LootFixtureReceipts;
                BarrelResearchCheck(Host.Execute("d8.batch.fatal", "while true do end").Status == RuntimeStatus.TIMEOUT && Host.Ready &&
                    Host.VmGenerationId != Vm && Root.Disposed && Addon.Disposed, "D8 generation retirement invalidates both defining owners");
                LootRegistryRule(LootRulePolicy.Prefab, Gameplay.Active); LootRegistryNone(LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(Addons.ProcessOne() && Addons.Status(LootBulkProvider, LootBulkToken)[2] == "Active" &&
                    !ReferenceEquals(LootRegistryOwner(LootRulePolicy.SecondPrefab), Addon) && LootFixtureReceipts == Receipts,
                    "D8 fresh addon reconstruction has no payout replay");
                LootRegistryPair("d8_batch_recovery_fresh_owners"); break;
            }
            case 9: {
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                int Frame = Time.frameCount, Before = LootFixtureReceipts;
                int Remaining = LootFrameNumber == Frame ? LootRulePolicy.PerFrame - LootFrameUsed : LootRulePolicy.PerFrame;
                for (int Index = 0; Index < Remaining; Index++)
                    LootRegistryPayout("d8_batch_shared_fill_" + Index, Index % 2 == 0 ? LootRulePolicy.Prefab : LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(LootFixtureReceipts == Before + Remaining && LootFrameNumber == Frame && LootFrameUsed == 8,
                    "D8 independent owners share exactly eight actual-frame admissions");
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK && Time.frameCount == Frame && LootFrameNumber == Frame && LootFrameUsed == 8,
                    "D8 replacement never resets exhausted shared producer budget");
                LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                foreach (string Prefab in new[] { LootRulePolicy.Prefab, LootRulePolicy.SecondPrefab }) {
                    try { LootFixtureVanilla(BarrelResearchCreate("d8_batch_shared_rejected", BarrelResearchMode.Vanilla, BarrelResearchPosition, true, 0, Prefab)); }
                    finally { BarrelResearchCleanup(); }
                }
                break;
            }
            case 10: {
                var Addon = LootRegistryOwner(LootRulePolicy.SecondPrefab); var Second = LootRegistryRule(LootRulePolicy.SecondPrefab, Addon);
                LootFixtureEntries = LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 empty root set withdraws only its owned key");
                LootRegistryNone(LootRulePolicy.Prefab); LootRegistryRule(LootRulePolicy.SecondPrefab, Addon, Second);
                LootRegistryPayout("d8_batch_addon_survives_root_withdrawal", LootRulePolicy.SecondPrefab);
                LootBulkRoot(LootBulkCommonEntries(), null);
                var Root = Gameplay.Active; var First = LootRegistryRule(LootRulePolicy.Prefab, Root);
                BarrelResearchCheck(Addons.UnloadProvider(LootBulkProvider) == 1 && Addon.Disposed, "D8 provider unload removes exact second owner");
                LootRegistryNone(LootRulePolicy.SecondPrefab); LootRegistryRule(LootRulePolicy.Prefab, Root, First);
                BarrelResearchCheck(Addons.UnloadProvider(LootBulkConflictProvider) == 1, "D8 failed conflict registration released");
                LootRegistryPayout("d8_batch_root_survives_provider_unload", LootRulePolicy.Prefab); break;
            }
            case 11:
                LootBulkRoot(LootBulkCommonEntries(), LootBulkCommonEntries()); LootRegistryPair("d8_batch_reclaimed_both");
                LootFixturePrefab = LootRulePolicy.SecondPrefab; BarrelResearchSelectedPrefab = LootFixturePrefab;
                LootFixtureEntries = LootBulkCommonEntries(); LootFixtureSecondEntries = null;
                BarrelResearchCheck(Host.Reload().Status == RuntimeStatus.OK, "D8 final public owner before unchanged B2 controls");
                LootRegistryPayout("d8_final_public_owner", LootRulePolicy.SecondPrefab);
                BarrelResearchCheck(Addons.Count == 0 && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D8 terminal provider and physical resource convergence");
                LootFixtureBulkDone = true;
                Puts("[CarbonLuau:LootFixture] PASS D8 bulk public batch offset=" + LootBulkBatchOffset + " count=" + LootBulkBatchCount +
                    " pass=" + LootBulkPublicPass + " fail=" + LootBulkPublicFail +
                    " common_publication_failure_recovery_budget_matrix=true per_item_receipts_authoritative=true ledger=0");
                break;
            default: throw new InvalidOperationException("D8 unknown bounded common batch phase");
            }
            LootRegistryRetention();
            BarrelResearchCheck(LootCurrent == null && BarrelResearchLedgerCount == 0, "D8 common batch phase exact physical convergence");
        }
        partial void GetLootBulkBudget(ref int Needed)
        {
            if (!LootFixtureBulkItems || LootFixtureStep < 62) return;
            if (!LootBulkPublicStarted) { Needed = 2; return; }
            if (LootBulkPublicIndex < LootBulkBatchOffset + LootBulkBatchCount) {
                Needed = LootBulkPublicPhase == 1 || LootBulkPublicPhase == 2 || LootBulkPublicPhase == 3 ? 4 :
                    LootBulkPublicPhase == 4 || LootBulkPublicPhase == 6 || LootBulkPublicPhase == 7 ? 1 : 2;
            } else Needed = LootBulkCommonPhase == 9 ? 8 : LootBulkCommonPhase >= 1 && LootBulkCommonPhase <= 5 ? 4 :
                LootBulkCommonPhase == 11 ? 3 : 2;
        }
        partial void RunLootBulkStep()
        {
            if (!LootBulkPublicStarted) LootBulkStartPublic();
            if (LootBulkPublicIndex >= LootBulkBatchOffset + LootBulkBatchCount) {
                LootBulkCommonStep(); LootBulkCommonPhase++; return;
            }
            var Spec = LootBulkApproved[LootBulkPublicIndex];
            try {
                LootBulkPublicItem(Spec); LootBulkPublicPhase++;
                if (LootBulkPublicPhase != 8) return;
                LootBulkPublicPass++;
                Puts("[CarbonLuau:LootFixture] D8_PUBLIC_ITEM name=" + Spec.Name + " item_id=" + Spec.Id + " stackable=" + Spec.Stack +
                    " family=" + Spec.Family + " result=PASS reason=both_prefabs_public_zero_min_max_range_repeat_mixed_stack_boundary_stored_bonus_controlled_bag");
            } catch (Exception Error) {
                LootBulkPublicFail++;
                Puts("[CarbonLuau:LootFixture] D8_PUBLIC_ITEM name=" + Spec.Name + " item_id=" + Spec.Id + " stackable=" + Spec.Stack +
                    " family=" + Spec.Family + " result=FAIL phase=" + LootBulkPublicPhase + " reason=" + LootBulkText(Error.GetType().Name + ":" + Error.Message));
                LootFixtureInjectSpawn = LootFixtureMutateAmount = LootFixtureForceBag = false; LootFixtureCreateFault = 0;
                BarrelResearchCleanup();
                LootBulkRoot(null, null);
                BarrelResearchCheck(Addons.Count == 0 && Gameplay.LootRetainedSessionCount == 0 && LootCurrent == null && BarrelResearchLedgerCount == 0,
                    "D8 failed candidate isolates before unrelated public item");
            }
            LootBulkPublicIndex++; LootBulkPublicPhase = 0;
        }
    }
}
