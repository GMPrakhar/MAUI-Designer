[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Tag,

    [string] $ManifestPath = (
        Join-Path $PSScriptRoot '..\src\MauiDesigner.Vsix\source.extension.vsixmanifest'),

    [string] $AssemblyInfoPath = (
        Join-Path $PSScriptRoot '..\src\MauiDesigner.Vsix\Properties\AssemblyInfo.cs')
)

$match = [regex]::Match(
    $Tag,
    '^vsix-v(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)-beta\.(?<revision>0|[1-9]\d*)$')
if (-not $match.Success) {
    throw "Release tag '$Tag' must use the format vsix-v<major>.<minor>.<patch>-beta.<number>."
}

$parts = @('major', 'minor', 'patch', 'revision') |
    ForEach-Object { [int]::Parse($match.Groups[$_].Value) }
if ($parts | Where-Object { $_ -gt 65534 }) {
    throw "Release tag '$Tag' contains a version component greater than 65534."
}

$version = $parts -join '.'
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)

function Replace-SingleMatch {
    param(
        [Parameter(Mandatory)]
        [string] $Content,

        [Parameter(Mandatory)]
        [string] $Pattern,

        [Parameter(Mandatory)]
        [string] $Description
    )

    $regex = [regex]::new($Pattern)
    if ($regex.Matches($Content).Count -ne 1) {
        throw "Expected exactly one $Description declaration."
    }

    return $regex.Replace(
        $Content,
        { param($value) $value.Groups[1].Value + $version + $value.Groups[2].Value },
        1)
}

$manifest = [IO.File]::ReadAllText($ManifestPath)
$manifest = Replace-SingleMatch `
    -Content $manifest `
    -Pattern '(<Identity\b[^>]*\bVersion=")[^"]+("[^>]*>)' `
    -Description 'VSIX manifest version'
[IO.File]::WriteAllText($ManifestPath, $manifest, $utf8WithoutBom)

$assemblyInfo = [IO.File]::ReadAllText($AssemblyInfoPath)
foreach ($attribute in @('AssemblyVersion', 'AssemblyFileVersion')) {
    $assemblyInfo = Replace-SingleMatch `
        -Content $assemblyInfo `
        -Pattern ('(\[assembly:\s*' + $attribute + '\(")[^"]+("\)\])') `
        -Description $attribute
}
[IO.File]::WriteAllText($AssemblyInfoPath, $assemblyInfo, $utf8WithoutBom)

Write-Output $version
