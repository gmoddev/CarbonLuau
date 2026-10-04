# Owns only a disposable server process and the unique position research fixture.
param([string]$Work = 'D:\Sandbox\Codex\Entity1AStartup',
    [ValidateSet('Bound','Phase','Access')][string]$Research = 'Bound')
$ErrorActionPreference = 'Stop'
$Root = Join-Path $Work 'server'
$Fixture = "CarbonLuau.WorldPosition${Research}Research.cs"
$Marker = "Position${Research}Research"
$Source = Join-Path $Work $Fixture
$Target = Join-Path (Join-Path $Root 'carbon\plugins') $Fixture
if (!(Test-Path -LiteralPath (Join-Path $Root 'RustDedicated.exe')) -or
    !(Test-Path -LiteralPath $Source) -or (Test-Path -LiteralPath $Target) -or
    (Get-Process RustDedicated -ErrorAction SilentlyContinue)) { throw 'Isolated server/fixture precondition failed' }
$Evidence = Join-Path $Work ("evidence\position-$($Research.ToLowerInvariant())-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $Evidence | Out-Null
$Log = Join-Path $Evidence 'server.log'
$Server = $null
try {
    Copy-Item -LiteralPath $Source -Destination $Target
    $Arguments = @('-batchmode','-nographics','-logfile',$Log,
        '+server.ip','127.0.0.1','+server.port','28335','+server.queryport','28337',
        '+server.identity','entity1a-adapter-windows','+server.hostname','CarbonLuauPositionResearch',
        '+server.worldsize','1000','+server.seed','13579','+server.maxplayers','1',
        '+server.saveinterval','600','+rcon.port','0')
    $Server = Start-Process (Join-Path $Root 'RustDedicated.exe') -ArgumentList $Arguments -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
    $Deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $Deadline -and !$Server.HasExited) { Start-Sleep -Seconds 1; $Server.Refresh() }
    if (!$Server.HasExited) { throw "Position research timeout: $Log" }
    $Content = Get-Content -Raw -LiteralPath $Log
    $Content -split "`n" | Where-Object { $_.Contains($Marker) -or $_ -match 'Server startup complete' }
    $PassPattern = '\[CarbonLuau:' + [regex]::Escape($Marker) + '\] OBSERVATION_PASS.*NOT_HARD_BOUND'
    if ($Content -notmatch $PassPattern -or $Content.Contains("$Marker] FAIL")) { throw "Position research evidence failed: $Log" }
    Write-Output "[CarbonLuau:$Marker] EVIDENCE $Log"
} finally {
    if ($null -ne $Server) { $Server.Refresh(); if (!$Server.HasExited) { $Server.Kill(); $Server.WaitForExit(30000) | Out-Null } }
    if (Test-Path -LiteralPath $Target) { Remove-Item -LiteralPath $Target }
    Write-Output "[CarbonLuau:$Marker] CLEANUP owned process/fixture only"
}
