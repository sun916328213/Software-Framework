[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$NuGetConfig,
    [string]$VendorDllRoot = $env:FRAMEWORK_VENDOR_DLL_ROOT,
    [switch]$IncludeDatabaseTests,
    [switch]$CheckOnly,
    [switch]$NoRestore
)
# 中文阅读说明：
#   全仓集成入口。原版（Build/PROVENANCE.md 记录的 ede41e98c248）引用
#   `Software-Framework.sln` 与 `Packing Machine/Packing Machine/Packing Machine.csproj`，
#   两者在重构后都已不存在 —— 脚本在第 24 行的工程数守卫上必然硬失败。
#   T02 按新布局改写：`Software Frame.sln` + `MachineApp/MachineApp/MachineApp.csproj`。
#
#   改写原则：**凡是能从工程文件推导的，都不要写死。** 原版把宿主的输出目录硬编码成
#   `net9.0-windows7.0`、把必需的部署配置硬编码成四条路径；而 MachineApp 现在
#   TargetFramework 是 `net9.0-windows10.0.19041.0`，bin 下同时残留着一个过期的
#   `net9.0-windows7.0`（Packing Machine 时期遗留）。硬编码会让脚本去校验**过期产物**却报通过。
#   因此这里改为：TFM 读 `//TargetFramework`，必需配置读 csproj 里带
#   `CopyToOutputDirectory` 的 `None` 项 —— 工程改了，脚本自动跟上。
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repo 'Software Frame.sln'
$hostProject = Join-Path $repo 'MachineApp/MachineApp/MachineApp.csproj'
$results = Join-Path $repo 'TestResults'
& (Join-Path $PSScriptRoot 'Restore-VendorDependencies.ps1') -VendorDllRoot $VendorDllRoot -CheckOnly:$CheckOnly
$vendorManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Dependencies/vendor-manifest.json') -Raw | ConvertFrom-Json
$declaredVendorPaths = @($vendorManifest.artifacts | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $repo $_.path)) })
function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) { throw "缺少解决方案：$solution" }
if (-not (Test-Path -LiteralPath $hostProject -PathType Leaf)) { throw "缺少集成宿主工程：$hostProject" }
$projects = @(Select-String -LiteralPath $solution -Pattern '"([^\"]+\.csproj)"' |
    ForEach-Object { Join-Path $repo $_.Matches[0].Groups[1].Value })
# 工程数守卫：原版是「旧布局 72 个工程 → 取 50」。新布局 sln 有 76 个工程，
# 这里取 60 作为「sln 被截断/指向了错文件」的兜底；真正的完备性由下面的
# $omittedOwnedProjects 全量比对保证，不靠这个魔数。
if ($projects.Count -lt 60 -or $hostProject -notin $projects) {
    throw "集成解决方案必须包含框架全部工程与 MachineApp 宿主（当前 $($projects.Count) 个工程）。"
}
$missingProjects = @()
$missingVendor = @()
$tests = @()
$integrationHosts = @()
foreach ($project in $projects) {
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        $missingProjects += $project
        continue
    }
    [xml]$xml = Get-Content -LiteralPath $project -Raw
    if (@($xml.SelectNodes('//IsIntegrationHost') | Where-Object { $_.InnerText.Trim() -eq 'true' }).Count) {
        $integrationHosts += $project
    }
    foreach ($reference in $xml.SelectNodes('//ProjectReference')) {
        $target = [IO.Path]::GetFullPath((Join-Path (Split-Path $project -Parent) $reference.Include))
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { $missingProjects += $target }
        if ($target -notin $projects) { throw "Solution omits a referenced project: $target" }
    }
    foreach ($hint in $xml.SelectNodes('//Reference/HintPath')) {
        $target = [IO.Path]::GetFullPath((Join-Path (Split-Path $project -Parent) $hint.InnerText))
        $dllRoot = Join-Path $repo 'Dll'
        if (-not $target.StartsWith($dllRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Vendor reference must stay inside repository Dll/: $target"
        }
        if ($target -notin $declaredVendorPaths) { throw "Vendor reference absent from dependency manifest: $target" }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { $missingVendor += $target }
    }
    if ($xml.SelectNodes('//PackageReference[@Include="Microsoft.NET.Test.Sdk"]').Count -gt 0) {
        $isWpf = @($xml.SelectNodes('//UseWPF') | Where-Object { $_.InnerText.Trim() -eq 'true' }).Count -gt 0
        $tests += [pscustomobject]@{ Path = $project; IsWpf = $isWpf }
    }
}
if ($missingProjects.Count) { throw "Missing project references:`n$($missingProjects -join "`n")" }
$ownedProjects = @(foreach ($folder in @('Software Frame', 'MachineApp')) {
    Get-ChildItem -LiteralPath (Join-Path $repo $folder) -Filter '*.csproj' -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|secs4net-base)[\\/]' -and
            $_.Name -notlike '*_wpftmp.csproj'
        } | ForEach-Object FullName
})
$omittedOwnedProjects = @($ownedProjects | Where-Object { $_ -notin $projects })
if ($omittedOwnedProjects.Count) {
    throw "Integration solution omits owned projects:`n$($omittedOwnedProjects -join "`n")"
}
if ($integrationHosts.Count -ne 1 -or $integrationHosts[0] -ne $hostProject) {
    throw 'MachineApp must be the single declared integration host.'
}
if (-not $tests.Count -or -not @($tests | Where-Object IsWpf).Count) {
    throw 'Both logic and WPF test lines must be present.'
}
Write-Host "Validated $($projects.Count) projects and $($tests.Count) test projects."
if ($CheckOnly) {
    Write-Host "Vendor binaries not provisioned: $(@($missingVendor | Select-Object -Unique).Count)"
    return
}
if ($missingVendor.Count) {
    throw "Provision vendor assemblies in Dll/ or pass -VendorDllRoot:`n$($missingVendor -join "`n")"
}
Push-Location $repo
try {
    if (-not $NoRestore) {
        $restoreArgs = @('restore', $solution)
        if ($NuGetConfig) { $restoreArgs += @('--configfile', $NuGetConfig) }
        Invoke-DotNet $restoreArgs
    }
    Invoke-DotNet @('build', $solution, '-c', $Configuration, '--no-restore', '-m:1')
    foreach ($line in @($false, $true)) {
        Write-Host "Running UseWPF=$line test line"
        foreach ($test in $tests | Where-Object { $_.IsWpf -eq $line }) {
            $name = [IO.Path]::GetFileNameWithoutExtension($test.Path)
            $testArgs = @('test', $test.Path, '-c', $Configuration, '--no-build', '--no-restore',
                '--logger', "trx;LogFilePrefix=$name", '--results-directory', $results,
                '--blame-hang-timeout', '5m')
            if (-not $IncludeDatabaseTests) { $testArgs += @('--filter', 'Category!=ExternalDatabase') }
            Invoke-DotNet $testArgs
        }
    }
    Invoke-DotNet @('build', $hostProject, '-c', $Configuration, '--no-restore')
    # TFM 与必需配置都从宿主工程推导，不写死（见文件头说明）。
    [xml]$hostXml = Get-Content -LiteralPath $hostProject -Raw
    $tfm = @($hostXml.SelectNodes('//TargetFramework') | Where-Object { $_.InnerText.Trim() })[0].InnerText.Trim()
    if (-not $tfm) { throw "MachineApp 未声明 TargetFramework，无法定位输出目录。" }
    $requiredConfig = @(
        foreach ($node in $hostXml.SelectNodes('//None')) {
            if (-not @($node.SelectNodes('CopyToOutputDirectory')).Count) { continue }
            $declared = if ($node.Include) { $node.Include } elseif ($node.Update) { $node.Update } else { $null }
            if ($declared) { $declared -replace '\\', '/' }
        }
    ) | Sort-Object -Unique
    if (-not $requiredConfig.Count) { throw "MachineApp 未声明任何随输出复制的配置项，无法校验部署。" }
    $output = Join-Path (Split-Path $hostProject -Parent) "bin/$Configuration/$tfm"
    if (-not (Test-Path -LiteralPath (Join-Path $output 'MachineApp.exe'))) {
        throw "MachineApp output is missing for $Configuration/$tfm : $output"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $output 'FlowNodes.dll'))) {
        throw 'MachineApp output is missing the dynamically loaded FlowNodes.dll.'
    }
    $deps = Get-Content -LiteralPath (Join-Path $output 'MachineApp.deps.json') -Raw | ConvertFrom-Json
    if (-not @($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'FlowNodes/*' }).Count) {
        throw 'MachineApp deps.json is missing FlowNodes.'
    }
    foreach ($config in $requiredConfig) {
        if (-not (Test-Path -LiteralPath (Join-Path $output $config) -PathType Leaf)) {
            throw "MachineApp output is missing required configuration: $config"
        }
    }
    Write-Host "MachineApp deployment verified: $tfm, $(@($requiredConfig).Count) configuration files."
} finally { Pop-Location }
