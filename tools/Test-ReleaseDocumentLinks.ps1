$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseDocumentLinks.ps1')
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Revision = 'dabb872094f74e60d75de099379d198fb63a6bf8'
$Map = @{}
foreach ($Name in @('docs/api/Inventory-Migration.md', 'docs/api/README.md', 'examples/player-inventory/init.luau')) {
    $Map[[IO.Path]::GetFullPath([IO.Path]::Combine($Root, $Name))] = $Name
}
function Assert-Link {
    param([string]$Source, [string]$Name, [string]$InputText, [string]$Expected)
    $Actual = Convert-ReleaseDocumentLinks -Text $InputText -Root $Root -Source (Join-Path $Root $Source) `
        -ArchiveName $Name -SourceRevision $Revision -DocumentMap $Map
    if ($Actual -cne $Expected) { throw "[CarbonLuau:ReleaseLinks] Mismatch: $Actual" }
}
Assert-Link 'docs/releases/0.7.0.md' 'RELEASE-NOTES.md' '[migration](../api/Inventory-Migration.md)' '[migration](docs/api/Inventory-Migration.md)'
Assert-Link 'docs/Installation.md' 'INSTALLATION.md' '[api](api/README.md)' '[api](docs/api/README.md)'
Assert-Link 'docs/api/Types/PlayerInventory.md' 'docs/api/Types/PlayerInventory.md' '[example](../../../examples/player-inventory/init.luau)' '[example](../../../examples/player-inventory/init.luau)'
Assert-Link 'docs/api/Gui.md' 'GUI.md' '[api](README.md)' '[api](docs/api/README.md)'
Assert-Link 'docs/api/Types/PlayerInventory.md' 'docs/api/Types/PlayerInventory.md' '[proof](../../GameplayC4-Validation.md#verdict)' "[proof](https://github.com/gmoddev/CarbonLuau/blob/$Revision/docs/GameplayC4-Validation.md#verdict)"
Assert-Link 'docs/Installation.md' 'INSTALLATION.md' '[local](#install) [web](https://example.com/a) [mail](mailto:test@example.invalid)' '[local](#install) [web](https://example.com/a) [mail](mailto:test@example.invalid)'
Assert-Link 'docs/Installation.md' 'INSTALLATION.md' '' ''
try {
    Convert-ReleaseDocumentLinks -Text '[escape](../../outside.md)' -Root $Root -Source (Join-Path $Root 'docs/Installation.md') `
        -ArchiveName 'INSTALLATION.md' -SourceRevision $Revision -DocumentMap $Map | Out-Null
    throw '[CarbonLuau:ReleaseLinks] Escaping fixture accepted'
} catch {
    if ($_.Exception.ToString() -notmatch 'Document target escapes repository') { throw }
}
Write-Output '[CarbonLuau:ReleaseLinks] PASS relocation, offline links, immutable evidence fallback, anchors/external links and escape rejection'
