# Narrow exact-host/root-field and bounded-reader structural evidence. This is
# not a substitute for supported-host thread semantics or live layout validation.
param([Parameter(Mandatory=$true)][string]$WindowsRust,
    [Parameter(Mandatory=$true)][string]$LinuxRust,
    [Parameter(Mandatory=$true)][string]$WindowsCore,
    [Parameter(Mandatory=$true)][string]$LinuxCore,
    [Parameter(Mandatory=$true)][string]$WindowsPlayer,
    [Parameter(Mandatory=$true)][string]$LinuxPlayer,
    [Parameter(Mandatory=$true)][string]$CecilAssembly)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilAssembly
$Checks = 0
function Check([bool]$Condition, [string]$Reason) {
    if (!$Condition) { throw "[CarbonLuau:DiscoveryEvidence] $Reason" }
    $script:Checks++
}
function Types($Collection) { foreach ($Type in $Collection) { $Type; Types $Type.NestedTypes } }
function InspectRust([string]$Path, [string]$Hash) {
    Check ((Get-FileHash -LiteralPath $Path).Hash -eq $Hash) 'Rust identity drift'
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        $Type = $Assembly.MainModule.GetType('BaseNetworkable')
        Check (@($Type.Fields | Where-Object { $_.Name -eq '_prefabName' -and $_.FieldType.FullName -eq 'System.String' }).Count -eq 1) 'cached prefab field drift'
        $Pool = $Assembly.MainModule.GetType('StringPool')
        Check (@($Pool.Fields | Where-Object { $_.Name -eq 'toString' -and $_.IsStatic -and $_.FieldType.FullName -eq 'System.Collections.Generic.Dictionary`2<System.UInt32,System.String>' }).Count -eq 1) 'manifest pool shape drift'
        $Getter = @($Type.Methods | Where-Object Name -eq 'get_TransformHandle')
        Check ($Getter.Count -eq 1 -and $Getter[0].Body.Instructions.Count -eq 3) 'cached root getter body changed'
        $Body = $Getter[0].Body.Instructions
        Check ($Body[0].OpCode.Code.ToString() -eq 'Ldarg_0' -and $Body[1].OpCode.Code.ToString() -eq 'Ldfld' -and
            $Body[1].Operand.Name -eq '_transformHandle' -and $Body[2].OpCode.Code.ToString() -eq 'Ret') 'getter is not fixed managed field copy'
        $Writes = 0
        foreach ($Current in (Types $Assembly.MainModule.Types)) {
            foreach ($Method in $Current.Methods) {
                if (!$Method.HasBody) { continue }
                foreach ($Instruction in $Method.Body.Instructions) {
                    if ($Instruction.OpCode.Code.ToString() -ne 'Stfld' -or $Instruction.Operand.Name -ne '_transformHandle') { continue }
                    $Writes++
                    Check ($Current.FullName -eq 'BaseNetworkable' -and $Method.Name -eq 'SpawnShared') 'cached root has an unqualified writer'
                    $Previous = $Instruction.Previous
                    Check ($Previous.Operand.Name -eq 'get_transformHandle') 'SpawnShared no longer refreshes exact Component root handle'
                }
            }
        }
        Check ($Writes -eq 1) 'cached root write coverage changed'
    }
    finally { $Assembly.Dispose() }
}
function InspectCore([string]$Path, [string]$Hash) {
    Check ((Get-FileHash -LiteralPath $Path).Hash -eq $Hash) 'CoreModule identity drift'
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        $Handle = $Assembly.MainModule.GetType('UnityEngine.TransformHandle')
        Check ($null -ne $Handle -and $Handle.IsValueType -and $Handle.IsSequentialLayout) 'root handle layout kind changed'
        $Fields = @($Handle.Fields | Where-Object { !$_.IsStatic })
        Check ($Fields.Count -eq 2 -and $Fields[0].Name -eq 'pTransformData' -and $Fields[0].FieldType.FullName -eq 'System.IntPtr' -and
            $Fields[1].Name -eq 'id' -and $Fields[1].FieldType.FullName -eq 'UnityEngine.EntityId') 'root handle field layout changed'
        $Access = $Assembly.MainModule.GetType('UnityEngine.Jobs.TransformAccess')
        Check ($null -ne $Access -and @($Access.Methods | Where-Object { $_.Name -match 'SetParent|Destroy|Resize|Capacity' }).Count -eq 0) 'supported job access acquired structural mutation'
    }
    finally { $Assembly.Dispose() }
}
InspectRust $WindowsRust 'bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2'
InspectRust $LinuxRust 'cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450'
InspectCore $WindowsCore '93b0e7e8e1b9a82f34d09740c45be2cbd7c13a82683a192af2858831b63ce45a'
InspectCore $LinuxCore 'ada97d7037c7c928d8d432f734115d82a3d805a2da628f481901da5d165795e2'
Check ((Get-FileHash -LiteralPath $WindowsPlayer).Hash -eq '6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a') 'Windows native image drift'
Check ((Get-FileHash -LiteralPath $LinuxPlayer).Hash -eq 'ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1') 'Linux native image drift'
$Root = Split-Path -Parent $PSScriptRoot
$Source = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/CarbonLuau.EntityPositionBorrow.Carbon.cs')
$Hot = $Source.Substring($Source.IndexOf('internal bool TryObserve('))
$Hot = [regex]::Replace($Hot, '(?m)//.*$', '')
Check ($Hot -cnotmatch '\.transform\b|\.position\b|\.Complete\(|\.Schedule\(|\.IsCompleted\b|DynamicInvoke|GetValue\(|GetMethod\(') 'borrow entered an engine/wait/reflection path'
foreach ($Term in @('while (Count < MaximumRecords)', 'Index < 0 || Index >= Capacity',
    'Capacity > Int32.MaxValue / 48', 'checked(Index * 48)', 'checked(Index * 4)', 'Parent == -1',
    'Thread.CurrentThread.ManagedThreadId != OwnerThread', 'Handle = Entity.TransformHandle')) {
    Check ($Hot.Contains($Term)) "bounded borrow guard missing: $Term"
}
Write-Output "[CarbonLuau:DiscoveryEvidence] PASS Checks=$Checks; exact root field/native pins/capped read shape; thread premise and live layout are separately qualified"
