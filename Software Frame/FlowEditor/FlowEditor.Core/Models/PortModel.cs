// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PortDirection、PortCategory、PortModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowEditor.Core.Models;

/// <summary>端口方向。</summary>
public enum PortDirection
{
    /// <summary>输入端口。</summary>
    Input,
    /// <summary>输出端口。</summary>
    Output
}

/// <summary>端口类型分类。</summary>
public enum PortCategory
{
    /// <summary>内置标准端口（Next_IN / Next_Out）。</summary>
    Standard,
    /// <summary>自定义端口（用户手动添加）。</summary>
    Custom
}

/// <summary>流程图端口模型，封装方向、类型和运行时值。</summary>
public partial class PortModel : ObservableObject
{
    /// <summary>端口唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
 
      private string _name = string.Empty;
    /// <summary>端口显示名称。</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name,
            string.IsNullOrWhiteSpace(value) ? "未命名端口" : value.Trim());
    }

    /// <summary>对应的节点属性名（用于 SyncInputValues 将端口值写入属性，或输出端口绑定属性）。</summary>
    public string? PropertyName { get; set; }

    /// <summary>端口方向（输入/输出）。</summary>
    public PortDirection Direction { get; set; }

    /// <summary>端口分类（标准端口 / 自定义端口）。</summary>
    public PortCategory Category { get; set; } = PortCategory.Custom;

    /// <summary>端口数据类型。</summary>
    public PortType DataType { get; set; } = PortType.Any;

    /// <summary>面向界面的完整端口说明。</summary>
    public string ToolTip => $"{(Direction == PortDirection.Input ? "输入" : "输出")} · {DataType} · {Name}";

    /// <summary>所属节点 ID。</summary>
    public string OwnerNodeId { get; set; } = string.Empty;

    private object? _value;
    /// <summary>端口当前值（执行时由引擎写入）。</summary>
    public object? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>验证传入值是否与端口类型兼容。</summary>
    public bool IsCompatible(object? value)
    {
        if (value is null) return true;
        return DataType switch
        {
            PortType.Any => true,
            PortType.Boolean => value is bool,
            PortType.Integer => value is int or long or short or byte,
            PortType.Double => value is double or float or decimal,
            PortType.String => value is string,
            PortType.Image => true,
            PortType.Object => true,
            _ => true
        };
    }

    /// <summary>验证源端口声明类型是否与当前端口类型兼容（用于连线时类型校验）。</summary>
    public bool IsTypeCompatibleWith(PortType sourceType)
    {
        if (DataType == PortType.Any || sourceType == PortType.Any) return true;
        return DataType == sourceType;
    }

    /// <summary>创建输入端口。</summary>
    public static PortModel CreateInput(string name, PortType type = PortType.Any, string ownerNodeId = "",
        PortCategory category = PortCategory.Custom)
        => new() { Name = name, Direction = PortDirection.Input, DataType = type, OwnerNodeId = ownerNodeId, Category = category };

    /// <summary>创建输出端口。</summary>
    public static PortModel CreateOutput(string name, PortType type = PortType.Any, string ownerNodeId = "",
        PortCategory category = PortCategory.Custom)
        => new() { Name = name, Direction = PortDirection.Output, DataType = type, OwnerNodeId = ownerNodeId, Category = category };
}
