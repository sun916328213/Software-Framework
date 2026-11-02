param(
    [Parameter(Mandatory = $true)][string]$PublicKeyFile,
    [Parameter(Mandatory = $true)][ValidatePattern('\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z')][string]$ProductId,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [ValidatePattern('\A[0-9A-Fa-f]{64}\z')][string]$OverrideExpectedFingerprint = ''
)
$ErrorActionPreference = 'Stop'
# 调用方（Licensing.Authorization）按 UTF-8 解码本脚本输出；不设置会让中文日志变成乱码。
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$moduleRoot = Split-Path -Parent $PSScriptRoot
if ($ProductId -eq 'SoftwareFrame.Default') { throw 'Use an explicit production product ID, not the default test ID.' }
$key = (Resolve-Path -LiteralPath $PublicKeyFile).Path
# 默认只允许发布仓库钉住的那把公钥：换了签发身份就先把新公钥入库并更新 Build\Trust.props，
# 让改动出现在 git diff 里。显式覆盖只用于测试和临时身份，覆盖值会同时传给构建守卫。
$trust = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Trust.props'))
$pinned = ([regex]'(?s)<LicenseExpectedPublicKeyFingerprint>([^<]+)</LicenseExpectedPublicKeyFingerprint>').Match($trust).Groups[1].Value
if ($pinned -notmatch '\A[0-9A-Fa-f]{64}\z') { throw 'Build\Trust.props does not pin a public key fingerprint.' }
$fingerprint = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash(
    [Convert]::FromBase64String(([IO.File]::ReadAllText($key)).Trim()))).Replace('-', '')
$expected = if ($OverrideExpectedFingerprint -ne '') { $OverrideExpectedFingerprint.ToUpperInvariant() } else { $pinned }
if ($fingerprint -ne $expected) {
    throw "Refusing to publish: key fingerprint $fingerprint is not the expected trust anchor $expected. Commit Build\Keys\public-$fingerprint.key and update Build\Trust.props, or pass -OverrideExpectedFingerprint for a non-delivered build."
}
& (Join-Path $PSScriptRoot 'Validate-PublicKey.ps1') -PublicKeyFile $key -ExpectedFingerprint $expected
if (-not $?) { throw 'Public key validation failed.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {
    if (-not (Test-Path -LiteralPath $output -PathType Container)) { throw 'Output must be a directory.' }
    if (@(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) {
        throw 'Use an empty output directory. Existing files will not be deleted or overwritten.'
    }
}
# 客户包是纯托管交付：只需要 .NET SDK，不需要 C++ 链接器、MSVC 或 Windows SDK。
Write-Output ('diagnostics: dotnet=' + (& dotnet --version 2>&1))
& dotnet publish (Join-Path $moduleRoot 'Licensing.Activation/Licensing.Activation.csproj') `
    -c Release -r win-x64 --self-contained true -o $output `
    "-p:LicensePublicKeyFile=$key" "-p:LicenseProductId=$ProductId" `
    "-p:LicenseExpectedPublicKeyFingerprint=$expected" -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Client publish failed (exit ${LASTEXITCODE}). Do not deploy partial output." }
& (Join-Path $PSScriptRoot 'Test-ProtectedDeployment.ps1') -Directory $output -RequireManagedAdapter
if (-not $?) { throw 'Protected deployment audit failed. Do not deploy this output.' }
foreach ($name in @('Licensing.Activation.exe', 'Licensing.Client.dll', 'Licensing.Core.dll', 'Licensing.Trust.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $name))) { throw "Missing client component: $name" }
}
@'
Offline activation tool for Windows x64.
Copy the complete folder, not only the EXE. A separate .NET installation is not required.
This package does NOT include the issuer tool or private key.
Authorization checks run in Licensing.Core.dll (managed, not obfuscated).
Decompiling these assemblies reveals the verification logic, but forging a license still requires
the vendor private key, which never leaves the issuer environment.
Your production application must independently integrate authorization checks and safe device handling.
'@ | Set-Content -LiteralPath (Join-Path $output 'DEPLOYMENT.txt') -Encoding ASCII
Write-Output "Client publish completed: $output"
