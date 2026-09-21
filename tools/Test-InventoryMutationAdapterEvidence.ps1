# Read-only Inventory-M2 exact-build evidence. This inspects metadata and IL;
# it never loads game code or performs an inventory mutation.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$CecilAssembly
)

$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilAssembly
$Opened = [System.Collections.Generic.List[System.IDisposable]]::new()
$script:Checks = 0

function Assert-Evidence([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw "[CarbonLuau:InventoryM2] Evidence changed: $Label" }
    $script:Checks++
}

function Read-Assembly([string]$Path) {
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    $Opened.Add($Assembly)
    Write-Host "[CarbonLuau:InventoryM2] $Path SHA256=$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash) MVID=$($Assembly.MainModule.Mvid)"
    return $Assembly
}

function Get-Type($Assembly, [string]$Name) {
    $Types = @($Assembly.MainModule.Types | Where-Object FullName -eq $Name)
    if ($Types.Count -ne 1) { throw "[CarbonLuau:InventoryM2] Expected one type: $Name" }
    return $Types[0]
}

function Get-Method($Assembly, [string]$Spec) {
    $Parts = $Spec.Split('|')
    $Methods = @((Get-Type $Assembly $Parts[0]).Methods | Where-Object {
        $_.Name -eq $Parts[1] -and $_.Parameters.Count -eq [int]$Parts[2]
    })
    if ($Methods.Count -ne 1 -or -not $Methods[0].HasBody) {
        throw "[CarbonLuau:InventoryM2] Expected one method body: $Spec"
    }
    return $Methods[0]
}

function Get-Field($Assembly, [string]$TypeName, [string]$FieldName) {
    $Fields = @((Get-Type $Assembly $TypeName).Fields | Where-Object Name -eq $FieldName)
    if ($Fields.Count -ne 1) { throw "[CarbonLuau:InventoryM2] Expected one field: $TypeName::$FieldName" }
    return $Fields[0]
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
    $Specs = @(
        'ItemManager|Create|5', 'Item|MoveToContainer|6', 'Item|SetParent|1',
        'Item|IsRemoved|0', 'Item|IsValid|0', 'Item|GetWorldEntity|0',
        'Item|Remove|1', 'ItemContainer|Insert|1', 'ItemContainer|Take|5',
        'PlayerInventory|ServerInit|1', 'PlayerInventory|Take|3'
    )
    foreach ($Spec in $Specs) {
        $Left = Get-Method $Windows $Spec
        $Right = Get-Method $Linux $Spec
        Assert-Evidence ((Get-Body $Left) -ceq (Get-Body $Right)) "Windows/Linux IL $Spec"
        Assert-Evidence ($Left.Attributes -eq $Right.Attributes) "Windows/Linux visibility $Spec"
    }

    foreach ($FieldSpec in @(
        @('PlayerInventory', 'containerMain'), @('PlayerInventory', 'containerBelt'),
        @('PlayerInventory', 'containerWear'), @('ItemContainer', 'capacity'),
        @('ItemContainer', 'itemList'), @('Item', 'parent'), @('Item', 'amount'),
        @('Item', 'position'), @('Item', 'removeTime')
    )) {
        Assert-Evidence (Get-Field $Windows $FieldSpec[0] $FieldSpec[1]).IsPublic "Public observation field $($FieldSpec[0])::$($FieldSpec[1])"
    }
    foreach ($Spec in @(
        'ItemManager|Create|5', 'Item|MoveToContainer|6', 'Item|IsRemoved|0',
        'Item|IsValid|0', 'Item|GetWorldEntity|0', 'Item|Remove|1',
        'PlayerInventory|Take|3'
    )) {
        Assert-Evidence (Get-Method $Windows $Spec).IsPublic "Public adapter method $Spec"
    }

    $Move = Get-Method $Windows 'Item|MoveToContainer|6'
    $ExpectedParameters = @('newcontainer', 'iTargetPos', 'allowStack', 'ignoreStackLimit', 'sourcePlayer', 'allowSwap')
    for ($Index = 0; $Index -lt $ExpectedParameters.Count; ++$Index) {
        Assert-Evidence ($Move.Parameters[$Index].Name -ceq $ExpectedParameters[$Index]) "MoveToContainer parameter $Index"
    }
    Assert-Order $Move @(
        'Item::CanMoveTo(', 'ItemContainer::SlotTaken(', 'Item::MaxStackable()',
        'stfld System.Int32 Item::amount', 'Item::RemoveFromWorld()',
        'Item::RemoveFromContainer()', 'Item::Remove(System.Single)'
    )
    Assert-Order $Move @(
        'ldfld System.Int32 ItemContainer::maxStackSize', 'Item::SplitItem(System.Int32)',
        'Item::Drop(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Quaternion)'
    )
    Assert-Order $Move @(
        'ItemContainer::CanAccept(Item)', 'Item::RemoveFromContainer()',
        'Item::RemoveFromWorld()', 'Item::SetParent(ItemContainer)'
    )

    Assert-Order (Get-Method $Windows 'ItemManager|Create|5') @(
        'Facepunch.Pool::Get<Item>()', 'stfld ItemDefinition Item::info',
        'stfld System.Int32 Item::amount', 'Item::Initialize(ItemDefinition)'
    )
    Assert-Order (Get-Method $Windows 'Item|Remove|1') @(
        'ItemMod::OnRemove(Item)', 'stfld System.Single Item::removeTime',
        'stfld System.Int32 Item::position', 'stfld System.Int32 Item::amount',
        'ItemManager::RemoveItem(Item,System.Single)'
    )
    Assert-Order (Get-Method $Windows 'ItemContainer|Insert|1') @(
        'System.Collections.Generic.List`1<Item>::Add(!0)',
        'stfld ItemContainer Item::parent', 'ItemContainer::FindPosition(Item)',
        'System.Action`2<Item,System.Boolean>::Invoke(!0,!1)'
    )

    Assert-Order (Get-Method $Windows 'PlayerInventory|Take|3') @(
        'PlayerInventory::containerMain', 'ItemContainer::Take(',
        'PlayerInventory::containerBelt', 'ItemContainer::Take(',
        'PlayerInventory::containerWear', 'ItemContainer::Take('
    )
    $ContainerTake = Get-Method $Windows 'ItemContainer|Take|5'
    Assert-Order $ContainerTake @(
        'Item::SplitItem(System.Int32)', 'Item::CollectedForCrafting(BasePlayer)',
        'System.Collections.Generic.List`1<Item>::Add(!0)'
    )
    Assert-Order $ContainerTake @(
        'Item::UseItem(System.Int32)', 'Item::Remove(System.Single)',
        'ItemManager::DoRemoves(System.Boolean)'
    )

    $ServerInit = Get-Method $Windows 'PlayerInventory|ServerInit|1'
    Assert-Order $ServerInit @(
        'PlayerInventory::containerMain', 'ldc.i4.s 24', 'ItemContainer::ServerInitialize(',
        'PlayerInventory::containerBelt', 'ldc.i4.6', 'ItemContainer::ServerInitialize(',
        'PlayerInventory::containerWear', 'ldc.i4.8', 'ItemContainer::ServerInitialize('
    )

    Write-Host "[CarbonLuau:InventoryM2] Static adapter evidence passed: $script:Checks checks."
}
finally {
    foreach ($Assembly in $Opened) { $Assembly.Dispose() }
}
