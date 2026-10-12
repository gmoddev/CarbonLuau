# Read-only metadata research; never reflection-loads Rust, contacts a host,
# launches a server, installs patches, or writes evidence/source files.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputDirectory,
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [string]$MethodPattern = '^(BaseProjectile::(CLProject|ServerUse|AssignInitiator)|BaseMelee::(CLProject|PlayerAttack)|BaseLauncher::SV_Launch|BasePlayer::(OnProjectileAttack|NoteFiredProjectile|ServerNoteFiredProjectile|CleanupExpiredProjectiles|WoundingTick|WoundedStartSharedCode|BecomeWounded|OnAttacked)|BasePlayer/FiredProjectile::(EnterPool|LeavePool)|HitInfo::(Clear|CopyFrom|Facepunch.Pool.IPooled.EnterPool|Facepunch.Pool.IPooled.LeavePool|System.IDisposable.Dispose|Init)|ThrownWeapon::(DoThrow|DoThrowImpl)|TimedExplosive::(Explode|SetFuse|ServerInit|ProjectileImpact)|ServerProjectile::ProcessHit|RFTimedExplosive::Load|FireBall::DoRadialDamage|DamageUtil::RadiusDamage|ItemModProjectileRadialDamage::ServerProjectileHit|AutoTurret::(ApplyDamage|FireGun)|BearTrap::DelayedFire|GunTrap::FireBullet|Landmine::Explode|FlameTurret::DoFlame|BeeTriggerHurt::ModifyHit|BaseNpc::StartAttack|DecayEntity::OnDecay|BaseMetabolism`1::DoMetabolismDamage|PlayerMetabolism::DoMetabolismDamage|BaseCombatEntity::(Hurt|OnAttacked)|Rust.Ai.Gen2.Trans_Triggerable_HitInfo::Trigger)$',
    [switch]$IncludeEdges,
    [switch]$VerifyReport,
    [switch]$IncludeInstructions
)
$ErrorActionPreference = 'Stop'
function Get-TextHash([string]$Value) {
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($Hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-', '').ToLowerInvariant() }
    finally { $Hasher.Dispose() }
}
function Get-Token($Member) { return '0x{0:x8}' -f $Member.MetadataToken.ToUInt32() }
function Assert-Hash([string]$Path, [string]$Expected) {
    $Actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($Actual -cne $Expected) { throw '[Gameplay:AttributionOffline] Input hash mismatch.' }
    return $Actual
}
try {
    $InputRoot = (Resolve-Path -LiteralPath $InputDirectory).Path
    $EvidenceRoot = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
    $CecilPath = Join-Path $InputRoot 'Mono.Cecil.dll'
    $CecilHash = Assert-Hash $CecilPath '0c6798722b72899c54648e58a950a945163f6e2d6ebbc1a1e11c6f771cdaed0f'
    # Validate every required input BEFORE loading even the trusted metadata reader.
    $Pins = @(
        @('win', 'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2'),
        @('linux', 'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450')
    )
    $InputPaths = @{}
    foreach ($Pin in $Pins) {
        $Path = Join-Path $InputRoot ('Assembly-CSharp-' + $Pin[0] + '.dll')
        $null = Assert-Hash $Path $Pin[1]
        $InputPaths[$Pin[0]] = $Path
    }
    $Receipts = @(
        @('origins','de81999768caabcfcd21b4407cb553c5962d77da2cc7534c94a3f26d4c99fae1','6ac070258c4b1302246a108ea5e681907b8e656726566974187acc5c1565bcbc'),
        @('connections','d52719be0227996218629fda881d307f196709c47b5502cca34d9a780c3bbe75','c9260eae67bb60176c2ddab19a08d5007dc515a2f1c2bc356d240c4781f3e052'),
        @('copy-and-dispatch','dec3d1d3216d75898d727ae40e5f1ef193dc5394bb3c96b4adbaff1df506aa49','4962d95dba0fc9fcaebac61e1ea3d4d44294c0d731ac61647021fb3c2a69a76e')
    )
    foreach ($Receipt in $Receipts) {
        foreach ($Index in 0..1) {
            $Path = Join-Path (Join-Path $EvidenceRoot $Receipt[0]) ('Assembly-CSharp-' + $Pins[$Index][0] + '.il.txt')
            $null = Assert-Hash $Path $Receipt[$Index + 1]
        }
    }
    Add-Type -Path $CecilPath
    $ExpectedSites = @(
        @('0x06001d15','IL_02d9: callvirt System.Void BasePlayer::NoteFiredProjectile'),
        @('0x06001793','IL_0269: callvirt System.Void BasePlayer::NoteFiredProjectile'),
        @('0x06001767','IL_023d: stfld BaseEntity BaseEntity::creatorEntity'),
        @('0x06001767','IL_0282: callvirt System.Void BaseNetworkable::Spawn()'),
        @('0x06005b89','IL_004a: callvirt System.Void ServerProjectile/IProjectileImpact::ProjectileImpact'),
        @('0x06003777','IL_0017: call System.Void FacepunchBehaviour::Invoke'),
        @('0x06003779','IL_0013: callvirt System.Void TimedExplosive::Explode()'),
        @('0x0600130a','IL_0125: call System.Void AutoTurret::ApplyDamage'),
        @('0x0600130a','IL_01d5: call System.Void AutoTurret::ApplyDamage'),
        @('0x060014c8','IL_0069: callvirt System.Void BaseCombatEntity::Hurt(HitInfo)'),
        @('0x06001494','IL_0413: callvirt System.Void BaseCombatEntity::Die(HitInfo)'),
        @('0x06001c61','IL_00fa: stfld BasePlayer BasePlayer/FiredProjectile::attacker'),
        @('0x060064d7','IL_0001: call System.Void HitInfo::Clear()'),
        @('0x060064d9','IL_0004: call System.Void Facepunch.Pool::Free<HitInfo>'),
        @('0x06001a3a','IL_180f: callvirt System.Void System.IDisposable::Dispose()'),
        @('0x06002ffa','IL_0158: callvirt System.Void BaseCombatEntity::Hurt'),
        @('0x06001bae','IL_0052: callvirt System.Void BaseCombatEntity::Die(HitInfo)'),
        @('0x06001bae','IL_008f: callvirt System.Void BaseCombatEntity::Die(HitInfo)')
    )
    $Outputs = [Collections.Generic.List[object]]::new()
    foreach ($Pin in $Pins) {
        $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($InputPaths[$Pin[0]])
        try {
            $Rows = [Collections.Generic.List[object]]::new()
            $Fields = [Collections.Generic.List[object]]::new()
            $Writers = [Collections.Generic.List[object]]::new()
            $Pending = [Collections.Generic.Queue[object]]::new()
            # Materialize Cecil's complete type inventory once, including nested types.
            foreach ($Type in @($Assembly.MainModule.GetTypes())) { $Pending.Enqueue($Type) }
            $TypeCount = 0; $MethodCount = 0; $CheckedSites = 0
            while ($Pending.Count -gt 0) {
                $Type = $Pending.Dequeue(); $TypeCount++
                if ($Type.FullName -in @('HitInfo','BasePlayer/FiredProjectile')) {
                    foreach ($Field in $Type.Fields) { $Fields.Add([ordered]@{ Name=$Field.FullName; Token=(Get-Token $Field) }) }
                }
                foreach ($Method in $Type.Methods) {
                    $MethodCount++
                    if (!$Method.HasBody) { continue }
                    $Token = Get-Token $Method
                    foreach ($Site in $ExpectedSites) {
                        if ($Token -cne $Site[0]) { continue }
                        $Matches = @($Method.Body.Instructions | Where-Object {
                            $_.ToString().StartsWith($Site[1], [StringComparison]::Ordinal)
                        })
                        if ($Matches.Count -ne 1) { throw '[Gameplay:AttributionOffline] Exact callsite assertion failed.' }
                        $CheckedSites++
                    }
                    foreach ($Instruction in $Method.Body.Instructions) {
                        if ($Instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
                            $Instruction.Operand.FullName -in @('BaseEntity HitInfo::Initiator','Network.Connection HitInfo::Predicted')) {
                            $Writers.Add([ordered]@{ Method=$Method.FullName; Token=(Get-Token $Method); Instruction=$Instruction.ToString() })
                        }
                    }
                    $Name = $Type.FullName + '::' + $Method.Name
                    if ($Name -notmatch $MethodPattern) { continue }
                    $Lines = @($Method.Body.Instructions | ForEach-Object { $_.ToString() })
                    $Edges = @($Method.Body.Instructions | Where-Object {
                        $_.OpCode.FlowControl -eq [Mono.Cecil.Cil.FlowControl]::Call -or
                        ($_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.FullName -match 'HitInfo::|FiredProjectile::|creatorEntity|creatorPlayerID|lastAttacker|lastDamage|firedProjectiles|RPCMessage::')
                    } | ForEach-Object { $_.ToString() })
                    $Row = [ordered]@{ Method=$Method.FullName; Token=(Get-Token $Method); CodeSize=$Method.Body.CodeSize; Instructions=$Lines.Count; EH=$Method.Body.ExceptionHandlers.Count; IlTextSha256=(Get-TextHash ($Lines -join "`n")) }
                    if ($IncludeEdges) { $Row.Edges = $Edges }
                    if ($IncludeInstructions) { $Row.Body = $Lines }
                    $Rows.Add($Row)
                }
            }
            $InitiatorCount = @($Writers | Where-Object { $_.Instruction -match 'HitInfo::Initiator$' }).Count
            $PredictedCount = @($Writers | Where-Object { $_.Instruction -match 'HitInfo::Predicted$' }).Count
            $ExpectedTypes = if ($Pin[0] -eq 'win') { 7666 } else { 7668 }
            if ($CheckedSites -ne $ExpectedSites.Count) { throw '[Gameplay:AttributionOffline] Incomplete callsite inventory.' }
            if ($TypeCount -ne $ExpectedTypes -or $MethodCount -ne 45570 -or $InitiatorCount -ne 19 -or $PredictedCount -ne 4) {
                throw "[Gameplay:AttributionOffline] Backing inventory disagrees: Types=$TypeCount Methods=$MethodCount Initiator=$InitiatorCount Predicted=$PredictedCount."
            }
            if (@($Fields | Where-Object { $_.Name -match 'Network.Connection BasePlayer/FiredProjectile::' }).Count -ne 0) {
                throw '[Gameplay:AttributionOffline] Unexpected stored projectile connection.'
            }
            $Output = [ordered]@{ Platform=$Pin[0]; AssemblySha256=$Pin[1]; Mvid=$Assembly.MainModule.Mvid.ToString(); Types=$TypeCount; Methods=$MethodCount; InitiatorWriters=$InitiatorCount; PredictedWriters=$PredictedCount; CheckedSites=$CheckedSites; SelectedMethods=$Rows.ToArray() }
            if ($IncludeEdges) { $Output.Fields=$Fields.ToArray(); $Output.Writers=$Writers.ToArray() }
            $Outputs.Add($Output)
        } finally { $Assembly.Dispose() }
    }
    $WindowsRows = $Outputs[0].SelectedMethods
    $LinuxRows = $Outputs[1].SelectedMethods
    if ($WindowsRows.Count -eq 0 -or $WindowsRows.Count -ne $LinuxRows.Count) { throw '[Gameplay:AttributionOffline] Empty or unequal method selection.' }
    for ($Index=0; $Index -lt $WindowsRows.Count; $Index++) {
        foreach ($Key in @('Method','Token','CodeSize','Instructions','EH','IlTextSha256')) {
            if ($WindowsRows[$Index][$Key] -cne $LinuxRows[$Index][$Key]) { throw '[Gameplay:AttributionOffline] Selected method parity failed.' }
        }
    }
    if ($VerifyReport) {
        $ReportPath = Join-Path $PSScriptRoot '../docs/design/GameplayB2-AttributionExpansion.md'
        $Report = Get-Content -LiteralPath $ReportPath -Raw
        foreach ($Row in $WindowsRows) {
            $LedgerRow = '| `' + $Row.Method + '` | `' + $Row.Token + '` | ' + $Row.CodeSize + '/' + $Row.Instructions + '/' + $Row.EH + ' | `' + $Row.IlTextSha256 + '` |'
            if (!$Report.Contains($LedgerRow)) { throw '[Gameplay:AttributionOffline] Exact report metadata row disagrees.' }
        }
    }
    [ordered]@{ Status='STATIC ONLY'; CecilSha256=$CecilHash; VerifiedReceiptFiles=6; ParityMethods=$WindowsRows.Count; ReportVerified=[bool]$VerifyReport; Platforms=$Outputs.ToArray() } | ConvertTo-Json -Depth 10
} catch {
    # Console-only failure, no dialogs or game-focus changes.
    Write-Error ('[Gameplay:AttributionOffline] ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
