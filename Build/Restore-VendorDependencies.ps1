[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'Dependencies/vendor-manifest.json'),
    [string]$VendorDllRoot = $env:FRAMEWORK_VENDOR_DLL_ROOT,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
function Resolve-ChildPath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative)) {
        throw "Expected relative dependency path: $Relative"
    }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $path = [IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if (-not $path.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Dependency path escapes its root: $Relative"
    }
    return $path
}
function Assert-Identity([string]$Path, [string]$Hash) {
    if ($Hash -notmatch '^[0-9a-fA-F]{64}$') { throw "Invalid SHA256 in manifest: $Path" }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) {
        throw "Dependency identity mismatch: $Path"
    }
}
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) { throw 'Unsupported dependency manifest schema.' }
$pending = @()
$missing = @()
$seen = @{}
foreach ($artifact in $manifest.artifacts) {
    $target = Resolve-ChildPath $RepositoryRoot $artifact.path
    if ($seen.ContainsKey($target)) { throw "Duplicate dependency target: $($artifact.path)" }
    $seen[$target] = $true
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Assert-Identity $target $artifact.sha256
        continue
    }
    $missing += $artifact.path
    if ($CheckOnly) { continue }
    if (-not $VendorDllRoot) { throw "Missing dependency; configure FRAMEWORK_VENDOR_DLL_ROOT: $($artifact.path)" }
    $source = Resolve-ChildPath $VendorDllRoot $artifact.source
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "SDK store missing dependency: $($artifact.source)" }
    Assert-Identity $source $artifact.sha256
    $pending += [pscustomobject]@{ Source = $source; Target = $target; Hash = $artifact.sha256 }
}
foreach ($dependency in $manifest.sourceDependencies) {
    if ($dependency.root) {
        $dependencyRoot = Resolve-ChildPath $RepositoryRoot $dependency.root
        $sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $dependencyRoot 'src') -File -Recurse |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs)[\\/]' -and
                $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.resx')
            })
        $sourceFiles += @(Get-ChildItem -LiteralPath $dependencyRoot -File |
            Where-Object { $_.Extension -in @('.props', '.targets') -or $_.Name -eq 'LICENSE' })
        $actualPaths = @($sourceFiles | ForEach-Object { [IO.Path]::GetRelativePath($RepositoryRoot, $_.FullName).Replace('\', '/') })
        $expectedPaths = @($dependency.files | ForEach-Object path)
        if (Compare-Object $expectedPaths $actualPaths) { throw "Source dependency inventory drift: $($dependency.name)" }
    }
    foreach ($file in $dependency.files) {
        $path = Resolve-ChildPath $RepositoryRoot $file.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing source dependency: $($file.path)" }
        Assert-Identity $path $file.sha256
    }
}
foreach ($package in $manifest.privatePackages) {
    [xml]$project = Get-Content -LiteralPath (Resolve-ChildPath $RepositoryRoot $package.sourceProject) -Raw
    if ($project.Project.PropertyGroup.Version -ne $package.version) { throw "Private package version drift: $($package.id)" }
    foreach ($consumer in $package.consumers) {
        [xml]$project = Get-Content -LiteralPath (Resolve-ChildPath $RepositoryRoot $consumer.path) -Raw
        $references = @($project.SelectNodes('//PackageReference') | Where-Object { $_.Include -eq $package.id })
        if ($references.Count -ne 1 -or $references[0].Version -ne $package.version) {
            throw "Private package consumer version drift: $($consumer.path)"
        }
    }
}
foreach ($package in $manifest.managedPackages) {
    foreach ($consumer in $package.consumers) {
        [xml]$project = Get-Content -LiteralPath (Resolve-ChildPath $RepositoryRoot $consumer) -Raw
        $references = @($project.SelectNodes('//PackageReference') | Where-Object { $_.Include -eq $package.id })
        if ($references.Count -ne 1 -or $references[0].Version -ne $package.version) {
            throw "Managed vendor package version drift: $consumer / $($package.id)"
        }
    }
}
# All identities and sources are checked before writing any files. Existing files are never replaced.
foreach ($item in $pending) {
    New-Item -ItemType Directory -Path (Split-Path $item.Target -Parent) -Force | Out-Null
    [IO.File]::Copy($item.Source, $item.Target, $false)
    Assert-Identity $item.Target $item.Hash
}
Write-Host "Vendor baseline: $($manifest.artifacts.Count) artifacts; missing=$($missing.Count); copied=$($pending.Count); source snapshots and private package versions verified."
if ($CheckOnly -and $missing.Count) { Write-Host ($missing -join "`n") }
