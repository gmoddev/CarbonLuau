param([Parameter(Mandatory=$true)][string[]]$AssemblyPath,
      [Parameter(Mandatory=$true)][string]$CecilPath)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
function Loot-Type($Type) { return $Type.FullName.Replace('/', '+') }
foreach ($Path in $AssemblyPath) {
    $Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $Path).Path)
    try {
        $Method = @($Assembly.MainModule.GetType('LootContainer').Methods | Where-Object Name -eq 'OnDied')[0]
        if ($Method.Body.CodeSize -ne 83 -or $Method.Body.ExceptionHandlers.Count -ne 0) { throw 'OnDied shape differs' }
        $Lines = foreach ($Instruction in $Method.Body.Instructions) {
            $Operand = $Instruction.Operand; $Text = ''
            if ($Operand -is [Mono.Cecil.Cil.Instruction]) { $Text = $Operand.Offset.ToString('x4') }
            elseif ($Operand -is [Mono.Cecil.MethodReference]) {
                $Parameters = @($Operand.Parameters | ForEach-Object { Loot-Type $_.ParameterType }) -join ','
                $Text = 'method:' + (Loot-Type $Operand.DeclaringType) + '::' + $Operand.Name + '(' + $Parameters + '):' + (Loot-Type $Operand.ReturnType)
            } elseif ($Operand -is [Mono.Cecil.FieldReference]) {
                $Text = 'field:' + (Loot-Type $Operand.DeclaringType) + '::' + $Operand.Name + ':' + (Loot-Type $Operand.FieldType)
            } elseif ($Operand -is [string]) { $Text = 'string:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Operand)) }
            elseif ($null -ne $Operand) { throw ('Unexpected operand: ' + $Operand.GetType().FullName) }
            $Instruction.Offset.ToString('x4') + '|' + $Instruction.OpCode.Name + '|' + $Text
        }
        $Hash = [Security.Cryptography.SHA256]::Create()
        try { $Digest = [BitConverter]::ToString($Hash.ComputeHash([Text.Encoding]::UTF8.GetBytes(($Lines -join "`n")))).Replace('-','').ToLowerInvariant() }
        finally { $Hash.Dispose() }
        Write-Output "ASSEMBLY=$Path TOKEN=$($Method.MetadataToken) SEMANTIC_SHA256=$Digest"
        $Lines
    } finally { $Assembly.Dispose() }
}
