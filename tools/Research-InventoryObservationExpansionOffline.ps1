# Cold Cecil inspection only: never loads or invokes Rust/Carbon code.
# This is a scoped callgraph ledger, not a runtime inventory scanner or completion certificate.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$WindowsAssembly,
    [Parameter(Mandatory=$true)][string]$LinuxAssembly,
    [Parameter(Mandatory=$true)][string]$CecilPath,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Require([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw ('[Research:InventoryExpansion] ' + $Message) }
}
function GetHash([byte[]]$Bytes) {
    $Hash = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($Hash.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() }
    finally { $Hash.Dispose() }
}
function GetRaw($Method, [byte[]]$Bytes) {
    $Pe = [BitConverter]::ToInt32($Bytes, 0x3c)
    $Count = [BitConverter]::ToUInt16($Bytes, $Pe + 6)
    $Start = $Pe + 24 + [BitConverter]::ToUInt16($Bytes, $Pe + 20)
    for ($Index = 0; $Index -lt $Count; $Index++) {
        $Section = $Start + 40 * $Index
        $Rva = [BitConverter]::ToUInt32($Bytes, $Section + 12)
        $Size = [Math]::Max([BitConverter]::ToUInt32($Bytes,$Section+8), [BitConverter]::ToUInt32($Bytes,$Section+16))
        if ($Method.RVA -lt $Rva -or $Method.RVA -ge $Rva + $Size) { continue }
        $Offset = [int]([BitConverter]::ToUInt32($Bytes,$Section+20) + $Method.RVA - $Rva)
        if (($Bytes[$Offset] -band 3) -eq 2) { $BodyStart = $Offset + 1; $BodySize = $Bytes[$Offset] -shr 2 }
        elseif (($Bytes[$Offset] -band 3) -eq 3) {
            $BodyStart = $Offset + (([BitConverter]::ToUInt16($Bytes,$Offset) -shr 12) * 4)
            $BodySize = [BitConverter]::ToInt32($Bytes,$Offset+4)
        } else { throw '[Research:InventoryExpansion] Unsupported CLI body header.' }
        Require ($BodySize -eq $Method.Body.CodeSize -and $BodySize -ge 0 -and $BodyStart + $BodySize -le $Bytes.Length) 'Invalid body extent.'
        $Raw = [byte[]]::new($BodySize)
        [Array]::Copy($Bytes,$BodyStart,$Raw,0,$BodySize)
        return ,$Raw
    }
    throw '[Research:InventoryExpansion] Method outside PE sections.'
}
# C2 support bodies are included only to connect the new enclosing/notification paths.
# No global writer census, hook catalog scan, publicizer rewrite or assembly Resolve.
$Selection = [ordered]@{
    PlayerInventory = @('Initialize','ServerInit','OnContentsDirty','OnItemAddedOrRemoved','OnItemRemoved',
        'ItemCmd','MoveItem','SwapHotbar','SwapHotbarWithMainRow','DeferredServerUpdate','ServerUpdate',
        'UpdateContainer','SendUpdatedInventory','GiveItem','Take','UseAmount')
    BasePlayer = @('GiveItem')
    WorldItem = @('Pickup','RemoveItem','OnItemDirty')
    ItemCrafter = @('CollectIngredient','CollectIngredients','CraftItem','FinishCrafting','CancelTask','ServerUpdate')
    ItemModConsume = @('DoAction')
    Item = @('MarkDirty','MoveToContainer','SplitItem','Drop','UseItem','ServerCommand')
    ItemContainer = @('MarkDirty','Insert','Remove','Take','TryTakeOne','UseAmount')
}
$Pins = @{
    Windows = 'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2'
    Linux = 'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450'
}
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$Records = [Collections.Generic.List[object]]::new()
$Inputs = [Collections.Generic.List[object]]::new()
$Parity = @{}
foreach ($InputRecord in @(@{Platform='Windows';Path=$WindowsAssembly}, @{Platform='Linux';Path=$LinuxAssembly})) {
    $Platform = $InputRecord.Platform
    $Path = (Resolve-Path -LiteralPath $InputRecord.Path).Path
    Require ((Get-Item -LiteralPath $Path).Length -le 64MB) 'Input assembly exceeds cold inspection bound.'
    $Bytes = [IO.File]::ReadAllBytes($Path)
    Require ((GetHash $Bytes) -eq $Pins[$Platform]) "$Platform assembly drift."
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        $Inputs.Add([ordered]@{Platform=$Platform;Path=$Path;SHA256=$Pins[$Platform];MVID=$Assembly.MainModule.Mvid.ToString()})
        foreach ($TypeName in $Selection.Keys) {
            $Type = $Assembly.MainModule.GetType($TypeName)
            Require ($null -ne $Type) "Missing $TypeName."
            foreach ($MethodName in $Selection[$TypeName]) {
                $Matches = @($Type.Methods | Where-Object Name -eq $MethodName)
                Require ($Matches.Count -gt 0 -and $Matches.Count -le 4) "Missing/oversized overload set $TypeName/$MethodName."
                foreach ($Method in $Matches) {
                    Require ($Method.HasBody -and $Method.Body.Instructions.Count -le 4096) 'Missing/oversized selected body.'
                    $Text = (@($Method.Body.Instructions | ForEach-Object ToString) -join "`n")
                    $EH = @($Method.Body.ExceptionHandlers | ForEach-Object {
                        [ordered]@{Kind=$_.HandlerType.ToString();TryStart=$_.TryStart.Offset
                            TryEnd=$(if ($_.TryEnd) {$_.TryEnd.Offset} else {$Method.Body.CodeSize})
                            HandlerStart=$_.HandlerStart.Offset
                            HandlerEnd=$(if ($_.HandlerEnd) {$_.HandlerEnd.Offset} else {$Method.Body.CodeSize})
                            FilterStart=$(if ($_.FilterStart) {$_.FilterStart.Offset} else {-1})
                            CatchType=$(if ($_.CatchType) {$_.CatchType.FullName} else {$null})}
                    })
                    $Shape = [ordered]@{IL=$Text;EH=$EH;Locals=@($Method.Body.Variables | ForEach-Object {$_.VariableType.FullName})
                        CodeSize=$Method.Body.CodeSize;MaxStack=$Method.Body.MaxStackSize;InitLocals=$Method.Body.InitLocals
                        Static=$Method.IsStatic;Virtual=$Method.IsVirtual;NewSlot=$Method.IsNewSlot}
                    $ShapeJson = ConvertTo-Json -InputObject $Shape -Depth 12 -Compress
                    $Key = $Method.FullName
                    if ($Platform -eq 'Windows') {$Parity[$Key]=$ShapeJson}
                    else {Require ($Parity.ContainsKey($Key) -and $Parity[$Key] -ceq $ShapeJson) "Selected shape drift: $Key."}
                    $Records.Add([ordered]@{Platform=$Platform;Identity=$Key;TokenHex=('0x{0:x8}' -f $Method.MetadataToken.ToUInt32())
                        RawILSHA256=(GetHash (GetRaw $Method $Bytes));ILTextSHA256=(GetHash ([Text.Encoding]::UTF8.GetBytes($Text)))
                        Shape=$Shape
                        Calls=@($Method.Body.Instructions | Where-Object {$_.OpCode.Name -in @('call','callvirt','newobj','ldftn','ldvirtftn')} |
                            ForEach-Object {[ordered]@{Offset=$_.Offset;Opcode=$_.OpCode.Name;Target=[string]$_.Operand}})
                        AmountStores=@($Method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'stfld' -and [string]$_.Operand -eq 'System.Int32 Item::amount'} | ForEach-Object Offset)
                        Returns=@($Method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ret'} | ForEach-Object Offset)
                        LoadedBodyMatch=$null;HarmonyTopologyMeasured=$false})
                }
            }
        }
    } finally { $Assembly.Dispose() }
}
Require ($Records.Count -eq 2 * $Parity.Count) 'Platform selection count mismatch.'
$Result = [ordered]@{EvidenceKind='STATIC original PE only; no Rust/Carbon execution, completion or loaded topology proof'
    CheckerSHA256=$(if ($PSCommandPath) {(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant()} else {$null})
    CecilSHA256=(Get-FileHash -LiteralPath $CecilPath).Hash.ToLowerInvariant()
    SelectedMethodsPerPlatform=$Parity.Count;SelectedShapeParity=$true;Inputs=$Inputs.ToArray();Methods=$Records.ToArray()}
$Json = ConvertTo-Json -InputObject $Result -Depth 20
if ($OutputPath) {
    Require ([IO.Path]::IsPathRooted($OutputPath)) 'Evidence path must be absolute.'
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
    $RepositoryCandidates = @((Get-Location).Path)
    if ($PSScriptRoot) { $RepositoryCandidates += (Join-Path $PSScriptRoot '..') }
    foreach ($Candidate in $RepositoryCandidates) {
        $Repository = [IO.Path]::GetFullPath($Candidate).TrimEnd('\','/')
        if (Test-Path -LiteralPath (Join-Path $Repository 'AICONTEXT.md')) {
            Require (!$OutputPath.StartsWith($Repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Evidence must stay outside checkout.'
        }
    }
    # CreateNew prevents overwriting historical receipts, including races.
    $Stream = [IO.File]::Open($OutputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $Encoded = [Text.UTF8Encoding]::new($false).GetBytes($Json); $Stream.Write($Encoded,0,$Encoded.Length) }
    finally { $Stream.Dispose() }
    Write-Output "[Research:InventoryExpansion] STATIC PASS: $($Parity.Count) methods/platform; SHA256=$((Get-FileHash -LiteralPath $OutputPath).Hash.ToLowerInvariant()); loaded/live NOT MEASURED."
} else { $Json }
