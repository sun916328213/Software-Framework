using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.ViewModels;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace FlowEditorTest;

/// <summary>
/// 锁定当前已知的反向依赖，防止迁移期间继续扩大架构债务。
/// 当某个历史依赖被移除时，应同步从 allowlist 删除对应文件。
/// </summary>
public sealed class ArchitectureDependencyTests
{
    private static readonly string ProjectDirectory = FindProjectDirectory();

    /// <summary>确保 Core 不会新增对表现层、WPF 或 Prism 的依赖。</summary>
    [Fact]
    public void CorePresentationDependencies_AreLimitedToKnownMigrationDebt()
    {
        var actual = FindDebtFiles(
            "Core",
            "FlowEditor.ViewModels",
            "FlowEditor.Views",
            "System.Windows",
            "Prism.");

        Assert.Empty(actual);
    }

    /// <summary>确保 Core 完全不引用 ViewModel 类型或命名空间。</summary>
    [Fact]
    public void CoreViewModelDependencies_AreAbsent()
    {
        var actual = FindDebtFiles(
            "Core",
            "FlowEditor.ViewModels",
            "NodeViewModel",
            "FlowChartViewModel");

        Assert.Empty(actual);
    }

    /// <summary>确保 P5 Runtime 可独立运行，并已拥有执行契约、引擎和容器 runner。</summary>
    [Fact]
    public void RuntimeProject_IsPlatformNeutralAndOwnsExecutionContracts()
    {
        var solutionDirectory = Directory.GetParent(ProjectDirectory)?.FullName
            ?? throw new DirectoryNotFoundException("无法定位 FlowEditor 解决方案目录。");
        var runtimeDirectory = Path.Combine(solutionDirectory, "FlowEditor.Runtime");
        var smokeDirectory = Path.Combine(solutionDirectory, "FlowEditor.Runtime.Smoke");
        var project = File.ReadAllText(Path.Combine(runtimeDirectory, "FlowEditor.Runtime.csproj"));
        var smokeProject = File.ReadAllText(Path.Combine(
            smokeDirectory, "FlowEditor.Runtime.Smoke.csproj"));
        var sourceFiles = Directory.EnumerateFiles(runtimeDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(runtimeDirectory, path).Replace('\\', '/');
                return !relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                       !relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
        var forbidden = new[]
        {
            "System.Windows",
            "Prism.",
            "FlowEditor.Views",
            "FlowEditor.ViewModels",
            "FlowEditor.Services",
            "Extension."
        };

        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", project, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", smokeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>", smokeProject, StringComparison.Ordinal);
        Assert.Contains("FlowEditor.Runtime.csproj", smokeProject, StringComparison.Ordinal);
        Assert.DoesNotContain(sourceFiles, path => forbidden.Any(pattern =>
            File.ReadAllText(path).Contains(pattern, StringComparison.Ordinal)));
        Assert.Equal("FlowEditor.Runtime", typeof(INodeExecutable).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ConnectionModel).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(PortModel).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowChartExecutionEngine).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ParallelFlowRunner).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(LoopFlowRunner).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ExecutionGraphScope).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ExecutionScopeRuntime).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowValidator).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowProgram).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowRuntimeInstance<>).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowChartDocumentModel).Assembly.GetName().Name);
    }

    /// <summary>确保表现层节点不再充当运行时执行适配器。</summary>
    [Fact]
    public void NodeViewModel_DoesNotImplementRuntimeExecutionInterfaces()
    {
        var interfaces = typeof(NodeViewModel).GetInterfaces();

        Assert.DoesNotContain(typeof(INodeExecutable), interfaces);
        Assert.DoesNotContain(typeof(INodeFaultPolicy), interfaces);
        Assert.DoesNotContain(typeof(ILoopControlNode), interfaces);
    }

    /// <summary>确保流程节点不依赖 WPF 窗口或表现层。</summary>
    [Fact]
    public void NodeWpfDependencies_AreAbsent()
    {
        var actual = FindDebtFiles(
            "Nodes",
            "FlowEditor.Views",
            "FlowEditor.Presentation",
            "System.Windows",
            "MessageBox",
            "Window");

        Assert.Empty(actual);
    }

    /// <summary>确保 Prism 容器解析不会扩散到更多非组合模块。</summary>
    [Fact]
    public void ContainerAccess_IsLimitedToKnownMigrationDebt()
    {
        var actual = FindDebtFiles(
                ".",
                "IContainerProvider",
                ".Resolve<",
                ".Resolve(")
            .Where(path => path != "FlowEditorModule.cs")
            .Where(path => path != "Presentation/Factories/PrismNodeFactory.cs")
            .Where(path => path != "Presentation/Factories/PrismFlowTabFactory.cs")
            .Where(path => path != "Presentation/NodeEditing/WpfNodeEditorLauncher.cs")
            .ToArray();

        Assert.Empty(actual);
    }

    /// <summary>确保 ViewModel 命令实例只在对应的命令 partial 文件中创建。</summary>
    [Fact]
    public void ViewModelCommandConstruction_IsKeptInCommandPartials()
    {
        var actual = FindDebtFiles(
            "ViewModels",
            "new RelayCommand",
            "new AsyncRelayCommand");

        Assert.Equal(
            [
                "ViewModels/Commands/FlowChartContainerViewModel.Commands.cs",
                "ViewModels/Commands/FlowChartViewModel.Commands.cs",
                "ViewModels/Commands/NodeActionViewModel.cs"
            ],
            actual);
    }

    /// <summary>确保视口平移和缩放状态不会重新膨胀回画布宿主视图。</summary>
    [Fact]
    public void FlowChartView_ViewportInteraction_IsDelegated()
    {
        var viewSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Views", "FlowChartView.xaml.cs"));
        var xamlSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Views", "FlowChartView.xaml"));
        var controllerSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Views", "Interaction", "FlowViewportController.cs"));

        Assert.Contains("FlowViewportController", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_isPanning", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnPanMouse", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnScrollViewerPreviewMouseWheel", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnPanMouse", xamlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnScrollViewerPreviewMouseWheel", xamlSource, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseWheel +=", controllerSource, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseDown +=", controllerSource, StringComparison.Ordinal);
    }

    /// <summary>确保没有为单次视图转发保留全局 WPF 路由命令。</summary>
    [Fact]
    public void RoutedCommandDeclarations_AreAbsent()
    {
        var actual = FindDebtFiles(".", "RoutedUICommand");

        Assert.Empty(actual);
    }

    /// <summary>确保流程应用服务不再直接访问 WPF Application 或 Dispatcher。</summary>
    [Fact]
    public void FlowChartService_WpfDispatcherDependencies_AreAbsent()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "Services", "FlowChartService.cs"));

        Assert.DoesNotContain("Application.Current", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".CheckAccess()", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".HasShutdownStarted", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".HasShutdownFinished", source, StringComparison.Ordinal);
    }

    /// <summary>确保执行会话生命周期不会重新膨胀回流程门面。</summary>
    [Fact]
    public void FlowChartService_ExecutionLifecycle_IsDelegatedToCoordinator()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "Services", "FlowChartService.cs"));

        Assert.DoesNotContain("CancellationTokenSource", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FlowValidator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionGraphScope", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FlowLogContext", source, StringComparison.Ordinal);
        Assert.Contains("FlowExecutionCoordinator", source, StringComparison.Ordinal);
    }

    /// <summary>确保文档事务和画布生命周期不会重新膨胀回流程门面。</summary>
    [Fact]
    public void FlowChartService_DocumentAndCanvasResponsibilities_AreDelegated()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "Services", "FlowChartService.cs"));

        Assert.DoesNotContain("JsonSerializer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnFlowSaved", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetVisualWidth", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnAttached(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_documentMapper", source, StringComparison.Ordinal);
        Assert.Contains("FlowDocumentSession", source, StringComparison.Ordinal);
        Assert.Contains("FlowCanvasSession", source, StringComparison.Ordinal);
        Assert.Contains("_documents.RebuildNodeFromDocument", source, StringComparison.Ordinal);
        Assert.Contains("_documents.BuildNodeDocument", source, StringComparison.Ordinal);
    }

    /// <summary>确保 Phase 4 应用模块保持平台无关，不直接引用 WPF 或 Prism。</summary>
    [Fact]
    public void Phase4ApplicationModules_WpfAndPrismDependencies_AreAbsent()
    {
        var moduleDirectories = new[]
        {
            "Services/Execution",
            "Services/Documents",
            "Services/Editing"
        };

        var offenders = moduleDirectories.SelectMany(directory => FindDebtFiles(
                directory,
                "System.Windows",
                "Prism.",
                "Application.Current",
                "IContainerProvider"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>确保对话框 seam 只暴露宿主无关意图，不泄漏 WPF 消息框类型或无调用入口。</summary>
    [Fact]
    public void DialogInterface_IsPlatformNeutralAndMinimal()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "Interfaces", "IDialogService.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowMessage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowWarning", source, StringComparison.Ordinal);
    }

    /// <summary>确保导出 seam 只暴露表现层实际调用的两个对话框意图。</summary>
    [Fact]
    public void ExportInterface_ContainsOnlyUsedDialogOperations()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "Interfaces", "IExportService.cs"));

        Assert.DoesNotContain("void Export", source, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, @"\bbool\s+ExportTo\w+\s*\(").Count);
    }

    /// <summary>确保画布和 ToolBlock 统一使用节点受控生命周期，不绕过事件退订与引用清理。</summary>
    [Fact]
    public void NodeAttachmentLifecycle_IsCentralizedInNodeModel()
    {
        var nodeSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Models", "NodeModel.cs"));
        var canvasSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Services", "Editing", "FlowCanvasSession.cs"));
        var toolBlockSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Nodes", "FlowControl", "ToolBlockNode.cs"));

        Assert.Contains("internal void AttachTo", nodeSource, StringComparison.Ordinal);
        Assert.Contains("internal void DetachFromFlow", nodeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Service =", canvasSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".OnAttached(", canvasSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".OnDetached(", canvasSource, StringComparison.Ordinal);
        Assert.Contains(".AttachTo(", canvasSource, StringComparison.Ordinal);
        Assert.Contains(".DetachFromFlow(", canvasSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Service =", toolBlockSource, StringComparison.Ordinal);
        Assert.Contains(".AttachTo(", toolBlockSource, StringComparison.Ordinal);
        Assert.Contains(".DetachFromFlow(", toolBlockSource, StringComparison.Ordinal);
    }

    /// <summary>确保流程文档存储 seam 只暴露保存和加载，不再承担模型映射。</summary>
    [Fact]
    public void FlowDocumentStoreInterface_ContainsOnlyFileOperations()
    {
        var methods = typeof(FlowEditor.Interfaces.IFlowDocumentStore)
            .GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Load", "Save"], methods);
    }

    /// <summary>确保反射节点 factory 不再暴露可被宿主或测试互相覆盖的静态状态。</summary>
    [Fact]
    public void ReflectionNodeFactory_PublicStaticMutableState_IsAbsent()
    {
        var type = typeof(ReflectionNodeFactory);
        var mutableFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => !field.IsInitOnly)
            .ToArray();
        var mutableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.SetMethod != null)
            .ToArray();

        Assert.Empty(mutableFields);
        Assert.Empty(mutableProperties);
    }

    /// <summary>确保测试通过真实 adapter 构造对象，不再使用 null-forgiving 掩盖缺失依赖。</summary>
    [Fact]
    public void TestSources_NullForgivingDependencies_AreAbsent()
    {
        var testDirectory = Directory.GetParent(ProjectDirectory)?.FullName is { } solutionDirectory
            ? Path.Combine(solutionDirectory, "FlowEditorTest")
            : throw new DirectoryNotFoundException("无法定位 FlowEditorTest 目录。");
        var nullForgivingToken = string.Concat("null", "!");
        var offenders = Directory.EnumerateFiles(testDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => File.ReadAllText(path).Contains(nullForgivingToken, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>确保已迁移的应用模块、ViewModel 和窗口不再使用 Prism Service Locator。</summary>
    [Fact]
    public void MigratedApplicationModules_PrismDependencies_AreAbsent()
    {
        var paths = new[]
        {
            Path.Combine(ProjectDirectory, "Core", "FlowToolDiscoveryService.cs"),
            Path.Combine(ProjectDirectory, "Services", "FlowChartContainerDocumentService.cs"),
            Path.Combine(ProjectDirectory, "Services", "FlowChartService.cs"),
            Path.Combine(ProjectDirectory, "Services", "ToolboxService.cs"),
            Path.Combine(ProjectDirectory, "Interfaces", "IFlowChartService.cs"),
            Path.Combine(ProjectDirectory, "ViewModels", "FlowChartContainerViewModel.cs"),
            Path.Combine(ProjectDirectory, "ViewModels", "FlowChartViewModel.cs"),
            Path.Combine(ProjectDirectory, "ViewModels", "ToolboxItems.cs"),
            Path.Combine(ProjectDirectory, "Views", "ToolBlockFlowWindow.xaml.cs")
        };

        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("Prism.Ioc", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IContainerProvider", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".Resolve<", source, StringComparison.Ordinal);
        }
    }

    /// <summary>确保全部生产 C# 文件中的每个函数都有紧邻的 XML 摘要。</summary>
    [Fact]
    public void CorePresentationFiles_MethodDocumentation_IsComplete()
    {
        var relativePaths = Directory
            .EnumerateFiles(ProjectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Select(path => Path.GetRelativePath(ProjectDirectory, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var methodPattern = new Regex(
            @"^\s*(public|internal|protected|private)\s+" +
            @"(?:(?:static|virtual|override|abstract|async|sealed|partial|new|readonly)\s+)*" +
            @"(?:(?:[\w\.\?\[\]]+|[\w\.\?\[\]]+\s*<[^;={}]+>)\s+)?" +
            @"\w+(?:<[^>]+>)?\s*\(",
            RegexOptions.CultureInvariant);
        var undocumented = new List<string>();
        var misplaced = new List<string>();

        foreach (var relativePath in relativePaths)
        {
            var lines = File.ReadAllLines(Path.Combine(ProjectDirectory, relativePath));
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].TrimStart().StartsWith("///", StringComparison.Ordinal))
                {
                    var previous = index - 1;
                    while (previous >= 0 && string.IsNullOrWhiteSpace(lines[previous]))
                        previous--;
                    if (previous >= 0 && lines[previous].TrimStart().StartsWith("[", StringComparison.Ordinal))
                        misplaced.Add($"{relativePath.Replace('\\', '/')}:{index + 1}");
                }

                if (!methodPattern.IsMatch(lines[index]))
                    continue;

                var preceding = index - 1;
                while (preceding >= 0 &&
                       (string.IsNullOrWhiteSpace(lines[preceding]) ||
                        lines[preceding].TrimStart().StartsWith("[", StringComparison.Ordinal)))
                {
                    preceding--;
                }

                if (preceding < 0 ||
                    !lines[preceding].TrimStart().StartsWith("///", StringComparison.Ordinal))
                {
                    undocumented.Add($"{relativePath.Replace('\\', '/')}:{index + 1}");
                }
            }
        }

        Assert.Empty(undocumented);
        Assert.Empty(misplaced);
    }

    /// <summary>查找指定目录中包含任一禁止文本的 C# 文件。</summary>
    /// <param name="relativeDirectory">相对于 FlowEditor 项目的扫描目录。</param>
    /// <param name="patterns">用于识别架构依赖的文本模式。</param>
    /// <returns>按路径排序并使用正斜杠的项目相对路径。</returns>
    private static string[] FindDebtFiles(string relativeDirectory, params string[] patterns)
    {
        var directory = Path.GetFullPath(Path.Combine(ProjectDirectory, relativeDirectory));
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return patterns.Any(pattern => source.Contains(pattern, StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(ProjectDirectory, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>判断文件是否位于编译生成目录。</summary>
    /// <param name="path">待检查的绝对文件路径。</param>
    /// <returns>位于 bin 或 obj 目录时返回 true。</returns>
    private static bool IsGeneratedPath(string path)
    {
        var relativePath = Path.GetRelativePath(ProjectDirectory, path).Replace('\\', '/');
        return relativePath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>从测试输出目录向上查找 FlowEditor 项目目录。</summary>
    /// <returns>FlowEditor.csproj 所在目录。</returns>
    /// <exception cref="DirectoryNotFoundException">无法定位 Flow.sln 或 FlowEditor.csproj 时抛出。</exception>
    private static string FindProjectDirectory()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            if (!File.Exists(Path.Combine(current.FullName, "Flow.sln")))
                continue;

            var projectDirectory = Path.Combine(current.FullName, "FlowEditor");
            if (File.Exists(Path.Combine(projectDirectory, "FlowEditor.csproj")))
                return projectDirectory;
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位 FlowEditor 项目。");
    }
}
