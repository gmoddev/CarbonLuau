# Bundle navigation follows the document's source path, not the relocated alias.
# Missing offline evidence pages use immutable source-revision GitHub links.
function Convert-ReleaseDocumentLinks {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Text,
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$ArchiveName,
        [Parameter(Mandatory)][string]$SourceRevision,
        [Parameter(Mandatory)][hashtable]$DocumentMap
    )
    if ($SourceRevision -cnotmatch '^[0-9a-f]{40}$') { throw '[CarbonLuau:ReleaseLinks] Invalid source revision' }
    $RootPath = [IO.Path]::GetFullPath($Root)
    $SourcePath = [IO.Path]::GetFullPath($Source)
    $BundleSource = [IO.Path]::Combine($RootPath, $ArchiveName)
    $Evaluator = [Text.RegularExpressions.MatchEvaluator]{
        param($Match)
        $Link = $Match.Groups[1].Value
        if ($Link.StartsWith('#') -or $Link -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $Link.StartsWith('//')) { return $Match.Value }
        # Repository Markdown uses inline relative paths; titles and reference-style
        # links are not rewritten. The document check remains their policy owner.
        $Parts = $Link -split '#', 2
        $Path = [Uri]::UnescapeDataString($Parts[0])
        if ($Path -match '\s' -or $Path.Contains('?') -or [IO.Path]::IsPathRooted($Path)) { return $Match.Value }
        $Target = [IO.Path]::GetFullPath([IO.Path]::Combine([IO.Path]::GetDirectoryName($SourcePath), $Path))
        if (!$Target.StartsWith($RootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw '[CarbonLuau:ReleaseLinks] Document target escapes repository'
        }
        $Fragment = if ($Parts.Count -eq 2) { '#' + $Parts[1] } else { '' }
        if ($DocumentMap.ContainsKey($Target)) {
            $BundleTarget = [IO.Path]::Combine($RootPath, [string]$DocumentMap[$Target])
            $Output = [IO.Path]::GetRelativePath([IO.Path]::GetDirectoryName($BundleSource), $BundleTarget).Replace('\','/')
        } else {
            $Relative = [IO.Path]::GetRelativePath($RootPath, $Target).Replace('\','/')
            $Escaped = ($Relative.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
            $Output = 'https://github.com/gmoddev/CarbonLuau/blob/' + $SourceRevision + '/' + $Escaped
        }
        return '](' + $Output + $Fragment + ')'
    }
    return [Text.RegularExpressions.Regex]::Replace($Text, '\]\(([^)]+)\)', $Evaluator)
}
