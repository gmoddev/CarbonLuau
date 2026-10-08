# Run on dockerbox only after the main owner coordinates these built artifacts.
# Without -RunCoordinated this performs read-only artifact/server preflight.
param(
    [string]$Work = 'D:\Sandbox\Codex\Entity1AStartup',
    [Parameter(Mandatory=$true)][string]$PackageSource,
    [Parameter(Mandatory=$true)][string]$NativeSource,
    [Parameter(Mandatory=$true)][string]$CompilerSource,
    [Parameter(Mandatory=$true)][string]$QualifiedHookSource,
    [switch]$RunCoordinated
)
$ErrorActionPreference = 'Stop'
$Prefix = '[CarbonLuau:DiscoveryPublicRunner]'
if ([IO.Path]::GetFullPath($Work).TrimEnd('\') -ne 'D:\Sandbox\Codex\Entity1AStartup') {
    throw "$Prefix Expected established task-owned Windows server"
}
$Root = Join-Path $Work 'server'
$Package = Join-Path $Root 'carbon\plugins\CarbonLuau.cszip'
$NativeDirectory = Join-Path $Root 'carbon\data\CarbonLuau\native\win-x64'
$Native = Join-Path $NativeDirectory 'carbonluau_native.dll'
$Compiler = Join-Path $NativeDirectory 'carbonluau_compiler.exe'
$CarbonConfig = Join-Path $Root 'carbon\config.json'
$ServerExe = Join-Path $Root 'RustDedicated.exe'
$HookNames = @('Carbon.Hooks.Base.dll','Carbon.Hooks.Community.dll','Carbon.Hooks.Oxide.dll')
$Targets = @($Package,$Native,$Compiler,$CarbonConfig,$ServerExe)
$Sources = @($PackageSource,$NativeSource,$CompilerSource)
foreach ($Name in $HookNames) {
    $Targets += Join-Path $Root ('carbon\managed\hooks\' + $Name)
    $Sources += Join-Path $QualifiedHookSource $Name
}
foreach ($Path in ($Targets + $Sources)) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Prefix Required file missing: $Path" }
}
if (Get-Process RustDedicated -ErrorAction SilentlyContinue) { throw "$Prefix Existing RustDedicated process; left untouched" }
if ((Get-FileHash (Join-Path $QualifiedHookSource 'Carbon.Hooks.Oxide.dll')).Hash -ne
    '71377237bcbfac6f97f28eea7a926890ba0e0b56d810e053c4485d012b27d40d' -or
    (Get-FileHash (Join-Path $QualifiedHookSource 'Carbon.Hooks.Community.dll')).Hash -ne
    '1c6a9a3d7511a334a429ca05750c7ef47abed251e760d0d6edb3a73421efafd3') {
    throw "$Prefix Qualified historical Windows hook tuple required"
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (!("DiscoveryPublicProcessErrors" -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class DiscoveryPublicProcessErrors {
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode);
}
'@
}
$Archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackageSource))
try {
    $Names = @($Archive.Entries | ForEach-Object FullName)
    if (@($Names | Where-Object { $_ -eq 'CarbonLuau.DiscoveryPublicFixtures.cs' }).Count -ne 1 -or
        $Names -contains 'CarbonLuau.EntityDiscoveryFixtures.cs') { throw "$Prefix Select the public fixture package exclusively" }
} finally { $Archive.Dispose() }
$Inputs = @(
    [pscustomobject]@{Target=$Package; Source=$PackageSource; Name='package.cszip'},
    [pscustomobject]@{Target=$Native; Source=$NativeSource; Name='native.dll'},
    [pscustomobject]@{Target=$Compiler; Source=$CompilerSource; Name='compiler.exe'}
)
$InputHashes = @{}
foreach ($InputRecord in $Inputs) {
    if ([IO.Path]::GetFullPath($InputRecord.Source) -eq [IO.Path]::GetFullPath($InputRecord.Target)) {
        throw "$Prefix Coordinated input must be staged outside its installed target"
    }
    $InputHashes[$InputRecord.Name] = (Get-FileHash -LiteralPath $InputRecord.Source).Hash
    Write-Output "$Prefix INPUT $($InputRecord.Name) SHA256=$($InputHashes[$InputRecord.Name])"
}
if (!$RunCoordinated) { Write-Output "$Prefix PREFLIGHT only; no server files/processes changed. Add -RunCoordinated after coordination."; return }
# Atomic task-root lease prevents overlapping runner writes; do not remove a
# pre-existing lease automatically. A failed/stale run needs explicit inspection.
$LeasePath = Join-Path $Work 'discovery-public.runner.lock'
$Lease = [IO.File]::Open($LeasePath,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$Evidence = Join-Path $Work ('evidence\discovery-public-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$Restorations = [Collections.Generic.List[object]]::new()
$Server = $null
$RestoreErrors = [Collections.Generic.List[string]]::new()
try {
    New-Item -ItemType Directory -Path $Evidence | Out-Null
    foreach ($Record in $Inputs) {
        $Backup = Join-Path $Evidence ('prior-' + $Record.Name)
        Copy-Item -LiteralPath $Record.Target -Destination $Backup
        $Restorations.Add([pscustomobject]@{Target=$Record.Target; Backup=$Backup; Hash=(Get-FileHash -LiteralPath $Backup).Hash})
    }
    foreach ($Name in $HookNames) {
        $Target = Join-Path $Root ('carbon\managed\hooks\' + $Name)
        $Backup = Join-Path $Evidence ('prior-' + $Name)
        Copy-Item -LiteralPath $Target -Destination $Backup
        $Restorations.Add([pscustomobject]@{Target=$Target; Backup=$Backup; Hash=(Get-FileHash -LiteralPath $Backup).Hash})
    }
    $Backup = Join-Path $Evidence 'prior-carbon-config.json'
    Copy-Item -LiteralPath $CarbonConfig -Destination $Backup
    $Restorations.Add([pscustomobject]@{Target=$CarbonConfig; Backup=$Backup; Hash=(Get-FileHash -LiteralPath $Backup).Hash})
    # Backups precede every installation/config change.
    foreach ($Record in $Inputs) { Copy-Item -LiteralPath $Record.Source -Destination $Record.Target }
    foreach ($Name in $HookNames) { Copy-Item -LiteralPath (Join-Path $QualifiedHookSource $Name) -Destination (Join-Path $Root ('carbon\managed\hooks\' + $Name)) }
    $Config = Get-Content -Raw -LiteralPath $CarbonConfig | ConvertFrom-Json
    $Config.SelfUpdating.Enabled = $false; $Config.SelfUpdating.HookUpdates = $false
    [IO.File]::WriteAllText($CarbonConfig, ($Config | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
    $Log = Join-Path $Evidence 'server.log'
    $Arguments = @('-batchmode','-nographics','-logfile',$Log,
        '+server.ip','127.0.0.1','+server.port','28335','+server.queryport','28337',
        '+server.identity','entity1a-adapter-windows','+server.hostname','CarbonLuauDiscoveryPublic',
        '+server.worldsize','1000','+server.seed','13579','+server.maxplayers','1',
        '+server.saveinterval','600','+rcon.port','0')
    # Child inherits process error mode; no machine-wide error policy is changed.
    $PreviousErrorMode = [DiscoveryPublicProcessErrors]::SetErrorMode(0x8003)
    try {
        $Server = Start-Process $ServerExe -ArgumentList $Arguments -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
        # Keep the task-owned process handle before exit. Windows PowerShell's
        # Start-Process object can otherwise lose ExitCode after polling HasExited.
        $TaskProcessHandle = $Server.Handle
    } finally { [DiscoveryPublicProcessErrors]::SetErrorMode($PreviousErrorMode) | Out-Null }
    $Deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $Deadline -and !$Server.HasExited) {
        if (Test-Path -LiteralPath $Log) {
            $Content = Get-Content -Raw -LiteralPath $Log
            if ($Content -match 'Failed compiling|Failed to compile|\[CarbonLuau:DiscoveryPublicFixture\] FAIL') {
                throw "$Prefix Compile/fixture failure: $Log"
            }
        }
        Start-Sleep -Seconds 1; $Server.Refresh()
    }
    if (!$Server.HasExited) { throw "$Prefix Timeout: $Log" }
    $Server.WaitForExit(); $Server.Refresh()
    if ($null -eq $Server.ExitCode) { throw "$Prefix Process exit status unavailable: $Log" }
    # Exact target ConVar.Global.quit calls Shutdown then Process.Kill (IL_0061),
    # which returns -1 on Windows. Accept only after full receipts/shutdown below.
    if ($Server.ExitCode -notin @(0,-1)) { throw "$Prefix Server exited with code $($Server.ExitCode): $Log" }
    $Content = Get-Content -Raw -LiteralPath $Log
    $Content -split "`n" | Where-Object { $_ -match 'DiscoveryPublicFixture|EntityLifetime|Server startup complete' }
    $Required = @('COMPLETE cases=', 'CLEANUP requests=0 traversal=0 vm=0 ownedEntities=0',
        'CHECK rejected-arguments-zero-requests','CHECK provisional-cold-nested-zero-requests-committed-export',
        'CHECK eight-global-two-domain-ready-reservations','CHECK one','CHECK limit-one-success','CHECK empty','CHECK boundary-many',
        'CHECK prefab-case-exact','CHECK prefab-no-prefix','CHECK result-limit',
        'CHECK default-256','CHECK explicit-256','CHECK default-257-fails','CHECK immutable-options-snapshot',
        'CHECK kill-after-traversal-before-op38-synthetic-id-reuse-no-retarget',
        'CHECK birth-watermark-and-spawn-after-traversal','CHECK external-movement-before-encounter-after-observation',
        'CHECK nested-callback-fresh-later-turn',
        'CHECK failed-root-preserves-ready-completion','CHECK successful-root-cancels-scanning-and-ready-work',
        'CHECK internal-cancel-stable-error','CHECK absolute-deadline-at-op38-injected-clock',
        'CHECK callback-fault-no-replay','CHECK fresh-vm','CHECK vm-fatal-ready-discard-fresh-authority',
        'CHECK failed-addon-preserves-ready-public-completions',
        'CHECK dependency-provider-loss-restoration-optional-no-rebind-retained-pure-value',
        'CHECK repeated-addon-replacement-provider-unload-scanning-ready-no-replay',
        'CHECK catalog-loss-empty-ready-before-op38','CHECK catalog-loss-scanning-live-callback-authority',
        'CHECK unexpected-pump-ondestroy-zero-pending-runtime-and-roots-cleared')
    foreach ($Marker in $Required) {
        if (!$Content.Contains('[CarbonLuau:DiscoveryPublicFixture] ' + $Marker)) { throw "$Prefix Missing receipt '$Marker': $Log" }
    }
    if ($Content -match '\[CarbonLuau:DiscoveryPublicFixture\] FAIL' -or
        $Content -notmatch '\[CarbonLuau:EntityLifetime\] Private startup observer qualified') { throw "$Prefix Qualification failed: $Log" }
    Write-Output "$Prefix EVIDENCE $Log SHA256=$((Get-FileHash -LiteralPath $Log).Hash) PACKAGE=$($InputHashes['package.cszip']) NATIVE=$($InputHashes['native.dll']) COMPILER=$($InputHashes['compiler.exe'])"
    if (!$Content.Contains('Shutting down Carbon..') -or !$Content.Contains('Saving complete')) {
        throw "$Prefix Missing qualified quit shutdown/save receipt: $Log"
    }
    Write-Output "$Prefix PROCESS_EXIT $($Server.ExitCode) qualified Shutdown -> Process.Kill"
} finally {
    $Exited = $true
    if ($null -ne $Server) {
        try {
            $Server.Refresh()
            if (!$Server.HasExited) { $Server.Kill(); $Exited = $Server.WaitForExit(30000) }
        } catch { $Exited=$false; $RestoreErrors.Add('Process cleanup: ' + $_.Exception.Message) }
        if (!$Exited) { $RestoreErrors.Add('Owned server did not exit; backups retained, loaded files not overwritten') }
    }
    if ($Exited) {
        foreach ($Record in $Restorations) {
            try {
                Copy-Item -LiteralPath $Record.Backup -Destination $Record.Target
                if ((Get-FileHash -LiteralPath $Record.Target).Hash -ne $Record.Hash) { throw 'restoration hash mismatch' }
            } catch { $RestoreErrors.Add($Record.Target + ': ' + $_.Exception.Message) }
        }
    }
    $Lease.Dispose()
    if ($RestoreErrors.Count -eq 0) {
        Remove-Item -LiteralPath $LeasePath
        Write-Output "$Prefix CLEANUP owned process exited; prior package/native/compiler/hooks/config hashes restored"
    } else { throw ($Prefix + ' CLEANUP_FAILED lease/backups retained: ' + ($RestoreErrors -join '; ')) }
}
