namespace FlowEditor.Models;

/// <summary>撤销/重做操作类型。</summary>
public enum UndoRedoActionType
{
    NodeAdded,
    NodeDeleted,
    NodeMoved,
    NodePropertyChanged,
    ConnectionAdded,
    ConnectionDeleted,
    NodesCleared,
    PortAdded,
    PortDeleted,
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
