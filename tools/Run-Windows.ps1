param([string]$Server = 'C:\Sandbox\Codex\Builds\CarbonLuau\server-win', [string]$Package = '', [string]$LogFile = '')
$ErrorActionPreference = 'Stop'
$Artifacts = 'C:\Sandbox\Codex\Artifacts\CarbonLuau'
if (!$Package) { $Package = "$Artifacts\CarbonLuau.cszip" }
if (!$LogFile) { $LogFile = "$Artifacts\server-win.log" }
$NativeDirectory = Join-Path $Server 'carbon\data\CarbonLuau\native\win-x64'
New-Item -ItemType Directory -Force $NativeDirectory,(Join-Path $Server 'carbon\plugins') | Out-Null
Copy-Item $Package "$Server\carbon\plugins\CarbonLuau.cszip" -Force
Copy-Item 'C:\Sandbox\Codex\Builds\CarbonLuau\win-x64\Release\carbonluau_native.dll' $NativeDirectory -Force
# Suppress Windows error dialogs for this process and its server child.
Add-Type 'using System.Runtime.InteropServices; public static class ErrorDialogs { [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint Mode); }'
[ErrorDialogs]::SetErrorMode(0x8003) | Out-Null
$Secret = [Guid]::NewGuid().ToString('N')
[IO.File]::WriteAllText("$Artifacts\rcon-win.secret", $Secret)
Set-Location $Server
& .\RustDedicated.exe -batchmode -nographics -logfile $LogFile +server.ip 127.0.0.1 +server.port 28115 +server.queryport 28117 +server.identity carbonluau-phase0 +server.hostname CarbonLuauPhase0 +server.worldsize 1000 +server.seed 13579 +server.maxplayers 1 +server.saveinterval 600 +rcon.ip 127.0.0.1 +rcon.port 28116 +rcon.web 1 +rcon.password $Secret
exit $LASTEXITCODE
