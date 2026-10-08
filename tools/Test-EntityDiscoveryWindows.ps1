param([string]$Work = 'D:\Sandbox\Codex\Entity1AStartup',
    [Parameter(Mandatory=$true)][string]$PackageSource,
    [Parameter(Mandatory=$true)][string]$QualifiedHookSource)
$ErrorActionPreference = 'Stop'
$Root = Join-Path $Work 'server'
$Package = Join-Path $Root 'carbon\plugins\CarbonLuau.cszip'
if (!(Test-Path -LiteralPath $PackageSource) -or !(Test-Path -LiteralPath $Package) -or
    (Get-Process RustDedicated -ErrorAction SilentlyContinue)) { throw 'Isolated package/server precondition failed' }
if ((Get-FileHash (Join-Path $QualifiedHookSource 'Carbon.Hooks.Oxide.dll')).Hash -ne
    '71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d' -or
    (Get-FileHash (Join-Path $QualifiedHookSource 'Carbon.Hooks.Community.dll')).Hash -ne
    '1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3') { throw 'Qualified historical hook tuple required' }
$Evidence = Join-Path $Work ('evidence\discovery-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $Evidence | Out-Null
$Restorations = [Collections.Generic.List[object]]::new()
$Server = $null
try {
    foreach ($Name in @('Carbon.Hooks.Base.dll','Carbon.Hooks.Community.dll','Carbon.Hooks.Oxide.dll')) {
        $Target = Join-Path $Root ('carbon\managed\hooks\' + $Name)
        $Backup = Join-Path $Evidence ('prior-' + $Name)
        Copy-Item -LiteralPath $Target -Destination $Backup
        $Restorations.Add([pscustomobject]@{ Target=$Target; Backup=$Backup })
        Copy-Item -LiteralPath (Join-Path $QualifiedHookSource $Name) -Destination $Target
    }
    foreach ($Record in @(@{Target=$Package; Name='prior-package.cszip'},
        @{Target=(Join-Path $Root 'carbon\config.json'); Name='prior-carbon-config.json'})) {
        $Backup = Join-Path $Evidence $Record.Name
        Copy-Item -LiteralPath $Record.Target -Destination $Backup
        $Restorations.Add([pscustomobject]@{Target=$Record.Target; Backup=$Backup})
    }
    $ConfigPath = Join-Path $Root 'carbon\config.json'
    $Config = Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json
    $Config.SelfUpdating.Enabled = $false; $Config.SelfUpdating.HookUpdates = $false
    [IO.File]::WriteAllText($ConfigPath, ($Config | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath $PackageSource -Destination $Package
    $Log = Join-Path $Evidence 'server.log'
    $Arguments = @('-batchmode','-nographics','-logfile',$Log,
        '+server.ip','127.0.0.1','+server.port','28335','+server.queryport','28337',
        '+server.identity','entity1a-adapter-windows','+server.hostname','CarbonLuauDiscoveryPrivate',
        '+server.worldsize','1000','+server.seed','13579','+server.maxplayers','1',
        '+server.saveinterval','600','+rcon.port','0')
    $Server = Start-Process (Join-Path $Root 'RustDedicated.exe') -ArgumentList $Arguments -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
    $Deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $Deadline -and !$Server.HasExited) {
        if (Test-Path -LiteralPath $Log) {
            $Content = Get-Content -Raw -LiteralPath $Log
            if ($Content -match 'Failed compiling|Failed to compile|\[CarbonLuau:DiscoveryFixture\] FAIL') {
                throw "Discovery compile/fixture failure: $Log"
            }
        }
        Start-Sleep -Seconds 1; $Server.Refresh()
    }
    if (!$Server.HasExited) { throw "Discovery timeout: $Log" }
    $Content = Get-Content -Raw -LiteralPath $Log
    $Content -split "`n" | Where-Object { $_ -match 'Discovery|EntityLifetime|Server startup complete' }
    if ($Content -notmatch '\[CarbonLuau:DiscoveryFixture\] PASS' -or
        $Content -notmatch '\[CarbonLuau:EntityLifetime\] Private startup observer qualified') { throw "Discovery qualification failed: $Log" }
    Write-Output "[CarbonLuau:DiscoveryFixture] EVIDENCE $Log SHA256=$((Get-FileHash -LiteralPath $Log).Hash) PACKAGE=$((Get-FileHash -LiteralPath $PackageSource).Hash)"
} finally {
    if ($null -ne $Server) { $Server.Refresh(); if (!$Server.HasExited) { $Server.Kill(); $Server.WaitForExit(30000) | Out-Null } }
    foreach ($Record in $Restorations) { Copy-Item -LiteralPath $Record.Backup -Destination $Record.Target }
    Write-Output '[CarbonLuau:DiscoveryFixture] CLEANUP owned process exited; prior package/hooks/task config restored'
}
