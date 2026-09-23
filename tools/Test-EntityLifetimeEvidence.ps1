# Read-only Entity-1A investigation. This checks static target evidence and a
# snapshot-model counterexample, NOT live Rust/Carbon behavior or an adapter.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$NetworkAssembly,
    [Parameter(Mandatory = $true)][string]$CecilAssembly,
    [string]$CarbonHookDirectory,
    [string]$RustGlobalAssembly
)

$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilAssembly
$Opened = [System.Collections.Generic.List[System.IDisposable]]::new()
$script:Checks = 0

function Assert-Evidence([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw "[CarbonLuau:EntityEvidence] Evidence changed: $Label" }
    $script:Checks++
}

function Read-Assembly([string]$Path) {
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    $Opened.Add($Assembly)
    Write-Host "[CarbonLuau:EntityEvidence] SHA256=$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash) MVID=$($Assembly.MainModule.Mvid) Path=$Path"
    return $Assembly
}

function Get-Method($Assembly, [string]$Spec) {
    $Parts = $Spec.Split('|')
    $Type = $Assembly.MainModule.GetType($Parts[0])
    if ($null -eq $Type) { throw "[CarbonLuau:EntityEvidence] Missing type: $Spec" }
    $Methods = @($Type.Methods | Where-Object {
        $_.Name -eq $Parts[1] -and $_.Parameters.Count -eq [int]$Parts[2]
    })
    if ($Methods.Count -ne 1 -or -not $Methods[0].HasBody) {
        throw "[CarbonLuau:EntityEvidence] Expected one method body: $Spec"
    }
    return $Methods[0]
}

function Get-Body($Method) {
    return ($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
}

function Get-Types($Types) {
    foreach ($Type in $Types) { $Type; Get-Types $Type.NestedTypes }
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

# Deliberately only the sampled host evidence, with all domain/publication/VM
# checks assumed valid. Incarnation is an oracle for this model, NOT a Rust field.
function Test-SampledLifetime($Captured, $Current) {
    return $Current.Alive -and
        [object]::ReferenceEquals($Captured.Object, $Current.Object) -and
        $Captured.Id -eq $Current.Id -and $Current.Id -ne 0 -and
        [object]::ReferenceEquals($Current.Occupant, $Captured.Object) -and
        $Captured.Prefab -ceq $Current.Prefab
}

try {
    $Windows = Read-Assembly $WindowsAssembly
    $Linux = Read-Assembly $LinuxAssembly
    $Network = Read-Assembly $NetworkAssembly
    $Specs = @(
        'BaseNetworkable/EntityRealm|Find|1',
        'BaseNetworkable/EntityRealm|RegisterID|1',
        'BaseNetworkable/EntityRealm|UnregisterID|1',
        'BaseNetworkable|Spawn|0', 'BaseNetworkable|SpawnShared|0',
        'BaseNetworkable|Kill|2', 'BaseNetworkable|DoEntityDestroy|0',
        'BaseNetworkable|TerminateOnServer|0', 'BaseNetworkable|EntityDestroy|0',
        'BaseNetworkable|InitLoad|1', 'GameManager|Retire|1',
        'GameManager|Instantiate|3', 'BaseNetworkable|KillAsMapEntity|0',
        'PrefabPoolCollection|Push|1', 'PrefabPoolCollection|Pop|3',
        'PrefabPool|Pop|2', 'Poolable|EnterPool|1', 'Poolable|LeavePool|0',
        'PoolableEx|SupportsPooling|1', 'PrefabPreProcess|ProcessObject|3'
    )
    foreach ($Spec in $Specs) {
        $Left = Get-Method $Windows $Spec
        $Right = Get-Method $Linux $Spec
        Assert-Evidence ((Get-Body $Left) -ceq (Get-Body $Right)) "Windows/Linux selected IL: $Spec"
        Assert-Evidence ($Left.Attributes -eq $Right.Attributes) "Windows/Linux visibility: $Spec"
    }
    Assert-Order (Get-Method $Linux 'BaseNetworkable/EntityRealm|Find|1') @('::TryGetValue(')
    Assert-Order (Get-Method $Linux 'BaseNetworkable|TerminateOnServer|0') @('::UnregisterID(', '::DestroyNetworkable(')
    Assert-Order (Get-Method $Linux 'BaseNetworkable|EntityDestroy|0') @('::ResetState()', '::Retire(')
    Assert-Order (Get-Method $Linux 'BaseNetworkable|SpawnShared|0') @('ldc.i4.0', '::set_IsDestroyed(', '::Register(')
    Assert-Order (Get-Method $Linux 'GameManager|Retire|1') @('::SupportsPooling(', '::Push(')
    Assert-Order (Get-Method $Linux 'GameManager|Instantiate|3') @('::Pop(')
    Assert-Order (Get-Method $Linux 'BaseNetworkable|InitLoad|1') @('::CreateNetworkable(', '::RegisterID(')
    Assert-Order (Get-Method $Network 'Network.Server|CreateNetworkable|1') @('::Get<Network.Networkable>()', 'stfld NetworkableId Network.Networkable::ID', '::RegisterUID(')
    Assert-Order (Get-Method $Network 'Network.Server|DestroyNetworkable|1') @('::Destroy()', '::Free<Network.Networkable>(')
    Assert-Order (Get-Method $Network 'Network.Networkable|EnterPool|0') @('::ID', 'initobj NetworkableId')
    Assert-Evidence ((Get-Method $Network 'Network.Networkable|LeavePool|0').Body.Instructions.Count -eq 1) 'Networkable LeavePool has no incarnation counter'
    Assert-Evidence ((Get-Method $Network 'Network.Server|ReturnUID|1').Body.Instructions.Count -eq 1) 'ReturnUID is a no-op; ordinary fresh allocation is not a free-ID pool'
    Assert-Order (Get-Method $Network 'Network.Server|TakeUID|0') @('::lastValueGiven', 'add', 'stfld System.UInt64 Network.Server::lastValueGiven')
    Assert-Order (Get-Method $Linux 'PrefabPool|Pop|2') @('::Pop()', '::LeavePool()', '::get_gameObject()')
    Assert-Evidence (@($Linux.MainModule.GetType('Poolable').Interfaces | Where-Object { $_.InterfaceType.Name -eq 'IClientComponent' }).Count -eq 1) 'Poolable is an IClientComponent; generic pool code does not prove server-prefab retention'
    Assert-Order (Get-Method $Linux 'PrefabPreProcess|.cctor|0') @('IClientComponent', '::clientsideOnlyTypes')
    Assert-Evidence ($Linux.MainModule.GetType('BaseNetworkable/EntityRealm').Events.Count -eq 0) 'EntityRealm exposes no CLR registry events'
    if ($RustGlobalAssembly) {
        $Global = Read-Assembly $RustGlobalAssembly
        Assert-Evidence ($Global.MainModule.GetType('Rust.Registry.Entity').Events.Count -eq 0) 'Transform entity registry exposes no CLR events'
    }
    if ($CarbonHookDirectory) {
        $HookNames = [System.Collections.Generic.List[string]]::new()
        foreach ($Name in @('Carbon.Hooks.Base.dll','Carbon.Hooks.Community.dll','Carbon.Hooks.Oxide.dll')) {
            $Hooks = Read-Assembly (Join-Path $CarbonHookDirectory $Name)
            foreach ($Type in (Get-Types $Hooks.MainModule.Types)) {
                foreach ($Attribute in $Type.CustomAttributes) {
                    if ($Attribute.AttributeType.Name -notin @('Patch','PatchAttribute')) { continue }
                    $Arguments = $Attribute.ConstructorArguments
                    if ($Arguments.Count -lt 4) { continue }
                    $Target = [string]$Arguments[2].Value
                    $Method = [string]$Arguments[3].Value
                    if ($Target -eq 'BaseNetworkable') {
                        $HookNames.Add([string]$Arguments[0].Value)
                        Write-Host "[CarbonLuau:EntityEvidence] Hook $($Arguments[0].Value) -> $Target.$Method"
                    }
                    # This negative check is limited to the shipped hook metadata,
                    # not a claim about every future Carbon extension/patch.
                    Assert-Evidence (-not ($Target -in @('BaseNetworkable/EntityRealm','BaseNetworkable+EntityRealm','Rust.Registry.Entity','PrefabPool','PrefabPoolCollection','Poolable') -or
                        ($Target -eq 'BaseNetworkable' -and $Method -in @('TerminateOnServer','EntityDestroy','DoEntityDestroy','InitLoad')))) "No shipped registry/pool/retirement patch: $Target.$Method"
                }
            }
        }
        Assert-Evidence ($HookNames.Contains('OnEntityKill') -and $HookNames.Contains('OnEntitySpawn') -and $HookNames.Contains('OnEntitySpawned')) 'Expected generic lifecycle hooks are present'
    }

    $Entity = [object]::new()
    $Captured = @{ Object = $Entity; Id = [uint64]42; Prefab = 'same-prefab'; Incarnation = 1 }
    $Current = @{ Object = $Entity; Id = [uint64]42; Prefab = 'same-prefab'; Occupant = $Entity; Alive = $true; Incarnation = 1 }
    Assert-Evidence (Test-SampledLifetime $Captured $Current) 'Model: unchanged lifetime passes'
    $Current.Alive = $false
    Assert-Evidence (-not (Test-SampledLifetime $Captured $Current)) 'Model: observed removal fails'
    $Current.Alive = $true
    $Current.Id = [uint64]43
    Assert-Evidence (-not (Test-SampledLifetime $Captured $Current)) 'Model: same object/new ID fails'
    $Current.Id = [uint64]42
    $Current.Object = [object]::new()
    $Current.Occupant = $Current.Object
    Assert-Evidence (-not (Test-SampledLifetime $Captured $Current)) 'Model: same ID/new object fails'
    $Current.Object = $Entity
    $Current.Occupant = $Entity
    $Current.Incarnation = 2
    Assert-Evidence ($Captured.Incarnation -ne $Current.Incarnation -and (Test-SampledLifetime $Captured $Current)) 'Model: unobserved same-object/same-ID/same-prefab ABA is indistinguishable'
    Write-Host "[CarbonLuau:EntityEvidence] $script:Checks structural/model checks passed. Entity-1A remains BLOCKED; this checker is not live evidence or a lifetime-safe adapter."
}
finally {
    foreach ($Assembly in $Opened) { $Assembly.Dispose() }
}
