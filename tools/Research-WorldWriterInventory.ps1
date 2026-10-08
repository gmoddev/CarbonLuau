# Offline Avenue B evidence only. Reads metadata/IL; never loads the host, patches,
# starts a server, resolves dependencies from a server, or writes an evidence file.
# A pinned finite IL inventory is NOT an exhaustive engine/plugin writer proof.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$AssemblyPath,
    [Parameter(Mandatory = $true)][string[]]$ExpectedSha256,
    [Parameter(Mandatory = $true)][string]$CecilPath
)
$ErrorActionPreference = 'Stop'
if ($AssemblyPath.Count -ne $ExpectedSha256.Count) {
    throw '[World:WriterInventory] Each input requires an explicit SHA256 pin'
}
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$Opened = [Collections.Generic.List[object]]::new()
$Inputs = [Collections.Generic.List[object]]::new()
$Definitions = @{}
$Methods = [Collections.Generic.List[object]]::new()
$Calls = [Collections.Generic.List[object]]::new()
$Unknowns = [Collections.Generic.List[object]]::new()
$Boundaries = [Collections.Generic.List[object]]::new()
$NativeBoundaries = [Collections.Generic.List[object]]::new()
$OtherUnityTargets = @{}
# Intentionally broad: getters, configuration, read-only jobs and non-root/client
# paths remain in the inventory. No name-based match is promoted to a root writer.
$FamilyPattern = '^UnityEngine\.(Transform$|Rigidbody(2D)?$|CharacterController$|Physics(Scene|2D)?$|Animator$|Animation($|State$)|AI\.(NavMeshAgent|NavMesh|NavMeshData)$|Animations\.|Jobs\.)'
$HostPattern = '^(BaseEntity|BasePlayer|BaseNetworkable)$'
$HostMethodPattern = 'Position|Rotation|Scale|Parent|Teleport|TransformChanged|NetworkPositionTick'
try {
    for ($Index = 0; $Index -lt $AssemblyPath.Count; $Index++) {
        $Path = (Resolve-Path -LiteralPath $AssemblyPath[$Index]).Path
        $Hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($Hash -cne $ExpectedSha256[$Index].ToLowerInvariant()) {
            throw "[World:WriterInventory] Input drift: $Path"
        }
        $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
        $Opened.Add($Assembly)
        $Inputs.Add([pscustomobject]@{ Path = $Path; Sha256 = $Hash; Mvid = "$($Assembly.MainModule.Mvid)"; Identity = $Assembly.Name.FullName })
        foreach ($Module in $Assembly.Modules) {
            $Pending = [Collections.Generic.Queue[object]]::new()
            foreach ($Type in $Module.Types) { $Pending.Enqueue($Type) }
            while ($Pending.Count) {
                $Type = $Pending.Dequeue()
                foreach ($Nested in $Type.NestedTypes) { $Pending.Enqueue($Nested) }
                foreach ($Method in $Type.Methods) {
                    $Key = $Assembly.Name.Name + '|' + $Method.FullName
                    if ($Definitions.ContainsKey($Key)) {
                        # Exact target contains duplicate-signature metadata. Do
                        # not collapse callers or pretend a reference is unique.
                        $Definitions[$Key] = 'AmbiguousDefinition'
                    } else {
                        $Definitions[$Key] = $Method
                    }
                    $Methods.Add([pscustomobject]@{ Assembly = $Assembly.Name.Name; Method = $Method })
                }
            }
        }
    }
    foreach ($Entry in $Methods) {
        $Method = $Entry.Method
        $TypeName = $Method.DeclaringType.FullName
        if ($Method.IsInternalCall -or $Method.IsPInvokeImpl) {
            $NativeBoundaries.Add([pscustomobject]@{
                Assembly = $Entry.Assembly; Method = $Method.FullName
                Token = $Method.MetadataToken.ToUInt32()
                Kind = $(if ($Method.IsInternalCall) { 'NativeInternalCall' } else { 'NativePInvoke' })
                Module = $(if ($Method.HasPInvokeInfo) { $Method.PInvokeInfo.Module.Name } else { $null })
                EntryPoint = $(if ($Method.HasPInvokeInfo) { $Method.PInvokeInfo.EntryPoint } else { $null })
                Relevance = 'NativeBodyAndRootReachabilityUnproven'
            })
        }
        if ($TypeName -cmatch $FamilyPattern) {
            $Kind = if ($Method.IsInternalCall) { 'NativeInternalCall' }
                elseif ($Method.IsPInvokeImpl) { 'NativePInvoke' }
                elseif ($Method.HasBody) { 'ManagedBody' }
                else { 'NoManagedBody' }
            $Boundaries.Add([pscustomobject]@{ Assembly = $Entry.Assembly; Method = $Method.FullName; Token = $Method.MetadataToken.ToUInt32(); Kind = $Kind; Public = $Method.IsPublic; ImplAttributes = "$($Method.ImplAttributes)" })
        }
        if (!$Method.HasBody) { continue }
        foreach ($Instruction in $Method.Body.Instructions) {
            $Code = "$($Instruction.OpCode.Code)"
            $Operand = $Instruction.Operand
            if ($Code -eq 'Calli' -or ($Operand -is [Mono.Cecil.MethodReference] -and
                $Operand.DeclaringType.FullName -cmatch '^System\.(Reflection\.|Delegate$|Activator$|Reflection\.Emit\.)')) {
                $Unknowns.Add([pscustomobject]@{ Assembly = $Entry.Assembly; Caller = $Method.FullName; Token = $Method.MetadataToken.ToUInt32(); Offset = $Instruction.Offset; Instruction = "$Instruction"; Kind = 'DynamicDispatchCandidate' })
            }
            if ($Operand -isnot [Mono.Cecil.MethodReference]) { continue }
            $TargetType = $Operand.DeclaringType.FullName
            if ($TargetType.StartsWith('UnityEngine.', [StringComparison]::Ordinal) -and $TargetType -cnotmatch $FamilyPattern) {
                $OtherKey = "$($Operand.DeclaringType.Scope)|$($Operand.FullName)"
                if (!$OtherUnityTargets.ContainsKey($OtherKey)) {
                    $OtherUnityTargets[$OtherKey] = [pscustomobject]@{
                        Target = $Operand.FullName; Scope = "$($Operand.DeclaringType.Scope)"
                        Instructions = 0; ExampleCaller = $Method.FullName
                        ExampleToken = $Method.MetadataToken.ToUInt32(); ExampleOffset = $Instruction.Offset
                        Kind = 'OtherUnityTypeRelevanceUnreviewed'
                    }
                }
                $OtherUnityTargets[$OtherKey].Instructions++
            }
            if ($TargetType -cnotmatch $FamilyPattern -and !($TargetType -cmatch $HostPattern -and $Operand.Name -cmatch $HostMethodPattern)) { continue }
            $Scope = $Operand.DeclaringType.Scope
            $TargetAssembly = if ($Scope -is [Mono.Cecil.ModuleDefinition]) { $Scope.Assembly.Name.Name }
                elseif ($Scope -is [Mono.Cecil.AssemblyNameReference]) { $Scope.Name }
                else { "$Scope" }
            $Definition = $Definitions[$TargetAssembly + '|' + $Operand.FullName]
            $Kind = if ($null -eq $Definition) { 'ExternalDefinitionNotSupplied' }
                elseif ($Definition -is [string]) { 'AmbiguousDefinition' }
                elseif ($Definition.IsInternalCall) { 'NativeInternalCall' }
                elseif ($Definition.IsPInvokeImpl) { 'NativePInvoke' }
                elseif ($Definition.HasBody) { 'ManagedBody' }
                else { 'NoManagedBody' }
            $Calls.Add([pscustomobject]@{
                Assembly = $Entry.Assembly; Caller = $Method.FullName
                Token = $Method.MetadataToken.ToUInt32(); Offset = $Instruction.Offset
                OpCode = $Code; TargetAssembly = $TargetAssembly; Target = $Operand.FullName
                TargetKind = $Kind
                Timing = 'Unproven'; Identity = 'RootOrAncestorNotProven'
                Bookkeeping = 'Unproven'; Thread = 'Unproven'
            })
        }
    }
    [pscustomobject]@{
        Status = 'RESEARCH_ONLY_NOT_COVERAGE_PASS'
        Scope = 'All method bodies and nested types in explicitly pinned input modules'
        Inputs = $Inputs.ToArray(); MethodCount = $Methods.Count
        NativePInvokeDefinitionCount = @($Methods | Where-Object { $_.Method.IsPInvokeImpl }).Count
        NativeInternalCallDefinitionCount = @($Methods | Where-Object { $_.Method.IsInternalCall }).Count
        Calls = $Calls.ToArray(); Boundaries = $Boundaries.ToArray()
        NativeBoundaries = $NativeBoundaries.ToArray()
        DynamicDispatchCandidates = $Unknowns.ToArray()
        OtherUnityTargets = @($OtherUnityTargets.Values | Sort-Object Scope,Target)
        Unresolved = @('Engine native producers are outside managed IL',
            'Later compiled plugins and additional assemblies are outside the input set',
            'Reflection/delegates/calli require reachability review',
            'Root/ancestor identity, synchronization, hard work and allocation bounds need independent proof')
    }
} finally {
    foreach ($Assembly in $Opened) { $Assembly.Dispose() }
}
