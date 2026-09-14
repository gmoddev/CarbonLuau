param([Parameter(Mandatory)][string]$LogFile, [int]$Cycles = 3)
$ErrorActionPreference = 'Stop'
$Server = 'C:\Sandbox\Codex\Builds\CarbonLuau\server-win'
$Connection = $null
function ReadLog {
    if (!(Test-Path -LiteralPath $LogFile)) { return '' }
    $Stream = [IO.File]::Open($LogFile,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $Reader = New-Object IO.StreamReader($Stream)
    try { $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}
function WaitLog([int]$Offset,[string]$Expected,[int]$Seconds=60) {
    $Watch=[Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text=(ReadLog).Substring($Offset)
        if ($Text.Contains('[CarbonLuau:HostFixture] FAIL')) { throw "Current fixture failed: $Text" }
        if ($Text.Contains($Expected)) { return }
        Start-Sleep -Milliseconds 250
    }
    throw "Missing current-run log: $Expected"
}
function Send([string]$Command,[string]$Expected='') {
    $Reply=(& "$PSScriptRoot/Rcon.ps1" -Command $Command -ExpectedMessage $Expected -Socket $Connection -KeepOpen) | ConvertFrom-Json
    $Reply.Message
}
try {
    WaitLog 0 'Server startup complete' 600
    $Connection=& "$PSScriptRoot/Rcon.ps1" -Command '' -KeepOpen
    for ($Cycle=1; $Cycle -le $Cycles; ++$Cycle) {
        Write-Output (Send 'carbonluau.status' 'CarbonLuau: ready')
        $Offset=(ReadLog).Length
        Write-Output (Send 'carbonluau.phase3fixture' 'CarbonLuau Phase3 fixture scheduled')
        WaitLog $Offset 'PASS teardown: owned chat registrations removed and native library released'
        $Text=(ReadLog).Substring($Offset)
        foreach ($Expected in @('PASS actual Carbon command dispatch','PASS A -> rejected B -> committed C','PASS reconnect, D10','production NextFrame event')) {
            if (!$Text.Contains($Expected)) { throw "Incomplete fixture evidence: $Expected" }
        }
        $Process=Get-Process RustDedicated | Where-Object { $_.Path -eq "$Server\RustDedicated.exe" }
        if (!$Process -or $Process.Modules.ModuleName -contains 'carbonluau_native.dll') { throw 'Native DLL remained mapped after teardown' }
        Write-Output "[CarbonLuau:LiveTest] PASS Windows Phase 3 cycle $Cycle; real host types, 100 command replacements, native unmapped"
        Write-Output (Send 'status' 'hostname: CarbonLuauPhase0')
        Send 'c.unload CarbonLuau' | Out-Null
        if ($Cycle -lt $Cycles) {
            $Offset=(ReadLog).Length
            Send 'c.load CarbonLuau' | Out-Null
            WaitLog $Offset 'Ready; generation=1'
        }
    }
    Write-Output "[CarbonLuau:LiveTest] PASS Windows Phase 3: $Cycles controlled-host cycles; no real-client delivery claim"
} finally {
    if ($Connection) {
        try { Send 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:LiveTest] quit sent; RCON may close during shutdown' }
        $Connection.Dispose()
    }
}
