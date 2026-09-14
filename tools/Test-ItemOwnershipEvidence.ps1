# Read-only D13 structural evidence, NOT an adapter/lifecycle safety test.
# Run against retained qualified assemblies on dockerbox; never load game code.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$NetworkAssembly,
    [Parameter(Mandatory = $true)][string]$CecilAssembly
)

$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilAssembly
$Opened = [System.Collections.Generic.List[System.IDisposable]]::new()
$script:Checks = 0

function Assert-Evidence([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw "[CarbonLuau:D13] Evidence changed: $Label" }
    $script:Checks++
}

function Read-Assembly([string]$Path) {
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    $Opened.Add($Assembly)
    Write-Host "[CarbonLuau:D13] $Path SHA256=$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash) MVID=$($Assembly.MainModule.Mvid)"
    return $Assembly
}

function Get-Method($Assembly, [string]$Spec) {
    $Parts = $Spec.Split('|')
    $Types = @($Assembly.MainModule.Types | Where-Object FullName -eq $Parts[0])
    if ($Types.Count -ne 1) { throw "[CarbonLuau:D13] Expected one type: $Spec" }
    $Methods = @($Types[0].Methods | Where-Object {
        $_.Name -eq $Parts[1] -and $_.Parameters.Count -eq [int]$Parts[2]
    })
    if ($Methods.Count -ne 1 -or -not $Methods[0].HasBody) {
        throw "[CarbonLuau:D13] Expected one method body: $Spec"
    }
    return $Methods[0]
}

function Get-Body($Method) {
    return ($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
}

function Assert-Order($Method, [string[]]$Fragments) {
    $Body = Get-Body $Method
    $Position = 0
    foreach ($Fragment in $Fragments) {
        $Found = $Body.IndexOf($Fragment, $Position, [StringComparison]::Ordinal)
        Assert-Evidence ($Found -ge 0) "$($Method.FullName): ordered $Fragment"
        $Position = $Found + $Fragment.Length
    }
}

try {
    $Windows = Read-Assembly $WindowsAssembly
    $Linux = Read-Assembly $LinuxAssembly
    $Network = Read-Assembly $NetworkAssembly
    # Exact signatures avoid selecting unrelated overloads. This is a targeted
    # comparison, not equivalence of all host code or runtime Carbon patches.
    $Specs = @(
        'Item|.ctor|0', 'Item|Facepunch.Pool.IPooled.EnterPool|0',
        'Item|Facepunch.Pool.IPooled.LeavePool|0', 'Item|Initialize|1',
        'Item|InitializeItemOwnership|0', 'Item|OnItemCreated|0',
        'Item|Remove|1', 'Item|DoRemove|0', 'Item|RemoveFromWorld|0',
        'Item|RemoveFromContainer|0', 'Item|SetParent|1', 'Item|SplitItem|1',
        'Item|MoveToContainer|6', 'Item|Drop|3',
        'ItemContainer|Insert|1', 'ItemContainer|Remove|1',
        'ItemContainer|FindPosition|1', 'ItemContainer|SlotTaken|2',
        'ItemContainer|CanAcceptItem|3', 'ItemContainer|Kill|0',
        'ItemContainer|Clear|0', 'ItemContainer|Facepunch.Pool.IPooled.EnterPool|0',
        'ItemContainer|Facepunch.Pool.IPooled.LeavePool|0',
        'ItemModEntity|OnItemCreated|1', 'ItemModEntity|CreateEntity|1',
        'ItemModEntity|OnRemove|1', 'ItemManager|Create|5',
        'ItemManager|RemoveItem|2', 'ItemManager|DoRemoves|1',
        'GameManager|CreateEntity|4', 'BaseNetworkable|Spawn|0',
        'BaseNetworkable|Kill|2'
    )
    foreach ($Spec in $Specs) {
        $Left = Get-Method $Windows $Spec
        $Right = Get-Method $Linux $Spec
        Assert-Evidence ((Get-Body $Left) -ceq (Get-Body $Right)) "Windows/Linux IL $Spec"
        Assert-Evidence ($Left.Attributes -eq $Right.Attributes) "Windows/Linux visibility $Spec"
    }
    Write-Host "[CarbonLuau:D13] Identical selected Windows/Linux bodies and visibility: $($Specs.Count)"

    foreach ($Spec in @('Item|.ctor|0', 'Item|Initialize|1')) {
        Assert-Evidence (Get-Method $Windows $Spec).IsPublic "Public construction step $Spec"
    }
    $Constructor = Get-Method $Windows 'Item|.ctor|0'
    $Calls = @($Constructor.Body.Instructions | Where-Object { $_.OpCode.Code -in 'Call', 'Callvirt', 'Newobj' })
    Assert-Evidence ($Calls.Count -eq 1 -and $Calls[0].Operand.FullName -eq 'System.Void System.Object::.ctor()') 'Item constructor has no item callbacks'

    foreach ($Spec in @('ItemManager|Create|5', 'Item|Initialize|1', 'Item|OnItemCreated|0',
        'ItemModEntity|CreateEntity|1', 'ItemModEntity|OnRemove|1', 'Item|Remove|1',
        'Item|DoRemove|0', 'ItemContainer|Insert|1', 'ItemContainer|Remove|1',
        'ItemContainer|SlotTaken|2', 'BaseNetworkable|Spawn|0', 'BaseNetworkable|Kill|2')) {
        Assert-Evidence ((Get-Method $Windows $Spec).Body.ExceptionHandlers.Count -eq 0) "No local exception cleanup $Spec"
    }
    Assert-Order (Get-Method $Windows 'Item|Initialize|1') @('Network.Server::TakeUID()', 'stfld ItemId Item::uid', 'Item::OnItemCreated()')
    Assert-Order (Get-Method $Windows 'ItemModEntity|CreateEntity|1') @('GameManager::CreateEntity(', 'BaseNetworkable::Spawn()', 'Item::SetHeldEntity(')
    Assert-Order (Get-Method $Windows 'ItemContainer|Insert|1') @('::Add(!0)', 'stfld ItemContainer Item::parent', 'ItemContainer::FindPosition(', 'ItemContainer::onItemAddedRemoved', 'ItemContainer::itemsWithOnCycle')
    Assert-Order (Get-Method $Windows 'ItemContainer|Remove|1') @('ItemContainer::onPreItemRemove', '::Invoke(!0)', '::Remove(!0)', 'stfld ItemContainer Item::parent', 'ItemContainer::onItemParentChanged')
    Assert-Order (Get-Method $Windows 'Item|Remove|1') @('ItemMod::OnRemove(', 'stfld System.Single Item::removeTime', 'ItemManager::RemoveItem(')
    Assert-Order (Get-Method $Windows 'ItemManager|DoRemoves|1') @('::RemoveAt(', 'Item::DoRemove()', 'Facepunch.Pool::Free<Item>')
    Assert-Order (Get-Method $Windows 'Item|SplitItem|1') @('stfld System.Int32 Item::amount', 'ItemManager::CreateByItemID(')
    Assert-Order (Get-Method $Windows 'ItemModEntity|OnRemove|1') @('BaseNetworkable::Kill(', 'Item::SetHeldEntity(')

    $ReturnUid = Get-Method $Network 'Network.Server|ReturnUID|1'
    Assert-Evidence ($ReturnUid.Body.Instructions.Count -eq 1 -and $ReturnUid.Body.Instructions[0].OpCode.Code -eq 'Ret') 'Inspected Windows ReturnUID is a no-op'
    Write-Host "[CarbonLuau:D13] Structural checks passed: $script:Checks. D13 remains BLOCKED; no item operation, injected failure or live grant ran."
}
finally {
    foreach ($Assembly in $Opened) { $Assembly.Dispose() }
}
