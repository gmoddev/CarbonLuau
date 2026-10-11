# Read-only exact backing/Carbon-hook inspection; no host patch or world mutation.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$CecilPath,
    [switch]$Body,
    [switch]$References
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Research-GameplayDeathHost.ps1') -AssemblyPath $AssemblyPath -CecilPath $CecilPath `
    -TypePattern '^(BaseCombatEntity|BaseEntity|BaseNetworkable|DecayEntity|StorageContainer|LootContainer|LootFill|DropUtil|Item|ItemContainer|ItemManager|WorldItem|DroppedItem)$' `
    -MethodPattern '^(Spawn|ServerInit|SpawnLoot|PopulateLoot|GenerateScrap|FillLoot|DelayFill|Die|OnDied|OnKilled|TryDropCorpse|DropItems|DropBonusItems|Create|CreateWorldObject|Drop|InitializeItem|SetWorldEntity|DestroyItem|RemoveFromWorld|Remove|RemoveItem|Clear|Kill|DoServerDestroy|EnterPool|ResetState)$' `
    -HookPattern '^(OnLootSpawn|OnContainerDropItems|OnBonusItemDrop|OnBonusItemDropped|OnItemDropped|OnItemRemove|OnEntityDeath|OnEntityKill)$' `
    -ReferenceInventory:$References -ReferencePattern '(LootContainer|StorageContainer)::(DropItems|DropBonusItems|GenerateScrap|SpawnLoot)|DropUtil::DropItems|Item::(CreateWorldObject|Drop)\(' -Body:$Body
if ($LASTEXITCODE) { throw '[CarbonLuau:LootResearch] Inspection failed' }
