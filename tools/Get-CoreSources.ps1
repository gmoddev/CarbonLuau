function Get-CoreSources {
    param([Parameter(Mandatory)][string]$Root)
    $CoreRoot = [IO.Path]::GetFullPath((Join-Path $Root 'src/CarbonLuau.Core'))
    $Project = [xml](Get-Content -Raw -LiteralPath (Join-Path $CoreRoot 'SharedSources.props'))
    foreach ($Entry in $Project.Project.ItemGroup.Compile) {
        $Relative = [string]$Entry.Include
        $Prefix = '$(MSBuildThisFileDirectory)'
        if (!$Relative.StartsWith($Prefix, [StringComparison]::Ordinal)) { throw 'Invalid Core source include' }
        $Path = [IO.Path]::GetFullPath((Join-Path $CoreRoot $Relative.Substring($Prefix.Length)))
        if (!$Path.StartsWith($CoreRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetExtension($Path) -ne '.cs') { throw 'Core source escapes allowlist' }
        Get-Item -LiteralPath $Path
    }
}
