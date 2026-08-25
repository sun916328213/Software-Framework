using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFCustomControls.ValueConvert;

/// <summary>
/// 布尔值取反转 Visibility 转换器。
/// true → Collapsed, false → Visible。
/// </summary>
public class BoolInverseToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Visible ? false : true;
    }
}
