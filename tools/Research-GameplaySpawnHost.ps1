# Read-only pinned Player activation/respawn IL research. Does not load a server,
# install a patch, or qualify a public gameplay event.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [string]$CompareAssemblyPath,
    [string]$TypePattern = '^(ServerMgr|BasePlayer|BaseCombatEntity|BaseGameMode|SleepingBag)$',
    [string]$MethodPattern = '^(SpawnNewPlayer|SpawnPlayerSleeping|ClientReady|PlayerInit|RespawnAt|Respawn|StartSleeping|EndSleeping|StopWounded|RecoverFromWounded|InitializeHealth|OnNewPlayer|CanPlayerRespawn|SpawnPlayer|IsAlive|IsDead|get_IsConnected|IsWounded)$',
    [string]$ReferencePattern,
    [switch]$HookInventory,
    [switch]$Body
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$Opened = [Collections.Generic.List[object]]::new()
function OpenAssembly([string]$Path) {
    $Resolved = (Resolve-Path -LiteralPath $Path).Path
    $Value = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Resolved)
    $Opened.Add($Value)
    Write-Output "[CarbonLuau:GameplaySpawnResearch] Assembly=$Resolved SHA256=$((Get-FileHash -LiteralPath $Resolved -Algorithm SHA256).Hash) MVID=$($Value.MainModule.Mvid)"
    return ,$Value
}
function Inventory([object]$Assembly) {
    $Queue = [Collections.Generic.Queue[object]]::new()
    foreach ($Type in $Assembly.MainModule.Types) { $Queue.Enqueue($Type) }
    while ($Queue.Count -gt 0) {
        $Type = $Queue.Dequeue()
        Write-Output $Type
        foreach ($Nested in $Type.NestedTypes) { $Queue.Enqueue($Nested) }
    }
}
function MethodText([object]$Method) {
    if (!$Method.HasBody) { return '' }
    return (($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n")
}
try {
    # OpenAssembly emits a header followed by the Cecil value. Keep the header
    # separate so it cannot become part of the type inventory.
    $Result = @(OpenAssembly $AssemblyPath)
    $Result[0]
    $Assembly = $Result[1]
    $CompareMethods = @{}
    if ($CompareAssemblyPath) {
        $Result = @(OpenAssembly $CompareAssemblyPath)
        $Result[0]
        $Compare = $Result[1]
        foreach ($Type in Inventory $Compare) {
            foreach ($Method in $Type.Methods) { $CompareMethods[$Method.FullName] = $Method }
        }
    }
    foreach ($Type in Inventory $Assembly) {
        if ($HookInventory) {
            foreach ($Attribute in $Type.CustomAttributes) {
                $Arguments = ($Attribute.ConstructorArguments | ForEach-Object { $_.Value }) -join ' | '
                if ($Arguments -match '^(OnPlayerSpawn|OnPlayerRespawn|IOnPlayerConnected|OnPlayerSleep)') {
                    "HOOK_TYPE $($Type.FullName) ATTRIBUTE=$($Attribute.AttributeType.FullName) ARGUMENTS=$Arguments"
                    foreach ($Method in $Type.Methods) { "HOOK_METHOD $($Method.FullName) TOKEN=$($Method.MetadataToken)" }
                }
            }
        }
        if ($Type.FullName -notmatch $TypePattern) { continue }
        foreach ($Method in $Type.Methods) {
            if ($Method.Name -notmatch $MethodPattern) { continue }
            if ($ReferencePattern) {
                if (!$Method.HasBody) { continue }
                foreach ($Instruction in $Method.Body.Instructions) {
                    if ($null -ne $Instruction.Operand -and $Instruction.Operand.ToString() -match $ReferencePattern) {
                        "REFERENCE $($Method.FullName) TOKEN=$($Method.MetadataToken) $Instruction"
                    }
                }
                continue
            }
            "METHOD $($Method.FullName) TOKEN=$($Method.MetadataToken) VIRTUAL=$($Method.IsVirtual) BODY=$($Method.HasBody)"
            if ($CompareAssemblyPath) {
                $Other = $CompareMethods[$Method.FullName]
                "PARITY Present=$($null -ne $Other) Equal=$($null -ne $Other -and (MethodText $Method) -ceq (MethodText $Other))"
            }
            if ($Body -and $Method.HasBody) {
                for ($Index = 0; $Index -lt $Method.Body.Instructions.Count; $Index++) {
                    "  INDEX=$Index $($Method.Body.Instructions[$Index])"
                }
            }
        }
    }
    $Hash = [Security.Cryptography.MD5]::Create()
    try {
        foreach ($Name in @('OnPlayerConnected', 'OnPlayerRespawned')) {
            $Bytes = $Hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Name))
            "MANIFEST_HASH Name=$Name Unsigned=$([BitConverter]::ToUInt32($Bytes, 0)) Signed=$([BitConverter]::ToInt32($Bytes, 0))"
        }
    } finally { $Hash.Dispose() }
} finally {
    foreach ($Assembly in $Opened) { $Assembly.Dispose() }
}
