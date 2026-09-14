param([Parameter(Mandatory)][string]$Library)
$ErrorActionPreference = 'Stop'
$VsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$DumpBin = & $VsWhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC/Tools/MSVC/**/bin/Hostx64/x64/dumpbin.exe' | Select-Object -First 1
if (!$DumpBin) { throw 'MSVC dumpbin not found' }
$Imports = & $DumpBin /dependents (Resolve-Path $Library)
if ($LASTEXITCODE) { throw 'dumpbin failed' }
if ($Imports -match '(?i)^\s+(MSVCP|VCRUNTIME|api-ms-win-crt)') { throw 'Native library depends on host-selected dynamic MSVC CRT' }
Write-Output '[CarbonLuau:ImportTest] PASS: no dynamic MSVC CRT dependencies'
