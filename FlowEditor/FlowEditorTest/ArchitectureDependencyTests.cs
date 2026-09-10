using System.IO;
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

        Assert.Equal(
            [
                "Core/FlowChartPersistenceService.cs",
                "Core/FlowToolDiscoveryService.cs"
            ],
            actual);
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

    /// <summary>确保流程节点不会新增对 WPF 窗口和表现层的依赖。</summary>
    [Fact]
    public void NodeWpfDependencies_AreLimitedToKnownMigrationDebt()
    {
        var actual = FindDebtFiles(
            "Nodes",
            "FlowEditor.Views",
            "System.Windows",
            "MessageBox",
            "Window");

        Assert.Equal(
            [
                "Nodes/FlowControl/LoopNode.cs",
                "Nodes/FlowControl/SwitchNode.cs"
            ],
            actual);
    }

    /// <summary>确保 Prism 容器解析不会扩散到更多非组合模块。</summary>
    [Fact]
    public void ContainerAccess_IsLimitedToKnownMigrationDebt()
    {
        var actual = FindDebtFiles(
                ".",
                "IContainerProvider",
                ".Resolve<",
                ".Resolve(",
                "FallbackResolver")
            .Where(path => path != "FlowEditorModule.cs")
            .ToArray();

        Assert.Equal(
            [
                "Core/FlowChartPersistenceService.cs",
                "Core/FlowToolDiscoveryService.cs",
                "Core/ReflectionNodeFactory.cs",
                "Interfaces/IFlowChartService.cs",
                "Services/FlowChartContainerDocumentService.cs",
                "Services/FlowChartService.cs",
                "Services/ToolboxService.cs",
                "ViewModels/FlowChartContainerViewModel.cs",
                "ViewModels/FlowChartViewModel.cs",
                "ViewModels/ToolboxItems.cs",
                "Views/ToolBlockFlowWindow.xaml.cs"
            ],
            actual);
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
                "ViewModels/Commands/FlowChartViewModel.Commands.cs"
            ],
            actual);
    }

    /// <summary>确保 WPF Routed Command 只在 Presentation 命令目录中声明。</summary>
    [Fact]
    public void RoutedCommandDeclarations_AreKeptInPresentationCommands()
    {
        var actual = FindDebtFiles(".", "RoutedUICommand");

        Assert.Equal(
            ["Presentation/Commands/FlowChartRoutedCommands.cs"],
            actual);
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
