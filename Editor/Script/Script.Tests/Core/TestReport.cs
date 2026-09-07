using System.IO;
using System.Text;

namespace Script.Tests.Core;

/// <summary>单项测试结果。</summary>
public sealed class TestResult
{
    /// <summary>测试名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>测试类别（如“核心功能验证”、“性能测试”、“UI 稳定性”）。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>是否通过。</summary>
    public bool Passed { get; init; }

    /// <summary>测试耗时（毫秒）。</summary>
    public double DurationMs { get; init; }

    /// <summary>失败时的错误信息。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>关键性能指标（名称 → 值 + 单位说明），用于报告中展示。</summary>
    public Dictionary<string, string> Metrics { get; } = new();
}

/// <summary>整个测试套件的汇总报告。</summary>
public sealed class TestReport
{
    /// <summary>报告生成时间。</summary>
    public DateTime GeneratedAt { get; } = DateTime.Now;

    /// <summary>宿主环境描述（OS / CLR / 处理器数）。</summary>
    public string Environment { get; init; } = string.Empty;

    /// <summary>全部测试结果。</summary>
    public List<TestResult> Results { get; } = new();

    public int TotalCount => Results.Count;
    public int PassedCount => Results.Count(r => r.Passed);
    public int FailedCount => Results.Count(r => !r.Passed);
    /// <summary>总耗时（毫秒）。</summary>
    public double TotalDurationMs => Results.Sum(r => r.DurationMs);

    public void Add(TestResult result) => Results.Add(result);
}

/// <summary>
/// 测试报告生成器：输出 Markdown 主报告 + 结构化 JSON（便于程序化消费）。
/// 报告目录：可执行文件同级 TestReports\。
/// </summary>
public static class TestReporter
{
    /// <summary>生成报告并写盘，返回报告文件路径。</summary>
    public static string Write(TestReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var stamp = report.GeneratedAt.ToString("yyyyMMdd_HHmmss");
        var mdPath = Path.Combine(outputDirectory, $"TestReport_{stamp}.md");
        var jsonPath = Path.Combine(outputDirectory, $"TestReport_{stamp}.json");

        File.WriteAllText(mdPath, BuildMarkdown(report), Encoding.UTF8);
        File.WriteAllText(jsonPath, BuildJson(report), Encoding.UTF8);
        return mdPath;
    }

    /// <summary>构建 Markdown 格式报告。</summary>
    public static string BuildMarkdown(TestReport report)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine($"# Script.Tests 测试报告");
        sb.AppendLine();
        sb.AppendLine($"- 生成时间：{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 环境：{report.Environment}");
        sb.AppendLine($"- 总用例：{report.TotalCount}，通过 {report.PassedCount}，失败 {report.FailedCount}，合计 {report.TotalDurationMs:F0} ms");
        sb.AppendLine();

        foreach (var category in report.Results.GroupBy(r => r.Category))
        {
            var passed = category.Count(r => r.Passed);
            sb.AppendLine($"## {category.Key}（{passed}/{category.Count()} 通过）");
            sb.AppendLine();
            sb.AppendLine("| 用例 | 结果 | 耗时(ms) | 关键指标 |");
            sb.AppendLine("|------|------|---------:|----------|");
            foreach (var r in category)
            {
                var metrics = r.Metrics.Count > 0
                    ? string.Join("; ", r.Metrics.Select(kv => $"{kv.Key}={kv.Value}"))
                    : "-";
                var status = r.Passed ? "✓" : $"✗ {r.ErrorMessage}";
                sb.AppendLine($"| {r.Name} | {status} | {r.DurationMs:F1} | {metrics} |");
            }
            sb.AppendLine();
        }

        if (report.FailedCount > 0)
        {
            sb.AppendLine("## 失败详情");
            sb.AppendLine();
            foreach (var r in report.Results.Where(r => !r.Passed))
            {
                sb.AppendLine($"- **{r.Category} / {r.Name}**：{r.ErrorMessage}");
                foreach (var kv in r.Metrics)
                    sb.AppendLine($"  - {kv.Key} = {kv.Value}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>构建 JSON 格式报告（简单手写序列化，避免引入第三方依赖）。</summary>
    public static string BuildJson(TestReport report)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("{");
        sb.AppendLine($"  \"generatedAt\": \"{report.GeneratedAt:O}\",");
        sb.AppendLine($"  \"environment\": {JsonEscape(report.Environment)},");
        sb.AppendLine($"  \"total\": {report.TotalCount},");
        sb.AppendLine($"  \"passed\": {report.PassedCount},");
        sb.AppendLine($"  \"failed\": {report.FailedCount},");
        sb.AppendLine($"  \"totalDurationMs\": {report.TotalDurationMs:F1},");
        sb.AppendLine("  \"results\": [");
        for (int i = 0; i < report.Results.Count; i++)
        {
            var r = report.Results[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": {JsonEscape(r.Name)},");
            sb.AppendLine($"      \"category\": {JsonEscape(r.Category)},");
            sb.AppendLine($"      \"passed\": {r.Passed.ToString().ToLowerInvariant()},");
            sb.AppendLine($"      \"durationMs\": {r.DurationMs:F1},");
            sb.AppendLine($"      \"errorMessage\": {(r.ErrorMessage == null ? "null" : JsonEscape(r.ErrorMessage))},");
            sb.AppendLine("      \"metrics\": {");
            for (int j = 0; j < r.Metrics.Count; j++)
            {
                var kv = r.Metrics.ElementAt(j);
                sb.AppendLine($"        {JsonEscape(kv.Key)}: {JsonEscape(kv.Value)}{(j < r.Metrics.Count - 1 ? "," : "")}");
            }
            sb.AppendLine("      }");
            sb.Append("    }");
            if (i < report.Results.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>JSON 字符串转义。</summary>
    private static string JsonEscape(string s) =>
        System.Text.Json.JsonEncodedText.Encode(s).ToString();
}
