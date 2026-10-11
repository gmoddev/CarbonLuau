# C2 bounded static sidecar. Never executes Rust/Carbon, patches, builds or servers.
# Selected-body publicizer reproduction is NOT a loaded Harmony topology receipt.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$WindowsAssembly,
    [Parameter(Mandatory=$true)][string]$LinuxAssembly,
    [Parameter(Mandatory=$true)][string]$WindowsCommon,
    [Parameter(Mandatory=$true)][string]$LinuxCommon,
    [Parameter(Mandatory=$true)][string]$WindowsStartup,
    [Parameter(Mandatory=$true)][string]$LinuxStartup,
    [Parameter(Mandatory=$true)][string]$CecilPath,
    [string[]]$WindowsHooks = @(),
    [string[]]$LinuxHooks = @(),
    [string[]]$DependencyDirectory = @(),
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
function Require([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw ('[Research:InventoryNet] ' + $Message) }
}
function GetHash([byte[]]$Bytes) {
    $Hash = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($Hash.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() }
    finally { $Hash.Dispose() }
}
function GetTypes($Module) {
    $Pending = [Collections.Generic.Queue[object]]::new()
    foreach ($Type in $Module.Types) { $Pending.Enqueue($Type) }
    $Count = 0
    while ($Pending.Count) {
        Require (++$Count -le 8192) 'Cold type inventory exceeds bound.'
        $Type = $Pending.Dequeue(); $Type
        foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
    }
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
        } else { throw '[Research:InventoryNet] Unsupported CLI body header.' }
        Require ($BodySize -eq $Method.Body.CodeSize -and $BodyStart + $BodySize -le $Bytes.Length) 'Invalid body extent.'
        $Raw = [byte[]]::new($BodySize)
        [Array]::Copy($Bytes,$BodyStart,$Raw,0,$BodySize)
        return ,$Raw
    }
    throw '[Research:InventoryNet] Method outside PE sections.'
}
function GetEH($Method) {
    foreach ($Handler in $Method.Body.ExceptionHandlers) {
        $TryEnd = if ($Handler.TryEnd) { $Handler.TryEnd.Offset } else { $Method.Body.CodeSize }
        $End = if ($Handler.HandlerEnd) { $Handler.HandlerEnd.Offset } else { $Method.Body.CodeSize }
        [ordered]@{ Kind=$Handler.HandlerType.ToString(); TryOffset=$Handler.TryStart.Offset
            TryLength=$TryEnd-$Handler.TryStart.Offset; HandlerOffset=$Handler.HandlerStart.Offset
            HandlerLength=$End-$Handler.HandlerStart.Offset; CatchType=$(if ($Handler.CatchType) {$Handler.CatchType.FullName} else {$null})
            FilterOffset=$(if ($Handler.FilterStart) {$Handler.FilterStart.Offset} else {-1}) }
    }
}
function GetShape($Method) {
    [ordered]@{ Text=(@($Method.Body.Instructions | ForEach-Object ToString) -join "`n")
        ExpectedEH=@(GetEH $Method); Locals=@($Method.Body.Variables | ForEach-Object {$_.VariableType.FullName})
        CodeSize=$Method.Body.CodeSize; MaxStack=$Method.Body.MaxStackSize; InitLocals=$Method.Body.InitLocals
        Virtual=$Method.IsVirtual; NewSlot=$Method.IsNewSlot; Static=$Method.IsStatic }
}
$Pins = @{
    Windows=@{ Assembly='bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2'; Common='0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a'; Startup='b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28' }
    Linux=@{ Assembly='cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450'; Common='5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb'; Startup='7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0' }
}
$Cases = @(
    'Item|IsValid','Item|IsRemoved','Item|GetWorldEntity','Item|MoveToContainer','Item|SetParent',
    'Item|RemoveFromContainer','Item|SplitItem','Item|UseItem','Item|Remove','Item|Drop','Item|MarkDirty',
    'ItemContainer|Insert','ItemContainer|Remove','ItemContainer|Take','ItemContainer|TryTakeOne','ItemContainer|MarkDirty',
    'PlayerInventory|MoveItem','PlayerInventory|Take','PlayerInventory|ServerUpdate',
    'HeldEntity|UseItemAmount','StringLights|UseItemAmount','WorldItem|Pickup','ItemCrafter|FinishCrafting','ItemManager|DoRemoves'
)
$Methods = [Collections.Generic.List[object]]::new()
$Catalogs = [Collections.Generic.List[object]]::new()
$Assemblies = [Collections.Generic.List[object]]::new()
$Parity = @{}
foreach ($InputRecord in @(
    @{Platform='Windows'; Assembly=$WindowsAssembly; Common=$WindowsCommon; Startup=$WindowsStartup; Hooks=$WindowsHooks},
    @{Platform='Linux'; Assembly=$LinuxAssembly; Common=$LinuxCommon; Startup=$LinuxStartup; Hooks=$LinuxHooks})) {
    $Platform = $InputRecord.Platform
    foreach ($Key in @('Assembly','Common','Startup')) {
        $InputRecord[$Key] = (Resolve-Path -LiteralPath $InputRecord[$Key]).Path
        Require ((Get-FileHash -LiteralPath $InputRecord[$Key]).Hash.ToLowerInvariant() -eq $Pins[$Platform][$Key]) "$Platform/$Key drift."
    }
    $Resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    foreach ($Key in @('Assembly','Common','Startup')) { $Resolver.AddSearchDirectory([IO.Path]::GetDirectoryName($InputRecord[$Key])) }
    foreach ($Directory in $DependencyDirectory) { $Resolver.AddSearchDirectory((Resolve-Path -LiteralPath $Directory).Path) }
    $Reader = [Mono.Cecil.ReaderParameters]::new(); $Reader.AssemblyResolver=$Resolver
    $Original = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($InputRecord.Assembly,$Reader)
    $Common = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($InputRecord.Common,$Reader)
    $Startup = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($InputRecord.Startup,$Reader)
    $Buffer = [IO.MemoryStream]::new(); $Copy=$null
    try {
        $Owner = @($Startup.MainModule.GetType('Carbon.Publicizer.AssemblyCSharp').Methods | Where-Object Name -eq 'InjectIPlayer')
        Require ($Owner.Count -eq 1) 'Publicizer owner missing.'
        Require ((GetHash ([Text.Encoding]::UTF8.GetBytes((GetShape $Owner[0]).Text))) -eq '7203f6d8341f7b13d338d640edb811c03d071b45cc82cbda52351d1a3dc478fe') 'Publicizer owner body drift.'
        $OriginalBytes=[IO.File]::ReadAllBytes($InputRecord.Assembly)
        $Before=@{}
        foreach ($Case in $Cases) {
            $Parts=$Case.Split('|'); $Match=@($Original.MainModule.GetType($Parts[0]).Methods | Where-Object Name -eq $Parts[1])
            Require ($Match.Count -eq 1 -and $Match[0].HasBody) "Ambiguous/missing $Case."
            $Before[$Case]=@{Method=$Match[0]; Shape=GetShape $Match[0]; Raw=GetHash (GetRaw $Match[0] $OriginalBytes)}
        }
        $Player=$Original.MainModule.GetType('BasePlayer')
        Require (@($Player.Fields | Where-Object Name -eq 'IPlayer').Count -eq 0) 'Input already publicized.'
        $IPlayer=$Common.MainModule.GetType('Oxide.Core.Libraries.Covalence','IPlayer')
        Require ($null -ne $IPlayer -and $IPlayer.IsInterface) 'Common IPlayer missing.'
        $Player.Fields.Add([Mono.Cecil.FieldDefinition]::new('IPlayer',[Mono.Cecil.FieldAttributes]134,$Original.MainModule.ImportReference($IPlayer)))
        $Original.Write($Buffer); $ProcessedBytes=$Buffer.ToArray(); $Buffer.Position=0
        $Copy=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Buffer,$Reader)
        $Assemblies.Add([ordered]@{Platform=$Platform; Path=$InputRecord.Assembly; SHA256=$Pins[$Platform].Assembly; MVID=$Original.MainModule.Mvid.ToString()})
        foreach ($Case in $Cases) {
            $Parts=$Case.Split('|'); $Method=@($Copy.MainModule.GetType($Parts[0]).Methods | Where-Object Name -eq $Parts[1])[0]
            $Shape=GetShape $Method
            $ShapeJson=ConvertTo-Json -InputObject $Shape -Depth 20 -Compress
            Require ($ShapeJson -ceq (ConvertTo-Json -InputObject $Before[$Case].Shape -Depth 20 -Compress)) "Reproduction semantic/shape drift: $Case."
            if ($Platform -eq 'Windows') {$Parity[$Case]=$ShapeJson} else {Require ($Parity[$Case] -ceq $ShapeJson) "Windows/Linux drift: $Case."}
            $Methods.Add([ordered]@{Platform=$Platform; Identity=$Method.FullName; Token=[int]$Method.MetadataToken.ToUInt32()
                TokenHex=('0x{0:x8}' -f $Method.MetadataToken.ToUInt32()); CodeSize=$Shape.CodeSize; OriginalRawSHA256=$Before[$Case].Raw
                ReproducedRawSHA256=(GetHash (GetRaw $Method $ProcessedBytes)); ILTextSHA256=(GetHash ([Text.Encoding]::UTF8.GetBytes($Shape.Text)))
                ExpectedEH=$Shape.ExpectedEH; Locals=$Shape.Locals; Virtual=$Shape.Virtual; NewSlot=$Shape.NewSlot; Static=$Shape.Static
                Calls=@($Method.Body.Instructions | Where-Object {$_.OpCode.Name -in @('call','callvirt','newobj')} | ForEach-Object {[ordered]@{Offset=$_.Offset; Target=[string]$_.Operand}})
                AmountStores=@($Method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'stfld' -and [string]$_.Operand -eq 'System.Int32 Item::amount'} | ForEach-Object Offset)
                LoadedBodyMatch=$null; HarmonyTopologyMeasured=$false})
        }
    } finally {if ($Copy) {$Copy.Dispose()}; $Buffer.Dispose(); $Original.Dispose(); $Common.Dispose(); $Startup.Dispose(); $Resolver.Dispose()}
    foreach ($Path in $InputRecord.Hooks) {
        $Hook=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $Path).Path)
        try {
            $Targets=[Collections.Generic.List[object]]::new()
            foreach ($Type in (GetTypes $Hook.MainModule)) {
                foreach ($Attr in $Type.CustomAttributes) {
                    if ($Attr.AttributeType.FullName -ne 'API.Hooks.HookAttribute/Patch' -or $Attr.ConstructorArguments.Count -lt 4) {continue}
                    $Name=[string]$Attr.ConstructorArguments[0].Value; $Target=[string]$Attr.ConstructorArguments[2].Value; $MethodName=[string]$Attr.ConstructorArguments[3].Value
                    if ($Name -notmatch '^OnItem(AddedToContainer|RemovedFromContainer|Stacked|Use|Split|Remove|Pickup|Dropped|CraftFinished)$' -and $MethodName -notin @('IsValid','IsRemoved','GetWorldEntity')) {continue}
                    $Bodies=@($Type.Methods)+@($Type.NestedTypes | ForEach-Object {$_.Methods})
                    $Targets.Add([ordered]@{Name=$Name; Class=$Type.FullName; TargetType=$Target; TargetMethod=$MethodName
                        Bodies=@($Bodies | Where-Object {$_.HasBody -and $_.Name -in @('Transpiler','MoveNext','Prefix','Postfix','Finalizer')} | ForEach-Object {[ordered]@{Identity=$_.FullName; TokenHex=('0x{0:x8}' -f $_.MetadataToken.ToUInt32()); ILTextSHA256=(GetHash ([Text.Encoding]::UTF8.GetBytes((GetShape $_).Text)))}})})
                }
            }
            $Catalogs.Add([ordered]@{Platform=$Platform; Path=$Path; SHA256=(Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant(); MVID=$Hook.MainModule.Mvid.ToString(); Targets=$Targets.ToArray()})
        } finally {$Hook.Dispose()}
    }
}
$Result=[ordered]@{EvidenceKind='Bounded exact-binary STATIC research; no live loaded/Harmony qualification'
    Transform='Pinned InjectIPlayer field import and Cecil memory serialization, selected-body reproduction only'
    Caveat='Hook catalog presence is not activation/order/body proof. Missing catalogs and trusted plugin field writes remain outside this ledger.'
    DependencyDirectories=$DependencyDirectory
    SelectedMethodsPerPlatform=$Cases.Count; WindowsLinuxShapeParity=$true; Assemblies=$Assemblies.ToArray(); Methods=$Methods.ToArray(); HookCatalogs=$Catalogs.ToArray()}
$Json=ConvertTo-Json -InputObject $Result -Depth 30
if ($OutputPath) {
    Require ([IO.Path]::IsPathRooted($OutputPath)) 'Artifact path must be absolute.'
    $OutputPath=[IO.Path]::GetFullPath($OutputPath)
    $Repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\','/')
    if (Test-Path -LiteralPath (Join-Path $Repository 'AICONTEXT.md')) {
        Require (!$OutputPath.StartsWith($Repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Artifact must stay outside checkout.'
    }
    Require (!(Test-Path -LiteralPath $OutputPath)) 'Artifact already exists; never overwrite.'
    [IO.File]::WriteAllText($OutputPath,$Json,[Text.UTF8Encoding]::new($false))
    Write-Output "[Research:InventoryNet] Static parity PASS: $($Cases.Count) methods/platform; artifact=$OutputPath SHA256=$((Get-FileHash -LiteralPath $OutputPath).Hash.ToLowerInvariant()). Loaded verification NOT MEASURED."
} else {$Json}
