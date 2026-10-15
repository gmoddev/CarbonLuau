# Item and Prefab catalogs

Availability: development API **0.7.5-experimental**. These globals are absent
from immutable published v0.7.0. No new package release or tag is implied.

`Item` is a frozen table of 221 PascalCase fields generated from the finite
[D8 manifest](../../api/loot-items.pinned.json). Each value is its exact ordinary
canonical shortname string. `Prefab` is a frozen table with one frozen `Loot`
table, containing exactly `Barrel1` and `Barrel2`. The latter values are the exact
qualified autospawn barrel paths. The [generated symbol map](../../generated/catalog-symbols.json)
records every spelling; the [qualified item table](LootItems.md) retains numeric
IDs, vanilla stack limits and operation-specific physical evidence.

```luau
assert(type(Item.Scrap) == "string" and Item.Scrap == "scrap")
assert(Item.MetalFragments == "metal.fragments")
assert(Item.Wood == "wood")
assert(Prefab.Loot.Barrel1 ==
    "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab")

game:GetService("Loot"):RegisterDropRule(Prefab.Loot.Barrel2, Enum.DropMode.Replace, {
    {Item = Item.Scrap, Min = 5, Max = 12},
    {Item = "wood", Min = 50, Max = 100},
})
```

Symbols follow version 1: split shortnames on `.`, `_` and `-`, uppercase the
first alphabetic character of each segment and concatenate. Numeric-leading
names gain `N`: `2module.car` becomes `Item.N2ModuleCar`. For example,
`ammo.pistol.hv` becomes `Item.AmmoPistolHv`. Symbols are case-sensitive;
`Item.scrap` is absent. There are no Get methods, categories or aliases.

All three tables are constructed once per shared VM generation. Assignment,
rawset and metatable mutation fail. Healthy root/addon replacement reuses the
same tables; fatal recovery constructs new equivalent tables before executing
domains. Ordinary copied strings have no stale facade authority. Private script
environments may shadow their own `Item` or `Prefab` binding without changing
the shared catalog or another environment.

`Item` is separate from `game:GetService("Items")`. The latter performs an
existing live-host existence query and can recognize names outside the catalog.
The constants perform no host lookup and grant no authority. Existing Loot,
PlayerInventory and Workspace methods still accept strings and run all their
own admission, lifetime, amount, publication and failure checks. D8 qualification
does not establish inventory placement, consumption, equip or weapon behavior
for every catalog item. `ImageSource.Item` remains a separate existing API.

Constants compare, format and serialize exactly like literal strings, including
accepted persistence string values. Persist values rather than the namespace
tables. Existing raw-string examples remain valid. Use a prefab constant as a
Workspace discovery filter or for exact Entity.Prefab comparison under the
existing D20 rules. Do not compare an Item shortname with a death context's
separately captured WeaponPrefab label.

Catalog fields are Pure in tooling preview and generated editor metadata;
actual operations retain their existing preview classification. Completion
does not certify arbitrary host versions or additional gameplay capabilities.
