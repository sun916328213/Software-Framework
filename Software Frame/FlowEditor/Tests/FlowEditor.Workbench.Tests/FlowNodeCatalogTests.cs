// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowNodeCatalogTests、VisibleProbe、HiddenProbe、FirstProbe、UnmarkedProbe、ProbeNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Reflection、System.Reflection.Emit、FlowEditor.Controls、FlowEditor.Controls.Core、FlowEditor.Controls.Discovery。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using FlowEditor.Controls;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Core;
using FlowEditor.Core.Models;
using MaterialDesignThemes.Wpf;
using Prism.DryIoc;
using Prism.Ioc;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// FlowNodeCatalogTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("NodeTypeRegistry")]
public sealed class FlowNodeCatalogTests
{
    /// <summary>
    /// 测试相关入口 MultipleConsumersShareOneScanAndImmutableDescriptions；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Discover、Same、TryResolveType、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MultipleConsumersShareOneScanAndImmutableDescriptions()
    {
        var scans = 0;
        var assembly = typeof(VisibleProbe).Assembly;
        var catalog = new FlowNodeCatalog([assembly, assembly], _ =>
        {
            scans++;
            return [typeof(VisibleProbe), typeof(HiddenProbe), typeof(UnmarkedProbe)];
        }, true);
        var discovery = new FlowToolDiscoveryService(catalog);

        Assert.Equal(3, catalog.Discover());
        Assert.Equal(1, discovery.Discover());
        Assert.Equal(3, catalog.Discover());
        Assert.Same(typeof(HiddenProbe), catalog.TryResolveType("s2.hidden-probe", null));
        Assert.Equal(1, scans);
        Assert.Throws<NotSupportedException>(() => ((IList<FlowNodeDescriptor>)catalog.Nodes).Clear());
        Assert.Single(catalog.Nodes, node => node.NodeType == typeof(UnmarkedProbe) && node.DisplayName == null);
    }

    /// <summary>
    /// 测试相关入口 ToolboxProjectionKeepsMetadataVisibilityOrderingAndIconFallback；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Discover、Equal、Select、Single、Search；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToolboxProjectionKeepsMetadataVisibilityOrderingAndIconFallback()
    {
        var catalog = new FlowNodeCatalog([typeof(VisibleProbe).Assembly], _ =>
            [typeof(VisibleProbe), typeof(HiddenProbe), typeof(FirstProbe)], true);
        var discovery = new FlowToolDiscoveryService(catalog);
        discovery.Discover();
        Assert.Equal(new[] { "First probe", "Visible probe" }, discovery.DiscoveredTools.Select(tool => tool.DisplayName));
        var visible = discovery.DiscoveredTools[1];
        Assert.Equal("S2 probe", visible.ToolGroup);
        Assert.Equal("Subgroup", visible.ToolSubGroup);
        Assert.Equal("Search text", visible.Description);
        Assert.Equal(PackIconKind.CubeOutline, visible.IconKind);
        Assert.Single(discovery.Search("search"));
        Assert.DoesNotContain(discovery.DiscoveredTools, tool => tool.ToolType == typeof(HiddenProbe));
    }

    /// <summary>
    /// 测试相关入口 ModuleUsesHostCatalogAndPreservesItAcrossRepeatedRegistration；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Same、Resolve、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ModuleUsesHostCatalogAndPreservesItAcrossRepeatedRegistration()
    {
        var catalog = new FlowNodeCatalog([typeof(HiddenProbe).Assembly], _ => [typeof(HiddenProbe)], true);
        var container = new DryIocContainerExtension();
        container.RegisterInstance<IFlowNodeCatalog>(catalog);
        new FlowEditorModule().RegisterTypes(container);
        new FlowEditorModule().RegisterTypes(container);
        Assert.Same(catalog, container.Resolve<IFlowNodeCatalog>());
        Assert.True(container.IsRegistered(typeof(HiddenProbe)));
        Assert.False(container.IsRegistered(typeof(VisibleProbe)));
        var discovery = container.Resolve<IFlowToolDiscoveryService>();
        Assert.Equal(0, discovery.Discover());
        var node = container.Resolve<INodeFactory>().Create(new NodeDocumentModel
        {
            NodeTypeId = "s2.hidden-probe", TypeName = "Unavailable.OldType, RemovedAssembly", Id = "hidden", Name = "restored"
        });
        Assert.IsType<HiddenProbe>(node);
        Assert.Equal("hidden", node.Id);
    }

    /// <summary>
    /// 测试相关入口 DocumentMappingAndRestorationUseCatalogEvenAfterGlobalRegistryIsCleared；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Discover、Clear、ToNodeDocument、Equal、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DocumentMappingAndRestorationUseCatalogEvenAfterGlobalRegistryIsCleared()
    {
        var catalog = new FlowNodeCatalog([typeof(VisibleProbe).Assembly], _ => [typeof(VisibleProbe)], true);
        catalog.Discover();
        NodeTypeRegistry.Clear();
        var factory = new ReflectionNodeFactory(null, catalog);
        var mapper = new FlowDocumentMapper(factory, catalog);
        var descriptor = mapper.ToNodeDocument(new VisibleProbe { Id = "roundtrip", Name = "unchanged" });
        Assert.Equal("s2.visible-probe", descriptor.NodeTypeId);
        Assert.Equal(2, descriptor.SchemaVersion);
        descriptor.TypeName = "Old.UnknownType, OldAssembly";
        Assert.IsType<VisibleProbe>(mapper.ToNode(descriptor));
    }

    /// <summary>
    /// 测试相关入口 CatalogAcceptsPersistedClrNamesAndFrameworkMigrationAliases；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Same、TryResolveType、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CatalogAcceptsPersistedClrNamesAndFrameworkMigrationAliases()
    {
        var catalog = new FlowNodeCatalog([typeof(LoopNode).Assembly]);
        Assert.Same(typeof(LoopNode), catalog.TryResolveType(null, typeof(LoopNode).AssemblyQualifiedName));
        Assert.Same(typeof(LoopNode), catalog.TryResolveType(null, "FlowEditor.Nodes.FlowControl.LoopNode, FlowEditor"));
        Assert.Same(typeof(LoopNode), catalog.TryResolveType("flow.loop", "Unknown.Type"));
        Assert.Null(catalog.TryResolveType("s2.unknown", "Unknown.Type, Missing"));
    }

    /// <summary>
    /// 测试相关入口 ScopedRestorationDoesNotTakeAnUnknownIdentityFromAnotherCatalog；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Discover、Throws、Create、IsType、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ScopedRestorationDoesNotTakeAnUnknownIdentityFromAnotherCatalog()
    {
        var assembly = typeof(VisibleProbe).Assembly;
        var catalog = new FlowNodeCatalog([assembly], _ => [typeof(VisibleProbe)], true);
        new FlowNodeCatalog([assembly], _ => [typeof(HiddenProbe)], true).Discover();
        var factory = new ReflectionNodeFactory(null, catalog);
        var document = new NodeDocumentModel { NodeTypeId = "s2.hidden-probe", TypeName = "Missing.Type, Missing" };
        Assert.Throws<TypeLoadException>(() => factory.Create(document));
        factory.AllowMissingNodes = true;
        var missing = Assert.IsType<FlowEditor.Controls.Nodes.MissingNode>(factory.Create(document));
        Assert.Equal(document.NodeTypeId, new FlowDocumentMapper(factory, catalog).ToNodeDocument(missing).NodeTypeId);
    }

    /// <summary>
    /// 测试相关入口 RequiredAssemblyTypeLoadFailurePreservesSourceAndOriginalCause；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Discover、Same、Contains、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RequiredAssemblyTypeLoadFailurePreservesSourceAndOriginalCause()
    {
        var cause = new FileNotFoundException("Missing native vendor dependency", "Vendor.SDK");
        var loadFailure = new ReflectionTypeLoadException([typeof(LoopNode)], [cause]);
        var catalog = new FlowNodeCatalog([typeof(LoopNode).Assembly], _ => throw loadFailure, true);
        var failure = Assert.Throws<InvalidOperationException>(() => catalog.Discover());
        Assert.Same(loadFailure, failure.InnerException);
        Assert.Contains("FlowEditor.Controls", failure.Message);
        Assert.Contains("Missing native vendor dependency", failure.Message);
        Assert.Same(loadFailure, Assert.Single(catalog.Diagnostics).Cause);
    }

    /// <summary>
    /// 测试相关入口 AmbientUnrelatedAssemblyFailureIsRecordedWithoutDiscardingBuiltInNodes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Discover、Same、Single、TryResolveType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AmbientUnrelatedAssemblyFailureIsRecordedWithoutDiscardingBuiltInNodes()
    {
        var unrelated = typeof(FlowNodeCatalogTests).Assembly;
        var cause = new BadImageFormatException("Unrelated library types unavailable");
        var catalog = new FlowNodeCatalog([unrelated, typeof(LoopNode).Assembly], assembly =>
            assembly == unrelated ? throw cause : [typeof(LoopNode)], false);
        Assert.Equal(1, catalog.Discover());
        Assert.Same(cause, Assert.Single(catalog.Diagnostics).Cause);
        Assert.Same(typeof(LoopNode), catalog.TryResolveType("flow.loop", null));
    }

    /// <summary>
    /// 测试相关入口 ConflictingStableIdentityFailsBeforePublishingTheCatalog；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 DefineDynamicAssembly、DefineType、DefineDynamicModule、SetCustomAttribute、GetConstructor；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ConflictingStableIdentityFailsBeforePublishingTheCatalog()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("S2CollisionProbe"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Probe").DefineType("CollisionProbe", TypeAttributes.Public, typeof(ProbeNode));
        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(NodeTypeIdAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("Missing attribute constructor"), ["flow.loop"]));
        var duplicate = type.CreateType() ?? throw new InvalidOperationException("Missing collision type");
        var catalog = new FlowNodeCatalog([typeof(LoopNode).Assembly], _ => [typeof(LoopNode), duplicate], true);
        var failure = Assert.Throws<InvalidOperationException>(() => catalog.Discover());
        Assert.Contains("Duplicate node identity 'flow.loop'", failure.Message);
        Assert.Contains("CollisionProbe", failure.Message);
    }

    /// <summary>
    /// VisibleProbe 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ProbeNode。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [FlowTool("Visible probe", "NoSuchIcon", ToolGroup = "S2 probe", ToolSubGroup = "Subgroup", SortIndex = 20, Description = "Search text")]
    [NodeTypeId("s2.visible-probe")]
    public sealed class VisibleProbe : ProbeNode;

    /// <summary>
    /// HiddenProbe 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ProbeNode。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [FlowTool("Hidden probe", "CubeOutline", VisibleInToolbox = false)]
    [NodeTypeId("s2.hidden-probe")]
    public sealed class HiddenProbe : ProbeNode;

    /// <summary>
    /// FirstProbe 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ProbeNode。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [FlowTool("First probe", "CubeOutline", SortIndex = 10)]
    public sealed class FirstProbe : ProbeNode;

    /// <summary>
    /// UnmarkedProbe 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ProbeNode。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class UnmarkedProbe : ProbeNode;

    /// <summary>
    /// ProbeNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class ProbeNode : NodeModel
    {
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
