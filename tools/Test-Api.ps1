$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Bootstrap = Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau')
$Managed = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Facade/FacadePolicy.cs')
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
$Version = $Release.apiVersion
$GuiGuide = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Gui.md')
$GuiReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Gui-Reference.md')
$GuiDescriptors = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Gui/GuiDescriptors.cs')
$ReleaseNotes = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/releases/0.4.0.md')
$Player1C = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/PlayerInteractionFoundation1C.md')
foreach ($Text in @($Bootstrap,$Managed,(Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Globals.md')),
    (Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Compatibility.md')),$GuiGuide,$GuiReference)) {
    if (!$Text.Contains($Version)) { throw 'API version differs between runtime and documentation' }
}
foreach ($Service in @('Players','Commands','Gui','Items')) {
    if (!$Bootstrap.Contains(('if Name == "{0}"' -f $Service))) { throw "Missing registered service: $Service" }
    if ($Service -ne 'Gui' -and !(Test-Path -LiteralPath (Join-Path $Root "docs/api/Services/$Service.md"))) { throw "Missing service reference: $Service" }
}
foreach ($Type in @('Player','Vector3','CommandContext','Signal','Connection')) {
    if (!(Test-Path -LiteralPath (Join-Path $Root "docs/api/Types/$Type.md"))) { throw "Missing type reference: $Type" }
}
$Documents = @(Get-Item (Join-Path $Root 'README.md')) + @(Get-Item (Join-Path $Root 'CHANGELOG.md')) +
    @(Get-ChildItem (Join-Path $Root 'docs') -Recurse -Filter '*.md')
foreach ($Document in $Documents) {
    foreach ($Match in [regex]::Matches((Get-Content -Raw -LiteralPath $Document.FullName), '\]\(([^)]+)\)')) {
        $Target = $Match.Groups[1].Value
        if ($Target -match '^(https?://|mailto:|#)') { continue }
        $Target = ($Target -split '#')[0]
        if (!(Test-Path -LiteralPath (Join-Path $Document.DirectoryName $Target))) { throw "Broken relative link: $($Document.Name): $Target" }
    }
}
foreach ($Example in @('player-events','player-position','player-health','player-inventory','hello-command')) {
    if (!(Test-Path -LiteralPath (Join-Path $Root "examples/$Example/init.luau"))) { throw "Missing runnable example: $Example" }
}
foreach ($Example in @('hello','shared-live','per-player','activated','images','scrolling',
        'layout-vertical','layout-horizontal','padding','layout-order','image-label','image-button',
        'item-skin','steam-avatar','scrolling-layout','shared-rich','per-player-rich','grid','grid-vertical','grid-padding',
        'grid-scrolling','clipping','nested-clipping','fonts','font-patch','scroll-effects','per-player-scroll','foundation3-combined')) {
    $ExamplePath = Join-Path $Root "examples/gui/$Example/init.luau"
    if (!(Test-Path -LiteralPath $ExamplePath)) { throw "Missing runnable GUI example: $Example" }
    $ExampleText = Get-Content -Raw -LiteralPath $ExamplePath
    if ($ExampleText -match '(?i)CuiHelper|AddUi|DestroyUi|carbonluau\.gui\.action') { throw "GUI example exposes raw CUI: $Example" }
}
foreach ($Path in @('examples/addons/economy/addon.json','examples/addons/economy/init.luau',
        'examples/addons/economy/api.luau','examples/addons/economy/formatting.luau',
        'examples/addons/shop/addon.json','examples/addons/shop/init.luau',
        'examples/addons/guiowner/addon.json','examples/addons/guiowner/init.luau','examples/addons/guiowner/api.luau',
        'examples/addons/guiconsumer/addon.json','examples/addons/guiconsumer/init.luau')) {
    if (!(Test-Path -LiteralPath (Join-Path $Root $Path))) { throw "Missing addon example file: $Path" }
}
foreach ($Name in @('ScreenGui','Frame','TextLabel','TextButton','ImageLabel','ImageButton','ScrollingFrame','UIListLayout','UIGridLayout','UIPadding','GuiObject','UDim','UDim2','Vector2','Color3','ImageSource','GuiFont')) {
    if (!$GuiReference.Contains(('`{0}`' -f $Name))) { throw "GUI reference omits public type: $Name" }
    if ($Name -notin @('GuiObject','UDim','UDim2','Vector2','Color3') -and !$GuiDescriptors.Contains(('"{0}"' -f $Name))) {
        throw "GUI descriptor omits implemented public type: $Name"
    }
}
foreach ($Name in @('Name','ClassName','Parent','Position','Size','AnchorPoint','Visible','BackgroundColor3',
        'BackgroundTransparency','ZIndex','LayoutOrder','Text','TextColor3','TextTransparency','TextSize','TextXAlignment','TextYAlignment',
        'Padding','FillDirection','HorizontalAlignment','VerticalAlignment','PaddingTop','PaddingBottom','PaddingLeft','PaddingRight',
        'Image','ImageColor3','ImageTransparency','CanvasSize','ScrollingDirection','ScrollingEnabled',
        'CellSize','CellPadding','FillDirectionMaxCells','ClipsDescendants','Font')) {
    if (!$GuiReference.Contains(('`{0}`' -f $Name))) { throw "GUI reference omits property: $Name" }
}
foreach ($Name in @('Create','Clone','Destroy','GetChildren','FindFirstChild','IsA','Show','Hide','IsShown','Activated','ScrollTo','ScrollToTop','ScrollToBottom')) {
    if (!$GuiReference.Contains($Name) -or !$Bootstrap.Contains($Name)) { throw "GUI method/event differs between bootstrap and reference: $Name" }
}
foreach ($Constructor in @('UDim.new','UDim2.new','UDim2.fromScale','UDim2.fromOffset','Vector2.new','Color3.new','Color3.fromRGB')) {
    if (!$GuiReference.Contains($Constructor) -or !$Bootstrap.Contains($Constructor)) { throw "GUI constructor differs between bootstrap and reference: $Constructor" }
}
if (!$Bootstrap.Contains('Vector3.new') -or !(Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/Vector3.md')).Contains('Vector3.new')) {
    throw 'Vector3 constructor differs between bootstrap and reference'
}
$PlayerReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/Player.md')
foreach ($Property in @('Health','MaxHealth')) {
    if (!$Bootstrap.Contains(('Key == "{0}"' -f $Property)) -or !$PlayerReference.Contains(('`Player.{0}`' -f $Property))) {
        throw "Player property differs between bootstrap and reference: $Property"
    }
}
foreach ($Method in @('CountItem','HasItem','Teleport')) {
    if (!$Bootstrap.Contains(('function PlayerMethods.{0}' -f $Method)) -or !$PlayerReference.Contains(('`Player:{0}' -f $Method))) {
        throw "Player method differs between bootstrap and reference: $Method"
    }
}
$ItemsReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Services/Items.md')
if (!$Bootstrap.Contains('function Items.Exists') -or !$ItemsReference.Contains('`Items:Exists')) {
    throw 'Items service differs between bootstrap and reference'
}
foreach ($Claim in @('128 direct entries','main, belt and wear','PREPARE/VERIFY','`25353106`')) {
    if (!$Player1C.Contains($Claim)) { throw "Player-1C evidence omits: $Claim" }
}
foreach ($Constructor in @('ImageSource.None','ImageSource.Sprite','ImageSource.Png','ImageSource.Item','ImageSource.SteamAvatar')) {
    if (!$GuiReference.Contains($Constructor) -or !$Bootstrap.Contains(($Constructor -split '\.')[1])) { throw "GUI image constructor differs between bootstrap and reference: $Constructor" }
}
foreach ($Member in @('GuiFont.RobotoCondensedRegular','GuiFont.RobotoCondensedBold','GuiFont.DroidSansMono','GuiFont.PermanentMarker')) {
    if (!$GuiReference.Contains($Member) -or !$Bootstrap.Contains(($Member -split '\.')[1])) { throw "GUI font member differs between bootstrap and reference: $Member" }
}
if ($Bootstrap -match 'function\s+GuiFont\.' -or $Bootstrap -match '(?i)GuiFont.*\.ttf') {
    throw 'GuiFont must expose singleton values without constructors or host font paths'
}
if ($GuiDescriptors -match '\bCanvasPosition\b' -or $Bootstrap -match '\bCanvasPosition\b') {
    throw 'GUI Foundation 3D must not expose retained or readable CanvasPosition'
}
foreach ($Claim in @('desired server state','does not transfer','not acknowledged','Player')) {
    if (!$GuiGuide.Contains($Claim)) { throw "GUI guide is missing required observable-semantics claim: $Claim" }
}
if ($GuiDescriptors -match '\bTextBox\b|\bSubmitted\b' -or $Bootstrap -match '\bTextBox\b|\bSubmitted\b') {
    throw 'Deferred TextBox or Submitted leaked into the production descriptor/bootstrap surface'
}
foreach ($Document in @($GuiGuide,$GuiReference,$ReleaseNotes,(Get-Content -Raw -LiteralPath (Join-Path $Root 'README.md')),
        (Get-Content -Raw -LiteralPath (Join-Path $Root 'CHANGELOG.md')))) {
    if (!$Document.Contains('TextBox') -or !$Document.Contains('not implemented')) {
        throw 'Public release documentation must identify TextBox as not implemented'
    }
}
Write-Output '[CarbonLuau:ApiTest] PASS version identity, gameplay/addon/GUI surface audit, runnable examples and relative links; runtime suite loads examples'
