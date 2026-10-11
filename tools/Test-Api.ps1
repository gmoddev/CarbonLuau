param([switch]$D3Only, [switch]$D3DocumentsOnly)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
function Test-D3Documents {
    $Paths = @('docs/api/Services/Loot.md','docs/api/Types/Enum.md','docs/api/Types/DropMode.md',
        'docs/api/Types/LootDropEntry.md','docs/api/Globals.md','docs/api/README.md','docs/_sidebar.md')
    $Navigation = (Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/_sidebar.md')) +
        (Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/README.md'))
    foreach ($Path in $Paths) {
        $Document = Get-Item -LiteralPath (Join-Path $Root $Path)
        $Text = Get-Content -Raw -LiteralPath $Document.FullName
        foreach ($Match in [regex]::Matches($Text, '\]\(([^)]+)\)')) {
            $Target = $Match.Groups[1].Value
            if ($Target -match '^(https?://|mailto:|#)') { continue }
            $Target = ($Target -split '#')[0]
            if (!(Test-Path -LiteralPath (Join-Path $Document.DirectoryName $Target))) {
                throw "Broken D3 navigation/reference link: ${Path}: $Target"
            }
        }
        if ($Path -match '^docs/api/(Services/Loot|Types/(Enum|DropMode|LootDropEntry))\.md$' -and
            (!$Text.Contains('0.6.5-experimental') -or !$Navigation.Contains(($Path -replace '^docs/', '')))) {
            throw "D3 reference identity/navigation missing: $Path"
        }
    }
    $LootReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Services/Loot.md')
    foreach ($Claim in @('Loot:RegisterDropRule(Prefab: string, Mode: DropMode, Drops: {LootDropEntry}): ()',
        'Exactly three arguments','during initialization','stages a rule','returns no values',
        'assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab','zero through eight',
        'scrap','metal.fragments','0 <= Min <= Max <= 1000','8000','One rule is allowed globally',
        'eight world payouts','eight','events per actual server frame','one event in flight',
        'earlier fill/generation','Before commit','leaves vanilla','After commit',
        'partial payout','no vanilla fallback','replay or compensation','restart','source gates')) {
        if (!$LootReference.Contains($Claim)) { throw "D3 public contract missing: $Claim" }
    }
    $EntryReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/LootDropEntry.md')
    $EnumReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/Enum.md')
    $ModeReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/DropMode.md')
    foreach ($Reference in @($LootReference,$EntryReference)) {
        foreach ($Claim in @('fresh small table','preallocated','controlled Luau resource error',
            '64 allocated array slots','64 allocated hash','logical limit of eight')) {
            if (!$Reference.Contains($Claim)) { throw "D3 table resource contract missing: $Claim" }
        }
    }
    if (!$EntryReference.Contains('export type LootDropEntry = {Item: string, Min: number, Max: number}') -or
        !$EnumReference.Contains('frozen global namespace') -or !$EnumReference.Contains('GiveItemBehavior') -or
        !$ModeReference.Contains('sealed immutable userdata') -or !$ModeReference.Contains('DropMode.Replace')) {
        throw 'D3 namespace/typed value/ordinary entry documentation drift'
    }
    $Example = Get-Content -Raw -LiteralPath (Join-Path $Root 'examples/scripts/loot-replace.luau')
    if (!$Example.Contains('game:GetService("Loot")') -or
        !$Example.Contains('Loot:RegisterDropRule(Prefab, Enum.DropMode.Replace, Drops)') -or
        $Example -match 'Commands:Register|:Connect\(|task\.|Enum\.DropMode\.(Get|new)') {
        throw 'D3 example must declare the supported positional rule during initialization'
    }
}
function Test-D3Capacity([string]$Bootstrap, [string]$Facade) {
    # Source audit only: capacity precedes next's scan of empty backing slots.
    # Exact-host/runtime tests remain the native and public fixture owners.
    $Rule = [regex]::Match($Bootstrap, '(?s)function Loot\.RegisterDropRule\(Self, \.\.\.\)\r?\n(?<Body>.*?)\r?\nend').Groups['Body'].Value
    foreach ($Table in @('Drops','Entry')) {
        $Guard = $Rule.IndexOf("CheckLootTable($Table)", [StringComparison]::Ordinal)
        $Scan = $Rule.IndexOf("for Key in next, $Table do", [StringComparison]::Ordinal)
        if ($Guard -lt 0 -or $Scan -lt 0 -or $Guard -ge $Scan) {
            throw "D3 table capacity guard must precede next: $Table"
        }
    }
    if ($Rule -notmatch 'if Count > 8 then Fail\(' -or $Rule -notmatch 'if Keys > 3 or' -or
        $Bootstrap -notmatch 'local Host, MakeUserdata, SharedValueEqual, StorageService, EntityIdentityRecords, SubmitDiscovery, CheckLootTable = \.\.\.') {
        throw 'D3 logical visit bounds/private capacity-helper injection drift'
    }
    $GuardBody = [regex]::Match($Facade, '(?s)int CheckLootTable\(lua_State\* State\)\r?\n\{(?<Body>.*?)\r?\n\}').Groups['Body'].Value
    if ($GuardBody -notmatch 'lua_gettop\(State\) != 1' -or $GuardBody -notmatch 'lua_type\(State, 1\) != LUA_TTABLE' -or
        $GuardBody -notmatch 'static_cast<const LuaTable\*>\(lua_topointer\(State, 1\)\)' -or
        $GuardBody -notmatch 'Value->sizearray > 64' -or $GuardBody -notmatch 'Value->lsizenode > 6' -or
        !$GuardBody.Contains('luaL_error(State, "loot declaration table capacity exceeds limit; use a small fresh table")') -or
        $GuardBody -match '\blua_(next|call|pcall|newtable|createtable)\s*\(' -or
        $Facade -notmatch 'lua_pushcfunction\(State, CheckLootTable, "private loot table bound"\);\s*lua_call\(State, 7, 6\);') {
        throw 'D3 native scalar capacity guard/private seventh bootstrap argument drift'
    }
}
function Test-D3Metadata($Catalog, [string]$Definitions, [string]$Bootstrap) {
    $LootTypes = @('Enum','DropMode','Loot','LootDropEntry')
    foreach ($Name in $LootTypes) {
        $Type = @($Catalog.Types | Where-Object { $_.Id -ceq $Name })
        $Kind = switch ($Name) { 'Enum' { 'Global' } 'DropMode' { 'Enum' } 'Loot' { 'Service' } default { 'Value' } }
        if ($Type.Count -ne 1 -or $Type[0].Kind -cne $Kind) { throw "D3 type classification drift: $Name" }
    }
    foreach ($Declaration in @($Catalog.Types) + @($Catalog.Members)) {
        if ($Declaration.Id -cin $LootTypes -or $Declaration.OwnerId -cin $LootTypes) {
            $Preview = if ($Declaration.Id -ceq 'LootDropEntry') { 'Unavailable' }
                elseif ($Declaration.Id -ceq 'Loot' -or $Declaration.OwnerId -ceq 'Loot') { 'HostMutation' } else { 'Pure' }
            if ($Declaration.Availability.SinceApi -cne '0.6.5-experimental' -or
                $Declaration.Availability.Qualification -cne 'Experimental' -or $Declaration.Preview -cne $Preview) {
                throw "D3 availability/tooling classification drift: $($Declaration.Id)"
            }
        }
    }
    foreach ($Owner in $LootTypes) {
        $Members = @($Catalog.Members | Where-Object { $_.OwnerId -ceq $Owner } | Sort-Object Name)
        $Expected = switch ($Owner) { 'Enum' { 'DropMode' } 'DropMode' { 'Replace' } 'Loot' { 'RegisterDropRule' } default { '' } }
        if (($Members.Name -join ',') -cne $Expected) { throw "Unexpected D3 public member: $Owner" }
    }
    $Namespace = @($Catalog.Members | Where-Object { $_.Id -ceq 'Enum.DropMode' })[0]
    $Replace = @($Catalog.Members | Where-Object { $_.Id -ceq 'DropMode.Replace' })[0]
    $Entry = @($Catalog.Types | Where-Object { $_.Id -ceq 'LootDropEntry' })[0]
    if ($Namespace.Kind -cne 'Property' -or $Namespace.Writable -or $Namespace.ValueType -cne 'DropMode' -or
        $Replace.Kind -cne 'Singleton' -or $Replace.Writable -or $Replace.ValueType -cne 'DropMode' -or
        $Entry.Representation -cne 'Alias' -or $Entry.TypeExpression -cne '{Item: string, Min: number, Max: number}') {
        throw 'D3 frozen namespace/nominal value/alias metadata drift'
    }
    $Rule = @($Catalog.Members | Where-Object { $_.Id -ceq 'Loot.RegisterDropRule' })[0]
    $Parameters = @($Rule.Signatures[0].Parameters)
    if ($Rule.Kind -cne 'Method' -or $Rule.Signatures.Count -ne 1 -or $Parameters.Count -ne 3 -or
        ($Parameters.Name -join ',') -cne 'Prefab,Mode,Drops' -or
        ($Parameters.Type -join ',') -cne 'string,DropMode,{LootDropEntry}' -or
        @($Parameters | Where-Object { $_.Optional -or $_.Variadic }).Count -ne 0 -or
        $Rule.Signatures[0].Returns.Count -ne 0) { throw 'D3 must have exactly three required positional arguments and no return' }
    foreach ($Signature in @("declare Enum: {`n    read DropMode: {read Replace: DropMode},`n}",
        "declare extern type DropMode with`nend", 'export type LootDropEntry = {Item: string, Min: number, Max: number}',
        'function RegisterDropRule(self, Prefab: string, Mode: DropMode, Drops: {LootDropEntry}): ()',
        'function GetService(self, Name: "Loot"): (Loot)',
        "declare GiveItemBehavior: {`n    read InventoryOnly: GiveItemBehavior,`n}")) {
        if (!$Definitions.Replace("`r`n", "`n").Contains($Signature)) { throw "Generated D3 definition drift: $Signature" }
    }
    if ($Definitions -match '(?m)^declare DropMode:' -or
        $Definitions -match 'function GetService\(self, Name: "(Enum|DropMode|LootDropEntry)"\)' -or
        !$Bootstrap.Contains('if Name == "Loot" then return Loot end')) { throw 'D3 global/service exposure drift' }
    $Native = Get-Content -Raw -LiteralPath (Join-Path $Root 'native/src/scripts/ModuleLoader.cpp')
    $Globals = @([regex]::Matches($Native, 'for \(const char\* Name : \{([^\r\n]+)\}') |
        Where-Object { $_.Groups[1].Value.Contains('"GiveItemBehavior"') })
    if ($Globals.Count -ne 1 -or !$Globals[0].Groups[1].Value.Contains('"Enum"') -or
        $Globals[0].Groups[1].Value.Contains('"DropMode"')) { throw 'Native enum globals differ from generated namespace' }
}
Test-D3Documents
Test-D3Capacity (Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau')) `
    (Get-Content -Raw -LiteralPath (Join-Path $Root 'native/src/facade/FacadeBridge.cpp'))
if ($D3DocumentsOnly) {
    Write-Output '[CarbonLuau:ApiTest] PASS focused D3 public contract, capacity-guard source audit, example and navigation; metadata/runtime qualification separate'
    return
}
if ($D3Only) {
    Test-D3Metadata (Get-Content -Raw -LiteralPath (Join-Path $Root 'api/carbonluau-api.json') | ConvertFrom-Json) `
        (Get-Content -Raw -LiteralPath (Join-Path $Root 'generated/carbonluau.d.luau')) `
        (Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau'))
    Write-Output '[CarbonLuau:ApiTest] PASS focused D3 public contract, metadata, definitions, example and navigation; runtime qualification separate'
    return
}
$Bootstrap = Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau')
$Managed = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Facade/FacadePolicy.cs')
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
$Version = $Release.apiVersion
$GuiGuide = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Gui.md')
$GuiReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Gui-Reference.md')
$GuiDescriptors = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau.Core/Gui/SharedGuiDescriptors.cs')
$ReleaseNotes = Get-Content -Raw -LiteralPath (Join-Path $Root "docs/releases/$($Release.releaseVersion).md")
$Sidebar = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/_sidebar.md')
$HomeDocument = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/Home.md')
if (!$Sidebar.Contains("releases/$($Release.releaseVersion).md") -or
    !$HomeDocument.Contains("v$($Release.releaseVersion)")) { throw 'Current release is missing from hosted documentation navigation' }
foreach ($QueryDocument in @('DataStoreOptions','DataStoreQuery','DataStoreQueryResult')) {
    if (!$Sidebar.Contains("api/Types/$QueryDocument.md")) { throw "Query reference missing from hosted documentation navigation: $QueryDocument" }
}
foreach ($WorldDocument in @('api/Services/Workspace.md','api/Types/Entity.md','api/Types/EntityDestroyedContext.md','api/World-Examples.md')) {
    if (!$Sidebar.Contains($WorldDocument)) { throw "World reference missing from hosted documentation navigation: $WorldDocument" }
}
$Player1C = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/PlayerInteractionFoundation1C.md')
$Player1D = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/PlayerInteractionFoundation1D.md')
$Player1FA = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/PlayerInteractionFoundation1FA.md')
foreach ($Text in @($Bootstrap,$Managed,(Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Globals.md')),
    (Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Compatibility.md')))) {
    if (!$Text.Contains($Version)) { throw 'API version differs between runtime and documentation' }
}
foreach ($Service in @('Players','Commands','Gui','Items','Workspace','DataStoreService','Loot')) {
    if (!$Bootstrap.Contains(('if Name == "{0}"' -f $Service))) { throw "Missing registered service: $Service" }
    if ($Service -ne 'Gui' -and !(Test-Path -LiteralPath (Join-Path $Root "docs/api/Services/$Service.md"))) { throw "Missing service reference: $Service" }
}
foreach ($Type in @('Player','Vector3','CommandContext','Signal','Connection','GiveItemBehavior','Entity','EntityDestroyedContext','EntityDeathContext',
        'DataStore','DataStoreOptions','DataStoreQuery','DataStoreQueryResult','PersistedValue','Enum','DropMode','LootDropEntry')) {
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
foreach ($Example in @('player-events','player-position','player-health','player-inventory','player-teleport','player-take-item','hello-command','player-status','player-give-item','player-shop','gui/inventory-reward')) {
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
foreach ($Method in @('CountItem','HasItem','Teleport','TakeItem','GiveItem')) {
    if (!$Bootstrap.Contains(('function PlayerMethods.{0}' -f $Method)) -or !$PlayerReference.Contains(('`Player:{0}' -f $Method))) {
        throw "Player method differs between bootstrap and reference: $Method"
    }
}
$ItemsReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Services/Items.md')
$BehaviorReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/GiveItemBehavior.md')
if (!$Bootstrap.Contains('GiveItemBehavior.InventoryOnly') -or !$BehaviorReference.Contains('GiveItemBehavior.InventoryOnly') -or
    !$PlayerReference.Contains('Behavior: GiveItemBehavior?')) { throw 'GiveItem typed behavior differs between runtime and public reference' }
if (!$Bootstrap.Contains('function Items.Exists') -or !$ItemsReference.Contains('`Items:Exists')) {
    throw 'Items service differs between bootstrap and reference'
}
foreach ($Claim in @('128 direct entries','main, belt and wear','PREPARE/VERIFY','`25353106`')) {
    if (!$Player1C.Contains($Claim)) { throw "Player-1C evidence omits: $Claim" }
}
foreach ($Claim in @('IMPLEMENTED / AUTHENTICATED-CLIENT UNQUALIFIED','`25353106`','`74b9b48e9375177bc17e76a4d939956d1076645b`','no values')) {
    if (!$Player1D.Contains($Claim)) { throw "Player-1D evidence omits: $Claim" }
}
foreach ($Claim in @('PREPARE, COMMIT and VERIFY','Player.inventory.Take(null','Q0 - Q1 == requestedAmount','1,000 successful','GiveItem')) {
    if (!$Player1FA.Contains($Claim)) { throw "Player-1F-A evidence omits: $Claim" }
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
$Persistence = Get-Content -Raw -LiteralPath (Join-Path $Root 'native/src/facade/PersistenceFacade.cpp')
$StoreReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/DataStore.md')
$ServiceReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Services/DataStoreService.md')
$Catalog = Get-Content -Raw -LiteralPath (Join-Path $Root 'api/carbonluau-api.json') | ConvertFrom-Json
$Definitions = Get-Content -Raw -LiteralPath (Join-Path $Root 'generated/carbonluau.d.luau')
Test-D3Metadata $Catalog $Definitions $Bootstrap
$PersistenceTypes = @('DataStoreService','DataStore','DataStoreOptions','DataStoreQuery','DataStoreQueryResult','PersistedValue')
$EntityTypes = @('Workspace','Entity','EntityDiscoveryOptions')
$GameplayTypes = @('PlayerDeathContext','PlayerSpawnContext','EntityDestroyedContext','EntityDeathContext')
$LootTypes = @('Enum','DropMode','Loot','LootDropEntry')
foreach ($Declaration in @($Catalog.Types) + @($Catalog.Members)) {
    $IsPersistence = $Declaration.Id -cin $PersistenceTypes -or $Declaration.OwnerId -cin $PersistenceTypes
    if ($IsPersistence) {
        if ($Declaration.Availability.SinceApi -cne '0.5.0-experimental' -or
            $Declaration.Availability.Qualification -cne 'Experimental' -or $Declaration.Preview -cne 'Unavailable') {
            throw "Persistence availability/qualification differs: $($Declaration.Id)"
        }
    } elseif ($Declaration.Id -cin @('Workspace.EntitySpawned','Workspace.EntityDestroyed','Workspace.EntityDied')) {
        if ($Declaration.Availability.SinceApi -cne '0.6.5-experimental' -or
            $Declaration.Availability.Qualification -cne 'Experimental' -or $Declaration.Preview -cne 'Unavailable') {
            throw "Workspace gameplay Signal availability/qualification differs: $($Declaration.Id)"
        }
    } elseif ($Declaration.Id -cin $EntityTypes -or $Declaration.OwnerId -cin $EntityTypes) {
        if ($Declaration.Availability.SinceApi -cne '0.6.0-experimental' -or
            $Declaration.Availability.Qualification -cne 'Experimental' -or $Declaration.Preview -cne 'Unavailable') {
            throw "Entity availability/qualification differs: $($Declaration.Id)"
        }
    } elseif ($Declaration.Id -ceq 'PlayerInventory.ItemChanged' -or $Declaration.Id -ceq 'InventoryChangeContext' -or $Declaration.OwnerId -ceq 'InventoryChangeContext') {
        if ($Declaration.Availability.SinceApi -cne '0.6.5-experimental' -or $Declaration.Preview -cne 'Unavailable') {
            throw 'Sampled inventory Signal metadata identity/preview differs'
        }
    } elseif ($Declaration.Id -ceq 'PlayerInventory' -or $Declaration.OwnerId -ceq 'PlayerInventory' -or $Declaration.Id -ceq 'Player.GetInventory') {
        if ($Declaration.Availability.SinceApi -cne '0.6.5-experimental' -or $Declaration.Preview -cne 'ReadMock') {
            throw 'PlayerInventory metadata identity/preview differs'
        }
    } elseif ($Declaration.Id -cin $GameplayTypes -or $Declaration.OwnerId -cin $GameplayTypes -or
        $Declaration.Id -cin @('Players.PlayerDied','Players.PlayerSpawned')) {
        if ($Declaration.Availability.SinceApi -cne '0.6.5-experimental' -or
            $Declaration.Availability.Qualification -cne 'Experimental' -or $Declaration.Preview -cne 'Unavailable') {
            throw "Gameplay context availability/qualification differs: $($Declaration.Id)"
        }
    } elseif ($Declaration.Id -cin $LootTypes -or $Declaration.OwnerId -cin $LootTypes) {
        # Exact D3 identity and per-declaration preview were audited above.
    } elseif ([version]($Declaration.Availability.SinceApi -split '-')[0] -gt [version]'0.4.0') {
        throw "Historical introduction version advanced: $($Declaration.Id)"
    }
}
foreach ($Name in @('PlayerDied','PlayerSpawned')) {
    $Signal = @($Catalog.Members | Where-Object { $_.OwnerId -ceq 'Players' -and $_.Name -ceq $Name })
    if ($Signal.Count -ne 1 -or $Signal[0].Kind -cne 'Signal' -or
        $Signal[0].Signatures[0].Parameters.Count -ne 2 -or
        !$Definitions.Contains("read ${Name}: SignalWith<")) { throw "Gameplay A typed Signal differs: $Name" }
}
foreach ($Method in @('GetDataStore','GetAsync','SetAsync','RemoveAsync','Query')) {
    if (!$Persistence.Contains("int $Method(lua_State* State)") -or
        !($StoreReference + $ServiceReference).Contains(":$Method(")) { throw "Persistence binding/reference differs: $Method" }
}
foreach ($Signature in @('Callback: (PersistedValue?, string?) -> ()','Callback: (boolean?, string?) -> ()',
        'export type PersistedValue = boolean | number | string | {PersistedValue} | {[string]: PersistedValue}',
        'export type DataStoreOptions = {Indexes: {string}?}',
        'export type DataStoreQuery = {Field: string, Type: ("number" | "string" | "boolean")?',
        'export type DataStoreQueryResult = {Items: {{Key: string, Value: PersistedValue}}, NextCursor: string?}',
        'function Query(self, Request: DataStoreQuery, Callback: (DataStoreQueryResult?, string?) -> ()): ()',
        'function GetDataStore(self, StoreName: string, Options: DataStoreOptions?): (DataStore)')) {
    if (!$Definitions.Contains($Signature)) { throw "Generated persistence type differs: $Signature" }
}
$StoreMethods = @($Catalog.Members | Where-Object { $_.OwnerId -ceq 'DataStore' } | ForEach-Object Name | Sort-Object)
if (($StoreMethods -join ',') -cne 'GetAsync,Query,RemoveAsync,SetAsync') { throw 'Unexpected persistence public method' }
foreach ($Example in @('query-equals','query-range','query-top','query-pages','query-ambiguity')) {
    $ExampleText = Get-Content -Raw -LiteralPath (Join-Path $Root "examples/persistence/$Example/init.luau")
    if (!$ExampleText.Contains('Store:Query(') -or !$ExampleText.Contains('ErrorCode')) {
        throw "Query example lacks public API/error handling: $Example"
    }
}
foreach ($Example in @('exact-lookup','equality','string-id')) {
    $ExamplePath = Join-Path $Root "examples/world/$Example/init.luau"
    if (!(Test-Path -LiteralPath $ExamplePath)) { throw "Missing runnable world example: $Example" }
    $ExampleText = Get-Content -Raw -LiteralPath $ExamplePath
    if (!$ExampleText.Contains('GetEntityById(') -or $ExampleText -match ':Spawn\(|:Destroy\(|:IsValid\(') {
        throw "World example differs from read-only surface: $Example"
    }
}
$EntityMembers = @($Catalog.Members | Where-Object { $_.OwnerId -ceq 'Entity' } | ForEach-Object Name | Sort-Object)
if (($EntityMembers -join ',') -cne 'Id,Position,Prefab') { throw 'Unexpected Entity public member' }
$WorkspaceMembers = @($Catalog.Members | Where-Object { $_.OwnerId -ceq 'Workspace' } | ForEach-Object Name | Sort-Object)
if (($WorkspaceMembers -join ',') -cne 'EntityDestroyed,EntityDied,EntitySpawned,GetEntitiesInRadiusAsync,GetEntityById' -or
    !$Definitions.Contains('read EntitySpawned: SignalWith<') -or
    !$Definitions.Contains('read EntityDestroyed: SignalWith<(Context: EntityDestroyedContext) -> ()>') -or
    !$Definitions.Contains('read EntityDied: SignalWith<(Context: EntityDeathContext) -> ()>') -or
    !$Definitions.Contains('function GetEntitiesInRadiusAsync(self, Position: Vector3, Radius: number, Callback: ({Entity}?, string?) -> (), Options: EntityDiscoveryOptions?): ()') -or
    !$Definitions.Contains('function GetEntityById(self, Id: string): (Entity?)') -or
    !$Definitions.Contains('function GetService(self, Name: "Workspace"): (Workspace)')) {
    throw 'Workspace runtime/catalog/definition drift'
}
$DestroyedSignal = @($Catalog.Members | Where-Object { $_.Id -ceq 'Workspace.EntityDestroyed' })
if ($DestroyedSignal.Count -ne 1 -or $DestroyedSignal[0].Kind -cne 'Signal' -or
    $DestroyedSignal[0].Signatures.Count -ne 1 -or $DestroyedSignal[0].Signatures[0].Parameters.Count -ne 1 -or
    $DestroyedSignal[0].Signatures[0].Parameters[0].Type -cne 'EntityDestroyedContext' -or
    $DestroyedSignal[0].Signatures[0].Returns.Count -ne 0) { throw 'EntityDestroyed callback signature differs' }
$DestroyedFields = @($Catalog.Members | Where-Object { $_.OwnerId -ceq 'EntityDestroyedContext' } | Sort-Object Name)
if (($DestroyedFields.Name -join ',') -cne 'Id,Position,Prefab' -or
    @($DestroyedFields | Where-Object { $_.Kind -cne 'Property' -or $_.Writable }).Count -ne 0 -or
    ($DestroyedFields.ValueType -join ',') -cne 'string,Vector3?,string' -or
    !$Definitions.Contains('export type EntityDestroyedContext = {')) { throw 'EntityDestroyed snapshot-only context differs' }
$DestroyedExample = Get-Content -Raw -LiteralPath (Join-Path $Root 'examples/world/entity-destroyed/init.luau')
if (!$DestroyedExample.Contains('EntityDestroyed:Connect(') -or !$DestroyedExample.Contains('if Context.Position then') -or
    $DestroyedExample -match 'GetEntityById\(|:Spawn\(|:Destroy\(') { throw 'EntityDestroyed example invents live entity or mandatory position' }
$DeathSignal = @($Catalog.Members | Where-Object { $_.Id -ceq 'Workspace.EntityDied' })
if ($DeathSignal.Count -ne 1 -or $DeathSignal[0].Kind -cne 'Signal' -or
    $DeathSignal[0].Signatures.Count -ne 1 -or $DeathSignal[0].Signatures[0].Parameters.Count -ne 1 -or
    $DeathSignal[0].Signatures[0].Parameters[0].Name -cne 'Context' -or
    $DeathSignal[0].Signatures[0].Parameters[0].Type -cne 'EntityDeathContext' -or
    $DeathSignal[0].Signatures[0].Parameters[0].Optional -or $DeathSignal[0].Signatures[0].Parameters[0].Variadic -or
    $DeathSignal[0].Signatures[0].Returns.Count -ne 0) { throw 'EntityDied callback signature differs' }
$DeathFields = @($Catalog.Members | Where-Object { $_.OwnerId -ceq 'EntityDeathContext' } | Sort-Object Name)
if (($DeathFields.Name -join ',') -cne 'DamageType,Id,Killer,KillerId,Position,Prefab,Weapon' -or
    @($DeathFields | Where-Object { $_.Kind -cne 'Property' -or $_.Writable }).Count -ne 0 -or
    ($DeathFields.ValueType -join ',') -cne 'string?,string,Player?,string?,Vector3?,string,string?' -or
    !$Definitions.Contains('export type EntityDeathContext = {')) { throw 'EntityDeathContext seven-field snapshot differs' }
$DeathExample = Get-Content -Raw -LiteralPath (Join-Path $Root 'examples/world/entity-died/init.luau')
if (!$DeathExample.Contains('EntityDied:Connect(') -or !$DeathExample.Contains('if Context.Position then') -or
    !$DeathExample.Contains('if Context.KillerId then') -or !$DeathExample.Contains('if Context.DamageType then') -or
    !$DeathExample.Contains('if Context.Weapon then') -or $DeathExample -match 'GetEntityById\(|GetPlayerByUserId\(|:Spawn\(|:Destroy\(') {
    throw 'EntityDied example invents optional data, live Entity or account re-resolution'
}
$DeathReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/EntityDeathContext.md')
if (!$DeathReference.Contains('0.6.5-experimental') -or
    !$DeathReference.Contains('Actor attribution is qualified for the narrow original, synchronous RPC melee') -or
    !$DeathReference.Contains('supported exact host targets') -or !$DeathReference.Contains('original D11') -or
    !$DeathReference.Contains('writer scope at full death completion') -or
    !$DeathReference.Contains('Unknown provenance gives nil Killer and KillerId') -or
    !$DeathReference.Contains('Callbacks never resolve a UserId to select a replacement connection') -or
    $DeathReference.Contains('qualification is pending') -or !$DeathReference.Contains('frozen')) {
    throw 'EntityDeathContext reference omits source-qualified narrow RPC actor, immutable/unknown or no-retarget semantics'
}
$WorkspaceReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Services/Workspace.md')
$EntityReference = Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Types/Entity.md')
foreach ($Name in @('GetEntityById','first','restart')) {
    if (!$WorkspaceReference.Contains($Name)) { throw "Workspace reference omits $Name" }
}
foreach ($Name in @('Entity.Id','Entity.Prefab','Entity.Position','stale','compare equal')) {
    if (!$EntityReference.Contains($Name)) { throw "Entity reference omits $Name" }
}
foreach ($Example in @('get','set','remove','player-key','snapshot','errors')) {
    $ExampleText = Get-Content -Raw -LiteralPath (Join-Path $Root "examples/persistence/$Example/init.luau")
    if (!$ExampleText.Contains('v0.5 experimental example; see PersistenceFoundation1C.md for qualification and release limits') -or !$ExampleText.Contains('pcall')) {
        throw "Persistence example lacks qualification/submission handling: $Example"
    }
}
$PlayerKey = Get-Content -Raw -LiteralPath (Join-Path $Root 'examples/persistence/player-key/init.luau')
if (!$PlayerKey.Contains('tostring(Player.UserId)') -or $PlayerKey -match 'tonumber\s*\(') { throw 'Player persistence key loses string identity' }
Write-Output '[CarbonLuau:ApiTest] PASS identity, gameplay/addon/GUI/persistence surface audit, example presence and relative links; execution qualification is separate'
