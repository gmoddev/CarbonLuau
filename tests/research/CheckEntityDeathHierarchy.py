"""Independently derive B2 slot coverage, pins, and normal-flow counterexamples.

Consumes the static Cecil inventory; never loads Rust, patches a host, or starts
a server. Writes derived evidence beside the input, outside the repository.
The CFG is intentionally conservative: branches are not value-proved and
finally handlers are reported separately, not treated as invisible success.
"""

import argparse
import copy
import hashlib
import json
from pathlib import Path


def Require(Condition, Message):
    if not Condition:
        raise ValueError(Message)


def Ancestors(Name, Types):
    Result = []
    while Name in Types:
        Require(Name not in Result, f"cyclic ancestry: {Name}")
        Result.append(Name)
        Name = Types[Name]["Base"]
    return Result


def Effective(Name, Slot, Types, Methods):
    for Parent in Ancestors(Name, Types):
        Matches = [Methods[Identity] for Identity in Types[Parent]["DeclaredDeathMethods"]
                   if Methods[Identity]["Name"] == Slot and Methods[Identity]["Virtual"]
                   and Methods[Identity]["ReturnType"] == "System.Void"]
        Require(len(Matches) <= 1, f"ambiguous {Name}.{Slot}")
        if Matches:
            return Matches[0]["Identity"]
    return None


def FlowFacts(Method):
    Instructions = Method["Instructions"]
    ByOffset = {Row["Offset"]: Row for Row in Instructions}
    Next = {Row["Offset"]: Instructions[Index + 1]["Offset"]
            for Index, Row in enumerate(Instructions[:-1])}
    BaseCalls = [Row for Row in Instructions if Row["Op"] == "call"
                 and Row["Target"] == "System.Void BaseCombatEntity::Die(HitInfo)"]
    Returns = [Row["Offset"] for Row in Instructions if Row["Op"] == "ret"]
    Seen = set()
    Pending = [(Instructions[0]["Offset"], False)] if Instructions else []
    ReturnStates = set()
    while Pending:
        Offset, PassedBase = Pending.pop()
        if (Offset, PassedBase) in Seen:
            continue
        Seen.add((Offset, PassedBase))
        Row = ByOffset[Offset]
        PassedBase |= Row in BaseCalls
        if Row["Op"] == "ret":
            ReturnStates.add((Offset, PassedBase))
            continue
        if Row["Flow"] in ("Return", "Throw"):
            continue
        Successors = list(Row["Targets"])
        if Row["Flow"] != "Branch" and Offset in Next:
            Successors.append(Next[Offset])
        for Target in Successors:
            Require(Target in ByOffset, f"invalid branch {Method['Identity']}: {Target}")
            Pending.append((Target, PassedBase))
    CallsAfterBase = [Row for Row in Instructions if BaseCalls and Row["Offset"] > BaseCalls[-1]["Offset"]
                      and Row["Op"] in ("call", "callvirt", "newobj", "calli")]
    DeathCalls = [Row for Row in Instructions if Row["Target"] and
                  ("::Die(HitInfo)" in Row["Target"] or "::OnDied(HitInfo)" in Row["Target"])]
    return {"Identity": Method["Identity"], "Returns": Returns,
            "ReachableReturnStates": [{"Offset": Offset, "PassedBaseDie": Passed}
                                      for Offset, Passed in sorted(ReturnStates)],
            "BaseDieCalls": BaseCalls, "CallsAfterBaseDie": CallsAfterBase,
            "DeathDispatch": DeathCalls, "Handlers": Method["Handlers"]}


def AliveResetPath(Identity, Methods):
    """Conservative call-reference closure; no claim about receiver aliasing.

    An alive field write in an available body excludes a broad type guarantee.
    Missing arbitrary callees are not interpreted as a proof of no state write;
    the full pinned PE writer inventory is retained for manual review.
    """
    Pending = [(Identity, [Identity])]
    Seen = set()
    while Pending:
        Current, PathNames = Pending.pop()
        if Current in Seen or Current not in Methods:
            continue
        Seen.add(Current)
        Instructions = Methods[Current]["Instructions"]
        for Index, Row in enumerate(Instructions):
            if Row["Op"] == "stfld" and Row["Operand"] == "BaseCombatEntity/LifeState BaseCombatEntity::lifestate" and Index and Instructions[Index - 1]["Op"] == "ldc.i4.0":
                return {"Path": PathNames, "Offset": Row["Offset"]}
            if Row["Op"] in ("call", "callvirt") and Row["Target"] in Methods:
                Pending.append((Row["Target"], PathNames + [Row["Target"]]))
    return None


def Derive(Report):
    Types = {Row["Name"]: Row for Row in Report["Types"]}
    Methods = {Row["Identity"]: Row for Row in Report["Methods"]}
    Require(len(Types) == len(Report["Types"]), "duplicate type")
    Require(len(Methods) == len(Report["Methods"]), "duplicate method")
    Require(hashlib.sha256(Path(Report["AssemblyPath"]).read_bytes()).hexdigest() == Report["AssemblySHA256"], "assembly hash drift")
    Expected = {"Windows": "bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2",
                "Linux": "cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450"}
    Require(Report["AssemblySHA256"] == Expected[Report["Platform"]], "not the qualified 25653776 backing binary")
    for Method in Methods.values():
        if Method["Category"] != "Support":
            Require(Method["Identity"] in Types[Method["Type"]]["DeclaredDeathMethods"], "unlisted death declaration")
        if not Method["RawPath"]:
            continue
        Raw = Path(Method["RawPath"]).read_bytes()
        Require(len(Raw) == Method["CodeSize"], f"raw size drift: {Method['Identity']}")
        Require(hashlib.sha256(Raw).hexdigest() == Method["RawSHA256"], f"raw hash drift: {Method['Identity']}")
        Text = "\n".join(Row["Text"] for Row in Method["Instructions"])
        Require(hashlib.sha256(Text.encode()).hexdigest() == Method["ILTextSHA256"], f"decoded drift: {Method['Identity']}")
        Require(Path(Method["RawPath"]).with_suffix(".txt").read_text(encoding="utf-8") == Text, "retained text drift")
    Coverage = []
    for Name, Type in Types.items():
        Parents = Ancestors(Name, Types)
        Require(Type["Combat"] == ("BaseCombatEntity" in Parents), f"combat ancestry mismatch: {Name}")
        Require(Type["Resource"] == ("ResourceEntity" in Parents), f"resource ancestry mismatch: {Name}")
        Require(Type["Player"] == ("BasePlayer" in Parents), f"player ancestry mismatch: {Name}")
        for Identity in Type["DeclaredDeathMethods"]:
            Require(Identity in Methods and Methods[Identity]["Type"] == Name, "missing declaration")
        if not Type["Combat"] and not Type["Resource"]:
            continue
        Coverage.append({"Type": Name, "Abstract": Type["Abstract"], "Ancestors": Parents,
                         "Family": "Resource" if Type["Resource"] else "NPCPlayer" if Type["NPCPlayer"] else "Player" if Type["Player"] else "Combat",
                         "Die": Effective(Name, "Die", Types, Methods),
                         "OnDied": Effective(Name, "OnDied", Types, Methods)})
    Combat = [Row for Row in Coverage if Row["Family"] != "Resource"]
    Concrete = [Row for Row in Combat if not Row["Abstract"]]
    Targets = sorted({Row[Slot] for Row in Concrete for Slot in ("Die", "OnDied") if Row[Slot]})
    for Identity in Targets:
        Method = Methods[Identity]
        Require(Method["Virtual"] and not Method["Static"] and not Method["Abstract"], f"invalid target: {Identity}")
        Require(Method["CodeSize"] <= 4096 and len(Method["Instructions"]) <= 1024, f"existing diagnostic bounds exceeded: {Identity}")
        Require(not Method["Overrides"], f"explicit override needs slot analysis: {Identity}")
        Require(Method["NewSlot"] == (Method["Type"] == "BaseCombatEntity"), f"unexpected hiding slot: {Identity}")
    # Whole PE discovery yields a finite qualification candidate, not a runtime
    # scan. Base completion only certifies paths actually entering base Die.
    # Exclude no-op modules and static-respawn SamSite from unconditional scope.
    ResetPaths = {Identity: ResetProof for Identity in Targets
                  if Methods[Identity]["Name"] == "OnDied"
                  and (ResetProof := AliveResetPath(Identity, Methods))}
    Candidate = [Row for Row in Concrete if "BaseVehicleModule" not in Row["Ancestors"]
                 and "SamSite" not in Row["Ancestors"] and "BasePlayer" not in Row["Ancestors"]
                 and Row["OnDied"] not in ResetPaths]
    CandidateTargets = sorted({Row[Slot] for Row in Candidate for Slot in ("Die", "OnDied") if Row[Slot]})
    BaseOnly = [Row for Row in Candidate if Row["Die"] == "System.Void BaseCombatEntity::Die(HitInfo)"]
    BaseOnlyTargets = sorted({Row[Slot] for Row in BaseOnly for Slot in ("Die", "OnDied") if Row[Slot]})
    NoTrain = [Row for Row in BaseOnly if "TrainCar" not in Row["Ancestors"]]
    LifeWrites = [Row for Row in Report["StateWrites"] if Row["Field"] == "BaseCombatEntity/LifeState BaseCombatEntity::lifestate"]
    Facts = [FlowFacts(Method) for Method in Methods.values() if Method["Category"] != "Support"]
    Pins = [{Key: Methods[Identity][Key] for Key in
             ("Identity", "Token", "CodeSize", "RawSHA256", "ILTextSHA256", "Handlers")}
            for Identity in Targets]
    return {"Platform": Report["Platform"], "AssemblySHA256": Report["AssemblySHA256"],
            "MVID": Report["MVID"], "Coverage": Coverage, "EffectiveCombatTargets": Targets,
            "EffectiveCombatPins": Pins, "NonPlayerNormalCandidateTargets": CandidateTargets,
            "NonPlayerNormalCandidateTypes": [Row["Type"] for Row in Candidate],
            "SmallestBaseDieCandidateTypes": [Row["Type"] for Row in BaseOnly],
            "SmallestBaseDieCandidateTargets": BaseOnlyTargets,
            "BaseDieCandidateWithoutTrainRepairTypes": [Row["Type"] for Row in NoTrain],
            "LifeStateWrites": LifeWrites,
            "AliveResetOnDiedPaths": ResetPaths,
            "ExcludedNormalTypes": [Row for Row in Concrete if Row not in Candidate],
            "FlowFacts": Facts,
            "Counts": {"AllModuleTypes": len(Types), "CombatTypes": len(Combat),
                       "ConcreteCombatTypes": len(Concrete), "ResourceTypes": sum(Row["Family"] == "Resource" for Row in Coverage),
                       "CombatDieBodies": sum(Methods[Identity]["Name"] == "Die" for Identity in Targets),
                       "CombatOnDiedBodies": sum(Methods[Identity]["Name"] == "OnDied" for Identity in Targets),
                       "CombatTargets": len(Targets), "NormalNonPlayerTypes": len(Candidate),
                       "NormalNonPlayerTargets": len(CandidateTargets), "BaseDieNonPlayerTypes": len(BaseOnly),
                       "BaseDieNonPlayerTargets": len(BaseOnlyTargets),
                       "BaseDieNonPlayerWithoutTrainRepairTypes": len(NoTrain),
                       "LifeStateWriteSites": len(LifeWrites), "LifeStateWriterMethods": len({Row["Method"] for Row in LifeWrites}),
                       "DeathDeclarations": sum(Method["Category"] != "Support" for Method in Methods.values()),
                       "DeathCallsites": len(Report["DeathCallsites"]),
                       "AlternateTransitionCallsites": len(Report["AlternateTransitionCallsites"]),
                       "StateWritesOrAddresses": len(Report["StateWrites"])},
            "Limits": {"MaxTargetBytes": max(Methods[Identity]["CodeSize"] for Identity in Targets),
                       "MaxTargetInstructions": max(len(Methods[Identity]["Instructions"]) for Identity in Targets),
                       "MaxTargetHandlers": max(len(Methods[Identity]["Handlers"]) for Identity in Targets),
                       "MaxAncestry": max(len(Row["Ancestors"]) for Row in Coverage)},
            "Status": "STATIC CANDIDATE ONLY; no live/publicizer/topology qualification"}


def SelfCheck(Report):
    Mutations = []
    Missing = copy.deepcopy(Report)
    Missing["Methods"] = [Row for Row in Missing["Methods"] if Row["Identity"] != "System.Void LootContainer::OnDied(HitInfo)"]
    Mutations.append(("missing override", Missing))
    Hidden = copy.deepcopy(Report)
    for Type in Hidden["Types"]:
        if Type["Name"] == "LootContainer":
            Type["DeclaredDeathMethods"] = []
    Mutations.append(("unlisted override", Hidden))
    HidingSlot = copy.deepcopy(Report)
    for Method in HidingSlot["Methods"]:
        if Method["Identity"] == "System.Void LootContainer::OnDied(HitInfo)":
            Method["NewSlot"] = True
    Mutations.append(("new hiding virtual slot", HidingSlot))
    WrongBase = copy.deepcopy(Report)
    for Type in WrongBase["Types"]:
        if Type["Name"] == "ResourceEntity":
            Type["Base"] = "BaseCombatEntity"
    Mutations.append(("resource ancestry drift", WrongBase))
    WrongBytes = copy.deepcopy(Report)
    WrongBytes["Methods"][0]["RawSHA256"] = "0" * 64
    Mutations.append(("method raw hash drift", WrongBytes))
    WrongAssembly = copy.deepcopy(Report)
    WrongAssembly["AssemblySHA256"] = "0" * 64
    Mutations.append(("assembly drift", WrongAssembly))
    for Label, Mutated in Mutations:
        try:
            Derive(Mutated)
        except (ValueError, KeyError):
            continue
        raise ValueError(f"negative checker failed to reject: {Label}")
    return [Label for Label, _ in Mutations]


def WriteSummary(Root, Reports, Derived):
    Report = Reports[0]
    Data = Derived[0]
    Methods = {Row["Identity"]: Row for Row in Report["Methods"]}
    Counts = Data["Counts"]
    Lines = ["# Gameplay B2 virtual death hierarchy — static research",
             "", "Pinned Rust build 25653776. This is original-PE structural evidence, not a live B2 qualification or an adopted public scope.",
             "", f"Windows assembly SHA256: `{Reports[0]['AssemblySHA256']}`.",
             f"Linux assembly SHA256: `{Reports[1]['AssemblySHA256']}`.",
             "", f"All-module types: Windows {len(Reports[0]['Types'])}, Linux {len(Reports[1]['Types'])}; platform-only unrelated types are not combat hierarchy drift.",
             f"Combat hierarchy: {Counts['CombatTypes']} types, {Counts['ConcreteCombatTypes']} concrete; effective declaration union {Counts['CombatTargets']} = {Counts['CombatDieBodies']} Die + {Counts['CombatOnDiedBodies']} OnDied.",
             f"All HitInfo Die/OnDied declarations: {Counts['DeathDeclarations']} = 60 combat void methods + 4 resource void methods + 12 component Boolean callbacks. Resources have {Counts['ResourceTypes']} types.",
             f"Reference evidence: {Counts['DeathCallsites']} death callsites, {Counts['AlternateTransitionCallsites']} alternate transition callsites, {Counts['StateWritesOrAddresses']} direct state writes/addresses.",
             "", "## Completion strategy for the main task", "",
             "The base normal terminal return at IL_0150 occurs AFTER TryDropCorpse dispatch, the complete effective virtual OnDied call at IL_013d, and TimeWarning.Dispose in finally. The dead-guard return IL_0008 must never advance completion. The base writes health=0 at IL_001c and lifestate=Dead(1) at IL_0023 BEFORE that work; flags therefore cannot establish completion.",
             "A base terminal marker must be progress in a bounded original-object attempt, not immediate event publication. Publish only after the full effective original Die returns normally, with original-run/topology proof, matched original OnDied completion, unchanged D20 incarnation and final lifestate=Dead. The base marker already waits through outer OnDied tails; a marker at BaseCombatEntity.OnDied return does not. For BasePlayer.Die, disposal at IL_00cb can still throw after base Die returns at IL_00c0. Keep pending snapshots until the outer fence; exceptions/skips/nesting must not certify.",
             "Capture bounded original D20 identity and optional scalars before the first death/removal invalidation; never reacquire by ID or read a removed wrapper at delivery. An OnDied-only observation has neither the base transition proof nor the outer Die completion proof. Deferred explosion, wreck, corpse removal and loot effects are not guaranteed by synchronous death completion. B3 removal remains independent.",
             "Human Player B2 can consume the existing qualified A outer PlayerDeathPatch completion, with its existing life epoch inside the exact D11 connection; do not add a redundant BasePlayer patch or dedupe humans once per SpawnEpoch. NPCPlayer descendants are excluded by A and need separate subtype/topology/outer-completion qualification. TrainCar.DoRepair also restores Alive without a Spawn; any generic once-per-Spawn latch needs a separately qualified life reset or explicit exclusion.",
             "", "## Smallest conservative normal-route candidate", "",
             f"Restricting to non-BasePlayer combat entities with effective Die exactly BaseCombatEntity.Die yields {Counts['BaseDieNonPlayerTypes']} concrete type declarations and {Counts['BaseDieNonPlayerTargets']} qualification methods. Adding BuildingPrivlidge's guarded, last-call-to-base override yields {Counts['NormalNonPlayerTypes']} types and {Counts['NormalNonPlayerTargets']} methods. These are type declarations, not prefab counts or proof that every instance is D20 admissible.",
             "This scope is explicitly the normal completed Die(HitInfo) route. BaseCombatEntity.DieInstantly (0x0600149c) also writes Dead and invokes virtual OnDied; it bypasses Die(HitInfo). A claim of all death routes needs its own original completion fence/pins (61-method combat-plus-Instant qualification union) or must state the route restriction. It has four callers: NPC off-navmesh cleanup, boat building cleanup, spectate and spectateid.",
             f"Removing TrainCar descendants from the {Counts['BaseDieNonPlayerTypes']}-type base-only candidate leaves {Counts['BaseDieNonPlayerWithoutTrainRepairTypes']} concrete declarations. This is an optional narrower route/type table, not proof that all remaining objects are permanently dead forever.",
             "The checker derives these candidate sets and complete ancestry tables. It does not decide the public contract. Live eligibility still needs exact owner-thread D20 proof, no replay, snapshots, occurrence/life dedupe inside the exact incarnation, skips/throws/nesting, and processed-host/topology qualification.",
             "", "## Exceptions and subtype semantics", "",
             "- BaseVehicleModule.Die (0x06001e7e, 1 byte) is ret only; 6 concrete module types inherit it. Zero health is not completed Die.",
             "- SamSite.Die (0x060033cc, 167 bytes): staticRespawn=true writes Dead at IL_0068 and health=0, schedules SelfHeal, and returns IL_009e without base Die or OnDied. The nonstatic branch calls base at IL_00a1 and returns IL_00a6. The conservative whole-type candidate excludes SamSite; conditional nonstatic admission needs separate proof.",
             "- WildlifeHazard and SnakeHazard: OnDied reaches TurnIntoCorpse (0x06003b1d), which sets health to MaxHealth and lifestate=Alive at IL_0034. First stage is not final terminal Dead despite normal base completion. Later corpse killing is a different path; conservative whole-type candidate excludes both.",
             "- ResourceEntity derives from BaseEntity, not BaseCombatEntity. Its OnAttacked decrements its own health and invokes resource OnDied. ResourceEntity/MonumentBlocker set isKilled then Kill; TreeEntity has its own killed guard/write and fall/deferred-kill tail; OreResourceEntity cleans bonus then calls resource base. No BCE base marker applies. Resource/staged/ore/tree/monument families remain separate proof.",
             "- Resource effective dispatch table: ResourceEntity and StagedResourceEntity use ResourceEntity.OnDied (0x06005a9e, 16 bytes); MonumentBlocker uses its own (0x06002d19, 16); OreResourceEntity its own (0x06002e14, 14); TreeEntity and VineSwingingTree use TreeEntity.OnDied (0x060038fb, 327). Ore calls base only after cleanup, then ret, with no post-base tail. Tree/Monument never call resource base. The full effective OnDied original, killed false-to-true progress and final killed state are needed; a write-only or resource-base-only marker is insufficient. Tree virtual OnFallServer/deferred kill may be fallible; a synchronous completion never promises the deferred removal.",
             "- BasePlayer is 1 concrete declaration; 20 concrete NPCPlayer descendants inherit BasePlayer.Die. NPCPlayer has no Die or OnDied declaration of its own. BasePlayer, NPCShopKeeper, ScientistNPC and VehicleVendor provide the Player-family effective OnDied union. NPCShopKeeper and ScientistNPC have fallible work after BasePlayer.OnDied. Gameplay A human-only eligibility is not an NPC qualification.",
             "- BaseNpc.OnDied, BasePlayer.OnDied, Landmine.OnDied, ModularCar.OnDied, MotorRowboat.OnDied, PlayerBoat.OnDied and WildlifeHazard.OnDied do not directly call an ancestor OnDied. This is valid subtype behavior, not automatic bypass of base Die. Keep delayed corpse/wreck/explosion semantics distinct from removal.",
             "- Component Boolean OnDied callbacks (EntityComponentBase plus 11 Gen2 FSM overrides) are a separate virtual slot; false can suppress the base OnDied Kill while completed death remains possible. They do not belong in the 55 void combat OnDied table.",
             "- Corpse entity classes such as BaseCorpse/PlayerCorpse/NPCPlayerCorpse inherit ordinary BCE death methods. Do not confuse these separate objects with WildlifeHazard resetting the same object to an alive corpse stage. No Corporation-named entity was found in either module.",
             "", "## Damage route mapping", "",
             "BaseCombatEntity.OnAttacked(HitInfo) dispatches virtual Hurt(HitInfo) at IL_0069; Hurt applies guards/scaling/components, writes health, and dispatches virtual Die(HitInfo) at IL_0413 when Health<=0. Scalar Hurt overloads feed HitInfo into virtual Hurt. The death fence certifies Die, not the enclosing Hurt/OnAttacked operation: Hurt has later combat logging/finally work and OnAttacked has a later BaseEntity.OnAttacked tail. This distinction must remain explicit.",
             "Player CheckDeathCondition/wounding/invalid-position and many plant, bee, boat, AI and ground-missing paths call the same virtual Die slot; complete callsites are in Windows.json/Linux.json. Resource OnAttacked bypasses that slot. Direct OnDied callers are base Die, DieInstantly, resource/monument attacks and ancestor calls; the PE has no additional direct BCE virtual OnDied transition caller.",
             "", "## Bounded reflection qualification and method pins", "",
             "ReflectionQualification.json is derived from all declarations plus independent ancestry/effective-slot checks. Compare exact DECLARED instance virtual void(HitInfo) methods, including inherited effective methods; reject a new hiding slot, explicit override ambiguity, missing declaration, or unlisted method. No module-wide runtime scanning without a fixed cap. Platform PE counts differ, so use platform pins plus the exact combat type table.",
             f"Measured target bounds: {Data['Limits']['MaxTargetBytes']} bytes, {Data['Limits']['MaxTargetInstructions']} instructions, {Data['Limits']['MaxTargetHandlers']} EH clauses, ancestry {Data['Limits']['MaxAncestry']}.",
             "MethodPins.tsv contains token, size, original raw IL SHA256, decoded resolved-operand SHA256 and EH count for all 60 on both platforms. JSON includes full EH boundaries, locals and branch/return/call facts. All retained resolved-opcode/EH/local shapes agree across platforms; two unrelated ConVar support callers have different tokens. Carbon.Startup publicization reindexes operands: original-PE raw hashes are NOT loaded live hashes. Main must independently reproduce/qualify every selected processed-live body and actual Harmony/hook topology before implementation claims.",
             "", "## Complete combat declaration table", "",
             "| Declaring type | Method | Token | Bytes | Returns (IL offsets) | Ancestor OnDied calls |",
             "|---|---|---|---:|---|---|"]
    Types = {Row["Name"]: Row for Row in Report["Types"]}
    for Identity in Data["EffectiveCombatTargets"]:
        Method = Methods[Identity]
        Parents = Ancestors(Method["Type"], Types)[1:]
        Calls = [Row for Row in Method["Instructions"] if Row["Op"] == "call" and Row["Target"] in Methods
                 and Methods[Row["Target"]]["Type"] in Parents and Methods[Row["Target"]]["Name"] == "OnDied"]
        Returns = ", ".join(f"{Row['Offset']:04x}" for Row in Method["Instructions"] if Row["Op"] == "ret")
        CallText = "; ".join(f"{Row['Offset']:04x}:{Methods[Row['Target']]['Type']}" for Row in Calls) or "none"
        Lines.append(f"| {Method['Type']} | {Method['Name']} | {Method['Token']} | {Method['CodeSize']} | {Returns} | {CallText} |")
    Lines += ["", "## Direct state-write inventory", "", "| Method | IL offset | Op | Field |", "|---|---|---|---|"]
    for Row in Report["StateWrites"]:
        Lines.append(f"| {Row['Method']} | {Row['Offset']:04x} | {Row['Op']} | {Row['Field']} |")
    Lines += ["", f"lifestate alone has {Counts['LifeStateWriteSites']} sites in {Counts['LifeStateWriterMethods']} methods on both binaries. BaseCombatEntity.ResetState is 145 bytes and has NO direct lifestate write; it restores health and sets ResetLifeStateOnSpawn=true. Whole-module field-address scanning found no lifestate address-take path. Load has both an Alive constant and a serialized-state assignment; initialization, fake Player setup and SpawnNewPlayer assignments are not newly observed deaths.",
              "", "The lifestate table covers direct managed stfld/stsfld and address-taking references in the pinned PE. It does not prove absence of reflection/native/foreign plugin writes outside I12, or authorize external host changes."]
    Summary = Root / "ResearchSummary.md"
    Require(not Summary.exists(), "summary evidence already exists")
    Summary.write_text("\n".join(Lines) + "\n", encoding="utf-8")


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("EvidenceDirectory", type=Path)
    Parser.add_argument("--self-check", action="store_true", help="exercise six independent negative inventory mutations in memory")
    Args = Parser.parse_args()
    Root = Args.EvidenceDirectory.resolve()
    Repository = Path(__file__).resolve().parents[2]
    Require(Root != Repository and Repository not in Root.parents, "evidence must be external")
    Reports = [json.loads((Root / f"{Platform}.json").read_text(encoding="utf-8")) for Platform in ("Windows", "Linux")]
    Derived = [Derive(Report) for Report in Reports]
    Negatives = SelfCheck(Reports[0]) if Args.self_check else []
    Require(Derived[0]["Coverage"] == Derived[1]["Coverage"], "cross-platform hierarchy/slot drift")
    Require(Derived[0]["EffectiveCombatTargets"] == Derived[1]["EffectiveCombatTargets"], "cross-platform target drift")
    Left = {Row["Identity"]: Row for Row in Reports[0]["Methods"]}
    Right = {Row["Identity"]: Row for Row in Reports[1]["Methods"]}
    Require(Left.keys() == Right.keys(), "cross-platform retained inventory drift")
    TokenDifferences = []
    for Identity in Left:
        for Key in ("CodeSize", "ILTextSHA256", "Handlers", "Locals", "InitLocals"):
            Require(Left[Identity][Key] == Right[Identity][Key], f"cross-platform semantic drift: {Identity}/{Key}")
        if Left[Identity]["Token"] != Right[Identity]["Token"]:
            Require(Left[Identity]["Category"] == "Support", f"death declaration token drift: {Identity}")
            TokenDifferences.append({"Identity": Identity, "Windows": Left[Identity]["Token"], "Linux": Right[Identity]["Token"]})
    Require(Reports[0]["StateWrites"] == Reports[1]["StateWrites"], "state-write inventory drift")
    Require(Reports[0]["DeathCallsites"] == Reports[1]["DeathCallsites"], "death-caller inventory drift")
    Require(Reports[0]["DeathAddressReferences"] == Reports[1]["DeathAddressReferences"], "address-reference drift")
    Alternate = [[{Key: Value for Key, Value in Row.items() if Key != "Token"}
                  for Row in Report["AlternateTransitionCallsites"]] for Report in Reports]
    Require(Alternate[0] == Alternate[1], "alternate route drift")
    # Regression premises are independently checked, not inferred from flags.
    Die = Left["System.Void BaseCombatEntity::Die(HitInfo)"]
    Writes = [Row for Row in Die["Instructions"] if Row["Op"] == "stfld" and "::lifestate" in Row["Operand"]]
    Dispatch = [Row for Row in Die["Instructions"] if Row["Op"] == "callvirt" and Row["Target"] == "System.Void BaseCombatEntity::OnDied(HitInfo)"]
    Require(len(Writes) == len(Dispatch) == 1 and Writes[0]["Offset"] < Dispatch[0]["Offset"], "base transition/dispatch premise drift")
    Require(len([Row for Row in Die["Instructions"] if Row["Op"] == "ret"]) == 2, "base early/terminal return premise drift")
    PlayerFacts = FlowFacts(Left["System.Void BasePlayer::Die(HitInfo)"])
    Require(PlayerFacts["CallsAfterBaseDie"] and PlayerFacts["Handlers"], "outer player fallible-tail counterexample lost")
    Module = Left["System.Void BaseVehicleModule::Die(HitInfo)"]
    Require([Row["Op"] for Row in Module["Instructions"]] == ["ret"], "module bypass premise drift")
    SamFacts = FlowFacts(Left["System.Void SamSite::Die(HitInfo)"])
    Require({Row["PassedBaseDie"] for Row in SamFacts["ReachableReturnStates"]} == {False, True}, "SamSite conditional bypass lost")
    Require("BaseCombatEntity" not in Ancestors("ResourceEntity", {Row["Name"]: Row for Row in Reports[0]["Types"]}), "resource family changed")
    Require("System.Void WildlifeHazard::OnDied(HitInfo)" in Derived[0]["AliveResetOnDiedPaths"], "corpse-alive counterexample lost")
    Instant = Left["System.Void BaseCombatEntity::DieInstantly()"]
    Require(any(Row["Target"] == "System.Void BaseCombatEntity::OnDied(HitInfo)" for Row in Instant["Instructions"])
            and not any(Row["Target"] == "System.Void BaseCombatEntity::Die(HitInfo)" for Row in Instant["Instructions"]), "instant death bypass changed")
    Reset = Left["System.Void BaseCombatEntity::ResetState()"]
    Require(Reset["CodeSize"] == 145 and not any(Row["Op"] == "stfld" and Row["Operand"] == "BaseCombatEntity/LifeState BaseCombatEntity::lifestate" for Row in Reset["Instructions"]), "ResetState lifestate premise changed")
    Require(len(Derived[0]["LifeStateWrites"]) == 12 and len({Row["Method"] for Row in Derived[0]["LifeStateWrites"]}) == 11, "pinned full-field-write inventory drift")
    Output = Root / "CheckedHierarchy.json"
    Require(not Output.exists(), "checked evidence already exists; preserve it")
    Output.write_text(json.dumps({"Platforms": Derived, "CrossPlatformSemanticParity": True,
                                 "SupportTokenDifferences": TokenDifferences}, indent=2), encoding="utf-8")
    Pins = Root / "MethodPins.tsv"
    Require(not Pins.exists(), "pin evidence already exists; preserve it")
    Lines = ["Platform\tIdentity\tToken\tCodeSize\tOriginalRawSHA256\tILTextSHA256\tEHCount"]
    for Platform in Derived:
        for Pin in Platform["EffectiveCombatPins"]:
            Lines.append("\t".join(str(Value) for Value in (Platform["Platform"], Pin["Identity"], Pin["Token"], Pin["CodeSize"], Pin["RawSHA256"], Pin["ILTextSHA256"], len(Pin["Handlers"]))))
    Pins.write_text("\n".join(Lines) + "\n", encoding="utf-8")
    # Finite table for later owner-thread metadata-only reflection matching.
    # Rows are DECLARED method identities, not an IsAssignableFrom guess at run
    # time. Original PE raw hashes MUST NOT be used as publicized live hashes.
    Qualification = Root / "ReflectionQualification.json"
    Require(not Qualification.exists(), "reflection evidence already exists")
    Spec = {"Status": "STATIC ONLY; publicized bodies and patch topology remain unqualified",
            "Root": "BaseCombatEntity", "Signature": "instance virtual void (HitInfo)",
            "ExpectedDeclaredDieCount": Derived[0]["Counts"]["CombatDieBodies"],
            "ExpectedDeclaredOnDiedCount": Derived[0]["Counts"]["CombatOnDiedBodies"],
            "ExpectedCombatTypesIncludingAbstract": Derived[0]["Counts"]["CombatTypes"],
            "Targets": Derived[0]["EffectiveCombatTargets"],
            "NormalNonPlayerTargets": Derived[0]["NonPlayerNormalCandidateTargets"],
            "NormalNonPlayerTypes": Derived[0]["NonPlayerNormalCandidateTypes"],
            "AdditionalAllRouteTarget": "System.Void BaseCombatEntity::DieInstantly()",
            "Bounds": Derived[0]["Limits"],
            "RequiredRuntimeChecks": ["exact assembly/publicizer/Harmony/hook identities",
                                      "equality of declared virtual slots, effective methods, and ancestry",
                                      "processed LIVE raw/canonical/EH pins for every target",
                                      "per-target original-run and topology qualification",
                                      "owner-thread, bounded nesting, no-wait topology observation"]}
    Qualification.write_text(json.dumps(Spec, indent=2), encoding="utf-8")
    WriteSummary(Root, Reports, Derived)
    print("[Research:EntityDeath] STATIC CHECK PASS; cross-platform semantic parity")
    print(json.dumps({"Counts": Derived[0]["Counts"], "Limits": Derived[0]["Limits"]}, indent=2))
    print(f"[Research:EntityDeath] Derived hierarchy and original-PE pins: {Output}; {Pins}")
    print(f"[Research:EntityDeath] Reflection candidate: {Qualification}")
    if Negatives:
        print(f"[Research:EntityDeath] Negative checker cases rejected ({len(Negatives)}): {', '.join(Negatives)}")


if __name__ == "__main__":
    Main()
