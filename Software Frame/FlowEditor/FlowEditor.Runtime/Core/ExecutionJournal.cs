// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：JournalKind、JournalEntry、ExecutionJournal。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、System.Collections.Concurrent、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using System.Collections.Concurrent;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Runtime;

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
    /// <summary>默认容量上限（按条数）。超出后丢弃最旧条目，防止长循环导致无界增长（OOM）。</summary>
    public const int DefaultCapacity = 10_000;

    private readonly ConcurrentQueue<JournalEntry> _entries = new();
    private readonly ConcurrentDictionary<string, JournalEntry> _latestEdgeValues = new();
    private readonly long _originTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    private readonly int _capacity;
    private int _droppedCount;
    // 自维护原子计数：ConcurrentQueue.Count 是 O(segment 链) 的加锁遍历，
    // 在高频写入（循环每轮多条日志）下开销会急剧放大；用 O(1) 计数器替代。
    private int _liveCount;

    /// <summary>构造；capacity 条数上限（&gt;0），超出自动丢弃最旧条目。默认 <see cref="DefaultCapacity"/>。</summary>
    public ExecutionJournal(int capacity = DefaultCapacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>容量上限（条数）。超出后自动丢弃最旧条目。</summary>
    public int Capacity => _capacity;

    /// <summary>因容量上限被丢弃的条目数（诊断用）。</summary>
    public int DroppedCount => Volatile.Read(ref _droppedCount);

    /// <summary>全部条目（按发生顺序的快照）。</summary>
    public IReadOnlyList<JournalEntry> Entries => _entries.ToList();

    /// <summary>当前保留的条目数（O(1) 原子读取）。</summary>
    public int Count => Volatile.Read(ref _liveCount);

    /// <summary>清空（新一轮循环前调用，避免无限增长）。</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        _latestEdgeValues.Clear();
        Interlocked.Exchange(ref _droppedCount, 0);
        Interlocked.Exchange(ref _liveCount, 0);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RecordNodeStart。
    /// 正文中的关键协作调用包括 Enqueue、ElapsedMs；具体条件和先后次序见方法体。
    /// </summary>
    internal void RecordNodeStart(string nodeId, string name)
        => Enqueue(new JournalEntry(ElapsedMs(), JournalKind.NodeStart, nodeId, Text: name));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RecordNodeFinish。
    /// 正文中的关键协作调用包括 Enqueue、ElapsedMs；具体条件和先后次序见方法体。
    /// </summary>
    internal void RecordNodeFinish(string nodeId, string name, NodeStatus status, long elapsedMs)
        => Enqueue(new JournalEntry(ElapsedMs(), JournalKind.NodeFinish, nodeId,
            Text: $"{name}|{status}|{elapsedMs:F0}ms"));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RecordNodeSkipped。
    /// 正文中的关键协作调用包括 Enqueue、ElapsedMs；具体条件和先后次序见方法体。
    /// </summary>
    internal void RecordNodeSkipped(string nodeId, string name)
        => Enqueue(new JournalEntry(ElapsedMs(), JournalKind.NodeSkipped, nodeId, Text: name));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RecordEdgeTransfer。
    /// 正文中的关键协作调用包括 ElapsedMs、Enqueue；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ElapsedMs。
    /// 正文中的关键协作调用包括 GetElapsedTime；具体条件和先后次序见方法体。
    /// </summary>
    private long ElapsedMs() => System.Diagnostics.Stopwatch.GetElapsedTime(_originTimestamp).TotalMilliseconds switch
    {
        var ms => (long)ms
    };

    /// <summary>
    /// 入队并执行容量淘汰：当条目数超过上限时丢弃最旧条目（尽力而为，
    /// 并发下可能短暂超出上限 1~2 条，属可接受的诊断日志误差）。
    /// 计数用自维护原子量，避免 ConcurrentQueue.Count 的 O(segment) 加锁遍历。
    /// </summary>
    private void Enqueue(JournalEntry entry)
    {
        _entries.Enqueue(entry);
        var live = Interlocked.Increment(ref _liveCount);
        if (live > _capacity)
        {
            if (_entries.TryDequeue(out _))
                Interlocked.Decrement(ref _liveCount);
            Interlocked.Increment(ref _droppedCount);
        }
    }
}
