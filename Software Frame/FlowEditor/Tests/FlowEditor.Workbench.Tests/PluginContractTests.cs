// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PluginContractTests、ContractFlowContext。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、System.IO、System.Reflection、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Xunit;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls;
using Prism.DryIoc;
using Prism.Ioc;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// 从独立编译产物验证当前第三方节点插件的公开契约。
/// 测试代码不引用夹具节点类型，防止退化为同程序集内的普通单元测试。
/// </summary>
public sealed class PluginContractTests
{
    private const string PluginAssemblyName = "FlowEditor.PluginContractFixture";
    private const string PluginTypeName =
        "FlowEditor.PluginContractFixture.ContractProbeNode, FlowEditor.PluginContractFixture";

    /// <summary>独立插件显式加入后，容器、工具箱和文档还原使用同一目录。</summary>
    [Fact]
    public void ExplicitPluginCatalogFeedsRegistrationDiscoveryAndRestoration()
    {
        var pluginType = LoadPluginType();
        var catalog = new FlowNodeCatalog([pluginType.Assembly]);
        var container = new DryIocContainerExtension();
        container.RegisterInstance<IFlowNodeCatalog>(catalog);
        new FlowEditorModule().RegisterTypes(container);
        Assert.True(container.IsRegistered(pluginType));
        var discovery = container.Resolve<IFlowToolDiscoveryService>();
        Assert.Equal(1, discovery.Discover());
        Assert.Equal(pluginType, Assert.Single(discovery.DiscoveredTools).ToolType);
        var mapper = container.Resolve<FlowDocumentMapper>();
        var original = container.Resolve<INodeFactory>().Create(pluginType);
        WriteProperty(original, "Payload", "catalog roundtrip");
        var restored = mapper.ToNode(mapper.ToNodeDocument(original));
        Assert.Equal(pluginType, restored.GetType());
        Assert.Equal("catalog roundtrip", ReadProperty<string>(restored, "Payload"));
    }

    /// <summary>验证独立插件 DLL 可被加载并由工具发现模块识别。</summary>
    [Fact]
    public void PluginArtifact_IsLoadedAndDiscovered()
    {
        var pluginType = LoadPluginType();
        var discovery = new FlowToolDiscoveryService();

        discovery.Discover();

        var metadata = Assert.Single(discovery.DiscoveredTools, item => item.ToolType == pluginType);
        Assert.Equal("插件契约探针", metadata.DisplayName);
        Assert.Equal("契约测试", metadata.ToolGroup);
    }

    /// <summary>验证反射 factory 能跨程序集创建插件节点。</summary>
    [Fact]
    public void ReflectionFactory_CreatesPluginNodeFromIndependentAssembly()
    {
        var pluginType = LoadPluginType();

        var node = new ReflectionNodeFactory().Create(pluginType);

        Assert.Equal(pluginType, node.GetType());
        Assert.Equal("插件契约探针", node.Name);
        Assert.Single(node.CustomOutputPorts, port => port.Name == "Result");
    }

    /// <summary>验证插件节点附加/卸载回调和旧式文档类型名还原保持兼容。</summary>
    [Fact]
    public void PluginNode_LifecycleAndDocumentRestorationRemainCompatible()
    {
        var pluginType = LoadPluginType();
        var factory = new ReflectionNodeFactory();
        var node = factory.Create(pluginType);
        var context = new ContractFlowContext(node);

        node.OnAttached(context);
        node.OnDetached();

        Assert.True(ReadProperty<bool>(node, "WasAttached"));
        Assert.True(ReadProperty<bool>(node, "WasDetached"));

        var document = new NodeDocumentModel
        {
            Id = "plugin-node",
            Name = "历史插件节点",
            TypeName = PluginTypeName,
            Properties = new Dictionary<string, object?>
            {
                ["Payload"] = "restored"
            }
        };

        var restored = new FlowDocumentMapper(factory).ToNode(document);

        Assert.Equal(pluginType, restored.GetType());
        Assert.Equal("plugin-node", restored.Id);
        Assert.Equal("历史插件节点", restored.Name);
        Assert.Equal("restored", ReadProperty<string>(restored, "Payload"));
    }

    /// <summary>验证独立插件节点无需启动 WPF 窗口即可完成一次实际执行。</summary>
    [Fact]
    public async Task PluginNode_ExecutesWithoutPresentationHost()
    {
        var node = new ReflectionNodeFactory().Create(LoadPluginType());
        WriteProperty(node, "Payload", "executed");

        await node.ExecuteAsync(CancellationToken.None);
        node.SyncOutputValues();

        Assert.Equal(1, ReadProperty<int>(node, "ExecutionCount"));
        Assert.Equal("executed", Assert.Single(node.CustomOutputPorts).Value);
    }

    /// <summary>加载当前构建配置生成的独立插件 DLL，并返回契约探针类型。</summary>
    private static Type LoadPluginType()
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
        var configuration = outputDirectory.Parent?.Name
                            ?? throw new InvalidOperationException("无法确定测试构建配置。");
        var solutionDirectory = TestModulePaths.ModuleDirectory;
        var assemblyPath = Path.Combine(
            solutionDirectory,
            PluginAssemblyName,
            "bin",
            configuration,
            "net9.0-windows7.0",
            $"{PluginAssemblyName}.dll");

        Assert.True(File.Exists(assemblyPath), $"插件契约产物不存在: {assemblyPath}");
        var assembly = Assembly.LoadFrom(assemblyPath);
        return assembly.GetType(PluginTypeName.Split(',')[0], throwOnError: true)!;
    }

    /// <summary>通过反射读取插件属性，保证测试项目不产生对夹具节点类型的编译引用。</summary>
    private static T ReadProperty<T>(NodeModel node, string propertyName)
        => (T)(node.GetType().GetProperty(propertyName)?.GetValue(node)
               ?? throw new InvalidOperationException($"插件属性不存在或为空: {propertyName}"));

    /// <summary>通过反射写入插件属性，保证测试项目不产生对夹具节点类型的编译引用。</summary>
    private static void WriteProperty(NodeModel node, string propertyName, object value)
    {
        var property = node.GetType().GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"插件属性不存在: {propertyName}");
        property.SetValue(node, value);
    }

    /// <summary>插件生命周期契约所需的最小画布上下文。</summary>
    private sealed class ContractFlowContext : IFlowChartService
    {
        private readonly NodeModel _node;

        /// <summary>创建只包含契约探针的画布上下文。</summary>
        public ContractFlowContext(NodeModel node)
        {
            _node = node;
        }

        /// <inheritdoc />
        public FlowChartModel Model { get; } = new();

        /// <inheritdoc />
        public IEnumerable<NodeModel> Nodes => [_node];

        /// <inheritdoc />
        public ObservableCollection<ConnectionModel> Connections { get; } = [];

        /// <inheritdoc />
        public INodeEditorLauncher NodeEditorLauncher => throw new NotSupportedException();

        /// <inheritdoc />
        public INodeNotificationSink NodeNotifications => throw new NotSupportedException();

        /// <inheritdoc />
        /// <remarks>
        /// 验证 FindNode 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </remarks>
        public NodeModel? FindNode(string nodeId) => _node.Id == nodeId ? _node : null;

        /// <inheritdoc />
        /// <remarks>
        /// 验证 RebuildNodeFromDocument 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
        /// </remarks>
        public NodeModel RebuildNodeFromDocument(NodeDocumentModel doc) =>
            new ReflectionNodeFactory().Create(doc);
    }
}
