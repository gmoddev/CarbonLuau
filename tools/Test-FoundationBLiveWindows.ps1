param(
    [string]$Server = 'C:\Sandbox\Codex\Builds\CarbonLuauPhase5Windows\server-win',
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][string]$NativeLibrary,
    [Parameter(Mandatory)][string]$ProviderFixture,
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [int]$Port = 28416
)
$ErrorActionPreference = 'Stop'
$RunId = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$ServerLog = Join-Path $ArtifactDirectory "foundation-b-live-$RunId.log"
$ConsoleOut = Join-Path $ArtifactDirectory "foundation-b-console-$RunId.log"
$ConsoleError = Join-Path $ArtifactDirectory "foundation-b-console-error-$RunId.log"
$SecretPath = Join-Path $ArtifactDirectory "foundation-b-rcon-$RunId.secret"
$Connection = $null
$ServerProcess = $null
$RunFailure = $null

function Read-ServerLog {
    if (!(Test-Path -LiteralPath $ServerLog)) { return '' }
    $Stream = [IO.File]::Open($ServerLog, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $Reader = New-Object IO.StreamReader($Stream)
    try { return $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}

function Wait-ServerLog([int]$Offset, [string]$Expected, [int]$Seconds = 120) {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text = (Read-ServerLog).Substring($Offset)
        if ($Text.Contains('[CarbonLuau:AddonLive] FAIL')) { throw "Live provider fixture failed: $Text" }
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

if (!(Test-Path -LiteralPath (Join-Path $Server 'RustDedicated.exe'))) { throw "Missing isolated server: $Server" }
$NativeDirectory = Join-Path $Server 'carbon\data\CarbonLuau\native\win-x64'
$PluginDirectory = Join-Path $Server 'carbon\plugins'
New-Item -ItemType Directory -Force $NativeDirectory,$PluginDirectory | Out-Null
$DeployedPackage = Join-Path $PluginDirectory 'CarbonLuau.cszip'
$DeployedNative = Join-Path $NativeDirectory 'carbonluau_native.dll'
$DeployedProvider = Join-Path $PluginDirectory 'CarbonLuauAddonProvider.cs'
$BackupDirectory = Join-Path $ArtifactDirectory "backup-$RunId"
$HadPackage = Test-Path -LiteralPath $DeployedPackage
$HadNative = Test-Path -LiteralPath $DeployedNative
$HadProvider = Test-Path -LiteralPath $DeployedProvider
New-Item -ItemType Directory -Force $BackupDirectory | Out-Null
if ($HadPackage) { Copy-Item -LiteralPath $DeployedPackage -Destination (Join-Path $BackupDirectory 'CarbonLuau.cszip') }
if ($HadNative) { Copy-Item -LiteralPath $DeployedNative -Destination (Join-Path $BackupDirectory 'carbonluau_native.dll') }
if ($HadProvider) { Copy-Item -LiteralPath $DeployedProvider -Destination (Join-Path $BackupDirectory 'CarbonLuauAddonProvider.cs') }

try {
    Copy-Item -LiteralPath $Package -Destination $DeployedPackage -Force
    Copy-Item -LiteralPath $NativeLibrary -Destination $DeployedNative -Force
    Copy-Item -LiteralPath $ProviderFixture -Destination $DeployedProvider -Force
    Add-Type 'using System.Runtime.InteropServices; public static class FoundationBErrorDialogs { [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode); }'
    [FoundationBErrorDialogs]::SetErrorMode(0x8003) | Out-Null
    $Secret = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($SecretPath, $Secret)
    $Arguments = @(
        '-batchmode', '-nographics', '-logfile', $ServerLog,
        '+server.ip', '127.0.0.1', '+server.port', '28415', '+server.queryport', '28417',
        '+server.identity', 'carbonluau-foundation-b', '+server.hostname', 'CarbonLuauFoundationB',
        '+server.worldsize', '1000', '+server.seed', '24680', '+server.maxplayers', '1', '+server.saveinterval', '600',
        '+rcon.ip', '127.0.0.1', '+rcon.port', $Port.ToString(), '+rcon.web', '1', '+rcon.password', $Secret
    )
    $ServerProcess = Start-Process -FilePath (Join-Path $Server 'RustDedicated.exe') -ArgumentList $Arguments `
        -WorkingDirectory $Server -PassThru -WindowStyle Hidden -RedirectStandardOutput $ConsoleOut -RedirectStandardError $ConsoleError
    Wait-ServerLog 0 'Server startup complete' 600
    $Connection = & "$PSScriptRoot\Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen
    Wait-ServerLog 0 '[CarbonLuau:AddonLive] PASS Active' 180
    Write-Output '[CarbonLuau:AddonLive] PASS initial provider activation'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.unload CarbonLuauAddonProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] provider Unload reached'
    Wait-ServerLog $Offset '[CarbonLuau:Addons] Retired 1 registration(s) for unloaded provider CarbonLuauAddonProvider.'
    if ((Send-Rcon 'c.find clfoundationb') -match '(?im)^\s*clfoundationb\s') { throw 'Addon command survived provider unload' }
    Write-Output '[CarbonLuau:AddonLive] PASS provider unload retired domain, task, command, and ID reservation'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.load CarbonLuauAddonProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:AddonLive] PASS Active' 180
    Write-Output '[CarbonLuau:AddonLive] PASS provider reload reclaimed the stable ID and activated'

    $Offset = (Read-ServerLog).Length
    Send-Rcon 'c.unload CarbonLuauAddonProvider' | Out-Null
    Wait-ServerLog $Offset '[CarbonLuau:Addons] Retired 1 registration(s) for unloaded provider CarbonLuauAddonProvider.'
    Write-Output '[CarbonLuau:FoundationBWorker] LIVE CARBON PASS'
} catch {
    $RunFailure = $_
} finally {
    if ($Connection) {
        try { Send-Rcon 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:AddonLive] RCON closed during clean shutdown' }
        $Connection.Dispose()
    }
    if ($ServerProcess -and !$ServerProcess.WaitForExit(120000)) {
        Stop-Process -Id $ServerProcess.Id -Force
        $ServerProcess.WaitForExit(15000) | Out-Null
        if (!$RunFailure) { $RunFailure = 'Live server required forced termination' }
    }
    if (Test-Path -LiteralPath $SecretPath) { Remove-Item -LiteralPath $SecretPath -Force }
    foreach ($Value in @(
        @{Had=$HadPackage; Deployed=$DeployedPackage; Backup=(Join-Path $BackupDirectory 'CarbonLuau.cszip')},
        @{Had=$HadNative; Deployed=$DeployedNative; Backup=(Join-Path $BackupDirectory 'carbonluau_native.dll')},
        @{Had=$HadProvider; Deployed=$DeployedProvider; Backup=(Join-Path $BackupDirectory 'CarbonLuauAddonProvider.cs')}
    )) {
        if ($Value.Had) { Copy-Item -LiteralPath $Value.Backup -Destination $Value.Deployed -Force }
        elseif (Test-Path -LiteralPath $Value.Deployed) { Remove-Item -LiteralPath $Value.Deployed -Force }
    }
}
if ($RunFailure) { throw $RunFailure }
