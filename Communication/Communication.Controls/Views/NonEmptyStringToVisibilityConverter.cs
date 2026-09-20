using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Communication.Controls.Views;

/// <summary>非空非空白字符串 → Visible，否则 Collapsed（用于「无提示文本时收起提示行」）。</summary>
public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
