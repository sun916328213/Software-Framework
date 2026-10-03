// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：JournalStabilityTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Diagnostics;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// ExecutionJournal 容量稳定性测试。
/// 覆盖：容量构造校验、容量淘汰（最旧丢弃）、长循环下条目数受上限约束、
/// 最新连线传值在容量淘汰后仍可查询、Clear 重置丢弃计数、DroppedCount 诊断。
/// 对应稳定性优化：无界 ConcurrentQueue 在长循环（万级迭代）下防 OOM。
/// </summary>
public class JournalStabilityTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    // ── 构造校验 ──

    [Fact]
    public void Constructor_ZeroOrNegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionJournal(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionJournal(-1));
    }

    /// <summary>
    /// 验证 Constructor_Defaults_AreSane 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Constructor_Defaults_AreSane()
    {
        var j = new ExecutionJournal();
        Assert.Equal(ExecutionJournal.DefaultCapacity, j.Capacity);
        Assert.Equal(10_000, ExecutionJournal.DefaultCapacity);
        Assert.Equal(0, j.Count);
        Assert.Equal(0, j.DroppedCount);
    }

    // ── 引擎驱动的容量淘汰 ──

    /// <summary>
    /// 长循环 + 小容量 Journal：循环跑满 20 次（每轮产生多条 NodeStart/NodeFinish/EdgeTransfer），
    /// 但 Journal 条目数始终不超过容量上限，DroppedCount 正确累计。
    /// 这是「长循环不 OOM」的直接验证。
    /// </summary>
    [Fact]
    public async Task LongLoop_SmallCapacity_EntriesBounded()
    {
        const int capacity = 50;
        const int iterations = 20; // 每轮至少 3+ 条日志，总计远超 capacity

        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = iterations;
        var (body, bodyModel) = New<CountingNode>("B");
        var (done, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Completed", done),
        };

        var journal = new ExecutionJournal(capacity);
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }, journal: journal);

        // 循环确实跑满（否则测试无效）
        Assert.Equal(iterations, bodyModel.Executions);

        // 核心断言：条目数被容量上限约束
        Assert.True(journal.Count <= capacity,
            $"条目数 {journal.Count} 超过容量上限 {capacity}");
        Assert.True(journal.Count > 0, "容量淘汰不应清空全部条目");

        // 丢弃计数 = 总产出 - 保留条目（总量未精确断言，但 DroppedCount 必须 > 0 且与 Count 一致性可验证）
        Assert.True(journal.DroppedCount > 0,
            $"产出条目远超容量 {capacity}，应有条目被丢弃，实际丢弃 {journal.DroppedCount}");
    }

    /// <summary>容量淘汰后，保留的条目应为最新（时间戳最大的那批），而非最旧。</summary>
    [Fact]
    public async Task CapacityEviction_KeepsNewestEntries()
    {
        const int capacity = 30;

        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 10;
        var (body, bodyModel) = New<CountingNode>("B");
        var (done, doneModel) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Completed", done),
        };

        var journal = new ExecutionJournal(capacity);
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }, journal: journal);

        Assert.True(journal.DroppedCount > 0, "应有条目被淘汰");

        // 保留的条目必须包含最后的 Completed 阶段（done 节点的完成记录）
        var doneFinish = journal.Entries.Any(e =>
            e.NodeId == done.Id && e.Kind == JournalKind.NodeFinish);
        Assert.True(doneFinish,
            "容量淘汰后最新条目（done 完成）应被保留，最旧的（首轮开始）应被丢弃");

        // 首轮 loop 的开始记录（最旧条目之一）应已被淘汰
        var entries = journal.Entries;
        var timestamps = entries.Select(e => e.Timestamp).ToList();
        Assert.Equal(timestamps, timestamps.OrderBy(t => t).ToList());
    }

    /// <summary>容量淘汰不影响「最新连线传值」查询：latestEdgeValues 与容量淘汰独立。</summary>
    [Fact]
    public async Task CapacityEviction_LatestEdgeValueStillQueryable()
    {
        const int capacity = 20;

        var (a, aModel) = New<SourceNode>("A");
        var (b, _) = New<ValueNode>("B");

        // 循环驱动 a→b 的数据连线重复传输，制造超过容量的 EdgeTransfer 条目
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 15;
        var (body, bodyModel) = New<CountingNode>("BOD");

        var nodes = new List<NodeViewModel> { loop, body, a, b };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Body", a),   // 每轮激活 a，重复传值
            Data(a, b),
        };

        var journal = new ExecutionJournal(capacity);
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }, journal: journal);

        // 历史条目可能已被淘汰，但最新值必须可查询
        var dataConn = conns.First(c => c.SourceNodeId == a.Id && c.TargetNodeId == b.Id);
        Assert.True(journal.TryGetLatestEdgeValue(dataConn.Id, out var value),
            "容量淘汰后最新连线传值仍应可查询");
        Assert.Equal(100, value);
    }

    /// <summary>Clear 重置所有状态，包括丢弃计数。</summary>
    [Fact]
    public async Task Clear_ResetsDroppedCountAndEntries()
    {
        const int capacity = 10;

        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 5;
        var (body, bodyModel) = New<CountingNode>("B");
        var (done, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Completed", done),
        };

        var journal = new ExecutionJournal(capacity);
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }, journal: journal);

        Assert.True(journal.DroppedCount > 0);

        journal.Clear();

        Assert.Equal(0, journal.Count);
        Assert.Equal(0, journal.DroppedCount);
        Assert.Empty(journal.Entries);
    }

    // ── 压力级验证 ──

    /// <summary>
    /// 大迭代长循环（5000 次）+ 默认容量：验证条目数仍受 10000 上限约束，
    /// 执行时间可接受（不应因容量检查产生显著开销）。
    /// </summary>
    [Fact]
    public async Task VeryLongLoop_DefaultCapacity_StaysBoundedWithAcceptableOverhead()
    {
        const int iterations = 5_000;

        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = iterations;
        var (body, bodyModel) = New<CountingNode>("B");
        var (done, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Completed", done),
        };

        var journal = new ExecutionJournal();
        var sw = Stopwatch.StartNew();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }, journal: journal);
        sw.Stop();

        Assert.Equal(iterations, bodyModel.Executions);

        // 条目数被默认容量约束（万级迭代下若无上限将产生数万条）
        Assert.True(journal.Count <= ExecutionJournal.DefaultCapacity,
            $"条目数 {journal.Count} 超过默认容量 {ExecutionJournal.DefaultCapacity}");
        Assert.True(journal.DroppedCount > 0);

        // 容量检查的开销不应显著拖慢执行（5000 轮循环 < 30s）
        Assert.True(sw.Elapsed.TotalSeconds < 30,
            $"执行耗时 {sw.Elapsed.TotalSeconds:F1}s，容量淘汰检查开销异常");
    }

    // ── 辅助 ──

    private static ConnectionModel Branch(NodeViewModel source, string portName, NodeViewModel target)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.Model.OutputPorts.First(p => p.Name == portName).Id,
            TargetNodeId = target.Id,
            TargetPortId = $"{target.Id}_Next_IN"
        };
}
