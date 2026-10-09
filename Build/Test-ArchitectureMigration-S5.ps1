[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$before = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/Architecture-S4.json') -Raw | ConvertFrom-Json
$migration = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/Architecture-S5-migrations.json') -Raw | ConvertFrom-Json
function Assert-Hash([string]$Path, [string]$Expected) {
    $absolute = Join-Path $repo $Path
    if (!(Test-Path -LiteralPath $absolute -PathType Leaf)) { throw "Missing file: $Path" }
    if ((Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Expected) { throw "Content changed: $Path" }
}
$moves = @{}
foreach ($move in $migration.fileMoves) {
    $moves[$move.oldPath] = $move.newPath
    if (Test-Path -LiteralPath (Join-Path $repo $move.oldPath)) { throw "Old entry remains: $($move.oldPath)" }
    if ($move.PSObject.Properties.Name -contains 'newSha256') { Assert-Hash $move.newPath $move.newSha256 }
    else { Assert-Hash $move.newPath $move.sha256 }
}
$testChanges = @(
    'Software Frame/Vision/Tests/Vision.Tests/Architecture/AIProviderDependencyTests.cs',
    'Software Frame/ScriptEditor/Tests/ScriptEditor.Tests/Architecture/LayeringTests.cs',
    'Software Frame/SecsGem/Tests/SecsGem.Tests/Architecture/LayeringTests.cs',
    'Software Frame/DataManager/Tests/DataManager.Tests/SQL/SqlDependencyArchitectureTests.cs',
    'MachineApp/Tests/MachineApp.Host.Tests/NamespaceMigrationHostTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/TierGuardTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/ArchitectureContractTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/BitOperationExTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/CorePlatformTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/DistributionProtectionTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/DistributionSecurityTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/PatternsObserverMediatorTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/PatternsProducerConsumerTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/PatternsPublishSubscribeTests.cs',
    'Software Frame/Extension/Tests/Extension.Tests/PatternsSingletonFactoryTests.cs'
)
foreach ($source in $before.sources) {
    if ($source.path -eq 'Software Frame/Extension/Extension/Core/Reflection/FrameworkTypeResolver.cs') {
        $bytes = [IO.File]::ReadAllBytes((Join-Path $repo $moves[$source.path]))
        $utf8 = [Text.UTF8Encoding]::new($false)
        $text = $utf8.GetString($bytes)
        $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $block = '            // S5 uses a direct assembly cutover; a missing Extension must not search Core.' + $nl +
            '            if (string.Equals(name.Name, "Extension", StringComparison.OrdinalIgnoreCase)) throw;' + $nl
        if (!$text.Contains($block)) { throw 'Missing S5 identity guard.' }
        $restored = $utf8.GetBytes($text.Replace($block, ''))
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($restored)).ToLowerInvariant()
        if ($hash -ne $source.sha256) { throw 'Resolver changed beyond the reviewed assembly identity guard.' }
    }
    elseif ($moves.ContainsKey($source.path)) { Assert-Hash $moves[$source.path] $source.sha256 }
    elseif ($source.path -notin $testChanges) { Assert-Hash $source.path $source.sha256 }
}
foreach ($file in $before.xaml) { Assert-Hash $file.path $file.sha256 }
foreach ($file in $before.configuration) {
    if ($file.path -eq $migration.resourceMove.oldPath) { Assert-Hash $migration.resourceMove.newPath $file.sha256 }
    else { Assert-Hash $file.path $file.sha256 }
}
foreach ($file in $before.solutions) {
    if ($file.path -notin $migration.changedSolutions) { Assert-Hash $file.path $file.sha256 }
}
foreach ($project in $before.projects) {
    if ($project.path -notin $migration.changedProjects.path) { Assert-Hash $project.path $project.sha256 }
}
[xml]$core = Get-Content -LiteralPath (Join-Path $repo 'Software Frame/Extension/Extension.Core/Extension.Core.csproj') -Raw
if ($core.Project.PropertyGroup.TargetFramework -ne 'net9.0') { throw 'Core must target net9.0.' }
if ($core.SelectNodes('//PackageReference|//ProjectReference|//FrameworkReference|//UseWPF|//UseWindowsForms').Count) { throw 'Core gained an external or desktop dependency.' }
if (Test-Path -LiteralPath (Join-Path $repo $migration.resourceMove.oldPath)) { throw 'Old alias resource remains.' }
foreach ($solution in $migration.changedSolutions) {
    $text = Get-Content -LiteralPath (Join-Path $repo $solution) -Raw
    if ($text -notmatch 'Project\("\{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC\}"\) = "Extension.Core",') { throw "Core project entry invalid: $solution" }
    if ($text -notmatch '\{CE632E27-FBB4-4ED9-B047-ECECA8B45A15\}\.Release\|Any CPU.Build.0') { throw "Core missing build configuration: $solution" }
}
Write-Host "S5 boundary verified: $($migration.fileMoves.Count) moves (58 byte-identical, 1 resolver identity guard), $($migration.movedPublicTypes.Count) extracted public names, all other production source/XAML/configuration and historical solutions unchanged."
