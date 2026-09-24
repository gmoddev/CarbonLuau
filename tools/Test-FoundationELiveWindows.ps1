param(
    [string]$Server = 'C:\Sandbox\Codex\Builds\CarbonLuauPhase5Windows\server-win',
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][string]$NativeLibrary,
    [Parameter(Mandatory)][string]$CompilerWorker,
    [Parameter(Mandatory)][string]$ProviderFixture,
    [Parameter(Mandatory)][string]$DependencyFixture,
    [Parameter(Mandatory)][string]$ConsumerFixture,
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [int]$Port = 28436
)
$ErrorActionPreference = 'Stop'
$Release = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../release.json') | ConvertFrom-Json
$RunId = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$ServerLog = Join-Path $ArtifactDirectory "foundation-e-live-$RunId.log"
$ConsoleOut = Join-Path $ArtifactDirectory "foundation-e-console-$RunId.log"
$ConsoleError = Join-Path $ArtifactDirectory "foundation-e-console-error-$RunId.log"
$SecretPath = Join-Path $ArtifactDirectory "foundation-e-rcon-$RunId.secret"
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
        $Text = Read-ServerLog
        $Current = if ($Text.Length -gt $Offset) { $Text.Substring($Offset) } else { '' }
        foreach ($Marker in @('[CarbonLuau:FoundationELive] FAIL', '[CarbonLuau:AddonLive] FAIL',
                '[CarbonLuau:AddonDependencyLive] FAIL', '[CarbonLuau:AddonConsumerLive] FAIL')) {
            if ($Current.Contains($Marker)) { throw "Live Foundation E fixture failed: $Current" }
        }
        if ($Current.Contains($Expected)) { return }
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
        $Status = Read-ConsumerStatus
        $DomainMatches = if ($Required -eq 'Blocked') { $Status.RequiredDomain -eq '' } else {
            $Status.RequiredDomain -ne '' -and $Status.RequiredDomain -ne $PreviousRequiredDomain
        }
        if ($Status.Required -eq $Required -and $DomainMatches -and
            $Status.Optional -eq 'Active' -and $Status.OptionalDomain -eq $OptionalDomain) { return $Status }
        Start-Sleep -Milliseconds 250
    }
    throw "Consumer did not reach required=$Required with the expected binding lifetimes"
}

if (!(Test-Path -LiteralPath (Join-Path $Server 'RustDedicated.exe'))) { throw "Missing isolated server: $Server" }
$NativeDirectory = Join-Path $Server 'carbon\data\CarbonLuau\native\win-x64'
$PluginDirectory = Join-Path $Server 'carbon\plugins'
New-Item -ItemType Directory -Force $NativeDirectory,$PluginDirectory | Out-Null
$Deployments = @(
    @{Source=$Package; Target=(Join-Path $PluginDirectory 'CarbonLuau.cszip')},
    @{Source=$NativeLibrary; Target=(Join-Path $NativeDirectory 'carbonluau_native.dll')},
    @{Source=$CompilerWorker; Target=(Join-Path $NativeDirectory 'carbonluau_compiler.exe')},
    @{Source=$ProviderFixture; Target=(Join-Path $PluginDirectory 'CarbonLuauAddonProvider.cs')},
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
    Add-Type 'using System.Runtime.InteropServices; public static class FoundationEErrorDialogs { [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode); }'
    [FoundationEErrorDialogs]::SetErrorMode(0x8003) | Out-Null
    $Secret = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($SecretPath, $Secret)
    $Arguments = @(
        '-batchmode', '-nographics', '-logfile', $ServerLog,
        '+server.ip', '127.0.0.1', '+server.port', '28435', '+server.queryport', '28437',
        '+server.identity', 'carbonluau-foundation-e', '+server.hostname', 'CarbonLuauFoundationE',
        '+server.worldsize', '1000', '+server.seed', '24682', '+server.maxplayers', '1', '+server.saveinterval', '600',
        '+rcon.ip', '127.0.0.1', '+rcon.port', $Port.ToString(), '+rcon.web', '1', '+rcon.password', $Secret
    )
    $ServerProcess = Start-Process -FilePath (Join-Path $Server 'RustDedicated.exe') -ArgumentList $Arguments `
        -WorkingDirectory $Server -PassThru -WindowStyle Hidden -RedirectStandardOutput $ConsoleOut -RedirectStandardError $ConsoleError
    Wait-ServerLog 0 'Server startup complete' 600
    $Connection = & "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen
    Wait-ServerLog 0 '[CarbonLuau:AddonLive] PASS Active'
    Wait-ServerLog 0 '[CarbonLuau:AddonDependencyLive] PASS Active'
    Wait-ServerLog 0 '[CarbonLuau:AddonConsumerLive] PASS RequiredActive'
    Wait-ServerLog 0 '[CarbonLuau:AddonConsumerLive] PASS OptionalActive'
    $InitialConsumer = Read-ConsumerStatus
    if ($InitialConsumer.Required -ne 'Active' -or $InitialConsumer.Optional -ne 'Active') { throw 'Initial consumers are unavailable' }
    $StatusText = Send-Rcon 'carbonluau.status' 'CarbonLuau: ready'
    foreach ($Identity in @("CarbonLuau package: $($Release.packageVersion)",
            "Scripting API: $($Release.apiName) $($Release.apiVersion)", "Native ABI: $($Release.nativeAbi) OK",
            "Addon protocol: $($Release.providerProtocolName) $($Release.providerProtocolVersion); package schema: $($Release.packageSchema)")) {
        if (!$StatusText.Contains($Identity)) { throw "Status omitted public identity: $Identity" }
    }
    $ServerProcess.Refresh(); $RssBeforeScale = $ServerProcess.WorkingSet64
    Write-Output '[CarbonLuau:FoundationEWorker] PASS initial providers and dependency consumers'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'carbonluau.foundationestates' 'CarbonLuau Foundation E lifecycle states PASS' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:FoundationELive] PASS reachable states, unload, stale token, replacement, unregister, re-registration'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'clfoundatione.providerreplace' 'replacement queued' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] PASS same-provider replacement'
    $Offset = (Read-ServerLog).Length
    Send-Rcon 'clfoundatione.providerunregister' 're-registration queued' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] PASS unregister and explicit re-registration'
    Write-Output '[CarbonLuau:FoundationEWorker] PASS registration-state and same-provider lifecycle matrix'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.unload CarbonLuauAddonDependencyProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] provider Unload reached'
    $LostConsumer = Wait-ConsumerState 'Blocked' $InitialConsumer.RequiredDomain $InitialConsumer.OptionalDomain
    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.load CarbonLuauAddonDependencyProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] PASS Active'
    $RestoredConsumer = Wait-ConsumerState 'Active' $InitialConsumer.RequiredDomain $InitialConsumer.OptionalDomain
    $Offset = (Read-ServerLog).Length
    Send-Rcon 'clfoundationc.replace' 'replacement queued' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] PASS Replaced'
    $ReplacedConsumer = Wait-ConsumerState 'Active' $RestoredConsumer.RequiredDomain $InitialConsumer.OptionalDomain
    Write-Output '[CarbonLuau:FoundationEWorker] PASS required loss/restoration/replacement and optional no-rebind semantics'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'carbonluau.foundationescale' 'CarbonLuau Foundation E scale100 PASS' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:FoundationELive] PASS scale100' 300
    $ServerProcess.Refresh()
    Write-Output ("[CarbonLuau:FoundationEWorker] PASS live 100-addon scale and delayed-wake checks; processRssBytes=" +
        $ServerProcess.WorkingSet64 + "; processRssDeltaBytes=" + ($ServerProcess.WorkingSet64 - $RssBeforeScale) +
        "; processPeakBytes=" + $ServerProcess.PeakWorkingSet64)

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.unload CarbonLuau' | Out-Null
    Wait-ServerLog $Offset 'Unloaded plugin CarbonLuau'
    Wait-ServerLog $Offset 'Native library unloaded successfully'
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] observed CarbonLuau unload'
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] observed CarbonLuau unload'
    Wait-ServerLog $Offset '[CarbonLuau:AddonConsumerLive] observed CarbonLuau unload'
    if (!(Send-Rcon 'status').Contains('hostname: CarbonLuauFoundationE')) { throw 'Rust server became unresponsive after CarbonLuau teardown' }
    $ServerProcess.Refresh()
    if ($ServerProcess.Modules.ModuleName -contains 'carbonluau_native.dll') { throw 'Native library remained mapped after CarbonLuau teardown' }
    if (Get-Process -Name 'carbonluau_compiler' -ErrorAction SilentlyContinue) { throw 'Compiler worker remained after CarbonLuau teardown' }
    Write-Output '[CarbonLuau:FoundationEWorker] PASS full CarbonLuau teardown with 100 addons while providers remained loaded'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.load CarbonLuau' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:Runtime] Ready; generation=' 180
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] PASS stale token rejected after CarbonLuau reload'
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] PASS stale token rejected after CarbonLuau reload'
    Wait-ServerLog $Offset '[CarbonLuau:AddonConsumerLive] PASS stale token rejected after CarbonLuau reload'
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] PASS Active'
    Wait-ServerLog $Offset '[CarbonLuau:AddonDependencyLive] PASS Active'
    Wait-ServerLog $Offset '[CarbonLuau:AddonConsumerLive] PASS RequiredActive'
    Wait-ServerLog $Offset '[CarbonLuau:AddonConsumerLive] PASS OptionalActive'
    $ReloadedConsumer = Read-ConsumerStatus
    if ($ReloadedConsumer.Required -ne 'Active' -or $ReloadedConsumer.Optional -ne 'Active' -or
        $ReloadedConsumer.RequiredDomain -eq '' -or $ReloadedConsumer.OptionalDomain -eq '') {
        throw 'Providers did not explicitly re-register against fresh CarbonLuau lifetimes'
    }
    Write-Output '[CarbonLuau:FoundationEWorker] PASS CarbonLuau reload, stale tokens, and explicit provider re-registration'
    Write-Output '[CarbonLuau:FoundationEWorker] LIVE CARBON PASS'
} catch {
    $RunFailure = $_
} finally {
    if ($Connection) {
        try { Send-Rcon 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:FoundationEWorker] RCON closed during clean shutdown' }
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
