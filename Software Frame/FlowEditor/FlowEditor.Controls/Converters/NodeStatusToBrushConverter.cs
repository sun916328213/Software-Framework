// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeStatusToBrushConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Globalization、System.Windows、System.Windows.Data、System.Windows.Media、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Converters;

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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Convert。
    /// 正文中的关键协作调用包括 GetResourceBrush、FromRgb；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ConvertBack。
    /// </remarks>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
