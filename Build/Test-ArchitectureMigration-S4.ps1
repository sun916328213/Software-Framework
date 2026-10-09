[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$baseline = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/Architecture-S3.json') -Raw | ConvertFrom-Json
$migration = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/Architecture-S4-migrations.json') -Raw | ConvertFrom-Json
$oldSources = @{}
foreach ($source in $baseline.sources) { $oldSources[$source.path] = $source }
function HashText($text, $bom) {
    $encoding = [Text.UTF8Encoding]::new($bom)
    $bytes = $encoding.GetBytes($text)
    if ($bom) { $bytes = $encoding.GetPreamble() + $bytes }
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function MatchesBaseline($text, $hash) {
    foreach ($candidate in @($text, $text.Replace("`r`n", "`n"), $text.Replace("`r`n", "`n").Replace("`n", "`r`n"))) {
        if ((HashText $candidate $false) -eq $hash -or (HashText $candidate $true) -eq $hash) { return $true }
    }
    return $false
}
$checked = 0
foreach ($move in $migration.fileMoves) {
    $oldPath = Join-Path $repoRoot $move.oldPath
    $newPath = Join-Path $repoRoot $move.newPath
    if (Test-Path -LiteralPath $oldPath) { throw "Old implementation still exists: $($move.oldPath)" }
    $text = [IO.File]::ReadAllText($newPath)
    foreach ($rename in $migration.typeRenames | Where-Object path -eq $move.newPath) {
        $oldNamespace = $rename.oldType.Substring(0, $rename.oldType.LastIndexOf('.'))
        $newNamespace = $rename.newType.Substring(0, $rename.newType.LastIndexOf('.'))
        $text = [regex]::Replace($text, ('namespace ' + [regex]::Escape($newNamespace) + '(?=[;\s{])'), "namespace $oldNamespace")
    }
    if ($move.newPath -like '*/Communication/Net*.cs') { $text = $text -replace '^using FlowNodes\.Communication\.CommunicationNew;\r?\n', '' }
    if ($move.newPath -like '*/Fixture/FixtureNode.cs') { $text = $text.Replace('class FixtureNode', 'class FixtrueNode') }
    if (-not (MatchesBaseline $text $oldSources[$move.oldPath].sha256)) { throw "Content differs beyond the declared namespace/name move: $($move.newPath)" }
    $checked++
}

# Reverse the declaration/import edits in existing host implementations, then
# compare the entire source to S3. This protects method bodies and user text.
$hostChecked = 0
$hostRoot = 'Software Frame/WPFControl/WPFControl/'
foreach ($row in $baseline.sources | Where-Object { $_.path.StartsWith($hostRoot) -and $_.path -notin $migration.fileMoves.oldPath }) {
    $text = [IO.File]::ReadAllText((Join-Path $repoRoot $row.path))
    if (MatchesBaseline $text $row.sha256) { $hostChecked++; continue }
    foreach ($rename in $migration.typeRenames | Where-Object assembly -eq 'WPFControl') {
        $text = $text.Replace($rename.newType, $rename.oldType)
        if ($rename.path -eq $row.path) {
            $oldNamespace = $rename.oldType.Substring(0, $rename.oldType.LastIndexOf('.'))
            $newNamespace = $rename.newType.Substring(0, $rename.newType.LastIndexOf('.'))
            $text = [regex]::Replace($text, ('namespace ' + [regex]::Escape($newNamespace) + '(?=[;\s{])'), "namespace $oldNamespace")
        }
    }
    $text = $text.Replace('using WPFControl.ProgramControl.Presentation.Data;', 'using WPFControl.ProgramControl.Presentation;')
    $text = $text.Replace('using WPFControl.ProgramControl.Presentation.Log;', 'using WPFControl.ProgramControl.Presentation;')
    if ($row.path.EndsWith('/WPFControlModule.cs')) {
        $text = [regex]::Replace($text, '(?m)^\s*WPFControl\.Integration\.Compatibility\.FeatureViewModelComposition\.Register\(\);\r?\n', '')
    }
    if ($row.path.EndsWith('/UserManagerService.cs')) {
        $message = [IO.File]::ReadAllText((Join-Path $repoRoot ($hostRoot + 'Compatibility/User/UserMessage.cs')))
        $start = $message.IndexOf('    public static class UserMessage')
        $message = $message.Substring($start, $message.LastIndexOf("}`n") - $start)
        $text = $text.Insert($text.IndexOf('    internal class UserManagerService'), $message)
    }
    $withoutImport = $text -replace '^using WPFControl\.ProgramControl\.(?:Services|ViewModels);\r?\n', ''
    if (-not (MatchesBaseline $text $row.sha256) -and -not (MatchesBaseline $withoutImport $row.sha256)) {
        throw "Host implementation differs beyond identity/import edits: $($row.path)"
    }
    $hostChecked++
}

# Check XAML identities and resource paths independently of design-time namespace changes.
$xamlChecked = 0
foreach ($row in $baseline.xaml) {
    $text = [IO.File]::ReadAllText((Join-Path $repoRoot $row.path))
    foreach ($feature in @('Alarm','Data','Log','Printer','Shell','User')) {
        $text = $text.Replace("clr-namespace:WPFControl.ProgramControl.ViewModels.$feature`"", 'clr-namespace:WPFControl.ProgramControl.ViewModels"')
    }
    if (-not (MatchesBaseline $text $row.sha256)) { throw "XAML changed beyond design namespace: $($row.path)" }
    $xamlChecked++
}
foreach ($row in @($baseline.projects) + @($baseline.solutions)) {
    if ((Get-FileHash -LiteralPath (Join-Path $repoRoot $row.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $row.sha256) { throw "Project/solution boundary drift: $($row.path)" }
}
$aliases = Get-Content -LiteralPath (Join-Path $repoRoot 'Software Frame/Extension/Extension/Core/Reflection/FrameworkTypeAliases.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($rename in $migration.typeRenames) {
    $alias = $aliases[$rename.oldType]
    if ($alias.Type -ne $rename.newType -or $alias.Assembly -ne $rename.assembly) { throw "Missing exact migration alias: $($rename.oldType)" }
}
Write-Host "PASS: $checked moves and $hostChecked existing host sources preserve content after reversing declared names/imports; $xamlChecked XAML identities/resources, $($baseline.projects.Count) projects, $($baseline.solutions.Count) solutions, $($migration.typeRenames.Count) aliases verified."
