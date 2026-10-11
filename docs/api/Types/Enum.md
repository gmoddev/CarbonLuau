# Enum

Development scripting API `0.6.5-experimental`; not in published v0.5.0.
`Enum` is a frozen global namespace. Its only member is the frozen `DropMode`
namespace, containing [Enum.DropMode.Replace](DropMode.md).

```luau
local Mode: DropMode = Enum.DropMode.Replace
```

The namespace is a table; Replace is a sealed typed userdata whose public type
is `DropMode`. There is no standalone `DropMode` global or enum constructor,
enumeration or introspection API. [GiveItemBehavior](GiveItemBehavior.md)
continues to use its existing global table and InventoryOnly value.
