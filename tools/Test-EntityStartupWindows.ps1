# Research-only Entity-1A startup probe. Run only against the disposable
# D:\Sandbox\Codex\Entity1AStartup server copy on the Windows worker.
param([switch]$Hotload)
$ErrorActionPreference = 'Stop'
$Work = 'D:\Sandbox\Codex\Entity1AStartup'
$ServerRoot = Join-Path $Work 'server'
$ServerExe = Join-Path $ServerRoot 'RustDedicated.exe'
$Fixture = Join-Path $ServerRoot 'carbon\plugins\CarbonLuau.EntityStartupEvidence.cs'
$Staged = Join-Path $Work 'hotload-fixture.cs'
if (!(Test-Path -LiteralPath $ServerExe) -or
    ($Hotload -and ((Test-Path -LiteralPath $Fixture) -or !(Test-Path -LiteralPath $Staged))) -or
    (!$Hotload -and !(Test-Path -LiteralPath $Fixture))) {
    throw '[CarbonLuau:EntityStartup] Disposable server or fixture missing'
}
if (Get-Process -Name RustDedicated -ErrorAction SilentlyContinue) {
    throw '[CarbonLuau:EntityStartup] Another RustDedicated process is running; leave it untouched'
}
$Evidence = Join-Path $Work ('evidence\' + $(if ($Hotload) { 'hotload-' } else { 'startup-' }) +
    (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $Evidence -ErrorAction Stop | Out-Null
$Log = Join-Path $Evidence 'server.log'
$Arguments = @(
    '-batchmode', '-nographics', '-logfile', $Log,
    '+server.ip', '127.0.0.1', '+server.port', '28335', '+server.queryport', '28337',
    '+server.identity', 'entity1a-startup-windows',
    '+server.hostname', 'CarbonLuauEntityStartupResearch',
    '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
    '+server.saveinterval', '600', '+rcon.port', '0'
)
$Server = Start-Process -FilePath $ServerExe -ArgumentList $Arguments -WorkingDirectory $ServerRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
try {
    $Deadline = (Get-Date).AddMinutes(15)
    $Hotloaded = $false
    while ((Get-Date) -lt $Deadline) {
        if (Test-Path -LiteralPath $Log) {
            $Content = Get-Content -Raw -LiteralPath $Log -ErrorAction SilentlyContinue
            if ($Hotload -and !$Hotloaded -and $Content -match 'Server startup complete') {
                Copy-Item -LiteralPath $Staged -Destination $Fixture -ErrorAction Stop
                $Hotloaded = $true
            }
            $Marker = if ($Hotload) { '\[CarbonLuau:EntityStartup\] HOTLOAD_UNQUALIFIED' }
                else { '\[CarbonLuau:EntityStartup\] PASS' }
            if ($Content -match $Marker) {
                $Content -split "`n" | Where-Object { $_ -match 'EntityStartup|Server startup complete' } |
                    Select-Object -Last 40
                Write-Output "[CarbonLuau:EntityStartup] Windows research PASS; hotload=$Hotload log=$Log"
                return
            }
            if ($Content -match '\[CarbonLuau:EntityStartup\] SAVE_FAILED|Failed compiling|Failed to compile') {
                throw "[CarbonLuau:EntityStartup] Fixture failure; log=$Log"
            }
        }
        $Server.Refresh()
        if ($Server.HasExited) { throw "[CarbonLuau:EntityStartup] Server exited; log=$Log" }
        Start-Sleep -Seconds 1
    }
    throw "[CarbonLuau:EntityStartup] Timeout; log=$Log"
}
finally {
    $Server.Refresh()
    if (!$Server.HasExited) {
        $Server.Kill()
        $Server.WaitForExit(30000) | Out-Null
    }
}
