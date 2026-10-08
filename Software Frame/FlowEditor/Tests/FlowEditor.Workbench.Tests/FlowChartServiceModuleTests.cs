// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowChartServiceModuleTests、TrackingNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Interfaces、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Nodes.FlowControl。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services.Storage;
using WPFCustomControls.PropertyGrid.Core.Models;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>通过稳定门面验证 P4 文档与画布模块的组合行为。</summary>
public sealed class FlowChartServiceModuleTests
{
    /// <summary>节点卸载后释放流程引用和事件订阅，重新附加后只接收一次变化。</summary>
    [Fact]
    public void RemoveAndReAddNode_ControlsFlowReferenceAndConnectionSubscription()
    {
        var service = TestFlowEditorFactory.CreateService();
        var node = new TrackingNode { Name = "生命周期节点" };
        var viewModel = service.Canvas.AddNode(node);
        var bindingProvider = (IDataBindingProvider)node;
        var bindingChangeCount = 0;
        bindingProvider.DataBindingsChanged += () => bindingChangeCount++;

        service.Connections.Add(new ConnectionModel());
        service.Canvas.RemoveNode(viewModel);
        service.Connections.Add(new ConnectionModel());

        Assert.Null(node.Service);
        Assert.Equal(1, bindingChangeCount);
        Assert.Equal(1, node.DetachedCount);

        service.Canvas.AddNode(node);
        service.Connections.Add(new ConnectionModel());

        Assert.Same(service, node.Service);
        Assert.Equal(2, bindingChangeCount);
        Assert.Equal(2, node.AttachedCount);
    }

    /// <summary>保存和加载通过门面恢复节点、画布状态及节点附加关系。</summary>
    [Fact]
    public void SaveAndLoad_RestoresDocumentAndAttachesRebuiltNodes()
    {
        var store = new InMemoryFlowDocumentStore();
        var service = TestFlowEditorFactory.CreateService(store);
        service.Model.TabTitle = "持久化流程";
        service.Model.ZoomLevel = 0.75;
        service.Canvas.AddNode(new StartNode { Name = "入口", X = 120, Y = 240 });

        Assert.True(service.Documents.Save("flow-session"));
        service.Canvas.ClearAll();
        Assert.True(service.Documents.Load("flow-session"));

        var restored = Assert.Single(service.Nodes);
        Assert.IsType<StartNode>(restored.Model);
        Assert.Same(service, restored.Model.Service);
        Assert.Equal("入口", restored.Name);
        Assert.Equal(120, restored.X);
        Assert.Equal(240, restored.Y);
        Assert.Equal("持久化流程", service.Model.TabTitle);
        Assert.Equal(0.75, service.Model.ZoomLevel);
        Assert.False(service.Model.IsModified);
    }

    /// <summary>节点上下文只公开领域节点模型，并保持画布顺序及按 ID 查询行为。</summary>
    [Fact]
    public void NodeContext_ExposesModelsWithoutViewModels()
    {
        var service = TestFlowEditorFactory.CreateService();
        var first = new TrackingNode { Name = "第一个节点" };
        var second = new TrackingNode { Name = "第二个节点" };
        service.Canvas.AddNode(first);
        service.Canvas.AddNode(second);
        IFlowChartService context = service;

        Assert.Equal(new NodeModel[] { first, second }, context.Nodes);
        Assert.Same(second, context.FindNode(second.Id));
    }

    /// <summary>验证单节点删除与批量清理会向派生节点提供不同的卸载上下文。</summary>
    [Fact]
    public void NodeDetachContext_DistinguishesRemovalFromBatchClear()
    {
        var service = TestFlowEditorFactory.CreateService();
        var removed = new TrackingNode { Name = "Removed" };
        var cleared = new TrackingNode { Name = "Cleared" };
        var removedViewModel = service.Canvas.AddNode(removed);
        service.Canvas.AddNode(cleared);

        service.Canvas.RemoveNode(removedViewModel);
        service.Canvas.ClearNodes();

        Assert.False(removed.WasBatchDetaching);
        Assert.True(cleared.WasBatchDetaching);
    }

    /// <summary>记录画布附加和卸载回调次数的测试节点。</summary>
    private sealed class TrackingNode : NodeModel
    {
        /// <summary>附加次数。</summary>
        public int AttachedCount { get; private set; }

        /// <summary>卸载次数。</summary>
        public int DetachedCount { get; private set; }

        /// <summary>最近一次卸载是否属于批量事务。</summary>
        public bool WasBatchDetaching { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 OnAttached；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public override void OnAttached(IFlowChartService service) => AttachedCount++;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 OnDetached；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public override void OnDetached()
        {
            DetachedCount++;
            WasBatchDetaching = IsBatchDetaching;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
