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
$Entry = "$Server/carbon/data/CarbonLuau/scripts/init.luau"
$Module = "$Server/carbon/data/CarbonLuau/scripts/modules/message.luau"
$Healthy = "local Message=require('message'); print(Message); task.defer(function() print('deferred callback') end); task.delay(0.1,function() print('delayed callback') end)"
function Source([string]$Text) { [IO.File]::WriteAllText($Entry, $Text, (New-Object Text.UTF8Encoding($false))) }
function Reload([string]$Text, [string]$Expected = 'reload OK') {
    Source $Text
    Expect 'carbonluau.reload' $Expected
    Expect 'status' 'hostname: CarbonLuauPhase0'
}
try {
    WaitLog 0 'Server startup complete' 600
    WaitLog 0 'CarbonLuau Phase 2 module loading works'
    WaitLog 0 'deferred callback'
    WaitLog 0 'delayed callback'
    $Connection = & "$PSScriptRoot/Rcon.ps1" -Command '' -KeepOpen
    Expect 'carbonluau.status' 'CarbonLuau: ready'
    $Offset = (ReadLog).Length
    Reload "task.delay(2,function() print('old queue preserved') end)"
    Reload 'local =' 'reload COMPILE_ERROR'
    [IO.File]::WriteAllText($Module,'local =')
    Reload "require('message')" 'reload RUNTIME_ERROR'
    [IO.File]::WriteAllText($Module,"error('intentional module failure')")
    Reload "require('message')" 'reload RUNTIME_ERROR'
    [IO.File]::WriteAllText($Module,'return "CarbonLuau Phase 2 module loading works"')
    Reload "task.defer(function() print('candidate leaked') end); error('entry failure')" 'reload RUNTIME_ERROR'
    WaitLog $Offset 'old queue preserved'
    if ((ReadLog).Substring($Offset).Contains('candidate leaked')) { throw 'Rejected candidate callback escaped' }
    $Offset = (ReadLog).Length
    Reload "task.delay(2,function() print('stale callback executed') end)"
    Reload $Healthy
    Start-Sleep -Seconds 3
    if ((ReadLog).Substring($Offset).Contains('stale callback executed')) { throw 'Old callback survived successful reload' }
    $Offset = (ReadLog).Length
    Reload "task.defer(function() error('intentional callback failure') end); task.defer(function() print('callback error survived') end)"
    WaitLog $Offset 'callback error survived'
    WaitLog $Offset 'intentional callback failure'
    $Offset = (ReadLog).Length
    Reload "print('recovery entry ran'); task.delay(1,function() while true do end end); task.delay(100,function() print('retired stale callback') end)"
    WaitLog $Offset 'callback deadline exceeded'
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    $Latched = $false
    while ($Watch.Elapsed.TotalSeconds -lt 15) {
        $Reply = Send 'carbonluau.status' 'CarbonLuau:'
        if ($Reply.Contains('CarbonLuau: unavailable') -and $Reply.Contains('automatic recovery exhausted')) { Write-Output $Reply; $Latched=$true; break }
        Start-Sleep -Milliseconds 250
    }
    if (!$Latched) { throw 'Second timeout did not latch unavailable' }
    if ([regex]::Matches((ReadLog).Substring($Offset), 'recovery entry ran').Count -ne 2) { throw 'Entrypoint was not reconstructed exactly once' }
    Expect 'status' 'hostname: CarbonLuauPhase0'
    Reload $Healthy
    Expect 'carbonluau.status' 'CarbonLuau: ready'
    $Offset = (ReadLog).Length
    Reload "task.delay(1,function() while true do end end)"
    Source 'local ='
    WaitLog $Offset 'callback deadline exceeded'
    Start-Sleep -Seconds 1
    Expect 'carbonluau.status' 'automatic recovery failed'
    Reload $Healthy
    for ($Cycle=1; $Cycle -le 10; $Cycle++) {
        Reload "for I=1,100 do task.delay(100,function() print('old runtime work') end) end"
        Reload $Healthy
        Expect 'carbonluau.status' 'CarbonLuau: ready'
        Write-Output "[CarbonLuau:LiveTest] PASS runtime cycle $Cycle; cancelled 100 delayed callbacks"
    }
    for ($Cycle=1; $Cycle -le 10; $Cycle++) {
        Reload "task.delay(2,function() print('old plugin work') end)"
        $Offset = (ReadLog).Length
        Send 'c.unload CarbonLuau' | Out-Null
        WaitLog $Offset 'Native library unloaded successfully'
        $Process = Get-Process RustDedicated | Where-Object { $_.Path -eq "$Server\RustDedicated.exe" }
        if (!$Process -or $Process.Modules.ModuleName -contains 'carbonluau_native.dll') { throw 'Unexpected native lifetime after unload' }
        Source $Healthy
        $Offset = (ReadLog).Length
        Send 'c.load CarbonLuau' | Out-Null
        WaitLog $Offset '[CarbonLuau:Runtime] Ready; generation=1'
        WaitLog $Offset 'deferred callback'
        WaitLog $Offset 'delayed callback'
        Expect 'carbonluau.status' 'CarbonLuau: ready'
        Write-Output "[CarbonLuau:LiveTest] PASS plugin cycle $Cycle; DLL unmapped during unload"
    }
    Start-Sleep -Seconds 3
    $Text = ReadLog
    foreach ($Forbidden in @('old plugin work','old runtime work','retired stale callback')) {
        if ($Text.Contains($Forbidden)) { throw "Stale callback executed: $Forbidden" }
    }
    Expect 'status' 'hostname: CarbonLuauPhase0'
    Write-Output '[CarbonLuau:LiveTest] PASS Windows Phase 2 production package; modules/errors/recovery; 10 runtime cancellation cycles and 10 plugin cycles'
} finally {
    if ($Connection) {
        try { Send 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:LiveTest] quit sent; RCON may close during shutdown' }
        $Connection.Dispose()
    }
}
