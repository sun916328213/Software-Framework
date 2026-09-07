namespace FlowEditor.Models;

using FlowEditor.Core;

/// <summary>
/// 流程图文档模型（序列化根对象）。
/// 完整存储画布全局配置、所有节点、所有连线，支持 1:1 还原。
/// </summary>
public class FlowChartDocumentModel
{
    /// <summary>流程图名称。</summary>
    public string Name { get; set; } = "新建流程图";

    /// <summary>文件格式版本号。</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>画布全局缩放比例。</summary>
    public double ZoomLevel { get; set; } = 1.0;

    /// <summary>画布视图偏移 X。</summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>画布视图偏移 Y。</summary>
    public double OffsetY { get; set; } = 0;

    /// <summary>所有节点集合。</summary>
    public List<NodeDocumentModel> Nodes { get; set; } = new();

    /// <summary>所有连线集合。</summary>
    public List<ConnectionDocumentModel> Connections { get; set; } = new();
}

/// <summary>节点文档模型（序列化节点）。</summary>
public class NodeDocumentModel
{
    /// <summary>节点唯一 ID。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>节点类型全名（用于反射还原）。</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>画布 X 坐标。</summary>
    public double X { get; set; }

    /// <summary>画布 Y 坐标。</summary>
    public double Y { get; set; }

    /// <summary>IN 勾选状态。</summary>
    public bool IsInEnabled { get; set; }

    /// <summary>Out 勾选状态。</summary>
    public bool IsOutEnabled { get; set; }

    /// <summary>节点权限。</summary>
    public NodePermission Permission { get; set; } = NodePermission.Normal;

    /// <summary>失败处理策略。</summary>
    public NodeErrorPolicy ErrorPolicy { get; set; } = NodeErrorPolicy.FailFast;

    /// <summary>失败自动重试次数。</summary>
    public int RetryCount { get; set; }

    /// <summary>重试间隔（毫秒）。</summary>
    public int RetryDelayMs { get; set; }

    /// <summary>单次执行超时（毫秒），0 = 不限制。</summary>
    public int TimeoutMs { get; set; }

    /// <summary>自定义输入端口列表。</summary>
    public List<CustomPortDocument> CustomInputPorts { get; set; } = new();

    /// <summary>自定义输出端口列表（包含属性绑定）。</summary>
    public List<CustomPortDocument> CustomOutputPorts { get; set; } = new();

    /// <summary>节点附加属性字典（子类特殊属性）。</summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>端口 ID 映射表（用于连线还原）。</summary>
    public Dictionary<string, string> PortIdMap { get; set; } = new();
}

/// <summary>自定义端口文档模型。</summary>
public class CustomPortDocument
{
    /// <summary>端口显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>端口 ID。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>绑定的节点属性名（仅输出端口有效）。</summary>
    public string? BoundProperty { get; set; }
}

/// <summary>连线文档模型。</summary>
public class ConnectionDocumentModel : ConnectionModel
{
}
