# Read-only exact-assembly inspection for World/Entity Foundation 2 research.
# This does not load Rust or Carbon into a server and is not a qualification gate.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [string]$TypePattern = 'Vis|Physics|EntityRealm|BaseEntity|BaseNetworkable',
    [string]$MethodPattern = '.*',
    [string]$ReferencePattern,
    [string]$BaseType,
    [switch]$ReferenceSummary,
    [switch]$TypesOnly,
    [switch]$Fields,
    [switch]$Body
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $AssemblyPath).Path)
try {
    Write-Output "[CarbonLuau:WorldDiscoveryResearch] Assembly=$AssemblyPath SHA256=$((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash) MVID=$($Assembly.MainModule.Mvid)"
    $Pending = [Collections.Generic.Queue[object]]::new()
    $TypeMap = @{}
    $Inventory = [Collections.Generic.List[object]]::new()
    foreach ($Type in $Assembly.MainModule.Types) { $Pending.Enqueue($Type) }
    while ($Pending.Count -gt 0) {
        $Type = $Pending.Dequeue()
        $TypeMap[$Type.FullName] = $Type
        $Inventory.Add($Type)
        foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
    }
    $SelectedTypes = 0
    $ConcreteTypes = 0
    $ReferenceCount = 0
    $ReferenceMethods = [Collections.Generic.HashSet[string]]::new()
    foreach ($Type in $Inventory) { $Pending.Enqueue($Type) }
    while ($Pending.Count -gt 0) {
        $Type = $Pending.Dequeue()
        if ($Type.FullName -notmatch $TypePattern) { continue }
        if ($BaseType) {
            $Current = $Type
            $Matches = $false
            $Depth = 0
            while ($null -ne $Current -and $Depth++ -lt 128) {
                if ($Current.FullName -eq $BaseType) { $Matches = $true; break }
                if ($null -eq $Current.BaseType) { break }
                $Current = $TypeMap[$Current.BaseType.FullName]
            }
            if (!$Matches) { continue }
        }
        $SelectedTypes++
        if (!$Type.IsAbstract -and !$Type.HasGenericParameters) { $ConcreteTypes++ }
        if ($ReferencePattern) {
            foreach ($Method in $Type.Methods) {
                if (!$Method.HasBody -or $Method.Name -notmatch $MethodPattern) { continue }
                foreach ($Instruction in $Method.Body.Instructions) {
                    if ($null -ne $Instruction.Operand -and
                        $Instruction.Operand.ToString() -match $ReferencePattern) {
                        $ReferenceCount++
                        $ReferenceMethods.Add($Method.FullName) | Out-Null
                        if (!$ReferenceSummary) {
                            Write-Output "REFERENCE $($Method.FullName) TOKEN=$($Method.MetadataToken) $Instruction"
                        }
                    }
                }
            }
            continue
        }
        Write-Output "TYPE $($Type.FullName) ATTRIBUTES=$($Type.Attributes)"
        if ($TypesOnly) { continue }
        if ($Fields) {
            foreach ($Field in $Type.Fields) {
                Write-Output "FIELD $($Field.FullName) STATIC=$($Field.IsStatic)"
            }
        }
        foreach ($Method in $Type.Methods) {
            if ($Method.Name -notmatch $MethodPattern) { continue }
            Write-Output "METHOD $($Method.FullName) TOKEN=$($Method.MetadataToken) STATIC=$($Method.IsStatic) BODY=$($Method.HasBody)"
            if ($Body -and $Method.HasBody) {
                foreach ($Instruction in $Method.Body.Instructions) {
                    Write-Output "  $Instruction"
                }
            }
        }
    }
    if ($ReferencePattern) {
        Write-Output "REFERENCE_SUMMARY Pattern=$ReferencePattern Instructions=$ReferenceCount Methods=$($ReferenceMethods.Count)"
    }
    Write-Output "TYPE_SUMMARY Selected=$SelectedTypes Concrete=$ConcreteTypes BaseType=$BaseType"
} finally {
    $Assembly.Dispose()
}
