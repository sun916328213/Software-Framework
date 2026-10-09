[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipDeployment,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
& (Join-Path $PSScriptRoot 'Test-ArchitectureBaseline.ps1') -Mode Verify -BaselinePath (Join-Path $PSScriptRoot 'Baselines/WPFControls-Consolidation.json')
& (Join-Path $PSScriptRoot 'Test-WPFControlsContent.ps1')
$hostXml = [xml](Get-Content -LiteralPath (Join-Path $repo 'MachineApp/MachineApp/MachineApp.csproj') -Raw)
$hostReferences = @($hostXml.SelectNodes('//ProjectReference'))
if ($hostReferences.Count -ne 1 -or $hostReferences[0].Include -ne '..\..\Software Frame\WPFControls\WPFControls\WPFControls.csproj') {
    throw 'MachineApp must compose framework modules through WPFControls.'
}
$uiXml = [xml](Get-Content -LiteralPath (Join-Path $repo 'Software Frame/WPFControls/WPFControls/WPFControls.csproj') -Raw)
if (@($uiXml.SelectNodes('//ProjectReference') | Where-Object { $_.Include -match 'MachineApp|WPFControl\.(Application|Controls)' }).Count) {
    throw 'A reverse host dependency or old UI assembly returned.'
}
function Read-Source([string]$Relative) { [IO.File]::ReadAllText((Join-Path $repo $Relative)) }
function Assert-Headless([string]$Relative) {
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push((Join-Path $repo $Relative))
    while ($pending.Count) {
        $path = $pending.Pop()
        if (!$seen.Add($path)) { continue }
        [xml]$project = Get-Content -LiteralPath $path -Raw
        $name = [IO.Path]::GetFileNameWithoutExtension($path)
        if ($name -in @('FlowNodes','Extension','WPFControls') -or $name -like '*.Controls' -or
            @($project.SelectNodes('//UseWPF') | Where-Object InnerText -eq 'true').Count) { throw "Presentation dependency: $path" }
        foreach ($reference in $project.SelectNodes('//ProjectReference')) {
            $pending.Push([IO.Path]::GetFullPath((Join-Path (Split-Path $path -Parent) $reference.Include)))
        }
        $assetsPath = Join-Path (Split-Path $path -Parent) 'obj/project.assets.json'
        $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
        foreach ($framework in $assets.project.frameworks.PSObject.Properties.Value) {
            if (@($framework.frameworkReferences.PSObject.Properties.Name | Where-Object { $_ -like 'Microsoft.WindowsDesktop*' }).Count) {
                throw "Evaluated desktop framework: $path"
            }
        }
        if (@($assets.libraries.PSObject.Properties.Name | Where-Object { $_ -match '^(Extension|FlowNodes|WPFControls)/|\.Controls/|^(HALCON|HalconDotNet|OpenCvSharp)' }).Count) {
            throw "Evaluated presentation/vision dependency: $path"
        }
    }
    Write-Host "Headless closure: $Relative ($($seen.Count) projects)."
}
foreach ($relative in @(
    'Software Frame/FlowEditor/FlowEditor.Runtime/FlowEditor.Runtime.csproj',
    'Software Frame/ScriptEditor/ScriptEditor.Runtime/ScriptEditor.Runtime.csproj',
    'Software Frame/Communication/Communication.Runtime/Communication.Runtime.csproj',
    'Software Frame/SecsGem/SecsGem.Runtime/SecsGem.Runtime.csproj',
    'Software Frame/Vision/Vision.Core/Vision.Core.csproj',
    'Software Frame/Extension/Extension.Core/Extension.Core.csproj'
)) { Assert-Headless $relative }
foreach ($relative in @('Station/StationServices.cs','Station/StationBase.cs','Flow/AutoProcess.cs','Flow/Close.cs')) {
    $source = Read-Source "MachineApp/MachineApp/Business/$relative"
    $source = [regex]::Replace($source, '(?m)^using [^;]+;\r?\n', '')
    if ($source -match '\b(PLCTool|SecsTool|VisionTool|ProductionDeviceSession|PrintManagerService|LogInfoService)\b') { throw "Concrete production collaborator: $relative" }
}
$facade = Read-Source 'Software Frame/WPFControls/WPFControls/Features/Data/Services/CapacityStatisticsService.cs'
if ($facade -match '\.Query\(|\.Update\(|\.Add\(' -and $facade -match 'StatsSQL') { throw 'SQL write policy returned to presentation.' }
# Check actual ignore status without staging or assuming an untracked file has already been committed.
$snapshot = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/WPFControls-Consolidation.json') -Raw | ConvertFrom-Json
$gitInfo = [Diagnostics.ProcessStartInfo]::new('git')
$gitInfo.UseShellExecute = $false
$gitInfo.CreateNoWindow = $true
$gitInfo.RedirectStandardInput = $true
$gitInfo.RedirectStandardOutput = $true
$gitInfo.RedirectStandardError = $true
$gitInfo.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
$gitInfo.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
foreach ($argument in @('-C', $repo, 'check-ignore', '--no-index', '-z', '--stdin')) { $gitInfo.ArgumentList.Add($argument) }
$gitProcess = [Diagnostics.Process]::Start($gitInfo)
try {
    $gitOutput = $gitProcess.StandardOutput.ReadToEndAsync()
    $gitError = $gitProcess.StandardError.ReadToEndAsync()
    # NUL delimiters avoid PowerShell's Windows CRLF pipe becoming part of a Git path.
    $gitProcess.StandardInput.Write([string]::Join([char]0, [string[]]$snapshot.sources.path) + [char]0)
    $gitProcess.StandardInput.Close()
    $gitProcess.WaitForExit()
    if ($gitProcess.ExitCode -gt 1) { throw "Cannot inspect source ignore rules: $($gitError.GetAwaiter().GetResult())" }
    $ignored = @($gitOutput.GetAwaiter().GetResult().Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
}
finally { $gitProcess.Dispose() }
if ($ignored.Count) { throw "Ignored required source files: $($ignored -join ', ')" }
if ($SkipDeployment) { Write-Host 'Architecture structure verified; deployment explicitly skipped.'; return }
$hosts = @(
    @{ Name='MachineApp'; Project='MachineApp/MachineApp/MachineApp.csproj'; Directory='MachineApp/MachineApp'; Output='MachineApp' },
    @{ Name='FlowNodes.Workbench'; Project='Software Frame/FlowNodes/FlowNodes.Workbench/FlowNodes.Workbench.csproj'; Directory='Software Frame/FlowNodes/FlowNodes.Workbench'; Output='FlowNodesWorkbench' }
)
function Get-TargetFramework([string]$ProjectPath) {
    $xml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
    return $xml.Project.PropertyGroup.TargetFramework
}
foreach ($hostInfo in $hosts) {
    $published = Join-Path $repo "TestResults/wpfcontrols-consolidation-deployment/$Configuration/$($hostInfo.Output)"
    if ($Publish) {
        & dotnet publish (Join-Path $repo $hostInfo.Project) -c $Configuration --no-restore -m:1 -o $published
        if ($LASTEXITCODE) { throw "Publish failed: $($hostInfo.Name)" }
    }
    # 从项目实际 TFM 推导输出目录。原实现硬编码 net9.0-windows7.0，而宿主实际为
    # net9.0-windows10.0.19041.0 —— 于是校验的是旧构建残留（deps.json 仍存在，读取成功）
    # 并误报通过。Test-Integration.ps1 已按 TFM 推导。
    $tfm = Get-TargetFramework (Join-Path $repo $hostInfo.Project)
    $build = Join-Path $repo "$($hostInfo.Directory)/bin/$Configuration/$tfm"
    foreach ($output in @($build, $published)) {
        $deps = Get-Content -LiteralPath (Join-Path $output "$($hostInfo.Name).deps.json") -Raw | ConvertFrom-Json
        $required = @('FlowNodes','FlowEditor.Runtime','ScriptEditor.Runtime','Communication.Runtime','FlowEditor.Controls')
        if ($hostInfo.Name -eq 'MachineApp') { $required += 'WPFControls' }
        if (@($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -match '^WPFControl(?:\.(?:Application|Controls))?/' }).Count) {
            throw "Old UI assembly remains in deployment: $output"
        }
        foreach ($name in $required) {
            if (!(Test-Path -LiteralPath (Join-Path $output "$name.dll")) -or
                !@($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like "$name/*" }).Count) { throw "Missing host dependency: $output -> $name" }
        }
        if (!(Test-Path -LiteralPath (Join-Path $output "$($hostInfo.Name).runtimeconfig.json"))) { throw "Missing runtime configuration: $output" }
        $nodesTfm = Get-TargetFramework (Join-Path $repo 'Software Frame/FlowNodes/FlowNodes/FlowNodes.csproj')
        $nodes = Join-Path $repo "Software Frame/FlowNodes/FlowNodes/bin/$Configuration/$nodesTfm/FlowNodes.dll"
        if ((Get-FileHash -LiteralPath (Join-Path $output 'FlowNodes.dll')).Hash -ne (Get-FileHash -LiteralPath $nodes).Hash) { throw "Stale nodes: $output" }
    }
}
Write-Host 'Architecture closure and both host deployments verified.'
