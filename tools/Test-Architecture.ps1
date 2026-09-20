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
    'src/CarbonLuau/Gui/GuiActions.cs',
    'src/CarbonLuau/Gui/GuiDescriptors.cs',
    'src/CarbonLuau/Gui/GuiImageSource.cs',
    'src/CarbonLuau/Gui/GuiModelContracts.cs',
    'src/CarbonLuau/Gui/GuiRetainedRegistry.cs',
    'src/CarbonLuau/Gui/GuiRenderPlan.cs',
    'src/CarbonLuau/Gui/GuiPresentation.cs',
    'src/CarbonLuau/Gui/IGuiBackend.cs',
    'src/CarbonLuau/Gui/InMemoryGuiBackend.cs',
    'src/CarbonLuau/Gui/RustCuiBackend.cs',
    'src/CarbonLuau/Gui/GuiHostCapabilities.cs',
    'src/CarbonLuau/CarbonLuau.Gui.Carbon.cs',
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
    if ($GuiText -match 'using\s+Oxide\.Game\.Rust\.Cui|\bBasePlayer\b|\bCuiHelper\b|\bCuiElement\b|using\s+UnityEngine') {
        throw "GUI retained/backend contracts leaked host CUI types: $($GuiSource.Name)"
    }
}
$Bootstrap = Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau')
if (!$Bootstrap.Contains('if Name == "Gui" then return Gui end') -or !$Bootstrap.Contains('MakeUserdata')) {
    throw 'GUI Foundation 1B must install the domain-bound Gui service and opaque userdata boundary'
}
foreach ($Required in @('GuiObjectMethods.Show','GuiObjectMethods.Hide','GuiObjectMethods.IsShown')) {
    if (!$Bootstrap.Contains($Required)) { throw "GUI Foundation 1C public screen lifecycle is missing: $Required" }
}
$Presentation = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/GuiPresentation.cs')
$Registry = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/GuiRetainedRegistry.cs')
$Actions = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/GuiActions.cs')
if (!$Presentation.Contains('GuiRenderCompiler') -or !$Registry.Contains('RandomNumberGenerator')) {
    throw 'GUI Foundation 1C must own deterministic render compilation and opaque presentation identity'
}
foreach ($Required in @('GuiScreenSynchronization','CompilePatch','FullRebuildRequired','PatchBatchesBeforeFull')) {
    if (!$Presentation.Contains($Required) -and !$Registry.Contains($Required)) {
        throw "GUI Foundation 1D synchronization owner is missing: $Required"
    }
}
foreach ($Required in @('ProjectChildren','GuiAffine','ContentRect')) {
    if (!$Presentation.Contains($Required)) { throw "GUI Foundation 2A layout compiler owner is missing: $Required" }
}
foreach ($Required in @('MarkLayoutAffected','UIListLayout','UIPadding','LayoutProjection')) {
    if (!$Registry.Contains($Required) -and !$Presentation.Contains($Required)) {
        throw "GUI Foundation 2A retained/synchronization owner is missing: $Required"
    }
}
if ($Presentation -match 'LayoutGroup|HorizontalLayoutGroup|VerticalLayoutGroup') {
    throw 'GUI Foundation 2A must not delegate canonical layout to a host layout group'
}
$RenderPlan = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/GuiRenderPlan.cs')
foreach ($Required in @('ScrollingFrame','CanvasSize','ScrollingDirection','ScrollingEnabled','ScrollContentClientId')) {
    if (!$Registry.Contains($Required) -and !$Presentation.Contains($Required)) {
        throw "GUI Foundation 2C retained/projection owner is missing: $Required"
    }
}
foreach ($Required in @('ScrollView','PrivateChildRootId','ProjectedElementCost')) {
    if (!$RenderPlan.Contains($Required)) { throw "GUI Foundation 2C bounded render-plan owner is missing: $Required" }
}
foreach ($Required in @('RandomNumberGenerator','MaxActionTokensGlobal','ValidateQueued','GuiActionDiagnostics','MaxPlayerInteractionsPerSecond')) {
    if (!$Actions.Contains($Required) -and !$Registry.Contains($Required)) {
        throw "GUI Foundation 1E interaction owner is missing: $Required"
    }
}
$Backend = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/RustCuiBackend.cs')
if (!$Backend.Contains('MeasureUpdate') -or !$Backend.Contains('Transport.Update') -or !$Backend.Contains('update')) {
    throw 'GUI Foundation 1D Rust CUI update path is missing'
}
if (!$Backend.Contains('ActionCommand') -or !$Backend.Contains('"command"')) {
    throw 'GUI Foundation 1E Rust CUI action command serialization is missing'
}
if (!$Backend.Contains('UnityEngine.UI.ScrollView') -or !$Backend.Contains('contentTransform') -or
    $Backend.Contains('NormalizedPosition') -or $Presentation.Contains('CanvasPosition')) {
    throw 'GUI Foundation 2C must project ScrollView content without retaining client scroll position'
}
$ScriptHostText = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Scripts/ScriptHost.cs')
if ($ScriptHostText.IndexOf('Vm.Callback') -ge $ScriptHostText.LastIndexOf('Facade.FlushGui')) {
    throw 'GUI Foundation 1D flush must remain after the bounded Luau callback drain'
}
$Transport = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/CarbonLuau.Gui.Carbon.cs')
if (!$Transport.Contains('CuiHelper.AddUi') -or !$Transport.Contains('CuiHelper.DestroyUi') -or
    !$Transport.Contains('ExactPlayerConnectionToken')) {
    throw 'GUI Foundation 1C Carbon adapter must use Rust CUI and exact Player connection identity'
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

Write-Output '[CarbonLuau:ArchitectureTest] PASS: managed/native invariant owners, retained GUI synchronization, secure action ingress, exact Rust CUI adapter, compiler boundary and native build graph'
