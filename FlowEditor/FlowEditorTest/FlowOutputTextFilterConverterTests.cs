using System.Globalization;
using FlowEditor.Presentation.Converters;
using Xunit;

namespace FlowEditorTest;

/// <summary>验证输出视图过滤不会改变原始日志，并正确组合级别与搜索条件。</summary>
public sealed class FlowOutputTextFilterConverterTests
{
    private readonly FlowOutputTextFilterConverter _converter = new();

    /// <summary>验证错误级别只显示错误和异常行。</summary>
    [Fact]
    public void Convert_ErrorLevel_ReturnsOnlyErrorLines()
    {
        const string raw = "[10:00] 普通信息\n[10:01] [错误] 节点失败\n[10:02] InvalidOperationException\n[10:03] [重试] 第一次";

        var result = Convert(raw, "Error", string.Empty);

        Assert.DoesNotContain("普通信息", result);
        Assert.Contains("[错误] 节点失败", result);
        Assert.Contains("InvalidOperationException", result);
        Assert.DoesNotContain("[重试]", result);
    }

    /// <summary>验证警告级别与搜索词会共同缩小显示范围。</summary>
    [Fact]
    public void Convert_WarningLevelAndSearch_AppliesBothConditions()
    {
        const string raw = "[10:00] [警告] Alpha\n[10:01] [容错] Beta\n[10:02] [重试] alpha retry";

        var result = Convert(raw, "Warning", "ALPHA");

        Assert.Contains("[警告] Alpha", result);
        Assert.Contains("[重试] alpha retry", result);
        Assert.DoesNotContain("Beta", result);
    }

    /// <summary>调用转换器并返回派生的显示文本。</summary>
    private string Convert(string raw, string level, string search)
        => Assert.IsType<string>(_converter.Convert(
            [raw, level, search], typeof(string), string.Empty, CultureInfo.InvariantCulture));
}
