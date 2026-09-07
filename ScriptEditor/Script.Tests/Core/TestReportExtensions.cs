namespace Script.Tests.Core;

/// <summary>报告摘要扩展方法。</summary>
public static class TestReportExtensions
{
    /// <summary>生成控制台摘要（类别 × 通过率的紧凑视图）。</summary>
    public static string ToSummary(this TestReport report)
    {
        var lines = new List<string>
        {
            $"用例总数: {report.TotalCount} | 通过: {report.PassedCount} | 失败: {report.FailedCount} | 总耗时: {report.TotalDurationMs:F0} ms"
        };
        foreach (var g in report.Results.GroupBy(r => r.Category))
        {
            var passed = g.Count(r => r.Passed);
            lines.Add($"  [{g.Key}] {passed}/{g.Count()} 通过");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
