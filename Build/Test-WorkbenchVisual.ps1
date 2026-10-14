[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string[]]$Names=@('Device','DataManager','Communication','MotionControl','FlowEditor','ScriptEditor','SecsGem','Vision','FlowNodes'),
    [switch]$SwitchThemes
)
$ErrorActionPreference='Stop'
$repo = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $repo ("TestResults/workbench-visual-smoke/" + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $repo 'TestResults/WorkbenchVisuals'
[IO.Directory]::CreateDirectory($directory) | Out-Null
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0-windows7.0</TargetFramework><UseWPF>true</UseWPF><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $directory 'Smoke.csproj'), $project)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WorkbenchVisualSmoke.cs') -Destination (Join-Path $directory 'Program.cs')
dotnet build (Join-Path $directory 'Smoke.csproj') -c $Configuration --verbosity quiet
if ($LASTEXITCODE) { throw 'Workbench visual harness build failed.' }
$failures = @()
foreach ($name in $Names) {
    # TFM 从该 Workbench 工程实际声明推导，不写死。写死会在某个 Workbench 改 TFM 后
    # 静默校验过期产物并报通过（同 Test-ArchitectureClosure.ps1 已修的那类问题）。
    $hostProject = Join-Path $repo "Software Frame/$name/$name.Workbench/$name.Workbench.csproj"
    $hostTfm = ([xml](Get-Content -LiteralPath $hostProject -Raw)).Project.PropertyGroup.TargetFramework
    $assembly = Join-Path $repo "Software Frame/$name/$name.Workbench/bin/$Configuration/$hostTfm/$name.Workbench.dll"
    $hostOutput = Split-Path $assembly -Parent
    dotnet exec --runtimeconfig (Join-Path $hostOutput "$name.Workbench.runtimeconfig.json") --depsfile (Join-Path $hostOutput "$name.Workbench.deps.json") --additionalprobingpath (Join-Path $env:USERPROFILE '.nuget/packages') (Join-Path $directory "bin/$Configuration/net9.0-windows7.0/Smoke.dll") $assembly $name $output $SwitchThemes.IsPresent
    if ($LASTEXITCODE) { $failures += $name }
}
if ($failures.Count) { throw "Workbench visual verification failed: $($failures -join ', ')" }
