// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PermissionToOpacityConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Globalization、System.Windows.Data、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Globalization;
using System.Windows.Data;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Converters;

/// <summary>
/// 节点权限 → 透明度转换器。
/// Normal=1.0, ReadOnly=0.8, Disabled=0.5。
/// </summary>
public class PermissionToOpacityConverter : IValueConverter
{
    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Convert。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ConvertBack。
    /// </remarks>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
