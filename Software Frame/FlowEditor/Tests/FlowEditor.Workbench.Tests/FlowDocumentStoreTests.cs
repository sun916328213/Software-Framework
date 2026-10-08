// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowDocumentStoreTests、RecordingFlowTabFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using System.IO;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证流程文档存储 seam 与标签页 factory 的无头适配能力。</summary>
public sealed class FlowDocumentStoreTests
{
    /// <summary>验证内存 adapter 保存的是独立 JSON 副本，而不是原对象引用。</summary>
    [Fact]
    public void InMemoryStore_SaveAndLoad_ReturnsIndependentDocument()
    {
        var store = new InMemoryFlowDocumentStore();
        var source = new FlowChartDocumentModel { Name = "before", ZoomLevel = 0.8 };

        store.Save("flow-1", source);
        source.Name = "after";
        var loaded = store.Load<FlowChartDocumentModel>("flow-1");

        Assert.Equal("before", loaded.Name);
        Assert.Equal(0.8, loaded.ZoomLevel);
        Assert.NotSame(source, loaded);
    }

    /// <summary>验证同一个存储 seam 可以保存和加载多流程根文档。</summary>
    [Fact]
    public void InMemoryStore_RoundTripsContainerDocument()
    {
        var store = new InMemoryFlowDocumentStore();
        var source = new FlowChartContainerDocumentModel
        {
            Flows = [new FlowChartDocumentModel { Name = "child" }]
        };

        store.Save("group-1", source);
        var loaded = store.Load<FlowChartContainerDocumentModel>("group-1");

        Assert.Equal("1.0", loaded.Version);
        Assert.Equal("child", Assert.Single(loaded.Flows).Name);
    }

    /// <summary>验证 JSON 文件 adapter 可创建目录并按共享格式往返单流程文档。</summary>
    [Fact]
    public void JsonStore_SaveAndLoad_RoundTripsFlowDocument()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"flow-editor-store-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "sample.flow");
        try
        {
            var store = new JsonFlowDocumentStore();
            store.Save(path, new FlowChartDocumentModel { Name = "file-flow", ZoomLevel = 1.2 });

            var loaded = store.Load<FlowChartDocumentModel>(path);

            Assert.Equal("file-flow", loaded.Name);
            Assert.Equal(1.2, loaded.ZoomLevel);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>验证多流程文档模块通过 factory 创建标签页并应用文档内容。</summary>
    [Fact]
    public void CreateTabFromFlow_UsesInjectedTabFactory()
    {
        var store = new InMemoryFlowDocumentStore();
        var tab = TestFlowEditorFactory.CreateViewModel(
            TestFlowEditorFactory.CreateService(documentStore: store));
        var factory = new RecordingFlowTabFactory(tab);
        var module = new FlowChartContainerDocumentService(
            new FlowDocumentMapper(), store, factory);

        var actual = module.CreateTabFromFlow(new FlowChartDocumentModel
        {
            Name = "loaded-tab",
            ZoomLevel = 0.6
        });

        Assert.Same(tab, actual);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal("loaded-tab", actual.Model.TabTitle);
        Assert.Equal(0.6, actual.Model.ZoomLevel);
        Assert.Equal("已加载", actual.Model.StatusText);
    }

    /// <summary>返回预设标签页并记录调用次数的测试 factory。</summary>
    private sealed class RecordingFlowTabFactory : IFlowTabFactory
    {
        private readonly FlowChartViewModel _tab;

        /// <summary>创建记录型标签页 factory。</summary>
        /// <param name="tab">每次创建时返回的标签页。</param>
        public RecordingFlowTabFactory(FlowChartViewModel tab)
        {
            _tab = tab;
        }

        /// <summary>创建调用次数。</summary>
        public int CreateCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public FlowChartViewModel Create()
        {
            CreateCount++;
            return _tab;
        }
    }
}
