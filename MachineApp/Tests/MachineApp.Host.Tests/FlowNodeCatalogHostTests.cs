// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowNodeCatalogHostTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Reflection、System.Text.Json、FlowEditor.Controls.Core、FlowEditor.Controls.Discovery、FlowEditor.Controls.Interfaces。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Reflection;
using System.Text.Json;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Core.Models;
using FlowEditor.Core;
using FlowEditor.Core.Nodes;
using FlowNodes.Integration;
using MaterialDesignThemes.Wpf;
using Prism.Ioc;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// FlowNodeCatalogHostTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class FlowNodeCatalogHostTests(ProductionFixture fixture)
{
    /// <summary>
    /// 测试相关入口 ProductionAndWorkbenchUseTheSameExplicitCompositionAndNodeIdentities；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、CreateNodeCatalog、Equal、Order；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProductionAndWorkbenchUseTheSameExplicitCompositionAndNodeIdentities()
    {
        fixture.Invoke(container =>
        {
            var production = container.Resolve<IFlowNodeCatalog>();
            var workbenchComposition = FlowNodesComposition.CreateNodeCatalog();
            Assert.Equal(production.Nodes, workbenchComposition.Nodes);
            Assert.Equal(new[] { "FlowEditor.Controls", "FlowNodes" }, production.Nodes
                .Select(node => node.NodeType.Assembly.GetName().Name).Distinct().Order());
            Assert.Empty(production.Diagnostics);
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Build", "Test-Integration.ps1")))
                directory = directory.Parent;
            var root = directory?.FullName ?? throw new DirectoryNotFoundException("Missing repository");
            foreach (var relative in new[] { "MachineApp/MachineApp/App.xaml.cs", "Software Frame/FlowNodes/FlowNodes.Workbench/App.xaml.cs" })
            {
                var source = File.ReadAllText(Path.Combine(root, relative));
                if (relative.StartsWith("MachineApp/", StringComparison.Ordinal))
                {
                    Assert.Contains("WPFControlsComposition.RegisterServices(containerRegistry)", source);
                    Assert.Contains("FlowNodesComposition.CreateNodeCatalog()", File.ReadAllText(Path.Combine(root,
                        "Software Frame/WPFControls/WPFControls/WPFControlsComposition.cs")));
                }
                else Assert.Contains("FlowNodesComposition.CreateNodeCatalog()", source);
                Assert.DoesNotContain("Assembly.Load(\"FlowNodes\")", source);
            }
            Directory.CreateDirectory(Path.Combine(root, "TestResults"));
            File.WriteAllText(Path.Combine(root, "TestResults", "architecture-s2-node-catalog.json"),
                JsonSerializer.Serialize(production.Nodes.Select(node => new
                {
                    typeName = node.NodeType.AssemblyQualifiedName,
                    assembly = node.NodeType.Assembly.GetName().Name,
                    nodeTypeId = node.NodeTypeId,
                    displayName = node.DisplayName,
                    toolGroup = node.ToolGroup,
                    toolSubGroup = node.ToolSubGroup,
                    iconResource = node.IconResource,
                    sortIndex = node.SortIndex,
                    description = node.Description,
                    visibleInToolbox = node.VisibleInToolbox
                }), new JsonSerializerOptions { WriteIndented = true }));
        });
    }

    /// <summary>
    /// 测试相关入口 ProductionToolboxKeepsTheOriginalMetadataFilteringOrderingAndIcons；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Discover、ToArray、ThenBy；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    [Fact]
    public void ProductionToolboxKeepsTheOriginalMetadataFilteringOrderingAndIcons()
    {
        fixture.Invoke(container =>
        {
            var catalog = container.Resolve<IFlowNodeCatalog>();
            var discovery = container.Resolve<IFlowToolDiscoveryService>();
            discovery.Discover();
            var expected = catalog.Nodes.Select(node => node.NodeType.Assembly).Distinct()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(NodeModel)))
                .Select(type => (Type: type, Tool: type.GetCustomAttribute<FlowToolAttribute>()))
                .Where(entry => entry.Tool?.VisibleInToolbox == true)
                .OrderBy(entry => entry.Tool!.SortIndex).ThenBy(entry => entry.Tool!.ToolName, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expected.Select(entry => entry.Type), discovery.DiscoveredTools.Select(tool => tool.ToolType));
            Assert.True(expected.Length > 30);
            for (var index = 0; index < expected.Length; index++)
            {
                var original = expected[index].Tool ?? throw new InvalidOperationException("Missing tool metadata");
                var actual = discovery.DiscoveredTools[index];
                Assert.Equal(original.ToolName, actual.DisplayName);
                Assert.Equal(original.ToolGroup, actual.ToolGroup);
                Assert.Equal(original.ToolSubGroup, actual.ToolSubGroup);
                Assert.Equal(original.SortIndex, actual.SortIndex);
                Assert.Equal(original.Description, actual.Description);
                var icon = PackIconKind.CubeOutline;
                try { if (!string.IsNullOrEmpty(original.IconResource)) icon = (PackIconKind)Enum.Parse(typeof(PackIconKind), original.IconResource); }
                catch (ArgumentException) { }
                Assert.Equal(icon, actual.IconKind);
                Assert.True(((IContainerRegistry)container).IsRegistered(actual.ToolType));
            }
        });
    }

    /// <summary>
    /// 测试相关入口 EveryVisibleProductionToolCreatesSavesAndRestoresThroughTheSharedCatalog；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Refresh、Combine、GetTempPath；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    [Fact]
    public void EveryVisibleProductionToolCreatesSavesAndRestoresThroughTheSharedCatalog()
    {
        fixture.Invoke(container =>
        {
            var toolbox = container.Resolve<ToolboxService>();
            toolbox.Refresh();
            var mapper = container.Resolve<FlowDocumentMapper>();
            var catalog = container.Resolve<IFlowNodeCatalog>();
            var nodes = new List<NodeModel>();
            var file = Path.Combine(Path.GetTempPath(), $"s2-catalog-{Guid.NewGuid():N}.flow");
            try
            {
                var failures = new List<string>();
                foreach (var item in toolbox.Items)
                {
                    try { nodes.Add(item.CreateNode()); }
                    catch (Exception error) { failures.Add($"{item.DisplayName}: {error.Message}"); }
                }
                Assert.Empty(failures);
                var originals = nodes.ToArray();
                var descriptors = originals.Select(mapper.ToNodeDocument).ToList();
                new JsonFlowDocumentStore().Save(file, new FlowChartDocumentModel { Name = "S2 compatibility", Nodes = descriptors });
                var loaded = new JsonFlowDocumentStore().Load<FlowChartDocumentModel>(file);
                Assert.Equal(toolbox.Items.Count, loaded.Nodes.Count);
                foreach (var (original, descriptor) in originals.Zip(loaded.Nodes))
                {
                    var restored = mapper.ToNode(descriptor);
                    nodes.Add(restored);
                    Assert.Equal(original.GetType(), restored.GetType());
                    Assert.Equal(original.Id, restored.Id);
                    Assert.Equal(original.Name, restored.Name);
                    Assert.Equal(original.CustomInputPorts.Select(port => port.Id), restored.CustomInputPorts.Select(port => port.Id));
                    Assert.Equal(original.CustomOutputPorts.Select(port => port.Id), restored.CustomOutputPorts.Select(port => port.Id));
                    Assert.Same(original.GetType(), catalog.TryResolveType(descriptor.NodeTypeId, descriptor.TypeName));
                }
            }
            finally
            {
                File.Delete(file);
                foreach (var node in nodes)
                {
                    if (node.GetType().GetProperty("visionTool")?.GetValue(node) is IDisposable tool) tool.Dispose();
                    if (node.GetType().GetProperty("Tool")?.GetValue(node) is IDisposable aiTool) aiTool.Dispose();
                }
            }
        });
    }

    /// <summary>
    /// 测试相关入口 CameraAndCommunicationNodeDependenciesResolveWithoutOpeningDevices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、IsType、Empty、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CameraAndCommunicationNodeDependenciesResolveWithoutOpeningDevices()
    {
        fixture.Invoke(container =>
        {
            var session = container.Resolve<MachineApp.Infra.Device.ProductionDeviceSession>();
            var provider = container.Resolve<FlowNodes.Device.ICameraProvider>();
            Assert.IsType<MachineApp.Infra.Device.ProductionFlowCameraProvider>(provider);
            Assert.Empty(provider.Names);
            Assert.Throws<KeyNotFoundException>(() => provider.Get("not configured"));
            Assert.Empty(session.Cameras);
            var manager = container.Resolve<Communication.Runtime.Core.CommunicationManager>();
            Assert.Same(manager, container.Resolve<Communication.Core.ICommunicationManager>());
            Assert.Empty(manager.Devices);
        });
    }
}
