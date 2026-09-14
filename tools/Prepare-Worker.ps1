$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Root = 'C:\Sandbox\Codex'
$Cache = Join-Path $Root 'Cache\CarbonLuau'
$Server = Join-Path $Root 'Builds\CarbonLuau\server-win'
New-Item -ItemType Directory -Force $Cache,$Server | Out-Null
if (!(Test-Path "$Cache\steamcmd\steamcmd.exe")) {
    Invoke-WebRequest https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip -OutFile "$Cache\steamcmd.zip" -UseBasicParsing
    Expand-Archive "$Cache\steamcmd.zip" "$Cache\steamcmd" -Force
}
& "$Cache\steamcmd\steamcmd.exe" +force_install_dir $Server +login anonymous +app_update 258550 validate +quit
if ($LASTEXITCODE -ne 0) { throw "SteamCMD failed: $LASTEXITCODE" }
Invoke-WebRequest https://github.com/CarbonCommunity/Carbon/releases/download/production_build/Carbon.Windows.Release.zip -OutFile "$Cache\Carbon.Windows.Release.zip" -UseBasicParsing
Expand-Archive "$Cache\Carbon.Windows.Release.zip" $Server -Force
Write-Output '[CarbonLuau:Setup] Windows server downloaded.'
