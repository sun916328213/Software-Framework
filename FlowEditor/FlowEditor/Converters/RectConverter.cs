using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowEditor.Converters;

/// <summary>
/// Rect 转换器（用于连线定位）。
/// </summary>
public class RectConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Rect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }
        return new Rect(0, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
