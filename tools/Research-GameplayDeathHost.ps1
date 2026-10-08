# Read-only pinned-host IL evidence. Does not launch or patch a server.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [string]$TypePattern = '^(BasePlayer|BaseCombatEntity|HitInfo)$',
    [string]$MethodPattern = '^(Die|OnDied|IsDead|IsAlive|WoundInsteadOfDying|EligibleForWounding|BecomeWounded|StopWounded|RecoverFromWounded|WoundingTick|GoToCrawling|GoToIncapacitated|get_InitiatorPlayer)$',
    [string]$HookPattern = '^(OnPlayerDeath|OnEntityDeath)$',
    [switch]$ReferenceInventory,
    [switch]$InitiatorInventory,
    [switch]$ReproducePublicizer,
    [string[]]$CommonAssemblyPath,
    [string[]]$LiveLogPath,
    [string]$DecoderSource,
    [switch]$Body
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
if ($ReproducePublicizer) {
    if ($CommonAssemblyPath.Count -ne $AssemblyPath.Count -or $LiveLogPath.Count -ne $AssemblyPath.Count -or !$DecoderSource) {
        throw 'Reproduction requires one pinned Common assembly and live receipt per Rust assembly, plus the diagnostic decoder source.'
    }
    $Source = Get-Content -Raw -LiteralPath $DecoderSource
    $Begin = $Source.IndexOf('        private static string GameplayIlType(')
    $End = $Source.IndexOf('        private void StopGameplayLifecycle()', $Begin)
    if ($Begin -lt 0 -or $End -le $Begin) { throw 'Diagnostic decoder source shape changed' }
    $Functions = $Source.Substring($Begin, $End - $Begin).Replace('private static', 'public static')
    $Header = 'using System; using System.Collections.Generic; using System.Reflection; using System.Reflection.Emit; using System.Globalization; using System.Text; public static class GameplayIlResearch {'
    $Canonical = @'
public static string Canonical(MethodInfo Method) {
    var Opcodes = new Dictionary<ushort, OpCode>();
    foreach (FieldInfo Field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
        if (Field.FieldType == typeof(OpCode)) { OpCode Code = (OpCode)Field.GetValue(null); Opcodes[unchecked((ushort)Code.Value)] = Code; }
    byte[] Bytes = Method.GetMethodBody().GetILAsByteArray();
    var Lines = new List<string>(); int Offset = 0, Characters = 0;
    if (Bytes.Length > 4096) throw new InvalidOperationException("raw bound");
    while (Offset < Bytes.Length) {
        if (Lines.Count >= 1024) throw new InvalidOperationException("instruction bound");
        int Start = Offset; ushort Value = Bytes[Offset++];
        if (Value == 0xfe) Value = (ushort)(0xfe00 | Bytes[Offset++]);
        OpCode Code = Opcodes[Value];
        string Line = Start.ToString("x4") + "|" + Code.Name + "|" + GameplayIlOperand(Method.Module, Bytes, ref Offset, Code.OperandType);
        Characters += Line.Length; if (Characters > 262144) throw new InvalidOperationException("text bound");
        Lines.Add(Line);
    }
    return String.Join("\n", Lines);
}
}
'@
    Add-Type -TypeDefinition ($Header + $Functions + $Canonical)
    function Get-ProofHash([byte[]]$Bytes) {
        $Hash = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($Hash.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
        finally { $Hash.Dispose() }
    }
    function Get-ProofEh($Method) {
        return @($Method.Body.ExceptionHandlers | ForEach-Object {
            "$($_.HandlerType)|$($_.TryStart.Offset)|$($_.TryEnd.Offset)|$($_.HandlerStart.Offset)|$($_.HandlerEnd.Offset)"
        }) -join "`n"
    }
    $CecilDirectory = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName((Resolve-Path -LiteralPath $CecilPath).Path))
    $ResearchRustDirectory = Join-Path $CecilDirectory.Parent.Parent.Parent.FullName 'RustDedicated_Data\Managed'
    $Resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    $Resolver.AddSearchDirectory($ResearchRustDirectory)
    $Resolver.AddSearchDirectory($CecilDirectory.Parent.FullName)
    $Parameters = [Mono.Cecil.ReaderParameters]::new()
    $Parameters.AssemblyResolver = $Resolver
    $ReflectionResolver = [ResolveEventHandler] {
        param($ResolverSender, $ResolverEvent)
        $Name = ([Reflection.AssemblyName]$ResolverEvent.Name).Name
        $Dependency = Join-Path $ResearchRustDirectory ($Name + '.dll')
        if (Test-Path -LiteralPath $Dependency) { return [Reflection.Assembly]::LoadFrom($Dependency) }
        return $null
    }
    [AppDomain]::CurrentDomain.add_AssemblyResolve($ReflectionResolver)
    try {
        for ($InputIndex = 0; $InputIndex -lt $AssemblyPath.Count; $InputIndex++) {
            $OriginalPath = (Resolve-Path -LiteralPath $AssemblyPath[$InputIndex]).Path
            $CommonPath = (Resolve-Path -LiteralPath $CommonAssemblyPath[$InputIndex]).Path
            $ReceiptPath = (Resolve-Path -LiteralPath $LiveLogPath[$InputIndex]).Path
            $Receipt = Get-Content -LiteralPath $ReceiptPath
            Write-Output "REPRO_SOURCE=$OriginalPath SHA256=$((Get-FileHash $OriginalPath).Hash.ToLowerInvariant()) COMMON=$CommonPath COMMON_SHA256=$((Get-FileHash $CommonPath).Hash.ToLowerInvariant()) RECEIPT_SHA256=$((Get-FileHash $ReceiptPath).Hash.ToLowerInvariant())"
            $Original = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalPath, $Parameters)
            $Common = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($CommonPath, $Parameters)
            $Buffer = [IO.MemoryStream]::new()
            $Copy = $null
            try {
                $Before = @{}; $BeforeEh = @{}
                foreach ($Type in $Original.MainModule.Types) {
                    foreach ($Method in $Type.Methods) {
                        if (!(($Type.Name -eq 'BasePlayer' -and $Method.Name -in @('Die', 'OnDied', 'RespawnAt')) -or
                            ($Type.Name -eq 'BaseCombatEntity' -and $Method.Name -eq 'Die'))) { continue }
                        $Before[$Method.FullName] = @($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
                        $BeforeEh[$Method.FullName] = Get-ProofEh $Method
                    }
                }
                # Carbon.Startup AssemblyCSharp.InjectIPlayer 0x06000085:
                # add IPlayer (Public|NotSerialized = 134), then UpdateBuffer
                # writes the module into memory. No target method is edited.
                $IPlayer = $Common.MainModule.GetType('Oxide.Core.Libraries.Covalence', 'IPlayer')
                $Original.MainModule.GetType('BasePlayer').Fields.Add([Mono.Cecil.FieldDefinition]::new(
                    'IPlayer', [Mono.Cecil.FieldAttributes]134, $Original.MainModule.ImportReference($IPlayer)))
                $Original.Write($Buffer)
                $Bytes = $Buffer.ToArray()
                $Buffer.Position = 0
                $Copy = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Buffer, $Parameters)
                # Metadata-only reflection; no Rust/Unity objects or methods run.
                $Loaded = [Reflection.Assembly]::Load($Bytes)
                foreach ($Type in $Copy.MainModule.Types) {
                    foreach ($Method in $Type.Methods) {
                        if (!$Before.ContainsKey($Method.FullName)) { continue }
                        $Text = @($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
                        $EH = Get-ProofEh $Method
                        $LiveMethod = $Loaded.ManifestModule.ResolveMethod($Method.MetadataToken.ToUInt32())
                        $Raw = $LiveMethod.GetMethodBody().GetILAsByteArray()
                        $CanonicalText = [GameplayIlResearch]::Canonical($LiveMethod)
                        $RawHash = Get-ProofHash $Raw
                        $CanonicalHash = Get-ProofHash ([Text.Encoding]::UTF8.GetBytes($CanonicalText))
                        $Identity = $Type.FullName + '.' + $Method.Name
                        $LiveHeader = @($Receipt | Where-Object { $_ -like ('* Method=' + $Identity + ' Token=*') })
                        $LiveCanonical = @($Receipt | Where-Object { $_ -like ('* Canonical ' + $Identity + ' Count=*') })
                        $RawMatch = $LiveHeader.Count -eq 1 -and $LiveHeader[0] -like ('*RawSHA256=' + $RawHash + ' *')
                        $CanonicalMatch = $LiveCanonical.Count -eq 1 -and $LiveCanonical[0].EndsWith('SHA256=' + $CanonicalHash)
                        $ExpectedEh = @($LiveMethod.GetMethodBody().ExceptionHandlingClauses | ForEach-Object {
                            " Kind=$($_.Flags) Try=$($_.TryOffset):$($_.TryLength) Handler=$($_.HandlerOffset):$($_.HandlerLength)"
                        })
                        $ActualEh = @($Receipt | Where-Object { $_ -like ('* EH ' + $Identity + ' Kind=*') })
                        $EhMatch = $ActualEh.Count -eq $ExpectedEh.Count
                        for ($Index = 0; $Index -lt $ExpectedEh.Count -and $EhMatch; $Index++) { $EhMatch = $ActualEh[$Index].EndsWith($ExpectedEh[$Index]) }
                        Write-Output "REPRO_METHOD=$Identity TOKEN=$($Method.MetadataToken) SIZE=$($Raw.Length) RAW_SHA256=$RawHash CANONICAL_SHA256=$CanonicalHash SOURCE_SEMANTIC_PARITY=$($Before[$Method.FullName] -ceq $Text) SOURCE_EH_PARITY=$($BeforeEh[$Method.FullName] -ceq $EH) LIVE_RAW_MATCH=$RawMatch LIVE_CANONICAL_MATCH=$CanonicalMatch LIVE_EH_MATCH=$EhMatch IL_TEXT_SHA256=$(Get-ProofHash ([Text.Encoding]::UTF8.GetBytes($Text)))"
                        if ($Before[$Method.FullName] -cne $Text -or $BeforeEh[$Method.FullName] -cne $EH -or !$RawMatch -or !$CanonicalMatch -or !$EhMatch) { throw 'Reproduction semantic/live receipt mismatch' }
                    }
                }
            }
            finally { if ($Copy) { $Copy.Dispose() }; $Buffer.Dispose(); $Original.Dispose(); $Common.Dispose() }
        }
    }
    finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($ReflectionResolver); $Resolver.Dispose() }
    return
}
function Format-Argument($Argument) {
    if ($Argument.Value -is [Mono.Cecil.CustomAttributeArgument[]]) {
        return '[' + (($Argument.Value | ForEach-Object { Format-Argument $_ }) -join ',') + ']'
    }
    return [string]$Argument.Value
}
function Write-Method($Method) {
    $Token = '0x{0:x8}' -f $Method.MetadataToken.ToUInt32()
    Write-Output "METHOD $($Method.FullName) TOKEN=$Token ATTRIBUTES=$($Method.Attributes)"
    if (!$Method.HasBody) { return }
    $Lines = @($Method.Body.Instructions | ForEach-Object { $_.ToString() })
    $Bytes = [Text.Encoding]::UTF8.GetBytes(($Lines -join "`n"))
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { $Hash = ([BitConverter]::ToString($Hasher.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $Hasher.Dispose() }
    Write-Output "IL_TEXT_SHA256=$Hash CODE_SIZE=$($Method.Body.CodeSize) INSTRUCTIONS=$($Lines.Count)"
    foreach ($Handler in $Method.Body.ExceptionHandlers) {
        Write-Output "HANDLER $($Handler.HandlerType) TRY=$($Handler.TryStart.Offset)-$($Handler.TryEnd.Offset) HANDLER=$($Handler.HandlerStart.Offset)-$($Handler.HandlerEnd.Offset)"
    }
    if ($Body) {
        for ($Index = 0; $Index -lt $Lines.Count; $Index++) { Write-Output "INDEX=$Index $($Lines[$Index])" }
    }
}
foreach ($Path in $AssemblyPath) {
    $Resolved = (Resolve-Path -LiteralPath $Path).Path
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Resolved)
    try {
        Write-Output "ASSEMBLY=$Resolved SHA256=$((Get-FileHash -LiteralPath $Resolved -Algorithm SHA256).Hash.ToLowerInvariant()) VERSION=$($Assembly.Name.Version) MVID=$($Assembly.MainModule.Mvid)"
        $Pending = [Collections.Generic.Queue[object]]::new()
        $Types = [Collections.Generic.List[object]]::new()
        foreach ($Type in $Assembly.MainModule.Types) { $Pending.Enqueue($Type) }
        while ($Pending.Count -gt 0) {
            $Type = $Pending.Dequeue()
            $Types.Add($Type)
            foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
        }
        foreach ($Type in $Types) {
            if ($Type.FullName -match $TypePattern) {
                foreach ($Method in $Type.Methods) {
                    if ($Method.Name -match $MethodPattern) { Write-Method $Method }
                }
            }
            $SelectedHook = $false
            foreach ($Attribute in $Type.CustomAttributes) {
                if ($Attribute.AttributeType.FullName -ne 'API.Hooks.HookAttribute/Patch') { continue }
                if ($Attribute.ConstructorArguments.Count -gt 0 -and
                    [string]$Attribute.ConstructorArguments[0].Value -match $HookPattern) { $SelectedHook = $true }
            }
            if ($SelectedHook) {
                Write-Output "HOOK_TYPE=$($Type.FullName)"
                foreach ($Attribute in $Type.CustomAttributes) {
                    $Arguments = @($Attribute.ConstructorArguments | ForEach-Object { Format-Argument $_ })
                    Write-Output "ATTRIBUTE=$($Attribute.AttributeType.FullName) ARGUMENTS=$($Arguments -join ';')"
                }
                foreach ($Method in $Type.Methods) { Write-Method $Method }
                foreach ($Nested in $Type.NestedTypes) {
                    foreach ($Method in $Nested.Methods) {
                        if ($Method.Name -eq 'MoveNext') { Write-Method $Method }
                    }
                }
            }
            if ($ReferenceInventory) {
                foreach ($Method in $Type.Methods) {
                    if (!$Method.HasBody) { continue }
                    foreach ($Instruction in $Method.Body.Instructions) {
                        if ($Instruction.Operand -is [Mono.Cecil.MethodReference] -and
                            $Instruction.Operand.FullName -match 'System.Void (BasePlayer|BaseCombatEntity)::Die\(HitInfo\)') {
                            Write-Output "DEATH_CALL=$($Method.FullName) TOKEN=0x$('{0:x8}' -f $Method.MetadataToken.ToUInt32()) $Instruction"
                        }
                    }
                }
            }
            if ($InitiatorInventory) {
                foreach ($Method in $Type.Methods) {
                    if (!$Method.HasBody) { continue }
                    if ($Method.Name -eq 'ToPlayer') { Write-Method $Method }
                    $Instructions = $Method.Body.Instructions
                    for ($Index = 0; $Index -lt $Instructions.Count; $Index++) {
                        $Instruction = $Instructions[$Index]
                        if ($Instruction.OpCode.Name -ne 'stfld' -or
                            [string]$Instruction.Operand -ne 'BaseEntity HitInfo::Initiator') { continue }
                        Write-Output "INITIATOR_SOURCE=$($Method.FullName) TOKEN=0x$('{0:x8}' -f $Method.MetadataToken.ToUInt32())"
                        for ($Near = [Math]::Max(0, $Index - 4); $Near -le $Index; $Near++) {
                            Write-Output "INDEX=$Near $($Instructions[$Near])"
                        }
                    }
                }
            }
        }
    } finally { $Assembly.Dispose() }
}
