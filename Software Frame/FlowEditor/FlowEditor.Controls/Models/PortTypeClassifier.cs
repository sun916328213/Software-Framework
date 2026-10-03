// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PortTypeClassifier。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Models;
namespace FlowEditor.Controls.Models;

/// <summary>将节点属性的 CLR 类型稳定映射为编辑器端口类型。</summary>
public static class PortTypeClassifier
{
    /// <summary>把可空 CLR 类型归类为连线兼容性使用的端口类型。</summary>
    public static PortType FromClrType(Type? type)
    {
        if (type == null)
            return PortType.Any;

        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool)) return PortType.Boolean;
        if (type == typeof(string) || type == typeof(char)) return PortType.String;
        if (type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong)) return PortType.Integer;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return PortType.Double;

        var typeName = type.FullName ?? type.Name;
        if (typeName.Contains("HObject", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Bitmap", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Image", StringComparison.OrdinalIgnoreCase))
            return PortType.Image;

        return PortType.Object;
    }
}
