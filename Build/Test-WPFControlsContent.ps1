[CmdletBinding()]
param([ValidateSet('Verify','Capture','Embedded-Capture')][string]$Mode='Verify')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$directory=Join-Path $repo 'TestResults/wpfcontrols-content-guard'
[IO.Directory]::CreateDirectory($directory) | Out-Null
$sdkVersion=(& dotnet --version).Trim()
$frameworkMajor=[int]$sdkVersion.Split('.')[0]
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>netSDKMAJOR.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.CodeAnalysis"><HintPath>$(MSBuildSDKsPath)/../Roslyn/bincore/Microsoft.CodeAnalysis.dll</HintPath></Reference>
    <Reference Include="Microsoft.CodeAnalysis.CSharp"><HintPath>$(MSBuildSDKsPath)/../Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll</HintPath></Reference>
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $directory 'Guard.csproj'),$project.Replace('SDKMAJOR',[string]$frameworkMajor))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WPFControlsContentGuard.cs') -Destination (Join-Path $directory 'Program.cs')
$manifest = if ($Mode -eq 'Embedded-Capture') { 'WPFControls-embedded-script-content.json' } else { 'WPFControls-business-content.json' }
& dotnet run --project (Join-Path $directory 'Guard.csproj') -- $repo (Join-Path $PSScriptRoot "Baselines/$manifest") $Mode.ToLowerInvariant()
if ($LASTEXITCODE) { throw 'WPFControls business content guard failed.' }
if ($Mode -eq 'Verify') {
    & dotnet run --project (Join-Path $directory 'Guard.csproj') --no-build -- $repo (Join-Path $PSScriptRoot 'Baselines/WPFControls-embedded-script-content.json') 'embedded-verify'
    if ($LASTEXITCODE) { throw 'Embedded startup business content guard failed.' }
}
