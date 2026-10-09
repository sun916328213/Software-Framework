[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$baseline = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/Architecture-S5.json') -Raw | ConvertFrom-Json
$manifestPath = Join-Path $PSScriptRoot 'Baselines/Architecture-S6-migrations.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
function Assert-Hash([string]$Path, [string]$Hash) {
    $absolute = Join-Path $repo $Path
    if (!(Test-Path -LiteralPath $absolute -PathType Leaf)) { throw "Missing file: $Path" }
    if ((Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Hash) {
        throw "Content changed: $Path"
    }
}
$changes = @{}
foreach ($change in $manifest.changes) {
    $changes[$change.oldPath] = $change
    if ($change.oldPath -ne $change.newPath -and (Test-Path -LiteralPath (Join-Path $repo $change.oldPath))) {
        throw "Old compatibility entry remains: $($change.oldPath)"
    }
    Assert-Hash $change.newPath $change.newSha256
}
foreach ($category in @('sources', 'projects', 'xaml', 'configuration', 'solutions')) {
    foreach ($entry in $baseline.$category) {
        if ($changes.ContainsKey($entry.path)) {
            if ($changes[$entry.path].oldSha256 -ne $entry.sha256) { throw "Incorrect S5 origin: $($entry.path)" }
        }
        else { Assert-Hash $entry.path $entry.sha256 }
    }
}
foreach ($entry in $manifest.addedSources) { Assert-Hash $entry.path $entry.sha256 }
$after = Get-Content -LiteralPath (Join-Path $repo $manifest.afterSnapshot) -Raw | ConvertFrom-Json
foreach ($category in @('sources', 'projects', 'xaml', 'configuration', 'solutions')) {
    $expectedPaths = @(foreach ($entry in $baseline.$category) {
        if ($changes.ContainsKey($entry.path)) { $changes[$entry.path].newPath }
        else { $entry.path }
    })
    if ($category -eq 'sources') { $expectedPaths += @($manifest.addedSources | ForEach-Object path) }
    $differences = @(Compare-Object ($expectedPaths | Sort-Object) (@($after.$category | ForEach-Object path) | Sort-Object))
    if ($differences.Count) { throw "Unreviewed file inventory change: $category" }
}
$core = Join-Path $repo 'Software Frame/Vision/Vision.Core'
[xml]$project = Get-Content -LiteralPath (Join-Path $core 'Vision.Core.csproj') -Raw
if ($project.Project.PropertyGroup.TargetFramework -ne 'net9.0' -or
    @($project.SelectNodes('//UseWPF|//ProjectReference|//FrameworkReference')).Count) {
    throw 'Vision.Core desktop/project dependency returned.'
}
$files = @(Get-ChildItem -LiteralPath $core -Recurse -Filter '*.cs' -File |
    Where-Object FullName -notmatch '[\\/](bin|obj)[\\/]')
if ($files.Count -lt 25) { throw 'Core scan is incomplete.' }
foreach ($file in $files) {
    $source = [IO.File]::ReadAllText($file.FullName)
    $source = [regex]::Replace($source, '/\*.*?\*/|//[^\r\n]*', '', 'Singleline')
    if ($source -match '\bSystem\.Windows\b') { throw "Desktop namespace in Core: $($file.FullName)" }
}
# Temporary helper has no package references and uses the SDK compiler libraries.
$tempRoot = [IO.Path]::GetFullPath($env:TEMP)
$helper = Join-Path $tempRoot ('architecture-s6-guard-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $helper | Out-Null
$projectText = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.CodeAnalysis"><HintPath>$(MSBuildSDKsPath)/../Roslyn/bincore/Microsoft.CodeAnalysis.dll</HintPath></Reference>
    <Reference Include="Microsoft.CodeAnalysis.CSharp"><HintPath>$(MSBuildSDKsPath)/../Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll</HintPath></Reference>
  </ItemGroup>
</Project>
'@
try {
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -or $sdkVersion -notmatch '^\d+\.') { throw 'Cannot determine installed SDK.' }
    $sdkMajor = $sdkVersion.Split('.')[0]
    $projectText = $projectText.Replace('net9.0', "net$sdkMajor.0")
    [IO.File]::WriteAllText((Join-Path $helper 'Guard.csproj'), $projectText)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'S6ContentGuard.cs') -Destination (Join-Path $helper 'Program.cs')
    & dotnet run --project (Join-Path $helper 'Guard.csproj') -c Release -- $repo $manifestPath
    if ($LASTEXITCODE) { throw "Business content guard failed: $LASTEXITCODE" }
}
finally {
    $resolved = [IO.Path]::GetFullPath($helper)
    if (!$resolved.StartsWith($tempRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Temporary cleanup escaped the task directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host 'S6 boundary verified: declared migrations only; all other source, XAML, configuration and solutions unchanged.'
