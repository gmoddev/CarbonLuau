# Isolated research runner. Owns only the process it starts and its unique fixture.
param([string]$Work = 'D:\Sandbox\Codex\Entity1AStartup', [string]$PackageSource,
    [string]$QualifiedHookSource)
$ErrorActionPreference = 'Stop'
$ServerRoot = Join-Path $Work 'server'
$ServerExe = Join-Path $ServerRoot 'RustDedicated.exe'
$FixtureSource = Join-Path $Work 'CarbonLuau.WorldMovementResearch.cs'
$FixtureTarget = Join-Path $ServerRoot 'carbon\plugins\CarbonLuau.WorldMovementResearch.cs'
if (!(Test-Path -LiteralPath $ServerExe) -or !(Test-Path -LiteralPath $FixtureSource) -or
    (Test-Path -LiteralPath $FixtureTarget)) { throw '[CarbonLuau:WorldMovementResearch] Fixture/server ownership precondition failed' }
if (Get-Process -Name RustDedicated -ErrorAction SilentlyContinue) {
    throw '[CarbonLuau:WorldMovementResearch] Existing RustDedicated process protected'
}
$Evidence = Join-Path $Work ('evidence\movement-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $Evidence | Out-Null
$Log = Join-Path $Evidence 'server.log'
$Server = $null
$PackageTarget = Join-Path $ServerRoot 'carbon\plugins\CarbonLuau.cszip'
$PackageBackup = Join-Path $Evidence 'prior-package.cszip'
$PackageReplaced = $false
$Restorations = [Collections.Generic.List[object]]::new()
try {
    if ($QualifiedHookSource) {
        $OxideHash = (Get-FileHash -LiteralPath (Join-Path $QualifiedHookSource 'Carbon.Hooks.Oxide.dll')).Hash
        $CommunityHash = (Get-FileHash -LiteralPath (Join-Path $QualifiedHookSource 'Carbon.Hooks.Community.dll')).Hash
        if ($OxideHash -ne '71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d' -or
            $CommunityHash -ne '1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3') {
            throw '[CarbonLuau:WorldMovementResearch] Expected qualified historical hook pair'
        }
        foreach ($Name in @('Carbon.Hooks.Base.dll','Carbon.Hooks.Community.dll','Carbon.Hooks.Oxide.dll')) {
            $Target = Join-Path $ServerRoot ('carbon\managed\hooks\' + $Name)
            $Backup = Join-Path $Evidence ('prior-' + $Name)
            Copy-Item -LiteralPath $Target -Destination $Backup
            $Restorations.Add([pscustomobject]@{ Target = $Target; Backup = $Backup })
            Copy-Item -LiteralPath (Join-Path $QualifiedHookSource $Name) -Destination $Target
        }
        $ConfigTarget = Join-Path $ServerRoot 'carbon\config.json'
        $ConfigBackup = Join-Path $Evidence 'prior-carbon-config.json'
        Copy-Item -LiteralPath $ConfigTarget -Destination $ConfigBackup
        $Restorations.Add([pscustomobject]@{ Target = $ConfigTarget; Backup = $ConfigBackup })
        $Config = Get-Content -Raw -LiteralPath $ConfigTarget | ConvertFrom-Json
        $Config.SelfUpdating.Enabled = $false
        $Config.SelfUpdating.HookUpdates = $false
        [IO.File]::WriteAllText($ConfigTarget, ($Config | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
    }
    if ($PackageSource) {
        if (!(Test-Path -LiteralPath $PackageSource) -or !(Test-Path -LiteralPath $PackageTarget)) {
            throw '[CarbonLuau:WorldMovementResearch] Package precondition failed'
        }
        Copy-Item -LiteralPath $PackageTarget -Destination $PackageBackup
        $PackageReplaced = $true
        Copy-Item -LiteralPath $PackageSource -Destination $PackageTarget
        Write-Output "[CarbonLuau:WorldMovementResearch] PACKAGE SHA256=$((Get-FileHash -LiteralPath $PackageTarget).Hash)"
    }
    Copy-Item -LiteralPath $FixtureSource -Destination $FixtureTarget
    $Arguments = @('-batchmode','-nographics','-logfile',$Log,
        '+server.ip','127.0.0.1','+server.port','28335','+server.queryport','28337',
        '+server.identity','entity1a-adapter-windows','+server.hostname','CarbonLuauMovementResearch',
        '+server.worldsize','1000','+server.seed','13579','+server.maxplayers','1',
        '+server.saveinterval','600','+rcon.port','0')
    $Server = Start-Process -FilePath $ServerExe -ArgumentList $Arguments -WorkingDirectory $ServerRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
    $Deadline = (Get-Date).AddMinutes(15)
    while ((Get-Date) -lt $Deadline) {
        $Server.Refresh()
        if ($Server.HasExited) { break }
        Start-Sleep -Seconds 1
    }
    $Server.Refresh()
    if (!$Server.HasExited) { throw "[CarbonLuau:WorldMovementResearch] Timeout; log=$Log" }
    $Content = Get-Content -Raw -LiteralPath $Log
    $Content -split "`n" | Where-Object { $_ -match 'WorldMovementResearch|Server startup complete' }
    if ($Content -notmatch '\[CarbonLuau:WorldMovementResearch\] PASS' -or
        $Content -match '\[CarbonLuau:WorldMovementResearch\] FAIL') {
        throw "[CarbonLuau:WorldMovementResearch] Evidence failed; log=$Log"
    }
    if ($QualifiedHookSource -and $Content -notmatch '\[CarbonLuau:EntityLifetime\] Private startup observer qualified for pinned host') {
        throw "[CarbonLuau:WorldMovementResearch] Exact-host baseline failed; log=$Log"
    }
    Write-Output "[CarbonLuau:WorldMovementResearch] PASS log=$Log exited=$($Server.HasExited)"
}
finally {
    if ($null -ne $Server) {
        $Server.Refresh()
        if (!$Server.HasExited) { $Server.Kill(); $Server.WaitForExit(30000) | Out-Null }
    }
    Remove-Item -LiteralPath $FixtureTarget -ErrorAction SilentlyContinue
    if ($PackageReplaced) { Copy-Item -LiteralPath $PackageBackup -Destination $PackageTarget }
    foreach ($Record in $Restorations) { Copy-Item -LiteralPath $Record.Backup -Destination $Record.Target }
    if (Test-Path -LiteralPath $FixtureTarget) { throw '[CarbonLuau:WorldMovementResearch] Owned fixture cleanup failed' }
    Write-Output '[CarbonLuau:WorldMovementResearch] CLEANUP owned process exited/stopped; unique plugin removed'
}
