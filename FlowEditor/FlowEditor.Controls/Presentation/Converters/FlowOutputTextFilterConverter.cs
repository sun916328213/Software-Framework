using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace FlowEditor.Presentation.Converters;

/// <summary>
/// 从原始输出文本派生当前视图文本。过滤仅影响显示，不修改缓冲和文件日志。
/// </summary>
public sealed class FlowOutputTextFilterConverter : IMultiValueConverter
{
    /// <summary>按级别和搜索词过滤逐行日志。</summary>
    /// <param name="values">依次为原始文本、级别标识和搜索词。</param>
    /// <param name="targetType">绑定目标类型。</param>
    /// <param name="parameter">未使用的转换参数。</param>
    /// <param name="culture">当前绑定区域性。</param>
    /// <returns>符合当前筛选条件的输出文本。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var raw = values.ElementAtOrDefault(0) as string ?? string.Empty;
        var level = values.ElementAtOrDefault(1) as string ?? "All";
        var search = (values.ElementAtOrDefault(2) as string)?.Trim();
        if (raw.Length == 0)
            return string.Empty;

        var result = new StringBuilder(raw.Length);
        using var reader = new StringReader(raw);
        while (reader.ReadLine() is { } line)
        {
            if (!MatchesLevel(line, level) ||
                (!string.IsNullOrEmpty(search) &&
                 !line.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
                continue;

            result.AppendLine(line);
        }

        return result.ToString();
    }

    /// <summary>输出视图为只读派生值，不支持反向写回原始日志。</summary>
    /// <param name="value">目标控件值。</param>
    /// <param name="targetTypes">源绑定类型。</param>
    /// <param name="parameter">未使用的转换参数。</param>
    /// <param name="culture">当前绑定区域性。</param>
    /// <returns>所有源绑定均保持不变。</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();

    /// <summary>判断单行日志是否属于所选显示级别。</summary>
    private static bool MatchesLevel(string line, string level)
    {
        if (string.Equals(level, "All", StringComparison.OrdinalIgnoreCase))
            return true;

        var actual = Classify(line);
        return string.Equals(actual, level, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>使用与执行日志落盘一致的标记把文本行分类为错误、警告或信息。</summary>
    private static string Classify(string line)
    {
        if (line.Contains("[错误]", StringComparison.Ordinal) ||
            line.Contains("[类型错误]", StringComparison.Ordinal) ||
            line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            return "Error";

        if (line.Contains("[警告]", StringComparison.Ordinal) ||
            line.Contains("[容错]", StringComparison.Ordinal) ||
            line.Contains("[重试]", StringComparison.Ordinal))
            return "Warning";

        return "Info";
    }
}
