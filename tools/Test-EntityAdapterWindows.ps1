# Private Entity-1A observer qualification on the task-owned disposable copy.
# Never attach to or stop a pre-existing RustDedicated process.
param([switch]$DemandHooks, [switch]$Hotload, [switch]$PrivateFixture, [switch]$ReadFixture)
$ErrorActionPreference = 'Stop'
$Work = 'D:\Sandbox\Codex\Entity1AStartup'
$ServerRoot = Join-Path $Work 'server'
$ServerExe = Join-Path $ServerRoot 'RustDedicated.exe'
$Package = Join-Path $ServerRoot 'carbon\plugins\CarbonLuau.cszip'
$StagedPackage = Join-Path $Work 'adapter-package-staged.cszip'
$DemandFixture = Join-Path $ServerRoot 'carbon\plugins\CarbonLuau.EntityHookDemandEvidence.cs'
if (!(Test-Path -LiteralPath $ServerExe) -or
    ($Hotload -and ((Test-Path -LiteralPath $Package) -or !(Test-Path -LiteralPath $StagedPackage))) -or
    (!$Hotload -and !(Test-Path -LiteralPath $Package)) -or
    ($DemandHooks -and !(Test-Path -LiteralPath $DemandFixture))) {
    throw '[CarbonLuau:EntityAdapter] Disposable server/package/fixture missing'
}
if (Get-Process -Name RustDedicated -ErrorAction SilentlyContinue) {
    throw '[CarbonLuau:EntityAdapter] Another RustDedicated process is running; leave it untouched'
}
$Evidence = Join-Path $Work ('evidence\adapter-' + $(if ($Hotload) { 'hotload-' } elseif ($DemandHooks) { 'demand-' } else { 'clean-' }) +
    (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $Evidence -ErrorAction Stop | Out-Null
$Log = Join-Path $Evidence 'server.log'
$Arguments = @(
    '-batchmode', '-nographics', '-logfile', $Log,
    '+server.ip', '127.0.0.1', '+server.port', '28335', '+server.queryport', '28337',
    '+server.identity', 'entity1a-adapter-windows',
    '+server.hostname', 'CarbonLuauEntityAdapterPrivate',
    '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1',
    '+server.saveinterval', '600', '+rcon.port', '0'
)
$Server = Start-Process -FilePath $ServerExe -ArgumentList $Arguments -WorkingDirectory $ServerRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
try {
    $Deadline = (Get-Date).AddMinutes(15)
    $PackageHotloaded = $false
    while ((Get-Date) -lt $Deadline) {
        if (Test-Path -LiteralPath $Log) {
            $Content = Get-Content -Raw -LiteralPath $Log -ErrorAction SilentlyContinue
            if ($Hotload -and !$PackageHotloaded -and $Content -match 'Server startup complete') {
                Copy-Item -LiteralPath $StagedPackage -Destination $Package -ErrorAction Stop
                $PackageHotloaded = $true
            }
            if ($Content -match 'Failed compiling|Failed to compile' -or
                (!$Hotload -and $Content -match '\[CarbonLuau:EntityLifetime\] Observer unavailable')) {
                throw "[CarbonLuau:EntityAdapter] Startup/compile rejected; log=$Log"
            }
            if ($PrivateFixture -and $Content -match '\[CarbonLuau:EntityPrivateFixture\] FAIL') {
                throw "[CarbonLuau:EntityAdapter] Private fixture failed; log=$Log"
            }
            if ($ReadFixture -and $Content -match '\[CarbonLuau:EntityReadFixture\] FAIL') {
                throw "[CarbonLuau:EntityAdapter] Read fixture failed; log=$Log"
            }
            if ($Hotload -and $PackageHotloaded -and
                $Content -match '\[CarbonLuau:EntityLifetime\] Observer unavailable' -and
                $Content -match 'Loaded plugin CarbonLuau v') {
                $Content -split "`n" | Where-Object { $_ -match 'EntityLifetime|Server startup complete|Loaded plugin CarbonLuau v' } |
                    Select-Object -Last 20
                Write-Output "[CarbonLuau:EntityAdapter] Windows first-install hotload fail-closed PASS; log=$Log"
                return
            }
            if ($Content -match '\[CarbonLuau:EntityLifetime\] Private startup observer qualified for pinned host' -and
                $Content -match 'Server startup complete' -and
                (!$PrivateFixture -or $Content -match '\[CarbonLuau:EntityPrivateFixture\] PASS') -and
                (!$ReadFixture -or $Content -match '\[CarbonLuau:EntityReadFixture\] PASS') -and
                (!$DemandHooks -or $Content -match '\[CarbonLuau:EntityHookDemand\] READY')) {
                $Content -split "`n" | Where-Object { $_ -match 'EntityLifetime|EntityHookDemand|EntityPrivateFixture|Server startup complete' } |
                    Select-Object -Last 20
                Write-Output "[CarbonLuau:EntityAdapter] Windows private observer startup PASS; demand=$DemandHooks log=$Log"
                return
            }
        }
        $Server.Refresh()
        if ($Server.HasExited) { throw "[CarbonLuau:EntityAdapter] Server exited; log=$Log" }
        Start-Sleep -Seconds 1
    }
    throw "[CarbonLuau:EntityAdapter] Timeout; log=$Log"
}
finally {
    $Server.Refresh()
    if (!$Server.HasExited) {
        $Server.Kill()
        $Server.WaitForExit(30000) | Out-Null
    }
}
