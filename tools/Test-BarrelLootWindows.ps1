# Run on dockerbox only after the main owner coordinates these built artifacts.
# Without -RunCoordinated this performs read-only artifact/server preflight.
param(
    [string]$Work = 'D:\Sandbox\Codex\Entity1AStartup',
    [Parameter(Mandatory=$true)][string]$PackageSource,
    [Parameter(Mandatory=$true)][string]$NativeSource,
    [Parameter(Mandatory=$true)][string]$CompilerSource,
    [Parameter(Mandatory=$true)][string]$QualifiedHookSource,
    [switch]$Qualification,
    [switch]$Public,
    [switch]$EntityDeath,
    [switch]$Barrel2,
    [switch]$LootItems,
    [switch]$LootItemProbeOnly,
    [switch]$LootRegistry,
    [switch]$LootAdditionalItems,
    [switch]$D7ProbeOnly,
    [switch]$LootBulkItems,
    [switch]$D8DiscoveryOnly,
    [switch]$D8ProbeOnly,
    [switch]$D8StackGapOnly,
    [switch]$D8StackGapFixed,
    [switch]$LootStackItems,
    [int]$D8StackPart = 0,
    [string]$StackProbeDirectory,
    [switch]$RunCoordinated
)
$ErrorActionPreference = 'Stop'
if ($D8StackGapOnly -and $D8StackGapFixed) { throw 'Select old-negative or fixed stack gap mode' }
if ($D8StackGapOnly -or $D8StackGapFixed) { $LootStackItems = $true }
if ($LootStackItems -and ($LootBulkItems -or $D7ProbeOnly -or $LootItemProbeOnly)) { throw 'Select stack qualification exclusively' }
if ($D8StackPart -lt 0 -or $D8StackPart -gt 3) { throw 'Stack part must be0..3' }
$StackMatrix = $LootStackItems -and !$D8StackGapOnly -and !$D8StackGapFixed
$StackTerminal = $StackMatrix -and $D8StackPart -eq 2
$StackSweep = $StackMatrix -and $D8StackPart -eq 3
if (($StackMatrix -and !$StackSweep) -ne [bool]$StackProbeDirectory) { throw 'Only stack ordinary/terminal matrix requires explicit separate probe directory' }
if ($StackTerminal -and $EntityDeath) { throw 'Terminal UID negative does not claim B2 completion' }
if ($LootStackItems) { $LootAdditionalItems = $true }
if (($D8DiscoveryOnly -or $D8ProbeOnly) -and !$LootBulkItems) { throw 'D8 mode requires LootBulkItems selection' }
if ($D8DiscoveryOnly -and $D8ProbeOnly) { throw 'Select D8 discovery or physical probe mode' }
if ($LootBulkItems -and ($D7ProbeOnly -or $LootItemProbeOnly)) { throw 'Select D8 mode exclusively' }
if ($LootBulkItems) { $LootAdditionalItems = $true }
$D8EarlyOnly = $D8DiscoveryOnly -or $D8ProbeOnly
if ($D7ProbeOnly -and !$LootAdditionalItems) { throw 'D7 probe requires LootAdditionalItems qualification selection' }
if ($LootAdditionalItems -and $LootItemProbeOnly) { throw 'Select the D7 probe exclusively' }
if ($LootAdditionalItems) { $LootRegistry = $true }
if ($LootRegistry -and $LootItemProbeOnly) { throw 'D6 registry requires full public qualification' }
if ($LootRegistry) { $LootItems = $true }
if ($LootItemProbeOnly -and !$LootItems) { throw 'D5 probe requires LootItems qualification selection' }
if ($LootItems) { $Barrel2 = $Public = $Qualification = $true }
$Prefix = '[CarbonLuau:BarrelLootResearchRunner]'
$FixtureMarker = if ($Qualification) { '[CarbonLuau:LootFixture]' } else { '[CarbonLuau:BarrelLootResearch]' }
$PassMarker = if ($D8DiscoveryOnly) { 'PASS D8 registry discovery' } elseif ($D8ProbeOnly) { 'PASS D8 bulk probe batch' } elseif ($D7ProbeOnly) { 'PASS D7 stones probe' } elseif ($LootItemProbeOnly) { 'PASS D5 item probe' } elseif ($Public) { 'PASS public qualification' } elseif ($Qualification) { 'PASS private qualification' } else { 'PASS mechanism' }
if ($StackTerminal) { $PassMarker = 'D8_STACK_BATCH mode=terminal_uid completed=true cases=1 unsupported_i12=1 normal_cleanup_claim=false process_restoration_required=true' }
if ($Public -and !$Qualification) { throw 'Public qualification requires the established D2A fixture substrate' }
if ($EntityDeath -and !$Public) { throw 'Entity death qualification requires the public fixture substrate' }
if ($Barrel2 -and !$Public) { throw 'Barrel-2 qualification requires the public fixture substrate' }
if ([IO.Path]::GetFullPath($Work).TrimEnd('\') -ne 'D:\Sandbox\Codex\Entity1AStartup') {
    throw "$Prefix Expected established task-owned Windows server"
}
$Root = Join-Path $Work 'server'
function Get-LootProfileFiles {
    param([string]$ProfileRoot)
    $ExpectedRoot = 'D:\Sandbox\Codex\Entity1AStartup\server\server\entity1a-adapter-windows'
    $WorldFiles = @('proceduralmap.1000.13579.289.sav','proceduralmap.1000.13579.289.sav.1',
        'proceduralmap.1000.13579.289.sav.2','proceduralmap.1000.13579.289.navmesh')
    if ([IO.Path]::GetFullPath($ProfileRoot) -cne $ExpectedRoot -or
        !(Test-Path -LiteralPath $ProfileRoot -PathType Container) -or
        ((Get-Item -LiteralPath $ProfileRoot).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "$Prefix Unsafe terminal profile directory" }
    $Ancestor = Get-Item -LiteralPath $ProfileRoot -Force; $Depth = 0
    while ($null -ne $Ancestor) {
        if (++$Depth -gt 16 -or ($Ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "$Prefix Unsafe terminal profile path ancestry" }
        $Ancestor = $Ancestor.Parent
    }
    $Entries = @(Get-ChildItem -LiteralPath $ProfileRoot -Force | Select-Object -First 257)
    if ($Entries.Count -gt 256) { throw "$Prefix Terminal profile directory entry bound exceeded" }
    $Files = @($Entries | Where-Object { $_.Name -match '\.db(?:-(?:wal|shm|journal))?$' -or $_.Name -cin $WorldFiles } | Sort-Object Name)
    if ($Files.Count -gt 128 -or ($Files | Measure-Object Length -Sum).Sum -gt 67108864) { throw "$Prefix Terminal profile snapshot bound exceeded" }
    foreach ($File in $Files) {
        if (($File.Name -notmatch '^[A-Za-z0-9_.-]+\.db(?:-(?:wal|shm|journal))?$' -and $File.Name -cnotin $WorldFiles) -or
            $File.PSIsContainer -or $File.LinkType -eq 'HardLink' -or ($File.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($File.FullName)) -cne $ExpectedRoot) { throw "$Prefix Unsafe terminal profile snapshot target" }
    }
    return $Files
}
function Get-LootProfileMetadata {
    param([string]$Path)
    $File = Get-Item -LiteralPath $Path -Force
    $Acl = Get-Acl -LiteralPath $Path
    return [pscustomobject]@{Attributes=[int]$File.Attributes; Created=$File.CreationTimeUtc.Ticks;
        Accessed=$File.LastAccessTimeUtc.Ticks; Written=$File.LastWriteTimeUtc.Ticks; Acl=$Acl; Sddl=$Acl.Sddl}
}
function Set-LootProfileMetadata {
    param([string]$Path, $Metadata)
    if ((Get-Acl -LiteralPath $Path).Sddl -cne $Metadata.Sddl) { Set-Acl -LiteralPath $Path -AclObject $Metadata.Acl }
    $File = Get-Item -LiteralPath $Path -Force
    $File.CreationTimeUtc = [DateTime]::new($Metadata.Created, [DateTimeKind]::Utc)
    $File.LastWriteTimeUtc = [DateTime]::new($Metadata.Written, [DateTimeKind]::Utc)
    $File.LastAccessTimeUtc = [DateTime]::new($Metadata.Accessed, [DateTimeKind]::Utc)
    $File.Attributes = [IO.FileAttributes]$Metadata.Attributes
    $Actual = Get-LootProfileMetadata $Path
    foreach ($Key in @('Attributes','Created','Accessed','Written','Sddl')) {
        if ($Actual.$Key -cne $Metadata.$Key) { throw "$Prefix Terminal profile metadata restoration mismatch: $Key" }
    }
}
function Save-LootProfile {
    param([string]$ProfileRoot, [string]$EvidenceDirectory)
    $DirectoryMetadata = Get-LootProfileMetadata $ProfileRoot
    $Files = @(Get-LootProfileFiles $ProfileRoot)
    New-Item -ItemType Directory -Path $EvidenceDirectory | Out-Null
    $Records = @{}
    foreach ($File in $Files) {
        $Metadata = Get-LootProfileMetadata $File.FullName
        $Backup = Join-Path $EvidenceDirectory $File.Name
        Copy-Item -LiteralPath $File.FullName -Destination $Backup
        $Records[$File.Name] = [pscustomobject]@{Target=$File.FullName; Backup=$Backup;
            Hash=(Get-FileHash -LiteralPath $Backup).Hash.ToLowerInvariant(); Bytes=$File.Length; Metadata=$Metadata}
        Set-LootProfileMetadata $File.FullName $Metadata
    }
    $Public = @($Records.Keys | Sort-Object | ForEach-Object {
        $Record = $Records[$_]
        [ordered]@{Name=$_; SHA256=$Record.Hash; Bytes=$Record.Bytes; Attributes=$Record.Metadata.Attributes;
            Created=$Record.Metadata.Created; Accessed=$Record.Metadata.Accessed; Written=$Record.Metadata.Written; Sddl=$Record.Metadata.Sddl}
    })
    $Text = ConvertTo-Json -InputObject $Public -Depth 5 -Compress
    $Bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    $Hash = [Security.Cryptography.SHA256]::Create()
    try { $Digest = [BitConverter]::ToString($Hash.ComputeHash($Bytes)).Replace('-','').ToLowerInvariant() } finally { $Hash.Dispose() }
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'profile-manifest.json'),$Text,[Text.UTF8Encoding]::new($false))
    Set-LootProfileMetadata $ProfileRoot $DirectoryMetadata
    return [pscustomobject]@{Root=$ProfileRoot; Files=$Records; DirectoryMetadata=$DirectoryMetadata; ManifestSHA256=$Digest}
}
function Restore-LootProfile {
    param($Snapshot)
    foreach ($File in @(Get-LootProfileFiles $Snapshot.Root)) {
        if (!$Snapshot.Files.ContainsKey($File.Name)) { Remove-Item -LiteralPath $File.FullName -Force }
    }
    foreach ($Record in $Snapshot.Files.Values) {
        if ((Test-Path -LiteralPath $Record.Target) -and (!(Test-Path -LiteralPath $Record.Target -PathType Leaf) -or
            ((Get-Item -LiteralPath $Record.Target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint))) { throw "$Prefix Unsafe terminal profile restore target" }
        if ((Get-FileHash -LiteralPath $Record.Backup).Hash.ToLowerInvariant() -cne $Record.Hash) { throw "$Prefix Terminal profile backup changed" }
        Copy-Item -LiteralPath $Record.Backup -Destination $Record.Target -Force
        if ((Get-FileHash -LiteralPath $Record.Target).Hash.ToLowerInvariant() -cne $Record.Hash -or
            (Get-Item -LiteralPath $Record.Target).Length -ne $Record.Bytes) { throw "$Prefix Terminal profile restoration bytes mismatch" }
        Set-LootProfileMetadata $Record.Target $Record.Metadata
    }
    $Restored = @(Get-LootProfileFiles $Snapshot.Root)
    if ($Restored.Count -ne $Snapshot.Files.Count -or @($Restored | Where-Object { !$Snapshot.Files.ContainsKey($_.Name) }).Count -ne 0) { throw "$Prefix Terminal profile restored file set mismatch" }
    Set-LootProfileMetadata $Snapshot.Root $Snapshot.DirectoryMetadata
    $Size = ($Snapshot.Files.Values | Measure-Object Bytes -Sum).Sum
    Write-Output "$Prefix FullProfileRestored files=$($Snapshot.Files.Count) bytes=$Size file_cap=128 byte_cap=67108864 metadata=true scope=db_companions_saves_navmesh manifest_sha256=$($Snapshot.ManifestSHA256)"
}
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
if (!("BarrelLootProcessErrors" -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class BarrelLootProcessErrors {
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode);
}
'@
}
$Archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackageSource))
try {
    $Names = @($Archive.Entries | ForEach-Object FullName)
    if (@($Names | Where-Object { $_ -eq 'CarbonLuau.BarrelLootResearch.cs' }).Count -ne 1 -or
        $Names -contains 'CarbonLuau.EntityDiscoveryFixtures.cs') { throw "$Prefix Select the public fixture package exclusively" }
    if (($Names -contains 'CarbonLuau.LootFixtures.cs') -ne [bool]$Qualification) { throw "$Prefix Qualification fixture selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootBarrel2Fixtures.cs') -ne [bool]$Barrel2) { throw "$Prefix Barrel-2 fixture selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootItemFixtures.cs') -ne [bool]$LootItems) { throw "$Prefix D5 fixture selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootItemProbeFixtures.cs') -ne [bool]$LootItemProbeOnly) { throw "$Prefix D5 probe selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootRegistryFixtures.cs') -ne [bool]$LootRegistry) { throw "$Prefix D6 registry selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootAdditionalItemFixtures.cs') -ne [bool]$LootAdditionalItems) { throw "$Prefix D7 fixture selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootAdditionalItemProbeFixtures.cs') -ne [bool]$D7ProbeOnly) { throw "$Prefix D7 probe selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootBulkItemFixtures.cs') -ne [bool]$LootBulkItems) { throw "$Prefix D8 fixture selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootStackFixtures.cs') -ne [bool]$LootStackItems) { throw "$Prefix D8 stack fixture selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootStackMatrixFixtures.cs') -ne [bool]($StackMatrix -and !$StackSweep) -or
        ($Names -contains 'CarbonLuau.LootStackModeFixtures.cs') -ne [bool]$StackMatrix) { throw "$Prefix D8 stack matrix selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootFinalItemFixtures.cs') -ne [bool]$StackSweep) { throw "$Prefix D8 final sweep selection mismatch" }
    if ($Names -contains 'CarbonLuauStackProbeA.cs' -or $Names -contains 'CarbonLuauStackProbeB.cs') { throw "$Prefix Hook probes must remain separate test plugins" }
    if (($Names -contains 'CarbonLuau.LootBulkItemModeFixtures.cs') -ne [bool]$D8EarlyOnly) { throw "$Prefix D8 mode selection mismatch" }
    if (($Names -contains 'CarbonLuau.LootBulkItemManifestFixtures.cs') -ne [bool]($LootBulkItems -and !$D8EarlyOnly)) { throw "$Prefix D8 public manifest selection mismatch" }
} finally { $Archive.Dispose() }
$Inputs = @(
    [pscustomobject]@{Target=$Package; Source=$PackageSource; Name='package.cszip'},
    [pscustomobject]@{Target=$Native; Source=$NativeSource; Name='native.dll'},
    [pscustomobject]@{Target=$Compiler; Source=$CompilerSource; Name='compiler.exe'}
)
$StackPreserved = @()
if ($StackMatrix -and !$StackSweep) {
    foreach ($Name in @('CarbonLuauStackProbeA','CarbonLuauStackProbeB')) {
        $Source = Join-Path $StackProbeDirectory ($Name + '.cs')
        if (!(Test-Path -LiteralPath $Source -PathType Leaf)) { throw "$Prefix Missing bounded separate stack probe: $Source" }
        $Inputs += [pscustomobject]@{Target=(Join-Path $Root ('carbon\plugins\' + $Name + '.cs')); Source=$Source; Name=($Name + '.cs')}
        $StackPreserved += Join-Path $Root ('carbon\configs\' + $Name + '.json')
    }
}
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
$LeasePath = Join-Path $Work 'loot-d2a.runner.lock'
$Lease = [IO.File]::Open($LeasePath,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$Evidence = Join-Path $Work ('evidence\loot-d2a-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$Restorations = [Collections.Generic.List[object]]::new()
$Server = $null
$RestoreErrors = [Collections.Generic.List[string]]::new()
$ProfileSnapshot = $null
try {
    New-Item -ItemType Directory -Path $Evidence | Out-Null
    if ($StackTerminal) {
        $ProfileSnapshot = Save-LootProfile (Join-Path $Root 'server\entity1a-adapter-windows') (Join-Path $Evidence 'prior-terminal-profile')
        Write-Output "$Prefix PROFILE_SNAPSHOT files=$($ProfileSnapshot.Files.Count) file_cap=128 byte_cap=67108864 manifest_sha256=$($ProfileSnapshot.ManifestSHA256)"
    }
    foreach ($Record in $Inputs) {
        $Backup = Join-Path $Evidence ('prior-' + $Record.Name)
        if (Test-Path -LiteralPath $Record.Target -PathType Leaf) {
            Copy-Item -LiteralPath $Record.Target -Destination $Backup
            $Restorations.Add([pscustomobject]@{Target=$Record.Target; Backup=$Backup; Hash=(Get-FileHash -LiteralPath $Backup).Hash; Absent=$false})
        } else {
            if (Test-Path -LiteralPath $Record.Target) { throw "$Prefix Installed target is not a file" }
            $Restorations.Add([pscustomobject]@{Target=$Record.Target; Backup=$null; Hash=$null; Absent=$true})
        }
    }
    foreach ($Target in $StackPreserved) {
        $Backup = Join-Path $Evidence ('prior-config-' + [IO.Path]::GetFileName($Target))
        if (Test-Path -LiteralPath $Target -PathType Leaf) {
            Copy-Item -LiteralPath $Target -Destination $Backup
            $Restorations.Add([pscustomobject]@{Target=$Target; Backup=$Backup; Hash=(Get-FileHash -LiteralPath $Backup).Hash; Absent=$false})
        } else {
            if (Test-Path -LiteralPath $Target) { throw "$Prefix Probe config target is not a file" }
            $Restorations.Add([pscustomobject]@{Target=$Target; Backup=$null; Hash=$null; Absent=$true})
        }
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
        '+server.identity','entity1a-adapter-windows','+server.hostname','CarbonLuauBarrelLootResearch',
        '+server.worldsize','1000','+server.seed','13579','+server.maxplayers','1',
        '+server.saveinterval','600','+rcon.port','0')
    # Child inherits process error mode; no machine-wide error policy is changed.
    $PreviousErrorMode = [BarrelLootProcessErrors]::SetErrorMode(0x8003)
    try {
        $Server = Start-Process $ServerExe -ArgumentList $Arguments -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence 'console.log') -RedirectStandardError (Join-Path $Evidence 'console.err') -PassThru
        # Keep the task-owned process handle before exit. Windows PowerShell's
        # Start-Process object can otherwise lose ExitCode after polling HasExited.
        $TaskProcessHandle = $Server.Handle
    } finally { [BarrelLootProcessErrors]::SetErrorMode($PreviousErrorMode) | Out-Null }
    $Deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $Deadline -and !$Server.HasExited) {
        if (Test-Path -LiteralPath $Log) {
            $Content = Get-Content -Raw -LiteralPath $Log
            if ($Content -match 'Failed compiling|Failed to compile|\[CarbonLuau:(BarrelLootResearch|LootFixture)\] (FAIL|CLEANUP_FAIL)') {
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
    $Content -split "`n" | Where-Object { $_ -match 'LootFixture|EntityDiedFixture|EntityMeleeFixture|MEASURED case=|PASS mechanism|EntityLifetime|Server startup complete' }
    $Required = @($PassMarker)
    if ($D8StackGapOnly -or $D8StackGapFixed) {
        $StackRows = [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_GAP item=[^\r\n]+')
        $StackBatches = [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_BATCH [^\r\n]+')
        $ExpectedBatch = if ($D8StackGapOnly) { '[CarbonLuau:LootFixture] D8_STACK_BATCH mode=gap_probe completed=true cases=12 pass=0 fail=12 cleanup=0 all_pass=False' } else { '[CarbonLuau:LootFixture] D8_STACK_BATCH mode=gap_probe completed=true cases=12 pass=12 fail=0 cleanup=0 all_pass=True' }
        $ExpectedRow = if ($D8StackGapOnly) { ' result=FAIL committed=True created=0 accepted=0 selected_delta=1 partial_delta=1 original_stored=2 vanilla_worlds=0 reason=stack_drop_after_prepare_suppresses_vanilla\z' } else { ' result=PASS committed=False created=0 accepted=0 selected_delta=1 partial_delta=0 original_stored=2 vanilla_worlds=2 reason=stack_drop_before_commit_preserves_vanilla\z' }
        if ($StackRows.Count -ne 12 -or $StackBatches.Count -ne 1 -or
            $StackBatches[0].Value -cne $ExpectedBatch -or
            @($StackRows | Where-Object { $_.Value -cnotmatch $ExpectedRow }).Count -ne 0 -or
            [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_CLEANUP [^\r\n]+ ledger=0').Count -ne 12) {
            throw "$Prefix Missing exact old-source stack-gap counterexample/cleanup receipts"
        }
        if ($D8StackGapOnly) { Write-Output "$Prefix EXPECTED_NEGATIVE D8 stack gap old-source counterexample; compatibility remains unqualified" }
    }
    if ($StackMatrix -and !$StackTerminal -and !$StackSweep) {
        $StackRows = [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_CASE [^\r\n]+')
        $Expected = '[CarbonLuau:LootFixture] D8_STACK_BATCH mode=matrix part=' + $D8StackPart + ' completed=true cases=57 pass=57 fail=0 cleanup=0 all_pass=True'
        if ($StackRows.Count -ne 57 -or [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_BATCH [^\r\n]+').Count -ne 1 -or !$Content.Contains($Expected) -or
            @($StackRows | Where-Object { $_.Value -cnotmatch (' D8_STACK_CASE part=' + $D8StackPart + ' item=[a-z0-9._-]+ phase=([0-9]|1[0-8]) case=[a-z_]+ result=PASS profiles=2 cleanup=0\z') }).Count -ne 0 -or
            @($StackRows | ForEach-Object { $_.Value } | Select-Object -Unique).Count -ne 57) { throw "$Prefix D8 stack ordinary matrix incomplete or failed" }
    }
    if ($StackTerminal) {
        if (!$Content.Contains('[CarbonLuau:LootFixture] D8_STACK_LEGACY before_terminal=true d3_d7_completed=true ledger=0 b2_terminal_claim=false') -or
            $Content.Contains('[CarbonLuau:LootFixture] PASS public qualification')) { throw "$Prefix Terminal negative must preserve separate scope and refuse normal cleanup claim" }
        if ([regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_TERMINAL [^\r\n]+').Count -ne 1 -or
            $Content -cnotmatch 'D8_STACK_TERMINAL item=sulfur id=-1581843485 result=UNSUPPORTED_I12 original_uid=[1-9][0-9]* actual_uid=0 world=[1-9][0-9]* completed=true reciprocal_ref=true partial_delta=1 cleanup_failed_delta=1 max_calls=0 can_calls=0 split_calls=0 drop_calls=1 normal_cleanup_claim=false uid_restored=false authority_refreshed=false') { throw "$Prefix Missing exact terminal I12 negative" }
    }
    if ($StackSweep) {
        $SweepRows = [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_SWEEP name=[^\r\n]+')
        $SweepPattern = '\[CarbonLuau:LootFixture\] D8_STACK_SWEEP name=([a-z0-9._-]+) id=(-?[0-9]+) stackable=([1-9][0-9]*) family=(Baseline|RawResource|Component|BasicMaterial|Consumable|Equivalent) prefab=(\S+) minimum=1 maximum=([1-9][0-9]*) result=PASS worlds=2 cleanup=0 original_uid_world_verified=true completed_spawn=true\z'
        if ($SweepRows.Count -ne 442 -or @($SweepRows | Where-Object { $_.Value -cnotmatch $SweepPattern }).Count -ne 0 -or
            @($SweepRows | ForEach-Object { $_.Value } | Select-Object -Unique).Count -ne 442 -or
            [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_SWEEP_WORLD [^\r\n]+').Count -ne 884 -or
            [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_SWEEP_BEGIN [^\r\n]+').Count -ne 1 -or
            [regex]::Matches($Content, '\[CarbonLuau:LootFixture\] D8_STACK_SWEEP_BATCH [^\r\n]+').Count -ne 1 -or
            !$Content.Contains('[CarbonLuau:LootFixture] D8_STACK_SWEEP_BATCH completed=true definitions=221 cases=442 pass=442 fail=0 cleanup=0 all_pass=True')) {
            throw "$Prefix D8 final-source sweep incomplete, duplicated or failed"
        }
    }
    foreach ($Marker in $Required) {
        if (!$Content.Contains($FixtureMarker + ' ' + $Marker)) { throw "$Prefix Missing receipt '$Marker': $Log" }
    }
    if ($Barrel2 -and !$LootItemProbeOnly -and !$D7ProbeOnly -and !$D8EarlyOnly -and !$StackTerminal -and !$Content.Contains('[CarbonLuau:LootFixture] PASS barrel-2')) { throw "$Prefix Barrel-2 physical matrix receipt missing: $Log" }
    if ($LootItems -and !$LootItemProbeOnly -and !$D7ProbeOnly -and !$D8EarlyOnly -and !$StackTerminal -and !$Content.Contains('[CarbonLuau:LootFixture] PASS D5 item expansion full public wood matrix')) { throw "$Prefix D5 physical item matrix receipt missing: $Log" }
    if ($LootRegistry -and !$D7ProbeOnly -and !$D8EarlyOnly -and !$StackTerminal -and !$Content.Contains('[CarbonLuau:LootFixture] PASS D6 bounded per-prefab registry')) { throw "$Prefix D6 registry matrix receipt missing: $Log" }
    if ($LootAdditionalItems -and !$D7ProbeOnly -and !$D8EarlyOnly -and !$StackTerminal -and !$Content.Contains('[CarbonLuau:LootFixture] PASS D7 stones full public')) { throw "$Prefix D7 public stones matrix receipt missing: $Log" }
    if ($LootBulkItems -and !$D8EarlyOnly -and !$Content.Contains('[CarbonLuau:LootFixture] PASS D8 bulk public')) { throw "$Prefix D8 public matrix receipt missing: $Log" }
    if ($LootBulkItems -and !$D8EarlyOnly) {
        $D8Batches = [regex]::Matches($Content, 'D8 bulk public batch offset=(\d+) count=(\d+) pass=(\d+) fail=(\d+)')
        $D8Rows = [regex]::Matches($Content, 'D8_PUBLIC_ITEM name=(\S+) item_id=(-?\d+) stackable=(\d+) family=(\S+) result=(PASS|FAIL)(?: |\r?$)')
        if ($D8Batches.Count -ne 1 -or [int]$D8Batches[0].Groups[2].Value -lt 1 -or [int]$D8Batches[0].Groups[2].Value -gt 32 -or
            [int]$D8Batches[0].Groups[3].Value -ne [int]$D8Batches[0].Groups[2].Value -or [int]$D8Batches[0].Groups[4].Value -ne 0 -or
            $D8Rows.Count -ne [int]$D8Batches[0].Groups[2].Value -or
            [regex]::Matches($Content, '\bD8_PUBLIC_ITEM\b').Count -ne $D8Rows.Count -or
            @($D8Rows | Where-Object { $_.Groups[5].Value -ne 'PASS' }).Count -ne 0 -or
            @($D8Rows | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique).Count -ne $D8Rows.Count) {
            throw "$Prefix D8 public per-item receipts incomplete, duplicated or failed: $Log"
        }
    }
    if ($EntityDeath -and (!$Content.Contains('[CarbonLuau:EntityDiedFixture] PASS bounded public transport and actual RPC source') -or
        !$Content.Contains('[CarbonLuau:EntityMeleeFixture] PASS actual RPC source/terminal proof and cleanup') -or
        $Content -match '\[CarbonLuau:(EntityDiedFixture|EntityMeleeFixture)\] (FAIL|CLEANUP FAIL)')) { throw "$Prefix Entity death receipt missing/failed: $Log" }
    if ($Content -match '\[CarbonLuau:(BarrelLootResearch|LootFixture)\] (FAIL|CLEANUP_FAIL)' -or
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
        if ($null -ne $ProfileSnapshot) {
            try { Restore-LootProfile $ProfileSnapshot }
            catch { $RestoreErrors.Add('Terminal profile restoration: ' + $_.Exception.Message) }
        }
        foreach ($Record in $Restorations) {
            try {
                if ($Record.Absent) {
                    if (Test-Path -LiteralPath $Record.Target -PathType Leaf) { Remove-Item -LiteralPath $Record.Target -Force }
                    if (Test-Path -LiteralPath $Record.Target) { throw 'created probe target absence not restored' }
                    continue
                }
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
