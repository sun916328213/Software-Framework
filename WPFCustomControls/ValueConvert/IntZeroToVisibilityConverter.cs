using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFCustomControls.ValueConvert;

/// <summary>
/// int 值为 0（或 null）时返回 Visible，否则 Collapsed。用于集合为空时的占位提示。
/// </summary>
public class IntZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
