using System.Collections.Concurrent;
using FlowEditor.Nodes;

namespace FlowEditor.Core;

/// <summary>执行日志条目种类。</summary>
public static class JournalKind
{
    /// <summary>节点开始执行。</summary>
    public const string NodeStart = "node.start";
    /// <summary>节点执行完成。</summary>
    public const string NodeFinish = "node.finish";
    /// <summary>连线传值（FillInputPorts 阶段记录）。</summary>
    public const string EdgeTransfer = "edge.transfer";
    /// <summary>节点被跳过。</summary>
    public const string NodeSkipped = "node.skipped";
}

/// <summary>执行日志条目。</summary>
/// <param name="Timestamp">相对实例创建时刻的毫秒数。</param>
/// <param name="Kind">条目种类（JournalKind 常量）。</param>
/// <param name="NodeId">关联节点 ID。</param>
/// <param name="ConnectionId">关联连线 ID（边事件）。</param>
/// <param name="Text">人类可读描述。</param>
/// <param name="Value">载荷值（边传值时为传输的数据）。</param>
public sealed record JournalEntry(
    long Timestamp,
    string Kind,
    string? NodeId = null,
    string? ConnectionId = null,
    string Text = "",
    object? Value = null);

/// <summary>
/// 执行日志/数据追踪器（P4）。
/// 每次运行按序记录节点开始/完成、每条连线的实际传值快照，
/// 支持运行后点击任意连线查看最近一次传输值与完整时间线。
/// 线程安全；由 FlowInstance 持有，引擎可选写入。
/// </summary>
public sealed class ExecutionJournal
{
    private readonly ConcurrentQueue<JournalEntry> _entries = new();
    private readonly ConcurrentDictionary<string, JournalEntry> _latestEdgeValues = new();
    private readonly long _originTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>全部条目（按发生顺序的快照）。</summary>
    public IReadOnlyList<JournalEntry> Entries => _entries.ToList();

    /// <summary>累计条目数。</summary>
    public int Count => _entries.Count;

    /// <summary>清空（新一轮循环前调用，避免无限增长）。</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        _latestEdgeValues.Clear();
    }

    internal void RecordNodeStart(string nodeId, string name)
        => Enqueue(new JournalEntry(ElapsedMs(), JournalKind.NodeStart, nodeId, Text: name));

    internal void RecordNodeFinish(string nodeId, string name, NodeStatus status, long elapsedMs)
        => Enqueue(new JournalEntry(ElapsedMs(), JournalKind.NodeFinish, nodeId,
            Text: $"{name}|{status}|{elapsedMs:F0}ms"));

    internal void RecordNodeSkipped(string nodeId, string name)
        => Enqueue(new JournalEntry(ElapsedMs(), JournalKind.NodeSkipped, nodeId, Text: name));

    internal void RecordEdgeTransfer(string connectionId, object? value)
    {
        var entry = new JournalEntry(ElapsedMs(), JournalKind.EdgeTransfer, ConnectionId: connectionId, Value: value);
        Enqueue(entry);
        _latestEdgeValues[connectionId] = entry;
    }

    /// <summary>查询某条连线最近一次传输的值；从未传输过返回 false。</summary>
    public bool TryGetLatestEdgeValue(string connectionId, out object? value)
    {
        if (_latestEdgeValues.TryGetValue(connectionId, out var entry))
        {
            value = entry.Value;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>提取某节点的全部相关条目（开始/完成/跳过）。</summary>
    public IEnumerable<JournalEntry> GetNodeTimeline(string nodeId)
        => Entries.Where(e => e.NodeId == nodeId && e.Kind != JournalKind.EdgeTransfer);

    private long ElapsedMs() => System.Diagnostics.Stopwatch.GetElapsedTime(_originTimestamp).TotalMilliseconds switch
    {
        var ms => (long)ms
    };

    private void Enqueue(JournalEntry entry) => _entries.Enqueue(entry);
}
