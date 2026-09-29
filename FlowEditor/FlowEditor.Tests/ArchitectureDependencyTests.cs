using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
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
    private static readonly string SolutionDirectory = Directory.GetParent(ProjectDirectory)!.FullName;

    /// <summary>确保 FlowEditor 的独立解决方案只引用当前工作区内真实存在的项目。</summary>
    [Fact]
    public void FlowEditorSolution_ReferencesExistingProjects()
    {
        var solutionPath = Path.Combine(SolutionDirectory, "FlowEditor.sln");
        var source = File.ReadAllText(solutionPath);
        var projectMatches = Regex.Matches(
            source,
            "Project\\([^\\r\\n]+?= [^\\r\\n]+?, \\\"(?<path>[^\\\"]+\\.csproj)\\\"");
        var projectPaths = projectMatches
            .Select(match => match.Groups["path"].Value)
            .ToList();

        Assert.Equal(8, projectPaths.Count);
        Assert.Contains("FlowEditor.Core\\FlowEditor.Core.csproj", projectPaths);
        Assert.Contains("FlowEditor.Controls\\FlowEditor.Controls.csproj", projectPaths);
        Assert.DoesNotContain("FlowEditor\\FlowEditor.csproj", projectPaths);
        Assert.Equal("FlowEditor.Controls", typeof(NodeModel).Assembly.GetName().Name);
        Assert.False(File.Exists(Path.Combine(SolutionDirectory, "FlowEditor", "FlowEditor.csproj")));
        Assert.Contains("FlowEditor.Runtime\\FlowEditor.Runtime.csproj", projectPaths);
        Assert.Contains("FlowEditor.Runtime.Smoke\\FlowEditor.Runtime.Smoke.csproj", projectPaths);
        Assert.Contains("FlowEditor.PluginContractFixture\\FlowEditor.PluginContractFixture.csproj", projectPaths);
        Assert.Contains("FlowEditor.Tests\\FlowEditor.Tests.csproj", projectPaths);
        Assert.Contains("WPFControl.Host.Tests\\WPFControl.Host.Tests.csproj", projectPaths);
        Assert.Contains("FlowEditor.DataManagerLogging\\FlowEditor.DataManagerLogging.csproj", projectPaths);
        Assert.All(projectPaths, relativePath =>
            Assert.True(
                File.Exists(Path.GetFullPath(relativePath, SolutionDirectory)),
                $"解决方案项目路径不存在: {relativePath}"));
    }

    /// <summary>确保宿主日志测试只依赖轻量适配器，不会重新带入完整 WPFControl 项目。</summary>
    [Fact]
    public void HostLoggingTests_DoNotReferenceFullWpfControl()
    {
        var hostTestProject = File.ReadAllText(Path.Combine(
            SolutionDirectory,
            "WPFControl.Host.Tests",
            "WPFControl.Host.Tests.csproj"));
        var adapterProject = File.ReadAllText(Path.Combine(
            SolutionDirectory,
            "FlowEditor.DataManagerLogging",
            "FlowEditor.DataManagerLogging.csproj"));

        Assert.Contains("FlowEditor.DataManagerLogging.csproj", hostTestProject, StringComparison.Ordinal);
        Assert.DoesNotContain("WPFControl.csproj", hostTestProject, StringComparison.Ordinal);
        Assert.DoesNotContain("WPFControl.csproj", adapterProject, StringComparison.Ordinal);
    }

    /// <summary>
    /// 确保宿主只把流程日志 adapter 装到共享 LogManager 上，不解析具体日志配置服务。
    /// FlowLog 与 RunLog / SEMILog 同构：消费方按名从 LogManager 取实例与模板，
    /// 不再有「专属配置接口 + 整机服务实现它」这条链路。
    /// </summary>
    [Fact]
    public void WpfHostLogging_WiresSinkThroughSharedLogManagerWithoutHostServiceCoupling()
    {
        var workspaceDirectory = Directory.GetParent(SolutionDirectory)?.FullName
                                 ?? throw new DirectoryNotFoundException("无法定位工作区目录。");
        var moduleSource = File.ReadAllText(Path.Combine(
            workspaceDirectory,
            "WPFControl",
            "WPFControl",
            "WPFControlModule.cs"));

        Assert.Contains("RegisterSingleton<IFlowLogSink>", moduleSource, StringComparison.Ordinal);
        Assert.Contains("new DataManagerFlowLogSink(", moduleSource, StringComparison.Ordinal);
        Assert.Contains("provider.Resolve<LogManager>()", moduleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolve<LogConfigService>", moduleSource, StringComparison.Ordinal);

        var adapterSource = File.ReadAllText(Path.Combine(
            SolutionDirectory,
            "FlowEditor.DataManagerLogging",
            "DataManagerFlowLogSink.cs"));

        Assert.Contains("\"FlowLog\"", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WPFControl", adapterSource, StringComparison.Ordinal);
    }

    /// <summary>确保编辑器日志契约不再把 Extension 及其设备依赖带入核心模块。</summary>
    [Fact]
    public void EditorLogging_DoesNotDependDirectlyOnExtension()
    {
        var projectSource = File.ReadAllText(Path.Combine(ProjectDirectory, "FlowEditor.Controls.csproj"));
        var extensionReferences = Directory.EnumerateFiles(
                ProjectDirectory,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("Extension.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(ProjectDirectory, path))
            .ToList();
        var logLevelSource = File.ReadAllText(Path.Combine(
            SolutionDirectory,
            "FlowEditor.Core",
            "Logging",
            "FlowLogLevel.cs"));

        Assert.DoesNotContain("Extension.csproj", projectSource, StringComparison.Ordinal);
        Assert.Empty(extensionReferences);
        Assert.Contains("public enum FlowLogLevel", logLevelSource, StringComparison.Ordinal);
        Assert.Contains("SysError", logLevelSource, StringComparison.Ordinal);
    }

    /// <summary>确保流程执行日志通过宿主 sink 注入，不在应用服务中直接获取 DataManager。</summary>
    [Fact]
    public void ExecutionLogging_UsesInjectedHostSink()
    {
        var coordinatorSource = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "Services",
            "Execution",
            "FlowExecutionCoordinator.cs"));
        var serviceSource = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "Services",
            "FlowChartService.cs"));
        var sinkSource = File.ReadAllText(Path.Combine(
            SolutionDirectory,
            "FlowEditor.Core",
            "Logging",
            "IFlowLogSink.cs"));
        var contextSource = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "Core",
            "FlowLogContext.cs"));

        Assert.Contains("IFlowLogSink flowLogSink", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("FlowLogContext.EnterSink(_flowLogSink)", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("IFlowLogSink? flowLogSink", serviceSource, StringComparison.Ordinal);
        Assert.Contains("FlowLogRecord", sinkSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FileSink", contextSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DataManager", coordinatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LogManager", coordinatorSource, StringComparison.Ordinal);
    }

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

    /// <summary>确保通用接口层不再反向引用具体 ViewModel，保持依赖方向无环。</summary>
    [Fact]
    public void GeneralInterfaces_DoNotDependOnViewModels()
    {
        var actual = FindDebtFiles(
            "Interfaces",
            "FlowEditor.ViewModels",
            "FlowChartViewModel",
            "NodeViewModel");
        var tabFactorySource = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "ViewModels",
            "IFlowTabFactory.cs"));

        Assert.Empty(actual);
        Assert.Contains("namespace FlowEditor.ViewModels", tabFactorySource, StringComparison.Ordinal);
        Assert.Contains("interface IFlowTabFactory", tabFactorySource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(ProjectDirectory, "Interfaces", "IFlowTabFactory.cs")));
    }

    /// <summary>确保 Core 与 Runtime 均保持无界面，且 Runtime 只向 Core 依赖。</summary>
    [Fact]
    public void RuntimeProject_IsPlatformNeutralAndOwnsExecutionContracts()
    {
        var solutionDirectory = Directory.GetParent(ProjectDirectory)?.FullName
            ?? throw new DirectoryNotFoundException("无法定位 FlowEditor 解决方案目录。");
        var runtimeDirectory = Path.Combine(solutionDirectory, "FlowEditor.Runtime");
        var coreDirectory = Path.Combine(solutionDirectory, "FlowEditor.Core");
        var smokeDirectory = Path.Combine(solutionDirectory, "FlowEditor.Runtime.Smoke");
        var coreProject = File.ReadAllText(Path.Combine(coreDirectory, "FlowEditor.Core.csproj"));
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
        var coreSourceFiles = Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(coreDirectory, path).Replace('\\', '/');
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

        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain(coreSourceFiles, path => forbidden.Any(pattern =>
            File.ReadAllText(path).Contains(pattern, StringComparison.Ordinal)));
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>", project, StringComparison.Ordinal);
        Assert.Contains("FlowEditor.Core.csproj", project, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(project, "<ProjectReference").Cast<Match>());
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", smokeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>", smokeProject, StringComparison.Ordinal);
        Assert.Contains("FlowEditor.Runtime.csproj", smokeProject, StringComparison.Ordinal);
        Assert.DoesNotContain(sourceFiles, path => forbidden.Any(pattern =>
            File.ReadAllText(path).Contains(pattern, StringComparison.Ordinal)));
        Assert.Equal("FlowEditor.Core", typeof(INodeExecutable).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Core", typeof(ConnectionModel).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Core", typeof(PortModel).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowChartExecutionEngine).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ParallelFlowRunner).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(LoopFlowRunner).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ExecutionGraphScope).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(ExecutionScopeRuntime).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowValidator).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowProgram).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Runtime", typeof(FlowRuntimeInstance<>).Assembly.GetName().Name);
        Assert.Equal("FlowEditor.Core", typeof(FlowChartDocumentModel).Assembly.GetName().Name);
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

    /// <summary>确保临时连线、端口吸附和坐标缓存不会重新膨胀回画布宿主视图。</summary>
    [Fact]
    public void FlowChartView_ConnectionDragInteraction_IsDelegated()
    {
        var viewSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Views", "FlowChartView.xaml.cs"));
        var controllerSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Views", "Interaction", "FlowConnectionDragController.cs"));

        Assert.Contains("FlowConnectionDragController", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_connectionCandidate", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_cachedConnection", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnCanvasConnectionMouseUp", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePendingConnectionPath", viewSource, StringComparison.Ordinal);
        Assert.Contains("Mouse.MouseMoveEvent", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Mouse.MouseUpEvent", controllerSource, StringComparison.Ordinal);
    }

    /// <summary>确保节点点击与框选状态不会重新膨胀回画布宿主视图或 XAML 事件入口。</summary>
    [Fact]
    public void FlowChartView_SelectionInteraction_IsDelegated()
    {
        var viewSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Views", "FlowChartView.xaml.cs"));
        var xamlSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Views", "FlowChartView.xaml"));
        var controllerSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Views", "Interaction", "FlowSelectionController.cs"));

        Assert.Contains("FlowSelectionController", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_isBoxSelecting", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_boxSelectStart", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateBoxSelection", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnCanvasMouseDown", xamlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnCanvasMouseMove", xamlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnCanvasMouseUp", xamlSource, StringComparison.Ordinal);
        Assert.Contains("RegisterNode", controllerSource, StringComparison.Ordinal);
        Assert.Contains("UpdateBoxSelection", controllerSource, StringComparison.Ordinal);
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

    /// <summary>确保执行协调模块只处理节点模型，不再反向依赖画布 ViewModel。</summary>
    [Fact]
    public void ExecutionCoordinator_DoesNotDependOnViewModels()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "Services",
            "Execution",
            "FlowExecutionCoordinator.cs"));

        Assert.DoesNotContain("FlowEditor.ViewModels", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeViewModel", source, StringComparison.Ordinal);
        Assert.Contains("Func<IReadOnlyList<NodeModel>>", source, StringComparison.Ordinal);
        Assert.Contains("StepContinueAsync(NodeModel node", source, StringComparison.Ordinal);
        Assert.Contains("ContinueFromNodeAsync(NodeModel node", source, StringComparison.Ordinal);
    }

    /// <summary>确保文档会话只映射节点模型，不再反向依赖画布 ViewModel。</summary>
    [Fact]
    public void DocumentSession_DoesNotDependOnViewModels()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "Services",
            "Documents",
            "FlowDocumentSession.cs"));

        Assert.DoesNotContain("FlowEditor.ViewModels", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeViewModel", source, StringComparison.Ordinal);
        Assert.Contains("Func<IReadOnlyList<NodeModel>>", source, StringComparison.Ordinal);
        Assert.Contains("Action<NodeModel> addNode", source, StringComparison.Ordinal);
        Assert.Contains("node.OnFlowSaved()", source, StringComparison.Ordinal);
    }

    /// <summary>确保画布领域规则只处理节点模型，表现层包装停留在专用适配器。</summary>
    [Fact]
    public void CanvasSession_DoesNotDependOnViewModels()
    {
        var sessionSource = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "Services",
            "Editing",
            "FlowCanvasSession.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "ViewModels",
            "FlowCanvasViewModelAdapter.cs"));

        Assert.DoesNotContain("FlowEditor.ViewModels", sessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeViewModel", sessionSource, StringComparison.Ordinal);
        Assert.Contains("Func<IReadOnlyList<NodeModel>>", sessionSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<NodeModel> ResolveDragParticipants", sessionSource, StringComparison.Ordinal);
        Assert.Contains("RefreshExecutionMembership(NodeModel node", sessionSource, StringComparison.Ordinal);
        Assert.Contains("FlowCanvasSession _session", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ObservableCollection<NodeViewModel>", adapterSource, StringComparison.Ordinal);
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
        Assert.Contains("Documents.RebuildNodeFromDocument", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildNodeDocument", source, StringComparison.Ordinal);
    }

    /// <summary>确保流程组合根不再转发执行、编辑、持久化、输出和视口操作。</summary>
    [Fact]
    public void FlowChartService_OperationalForwarders_AreAbsent()
    {
        var serviceSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Services", "FlowChartService.cs"));
        var contextSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Interfaces", "IFlowChartService.cs"));
        var viewModelSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    Path.Combine(ProjectDirectory, "ViewModels"),
                    "FlowChartViewModel*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        foreach (var forbidden in new[]
                 {
                     "public Task RunAsync", "public bool Save(", "public void AddNode(",
                     "public void AppendLog", "public void ZoomIn", "public void ClearAll"
                 })
        {
            Assert.DoesNotContain(forbidden, serviceSource, StringComparison.Ordinal);
            Assert.DoesNotContain(forbidden, contextSource, StringComparison.Ordinal);
        }

        Assert.Contains("_execution.RunAsync", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_documents.Save", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_canvas.ClearAll", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_output.Append", viewModelSource, StringComparison.Ordinal);
    }

    /// <summary>确保流程 ViewModel 的高变化职责保留在命名明确的 partial 文件中。</summary>
    [Fact]
    public void FlowChartViewModel_Responsibilities_AreSplitIntoPartialFiles()
    {
        var viewModelDirectory = Path.Combine(ProjectDirectory, "ViewModels");
        var mainSource = File.ReadAllText(Path.Combine(viewModelDirectory, "FlowChartViewModel.cs"));
        var responsibilityFiles = new[]
        {
            "FlowChartViewModel.Execution.cs",
            "FlowChartViewModel.Documents.cs",
            "FlowChartViewModel.Connections.cs",
            "FlowChartViewModel.Presentation.cs",
            "FlowChartViewModel.Interaction.cs",
            "FlowChartViewModel.Clipboard.cs"
        };

        Assert.True(File.ReadAllLines(Path.Combine(viewModelDirectory, "FlowChartViewModel.cs")).Length <= 350);
        Assert.DoesNotContain("_execution.RunAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_documents.Save", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompleteConnection(", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportPng(", mainSource, StringComparison.Ordinal);

        foreach (var fileName in responsibilityFiles)
        {
            var source = File.ReadAllText(Path.Combine(viewModelDirectory, fileName));
            Assert.Contains("partial class FlowChartViewModel", source, StringComparison.Ordinal);
        }
    }

    /// <summary>确保容器几何、成员归属与作用域连线清理由画布编辑模块统一拥有。</summary>
    [Fact]
    public void ContainerMembershipRules_AreOwnedByCanvasModule()
    {
        var viewModelDirectory = Path.Combine(ProjectDirectory, "ViewModels");
        var viewModelSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    viewModelDirectory,
                    "FlowChartViewModel*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        var canvasSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Services", "Editing", "FlowCanvasSession.cs"));

        Assert.Contains("ResolveDragParticipants", canvasSource, StringComparison.Ordinal);
        Assert.Contains("RefreshExecutionMembership", canvasSource, StringComparison.Ordinal);
        Assert.Contains("RemoveConnectionsOutsideCurrentScope", canvasSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldPreserveLoopBoundaryConnections", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsExecutionDescendantOf", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveConnectionsOutsideCurrentScope", viewModelSource, StringComparison.Ordinal);
    }

    /// <summary>确保节点生命周期上下文不再向领域节点泄漏表现层 ViewModel。</summary>
    [Fact]
    public void FlowChartNodeContext_ViewModelDependencies_AreAbsent()
    {
        var contextSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Interfaces", "IFlowChartService.cs"));
        var serviceSource = File.ReadAllText(Path.Combine(ProjectDirectory, "Services", "FlowChartService.cs"));

        Assert.DoesNotContain("FlowEditor.ViewModels", contextSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeViewModel", contextSource, StringComparison.Ordinal);
        Assert.Contains("IEnumerable<NodeModel>", contextSource, StringComparison.Ordinal);
        Assert.Contains("IFlowChartService.Nodes", serviceSource, StringComparison.Ordinal);
        Assert.Contains("node.Model", serviceSource, StringComparison.Ordinal);
    }

    /// <summary>确保普通执行与循环边界复用 Runtime 中唯一的端口值传递规则。</summary>
    [Fact]
    public void PortValueTransferRules_HaveSingleRuntimeImplementation()
    {
        var transferSource = File.ReadAllText(Path.Combine(
            SolutionDirectory, "FlowEditor.Runtime", "Core", "PortValueTransfer.cs"));
        var engineSource = File.ReadAllText(Path.Combine(
            SolutionDirectory, "FlowEditor.Runtime", "Core", "FlowChartExecutionEngine.cs"));
        var loopSource = File.ReadAllText(Path.Combine(
            ProjectDirectory, "Nodes", "FlowControl", "LoopContainerNode.cs"));

        Assert.Contains("IsTypeCompatibleWith", transferSource, StringComparison.Ordinal);
        Assert.Contains("IsCompatible", transferSource, StringComparison.Ordinal);
        Assert.Contains("PortValueTransfer.TryTransfer", engineSource, StringComparison.Ordinal);
        Assert.Contains("PortValueTransfer.TryTransfer", loopSource, StringComparison.Ordinal);

        foreach (var callerSource in new[] { engineSource, loopSource })
        {
            Assert.DoesNotContain("IsTypeCompatibleWith", callerSource, StringComparison.Ordinal);
            Assert.DoesNotContain(".IsCompatible(", callerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("声明类型不兼容", callerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("运行时类型:", callerSource, StringComparison.Ordinal);
        }
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
        var nodeSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    Path.Combine(ProjectDirectory, "Models"),
                    "NodeModel*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
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

    /// <summary>确保节点数据绑定与连线订阅职责保留在命名 partial 文件中。</summary>
    [Fact]
    public void NodeModel_DataBindingResponsibilities_AreSplitFromCoreFile()
    {
        var modelDirectory = Path.Combine(ProjectDirectory, "Models");
        var coreSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.cs"));
        var bindingSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.DataBindings.cs"));

        Assert.True(File.ReadAllLines(Path.Combine(modelDirectory, "NodeModel.cs")).Length <= 800);
        Assert.Contains("partial class NodeModel", bindingSource, StringComparison.Ordinal);
        Assert.Contains("IDataBindingProvider.CreateDataBinding", bindingSource, StringComparison.Ordinal);
        Assert.Contains("EnsureConnectionSubscription", bindingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IDataBindingProvider.CreateDataBinding", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureConnectionSubscription", coreSource, StringComparison.Ordinal);
    }

    /// <summary>确保动态端口集合、缓存、编辑和属性端口配置集中在命名 partial 文件中。</summary>
    [Fact]
    public void NodeModel_PortResponsibilities_AreSplitFromCoreFile()
    {
        var modelDirectory = Path.Combine(ProjectDirectory, "Models");
        var corePath = Path.Combine(modelDirectory, "NodeModel.cs");
        var coreSource = File.ReadAllText(corePath);
        var portSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.Ports.cs"));

        Assert.True(File.ReadAllLines(corePath).Length <= 500);
        Assert.Contains("partial class NodeModel", portSource, StringComparison.Ordinal);
        Assert.Contains("BuildInputPorts", portSource, StringComparison.Ordinal);
        Assert.Contains("RemovePortConnections", portSource, StringComparison.Ordinal);
        Assert.Contains("SetPortConfig", portSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildInputPorts", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RemovePortConnections", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetPortConfig", coreSource, StringComparison.Ordinal);
    }

    /// <summary>确保端口查询、运行时属性同步与反射缓存集中在命名 partial 文件中。</summary>
    [Fact]
    public void NodeModel_PortValueResponsibilities_AreSplitFromCoreFile()
    {
        var modelDirectory = Path.Combine(ProjectDirectory, "Models");
        var corePath = Path.Combine(modelDirectory, "NodeModel.cs");
        var coreSource = File.ReadAllText(corePath);
        var valueSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.PortValues.cs"));

        Assert.True(File.ReadAllLines(corePath).Length <= 350);
        Assert.Contains("partial class NodeModel", valueSource, StringComparison.Ordinal);
        Assert.Contains("public void SyncInputValues()", valueSource, StringComparison.Ordinal);
        Assert.Contains("PropertyLookupCache", valueSource, StringComparison.Ordinal);
        Assert.Contains("public List<string> GetBindableProperties()", valueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public void SyncInputValues()", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyLookupCache", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public List<string> GetBindableProperties()", coreSource, StringComparison.Ordinal);
    }

    /// <summary>确保 PropertyGrid 接口声明和 ICommand 扩展入口不再散落到节点核心与生命周期文件。</summary>
    [Fact]
    public void NodeModel_PropertyGridCompatibility_IsIsolatedInNamedPartial()
    {
        var modelDirectory = Path.Combine(ProjectDirectory, "Models");
        var coreSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.cs"));
        var lifecycleSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.DataBindings.cs"));
        var propertyGridSource = File.ReadAllText(Path.Combine(modelDirectory, "NodeModel.PropertyGrid.cs"));
        var projectSource = File.ReadAllText(Path.Combine(ProjectDirectory, "FlowEditor.Controls.csproj"));

        Assert.Contains(
            "partial class NodeModel : IPortConfigurable, IPortManageable, IDataBindingProvider",
            propertyGridSource,
            StringComparison.Ordinal);
        Assert.Contains("PropertyGridAction", propertyGridSource, StringComparison.Ordinal);
        Assert.Contains("ICommand? OpenScriptEditorCommand", propertyGridSource, StringComparison.Ordinal);
        Assert.Contains("WPFCustomControls.PropertyGrid.Core.csproj", projectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IPortConfigurable", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IDataBindingProvider", coreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Input", lifecycleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyGridAction", lifecycleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using WPFCustomControls.PropertyGrid;", propertyGridSource, StringComparison.Ordinal);
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

    /// <summary>确保插件契约测试通过独立 DLL seam 验收，而不直接编译引用夹具节点类型。</summary>
    [Fact]
    public void PluginContractTests_LoadIndependentArtifactWithoutTypeReference()
    {
        var solutionDirectory = Directory.GetParent(ProjectDirectory)?.FullName
                                ?? throw new InvalidOperationException("无法定位解决方案目录。");
        var testProject = File.ReadAllText(Path.Combine(
            solutionDirectory,
            "FlowEditor.Tests",
            "FlowEditor.Tests.csproj"));
        var contractTests = File.ReadAllText(Path.Combine(
            solutionDirectory,
            "FlowEditor.Tests",
            "PluginContractTests.cs"));

        Assert.Contains("FlowEditor.PluginContractFixture.csproj", testProject, StringComparison.Ordinal);
        Assert.Contains("ReferenceOutputAssembly=\"false\"", testProject, StringComparison.Ordinal);
        Assert.Contains("Assembly.LoadFrom", contractTests, StringComparison.Ordinal);
        Assert.DoesNotContain("using FlowEditor.PluginContractFixture", contractTests, StringComparison.Ordinal);
    }

    /// <summary>确保测试通过真实 adapter 构造对象，不再使用 null-forgiving 掩盖缺失依赖。</summary>
    [Fact]
    public void TestSources_NullForgivingDependencies_AreAbsent()
    {
        var testDirectory = Directory.GetParent(ProjectDirectory)?.FullName is { } solutionDirectory
            ? Path.Combine(solutionDirectory, "FlowEditor.Tests")
            : throw new DirectoryNotFoundException("无法定位 FlowEditor.Tests 目录。");
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

    /// <summary>从测试输出目录向上查找 Controls 项目目录。</summary>
    /// <returns>FlowEditor.Controls.csproj 所在目录。</returns>
    /// <exception cref="DirectoryNotFoundException">无法定位 FlowEditor.sln 或 FlowEditor.Controls.csproj 时抛出。</exception>
    private static string FindProjectDirectory()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            // P13：Flow.sln 已作为重复聚合入口删除，统一以 FlowEditor.sln 为模块入口。
            if (!File.Exists(Path.Combine(current.FullName, "FlowEditor.sln")))
                continue;

            var projectDirectory = Path.Combine(current.FullName, "FlowEditor.Controls");
            if (File.Exists(Path.Combine(projectDirectory, "FlowEditor.Controls.csproj")))
                return projectDirectory;
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位 FlowEditor 项目。");
    }
}
