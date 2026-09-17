$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Bootstrap = Get-Content -Raw -LiteralPath (Join-Path $Root 'scripts/bootstrap.luau')
$Managed = Get-Content -Raw -LiteralPath (Join-Path $Root 'src/CarbonLuau/Facade/FacadePolicy.cs')
$Release = Get-Content -Raw -LiteralPath (Join-Path $Root 'release.json') | ConvertFrom-Json
$Version = $Release.apiVersion
foreach ($Text in @($Bootstrap,$Managed,(Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Globals.md')),
    (Get-Content -Raw -LiteralPath (Join-Path $Root 'docs/api/Compatibility.md')))) {
    if (!$Text.Contains($Version)) { throw 'API version differs between runtime and documentation' }
}
foreach ($Service in @('Players','Commands')) {
    if (!$Bootstrap.Contains(('if Name == "{0}"' -f $Service))) { throw "Missing registered service: $Service" }
    if (!(Test-Path -LiteralPath (Join-Path $Root "docs/api/Services/$Service.md"))) { throw "Missing service reference: $Service" }
}
foreach ($Type in @('Player','CommandContext','Signal','Connection')) {
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
foreach ($Example in @('player-events','hello-command')) {
    if (!(Test-Path -LiteralPath (Join-Path $Root "examples/$Example/init.luau"))) { throw "Missing runnable example: $Example" }
}
foreach ($Path in @('examples/addons/economy/addon.json','examples/addons/economy/init.luau',
        'examples/addons/economy/api.luau','examples/addons/economy/formatting.luau',
        'examples/addons/shop/addon.json','examples/addons/shop/init.luau')) {
    if (!(Test-Path -LiteralPath (Join-Path $Root $Path))) { throw "Missing addon example file: $Path" }
}
Write-Output '[CarbonLuau:ApiTest] PASS version identity, canonical services/types, example presence and relative links; runtime suite loads examples'
