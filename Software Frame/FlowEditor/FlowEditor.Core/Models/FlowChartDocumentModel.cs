// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartDocumentModel、NodeDocumentModel、CustomPortDocument、ConnectionDocumentModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core。导入不代表所有类型都会在运行时加载。
namespace FlowEditor.Core.Models;

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

    /// <summary>节点类型全名（用于反射还原）。旧文档仅有此字段；新文档优先使用 NodeTypeId。</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 稳定节点类型标识（P2，如 "flow.loop"）。保存时写入，加载时优先解析；
    /// 旧文档为空，加载时走 TypeName 兼容路径。类名/命名空间/程序集调整不影响此标识。
    /// </summary>
    public string NodeTypeId { get; set; } = string.Empty;

    /// <summary>
    /// 节点 Schema 版本（P2）。1 = 旧格式（仅 CLR 类型名）；2 = 稳定标识。
    /// 缺失插件恢复后可据此重新解析为真实节点。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

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

    /// <summary>是否位于不执行节点容器。</summary>
    public bool IsExcludedFromExecution { get; set; }

    /// <summary>所属结构化执行容器 ID。</summary>
    public string? ParentExecutionContainerId { get; set; }

    /// <summary>所属容器泳道 ID。</summary>
    public string? ExecutionLaneId { get; set; }

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

    /// <summary>端口数据类型；null 表示旧版文档未保存该字段，恢复时沿用节点默认类型。</summary>
    public PortType? DataType { get; set; }
}

/// <summary>连线文档模型。</summary>
public class ConnectionDocumentModel : ConnectionModel
{
}
