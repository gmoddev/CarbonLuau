# Read-only Player-1F-B supplement. A successful check confirms a qualification
# gap, NOT a safe GiveItem adapter. I12 adoption does not requalify G1.
# Never executes Rust or mutates inventory.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$CecilAssembly
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilAssembly
$ExpectedHashes = @(
    '87a02eef432e3312c32cfc947937b5b526d1c4425df8c07193779389398d1a9a',
    '22a20500e30ebebd9c648bcac199cd7bc5e37af524b0b64cf9a4c74eb857d01b'
)
$Paths = @($WindowsAssembly, $LinuxAssembly)
$Opened = [System.Collections.Generic.List[System.IDisposable]]::new()
$script:Checks = 0
function Assert-Evidence([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw "[CarbonLuau:Player1FB] Evidence changed: $Label" }
    $script:Checks++
}
function Get-Body($Assembly, [string]$TypeName, [string]$Name, [int]$Arguments) {
    $Type = @($Assembly.MainModule.Types | Where-Object FullName -CEQ $TypeName)
    Assert-Evidence ($Type.Count -eq 1) "type $TypeName"
    $Method = @($Type[0].Methods | Where-Object { $_.Name -ceq $Name -and $_.Parameters.Count -eq $Arguments })
    Assert-Evidence ($Method.Count -eq 1 -and $Method[0].HasBody) "method $TypeName::$Name"
    return ($Method[0].Body.Instructions | ForEach-Object ToString) -join "`n"
}
function Assert-Order([string]$Body, [string[]]$Fragments) {
    $Position = 0
    foreach ($Fragment in $Fragments) {
        $Found = $Body.IndexOf($Fragment, $Position, [StringComparison]::Ordinal)
        Assert-Evidence ($Found -ge 0) "ordered $Fragment"
        $Position = $Found + $Fragment.Length
    }
}
try {
    for ($Index = 0; $Index -lt 2; ++$Index) {
        Assert-Evidence ((Get-FileHash -LiteralPath $Paths[$Index] -Algorithm SHA256).Hash -ieq $ExpectedHashes[$Index]) "pinned assembly $Index"
        $Opened.Add([Mono.Cecil.AssemblyDefinition]::ReadAssembly($Paths[$Index]))
    }
    $Bodies = @{}
    foreach ($Spec in @(
        @('Item', 'MoveToContainer', 6), @('Item', 'CanMoveTo', 3),
        @('ItemContainer', 'CanAcceptItem', 3), @('Item', 'RemoveConflictingSlots', 3),
        @('Item', 'MaxStackable', 0), @('Item', 'CanStack', 1)
    )) {
        $Left = Get-Body $Opened[0] $Spec[0] $Spec[1] $Spec[2]
        $Right = Get-Body $Opened[1] $Spec[0] $Spec[1] $Spec[2]
        Assert-Evidence ($Left -ceq $Right) "Windows/Linux $($Spec[0])::$($Spec[1])"
        $Bodies[$Spec[1]] = $Left
    }
    Assert-Order $Bodies.MoveToContainer @(
        'Item::CanMoveTo(', 'ItemContainer::SlotTaken(',
        'ldfld System.Int32 ItemContainer::maxStackSize', 'Item::SplitItem(System.Int32)',
        'Item::MoveToContainer(', 'Item::Drop(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Quaternion)'
    )
    Assert-Order $Bodies.CanMoveTo @('ItemContainer::CanAcceptItem(', 'ItemContainer::capacity')
    Assert-Order $Bodies.CanAcceptItem @('ItemContainer::canAcceptItem', '::Invoke(!0,!1,!2)', 'ItemContainer::allowedContents')
    Assert-Order $Bodies.MoveToContainer @('ItemContainer::CanAccept(Item)', 'Item::RemoveConflictingSlots(', 'Item::SetParent(')
    Assert-Order $Bodies.RemoveConflictingSlots @('ItemContainer::get_HasAvailableSlotsDefined()', 'Item::RemoveFromContainer()', '::GiveItem(')
    $Container = $Opened[0].MainModule.Types | Where-Object FullName -CEQ 'ItemContainer'
    foreach ($Name in @('maxStackSize', 'canAcceptItem')) {
        $Field = @($Container.Fields | Where-Object Name -CEQ $Name)
        Assert-Evidence ($Field.Count -eq 1 -and $Field[0].IsPublic) "public mutable $Name"
    }
    Write-Host "[CarbonLuau:Player1FB] Static exposure confirmed: $script:Checks checks; this exposure checker does not establish supported-host G1 PASS. See separate live qualification."
}
finally { foreach ($Assembly in $Opened) { $Assembly.Dispose() } }
