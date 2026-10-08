// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NamespaceMigrationHostTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Reflection、System.Text.Json、System.Windows、System.Windows.Media、System.Windows.Media.Imaging。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Core;
using FlowEditor.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Prism.Ioc;
using Prism.Regions;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// NamespaceMigrationHostTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class NamespaceMigrationHostTests(ProductionFixture fixture)
{
    /// <summary>
    /// 测试相关入口 AllDeclaredPublicApiNamesPreserveBaselineAndExplicitControlsAdditions；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Read、EnumerateObject、Select、EnumerateArray、GetString；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AllDeclaredPublicApiNamesPreserveBaselineAndExplicitControlsAdditions()
    {
        var baseline = Read("Architecture-S4-public-types.json");
        foreach (var entry in baseline.EnumerateObject())
        {
            if (entry.Name is "WPFControl" or "WPFControl.Controls")
            {
                foreach (var name in entry.Value.EnumerateArray().Select(value => value.GetString()!))
                {
                    if (name.StartsWith("XamlGeneratedNamespace.") || name.EndsWith("Module")) continue;
                    var restored = FrameworkTypeResolver.ResolvePersistedType($"{name}, {entry.Name}");
                    Assert.True(restored != null, name + ", " + entry.Name);
                    Assert.Contains(restored.Assembly.GetName().Name, new[] { "WPFControls", "MachineApp" });
                }
                continue;
            }
            var assemblies = entry.Name == "Extension" ? new[] { "Extension", "Extension.Core" } : new[] { entry.Name };
            var actual = assemblies.SelectMany(name => Assembly.Load(name).GetExportedTypes()).Select(type => type.FullName!).Order(StringComparer.Ordinal).ToArray();
            const string helper = "XamlGeneratedNamespace.GeneratedInternalTypeHelper";
            string[] additions = entry.Name switch
            {
                "FlowEditor.Controls" => ["FlowEditor.Controls.FlowEditorModuleOptions"],
                "MotionControl.Controls" => ["MotionControl.Controls.MotionControlsModule"],
                "Vision.Controls" => ["Vision.Controls.VisionControlsModule"],
                _ => []
            };
            Assert.Equal(entry.Value.EnumerateArray().Select(value => value.GetString()).Where(name => name != helper)
                .Concat(additions).Order(StringComparer.Ordinal), actual.Where(name => name != helper));
        }
        }

    /// <summary>
    /// 测试相关入口 ExactLegacyNamesResolveThroughEverySharedResolverIncludingEarlierAliases；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 EnumerateArray、GetProperty、Read、GetString、Load；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ExactLegacyNamesResolveThroughEverySharedResolverIncludingEarlierAliases()
    {
        foreach (var rename in Read("Architecture-S4-migrations.json").GetProperty("typeRenames").EnumerateArray())
        {
            var originalAssembly = rename.GetProperty("assembly").GetString()!;
            var assembly = Assembly.Load(originalAssembly is "WPFControl" or "WPFControl.Controls" ? "WPFControls" : originalAssembly);
            var oldName = rename.GetProperty("oldType").GetString()!;
            var newName = rename.GetProperty("newType").GetString()!.Replace("WPFControl.", "WPFControls.");
            var generic = newName.EndsWith(".DataQueryPresentation", StringComparison.Ordinal) ? "`2" : "";
            var current = assembly.GetType(newName + generic, throwOnError: true)!;
            Assert.False(current.IsVisible);
            var persisted = oldName + generic + ", " + originalAssembly;
            Assert.Same(current, FrameworkTypeResolver.ResolvePersistedType(persisted));
            Assert.Same(current, Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(persisted));
            Assert.Same(current, Vision.Core.Tools.FrameworkTypeResolver.ResolvePersistedType(persisted));
        }
        var node = FrameworkTypeResolver.ResolvePersistedType("FlowNodes.Communication.CommunicationNew.NetReadNode, FlowNodes")!;
        Assert.Equal(node.MakeArrayType(), FrameworkTypeResolver.ResolvePersistedType("FlowNodes.Communication.CommunicationNew.NetReadNode[], FlowNodes"));
        Assert.Equal(typeof(List<>).MakeGenericType(node), FrameworkTypeResolver.ResolvePersistedType(
            "System.Collections.Generic.List`1[[FlowNodes.Communication.CommunicationNew.NetReadNode, FlowNodes]], System.Private.CoreLib"));
        Assert.Equal("FlowNodes.Communication.NetReceiveInbox+ReceivedPacket", FrameworkTypeResolver.ResolvePersistedType(
            "FlowNodes.Communication.CommunicationNew.NetReceiveInbox+ReceivedPacket, FlowNodes")!.FullName);
        Assert.Equal("WPFControls.ProgramControl.Services.Printer.PrintService", FrameworkTypeResolver.ResolvePersistedType(
            "WPFControl.CustomControl.Services.PrintService, WPFControl")!.FullName);
        Assert.Null(FrameworkTypeResolver.ResolvePersistedType("FlowNodes.Communication.CommunicationNew.UnknownNode, FlowNodes"));
        var aliasFile = Path.Combine(Root(), "Software Frame/Extension/Extension.Core/Core/Reflection/FrameworkTypeAliases.json");
        using var aliases = JsonDocument.Parse(File.ReadAllText(aliasFile));
        foreach (var alias in aliases.RootElement.EnumerateObject().Where(entry => entry.Name.StartsWith("WPFControl.", StringComparison.Ordinal)))
        {
            var targetName = alias.Value.GetProperty("Type").GetString()!;
            var targetAssembly = Assembly.Load(alias.Value.GetProperty("Assembly").GetString()!);
            var target = targetAssembly.GetType(targetName) ?? targetAssembly.GetTypes().SingleOrDefault(type =>
                type.DeclaringType is null && type.FullName!.StartsWith(targetName + "`", StringComparison.Ordinal));
            Assert.True(target != null, alias.Name + " -> " + targetName);
            var oldName = alias.Name;
            if (target!.IsGenericTypeDefinition && !oldName.Contains('`')) oldName += "`" + target.GetGenericArguments().Length;
            Assert.Same(target, FrameworkTypeResolver.ResolvePersistedType(oldName + ", WPFControl"));
        }
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
            assembly.GetName().Name is "WPFControl" or "WPFControl.Controls" or "WPFControl.Application");
    }

    /// <summary>
    /// 测试相关入口 OriginalCatalogMetadataAndOldFlowDocumentsSurviveMigration；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、ToArray、EnumerateArray、Read；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OriginalCatalogMetadataAndOldFlowDocumentsSurviveMigration()
    {
        fixture.Invoke(container =>
        {
            var catalog = container.Resolve<IFlowNodeCatalog>();
            var mapper = container.Resolve<FlowDocumentMapper>();
            var original = Read("Architecture-S2-nodes.json").EnumerateArray().ToArray();
            Assert.Equal(original.Length, catalog.Nodes.Count);
            var documents = new List<NodeDocumentModel>();
            foreach (var row in original)
            {
                var oldName = row.GetProperty("typeName").GetString()!;
                var current = catalog.TryResolveType(row.GetProperty("nodeTypeId").GetString(), oldName);
                var descriptor = Assert.Single(catalog.Nodes, node => node.NodeType == current);
                Assert.Equal(row.GetProperty("displayName").GetString(), descriptor.DisplayName);
                Assert.Equal(row.GetProperty("toolGroup").GetString(), descriptor.ToolGroup);
                Assert.Equal(row.GetProperty("toolSubGroup").GetString(), descriptor.ToolSubGroup);
                Assert.Equal(row.GetProperty("iconResource").GetString(), descriptor.IconResource);
                Assert.Equal(row.GetProperty("sortIndex").GetInt32(), descriptor.SortIndex);
                Assert.Equal(row.GetProperty("description").GetString(), descriptor.Description);
                Assert.Equal(row.GetProperty("visibleInToolbox").GetBoolean(), descriptor.VisibleInToolbox);
                if (!oldName.Contains("CommunicationNew.") && !oldName.Contains("FixtrueNode")) continue;
                documents.Add(new() { Id = Guid.NewGuid().ToString(), TypeName = oldName, Name = descriptor.DisplayName });
            }
            Assert.Equal(18, documents.Count);
            var path = Path.Combine(Path.GetTempPath(), $"s4-legacy-{Guid.NewGuid():N}.flow");
            try
            {
                var store = new JsonFlowDocumentStore();
                store.Save(path, new FlowChartDocumentModel { Name = "S4 legacy identities", Nodes = documents });
                foreach (var document in store.Load<FlowChartDocumentModel>(path).Nodes)
                {
                    var restored = mapper.ToNode(document);
                    try
                    {
                        Assert.IsNotType<MissingNode>(restored);
                        Assert.Same(catalog.TryResolveType(null, document.TypeName), restored.GetType());
                        Assert.Equal(document.Id, restored.Id);
                        Assert.Equal(document.Name, restored.Name);
                        var saved = mapper.ToNodeDocument(restored);
                        Assert.DoesNotContain("CommunicationNew.", saved.TypeName);
                        Assert.DoesNotContain("FixtrueNode", saved.TypeName);
                        Assert.Equal(restored.GetType(), mapper.ToNode(saved).GetType());
                    }
                    finally
                    {
                        if (restored.GetType().GetProperty("visionTool")?.GetValue(restored) is IDisposable tool) tool.Dispose();
                    }
                }
            }
            finally { File.Delete(path); }
        });
    }

    /// <summary>
    /// 测试相关入口 AllTenMigratedViewModelsNavigateAndRenderUnderOriginalPublicViewNames；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、RegisterTypes、Resolve、Add、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AllTenMigratedViewModelsNavigateAndRenderUnderOriginalPublicViewNames()
    {
        fixture.Invoke(container =>
        {
            new WPFControls.WPFControlsModule().RegisterTypes((IContainerRegistry)container);
            var manager = container.Resolve<IRegionManager>();
            var region = new Region();
            manager.Regions.Add("S4NamespaceAcceptance", region);
            foreach (var rename in Read("Architecture-S4-migrations.json").GetProperty("typeRenames").EnumerateArray()
                         .Where(value => value.GetProperty("oldType").GetString()!.StartsWith("WPFControls.ProgramControl.ViewModels.")))
            {
                var modelName = rename.GetProperty("newType").GetString()!;
                var viewName = modelName.Split('.').Last().Replace("ViewModel", "View", StringComparison.Ordinal);
                NavigationResult? result = null;
                manager.RequestNavigate("S4NamespaceAcceptance", viewName, value => result = value);
                Assert.True(result?.Result, result?.Error?.ToString());
                var view = Assert.IsAssignableFrom<FrameworkElement>(Assert.Single(region.ActiveViews, value => value.GetType().Name == viewName));
                Assert.Equal("WPFControls.ProgramControl.Views." + viewName, view.GetType().FullName);
                Assert.Equal(modelName, view.DataContext?.GetType().FullName);
                if (viewName == "PrintView") Assert.NotSame(view, container.Resolve<object>(viewName));
                else Assert.Same(view, container.Resolve<object>(viewName));
                view.Measure(new Size(1000, 700)); view.Arrange(new Rect(0, 0, 1000, 700)); view.UpdateLayout();
                new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32).Render(view);
            }
        });
    }

    /// <summary>
    /// 测试相关入口 ExternalPluginCompilesAndExecutesAgainstTheNewUiLibraryNamespace；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Select、Distinct、Concat、Split、GetData；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ExternalPluginCompilesAndExecutesAgainstTheNewUiLibraryNamespace()
    {
        // Literal old API names deliberately do not come from the migration map.
        const string source = """
            using FlowNodes.Communication.CommunicationNew;
            using FlowEditor.Controls.Nodes;
            using WPFControls.Method;
            using WPFControls.ValueConvert;
            using WPFControls.Controls.Resources;
            public static class LegacyPlugin
            {
                public static string Identify() => string.Join("|", (int)ENetDataFormat.Hex,
                    typeof(NodeModel).FullName, typeof(QuerySave).FullName,
                    typeof(DeviceStatusToColorConvert).FullName, typeof(ColorHelper).FullName);
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[] { "FlowNodes", "FlowEditor.Controls", "WPFControls" }.Select(name => Assembly.Load(name).Location))
            .Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("S4LegacyPlugin" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var plugin = Assembly.Load(stream.ToArray());
        Assert.Equal("1|FlowEditor.Controls.Nodes.NodeModel|WPFControls.Method.QuerySave|WPFControls.ValueConvert.DeviceStatusToColorConvert|WPFControls.Controls.Resources.ColorHelper",
            plugin.GetType("LegacyPlugin")!.GetMethod("Identify")!.Invoke(null, null));
    }

    /// <summary>
    /// 测试相关入口 ProductionScriptRuntimeUsesTheExtractedCoreAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、True、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ProductionScriptRuntimeUsesTheExtractedCoreAssembly()
    {
        var script = new ScriptEditor.Core.Models.ScriptProjectItem
        {
            Title = "S5 Core boundary",
            Code = """
                using Extension.Core.Collections;
                using Extension.Core.Conversion;
                var values = new ConcurrentList<int>(new[] { 3, 5, 8 });
                var restored = values.ToJson().FromJson<int[]>();
                Console.WriteLine(typeof(ConcurrentList<>).Assembly.GetName().Name + ":" + string.Join(",", restored));
                values.Dispose();
                """
        };
        var result = await ScriptEditor.Runtime.Services.RoslynScriptRuntime.Shared.ExecuteAsync(script);
        Assert.True(result.Success, result.Status + Environment.NewLine + result.Output);
        Assert.Contains("Extension.Core:3,5,8", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 Root；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine；具体条件和先后次序见方法体。
    /// </summary>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Build", "Test-Integration.ps1"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Missing repository");
    }

    /// <summary>
    /// 测试相关入口 Read；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、ReadAllText、Combine、Root、Clone；具体条件和先后次序见方法体。
    /// </summary>
    private static JsonElement Read(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Build", "Baselines", name)));
        return document.RootElement.Clone();
    }
}
