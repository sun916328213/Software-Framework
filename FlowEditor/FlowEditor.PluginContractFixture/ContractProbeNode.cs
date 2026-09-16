using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Nodes;

namespace FlowEditor.PluginContractFixture;

/// <summary>
/// 独立程序集中的最小节点插件，用于验证 FlowEditor 的公开插件契约。
/// </summary>
[FlowTool(
    "插件契约探针",
    "TestTube",
    ToolGroup = "契约测试",
    Description = "验证独立节点程序集的发现、还原、生命周期和执行兼容性")]
public sealed class ContractProbeNode : NodeModel
{
    /// <summary>初始化契约探针的动态输出端口。</summary>
    public ContractProbeNode()
    {
        AddCustomOutputPort("Result", nameof(Payload));
    }

    /// <summary>可持久化并在执行时写入节点值的测试载荷。</summary>
    public string Payload { get; set; } = "initial";

    /// <summary>节点是否收到画布附加回调。</summary>
    public bool WasAttached { get; private set; }

    /// <summary>节点是否收到画布卸载回调。</summary>
    public bool WasDetached { get; private set; }

    /// <summary>当前插件实例的执行次数。</summary>
    public int ExecutionCount { get; private set; }

    /// <inheritdoc />
    public override void OnAttached(IFlowChartService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        WasAttached = true;
    }

    /// <inheritdoc />
    public override void OnDetached()
    {
        WasDetached = true;
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ExecutionCount++;
        return Task.CompletedTask;
    }
}
