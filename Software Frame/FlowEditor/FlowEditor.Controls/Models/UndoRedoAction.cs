// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：UndoRedoActionType、UndoRedoAction。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Models;
namespace FlowEditor.Controls.Models;

/// <summary>撤销/重做操作类型。</summary>
public enum UndoRedoActionType
{
    /// <summary>添加节点。</summary>
    NodeAdded,
    /// <summary>删除节点。</summary>
    NodeDeleted,
    /// <summary>移动节点。</summary>
    NodeMoved,
    /// <summary>修改节点属性。</summary>
    NodePropertyChanged,
    /// <summary>添加连线。</summary>
    ConnectionAdded,
    /// <summary>删除连线。</summary>
    ConnectionDeleted,
    /// <summary>清空全部节点。</summary>
    NodesCleared,
    /// <summary>添加端口。</summary>
    PortAdded,
    /// <summary>删除端口。</summary>
    PortDeleted,
    /// <summary>重命名端口。</summary>
    PortRenamed
}

/// <summary>撤销/重做操作记录。</summary>
public class UndoRedoAction
{
    /// <summary>操作类型。</summary>
    public UndoRedoActionType ActionType { get; set; }

    /// <summary>操作描述（用于调试和日志）。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>操作时间戳。</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>序列化的操作前数据（JSON）。</summary>
    public string? BeforeData { get; set; }

    /// <summary>序列化的操作后数据（JSON）。</summary>
    public string? AfterData { get; set; }

    /// <summary>相关节点 ID。</summary>
    public string? NodeId { get; set; }

    /// <summary>相关连线 ID。</summary>
    public string? ConnectionId { get; set; }
}
