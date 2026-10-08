#requires -Version 7.0
<#
中文阅读说明：
  Workbench 测试宿主「关闭链路」验收脚本。
  背景：七个 *.Workbench.Tests 夹具各自在 STA 线程上跑真实的 Prism App，退出时经
  Build/TestSupport/WorkbenchTestLifetime.cs 走真实关闭路径
  （Window.Close → Closing 排空 → Closed → Application.Exit → UI 线程退出）。
  该文件曾整体丢失（见 Build/PROVENANCE.md），本脚本是 **T02 重建物**，不是逐字节恢复。

  本脚本回答的问题：**这套关闭链路是不是确定性的？**
  —— 同一夹具重复跑 N 次，每次独占一个测试宿主进程，是否每次都能在超时内真正关掉、
  且从不出现「Closed/Exit 未触发」「UI 线程没退出」「关闭前场景失败」这几类失败。

  做三件事：
    ① 源码级守卫：凡是通过 <Compile Include> 引入共享生命周期的工程，其夹具必须调用 CloseAndWait。
    ② 逐工程 × N 轮真实运行（每轮独立进程），断言退出码 0、TRX 完整、零失败。
    ③ 在日志里显式搜索关闭链路的失败文案（而不是只看「测试全过」这一层推断）。

  用法（必须 PowerShell 7+，见 Build/PROVENANCE.md）：
    & $pwsh -NoProfile -ExecutionPolicy Bypass -File Build/Test-WorkbenchTestHostShutdown.ps1 -Runs 3

  默认排除 Category=ExternalDatabase 的测试（它们连本机真实 PG/MySQL，没库时必然 30 秒超时，
  属环境问题而非关闭链路问题）。要连库跑就加 -IncludeDatabaseTests。

  本机（VS 常驻会锁 bin 下的 .pdb）建议追加：
    -BuildProperty 'DebugType=none','DebugSymbols=false','UseSharedCompilation=false'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateRange(1, 20)][int]$Runs = 3,
    [string]$ResultsDirectory,
    [switch]$NoBuild,
    [switch]$IncludeDatabaseTests,
    [string]$BlameHangTimeout,
    [string[]]$BuildProperty = @()
)

$ErrorActionPreference = 'Stop'

$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$frameworkRoot = Join-Path $repo 'Software Frame'
$sharedLifetime = Join-Path $PSScriptRoot 'TestSupport/WorkbenchTestLifetime.cs'
if (-not $ResultsDirectory) { $ResultsDirectory = Join-Path $repo 'TestResults/workbench-testhost-shutdown' }

if (-not (Test-Path -LiteralPath $sharedLifetime -PathType Leaf)) {
    throw "共享测试生命周期缺失：$sharedLifetime（七个 Workbench.Tests 工程都以源码链接引用它）"
}

# ── ① 源码级守卫：消费共享生命周期的工程，其夹具必须真的走 CloseAndWait ────────────────
# 之所以从 csproj 反查而不是写死七个路径：一旦有人新增/改名 Workbench.Tests 工程，
# 写死的清单会静默漏跑；反查则自动纳管，并在下面的数量守卫上暴露变化。
$consumerProjects = @(
    Get-ChildItem -LiteralPath $frameworkRoot -Filter '*.csproj' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.vs|secs4net-base)[\\/]' -and $_.Name -notlike '*_wpftmp.csproj' } |
        Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match 'WorkbenchTestLifetime\.cs' } |
        ForEach-Object { $_.FullName } |
        Sort-Object
)
if (-not $consumerProjects.Count) {
    throw "没有任何工程引用 $sharedLifetime —— 共享生命周期已失去调用方，本脚本失去验收对象。"
}

$fixtureGuard = @()
foreach ($project in $consumerProjects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    $fixture = Join-Path (Split-Path $project -Parent) 'WorkbenchAppFixture.cs'
    if (-not (Test-Path -LiteralPath $fixture -PathType Leaf)) {
        throw "$name 引用了共享生命周期，却没有 WorkbenchAppFixture.cs —— 关闭链路无人验收。"
    }
    $fixtureText = [IO.File]::ReadAllText($fixture)
    if ($fixtureText -notmatch 'CloseAndWait\s*\(') {
        throw "$name 的夹具没有调用 CloseAndWait —— 它不会走真实关闭路径。"
    }
    if ($fixtureText -notmatch 'ObserveMainWindow\s*\(') {
        throw "$name 的夹具没有调用 ObserveMainWindow —— 主窗未被登记，Closed 断言会落空。"
    }
    $fixtureGuard += $name
}
Write-Host "共享测试生命周期调用方：$($consumerProjects.Count) 个工程，夹具均已调用 ObserveMainWindow + CloseAndWait。"

# ── ② 逐工程 × N 轮真实运行（每轮独占进程）──────────────────────────────────────────
# 这些文案来自 WorkbenchTestLifetime 的抛出点。把它们显式搜出来，
# 是为了让「关闭链路失败」在报告里直接可见，而不是被「测试全过」这一层推断掩盖。
$shutdownFailureMarkers = @(
    '主窗口未触发 Closed 事件',
    'Application.Exit 未触发',
    '工作台 UI 线程在',
    '工作台 UI 线程以异常结束',
    '关闭前的运行场景失败',
    '工作台未创建主窗口',
    '工作台未初始化退出处理'
)

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

$baseArguments = @('test')
if ($NoBuild) { $baseArguments += @('--no-build', '--no-restore') }
# DataManager.Workbench.Tests 里的 SqlViewModelSmokeTests 带 [Trait("Category","ExternalDatabase")]，
# 连的是本机真实 PG（DM_PG_* 环境变量）。没有库的机器上它会 30 秒超时失败，
# 而那是环境问题、不是关闭链路问题 —— 与 Test-Integration.ps1 同一口径默认排除。
if (-not $IncludeDatabaseTests) { $baseArguments += @('--filter', 'Category!=ExternalDatabase') }
foreach ($property in $BuildProperty) { $baseArguments += "-p:$property" }
if ($BlameHangTimeout) { $baseArguments += @('--blame-hang-timeout', $BlameHangTimeout) }

$records = @()
$failures = @()
$runIndex = 0
$totalRuns = $consumerProjects.Count * $Runs

foreach ($project in $consumerProjects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    for ($round = 1; $round -le $Runs; $round++) {
        $runIndex++
        $logName = "$name-run$round"
        $trxPath = Join-Path $ResultsDirectory "$logName.trx"
        $logPath = Join-Path $ResultsDirectory "$logName.log"
        if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath -Force }

        Write-Host "[$runIndex/$totalRuns] $name 第 $round 轮（独立进程）…"

        $arguments = $baseArguments + @(
            $project, '-c', $Configuration,
            '--logger', "trx;LogFileName=$logName.trx",
            '--results-directory', $ResultsDirectory
        )
        & dotnet @arguments *>&1 | Tee-Object -FilePath $logPath | Out-Null
        $exitCode = $LASTEXITCODE

        $record = [pscustomobject]@{
            Project = $name
            Round   = $round
            ExitCode = $exitCode
            Total   = 0
            Passed  = 0
            Failed  = 0
            Trx     = (Test-Path -LiteralPath $trxPath)
        }

        $log = if (Test-Path -LiteralPath $logPath) { [IO.File]::ReadAllText($logPath) } else { '' }
        $markers = @($shutdownFailureMarkers | Where-Object { $log.Contains($_) })

        if (-not $record.Trx) {
            $failures += "$name 第 $round 轮没有产出 TRX（测试宿主未正常收尾）"
        }
        else {
            $trx = [xml](Get-Content -LiteralPath $trxPath -Raw)
            $counters = $trx.TestRun.ResultSummary.Counters
            $record.Total = [int]$counters.total
            $record.Passed = [int]$counters.passed
            $record.Failed = [int]$counters.failed
            if ($record.Total -eq 0) { $failures += "$name 第 $round 轮 TRX 为空（0 个测试）" }
            if ($record.Failed -gt 0) { $failures += "$name 第 $round 轮有 $($record.Failed) 个失败" }
        }

        if ($markers.Count) {
            $failures += "$name 第 $round 轮出现关闭链路失败文案：$($markers -join ' / ')"
        }
        if ($exitCode -ne 0) {
            $failures += "$name 第 $round 轮退出码 $exitCode（非 0）"
        }

        $records += $record
        Write-Host ("    -> exit={0} total={1} passed={2} failed={3}" -f $record.ExitCode, $record.Total, $record.Passed, $record.Failed)
    }
}

# ── ③ 汇总 ─────────────────────────────────────────────────────────────────────────
$evidencePath = Join-Path $ResultsDirectory 'shutdown-evidence.json'
[pscustomobject]@{
    generatedAt       = (Get-Date).ToString('o')
    configuration     = $Configuration
    runsPerProject    = $Runs
    consumerProjects  = $consumerProjects
    records           = $records
    failures          = $failures
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $evidencePath -Encoding utf8

$records | Format-Table -AutoSize | Out-String -Width 200 | Write-Host

if ($failures.Count) {
    throw "Workbench 测试宿主关闭链路验收未通过（$($failures.Count) 项）：`n$($failures -join "`n")"
}

$totalTests = ($records | Measure-Object -Property Passed -Sum).Sum
Write-Host "Workbench 测试宿主关闭链路验收通过：$($consumerProjects.Count) 个工程 × $Runs 轮 = $totalRuns 个独立进程，共 $totalTests 条测试，零失败。"
Write-Host "证据：$evidencePath"
