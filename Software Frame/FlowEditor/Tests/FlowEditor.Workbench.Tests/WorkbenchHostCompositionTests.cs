// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchHostCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Linq、System.Xml.Linq、Xunit。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// 宿主装配静态断言：不启动 WPF，只核对 <c>FlowEditor.Workbench</c> 的装配契约——
/// 壳经模块 + 区域导航装载编辑器、不重复合并 FlowEditor 资源、不内嵌编辑视图、
/// 不引用 FlowNodes 或测试工程。改动宿主装配方式时，此线会先失败。
/// </summary>
public sealed class WorkbenchHostCompositionTests
{
    private static readonly string SolutionDirectory = FindSolutionDirectory();
    private static readonly string WorkbenchDirectory = Path.Combine(SolutionDirectory, "FlowEditor.Workbench");

    /// <summary>宿主装配入口文件必须齐备。</summary>
    [Fact]
    public void WorkbenchHost_CarriesEntryPointFiles()
    {
        foreach (var relative in new[]
                 {
                     "FlowEditor.Workbench.csproj",
                     "App.xaml",
                     "App.xaml.cs",
                     Path.Combine("Views", "MainWindow.xaml"),
                     Path.Combine("Views", "MainWindow.xaml.cs")
                 })
        {
            Assert.True(
                File.Exists(Path.Combine(WorkbenchDirectory, relative)),
                $"宿主装配文件缺失: {relative}");
        }
    }

    /// <summary>宿主必须注册 FlowEditor 模块并经由区域导航装载编辑器，而非直接内嵌视图。</summary>
    [Fact]
    public void Host_ComposesModuleAndNavigatesEditorIntoRegion()
    {
        var app = File.ReadAllText(Path.Combine(WorkbenchDirectory, "App.xaml.cs"));

        Assert.Contains("moduleCatalog.AddModule<FlowEditorModule>()", app, StringComparison.Ordinal);
        Assert.Contains("?.SelectFirst()", app, StringComparison.Ordinal);
        var shell = File.ReadAllText(Path.Combine(WorkbenchDirectory, "ViewModels", "MainWindowViewModel.cs"));
        Assert.Contains("WorkbenchNavigationViewModel<WorkbenchPage>", shell, StringComparison.Ordinal);
        Assert.Contains("\"FlowEditorRegion\"", shell, StringComparison.Ordinal);
        Assert.Contains("nameof(FlowChartContainerView)", shell, StringComparison.Ordinal);
        Assert.Contains("Container.Resolve<MainWindow>()", app, StringComparison.Ordinal);
    }

    /// <summary>App 层只合并 MaterialDesign 与公共控件资源；FlowEditor 资源由模块合并，不得重复。</summary>
    [Fact]
    public void ShellResources_DoNotDuplicateFlowEditorModuleDictionary()
    {
        var appXaml = File.ReadAllText(Path.Combine(WorkbenchDirectory, "App.xaml"));

        Assert.Contains("MaterialDesign3.Defaults.xaml", appXaml, StringComparison.Ordinal);
        Assert.Contains(
            "pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml",
            appXaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FlowEditor.Controls;component/Resources/Resources.xaml",
            appXaml,
            StringComparison.Ordinal);
    }

    /// <summary>壳只声明区域，不得在 XAML 内嵌编辑器视图（CreateShell 阶段模块尚未加载）。</summary>
    [Fact]
    public void ShellWindow_DeclaresRegionWithoutEmbeddingEditorView()
    {
        var shellXaml = File.ReadAllText(Path.Combine(WorkbenchDirectory, "Views", "MainWindow.xaml"));
        // 注释里会说明「为何不能内嵌该视图」，故比较前先去掉 XML 注释。
        var markup = XamlCommentPattern.Replace(shellXaml, string.Empty);

        Assert.Contains(
            "prism:RegionManager.RegionName=\"FlowEditorRegion\"",
            markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain("FlowChartContainerView", markup, StringComparison.Ordinal);
    }

    /// <summary>XAML 注释块，用于在静态断言前剔除说明文字。</summary>
    private static readonly System.Text.RegularExpressions.Regex XamlCommentPattern =
        new("<!--.*?-->", System.Text.RegularExpressions.RegexOptions.Singleline);

    /// <summary>宿主工程保持 WinExe + WPF，只引用生产工程，不引 FlowNodes 与测试工程。</summary>
    [Fact]
    public void HostProject_StaysWinExeWpfAndFreeOfFlowNodesAndTestProjects()
    {
        var csproj = XDocument.Parse(File.ReadAllText(
            Path.Combine(WorkbenchDirectory, "FlowEditor.Workbench.csproj")));
        var references = csproj.Descendants("ProjectReference")
            .Select(element => ((string?)element.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .ToArray();

        Assert.Equal("WinExe", Value(csproj, "OutputType"));
        Assert.Equal("true", Value(csproj, "UseWPF"));
        Assert.Contains(references, r => r.EndsWith("FlowEditor.Controls.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.Contains("FlowNodes", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.EndsWith(".Tests.csproj", StringComparison.Ordinal) ||
                                              r.EndsWith(".Test.csproj", StringComparison.Ordinal));
    }

    /// <summary>测试线显式引用宿主工程，保证装配入口被真实编译引用而非仅靠传递依赖。</summary>
    [Fact]
    public void WorkbenchTests_ReferenceHostProjectDirectly()
    {
        var csproj = File.ReadAllText(Path.Combine(
            SolutionDirectory,
            "Tests", "FlowEditor.Workbench.Tests",
            "FlowEditor.Workbench.Tests.csproj"));

        Assert.Contains("..\\FlowEditor.Workbench\\FlowEditor.Workbench.csproj", csproj, StringComparison.Ordinal);
    }

    /// <summary>读取 csproj 中首个同名元素的值。</summary>
    /// <param name="csproj">已解析的 csproj 文档。</param>
    /// <param name="name">元素名。</param>
    /// <returns>元素值（去空白），不存在时返回空字符串。</returns>
    private static string Value(XDocument csproj, string name)
        => csproj.Descendants(name).FirstOrDefault()?.Value.Trim() ?? string.Empty;

    /// <summary>从测试输出目录向上查找包含 FlowEditor.sln 的解决方案目录。</summary>
    /// <returns>FlowEditor 解决方案目录。</returns>
    /// <exception cref="DirectoryNotFoundException">无法定位 FlowEditor.sln 时抛出。</exception>
    private static string FindSolutionDirectory()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "FlowEditor.sln")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位 FlowEditor 解决方案目录。");
    }
}
