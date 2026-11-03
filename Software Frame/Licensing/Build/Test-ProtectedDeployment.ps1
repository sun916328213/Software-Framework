param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [switch]$RequireManagedAdapter
)
$ErrorActionPreference = 'Stop'
# 客户包现在交付托管程序集：必须有 Licensing.Core.dll（验签核心）与 Licensing.Client.dll（适配层），
# 且不得含厂商工具、签发身份、备份、调试符号和密钥文件。
$root = (Resolve-Path -LiteralPath $Directory).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Deployment path must be a directory.' }
$pending = New-Object 'System.Collections.Generic.Stack[string]'
$files = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
$pending.Push($root)
while ($pending.Count -gt 0) {
    $current = $pending.Pop()
    if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Deployment audit requires physical directories without junctions or symlinks.'
    }
    foreach ($item in Get-ChildItem -LiteralPath $current -Force) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Deployment contains a junction or symlink.' }
        if ($item.PSIsContainer) { $pending.Push($item.FullName) }
        else { $files.Add($item) }
    }
}
$core = Join-Path $root 'Licensing.Core.dll'
if (-not (Test-Path -LiteralPath $core -PathType Leaf)) {
    throw 'Managed authorization core (Licensing.Core.dll) is missing. Do not deploy this output.'
}
$trust = Join-Path $root 'Licensing.Trust.dll'
if (-not (Test-Path -LiteralPath $trust -PathType Leaf)) {
    throw 'Trust anchor (Licensing.Trust.dll) is missing: without it the client cannot verify any license.'
}
$adapter = Join-Path $root 'Licensing.Client.dll'
if ($RequireManagedAdapter -and -not (Test-Path -LiteralPath $adapter -PathType Leaf)) {
    throw 'Managed native-call adapter (Licensing.Client.dll) is missing.'
}
$forbidden = @($files | Where-Object {
    $_.Name -in 'Extension.Licensing.dll', 'issuer.bin', 'issuer.identity' -or
    $_.Name -like 'Licensing.Authorization*' -or
    $_.Name -like 'Licensing.Workbench*' -or
    $_.Name -like 'Licensing.Native*' -or
    $_.Extension -in '.pdb', '.key', '.pem', '.snk', '.pfx', '.p12', '.sfib', '.sfunlock'
})
if ($forbidden.Count -gt 0) {
    $names = ($forbidden | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
    throw "Protected deployment contains forbidden vendor/issuer/debug/key files. Review, do not blindly delete dependencies:`n$names"
}
Write-Output 'Protected deployment file checks passed. Runtime integration and device safety are NOT covered.'
