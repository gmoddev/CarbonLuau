param([Parameter(Mandatory)][string]$LogFile)
$ErrorActionPreference = 'Stop'
$Server = 'C:\Sandbox\Codex\Builds\CarbonLuau\server-win'
$Connection = $null
function ReadLog {
    if (!(Test-Path $LogFile)) { return '' }
    $Stream = [IO.File]::Open($LogFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $Reader = New-Object IO.StreamReader($Stream)
    try { $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}
function WaitLog([int]$Offset, [string]$Expected, [int]$Seconds = 30) {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text = ReadLog
        if ($Text.Length -gt $Offset -and $Text.Substring($Offset).Contains($Expected)) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Missing current-run log: $Expected"
}
function Send([string]$Command, [string]$Expected = '') {
    $Reply = (& "$PSScriptRoot/Rcon.ps1" -Command $Command -ExpectedMessage $Expected -Socket $Connection -KeepOpen) | ConvertFrom-Json
    return $Reply.Message
}
function Expect([string]$Command, [string]$Expected) {
    $Reply = Send $Command $Expected
    if (!$Reply.Contains($Expected)) { throw "$Command returned: $Reply" }
    Write-Output $Reply
}
try {
    WaitLog 0 'Server startup complete' 600
    WaitLog 0 'hello from Luau'
    WaitLog 0 '[CarbonLuau:Runtime] Ready; generation=1'
    $Connection = & "$PSScriptRoot/Rcon.ps1" -Command '' -KeepOpen
    Expect 'carbonluau.status' 'CarbonLuau: ready'
    foreach ($Fixture in @('timeout','valid','memory','valid','failed-reload','valid')) {
        Expect "carbonluau.fixture $Fixture" "PASS $Fixture"
        Expect 'status' 'hostname: CarbonLuauPhase0'
    }
    for ($Cycle = 1; $Cycle -le 10; $Cycle++) {
        Expect 'carbonluau.reload' 'reload OK'
        Expect 'carbonluau.status' 'CarbonLuau: ready'
    }
    for ($Cycle = 1; $Cycle -le 10; $Cycle++) {
        $Offset = (ReadLog).Length
        Send 'c.unload CarbonLuau' | Out-Null
        WaitLog $Offset 'Native library unloaded successfully'
        $Process = Get-Process RustDedicated | Where-Object { $_.Path -eq "$Server\RustDedicated.exe" }
        if (!$Process -or $Process.Modules.ModuleName -contains 'carbonluau_native.dll') { throw 'Unexpected process/native lifetime after unload' }
        $Offset = (ReadLog).Length
        Send 'c.load CarbonLuau' | Out-Null
        WaitLog $Offset '[CarbonLuau:Runtime] Ready; generation=1'
        Expect 'carbonluau.status' 'CarbonLuau: ready'
        Write-Output "[CarbonLuau:LiveTest] PASS plugin cycle $Cycle; DLL unmapped during unload"
    }
    # Qualify the actual production package too, without the fixture command.
    $Offset = (ReadLog).Length
    Send 'c.unload CarbonLuau' | Out-Null
    WaitLog $Offset 'Native library unloaded successfully'
    Copy-Item 'C:/Sandbox/Codex/Artifacts/CarbonLuau/CarbonLuau.cszip' "$Server/carbon/plugins/CarbonLuau.cszip" -Force
    # Let any host watcher request settle, then explicitly serialize a reload.
    # Watcher notifications differ between the worker's Windows/Linux hosts.
    Start-Sleep -Seconds 5
    $Offset = (ReadLog).Length
    Send 'c.load CarbonLuau' | Out-Null
    WaitLog $Offset '[CarbonLuau:Runtime] Ready; generation=1'
    Expect 'carbonluau.status' 'CarbonLuau: ready'
    Expect 'carbonluau.reload' 'reload OK'
    Write-Output '[CarbonLuau:LiveTest] PASS Windows Phase 1; 10 runtime replacements, 10 plugin cycles, production package smoke/reload'
} finally {
    if ($Connection) {
        try { Send 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:LiveTest] quit sent; RCON may close during shutdown' }
        $Connection.Dispose()
    }
}
