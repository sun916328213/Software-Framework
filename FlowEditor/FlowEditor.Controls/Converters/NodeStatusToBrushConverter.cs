using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlowEditor.Nodes;

namespace FlowEditor.Converters;

/// <summary>
/// 节点执行状态 → 边框颜色转换器。
/// 直接引用 WPFCustomControls 的 ThemeColors.xaml 基础色板。
/// </summary>
public class NodeStatusToBrushConverter : IValueConverter
{
    /// <summary>从当前应用资源中读取指定画刷。</summary>
    private static Brush? GetResourceBrush(string key)
    {
        var resource = Application.Current?.TryFindResource(key);
        return resource is Brush brush ? brush : null;
    }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeStatus status)
        {
            var key = status switch
            {
                NodeStatus.Idle => "Color_Gray8",
                NodeStatus.Running => "Color_Green",
                NodeStatus.Success => "Color_Green",
                NodeStatus.Error => "Color_Red",
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

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
