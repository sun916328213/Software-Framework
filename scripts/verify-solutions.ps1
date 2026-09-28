<#
.SYNOPSIS
  P13 solution 入口守卫：允许存在的 Solution 白名单 + AnyCPU 配置检查。
  防止后续又产生临时 *.sln 或加回无意义 x64/x86 别名。
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-solutions.ps1
#>
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# 允许存在的 Solution（相对仓库根）。secs4net.sln 为第三方本地化副本，只验存在、不验配置。
$whitelist = @(
  'Frame.sln',
  'DataManager\DataManager.sln',
  'Communication\Communication.sln',
  'Device\Device.sln',
  'Extension\Extension.sln',
  'MotionControl\MotionControl.sln',
  'Vision\Vision.sln',
  'FlowEditor\FlowEditor.sln',
  'WPFCustomControls\WPFCustomControls.sln',
  'WPFControl\WPFControl.sln',
  'Secs\Secs.sln',
  'Secs\secs4net-base\secs4net.sln',
  'NugetPackages\NugetPackages.sln',
  'Packing Machine\Packing Machine.sln',
  'WinFormControl\WinFormControl.sln'
)

$skipConfigCheck = @('Secs\secs4net-base\secs4net.sln')
$failures = @()

# 1. 白名单外的 .sln 必须为 0（跳过 bin/obj/.vs/.git）。
$allSlns = Get-ChildItem -Path $repoRoot -Recurse -Filter '*.sln' -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|\.git)\\' } |
  ForEach-Object {
    $rel = $_.FullName.Substring($repoRoot.Length + 1)
    $rel
  }
foreach ($sln in $allSlns) {
  if ($whitelist -notcontains $sln) {
    $failures += "unexpected solution: $sln"
  }
}

# 2. 已删除的历史入口不得复活。
foreach ($gone in @('FlowEditor\Flow.sln')) {
  if (Test-Path (Join-Path $repoRoot $gone)) {
    $failures += "deleted solution still exists: $gone"
  }
}

# 3. 白名单成员必须存在；除第三方副本外只允许 AnyCPU 配置。
foreach ($rel in $whitelist) {
  $path = Join-Path $repoRoot $rel
  if (-not (Test-Path $path)) {
    $failures += "missing whitelisted solution: $rel"
    continue
  }
  if ($skipConfigCheck -contains $rel) { continue }

  $text = [System.IO.File]::ReadAllText($path)
  foreach ($m in [regex]::Matches($text, '(?m)^\s*(Debug|Release)\|([^\s=]+)\s*=')) {
    if ($m.Groups[2].Value -ne 'Any CPU') {
      $failures += "$rel has non-AnyCPU solution config: $($m.Groups[0].Value.Trim())"
    }
  }
  foreach ($m in [regex]::Matches($text, '(?m)^\s*\{[0-9A-Fa-f-]+\}\.(Debug|Release)\|([^\s.]+)\.')) {
    if ($m.Groups[2].Value -ne 'Any CPU') {
      $failures += "$rel has non-AnyCPU project mapping: $($m.Groups[0].Value.Trim())"
      break
    }
  }
}

if ($failures.Count -gt 0) {
  Write-Output 'VERIFY-SOLUTIONS FAILED:'
  $failures | ForEach-Object { Write-Output "  - $_" }
  exit 1
}

Write-Output "VERIFY-SOLUTIONS OK: $($whitelist.Count) whitelisted, $($allSlns.Count) found."
