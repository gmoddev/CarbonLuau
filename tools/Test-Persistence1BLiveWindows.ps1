<#
Representative 1B only: production package + separate provider fixture, fresh
host-owned data, root Set/Get/Remove/reload, addon isolation/replacement, full
host reload, native unmap and exact owner-worker teardown. No client, crash,
power-loss, quota/scale, platform closure or Persistence-1C claim.
NamespaceRestartSupplement selects the narrow Root/A/B same-name isolation
and actual clean Rust process restart supplement, using one isolated data tree.

Run on dockerbox. Default is read-only preflight. Parent must approve the exact
production artifacts and controlled-fixture plan before passing both approval
switches. This script never
builds/downloads artifacts or changes machine policy. Rcon.ps1 is required.
The whole legacy plugins/data trees and CarbonLuau config are moved intact to
the run backup and restored only after the owned server/workers have stopped.
Fixture data/journals and deployed inputs remain in the evidence directory.
#>
[CmdletBinding()]
param(
    [string]$Server = 'C:\Sandbox\Codex\Builds\CarbonLuauPhase5Windows\server-win',
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][string]$NativeLibrary,
    [Parameter(Mandatory)][string]$CompilerWorker,
    [Parameter(Mandatory)][string]$StorageWorker,
    [string]$Fixture = (Join-Path $PSScriptRoot '..\tests\live\CarbonLuau.Persistence1BFixture.cs'),
    [string]$ReleaseMetadata = (Join-Path $PSScriptRoot '..\release.json'),
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [ValidateRange(1024,65532)][int]$Port = 28536,
    [switch]$ArtifactReadyApproved,
    [switch]$ControlledFixturePlanApproved,
    [switch]$NamespaceRestartSupplement
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SandboxPath([string]$Path) {
    $Full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (!$Full.StartsWith('C:\Sandbox\Codex\', [StringComparison]::OrdinalIgnoreCase)) {
        throw '[CarbonLuau:Persistence1BWorker] All run paths must be below C:\Sandbox\Codex'
    }
    $Cursor = $Full
    while ($Cursor) {
        if ((Test-Path -LiteralPath $Cursor) -and
            ((Get-Item -LiteralPath $Cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw '[CarbonLuau:Persistence1BWorker] Reparse-point run path rejected'
        }
        $Cursor = [IO.Path]::GetDirectoryName($Cursor)
    }
    return $Full
}

function Get-Helpers {
    return @(Get-CimInstance Win32_Process -Filter "Name='carbonluau_storage.exe' OR Name='carbonluau_compiler.exe'")
}

$Server = Assert-SandboxPath $Server
$ArtifactDirectory = Assert-SandboxPath $ArtifactDirectory
if ($ArtifactDirectory.StartsWith($Server + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $Server.StartsWith($ArtifactDirectory + '\', [StringComparison]::OrdinalIgnoreCase) -or $Server -eq $ArtifactDirectory) {
    throw '[CarbonLuau:Persistence1BWorker] Evidence must be separate from the server tree'
}
foreach ($InputPath in @($Package,$NativeLibrary,$CompilerWorker,$StorageWorker,$Fixture,$ReleaseMetadata,(Join-Path $PSScriptRoot 'Rcon.ps1'))) {
    if (!(Test-Path -LiteralPath $InputPath -PathType Leaf)) { throw '[CarbonLuau:Persistence1BWorker] Required artifact/helper missing' }
    $Full = Assert-SandboxPath $InputPath
    if ($Full.StartsWith($Server + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw '[CarbonLuau:Persistence1BWorker] Inputs must be outside the server tree'
    }
}
$Release = Get-Content -LiteralPath $ReleaseMetadata -Raw | ConvertFrom-Json
if ($Release.packageVersion -ne '0.4.0' -or $Release.apiVersion -ne '0.5.0-experimental' -or $Release.nativeAbi -ne '1.5') {
    throw '[CarbonLuau:Persistence1BWorker] Expected development identity: package 0.4.0, API 0.5.0-experimental, ABI 1.5'
}
$ExpectedIdentities = @(
    "CarbonLuau package: $($Release.packageVersion)",
    "Scripting API: $($Release.apiName) $($Release.apiVersion)",
    "Native ABI: $($Release.nativeAbi) OK",
    "Addon protocol: $($Release.providerProtocolName) $($Release.providerProtocolVersion); package schema: $($Release.packageSchema)",
    "Luau: $($Release.luauRevision)"
)
$Executable = Join-Path $Server 'RustDedicated.exe'
if (!(Test-Path -LiteralPath $Executable -PathType Leaf)) { throw '[CarbonLuau:Persistence1BWorker] Windows server unavailable' }
$ExpectedQuitExitCode = 0
$HostQuitEvidence = $null
if ($NamespaceRestartSupplement) {
    $HostAssemblyPath = Join-Path $Server 'RustDedicated_Data\Managed\Assembly-CSharp.dll'
    $HostSystemPath = Join-Path $Server 'RustDedicated_Data\Managed\System.dll'
    $HostAssemblyHash = (Get-FileHash -LiteralPath $HostAssemblyPath -Algorithm SHA256).Hash
    $HostSystemHash = (Get-FileHash -LiteralPath $HostSystemPath -Algorithm SHA256).Hash
    # This exact ConVar.Global.quit path saves, stops networking, then self-Kills.
    # Its installed Process.Kill explicitly passes -1 to TerminateProcess.
    if ($HostAssemblyHash -eq '543C0EB569EC6B3889DE9AA23F61CD5AF7AE5A8F0D07C2D6F1E9FB94B3D49D09' -and
        $HostSystemHash -eq '6E03AF58CD23577B5588C90A951D8B4F9E3DFE34C80E3E4171D83D29BA895AEA') {
        $ExpectedQuitExitCode = -1
    } else {
        throw '[CarbonLuau:Persistence1BWorker] Restart supplement host quit-path hashes are not qualified'
    }
    $HostQuitEvidence = [ordered]@{AssemblyCSharpSha256=$HostAssemblyHash; SystemSha256=$HostSystemHash;
        ExpectedQuitExitCode=$ExpectedQuitExitCode; HashQualifiedSelfKill=($ExpectedQuitExitCode -eq -1)}
}
$NativeDirectory = Join-Path $Server 'carbon\data\CarbonLuau\native\win-x64'
$ExistingServers = @(Get-CimInstance Win32_Process -Filter "Name='RustDedicated.exe'" | Where-Object { $_.ExecutablePath -eq $Executable -or !$_.ExecutablePath })
$ExistingHelpers = @(Get-Helpers | Where-Object { !$_.ExecutablePath -or $_.ExecutablePath.StartsWith($NativeDirectory + '\', [StringComparison]::OrdinalIgnoreCase) })
if ($ExistingServers.Count -or $ExistingHelpers.Count) { throw '[CarbonLuau:Persistence1BWorker] Installation is already owned by a process (or ownership is unreadable)' }
$BusyPorts = @(Get-NetTCPConnection -ErrorAction Stop | Where-Object { $_.LocalPort -in @($Port,($Port-1),($Port+1)) })
$BusyPorts += @(Get-NetUDPEndpoint -ErrorAction Stop | Where-Object { $_.LocalPort -in @($Port,($Port-1),($Port+1)) })
if ($BusyPorts.Count) { throw '[CarbonLuau:Persistence1BWorker] Requested loopback ports are occupied' }
if (!$ArtifactReadyApproved -or !$ControlledFixturePlanApproved) {
    Write-Output '[CarbonLuau:Persistence1BWorker] PREFLIGHT ONLY: files present and installation idle; artifact compatibility unverified. Parent artifact-readiness and controlled-plan approval required to deploy/start.'
    return
}

$RunId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$RunDirectory = Assert-SandboxPath (Join-Path $ArtifactDirectory ('persistence1b-' + $RunId))
$BackupDirectory = Assert-SandboxPath (Join-Path $RunDirectory 'backup')
$RetainedDirectory = Assert-SandboxPath (Join-Path $RunDirectory 'fixture-retained')
$ServerLog = Join-Path $RunDirectory 'server.log'
$ConsoleOut = Join-Path $RunDirectory 'console.log'
$ConsoleError = Join-Path $RunDirectory 'console-error.log'
$LogPaths = @($ServerLog,$ConsoleOut,$ConsoleError)
$SecretPath = Join-Path $RunDirectory 'rcon.secret'
$ServerProcess = $null; $Connection = $null; $RunFailure = $null; $Secret = $null
$OwnedHelpers = @{}; $Lock = $null; $Restored = $false; $SafeToRestore = $false
$Phase = 'Prepare'
$RestartProof = $null
$FinalNativeUnmapped = $false; $FinalQuitAcknowledged = $false; $FinalQuitOffset = 0
$Targets = @(
    @{Name='plugins'; Target=(Join-Path $Server 'carbon\plugins'); Moved=$false; Prepared=$false},
    @{Name='data'; Target=(Join-Path $Server 'carbon\data\CarbonLuau'); Moved=$false; Prepared=$false},
    @{Name='CarbonLuau.json'; Target=(Join-Path $Server 'carbon\configs\CarbonLuau.json'); Moved=$false; Prepared=$false},
    @{Name='server-identity'; Target=(Join-Path $Server ('server\carbonluau-persistence1b-' + $RunId)); Moved=$false; Prepared=$false}
)

function Read-ServerLog {
    if (!(Test-Path -LiteralPath $ServerLog)) { return '' }
    $Stream = [IO.File]::Open($ServerLog, 'Open', 'Read', 'ReadWrite')
    $Reader = New-Object IO.StreamReader($Stream)
    try { return $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}

function Get-OwnedHelpers {
    $Found = @(Get-Helpers | Where-Object {
        ($OwnedHelpers.ContainsKey([int]$_.ProcessId) -and $OwnedHelpers[[int]$_.ProcessId] -eq $_.CreationDate) -or
        (($ServerProcess -and $_.ParentProcessId -eq $ServerProcess.Id) -and
        (!$_.ExecutablePath -or $_.ExecutablePath.StartsWith($NativeDirectory + '\', [StringComparison]::OrdinalIgnoreCase)))
    })
    foreach ($Child in $Found) { $OwnedHelpers[[int]$Child.ProcessId] = $Child.CreationDate }
    return $Found
}

function Wait-Log([int]$Offset, [string]$Expected, [int]$Seconds = 45) {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text = Read-ServerLog
        if ($Text.Contains('[CarbonLuau:Persistence1BLive] FAIL')) { throw '[CarbonLuau:Persistence1BWorker] Live fixture failed; see redacted run logs' }
        if ($Text.Length -gt $Offset -and $Text.Substring($Offset).Contains($Expected)) { return }
        if ($ServerProcess.HasExited) { throw '[CarbonLuau:Persistence1BWorker] Owned server exited early' }
        Get-OwnedHelpers | Out-Null
        Start-Sleep -Milliseconds 250
    }
    throw "[CarbonLuau:Persistence1BWorker] Missing current-phase marker: $Expected"
}

function Send-Rcon([string]$Command) {
    [IO.File]::AppendAllText((Join-Path $RunDirectory 'rcon-stages.log'), ([DateTime]::UtcNow.ToString('o') + " send $Command`n"))
    try {
        $Reply = (& "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command $Command -Socket $Connection -KeepOpen) | ConvertFrom-Json
        [IO.File]::AppendAllText((Join-Path $RunDirectory 'rcon-stages.log'), ([DateTime]::UtcNow.ToString('o') + " received $Command`n"))
        return $Reply.Message
    } catch { throw "[CarbonLuau:Persistence1BWorker] RCON command '$Command' failed (details suppressed to protect secret)" }
}

function Wait-Ready {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $Status = Send-Rcon 'carbonluau.status'
        [IO.File]::WriteAllText((Join-Path $RunDirectory 'last-status.txt'), $Status)
        if ($Status.Contains('CarbonLuau: ready') -and $Status.Contains('[CarbonLuau:Persistence] ready=True')) {
            foreach ($Identity in $ExpectedIdentities) {
                if (!$Status.Contains($Identity)) { throw "[CarbonLuau:Persistence1BWorker] Artifact identity mismatch: $Identity" }
            }
            return $Status
        }
        Start-Sleep -Milliseconds 500
    } while ($Watch.Elapsed.TotalSeconds -lt 45)
    throw '[CarbonLuau:Persistence1BWorker] Production runtime/storage did not become ready'
}

function Set-RootScript([string]$Stage) {
    $Read = @'
S:GetAsync('Retained', function(Value, ErrorCode)
    assert(ErrorCode == nil and Value.Owner == 'Root' and Value.Flag == false and Value.Values[2] == 42,
        '[CarbonLuau:Persistence1BLive] FAIL root retained read')
    print('[CarbonLuau:Persistence1BLive] PASS __STAGE__')
end)
'@
    $Body = $Read
    if ($Stage -eq 'RootSeed') {
        $Body = @'
local Value = { Owner = 'Root', Flag = false, Values = { 'snapshot', 42 } }
local Returned = false
local Count = select('#', S:SetAsync('Temporary', Value, function(Saved, SaveError)
    assert(Returned and Saved == true and SaveError == nil, '[CarbonLuau:Persistence1BLive] FAIL later set callback')
    S:GetAsync('Temporary', function(Found, GetError)
        assert(GetError == nil and Found.Owner == 'Root' and Found.Flag == false and Found.Values[2] == 42,
            '[CarbonLuau:Persistence1BLive] FAIL snapshot get')
        S:RemoveAsync('Temporary', function(Removed, RemoveError)
            assert(Removed == true and RemoveError == nil, '[CarbonLuau:Persistence1BLive] FAIL remove existing')
            S:RemoveAsync('Temporary', function(Absent, AbsentError)
                assert(Absent == false and AbsentError == nil, '[CarbonLuau:Persistence1BLive] FAIL remove absent')
                S:GetAsync('Temporary', function(Missing, MissingError)
                    assert(Missing == nil and MissingError == nil, '[CarbonLuau:Persistence1BLive] FAIL get absent')
                    S:SetAsync('Retained', Found, function(Persisted, PersistError)
                        assert(Persisted == true and PersistError == nil, '[CarbonLuau:Persistence1BLive] FAIL retained set')
                        __READ__
                    end)
                end)
            end)
        end)
    end)
end))
assert(Count == 0, '[CarbonLuau:Persistence1BLive] FAIL nonyielding return')
Value.Owner = 'Changed'; Value.Values[2] = -1; Returned = true
'@
        $Body = $Body.Replace('__READ__', $Read)
    }
    $Source = "local S=game:GetService('DataStoreService'):GetDataStore('Persistence1BLive')`ntask.defer(function()`n" +
        $Body.Replace('__STAGE__', $Stage) + "`nend)`nreturn true`n"
    [IO.File]::WriteAllText((Join-Path $Server 'carbon\data\CarbonLuau\scripts\init.luau'), $Source, (New-Object Text.UTF8Encoding($false)))
}

function Wait-WorkersStopped {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $Remaining = @(Get-OwnedHelpers)
        if (!$Remaining.Count) { return }
        Start-Sleep -Milliseconds 100
    } while ($Watch.Elapsed.TotalSeconds -lt 15)
    throw '[CarbonLuau:Persistence1BWorker] Owned helper remained after plugin teardown'
}

function Start-OwnedServer {
    if ($NamespaceRestartSupplement -and
        ((Get-FileHash -LiteralPath $HostAssemblyPath -Algorithm SHA256).Hash -ne $HostAssemblyHash -or
         (Get-FileHash -LiteralPath $HostSystemPath -Algorithm SHA256).Hash -ne $HostSystemHash)) {
        throw '[CarbonLuau:Persistence1BWorker] Host quit-path assemblies changed after preflight'
    }
    if (!('Persistence1BErrorDialogs' -as [type])) {
        Add-Type 'using System.Runtime.InteropServices; public static class Persistence1BErrorDialogs { [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode); }'
    }
    $PreviousErrorMode = [Persistence1BErrorDialogs]::SetErrorMode(0x8003)
    try {
        $Arguments = @('-batchmode','-nographics','-logfile',('"' + $ServerLog + '"'),
            '+server.ip','127.0.0.1','+server.port',($Port-1),'+server.queryport',($Port+1),
            '+server.identity',('carbonluau-persistence1b-' + $RunId),'+server.hostname','CarbonLuauPersistence1B',
            '+server.worldsize','1000','+server.seed','24682','+server.maxplayers','1','+server.saveinterval','600',
            '+rcon.ip','127.0.0.1','+rcon.port',$Port,'+rcon.web','1','+rcon.password',$Secret)
        $StartedProcess = Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $Server -PassThru `
            -WindowStyle Hidden -RedirectStandardOutput $ConsoleOut -RedirectStandardError $ConsoleError
        # Windows PowerShell 5.1 can otherwise lose ExitCode after the process exits.
        $null = $StartedProcess.Handle
        return $StartedProcess
    } finally { [Persistence1BErrorDialogs]::SetErrorMode($PreviousErrorMode) | Out-Null }
}

function Save-OperationCounters([string]$Name, [int]$Gets, [int]$Sets, [int]$Removes) {
    $Status = Send-Rcon 'carbonluau.status'
    [IO.File]::WriteAllText((Join-Path $RunDirectory ($Name + '-status.txt')), $Status)
    $Expected = @{
        pending=0; starts=1; retries=0; sent=($Gets+$Sets+$Removes)
        completed_get=$Gets; completed_set=$Sets; completed_remove=$Removes
        queue_rejected=0; rate_rejected=0; quota_rejected=0; expired=0
        backend_failures=0; corruptions=0; discarded=0
    }
    foreach ($Entry in $Expected.GetEnumerator()) {
        if ($Status -notmatch ('(?:\] |; )' + $Entry.Key + '=' + $Entry.Value + '(?:;|\r?\n|$)')) {
            throw "[CarbonLuau:Persistence1BWorker] $Name counter mismatch: $($Entry.Key)"
        }
    }
    if (!$Status.Contains('ready=True') -or !$Status.Contains('failure=None')) {
        throw "[CarbonLuau:Persistence1BWorker] $Name storage not healthy"
    }
}

try {
    # Lock only this installation; no process-name-wide stop or host mutation.
    $Lock = [IO.File]::Open((Join-Path $Server '.persistence1b-owner.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    # Recheck after acquiring ownership; the read-only preflight may have raced
    # another harness or an operator launch before this lock was obtained.
    $NowServers = @(Get-CimInstance Win32_Process -Filter "Name='RustDedicated.exe'" | Where-Object { $_.ExecutablePath -eq $Executable -or !$_.ExecutablePath })
    $NowHelpers = @(Get-Helpers | Where-Object { !$_.ExecutablePath -or $_.ExecutablePath.StartsWith($NativeDirectory + '\', [StringComparison]::OrdinalIgnoreCase) })
    if ($NowServers.Count -or $NowHelpers.Count) { throw '[CarbonLuau:Persistence1BWorker] Installation acquired by another process; no deployment performed' }
    New-Item -ItemType Directory -Path $BackupDirectory,$RetainedDirectory -Force | Out-Null
    $Evidence = foreach ($InputPath in @($Package,$NativeLibrary,$CompilerWorker,$StorageWorker,$Fixture,$ReleaseMetadata,$PSCommandPath,(Join-Path $PSScriptRoot 'Rcon.ps1'))) {
        [pscustomobject]@{Path=[IO.Path]::GetFullPath($InputPath); SHA256=(Get-FileHash -LiteralPath $InputPath -Algorithm SHA256).Hash}
    }
    $Evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'input-sha256.json')
    if ($HostQuitEvidence) {
        $HostQuitEvidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'host-quit-identity.json')
    }
    Copy-Item -LiteralPath $ReleaseMetadata -Destination (Join-Path $RunDirectory 'release.json')
    $Manifest = Join-Path $Server 'steamapps\appmanifest_258550.acf'
    $BuildId = 'unknown'
    if ((Test-Path -LiteralPath $Manifest) -and ((Get-Content -LiteralPath $Manifest -Raw) -match '"buildid"\s+"(\d+)"')) { $BuildId = $Matches[1] }
    [pscustomobject]@{RustSteamBuildId=$BuildId; Server=$Server; RuntimeVersionEvidence='server.log';
        ClientQualification='not performed'; Scope='representative 1B only'} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'environment.json')
    foreach ($Value in $Targets) {
        $Value.Target = Assert-SandboxPath $Value.Target
        $Value.Backup = Assert-SandboxPath (Join-Path $BackupDirectory $Value.Name)
        if (Test-Path -LiteralPath $Value.Target) {
            Move-Item -LiteralPath $Value.Target -Destination $Value.Backup
            $Value.Moved = $true
        }
        $Value.Prepared = $true
    }
    $PluginDirectory = $Targets[0].Target
    New-Item -ItemType Directory -Force -Path $PluginDirectory,$NativeDirectory,
        (Join-Path $Server 'carbon\data\CarbonLuau\scripts\modules'),(Split-Path $Targets[2].Target) | Out-Null
    Copy-Item -LiteralPath $Package -Destination (Join-Path $PluginDirectory 'CarbonLuau.cszip')
    Copy-Item -LiteralPath $Fixture -Destination (Join-Path $PluginDirectory 'CarbonLuauPersistence1BFixture.cs')
    Copy-Item -LiteralPath $NativeLibrary -Destination (Join-Path $NativeDirectory 'carbonluau_native.dll')
    Copy-Item -LiteralPath $CompilerWorker -Destination (Join-Path $NativeDirectory 'carbonluau_compiler.exe')
    Copy-Item -LiteralPath $StorageWorker -Destination (Join-Path $NativeDirectory 'carbonluau_storage.exe')
    # Production default budgets. Arm scripts only after storage Ready is observed.
    [IO.File]::WriteAllText($Targets[2].Target, '{"Enabled":true,"MaxVmMemoryMiB":64,"MaxCallbackMilliseconds":3,"ScriptRoot":"scripts","EntryScript":"init.luau","ModuleRoot":"modules","FrameDrainBudgetMilliseconds":5,"MaxQueuedCallbacks":4096}')
    [IO.File]::WriteAllText((Join-Path $Server 'carbon\data\CarbonLuau\scripts\init.luau'), 'return true')
    $Secret = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($SecretPath, $Secret)
    $ServerProcess = Start-OwnedServer
    $Phase = 'ServerStartup'
    Wait-Log 0 'Server startup complete' 600
    try { $Connection = & "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen }
    catch { throw '[CarbonLuau:Persistence1BWorker] RCON connect failed (details suppressed)' }
    $Phase = 'InitialReadiness'
    $InitialStatus = Wait-Ready
    $InitialStatus | Set-Content -LiteralPath (Join-Path $RunDirectory 'initial-status.txt')
    if (@(Get-OwnedHelpers | Where-Object Name -eq 'carbonluau_storage.exe').Count -ne 1) {
        throw '[CarbonLuau:Persistence1BWorker] Expected one owned production storage worker'
    }
    $Offset = (Read-ServerLog).Length
    $Phase = 'RootSeed'
    Set-RootScript 'RootSeed'
    if (!(Send-Rcon 'carbonluau.reload').Contains('reload OK')) { throw '[CarbonLuau:Persistence1BWorker] Root seed reload failed' }
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS RootSeed'
    if ($NamespaceRestartSupplement) {
        $Phase = 'SeedAddonPair'
        $Offset = (Read-ServerLog).Length
        Send-Rcon 'clpersistence1b.abseed' | Out-Null
        foreach ($Marker in @('AddonASeed','AddonBSeed','Active AddonASeed','Active AddonBSeed')) {
            Wait-Log $Offset ('[CarbonLuau:Persistence1BLive] PASS ' + $Marker)
        }
        $Phase = 'RootReadBeforeServerRestart'
        $Offset = (Read-ServerLog).Length
        Set-RootScript 'RootReadBeforeServerRestart'
        Send-Rcon 'carbonluau.reload' | Out-Null
        Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS RootReadBeforeServerRestart'
        Save-OperationCounters 'before-server-restart' 8 4 2
        Write-Output '[CarbonLuau:Persistence1BWorker] PASS Root/A/B seeded in private namespaces; counters get/set/remove=8/4/2'

        $Phase = 'CleanServerStop'
        $FirstPid = $ServerProcess.Id
        $FirstStarted = $ServerProcess.StartTime.ToUniversalTime().ToString('o')
        # Next process starts idle; no async operation races worker startup.
        [IO.File]::WriteAllText((Join-Path $Server 'carbon\data\CarbonLuau\scripts\init.luau'), 'return true')
        $QuitOffset = (Read-ServerLog).Length
        Send-Rcon 'quit' | Out-Null
        $Connection.Dispose(); $Connection = $null
        if (!$ServerProcess.WaitForExit(30000)) { throw '[CarbonLuau:Persistence1BWorker] Clean restart requires normal Rust exit' }
        Wait-WorkersStopped
        $FirstExitCode = $ServerProcess.ExitCode
        $QuitText = (Read-ServerLog).Substring($QuitOffset)
        $ShutdownMarkers = @('Native library unloaded successfully','Saving complete','Config Saved')
        $ShutdownObserved = $true
        foreach ($Marker in $ShutdownMarkers) { if (!$QuitText.Contains($Marker)) { $ShutdownObserved = $false } }
        [pscustomobject]@{Pid=$FirstPid; StartedUtc=$FirstStarted; HasExited=$ServerProcess.HasExited;
            ExitCode=$FirstExitCode; ExitCodeAvailable=($null -ne $FirstExitCode);
            ExpectedQuitExitCode=$ExpectedQuitExitCode; QuitAcknowledged=$true; ShutdownMarkersObserved=$ShutdownObserved;
            ObservedUtc=[DateTime]::UtcNow.ToString('o'); HelpersAfterStop=0} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'first-server-exit.json')
        if ($null -eq $FirstExitCode) { throw '[CarbonLuau:Persistence1BWorker] First Rust process exit code unavailable' }
        if ($FirstExitCode -ne $ExpectedQuitExitCode) { throw "[CarbonLuau:Persistence1BWorker] First Rust exit code $FirstExitCode differs from qualified quit code $ExpectedQuitExitCode" }
        if (!$ShutdownObserved) { throw '[CarbonLuau:Persistence1BWorker] First Rust quit missing native-unload/save/config markers' }
        $PersistencePath = Join-Path $Server 'carbon\data\CarbonLuau\persistence'
        $DatabaseHash = (Get-FileHash -LiteralPath (Join-Path $PersistencePath 'store.sqlite3') -Algorithm SHA256).Hash
        $RestartProof = [ordered]@{FirstPid=$FirstPid; FirstStartedUtc=$FirstStarted; FirstExitCode=$FirstExitCode;
            FirstExitedUtc=[DateTime]::UtcNow.ToString('o'); HelpersAfterStop=0; ExpectedQuitExitCode=$ExpectedQuitExitCode;
            FirstQuitAcknowledged=$true; FirstShutdownMarkersObserved=$ShutdownObserved;
            DataDirectory=$PersistencePath; DatabaseSha256AfterStop=$DatabaseHash;
            ServerIdentity=('carbonluau-persistence1b-' + $RunId)}
        $RestartProof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'restart-proof.json')
        Write-Output '[CarbonLuau:Persistence1BWorker] PASS first Rust normal host-command shutdown and zero workers; persistence directory retained'

        $Phase = 'SecondServerStartup'
        $ServerLog = Join-Path $RunDirectory 'server-restart.log'
        $ConsoleOut = Join-Path $RunDirectory 'console-restart.log'
        $ConsoleError = Join-Path $RunDirectory 'console-restart-error.log'
        $LogPaths += @($ServerLog,$ConsoleOut,$ConsoleError)
        $ServerProcess = Start-OwnedServer
        $RestartProof.SecondPid = $ServerProcess.Id
        $RestartProof.SecondStartedUtc = $ServerProcess.StartTime.ToUniversalTime().ToString('o')
        $RestartProof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'restart-proof.json')
        if ($RestartProof.SecondStartedUtc -eq $FirstStarted) { throw '[CarbonLuau:Persistence1BWorker] Restart did not create a fresh process' }
        Wait-Log 0 'Server startup complete' 600
        try { $Connection = & "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen }
        catch { throw '[CarbonLuau:Persistence1BWorker] Restart RCON connect failed (details suppressed)' }
        $RestartStatus = Wait-Ready
        [IO.File]::WriteAllText((Join-Path $RunDirectory 'server-restarted-ready-status.txt'), $RestartStatus)
        Save-OperationCounters 'server-restarted-idle' 0 0 0
        $Phase = 'RootReadServerRestart'
        $Offset = (Read-ServerLog).Length
        Set-RootScript 'RootReadServerRestart'
        Send-Rcon 'carbonluau.reload' | Out-Null
        Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS RootReadServerRestart'
        $Phase = 'AddonPairReadServerRestart'
        $Offset = (Read-ServerLog).Length
        Send-Rcon 'clpersistence1b.abrestartread' | Out-Null
        foreach ($Marker in @('AddonAReadServerRestart','AddonBReadServerRestart',
                'Active AddonAReadServerRestart','Active AddonBReadServerRestart')) {
            Wait-Log $Offset ('[CarbonLuau:Persistence1BLive] PASS ' + $Marker)
        }
        Save-OperationCounters 'after-server-restart' 3 0 0
        Write-Output '[CarbonLuau:Persistence1BWorker] PASS Root/A/B same store/key isolation survives actual Rust restart; fresh facades and reads only; counters get/set/remove=3/0/0'
        $Phase = 'SupplementFinalUnload'
        $Offset = (Read-ServerLog).Length
        Send-Rcon 'c.unload CarbonLuau' | Out-Null
        Wait-Log $Offset 'Native library unloaded successfully'
        Wait-WorkersStopped
        $ServerProcess.Refresh()
        if ($ServerProcess.Modules.ModuleName -contains 'carbonluau_native.dll') { throw '[CarbonLuau:Persistence1BWorker] Native module still mapped after supplement' }
        $FinalNativeUnmapped = $true
    } else {
    $BeforeReload = Send-Rcon 'carbonluau.status'
    $Offset = (Read-ServerLog).Length
    $Phase = 'RootReadReload'
    Set-RootScript 'RootReadReload'
    Send-Rcon 'carbonluau.reload' | Out-Null
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS RootReadReload'
    $AfterReload = Send-Rcon 'carbonluau.status'
    if ($BeforeReload -notmatch 'VM generation: (\d+); root domain: (\d+)') { throw 'Missing pre-reload lifetime evidence' }
    $PreviousVm = $Matches[1]; $PreviousDomain = $Matches[2]
    if ($AfterReload -notmatch 'VM generation: (\d+); root domain: (\d+)' -or $Matches[1] -ne $PreviousVm -or $Matches[2] -eq $PreviousDomain) {
        throw '[CarbonLuau:Persistence1BWorker] Healthy root replacement lifetime mismatch'
    }
    foreach ($Step in @(@{Command='clpersistence1b.seed'; Marker='AddonSeed'; Active='AddonSeed'},
            @{Command='clpersistence1b.replace'; Marker='AddonReadReplacement'; Active='Replacement'})) {
        $Offset = (Read-ServerLog).Length
        $Phase = $Step.Marker
        Send-Rcon $Step.Command | Out-Null
        Wait-Log $Offset ('[CarbonLuau:Persistence1BLive] PASS ' + $Step.Marker)
        Wait-Log $Offset ('[CarbonLuau:Persistence1BLive] PASS Active ' + $Step.Active)
    }
    # Read again after addon writes to prove root/addon separation in both directions.
    $Offset = (Read-ServerLog).Length
    Send-Rcon 'carbonluau.reload' | Out-Null
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS RootReadReload'
    $Offset = (Read-ServerLog).Length
    $Phase = 'FirstHostUnload'
    Send-Rcon 'c.unload CarbonLuau' | Out-Null
    Wait-Log $Offset 'Native library unloaded successfully'
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS ObservedHostUnload'
    Wait-WorkersStopped
    $ServerProcess.Refresh()
    if ($ServerProcess.Modules.ModuleName -contains 'carbonluau_native.dll') { throw '[CarbonLuau:Persistence1BWorker] Native module still mapped' }
    if (!(Send-Rcon 'status').Contains('hostname: CarbonLuauPersistence1B')) { throw '[CarbonLuau:Persistence1BWorker] Host unresponsive after unload' }
    Write-Output '[CarbonLuau:Persistence1BWorker] PASS native unmap and owned compiler/storage worker teardown'
    # Idle entry avoids racing asynchronous storage startup after host reload.
    [IO.File]::WriteAllText((Join-Path $Server 'carbon\data\CarbonLuau\scripts\init.luau'), 'return true')
    $Phase = 'HostReloadCommand'
    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.load CarbonLuau' | Out-Null
    $Phase = 'HostReloadReadiness'
    # c.load acknowledges the compilation request before the plugin's commands
    # exist. Querying status in that gap can receive no correlated RCON reply.
    Wait-Log $Offset '[CarbonLuau:Runtime] Ready; generation='
    $ReloadStatus = Wait-Ready
    $ReloadStatus | Set-Content -LiteralPath (Join-Path $RunDirectory 'reloaded-status.txt')
    $Offset = (Read-ServerLog).Length
    $Phase = 'RootReadHostReload'
    Set-RootScript 'RootReadHostReload'
    Send-Rcon 'carbonluau.reload' | Out-Null
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS RootReadHostReload'
    $Offset = (Read-ServerLog).Length
    $Phase = 'AddonReadHostReload'
    Send-Rcon 'clpersistence1b.reregister' | Out-Null
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS StaleAddonToken'
    Wait-Log $Offset '[CarbonLuau:Persistence1BLive] PASS AddonReadHostReload'
    $Offset = (Read-ServerLog).Length
    $Phase = 'FinalHostUnload'
    Send-Rcon 'c.unload CarbonLuau' | Out-Null
    Wait-Log $Offset 'Native library unloaded successfully'
    Wait-WorkersStopped
    }
} catch {
    $RunFailure = if ($Secret) { $_.Exception.Message.Replace($Secret, '[REDACTED]') } else { $_.Exception.Message }
    if (Test-Path -LiteralPath $RunDirectory) {
        [pscustomobject]@{Phase=$Phase;Message=$RunFailure} | ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $RunDirectory 'primary-failure.json')
    }
    Write-Output "[CarbonLuau:Persistence1BWorker] FAIL phase=$Phase; $RunFailure"
} finally {
    try {
        if ($Connection) {
            if (!$ServerProcess.HasExited -and $Connection.State -ne [Net.WebSockets.WebSocketState]::Open) {
                $Connection.Dispose()
                try { $Connection = & "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen }
                catch { $Connection = $null }
            }
        }
        if ($Connection) {
            try {
                $FinalQuitOffset = (Read-ServerLog).Length
                Send-Rcon 'quit' | Out-Null
                $FinalQuitAcknowledged = $true
            } catch { }
            $Connection.Dispose()
        }
        if ($ServerProcess -and !$ServerProcess.HasExited) {
            if (!$ServerProcess.WaitForExit(30000)) {
                $ServerProcess.Kill()
                $ServerProcess.WaitForExit(15000) | Out-Null
                $RunFailure = "$RunFailure; [CarbonLuau:Persistence1BWorker] Server required forced termination; not a clean PASS"
            }
        }
        $SafeToRestore = !$ServerProcess -or $ServerProcess.HasExited
        if ($SafeToRestore -and $ServerProcess) {
            try { Wait-WorkersStopped } catch { $SafeToRestore = $false; $RunFailure = $_.Exception.Message }
            # Verify recorded PID plus creation time, guarding PID reuse. Never stop
            # unrelated workers or restore a database while a worker can hold it.
            foreach ($Child in @(Get-Helpers)) {
                if (($OwnedHelpers.ContainsKey([int]$Child.ProcessId) -and $OwnedHelpers[[int]$Child.ProcessId] -eq $Child.CreationDate) -or
                    ($Child.ExecutablePath -and $Child.ExecutablePath.StartsWith($NativeDirectory + '\', [StringComparison]::OrdinalIgnoreCase))) {
                    $SafeToRestore = $false
                }
            }
        }
        if ($SafeToRestore) {
            if ($NamespaceRestartSupplement -and $RestartProof -and $RestartProof.Contains('SecondPid') -and
                $ServerProcess -and $ServerProcess.Id -eq $RestartProof.SecondPid) {
                $RestartProof.SecondExitCode = $ServerProcess.ExitCode
                $RestartProof.SecondExitedUtc = [DateTime]::UtcNow.ToString('o')
                $RestartProof.FinalHelpersAfterStop = 0
                $FinalQuitText = (Read-ServerLog).Substring($FinalQuitOffset)
                $RestartProof.SecondQuitAcknowledged = $FinalQuitAcknowledged
                $RestartProof.SecondShutdownMarkersObserved = ($FinalQuitText.Contains('Saving complete') -and $FinalQuitText.Contains('Config Saved'))
                $RestartProof.FinalNativeUnmapped = $FinalNativeUnmapped
                $RestartProof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDirectory 'restart-proof.json')
                if ($null -eq $ServerProcess.ExitCode) { $RunFailure = "$RunFailure; second Rust process exit code unavailable" }
                elseif ($ServerProcess.ExitCode -ne $ExpectedQuitExitCode) { $RunFailure = "$RunFailure; second Rust exit code differs from qualified quit code: $($ServerProcess.ExitCode)" }
                if (!$FinalQuitAcknowledged -or !$RestartProof.SecondShutdownMarkersObserved -or !$FinalNativeUnmapped) {
                    $RunFailure = "$RunFailure; second Rust quit missing acknowledgement/save/config/native-unmap evidence"
                }
            }
            foreach ($Value in $Targets) {
                if (!$Value.Prepared) { continue }
                if (Test-Path -LiteralPath $Value.Target) {
                    $Target = Assert-SandboxPath $Value.Target
                    $Retained = Assert-SandboxPath (Join-Path $RetainedDirectory $Value.Name)
                    Move-Item -LiteralPath $Target -Destination $Retained
                }
                if ($Value.Moved) { Move-Item -LiteralPath $Value.Backup -Destination $Value.Target }
            }
            $Restored = $true
        } else {
            $RunFailure = '[CarbonLuau:Persistence1BWorker] Processes still active; backups preserved and restore deferred until confirmed stop'
        }
    } catch {
        $RunFailure = '[CarbonLuau:Persistence1BWorker] Cleanup incomplete; preserved backup/fixture trees require inspection before another run'
    } finally {
        try {
            if (Test-Path -LiteralPath $SecretPath) { Remove-Item -LiteralPath $SecretPath -Force }
            # Unity can echo its command line; never emit raw log tails or RCON errors.
            if ($SafeToRestore -and $Secret) {
                foreach ($Log in $LogPaths) {
                    if (Test-Path -LiteralPath $Log) {
                        $Text = [IO.File]::ReadAllText($Log)
                        [IO.File]::WriteAllText($Log, $Text.Replace($Secret, '[REDACTED]'))
                    }
                }
            }
        } catch {
            $RunFailure = '[CarbonLuau:Persistence1BWorker] Evidence redaction/secret cleanup failed; do not publish raw run logs'
        } finally { if ($Lock) { $Lock.Dispose() } }
    }
}
if ($RunFailure) { throw $RunFailure }
if (!$Restored) { throw '[CarbonLuau:Persistence1BWorker] Restoration was not confirmed' }
Write-Output "[CarbonLuau:Persistence1BWorker] REPRESENTATIVE LIVE PASS; legacy trees restored; evidence=$RunDirectory; no authenticated-client/1C claim"
