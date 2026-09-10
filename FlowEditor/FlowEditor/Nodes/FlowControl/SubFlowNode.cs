using FlowEditor.Core;
using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 子流程节点（P5）：把一个 .flow 文档作为可复用子程序执行。
/// - InnerFlow：子流程文档（随节点属性自动持久化）
/// - 端口映射约定：自定义端口按「内部节点名.端口名」命名，
///   输入端口在执行前写入子流程对应节点的同名输入端口；
///   输出端口在执行后读取子流程对应节点的同名输出端口值。
/// - 递归防护：嵌套深度受 FlowRecursionGuard.MaxDepth 限制，循环引用会抛出异常。
/// </summary>
[FlowTool("子流程", "SourceMerge", ToolGroup = "流程控制", SortIndex = 6,
    Description = "旧版内嵌子流程节点；保留用于加载历史流程",
    VisibleInToolbox = false)]
public class SubFlowNode : NodeModel
{
    /// <summary>
    /// 实例创建委托（可注入点）：默认使用反射工厂 + 共享引擎。
    /// 单测/宿主可替换以拦截实例创建过程。
    /// </summary>
    public static Func<FlowProgram, FlowInstance>? InstanceFactory { get; set; }

    private FlowChartDocumentModel? _innerFlow;
    /// <summary>内嵌子流程文档。</summary>
    public FlowChartDocumentModel? InnerFlow
    {
        get => _innerFlow;
        set => SetProperty(ref _innerFlow, value);
    }

    public SubFlowNode()
    {
        Name = "子流程";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        if (InnerFlow == null)
            return; // 未配置子流程：直通

        using var _ = FlowRecursionGuard.Enter();

        var program = FlowProgram.FromDocument(InnerFlow);
        var instance = CreateInstance(program);

        // 输入映射：「内部节点名.端口名」→ 子流程输入端口
        foreach (var outerPort in CustomInputPorts)
        {
            if (!TrySplitPortName(outerPort.PropertyName ?? outerPort.Name, out var nodeName, out var portName))
                continue;
            var innerNode = instance.Nodes.FirstOrDefault(n => n.Name == nodeName);
            var innerPort = innerNode?.InputPorts.FirstOrDefault(p => p.Name == portName);
            if (innerPort != null)
                innerPort.Value = outerPort.Value;
        }

        await instance.RunAsync(ct, msg => Console.WriteLine(msg));

        // 输出映射：子流程输出端口 → 「内部节点名.端口名」自定义输出端口
        foreach (var outerPort in CustomOutputPorts)
        {
            if (!TrySplitPortName(outerPort.PropertyName ?? outerPort.Name, out var nodeName, out var portName))
                continue;
            var innerNode = instance.Nodes.FirstOrDefault(n => n.Name == nodeName);
            var innerPort = innerNode?.OutputPorts.FirstOrDefault(p => p.Name == portName);
            if (innerPort != null)
                outerPort.Value = innerPort.Value;
        }
    }

    private static FlowInstance CreateInstance(FlowProgram program)
        => InstanceFactory != null
            ? InstanceFactory(program)
            : FlowInstance.Create(program, ReflectionNodeFactory.Instance);

    /// <summary>拆分「节点名.端口名」（取第一个 '.' 分隔）。</summary>
    private static bool TrySplitPortName(string composite, out string nodeName, out string portName)
    {
        var dot = composite.IndexOf('.');
        if (dot <= 0 || dot == composite.Length - 1)
        {
            nodeName = portName = string.Empty;
            return false;
        }
        nodeName = composite[..dot];
        portName = composite[(dot + 1)..];
        return true;
    }
}
