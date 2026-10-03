// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PortColorConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Globalization、System.Windows、System.Windows.Data、System.Windows.Media、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Converters;

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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Convert。
    /// 正文中的关键协作调用包括 ResolveBrush；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ConvertBack。
    /// </remarks>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
