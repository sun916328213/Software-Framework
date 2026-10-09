[CmdletBinding()]
param(
    [ValidateSet('Capture', 'Verify')][string]$Mode = 'Verify',
    [string]$BaselinePath,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $BaselinePath) {
    $BaselinePath = Join-Path $repoRoot 'Build/Baselines/Architecture-P0.json'
}
$BaselinePath = [IO.Path]::GetFullPath($BaselinePath)
if ($Force -and $Mode -ne 'Capture') { throw '-Force is only valid with -Mode Capture.' }
if ($Mode -eq 'Capture' -and (Test-Path -LiteralPath $BaselinePath) -and -not $Force) {
    throw 'Baseline already exists. Review drift before explicitly using -Mode Capture -Force.'
}
function Get-RelativePath([string]$Path) {
    [IO.Path]::GetRelativePath($repoRoot, $Path).Replace('\', '/')
}
function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-MatchedText([string]$Source, [string]$Pattern) {
    @([regex]::Matches($Source, $Pattern) | ForEach-Object { $_.Value.Trim() })
}
# Enumerate the filesystem directly: gitignore excludes real ProgramControl/Log source directories.
$files = @(foreach ($sourceRoot in @('Software Frame', 'MachineApp')) {
    Get-ChildItem -LiteralPath (Join-Path $repoRoot $sourceRoot) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs)[\\/]' }
})
$projects = @(foreach ($file in $files | Where-Object {
        $_.Extension -eq '.csproj' -and $_.Name -notlike '*_wpftmp.csproj'
    } | Sort-Object FullName) {
    [xml]$projectXml = Get-Content -LiteralPath $file.FullName -Raw
    $declaredProperties = @(foreach ($property in $projectXml.SelectNodes('//PropertyGroup/*') |
            Where-Object { $_.Name -in @('AssemblyName', 'RootNamespace', 'TargetFramework',
                'TargetFrameworks', 'UseWPF', 'OutputType', 'IsIntegrationHost') }) {
        [ordered]@{
            name = $property.Name
            value = $property.InnerText.Trim()
            condition = ($property.ParentNode.GetAttribute('Condition') + ' ' + $property.GetAttribute('Condition')).Trim()
        }
    })
    [ordered]@{
        path = Get-RelativePath $file.FullName
        sha256 = Get-Sha256 $file.FullName
        declaredProperties = $declaredProperties
        projectReferences = @($projectXml.SelectNodes('//ProjectReference') | ForEach-Object {
            [ordered]@{
                path = Get-RelativePath ([IO.Path]::GetFullPath((Join-Path $file.DirectoryName $_.Include)))
                condition = ($_.ParentNode.GetAttribute('Condition') + ' ' + $_.GetAttribute('Condition')).Trim()
            }
        })
        packageReferences = @($projectXml.SelectNodes('//PackageReference') | ForEach-Object {
            [ordered]@{ name = $_.Include; version = $_.GetAttribute('Version'); condition = $_.GetAttribute('Condition') }
        })
    }
})
$ownedFiles = @($files | Where-Object { $_.FullName -notmatch '[\\/]secs4net-base[\\/]' } | Sort-Object FullName)
$sources = @(foreach ($file in $ownedFiles | Where-Object { $_.Extension -eq '.cs' }) {
    $sourceText = Get-Content -LiteralPath $file.FullName -Raw
    if ($null -eq $sourceText) { $sourceText = '' }
    [ordered]@{
        path = Get-RelativePath $file.FullName
        sha256 = Get-Sha256 $file.FullName
        namespaces = @([regex]::Matches($sourceText, '(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)') |
            ForEach-Object { $_.Groups[1].Value })
        # Textual declarations, not a substitute for compiled CLR type or plugin discovery tests.
        typeDeclarations = Get-MatchedText $sourceText '(?m)^\s*(?:(?:public|internal|private|protected|abstract|sealed|partial|static|readonly|ref|unsafe|new|file)\s+)*(?:class|interface|struct|enum|record(?:\s+(?:class|struct))?)\s+[A-Za-z_][A-Za-z0-9_]*(?:<[^\r\n>{}]+>)?'
        nodeMetadataDeclarations = Get-MatchedText $sourceText '(?ms)^\s*\[(?:FlowTool|NodeAction|NodeProperty)\s*\(.*?\)\s*\]'
        navigationDeclarations = Get-MatchedText $sourceText '(?m)^.*(?:Register(?:ForNavigation|NavigationView|NavigationViewsFromNamespace)|RequestNavigate|ModuleDependency|Assembly\.Load\("FlowNodes"|NodeTypeRegistry\.Register).*$'
    }
})
$xaml = @(foreach ($file in $ownedFiles | Where-Object { $_.Extension -eq '.xaml' }) {
    $xamlText = Get-Content -LiteralPath $file.FullName -Raw
    [ordered]@{
        path = Get-RelativePath $file.FullName
        sha256 = Get-Sha256 $file.FullName
        classDeclarations = Get-MatchedText $xamlText 'x:Class\s*=\s*"[^"]+"'
        clrNamespaceDeclarations = Get-MatchedText $xamlText 'xmlns(?::[A-Za-z_][A-Za-z0-9_.]*)?\s*=\s*"clr-namespace:[^"]+"'
        resourceSources = Get-MatchedText $xamlText '\bSource\s*=\s*"[^"]+"'
    }
})
# Content is deliberately not copied: configurations can contain credentials or host-specific values.
$configuration = @(foreach ($file in $ownedFiles | Where-Object {
        $_.Extension -in @('.json', '.csv', '.flow', '.taskproj', '.config', '.resx', '.settings')
    }) {
    [ordered]@{ path = Get-RelativePath $file.FullName; sha256 = Get-Sha256 $file.FullName }
})
$solutions = @(Get-ChildItem -LiteralPath $repoRoot -Filter '*.sln' -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|TestResults)[\\/]' } |
    Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = Get-RelativePath $_.FullName; sha256 = Get-Sha256 $_.FullName }
    })
$snapshot = [ordered]@{
    schemaVersion = 1
    scope = 'Declared project structure, owned source identities, UI resources, configuration and serialization fixtures; excludes build outputs. Textual declarations are not evaluated MSBuild or CLR metadata.'
    projects = $projects
    sources = $sources
    xaml = $xaml
    configuration = $configuration
    solutions = $solutions
}
$snapshotJson = ConvertTo-Json -InputObject $snapshot -Depth 12
if ($Mode -eq 'Capture') {
    $baselineDirectory = Split-Path $BaselinePath -Parent
    if (-not (Test-Path -LiteralPath $baselineDirectory)) {
        New-Item -ItemType Directory -Path $baselineDirectory | Out-Null
    }
    [IO.File]::WriteAllText($BaselinePath, $snapshotJson + "`n", [Text.UTF8Encoding]::new($false))
} else {
    if (-not (Test-Path -LiteralPath $BaselinePath -PathType Leaf)) { throw 'Baseline is missing; explicitly capture a reviewed baseline first.' }
    $expected = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
    if ($expected.schemaVersion -ne 1 -or $expected.scope -ne $snapshot.scope) {
        throw 'Baseline schema or scope differs. Review the baseline before migration.'
    }
    $differences = [Collections.Generic.List[string]]::new()
    foreach ($section in @('projects', 'sources', 'xaml', 'configuration', 'solutions')) {
        $expectedEntries = @{}
        foreach ($entry in $expected.$section) { $expectedEntries[$entry.path] = $entry }
        foreach ($entry in $snapshot[$section]) {
            if (-not $expectedEntries.ContainsKey($entry.path)) {
                $differences.Add("Added ${section}: $($entry.path)")
            } else {
                $expectedJson = ConvertTo-Json -InputObject $expectedEntries[$entry.path] -Depth 10 -Compress
                $actualJson = ConvertTo-Json -InputObject $entry -Depth 10 -Compress
                if ($expectedJson -cne $actualJson) { $differences.Add("Changed ${section}: $($entry.path)") }
                $expectedEntries.Remove($entry.path)
            }
        }
        foreach ($removedPath in $expectedEntries.Keys | Sort-Object) { $differences.Add("Removed ${section}: $removedPath") }
    }
    if ($differences.Count) {
        $differences | Select-Object -First 30 | ForEach-Object { Write-Host $_ }
        throw "Architecture baseline drift: $($differences.Count) entries. Do not automatically overwrite the baseline; review identities and behavior compatibility first."
    }
}
Write-Host "Architecture baseline $Mode succeeded: $($projects.Count) projects, $($sources.Count) sources, $($xaml.Count) XAML files, $($configuration.Count) configuration/fixture files, $($solutions.Count) solutions."
