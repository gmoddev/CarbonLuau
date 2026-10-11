# Metadata-only exact-host research. Never loads or executes Rust/Unity code.
# Raw vendor bodies must remain outside the checkout, under the user's Temp.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$TypePattern = '^(HitInfo|Rust\.DamageType(List)?|GameObjectRef)$',
    [string]$MethodPattern = '.',
    [string]$ReferencePattern = 'HitInfo::(Initiator|Weapon|WeaponPrefab|damageTypes|\.ctor|Init|LoadFromAttack)|::(ToPlayer|SetCreatorEntity|GetCreatorEntity)\(|BaseCombatEntity::Hurt\(',
    [switch]$InventoryOnly
)
$ErrorActionPreference = 'Stop'
$OutputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$TempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (!$OutputRoot.StartsWith($TempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw '[Gameplay:B2Research] Raw evidence output must be under the current user Temp.'
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$CecilFile = (Resolve-Path -LiteralPath $CecilPath).Path
Add-Type -Path $CecilFile
function Get-TextHash([string]$Value) {
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($Hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)))).Replace('-', '').ToLowerInvariant() }
    finally { $Hasher.Dispose() }
}
function Get-Token($Member) { return '0x{0:x8}' -f $Member.MetadataToken.ToUInt32() }
foreach ($Path in $AssemblyPath) {
    $Resolved = (Resolve-Path -LiteralPath $Path).Path
    $Stem = [IO.Path]::GetFileNameWithoutExtension($Resolved)
    $ReceiptPath = Join-Path $OutputRoot ($Stem + '.il.txt')
    $SummaryPath = Join-Path $OutputRoot ($Stem + '.methods.tsv')
    if ((Test-Path -LiteralPath $ReceiptPath) -or (Test-Path -LiteralPath $SummaryPath)) {
        throw '[Gameplay:B2Research] Use a fresh output directory; preserve previous receipts.'
    }
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Resolved)
    $Writer = [IO.StreamWriter]::new($ReceiptPath, $false, [Text.UTF8Encoding]::new($false))
    $Summary = [IO.StreamWriter]::new($SummaryPath, $false, [Text.UTF8Encoding]::new($false))
    try {
        $Writer.WriteLine("ASSEMBLY=$Resolved SHA256=$((Get-FileHash -LiteralPath $Resolved).Hash.ToLowerInvariant()) MVID=$($Assembly.MainModule.Mvid)")
        $Writer.WriteLine("CECIL=$CecilFile SHA256=$((Get-FileHash -LiteralPath $CecilFile).Hash.ToLowerInvariant()) VERSION=$([Mono.Cecil.AssemblyDefinition].Assembly.GetName().Version)")
        $Writer.WriteLine("TYPE_PATTERN=$TypePattern METHOD_PATTERN=$MethodPattern REFERENCE_PATTERN=$ReferencePattern INVENTORY_ONLY=$InventoryOnly")
        $Summary.WriteLine("Method`tToken`tCodeSize`tInstructions`tEH`tIlTextSha256")
        $Pending = [Collections.Generic.Queue[object]]::new()
        foreach ($Type in $Assembly.MainModule.Types) { $Pending.Enqueue($Type) }
        $TypeCount = 0; $MethodCount = 0; $ReferenceCount = 0; $BodyCount = 0
        while ($Pending.Count -gt 0) {
            $Type = $Pending.Dequeue(); $TypeCount++
            foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
            $SelectedType = $Type.FullName -match $TypePattern
            if ($SelectedType) {
                $Writer.WriteLine("TYPE=$($Type.FullName) TOKEN=$(Get-Token $Type) BASE=$($Type.BaseType)")
                foreach ($Field in $Type.Fields) {
                    $Constant = if ($Field.HasConstant) { $Field.Constant } else { '' }
                    $Writer.WriteLine("FIELD=$($Field.FullName) TOKEN=$(Get-Token $Field) ATTRIBUTES=$($Field.Attributes) CONSTANT=$Constant")
                }
                foreach ($Property in $Type.Properties) {
                    $Writer.WriteLine("PROPERTY=$($Property.FullName) TOKEN=$(Get-Token $Property)")
                }
            }
            foreach ($Method in $Type.Methods) {
                $MethodCount++
                if (!$Method.HasBody) { continue }
                $References = [Collections.Generic.List[int]]::new()
                for ($Index = 0; $Index -lt $Method.Body.Instructions.Count; $Index++) {
                    $Instruction = $Method.Body.Instructions[$Index]
                    if ($Instruction.Operand -is [Mono.Cecil.MemberReference] -and $Instruction.Operand.FullName -match $ReferencePattern) {
                        $References.Add($Index); $ReferenceCount++
                    }
                }
                $SelectedBody = !$InventoryOnly -and (($SelectedType -and $Method.Name -match $MethodPattern) -or $Method.Name -eq 'ToPlayer')
                if (!$SelectedBody -and $References.Count -eq 0) { continue }
                $Lines = @($Method.Body.Instructions | ForEach-Object { $_.ToString() })
                $Hash = Get-TextHash ($Lines -join "`n")
                $Summary.WriteLine("$($Method.FullName)`t$(Get-Token $Method)`t$($Method.Body.CodeSize)`t$($Lines.Count)`t$($Method.Body.ExceptionHandlers.Count)`t$Hash")
                $Writer.WriteLine("METHOD=$($Method.FullName) TOKEN=$(Get-Token $Method) ATTRIBUTES=$($Method.Attributes) CODE_SIZE=$($Method.Body.CodeSize) INSTRUCTIONS=$($Lines.Count) EH=$($Method.Body.ExceptionHandlers.Count) IL_TEXT_SHA256=$Hash")
                foreach ($Handler in $Method.Body.ExceptionHandlers) {
                    $Writer.WriteLine("HANDLER=$($Handler.HandlerType) TRY=$($Handler.TryStart.Offset):$($Handler.TryEnd.Offset) HANDLER=$($Handler.HandlerStart.Offset):$($Handler.HandlerEnd.Offset)")
                }
                if ($SelectedBody) {
                    $BodyCount++
                    for ($Index = 0; $Index -lt $Lines.Count; $Index++) { $Writer.WriteLine("INDEX=$Index $($Lines[$Index])") }
                }
                foreach ($Index in $References) {
                    $Writer.WriteLine("REFERENCE METHOD=$($Method.FullName) TOKEN=$(Get-Token $Method) INDEX=$Index $($Lines[$Index])")
                    if (!$SelectedBody) {
                        for ($Near = [Math]::Max(0, $Index - 8); $Near -le [Math]::Min($Lines.Count - 1, $Index + 2); $Near++) {
                            $Writer.WriteLine("NEAR=$Near $($Lines[$Near])")
                        }
                    }
                }
            }
        }
        $Writer.WriteLine("SCAN_COMPLETE TYPES=$TypeCount METHODS=$MethodCount REFERENCES=$ReferenceCount FULL_BODIES=$BodyCount")
    }
    finally { $Summary.Dispose(); $Writer.Dispose(); $Assembly.Dispose() }
    Write-Output "[Gameplay:B2Research] Receipt=$ReceiptPath SHA256=$((Get-FileHash -LiteralPath $ReceiptPath).Hash.ToLowerInvariant()) Summary=$SummaryPath SHA256=$((Get-FileHash -LiteralPath $SummaryPath).Hash.ToLowerInvariant())"
}
