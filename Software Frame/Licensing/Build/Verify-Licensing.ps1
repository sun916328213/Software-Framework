# Developer verification: opens brief STA test windows, never production hosts or devices.
$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit ${LASTEXITCODE}): $($Arguments -join ' ')" }
}

# 纯托管交付：构建整个解决方案后直接跑两组测试，不再需要先发布原生组件。
Invoke-DotNet -Arguments @('build', (Join-Path $moduleRoot 'Licensing.sln'), '-c', 'Debug')
Invoke-DotNet -Arguments @('test', (Join-Path $moduleRoot 'Tests/Licensing.Tests/Licensing.Tests.csproj'),
    '-c', 'Debug', '--no-build')
Invoke-DotNet -Arguments @('test', (Join-Path $moduleRoot 'Tests/Licensing.Wpf.Tests/Licensing.Wpf.Tests.csproj'),
    '-c', 'Debug', '--no-build')
