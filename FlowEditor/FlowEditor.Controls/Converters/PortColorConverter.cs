using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlowEditor.Models;

namespace FlowEditor.Converters;

/// <summary>
/// 端口数据类型 → 颜色转换器。颜色保持稳定，使同类型端口可被快速识别。
/// </summary>
public class PortColorConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<PortType, (string ResourceKey, Color Fallback)> Palette =
        new Dictionary<PortType, (string, Color)>
        {
            [PortType.Flow] = ("Flow_Port_Flow", Color.FromRgb(0x8B, 0x5C, 0xF6)),
            [PortType.Boolean] = ("Flow_Port_Boolean", Color.FromRgb(0xE8, 0x5D, 0x8E)),
            [PortType.Integer] = ("Flow_Port_Integer", Color.FromRgb(0x71, 0xB8, 0x4B)),
            [PortType.Double] = ("Flow_Port_Double", Color.FromRgb(0xB8, 0xC0, 0xC8)),
            [PortType.String] = ("Flow_Port_String", Color.FromRgb(0x58, 0xA9, 0xDE)),
            [PortType.Image] = ("Flow_Port_Image", Color.FromRgb(0xF5, 0x9E, 0x0B)),
            [PortType.Object] = ("Flow_Port_Object", Color.FromRgb(0x4F, 0x67, 0xC7)),
            [PortType.Any] = ("Flow_Port_Any", Color.FromRgb(0x7B, 0x87, 0x94))
        };

    /// <summary>从当前应用资源中读取指定画刷。</summary>
    private static Brush? GetResourceBrush(string key)
    {
        var resource = Application.Current?.TryFindResource(key);
        return resource is Brush brush ? brush : null;
    }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value is PortModel port
            ? port.DataType
            : value is PortType portType ? portType : PortType.Any;
        return ResolveBrush(type);
    }

    /// <summary>解析端口类型的语义色，可供端口和数据连线共享。</summary>
    public static Brush ResolveBrush(PortType type)
    {
        var (resourceKey, fallback) = Palette.GetValueOrDefault(type, Palette[PortType.Any]);
        var brush = GetResourceBrush(resourceKey) ?? new SolidColorBrush(fallback);
        if (brush.CanFreeze && !brush.IsFrozen)
            brush.Freeze();
        return brush;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
