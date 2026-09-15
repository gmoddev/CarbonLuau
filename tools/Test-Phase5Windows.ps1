param(
    [string]$Server = 'C:\Sandbox\Codex\Builds\CarbonLuauPhase5Windows\server-win',
    [string]$BuildDirectory = 'C:\Sandbox\Codex\Builds\CarbonLuauPhase5Windows\win-x64',
    [string]$FixtureDirectory = 'C:\Sandbox\Codex\Artifacts\CarbonLuauPhase5Windows\phase5-fixture',
    [string]$ArtifactDirectory = 'C:\Sandbox\Codex\Artifacts\CarbonLuauPhase5Windows',
    [int]$LifecycleCycles = 10,
    [int]$SoakMinutes = 30,
    [int]$ProfilerSeconds = 10,
    [int]$Port = 28316
)
$ErrorActionPreference = 'Stop'
$RunId = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$ServerLog = Join-Path $ArtifactDirectory "phase5-server-windows-$RunId.log"
$ConsoleOut = Join-Path $ArtifactDirectory "phase5-console-windows-$RunId.log"
$ConsoleError = Join-Path $ArtifactDirectory "phase5-console-error-windows-$RunId.log"
$RunnerLog = Join-Path $ArtifactDirectory "phase5-runner-windows-$RunId.log"
$Samples = Join-Path $ArtifactDirectory "phase5-samples-windows-$RunId.jsonl"
$SecretPath = Join-Path $ArtifactDirectory "phase5-rcon-$RunId.secret"
$Connection = $null
$ServerProcess = $null
$TranscriptStarted = $false
$ShutdownFailure = ''

function ReadLog {
    if (!(Test-Path -LiteralPath $ServerLog)) { return '' }
    $Stream = [IO.File]::Open($ServerLog, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $Reader = New-Object IO.StreamReader($Stream)
    try { return $Reader.ReadToEnd() } finally { $Reader.Dispose() }
}

function WaitLog([int]$Offset, [string]$Expected, [int]$Seconds = 60) {
    $Watch = [Diagnostics.Stopwatch]::StartNew()
    while ($Watch.Elapsed.TotalSeconds -lt $Seconds) {
        $Text = (ReadLog).Substring($Offset)
        if ($Text.Contains('[CarbonLuau:Phase5Fixture] FAIL')) { throw "Phase 5 fixture failed: $Text" }
        if ($Text.Contains($Expected)) { return }
        if ($ServerProcess -and $ServerProcess.HasExited) { throw "Server exited before: $Expected" }
        Start-Sleep -Milliseconds 250
    }
    throw "Missing current-run log: $Expected"
}

function Send([string]$Command, [string]$Expected = '') {
    $Reply = (& "$PSScriptRoot/Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command $Command -ExpectedMessage $Expected -Socket $Connection -KeepOpen) | ConvertFrom-Json
    return $Reply.Message
}

function RustProcess {
    $Process = Get-Process RustDedicated -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq (Join-Path $Server 'RustDedicated.exe') } |
        Select-Object -First 1
    if (!$Process) { throw 'Rust process missing' }
    return $Process
}

function CheckUnmapped {
    $Process = RustProcess
    $Process.Refresh()
    if ($Process.Modules.ModuleName -contains 'carbonluau_native.dll') {
        throw 'Native DLL remained mapped after plugin unload'
    }
}

function Sample([string]$Label) {
    $Process = RustProcess
    $Process.Refresh()
    $Record = [ordered]@{
        elapsed_seconds = [Math]::Round($Started.Elapsed.TotalSeconds, 3)
        label = $Label
        process = [ordered]@{
            WorkingSet64 = $Process.WorkingSet64
            PrivateMemorySize64 = $Process.PrivateMemorySize64
            VirtualMemorySize64 = $Process.VirtualMemorySize64
            PagedMemorySize64 = $Process.PagedMemorySize64
        }
        carbonluau_status = Send 'carbonluau.status' 'CarbonLuau:'
    }
    $Json = $Record | ConvertTo-Json -Depth 5 -Compress
    Add-Content -LiteralPath $Samples -Value $Json -Encoding UTF8
    Write-Output "[CarbonLuau:Phase5] sample $Json"
}

function RunProfiler {
    if ($ProfilerSeconds -le 0) {
        Write-Output '[CarbonLuau:Phase5] profiler skipped by development-run setting'
        return
    }
    Write-Output "[CarbonLuau:Phase5] profiler commands: $(Send 'c.find profile')"
    foreach ($Profile in @(@{ Label = 'idle'; Active = $false }, @{ Label = 'active'; Active = $true })) {
        $Reply = Send "c.profile $ProfilerSeconds -c -m -t -gc"
        if ($Reply.ToLowerInvariant().Contains('disabled')) { throw "Carbon profiler unexpectedly disabled: $Reply" }
        Write-Output "[CarbonLuau:Phase5] profiler $($Profile.Label) start: $Reply"
        $Deadline = [DateTime]::UtcNow.AddSeconds($ProfilerSeconds + 2)
        while ([DateTime]::UtcNow -lt $Deadline) {
            if ($Profile.Active) { Write-Output (Send 'carbonluau.phase5pulse' 'Phase5 pulse PASS') }
            Start-Sleep -Milliseconds 1000
        }
        Write-Output "[CarbonLuau:Phase5] profiler $($Profile.Label) status: $(Send 'c.profilestatus')"
        Write-Output "[CarbonLuau:Phase5] profiler $($Profile.Label) result: $(Send 'c.profiler.print -j')"
    }
}

try {
    Start-Transcript -LiteralPath $RunnerLog | Out-Null
    $TranscriptStarted = $true
    if (!(Test-Path -LiteralPath (Join-Path $Server 'RustDedicated.exe'))) { throw "Missing isolated server: $Server" }
    $NativeSource = Join-Path $BuildDirectory 'Release\carbonluau_native.dll'
    $FixturePackage = Join-Path $FixtureDirectory 'CarbonLuau.cszip'
    if (!(Test-Path -LiteralPath $NativeSource)) { throw "Missing native library: $NativeSource" }
    if (!(Test-Path -LiteralPath $FixturePackage)) { throw "Missing fixture package: $FixturePackage" }
    $NativeDirectory = Join-Path $Server 'carbon\data\CarbonLuau\native\win-x64'
    $PluginDirectory = Join-Path $Server 'carbon\plugins'
    $ScriptDirectory = Join-Path $Server 'carbon\data\CarbonLuau\scripts'
    New-Item -ItemType Directory -Force $NativeDirectory, $PluginDirectory, $ScriptDirectory | Out-Null
    Copy-Item -LiteralPath $NativeSource -Destination (Join-Path $NativeDirectory 'carbonluau_native.dll') -Force
    Copy-Item -LiteralPath $FixturePackage -Destination (Join-Path $PluginDirectory 'CarbonLuau.cszip') -Force
    Copy-Item -Path (Join-Path $FixtureDirectory 'scripts\*') -Destination $ScriptDirectory -Recurse -Force

    $ProfilerConfig = Join-Path $Server 'carbon\config.profiler.json'
    if (Test-Path -LiteralPath $ProfilerConfig) {
        $ProfilerSettings = Get-Content -LiteralPath $ProfilerConfig -Raw | ConvertFrom-Json
    } else { $ProfilerSettings = [pscustomobject]@{} }
    $RequiredProfilerSettings = [ordered]@{
        Enabled = $true; TrackCalls = $true; SourceViewer = $false
        Assemblies = @(); Plugins = @('CarbonLuau'); Modules = @(); Extensions = @(); Harmony = @()
    }
    foreach ($Setting in $RequiredProfilerSettings.GetEnumerator()) {
        if ($ProfilerSettings.PSObject.Properties.Name -contains $Setting.Key) {
            $ProfilerSettings.($Setting.Key) = $Setting.Value
        } else { $ProfilerSettings | Add-Member -NotePropertyName $Setting.Key -NotePropertyValue $Setting.Value }
    }
    # Carbon's native profiler parser rejects the UTF-8 BOM emitted by Windows PowerShell 5.1.
    $ProfilerJson = ($ProfilerSettings | ConvertTo-Json -Depth 8) + [Environment]::NewLine
    [IO.File]::WriteAllText($ProfilerConfig, $ProfilerJson, (New-Object Text.UTF8Encoding($false)))

    Add-Type 'using System.Runtime.InteropServices; public static class Phase5ErrorDialogs { [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode); }'
    [Phase5ErrorDialogs]::SetErrorMode(0x8003) | Out-Null
    $Secret = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($SecretPath, $Secret)
    $Arguments = @(
        '-batchmode', '-nographics', '-logfile', $ServerLog,
        '+server.ip', '127.0.0.1', '+server.port', '28315', '+server.queryport', '28317',
        '+server.identity', 'carbonluau-phase5-windows', '+server.hostname', 'CarbonLuauPhase5Windows',
        '+server.worldsize', '1000', '+server.seed', '13579', '+server.maxplayers', '1', '+server.saveinterval', '600',
        '+rcon.ip', '127.0.0.1', '+rcon.port', $Port.ToString(), '+rcon.web', '1', '+rcon.password', $Secret
    )
    $Started = [Diagnostics.Stopwatch]::StartNew()
    Write-Output "[CarbonLuau:Phase5] log: $ServerLog"
    $ServerProcess = Start-Process -FilePath (Join-Path $Server 'RustDedicated.exe') -ArgumentList $Arguments `
        -WorkingDirectory $Server -PassThru -WindowStyle Hidden -RedirectStandardOutput $ConsoleOut -RedirectStandardError $ConsoleError
    WaitLog 0 'Server startup complete' 600
    $Connection = & "$PSScriptRoot/Rcon.ps1" -Port $Port -SecretPath $SecretPath -Command '' -KeepOpen
    WaitLog 0 'Ready; generation=1' 120
    Sample 'startup'

    $Offset = (ReadLog).Length
    Write-Output (Send 'carbonluau.phase5fixture' 'CarbonLuau Phase5 fixture scheduled')
    WaitLog $Offset 'PASS complete Phase 5 controlled-host fixture' 900
    Sample 'composite-fixture'
    Write-Output (Send 'carbonluau.phase5latency' 'Phase5 latency PASS')

    for ($Cycle = 1; $Cycle -le $LifecycleCycles; ++$Cycle) {
        Write-Output (Send 'carbonluau.phase5arm' 'Phase5 armed')
        $Armed = Send 'carbonluau.status' 'CarbonLuau: ready'
        if (!$Armed.Contains('Facade pending/listeners/commands: 0/2/1') -or !$Armed.Contains('Queued: 1')) {
            throw "Lifecycle state was not fully armed: $Armed"
        }
        $Offset = (ReadLog).Length
        Send 'c.unload CarbonLuau' | Out-Null
        WaitLog $Offset 'Unloaded plugin CarbonLuau'
        WaitLog $Offset 'Native library unloaded successfully'
        CheckUnmapped
        $CommandSearch = Send 'c.find clphase5'
        if ($CommandSearch -match '(?im)^\s*clphase5\s') { throw 'Host command remained registered after unload' }
        if (!(Send 'status').Contains('hostname: CarbonLuauPhase5Windows')) { throw 'Server unresponsive after unload' }
        $Offset = (ReadLog).Length
        Send 'c.load CarbonLuau' | Out-Null
        WaitLog $Offset 'Ready; generation=1' 120
        Write-Output "[CarbonLuau:Phase5] PASS plugin unload/load cycle $Cycle; native unmapped and server responsive"
    }
    Sample 'lifecycle-soak'

    Write-Output (Send 'carbonluau.phase5arm' 'Phase5 armed')
    $SoakDeadline = [DateTime]::UtcNow.AddMinutes($SoakMinutes)
    $Pulse = 0
    while ([DateTime]::UtcNow -lt $SoakDeadline) {
        $Pulse++
        Write-Output (Send 'carbonluau.phase5pulse' 'Phase5 pulse PASS')
        Sample "soak-$Pulse"
        $Remaining = ($SoakDeadline - [DateTime]::UtcNow).TotalSeconds
        if ($Remaining -gt 0) { Start-Sleep -Seconds ([Math]::Min(60, $Remaining)) }
    }
    Sample 'soak-final'
    RunProfiler
    Write-Output (Send 'carbonluau.phase5cleanup' 'controlled player removed')
    Sample 'final'
    Write-Output "[CarbonLuau:Phase5] PASS Windows Phase 5 runner; lifecycle cycles=$LifecycleCycles; soak minutes=$SoakMinutes"
} finally {
    if ($Connection) {
        try { Send 'quit' | Out-Null } catch { Write-Output '[CarbonLuau:Phase5] quit sent; RCON closed during shutdown' }
        $Connection.Dispose()
    }
    if ($ServerProcess) {
        if (!$ServerProcess.WaitForExit(60000)) {
            Stop-Process -Id $ServerProcess.Id -Force
            $ServerProcess.WaitForExit(15000) | Out-Null
            $ShutdownFailure = 'Server required termination instead of clean quit'
        }
        $ServerProcess.Refresh()
        Write-Output "[CarbonLuau:Phase5] server exit code: $($ServerProcess.ExitCode)"
    }
    if (Test-Path -LiteralPath $SecretPath) { Remove-Item -LiteralPath $SecretPath -Force }
    if ($TranscriptStarted) { Stop-Transcript | Out-Null }
    if ($ShutdownFailure) { throw $ShutdownFailure }
}
