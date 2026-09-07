using System.Diagnostics;
using System.IO;

namespace Script.Tests.Core;

/// <summary>
/// 单个测试用例的执行上下文：通过 <see cref="Metric"/> 记录关键性能指标，
/// 通过 <see cref="Fail"/> / <see cref="Assert"/> 主动判定失败。
/// 不调用 Fail 且未抛异常即视为通过。
/// </summary>
public sealed class TestContext
{
    private readonly Dictionary<string, string> _metrics = new();
    internal string? ErrorMessage { get; private set; }
    internal IReadOnlyDictionary<string, string> Metrics => _metrics;

    /// <summary>记录一个关键性能指标（名称 → 值，值中可含单位）。</summary>
    public void Metric(string name, string value) => _metrics[name] = value;

    /// <summary>记录数值型指标（自动附加 ms 后缀）。</summary>
    public void Metric(string name, double milliseconds) => _metrics[name] = $"{milliseconds:F2} ms";

    /// <summary>标记测试失败并附原因。</summary>
    public void Fail(string message)
    {
        if (ErrorMessage == null) ErrorMessage = message;
    }

    /// <summary>条件断言：condition 为 false 时标记失败。</summary>
    public void Assert(bool condition, string message)
    {
        if (!condition) Fail(message);
    }
}

/// <summary>
/// 测试场景调度器：统一封装用例注册、执行计时、异常捕获与结果收集。
/// 用例串行执行（保证场景间互不干扰、指标可复现），结果汇总进 <see cref="TestReport"/>。
/// </summary>
public sealed class TestRunner
{
    private readonly record struct Case(string Name, string Category, Func<TestContext, Task> Action);

    private readonly List<Case> _cases = new();
    private readonly TestReport _report = new()
    {
        Environment = $"{Environment.OSVersion.VersionString}; CLR {Environment.Version}; {Environment.ProcessorCount} 逻辑处理器"
    };

    /// <summary>注册一个测试用例。</summary>
    public void Register(string category, string name, Func<TestContext, Task> action)
        => _cases.Add(new Case(name, category, action));

    /// <summary>注册一个同步测试用例。</summary>
    public void Register(string category, string name, Action<TestContext> action)
        => _cases.Add(new Case(name, category, ctx => { action(ctx); return Task.CompletedTask; }));

    /// <summary>执行全部用例，返回汇总报告。</summary>
    public async Task<TestReport> RunAllAsync(TextWriter? console = null)
    {
        console?.WriteLine($"====== Script.Tests 开始：共 {_cases.Count} 个用例 ======");
        var swTotal = Stopwatch.StartNew();

        foreach (var c in _cases)
        {
            var ctx = new TestContext();
            var sw = Stopwatch.StartNew();
            try
            {
                await c.Action(ctx);
            }
            catch (Exception ex)
            {
                ctx.Fail($"异常: {ex.Message}");
            }
            sw.Stop();

            var result = new TestResult
            {
                Name = c.Name,
                Category = c.Category,
                Passed = ctx.ErrorMessage == null,
                ErrorMessage = ctx.ErrorMessage,
                DurationMs = sw.Elapsed.TotalMilliseconds
            };
            foreach (var kv in ctx.Metrics)
                result.Metrics[kv.Key] = kv.Value;

            _report.Add(result);
            console?.WriteLine(
                $"[{(result.Passed ? "PASS" : "FAIL")}] {c.Category} / {c.Name} ({result.DurationMs:F0} ms)" +
                (result.Passed ? "" : $" -> {result.ErrorMessage}"));
        }

        swTotal.Stop();
        console?.WriteLine($"====== 完成：{_report.PassedCount}/{_report.TotalCount} 通过，总耗时 {swTotal.Elapsed.TotalMilliseconds:F0} ms ======");
        return _report;
    }
}
