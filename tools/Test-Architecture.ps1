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
    'src/CarbonLuau/Gui/GuiConfig.cs',
    'src/CarbonLuau/Gui/GuiDescriptors.cs',
    'src/CarbonLuau/Gui/GuiModelContracts.cs',
    'src/CarbonLuau/Gui/GuiRetainedRegistry.cs',
    'src/CarbonLuau/Gui/GuiRenderPlan.cs',
    'src/CarbonLuau/Gui/IGuiBackend.cs',
    'src/CarbonLuau/Gui/InMemoryGuiBackend.cs',
    'src/CarbonLuau/Gui/GuiHostCapabilities.cs',
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

$GuiSources = @(Get-ChildItem (Join-Path $Root 'src/CarbonLuau/Gui') -File -Filter '*.cs')
foreach ($GuiSource in $GuiSources) {
    $GuiText = Get-Content -Raw -LiteralPath $GuiSource.FullName
    if ($GuiText -match 'System\.Reflection|GetProperties\s*\(|GetMethods\s*\(|GetEvents\s*\(') {
        throw "GUI schema must not discover public members through reflection: $($GuiSource.Name)"
    }
    if ($GuiText -match '\bCui|\bLui|BasePlayer|UnityEngine') {
        throw "GUI retained/backend contracts leaked host CUI types: $($GuiSource.Name)"
    }
}
$Bootstrap = Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau')
if (!$Bootstrap.Contains('if Name == "Gui" then return Gui end') -or !$Bootstrap.Contains('MakeUserdata')) {
    throw 'GUI Foundation 1B must install the domain-bound Gui service and opaque userdata boundary'
}
foreach ($Deferred in @('GuiObjectMethods.Show','GuiObjectMethods.Hide','GuiObjectMethods.IsShown','Presentation','RustCuiBackend')) {
    if ($Bootstrap.Contains($Deferred)) { throw "GUI Foundation 1B crossed into deferred presentation/render behavior: $Deferred" }
}
if (Test-Path -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/RustCuiBackend.cs')) {
    throw 'GUI Foundation 1A must not implement the production Rust CUI backend'
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

Write-Output '[CarbonLuau:ArchitectureTest] PASS: managed/native invariant owners, retained GUI boundary, deferred rendering, compiler boundary and native build graph'
