namespace FlowEditor.Models;

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
