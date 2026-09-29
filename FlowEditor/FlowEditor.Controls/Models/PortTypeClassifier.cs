namespace FlowEditor.Models;

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
