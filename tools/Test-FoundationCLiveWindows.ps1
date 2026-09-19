param(
    [string]$Server = 'C:\Sandbox\Codex\Builds\CarbonLuauPhase5Windows\server-win',
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][string]$NativeLibrary,
    [Parameter(Mandatory)][string]$DependencyFixture,
    [Parameter(Mandatory)][string]$ConsumerFixture,
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [int]$Port = 28426
)
$ErrorActionPreference = 'Stop'
$RunId = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$ServerLog = Join-Path $ArtifactDirectory "foundation-c-live-$RunId.log"
$ConsoleOut = Join-Path $ArtifactDirectory "foundation-c-console-$RunId.log"
$ConsoleError = Join-Path $ArtifactDirectory "foundation-c-console-error-$RunId.log"
$SecretPath = Join-Path $ArtifactDirectory "foundation-c-rcon-$RunId.secret"
$Connection = $null
$ServerProcess = $null
$RunFailure = $null

function Read-ServerLog {
    if (!(Test-Path -LiteralPath $ServerLog)) { return '' }
    $Stream = [IO.File]::Open($ServerLog, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $Reader = New-Object IO.StreamReader($Stream)
    try { return $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}

function Wait-ServerLog([int]$Offset, [string]$Expected, [int]$Seconds = 180) {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text = (Read-ServerLog).Substring($Offset)
        if ($Text.Contains('[CarbonLuau:AddonDependencyLive] FAIL') -or $Text.Contains('[CarbonLuau:AddonConsumerLive] FAIL')) {
            throw "Live dependency fixture failed: $Text"
        }
        if ($Text.Contains($Expected)) { return }
        if ($ServerProcess -and $ServerProcess.HasExited) { throw "Server exited before: $Expected" }
        Start-Sleep -Milliseconds 250
    }
    throw "Missing current-run log: $Expected"
}

function Send-Rcon([string]$Command, [string]$Expected = '') {
    $Reply = (& "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command $Command `
        -ExpectedMessage $Expected -Socket $Connection -KeepOpen) | ConvertFrom-Json
    return $Reply.Message
}

function Read-ConsumerStatus {
    $Message = Send-Rcon 'clfoundationc.consumerstatus'
    if ($Message -notmatch 'required=([^;]+);requiredDomain=([^;]*);optional=([^;]+);optionalDomain=([^;]*)') {
        throw "Malformed consumer status: $Message"
    }
    return @{Required=$Matches[1]; RequiredDomain=$Matches[2]; Optional=$Matches[3]; OptionalDomain=$Matches[4]}
}

function Wait-ConsumerState([string]$Required, [string]$PreviousRequiredDomain, [string]$OptionalDomain, [int]$Seconds = 180) {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text = Read-ServerLog
        if ($Text.Contains('[CarbonLuau:AddonDependencyLive] FAIL') -or $Text.Contains('[CarbonLuau:AddonConsumerLive] FAIL')) {
            throw 'Live dependency fixture reported failure while polling consumer state'
        }
        $Status = Read-ConsumerStatus
        $RequiredDomainMatches = if ($Required -eq 'Blocked') { $Status.RequiredDomain -eq '' } else { $Status.RequiredDomain -ne '' -and $Status.RequiredDomain -ne $PreviousRequiredDomain }
        if ($Status.Required -eq $Required -and $RequiredDomainMatches -and
            $Status.Optional -eq 'Active' -and $Status.OptionalDomain -eq $OptionalDomain) { return $Status }
        Start-Sleep -Milliseconds 250
    }
    throw "Consumer did not reach required=$Required with stable optional domain"
}

if (!(Test-Path -LiteralPath (Join-Path $Server 'RustDedicated.exe'))) { throw "Missing isolated server: $Server" }
$NativeDirectory = Join-Path $Server 'carbon\data\CarbonLuau\native\win-x64'
$PluginDirectory = Join-Path $Server 'carbon\plugins'
New-Item -ItemType Directory -Force $NativeDirectory,$PluginDirectory | Out-Null
$Deployments = @(
    @{Source=$Package; Target=(Join-Path $PluginDirectory 'CarbonLuau.cszip')},
    @{Source=$NativeLibrary; Target=(Join-Path $NativeDirectory 'carbonluau_native.dll')},
    @{Source=$DependencyFixture; Target=(Join-Path $PluginDirectory 'CarbonLuauAddonDependencyProvider.cs')},
    @{Source=$ConsumerFixture; Target=(Join-Path $PluginDirectory 'CarbonLuauAddonDependencyConsumer.cs')}
)
$BackupDirectory = Join-Path $ArtifactDirectory "backup-$RunId"
New-Item -ItemType Directory -Force $BackupDirectory | Out-Null
for ($Index = 0; $Index -lt $Deployments.Count; ++$Index) {
    $Value = $Deployments[$Index]; $Value.Had = Test-Path -LiteralPath $Value.Target
    $Value.Backup = Join-Path $BackupDirectory ("item-" + $Index)
    if ($Value.Had) { Copy-Item -LiteralPath $Value.Target -Destination $Value.Backup }
}

try {
    foreach ($Value in $Deployments) { Copy-Item -LiteralPath $Value.Source -Destination $Value.Target -Force }
    Add-Type 'using System.Runtime.InteropServices; public static class FoundationCErrorDialogs { [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode); }'
    [FoundationCErrorDialogs]::SetErrorMode(0x8003) | Out-Null
    $Secret = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($SecretPath, $Secret)
    $Arguments = @(
        '-batchmode', '-nographics', '-logfile', $ServerLog,
        '+server.ip', '127.0.0.1', '+server.port', '28425', '+server.queryport', '28427',
        '+server.identity', 'carbonluau-foundation-c', '+server.hostname', 'CarbonLuauFoundationC',
        '+server.worldsize', '1000', '+server.seed', '24681', '+server.maxplayers', '1', '+server.saveinterval', '600',
        '+rcon.ip', '127.0.0.1', '+rcon.port', $Port.ToString(), '+rcon.web', '1', '+rcon.password', $Secret
    )
    $ServerProcess = Start-Process -FilePath (Join-Path $Server 'RustDedicated.exe') -ArgumentList $Arguments `
        -WorkingDirectory $Server -PassThru -WindowStyle Hidden -RedirectStandardOutput $ConsoleOut -RedirectStandardError $ConsoleError
    Wait-ServerLog 0 'Server startup complete' 600
    $Connection = & "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen
    Wait-ServerLog 0 '[CarbonLuau:AddonDependencyLive] PASS Active'
    Wait-ServerLog 0 '[CarbonLuau:AddonConsumerLive] PASS RequiredActive'
    Wait-ServerLog 0 '[CarbonLuau:AddonConsumerLive] PASS OptionalActive'
    $InitialConsumer = Read-ConsumerStatus
    if ($InitialConsumer.Required -ne 'Active' -or $InitialConsumer.Optional -ne 'Active' -or
        $InitialConsumer.RequiredDomain -eq '' -or $InitialConsumer.OptionalDomain -eq '') { throw 'Initial consumer domains are unavailable' }
    Write-Output '[CarbonLuau:FoundationCWorker] PASS initial dependency ordering and activation'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.unload CarbonLuauAddonDependencyProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] provider Unload reached'
    Wait-ServerLog $Offset '[CarbonLuau:Addons] Retired 1 registration(s) for unloaded provider CarbonLuauAddonDependencyProvider.'
    $LostConsumer = Wait-ConsumerState 'Blocked' $InitialConsumer.RequiredDomain $InitialConsumer.OptionalDomain
    Write-Output '[CarbonLuau:FoundationCWorker] PASS provider unload propagated required loss and preserved optional consumer'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.load CarbonLuauAddonDependencyProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] PASS Active'
    $RestoredConsumer = Wait-ConsumerState 'Active' $InitialConsumer.RequiredDomain $InitialConsumer.OptionalDomain
    Write-Output '[CarbonLuau:FoundationCWorker] PASS provider reload restored required consumer with fresh lifetime'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'clfoundationc.replace' 'replacement queued' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] PASS Replaced'
    $ReplacedConsumer = Wait-ConsumerState 'Active' $RestoredConsumer.RequiredDomain $InitialConsumer.OptionalDomain
    Write-Output '[CarbonLuau:FoundationCWorker] PASS dependency replacement reinitialized required consumer without optional rebind'
    Write-Output '[CarbonLuau:FoundationCWorker] LIVE CARBON PASS'
} catch {
    $RunFailure = $_
} finally {
    if ($Connection) {
        try { Send-Rcon 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:FoundationCWorker] RCON closed during clean shutdown' }
        $Connection.Dispose()
    }
    if ($ServerProcess -and !$ServerProcess.WaitForExit(120000)) {
        Stop-Process -Id $ServerProcess.Id -Force
        $ServerProcess.WaitForExit(15000) | Out-Null
        if (!$RunFailure) { $RunFailure = 'Live server required forced termination' }
    }
    if (Test-Path -LiteralPath $SecretPath) { Remove-Item -LiteralPath $SecretPath -Force }
    foreach ($Value in $Deployments) {
        if ($Value.Had) { Copy-Item -LiteralPath $Value.Backup -Destination $Value.Target -Force }
        elseif (Test-Path -LiteralPath $Value.Target) { Remove-Item -LiteralPath $Value.Target -Force }
    }
}
if ($RunFailure) { throw $RunFailure }
