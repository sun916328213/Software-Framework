// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：MissingNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Nodes;

/// <summary>
/// 缺失插件占位节点（P2）。插件未安装时完整保留原始配置和连线：
/// 流程仍能打开、保存且不丢原始 Node 配置；插件恢复后可重新解析为真实节点。
/// <para/>
/// 运行语义：执行与验证均报稳定的 E-PLUGIN-MISSING，不抛 TypeLoadException；
/// 不参与工具箱（VisibleInToolbox=false）。
/// </summary>
[FlowTool("缺失节点", "AlertCircleOutline", ToolGroup = "常规", Description = "插件缺失时的占位节点（保留原始配置）", VisibleInToolbox = false)]
public class MissingNode : NodeModel
{
    /// <summary>创建空白占位节点（工厂还原时填充）。</summary>
    public MissingNode()
    {
        Name = "缺失节点";
    }

    /// <summary>原始 CLR 类型全名。</summary>
    public string OriginalTypeName { get; set; } = string.Empty;

    /// <summary>原始稳定节点类型标识（可为空，旧文档）。</summary>
    public string OriginalNodeTypeId { get; set; } = string.Empty;

    /// <summary>原始节点 Schema 版本。</summary>
    public int OriginalSchemaVersion { get; set; } = 1;

    /// <summary>捕获的原始附加属性（round-trip 保存时原样写回）。</summary>
    public Dictionary<string, object?> RawProperties { get; } = new();

    /// <summary>从节点文档捕获原始标识与配置。</summary>
    /// <param name="document">无法解析的节点文档。</param>
    /// <returns>填充完成的占位节点。</returns>
    public static MissingNode Capture(NodeDocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = new MissingNode
        {
            OriginalTypeName = document.TypeName,
            OriginalNodeTypeId = document.NodeTypeId,
            OriginalSchemaVersion = document.SchemaVersion
        };
        foreach (var (key, value) in document.Properties)
            node.RawProperties[key] = value;
        return node;
    }

    /// <summary>占位节点是否对应指定稳定类型标识。</summary>
    public bool MatchesTypeId(string? nodeTypeId) =>
        !string.IsNullOrWhiteSpace(nodeTypeId) &&
        string.Equals(OriginalNodeTypeId, nodeTypeId.Trim(), StringComparison.Ordinal);

    /// <summary>重建原始节点文档（插件恢复后升级为真实节点用）。</summary>
    public NodeDocumentModel RebuildDescriptor() => new()
    {
        Id = Id,
        TypeName = OriginalTypeName,
        NodeTypeId = OriginalNodeTypeId,
        SchemaVersion = OriginalSchemaVersion,
        Name = Name,
        X = X,
        Y = Y,
        IsInEnabled = IsInEnabled,
        IsOutEnabled = IsOutEnabled,
        Permission = Permission,
        IsExcludedFromExecution = IsExcludedFromExecution,
        ParentExecutionContainerId = ParentExecutionContainerId,
        ExecutionLaneId = ExecutionLaneId,
        ErrorPolicy = ErrorPolicy,
        RetryCount = RetryCount,
        RetryDelayMs = RetryDelayMs,
        TimeoutMs = TimeoutMs,
        CustomInputPorts = CustomInputPorts.Select(ToPortDocument).ToList(),
        CustomOutputPorts = CustomOutputPorts.Select(ToPortDocument).ToList(),
        Properties = new Dictionary<string, object?>(RawProperties),
        PortIdMap = new Dictionary<string, string>()
    };

    /// <summary>插件恢复后重新解析为真实节点（配置与连线所需的端口文档完整保留）。</summary>
    /// <param name="factory">节点工厂（此时应能解析原始类型）。</param>
    /// <returns>恢复后的真实节点；仍无法解析时返回自身。</returns>
    public NodeModel Upgrade(INodeFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        try
        {
            return factory.Create(RebuildDescriptor());
        }
        catch
        {
            return this;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Config；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        var id = string.IsNullOrWhiteSpace(OriginalNodeTypeId) ? OriginalTypeName : OriginalNodeTypeId;
        throw FlowNodeException.Config(
            "E-PLUGIN-MISSING", $"节点 [{Name}] 所需插件缺失（{id}），请安装对应节点包后重试。", nodeId: Id);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateAsync。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、FromResult、ValidationError；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx)
    {
        var id = string.IsNullOrWhiteSpace(OriginalNodeTypeId) ? OriginalTypeName : OriginalNodeTypeId;
        return Task.FromResult<IEnumerable<FlowDiagnostic>>(
            [ValidationError("E-PLUGIN-MISSING", $"所需插件缺失（{id}），安装后可恢复。原始配置已保留。")]);
    }

    /// <summary>将运行时端口转为端口文档。</summary>
    /// <param name="port">待转换端口。</param>
    private static CustomPortDocument ToPortDocument(PortModel port) => new()
    {
        Id = port.Id,
        Name = port.Name,
        BoundProperty = port.PropertyName,
        DataType = port.DataType
    };
}
