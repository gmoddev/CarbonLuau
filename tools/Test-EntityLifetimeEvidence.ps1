# Read-only Entity-1A investigation. This checks static target evidence and a
# snapshot-model counterexample, NOT live Rust/Carbon behavior or an adapter.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowsAssembly,
    [Parameter(Mandatory = $true)][string]$LinuxAssembly,
    [Parameter(Mandatory = $true)][string]$NetworkAssembly,
    [Parameter(Mandatory = $true)][string]$CecilAssembly,
    [string]$CarbonHookDirectory,
    [string]$WindowsHookAssembly,
    [string]$WindowsUpdatedCommunityHookAssembly,
    [string]$WindowsOxideHookAssembly,
    [string]$WindowsUpdatedOxideHookAssembly,
    [string]$WindowsBootstrapAssembly,
    [string]$LinuxBootstrapAssembly,
    [string]$RustGlobalAssembly,
    [string]$HarmonyAssembly,
    [switch]$SpawnEpochProof,
    [switch]$CompletionResearch,
    [switch]$StartupObserverProof
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
    Write-Host "[CarbonLuau:EntityEvidence] SHA256=$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash) Version=$($Assembly.Name.Version) MVID=$($Assembly.MainModule.Mvid) Path=$Path"
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
    if ($HarmonyAssembly) { $Harmony = Read-Assembly $HarmonyAssembly }
    $Specs = @(
        'BaseNetworkable/EntityRealm|Find|1',
        'BaseNetworkable/EntityRealm|RegisterID|1',
        'BaseNetworkable/EntityRealm|UnregisterID|1',
        'BaseNetworkable|Spawn|0', 'BaseNetworkable|SpawnShared|0',
        'BaseNetworkable|SendNetworkUpdateImmediate|0',
        'OnSendNetworkUpdateEx|SendOnSendNetworkUpdate|2',
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
        $EntitySpawnHooks = [System.Collections.Generic.List[string]]::new()
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
                    if ($Target -eq 'BaseEntity' -and $Method -eq 'Spawn') {
                        $EntitySpawnHooks.Add([string]$Arguments[0].Value)
                    }
                    # This negative check is limited to the shipped hook metadata,
                    # not a claim about every future Carbon extension/patch.
                    Assert-Evidence (-not ($Target -in @('BaseNetworkable/EntityRealm','BaseNetworkable+EntityRealm','Rust.Registry.Entity','PrefabPool','PrefabPoolCollection','Poolable') -or
                        ($Target -eq 'BaseNetworkable' -and $Method -in @('TerminateOnServer','EntityDestroy','DoEntityDestroy','InitLoad')))) "No shipped registry/pool/retirement patch: $Target.$Method"
                }
            }
        }
        Assert-Evidence ($HookNames.Contains('OnEntityKill') -and $HookNames.Contains('OnEntitySpawn') -and $HookNames.Contains('OnEntitySpawned')) 'Expected generic lifecycle hooks are present'
        if ($CompletionResearch) {
            Write-Host "[CarbonLuau:EntityEvidence] Installed BaseEntity.Spawn hook patches: $($EntitySpawnHooks -join ', ')"
            Assert-Evidence ($EntitySpawnHooks.Count -eq 0) 'No shipped Carbon BaseEntity.Spawn completion patch'
        }
    }

    if ($SpawnEpochProof) {
        if (-not $CarbonHookDirectory) { throw '[CarbonLuau:EntityEvidence] Spawn-epoch proof requires installed Carbon hooks' }
        foreach ($Target in @($Windows, $Linux)) {
            $Writers = [System.Collections.Generic.List[string]]::new()
            $AddressTakers = [System.Collections.Generic.List[string]]::new()
            $InitLoadCallers = [System.Collections.Generic.List[string]]::new()
            foreach ($Type in (Get-Types $Target.MainModule.Types)) {
                foreach ($Method in $Type.Methods) {
                    if (-not $Method.HasBody) { continue }
                    foreach ($Instruction in $Method.Body.Instructions) {
                        if ($null -eq $Instruction.Operand) { continue }
                        $Operand = [string]$Instruction.Operand
                        if ($Operand -eq 'System.Boolean BaseNetworkable::isSpawned') {
                            if ($Instruction.OpCode.Name -eq 'stfld') { $Writers.Add($Method.FullName) }
                            if ($Instruction.OpCode.Name -eq 'ldflda') { $AddressTakers.Add($Method.FullName) }
                        }
                        if ($Instruction.OpCode.Name -in @('call', 'callvirt') -and
                            $Operand -eq 'System.Void BaseNetworkable::InitLoad(NetworkableId)') {
                            $InitLoadCallers.Add($Method.FullName)
                        }
                    }
                }
            }
            $ExpectedWriters = @('System.Void BaseNetworkable::DoServerDestroy()', 'System.Void BaseNetworkable::Spawn()')
            $WritersMatch = $Writers.Count -eq 2 -and
                @($Writers | Sort-Object | Compare-Object -ReferenceObject ($ExpectedWriters | Sort-Object)).Count -eq 0
            Assert-Evidence $WritersMatch 'Only Spawn and DoServerDestroy write isSpawned'
            Assert-Evidence ($AddressTakers.Count -eq 0) 'No isSpawned address-taken writer'
            Assert-Order (Get-Method $Target 'BaseNetworkable|Spawn|0') @('ldc.i4.1', 'stfld System.Boolean BaseNetworkable::isSpawned')
            Assert-Order (Get-Method $Target 'BaseNetworkable|Spawn|0') @(
                'stfld System.Boolean BaseNetworkable::isSpawned',
                '::SendNetworkUpdateImmediate()', '::Invoke(', 'ret')
            Assert-Order (Get-Method $Target 'BaseNetworkable|SendNetworkUpdateImmediate|0') @(
                '::SendOnSendNetworkUpdate(')
            Assert-Order (Get-Method $Target 'OnSendNetworkUpdateEx|SendOnSendNetworkUpdate|2') @(
                'IOnSendNetworkUpdate::OnSendNetworkUpdate(')
            Assert-Order (Get-Method $Target 'BaseNetworkable|DoServerDestroy|0') @('ldc.i4.0', 'stfld System.Boolean BaseNetworkable::isSpawned')
            $FullySpawned = Get-Method $Target 'BaseNetworkable|IsFullySpawned|0'
            $GetterMatches = $FullySpawned.Body.Instructions.Count -eq 3 -and
                $FullySpawned.Body.Instructions[1].Operand.ToString() -eq 'System.Boolean BaseNetworkable::isSpawned'
            Assert-Evidence $GetterMatches 'IsFullySpawned reads only isSpawned'
            $ExpectedCallers = @(
                'System.Boolean SaveRestore::Load(System.String,System.Boolean)',
                'System.Collections.Generic.List`1<BaseEntity> ConVar.CopyPaste::PasteEntitiesInternal(ProtoBuf.CopyPasteEntityInfo,ConVar.CopyPaste/PasteOptions,System.UInt64)',
                'System.Void Rust.Nexus.Handlers.TransferHandler::SpawnEntities(System.Collections.Generic.Dictionary`2<System.UInt64,BasePlayer>)'
            )
            $CallersMatch = $InitLoadCallers.Count -eq 3 -and
                @($InitLoadCallers | Sort-Object | Compare-Object -ReferenceObject ($ExpectedCallers | Sort-Object)).Count -eq 0
            Assert-Evidence $CallersMatch 'All direct InitLoad callers enumerated'
            foreach ($Caller in $InitLoadCallers) {
                Write-Host "[CarbonLuau:EntityEvidence] InitLoad caller: $Caller"
            }
            Write-Host "[CarbonLuau:EntityEvidence] Target writers: $($Writers -join ', ')"
        }
        $Community = $Opened | Where-Object { $_.MainModule.Name -eq 'Carbon.Hooks.Community.dll' } | Select-Object -First 1
        $PrefixType = @(Get-Types $Community.MainModule.Types | Where-Object {
            $_.FullName -eq 'Carbon.Hooks.Category_Entity/Entity_BaseNetworkable/OnEntitySpawn'
        })
        Assert-Evidence ($PrefixType.Count -eq 1) 'Installed OnEntitySpawn patch type'
        $Prefix = @($PrefixType[0].Methods | Where-Object { $_.Name -eq 'Prefix' })
        $PrefixMatches = $Prefix.Count -eq 1 -and $Prefix[0].Parameters.Count -eq 1 -and
            $Prefix[0].Parameters[0].ParameterType.FullName -eq 'BaseNetworkable&'
        Assert-Evidence $PrefixMatches 'Installed OnEntitySpawn synchronous prefix signature'
        Assert-Order $Prefix[0] @('ldind.ref', '::CallStaticHook(', 'pop', 'ret')
        if ($WindowsHookAssembly) {
            $WindowsCommunity = Read-Assembly $WindowsHookAssembly
            $WindowsPrefixType = @(Get-Types $WindowsCommunity.MainModule.Types | Where-Object {
                $_.FullName -eq 'Carbon.Hooks.Category_Entity/Entity_BaseNetworkable/OnEntitySpawn'
            })
            Assert-Evidence ($WindowsPrefixType.Count -eq 1) 'Windows installed OnEntitySpawn patch type'
            $WindowsPrefix = @($WindowsPrefixType[0].Methods | Where-Object { $_.Name -eq 'Prefix' })
            Assert-Evidence ($WindowsPrefix.Count -eq 1 -and (Get-Body $WindowsPrefix[0]) -ceq (Get-Body $Prefix[0]))
                'Windows/Linux exact Spawn prefix body'
        }
        Write-Host '[CarbonLuau:EntityEvidence] Spawn prefix and fallible post-flag update tail structurally confirmed; live callback failure remains a separate gate.'
    }

    if ($CompletionResearch) {
        foreach ($Target in @($Windows, $Linux)) {
            $BaseSpawn = Get-Method $Target 'BaseNetworkable|Spawn|0'
            $EntitySpawn = Get-Method $Target 'BaseEntity|Spawn|0'
            $ParentBroadcast = Get-Method $Target 'OnParentSpawningEx|BroadcastOnParentSpawning|1'
            Assert-Evidence (@($BaseSpawn.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' }).Count -eq 1 -and
                $BaseSpawn.Body.ExceptionHandlers.Count -eq 0) 'BaseNetworkable.Spawn has one normal return and no local catch/finally'
            Assert-Order $EntitySpawn @('BaseNetworkable::Spawn()', 'OnParentSpawningEx::BroadcastOnParentSpawning(')
            Assert-Evidence ($EntitySpawn.Body.ExceptionHandlers.Count -eq 0) 'BaseEntity.Spawn does not catch a post-base callback failure'
            Assert-Order $ParentBroadcast @('IOnParentSpawning::OnParentSpawning(')
            Assert-Evidence ($ParentBroadcast.Body.ExceptionHandlers.Count -eq 0) 'Parent-spawn component callback exceptions are not caught by broadcaster'
            $SpawnMethods = @(foreach ($Type in (Get-Types $Target.MainModule.Types)) {
                foreach ($Method in $Type.Methods) {
                    if ($Method.Name -eq 'Spawn' -and $Method.Parameters.Count -eq 0 -and $Method.HasBody) { $Method }
                }
            })
            # Research ledger only: this is not an installed patch or a
            # successful-completion proof. Fail if the exact virtual surface
            # changes before a future AutoPatch adapter is considered.
            $ExpectedEntityOverrides = @(
                'BaseEntity', 'CargoShip', 'ClanManager', 'ExcavatorArm',
                'JunkPile', 'DiveSite', 'JunkPileWater', 'Marketplace',
                'ProceduralLift', 'BaseVehicle', 'TrainCar',
                'TrainCarUnloadableLoot', 'VehicleModuleCamper',
                'VehicleModuleStorage', 'SlotMachine', 'HalloweenDungeon',
                'IOEntity', 'PoweredRemoteControlEntity', 'ElevatorStatic',
                'SlidingProgressDoor', 'Lift', 'Drone', 'ItemPickup'
            )
            $ActualEntityOverrides = @($SpawnMethods | Where-Object {
                $_.DeclaringType.FullName -notin @('BaseNetworkable', 'SpawnGroup')
            } | ForEach-Object { $_.DeclaringType.FullName })
            Assert-Evidence ($SpawnMethods.Count -eq 25 -and
                $ActualEntityOverrides.Count -eq 23 -and
                @($ActualEntityOverrides | Sort-Object | Compare-Object -ReferenceObject ($ExpectedEntityOverrides | Sort-Object)).Count -eq 0) `
                'Exact BaseEntity virtual Spawn override inventory is unchanged'
            foreach ($Override in @($SpawnMethods | Where-Object {
                $_.DeclaringType.FullName -in $ExpectedEntityOverrides
            })) {
                Assert-Evidence ($Override.IsVirtual -and -not $Override.IsNewSlot -and
                    $Override.ReturnType.FullName -eq 'System.Void') `
                    "$($Override.FullName): existing void virtual Spawn slot"
                $Ancestor = $Override.DeclaringType.BaseType.Resolve()
                $ImmediateBaseSpawn = $null
                while ($null -ne $Ancestor -and $null -eq $ImmediateBaseSpawn) {
                    $Candidate = @($Ancestor.Methods | Where-Object {
                        $_.Name -eq 'Spawn' -and $_.Parameters.Count -eq 0 -and
                        $_.ReturnType.FullName -eq 'System.Void'
                    })
                    if ($Candidate.Count -gt 1) { throw '[CarbonLuau:EntityEvidence] Ambiguous base Spawn body' }
                    if ($Candidate.Count -eq 1) { $ImmediateBaseSpawn = $Candidate[0] }
                    elseif ($null -ne $Ancestor.BaseType) { $Ancestor = $Ancestor.BaseType.Resolve() }
                    else { $Ancestor = $null }
                }
                Assert-Evidence ($null -ne $ImmediateBaseSpawn) "$($Override.FullName): base Spawn body exists"
                $BaseCalls = @($Override.Body.Instructions | Where-Object {
                    $_.OpCode.Name -in @('call', 'callvirt') -and
                    [string]$_.Operand -eq $ImmediateBaseSpawn.FullName
                })
                Assert-Evidence ($BaseCalls.Count -eq 1) "$($Override.FullName): calls immediate base Spawn exactly once"
            }
            Write-Host "[CarbonLuau:EntityEvidence] Spawn method bodies in $($Target.MainModule.Name): $($SpawnMethods.Count)"
            foreach ($Method in $SpawnMethods) {
                $CallsBase = @($Method.Body.Instructions | Where-Object {
                    $_.OpCode.Name -in @('call', 'callvirt') -and
                    [string]$_.Operand -eq 'System.Void BaseNetworkable::Spawn()'
                })
                Write-Host "[CarbonLuau:EntityEvidence] Spawn body $($Method.FullName); calls BaseNetworkable.Spawn=$($CallsBase.Count)"
                if ($CallsBase.Count -gt 0) {
                    $Instructions = @($Method.Body.Instructions)
                    $CallIndex = [array]::IndexOf($Instructions, $CallsBase[0])
                    $Tail = @($Instructions[($CallIndex + 1)..($Instructions.Count - 1)] | Where-Object {
                        $_.OpCode.Name -in @('call', 'callvirt', 'throw', 'rethrow')
                    } | ForEach-Object { $_.ToString() })
                    Write-Host "[CarbonLuau:EntityEvidence] After base Spawn: $($Tail -join ' | ')"
                }
            }
        }
    }

    if ($StartupObserverProof) {
        if (!$WindowsBootstrapAssembly -or !$LinuxBootstrapAssembly -or
            !$WindowsOxideHookAssembly -or !$CarbonHookDirectory) {
            throw '[CarbonLuau:EntityEvidence] Startup proof requires both bootstrap assemblies and both Oxide hook assemblies'
        }
        foreach ($Target in @($Windows, $Linux)) {
            Assert-Order (Get-Method $Target 'ServerMgr|Initialize|4') @(
                'SaveRestore::Load(', 'SaveRestore::SpawnMapEntities(',
                'SpawnHandler::InitialSpawn()', 'SpawnHandler::StartSpawnTick()')
        }
        $WindowsBootstrap = Read-Assembly $WindowsBootstrapAssembly
        $LinuxBootstrap = Read-Assembly $LinuxBootstrapAssembly
        foreach ($Spec in @('Patches.FileSystem_WarmupHalt|Prefix|3',
                            'Patches.FileSystem_WarmupHalt/<Process>d__2|MoveNext|0')) {
            $Left = Get-Method $WindowsBootstrap $Spec
            $Right = Get-Method $LinuxBootstrap $Spec
            Assert-Evidence ((Get-Body $Left) -ceq (Get-Body $Right)) "Windows/Linux bootstrap IL: $Spec"
        }
        Assert-Order (Get-Method $LinuxBootstrap 'Patches.FileSystem_WarmupHalt|Prefix|3') @(
            'FileSystem_WarmupHalt::IsReady', 'ModLoader::IsBatchComplete',
            'FileSystem_WarmupHalt::Process(')
        Assert-Order (Get-Method $LinuxBootstrap 'Patches.FileSystem_WarmupHalt/<Process>d__2|MoveNext|0') @(
            'FileSystem_WarmupHalt::IsReady', 'ModLoader::IsBatchComplete',
            'FileSystem_WarmupHalt::AllowNative', 'FileSystem_Warmup::Run(')
        $WindowsOxide = Read-Assembly $WindowsOxideHookAssembly
        $LinuxOxide = $Opened | Where-Object { $_.MainModule.Name -eq 'Carbon.Hooks.Oxide.dll' } | Select-Object -First 1
        $Generated = 'Carbon.Hooks.Category_Entity/Entity_BaseNetworkable/Entity_BaseNetworkable_7cc7a8d1f2b14faf96c00d917542524f'
        foreach ($Spec in @("$Generated|Transpiler|3", "$Generated/<Transpiler>d__0|MoveNext|0")) {
            $Left = Get-Method $WindowsOxide $Spec
            $Right = Get-Method $LinuxOxide $Spec
            Assert-Evidence ((Get-Body $Left) -ceq (Get-Body $Right)) "Windows/Linux Carbon Spawn transpiler IL: $Spec"
        }
        Assert-Evidence ((Get-Body (Get-Method $LinuxOxide "$Generated/<Transpiler>d__0|MoveNext|0")) -like '*CallStaticHook*') `
            'Qualified Carbon Spawn transpiler contains the OnEntitySpawned hook insertion'
        if ($WindowsUpdatedOxideHookAssembly) {
            $UpdatedOxide = Read-Assembly $WindowsUpdatedOxideHookAssembly
            Assert-Evidence (((Get-FileHash -LiteralPath $WindowsUpdatedOxideHookAssembly -Algorithm SHA256).Hash) -eq
                '0B668E1F4819EF3455C70781428C0AFDD02601D2398EEF71BD28428A8A073AFA') `
                'Updated Windows hook-component hash'
            Assert-Evidence ($UpdatedOxide.MainModule.Mvid.ToString() -eq 'eaf4dad6-6dd5-4f38-a5af-d61fab1780bc') `
                'Updated Windows hook-component MVID'
            $UpdatedGenerated = 'Carbon.Hooks.Category_Entity/Entity_BaseNetworkable/Entity_BaseNetworkable_3dfbe84a8fb1482ea238d43e7f12aece'
            foreach ($Pair in @(@('Transpiler', 'Transpiler', 3), @('<Transpiler>d__0', 'MoveNext', 0))) {
                $OldType = if ($Pair[0] -eq 'Transpiler') { $Generated } else { "$Generated/$($Pair[0])" }
                $NewType = if ($Pair[0] -eq 'Transpiler') { $UpdatedGenerated } else { "$UpdatedGenerated/$($Pair[0])" }
                $OldBody = (Get-Body (Get-Method $LinuxOxide "$OldType|$($Pair[1])|$($Pair[2])")) -replace
                    '7cc7a8d1f2b14faf96c00d917542524f', 'GEN'
                $NewBody = (Get-Body (Get-Method $UpdatedOxide "$NewType|$($Pair[1])|$($Pair[2])")) -replace
                    '3dfbe84a8fb1482ea238d43e7f12aece', 'GEN'
                Assert-Evidence ($OldBody -ceq $NewBody) "Updated Windows Spawn hook IL: $($Pair[1])"
            }
        }
        if ($WindowsUpdatedCommunityHookAssembly) {
            if (!$WindowsHookAssembly) { throw '[CarbonLuau:EntityEvidence] Updated Community proof requires original Windows Community assembly' }
            $OriginalCommunity = Read-Assembly $WindowsHookAssembly
            $UpdatedCommunity = Read-Assembly $WindowsUpdatedCommunityHookAssembly
            Assert-Evidence (((Get-FileHash -LiteralPath $WindowsUpdatedCommunityHookAssembly -Algorithm SHA256).Hash) -eq
                '4364A8782FD012DD8CFA8ACF1D778C1E6BEE9BF6BBE0EFB801DEBA685778F50E') `
                'Updated Windows Community hook-component hash'
            Assert-Evidence ($UpdatedCommunity.MainModule.Mvid.ToString() -eq 'e7a46446-eb51-426d-908e-59aa9cdb4677') `
                'Updated Windows Community hook-component MVID'
            $SpawnPrefix = 'Carbon.Hooks.Category_Entity/Entity_BaseNetworkable/OnEntitySpawn|Prefix|1'
            Assert-Evidence ((Get-Body (Get-Method $OriginalCommunity $SpawnPrefix)) -ceq
                (Get-Body (Get-Method $UpdatedCommunity $SpawnPrefix))) `
                'Updated Windows OnEntitySpawn prefix IL'
        }
        Write-Host '[CarbonLuau:EntityEvidence] Startup bootstrap and installed Carbon Spawn-transpiler identities structurally matched; live observer timing is a separate gate.'
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
    Write-Host "[CarbonLuau:EntityEvidence] $script:Checks structural/model checks passed. This checker is not live evidence or a lifetime-safe adapter."
}
finally {
    foreach ($Assembly in $Opened) { $Assembly.Dispose() }
}
