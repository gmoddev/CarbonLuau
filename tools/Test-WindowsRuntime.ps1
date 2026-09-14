$ErrorActionPreference = 'Stop'
$Root = 'C:\Sandbox\Codex'
$Server = "$Root\Builds\CarbonLuau\server-win"
$Library = "$Server\carbon\data\CarbonLuau\native\win-x64\carbonluau_native.dll"
$Good = "$Root\Builds\CarbonLuau\win-x64\Release\carbonluau_native.dll"
$Log = "$Root\Artifacts\CarbonLuau\server-win.log"
$Connection = & "$PSScriptRoot\Rcon.ps1" -Command '' -KeepOpen
function Send([string]$Command) { & "$PSScriptRoot\Rcon.ps1" -Command $Command -Socket $Connection -KeepOpen }
function ReadLog {
    $Stream = [IO.File]::Open($Log, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $Reader = New-Object IO.StreamReader($Stream)
    try { $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}
function WaitLog([int]$Offset, [string]$Expected) {
    for ($Attempt = 0; $Attempt -lt 60; $Attempt++) {
        $Text = ReadLog
        if ($Text.Length -gt $Offset -and $Text.Substring($Offset).Contains($Expected)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Missing log: $Expected"
}
function Unload {
    $Offset = (ReadLog).Length
    Send 'c.unload CarbonLuau'
    WaitLog $Offset 'Unloaded plugin CarbonLuau'
    $Process = Get-Process RustDedicated | Where-Object { $_.Path -eq "$Server\RustDedicated.exe" }
    if (!$Process) { throw 'Task server exited' }
    if ($Process.Modules.ModuleName -contains 'carbonluau_native.dll') { throw 'Probe remains loaded after unload' }
}
for ($Cycle = 1; $Cycle -le 10; $Cycle++) {
    Unload
    # Confirm the DLL can be replaced between plugin instances.
    Copy-Item $Good $Library -Force
    $Offset = (ReadLog).Length
    Send 'c.load CarbonLuau'
    WaitLog $Offset 'Native probe loaded successfully'
    Write-Output "[CarbonLuau:LiveTest] PASS cycle $Cycle; DLL absent from process modules during unload"
}
foreach ($Fixture in @('Missing','Broken','MissingSymbol','WrongProbe')) {
    Unload
    if ($Fixture -eq 'Missing') { Remove-Item -LiteralPath $Library }
    elseif ($Fixture -eq 'Broken') { [IO.File]::WriteAllText($Library, 'invalid native image') }
    else { Copy-Item "$Root\Builds\CarbonLuau\win-x64\Release\$Fixture.dll" $Library -Force }
    $Offset = (ReadLog).Length
    Send 'c.load CarbonLuau'
    WaitLog $Offset 'Unavailable:'
    Send 'status'
    Unload
    Copy-Item $Good $Library -Force
    $Offset = (ReadLog).Length
    Send 'c.load CarbonLuau'
    WaitLog $Offset 'Native probe loaded successfully'
    Write-Output "[CarbonLuau:LiveTest] PASS $Fixture failure and recovery"
}
$Offset = (ReadLog).Length
Send 'c.reload CarbonLuau'
WaitLog $Offset 'Native probe loaded successfully'
Send 'status'
Write-Output '[CarbonLuau:LiveTest] PASS Windows runtime validation'
$Connection.Dispose()
