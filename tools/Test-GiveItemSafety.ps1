$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Adapter = Get-Content -Raw (Join-Path $Root 'src/CarbonLuau/CarbonLuau.Inventory.Carbon.cs')
$Core = Get-Content -Raw (Join-Path $Root 'src/CarbonLuau/Facade/InventoryGrant.cs')
$Facade = Get-Content -Raw (Join-Path $Root 'src/CarbonLuau/Facade/FacadeSession.cs')
$Bootstrap = Get-Content -Raw (Join-Path $Root 'scripts/bootstrap.luau')
$Native = Get-Content -Raw (Join-Path $Root 'native/src/facade/FacadeBridge.cpp')
$Transfers = [regex]::Matches($Adapter, '\.MoveToContainer\s*\(([^;]+)\)')
if ($Transfers.Count -ne 1 -or $Transfers[0].Groups[1].Value -cne 'Target, Chunk.Slot, AllowStack, false, Player, false') {
    throw 'GiveItem must use only the qualified exact-slot/no-ignore/no-swap adapter'
}
if ($Adapter -match '\.(GiveItem|Drop|SplitItem|RemoveFromContainer|DoRemoves)\s*\(') {
    throw 'GiveItem adapter contains an unsupported mutation/fallback'
}
foreach ($Guard in @('Slot < 0','Target.HasAvailableSlotsDefined','ItemDefinition.Flag.Backpack','ItemModParachute',
    'Restriction.CanExistWith','Clothing.CanExistWith','Chunk.Amount > StackLimit','Occupant.CanStack(Value)',
    'Value.GetWorldEntity()','Value.Remove(0)','ViewInventory(Player)')) {
    if (!$Adapter.Contains($Guard)) { throw "Missing qualified adapter guard: $Guard" }
}
if ([regex]::Matches($Adapter, 'ItemManager.Create\(').Count -ne 1) { throw 'Unexpected creation surface' }
foreach ($Guard in @('ValidateMutationCapacity(Source)','CountForMutation','Chunks.Count >= FacadePolicy.InventoryStacks',
    'Host.Create(Definition, Chunk.Amount)','Returned[Index] = Value','!Transferred[Index]',
    'Plan.Before + Amount','Gate.Exit(Token)','Host.Cleanup(Value)')) {
    if (!$Core.Contains($Guard)) { throw "Missing grant proof boundary: $Guard" }
}
$Commit = $Core.IndexOf('// COMMIT:')
if ($Core.Substring($Commit) -match 'return false;') { throw 'False is forbidden after COMMIT' }
if (!$Facade.Contains('new PlayerGiveItemOperation(TakeItems.SharedGate)')) { throw 'Give/Take gate split' }
if (!$Native.Contains('Operation == 30') -or !$Native.Contains('Runtime.Admission->Provisional')) { throw 'Missing native provisional ingress guard' }
if (!$Bootstrap.Contains('getmetatable(Behavior) ~= "CarbonLuau.GiveItemBehavior"')) { throw 'Behavior must be typed' }
if ($Bootstrap -match 'DropRemainder|DropIfFull') { throw 'Future drop behavior exposed' }
Write-Output '[CarbonLuau:GiveItemSafety] PASS exact adapter, bounded planning, combined VERIFY, shared gate, typed InventoryOnly and no post-COMMIT false (structural supplement, not host proof)'
