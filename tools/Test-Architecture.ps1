$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

$Expected = @(
    'src/CarbonLuau/Runtime/RuntimeConfig.cs',
    'src/CarbonLuau/Runtime/NativeLibraryLoader.cs',
    'src/CarbonLuau/Runtime/NativeRuntime.cs',
    'src/CarbonLuau/Runtime/RuntimeGeneration.cs',
    'src/CarbonLuau/Scripts/ScriptSnapshot.cs',
    'src/CarbonLuau/Scripts/RuntimeDomain.cs',
    'src/CarbonLuau/Scripts/ScriptHost.cs',
    'src/CarbonLuau/Facade/PlayerDirectory.cs',
    'src/CarbonLuau/Facade/CommandRegistry.cs',
    'src/CarbonLuau/Facade/FacadeSession.cs',
    'src/CarbonLuau/Addons/AddonPackage.cs',
    'src/CarbonLuau/Addons/AddonRegistry.cs',
    'src/CarbonLuau/Addons/DependencyGraph.cs',
    'native/src/runtime/RuntimeInternal.hpp',
    'native/src/runtime/VmRegistry.cpp',
    'native/src/runtime/VmState.cpp',
    'native/src/runtime/Publication.cpp',
    'native/src/runtime/Deadline.cpp',
    'native/src/scripts/Compiler.cpp',
    'native/src/scripts/CompilerProtocol.hpp',
    'native/src/scripts/CompilerWorker.cpp',
    'native/src/scripts/ModuleLoader.cpp',
    'native/src/facade/FacadeBridge.cpp'
)
foreach ($Path in $Expected) {
    if (!(Test-Path -LiteralPath (Join-Path $Root $Path))) { throw "Missing architecture owner: $Path" }
}

$Forbidden = @(
    'src/CarbonLuau/CarbonLuau.Runtime.cs',
    'src/CarbonLuau/CarbonLuau.Scripts.cs',
    'src/CarbonLuau/CarbonLuau.Facade.cs',
    'src/CarbonLuau/CarbonLuau.Addons.cs',
    'native/src/Scripts.inl',
    'native/src/Facade.inl'
)
foreach ($Path in $Forbidden) {
    if (Test-Path -LiteralPath (Join-Path $Root $Path)) { throw "Retired implementation concentration returned: $Path" }
}
if (Get-ChildItem (Join-Path $Root 'native/src') -Recurse -File -Filter '*.inl') {
    throw 'Private native .inl implementation coupling returned'
}

$Worker = Get-Content -Raw -LiteralPath (Join-Path $Root 'native/src/scripts/CompilerWorker.cpp')
if ([regex]::Matches($Worker, 'Luau::compile').Count -ne 1) { throw 'Isolated compiler worker must own the Luau compile call' }
foreach ($Path in @('native/src/scripts/Compiler.cpp','native/src/Runtime.cpp','native/src/scripts/ModuleLoader.cpp','native/src/facade/FacadeBridge.cpp')) {
    if ((Get-Content -Raw -LiteralPath (Join-Path $Root $Path)).Contains('Luau::compile')) {
        throw "Luau compile call escaped compiler owner: $Path"
    }
}

$CMake = Get-Content -Raw -LiteralPath (Join-Path $Root 'native/CMakeLists.txt')
foreach ($Path in $Expected | Where-Object { $_ -like 'native/src/*.cpp' -or $_ -like 'native/src/*/*.cpp' }) {
    $Relative = $Path.Substring('native/'.Length)
    if (!$CMake.Contains($Relative)) { throw "Native owner missing from build graph: $Relative" }
}

Write-Output '[CarbonLuau:ArchitectureTest] PASS: managed and native invariant owners, retired god files, compiler boundary and native build graph'
