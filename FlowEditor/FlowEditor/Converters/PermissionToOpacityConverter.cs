using System.Globalization;
using System.Windows.Data;
using FlowEditor.Models;

namespace FlowEditor.Converters;

/// <summary>
/// 节点权限 → 透明度转换器。
/// Normal=1.0, ReadOnly=0.8, Disabled=0.5。
/// </summary>
public class PermissionToOpacityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodePermission permission)
        {
            return permission switch
            {
                NodePermission.Normal => 1.0,
                NodePermission.ReadOnly => 0.8,
                NodePermission.Disabled => 0.5,
                _ => 1.0
            };
        }
        return 1.0;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
