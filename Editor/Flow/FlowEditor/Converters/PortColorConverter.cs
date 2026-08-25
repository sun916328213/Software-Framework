using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlowEditor.Models;

namespace FlowEditor.Converters;

/// <summary>
/// 端口方向 → 颜色转换器。
/// 直接引用 WPFCustomControls 的 ThemeColors.xaml 基础色板。
/// </summary>
public class PortColorConverter : IValueConverter
{
    private static Brush? GetResourceBrush(string key)
    {
        var resource = Application.Current?.TryFindResource(key);
        return resource is Brush brush ? brush : null;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PortDirection direction)
        {
            var key = direction switch
            {
                PortDirection.Input => "Color_Blue",
                PortDirection.Output => "Color_Orange",
                _ => null
            };
            if (key != null)
            {
                var brush = GetResourceBrush(key);
                if (brush != null) return brush;
            }
        }
        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
