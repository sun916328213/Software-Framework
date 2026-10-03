// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ContractProbeNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

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
    /// <remarks>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnAttached。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </remarks>
    public override void OnAttached(IFlowChartService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        WasAttached = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnDetached。
    /// </remarks>
    public override void OnDetached()
    {
        WasDetached = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ExecutionCount++;
        return Task.CompletedTask;
    }
}
