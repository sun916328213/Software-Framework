# Pre-pack check for Mitsubishi.MxComponent.Interop.
# Verifies the 6 vendor RCW DLLs exist under Dll/Mitsubishi and match vendor-manifest.json
# (file name + SHA-256 + file version). Fails the pack if anything mismatches, so a
# same-version-but-different-content nupkg can never be produced silently.
# Run from the repository root before pack:
#   powershell -ExecutionPolicy Bypass -File NugetPackages/Mitsubishi.MxComponent.Interop/verify-vendor-manifest.ps1
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
$manifestPath = Join-Path (Split-Path -Parent $PSCommandPath) "vendor-manifest.json"
$dllDir = Join-Path $repoRoot "Dll\Mitsubishi"

$manifest = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
$failed = $false

foreach ($file in $manifest.files) {
    $path = Join-Path $dllDir $file.name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Output "MISSING: $($file.name) not found in Dll\Mitsubishi"
        $failed = $true
        continue
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $file.sha256.ToLowerInvariant()) {
        Write-Output "HASH MISMATCH: $($file.name)`n  expected: $($file.sha256)`n  actual:   $actualHash"
        $failed = $true
    }
    $actualVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion
    if ($actualVersion -ne $file.fileVersion) {
        Write-Output "VERSION MISMATCH: $($file.name) expected $($file.fileVersion), actual $actualVersion"
        $failed = $true
    }
}

if ($failed) {
    Write-Output "vendor-manifest check FAILED. Refusing to pack."
    exit 1
}

Write-Output "vendor-manifest check passed: $($manifest.files.Count) files verified."
