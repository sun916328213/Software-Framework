// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowInstance。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Core;

/// <summary>
/// 流程运行实例（P3 编辑/运行分离的运行层）。
/// 从 FlowProgram 物化出一套完全独立的节点对象与连线，经引擎执行；
/// 同一 Program 可并发创建任意多个实例，节点状态/端口值/执行计数互不干扰。
/// 不依赖 WPF 与 IoC 容器，可在服务/控制台等无头环境直接运行。
/// </summary>
public sealed class FlowInstance
{
    private readonly List<NodeModel> _nodes;
    private readonly List<ConnectionModel> _connections;
    private readonly FlowRuntimeInstance<NodeModel> _runtime;
    private readonly IFlowLogSink _flowLogSink;

    /// <summary>创建已经物化并具有独立运行状态的流程实例。</summary>
    private FlowInstance(
        string name,
        List<NodeModel> nodes,
        List<ConnectionModel> connections,
        FlowChartExecutionEngine engine,
        IFlowLogSink flowLogSink)
    {
        Name = name;
        _nodes = nodes;
        _connections = connections;
        _runtime = new FlowRuntimeInstance<NodeModel>(name, nodes, connections, engine);
        _flowLogSink = flowLogSink;

        // 节点状态变化事件桥（订阅方自行处理线程封送）
        foreach (var node in nodes)
        {
            node.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NodeModel.Status))
                    NodeStatusChanged?.Invoke(node.Id, node.Status);
            };
        }
    }

    /// <summary>流程名称。</summary>
    public string Name { get; }

    /// <summary>本实例独立的节点集合（与其它实例、编辑画布完全隔离）。</summary>
    public IReadOnlyList<NodeModel> Nodes => _nodes;

    /// <summary>本实例独立的连线集合。</summary>
    public IReadOnlyList<ConnectionModel> Connections => _connections;

    /// <summary>执行日志/数据追踪器（P4）：记录节点事件与每条连线的传值快照。</summary>
    public ExecutionJournal Journal => _runtime.Journal;

    /// <summary>调试控制器（P4）：断点与单步门闩；非 null 时 RunAsync 自动启用。</summary>
    public DebugController? Debug
    {
        get => _runtime.Debug;
        set => _runtime.Debug = value;
    }

    /// <summary>节点状态变化事件（参数：节点 ID、新状态）。后台线程触发。</summary>
    public event Action<string, NodeStatus>? NodeStatusChanged;

    /// <summary>从编译产物创建运行实例。</summary>
    /// <param name="program">流程程序。</param>
    /// <param name="factory">节点工厂；默认纯反射实现。</param>
    /// <param name="engine">执行引擎；默认创建带桌面日志上下文的实例专用引擎。</param>
    /// <param name="flowLogSink">结构化流程日志 sink；空值时不产生外部日志副作用。</param>
    public static FlowInstance Create(
        FlowProgram program,
        INodeFactory? factory = null,
        FlowChartExecutionEngine? engine = null,
        IFlowLogSink? flowLogSink = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        factory ??= new ReflectionNodeFactory();
        engine ??= new FlowChartExecutionEngine(new FlowLogExecutionContextAdapter());

        var nodeMap = new Dictionary<string, NodeModel>(program.Nodes.Count);
        var nodes = new List<NodeModel>(program.Nodes.Count);
        foreach (var descriptor in program.Nodes)
        {
            var node = factory.Create(descriptor);
            if (!nodeMap.TryAdd(node.Id, node))
                throw new InvalidOperationException($"流程 [{program.Name}] 存在重复节点 ID: {node.Id}");
            nodes.Add(node);
        }

        var connections = new List<ConnectionModel>(program.Connections.Count);
        foreach (var connDoc in program.Connections)
        {
            // 两端节点都存在的连线才有效（与编辑器容错一致）
            if (!nodeMap.ContainsKey(connDoc.SourceNodeId) || !nodeMap.ContainsKey(connDoc.TargetNodeId))
                continue;
            connections.Add(new ConnectionModel
            {
                Id = connDoc.Id,
                SourceNodeId = connDoc.SourceNodeId,
                SourcePortId = connDoc.SourcePortId,
                TargetNodeId = connDoc.TargetNodeId,
                TargetPortId = connDoc.TargetPortId
            });
        }

        return new FlowInstance(
            program.Name,
            nodes,
            connections,
            engine,
            flowLogSink ?? NullFlowLogSink.Instance);
    }

    /// <summary>按 ID 查找节点。</summary>
    public NodeModel? FindNode(string nodeId)
        => _nodes.FirstOrDefault(n => n.Id == nodeId);

    /// <summary>执行整个流程（每次调用前清空 Journal，多轮由调用方循环）。</summary>
    public async Task RunAsync(CancellationToken ct, Action<string>? log = null)
    {
        // 进入流程作用域：节点日志按当前流程（+节点+运行序号）路由落盘
        using (FlowLogContext.EnterSink(_flowLogSink))
        using (FlowLogContext.EnterFlow(Name))
            await _runtime.RunAsync(ct, log);
    }
}
