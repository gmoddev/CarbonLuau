# Two-method, metadata-only reproduction of the already-qualified IPlayer publicizer.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InputDirectory,
    [Parameter(Mandatory=$true)][string]$RustDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$TempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (!$OutputDirectory.StartsWith($TempRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputDirectory)) {
    throw '[Research:MeleePins] Use a fresh directory under user Temp.'
}
# Reuse only the pure inspection functions, not the main script or its 67-target work.
$Tokens=$null; $Errors=$null
$Ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Reproduce-EntityDeathPins.ps1'),[ref]$Tokens,[ref]$Errors)
if ($Errors.Count) { throw '[Research:MeleePins] Inspection helper parse failed.' }
foreach ($Name in @('Require','GetHash','GetSections','GetRaw','GetEH','GetShape','GetJson')) {
    $Function=$Ast.Find({param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq $Name},$true)
    if (!$Function) { throw '[Research:MeleePins] Missing inspection helper.' }
    . ([scriptblock]::Create($Function.Extent.Text))
}
Add-Type -Path (Join-Path $RustDirectory 'Mono.Cecil.dll')
$Expected=@{
    win=@('bb3de3439f82440280ef61b10287a70b1731adefafa30d1716220a16878376b2','0e0a2635838e1ea2f5f67f7d63db272719a74b1e4b699929cf26106234f0e99a','b7fcd4e088dcf362a2ff13931119c411a60bd7f2205efd9fa4a0fcffe715fe28')
    linux=@('cb2bf76bb351b17f10be1ee5b31c293eb6effed419e079f2f9d3d3e5c24d8450','5f587e079d0667fdbb558118d5dfe0db1c9bf57c9ea8152bf1f24a6d6bda8aeb','7f3f16e569ed7963d54f03a8074a8b5f1206071c0e616da34aafb58af7b165f0')
}
$ExpectedText=@{PlayerAttack='6a1ce7c1d0499e1a875d49581d6c45992765c4755461320a085c13571c9e1787';DoAttackShared='261a344bcfc90575c2727f42e69835e70d75abc19813dd2d07cbf69769e59e89'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$Rows=[Collections.Generic.List[object]]::new()
foreach ($Platform in @('win','linux')) {
    $Paths=@((Join-Path $RustDirectory "Assembly-CSharp-$Platform.dll"),(Join-Path $InputDirectory "Carbon.Common-$Platform.dll"),(Join-Path $InputDirectory "Carbon.Startup-$Platform.dll"))
    for($Index=0;$Index -lt 3;$Index++) { Require ((Get-FileHash -LiteralPath $Paths[$Index]).Hash.ToLowerInvariant() -ceq $Expected[$Platform][$Index]) 'Qualified input hash mismatch.' }
    $Resolver=[Mono.Cecil.DefaultAssemblyResolver]::new(); $Resolver.AddSearchDirectory($InputDirectory); $Resolver.AddSearchDirectory($RustDirectory)
    $Reader=[Mono.Cecil.ReaderParameters]::new(); $Reader.AssemblyResolver=$Resolver
    $Original=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Paths[0],$Reader)
    $Common=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Paths[1],$Reader)
    $Buffer=[IO.MemoryStream]::new(); $Copy=$null
    try {
        $Before=@{}
        foreach($Method in $Original.MainModule.GetType('BaseMelee').Methods) {
            if (!$ExpectedText.ContainsKey($Method.Name)) { continue }
            $Shape=GetShape $Method
            Require ($Shape.ILTextSHA256 -ceq $ExpectedText[$Method.Name]) 'Backing melee semantics drift.'
            $Before[$Method.Name]=$Shape
        }
        Require ($Before.Count -eq 2) 'Exactly two melee targets required.'
        $IPlayer=$Common.MainModule.GetType('Oxide.Core.Libraries.Covalence','IPlayer')
        $Original.MainModule.GetType('BasePlayer').Fields.Add([Mono.Cecil.FieldDefinition]::new('IPlayer',[Mono.Cecil.FieldAttributes]134,$Original.MainModule.ImportReference($IPlayer)))
        $Original.Write($Buffer); $Bytes=$Buffer.ToArray(); $Buffer.Position=0
        [IO.File]::WriteAllBytes((Join-Path $OutputDirectory "$Platform-Processed-Assembly-CSharp.dll"),$Bytes)
        $Copy=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Buffer,$Reader); $Sections=@(GetSections $Bytes)
        foreach($Method in $Copy.MainModule.GetType('BaseMelee').Methods) {
            if (!$Before.ContainsKey($Method.Name)) { continue }
            $After=GetShape $Method; $Prior=$Before[$Method.Name]
            foreach($Key in @('Text','Token','CodeSize','ExpectedEH','Locals','InitLocals')) { Require ((GetJson $Prior[$Key]) -ceq (GetJson $After[$Key])) ('Processed melee shape drift: '+$Key) }
            $Raw=GetRaw $Method $Bytes $Sections
            $EH=@($After.ExpectedEH | ForEach-Object { "$($_.TryOffset):$($_.TryLength):$($_.HandlerOffset):$($_.HandlerLength):$($_.Kind)" }) -join ','
            $Rows.Add([pscustomobject]@{Platform=$Platform;Name=$Method.Name;Token=('{0:x8}' -f $After.Token);Size=$Raw.Length;RawSHA256=(GetHash $Raw);EH=$EH;ILTextSHA256=$After.ILTextSHA256;Locals=$After.Locals;LiveMatch=$false})
            [IO.File]::WriteAllBytes((Join-Path $OutputDirectory "$Platform-$($Method.Name).processed.il.bin"),$Raw)
            [IO.File]::WriteAllText((Join-Path $OutputDirectory "$Platform-$($Method.Name).resolved.il.txt"),$After.Text)
        }
    } finally { if($Copy){$Copy.Dispose()};$Buffer.Dispose();$Original.Dispose();$Common.Dispose();$Resolver.Dispose() }
}
$Rows | ConvertTo-Json -Depth 10 | Out-File -LiteralPath (Join-Path $OutputDirectory 'MeleeProcessedPins.json') -Encoding utf8
foreach($Name in @('PlayerAttack','DoAttackShared')) {
    $Win=@($Rows | Where-Object { $_.Platform -eq 'win' -and $_.Name -eq $Name })[0]
    $Linux=@($Rows | Where-Object { $_.Platform -eq 'linux' -and $_.Name -eq $Name })[0]
    foreach($Key in @('Token','Size','EH','ILTextSHA256','Locals')){Require ((GetJson $Win.$Key) -ceq (GetJson $Linux.$Key)) 'Cross-platform semantic/slot drift.'}
    Write-Output "$Name $($Win.Token)|$($Win.Size)|$($Win.RawSHA256)|$($Linux.RawSHA256)|$($Win.EH)"
}
Write-Output '[Research:MeleePins] Reproduced candidates only; actual loaded bodies, topology and RPC fixture remain qualification gates.'
