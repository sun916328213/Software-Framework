// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ControlsDependencyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.RegularExpressions、Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P8 验收守卫 —— Controls 分层（计划 §P8 / §13 检查表「Controls 是否没有被
// Tool 项目反向依赖」+「HALCON 特有显示先以 namespace 组织」）：
//   ① Core / Tools.* / AI.* 一律不得引用 Vision.Controls；
//   ② 新 UI 逻辑（ViewModels/、Presentation/、View/）不得出现视觉 SDK /
//      native / 迁移源 token；旧HALCON原生显示已退役，不再设SDK目录豁免。
// 口径沿用既有守卫：注释先剥、断言配防假绿下限.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.RegularExpressions;
using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>P8 Controls 分层守卫。</summary>
[Trait("Category", "Architecture")]
public sealed class ControlsDependencyTests
{
    private static readonly string[] ToolLayerProjects =
    [
        "Vision.Core",
        "Vision.Tools.Halcon",
        "Vision.Tools.OpenCv",
        "Vision.Tools.AI",
        "Vision.AI.Halcon",
        "Vision.AI.Aiditron",
        "Vision.AI.Invi",
    ];

    private static readonly string[] ForbiddenTokens =
    [
        "HalconDotNet",
        "HObject",
        "HTuple",
        "HWindow",
        "HSmartWindow",
        "OpenCvSharp",
        "MVTec",
        "DPLibrary_Halcon",
        "DllImport",
    ];

    // ── 1. Tool 层不得反向引用 Controls ─────────────────────────────

    [Fact]
    public void ToolLayerProjects_DoNotReferenceControls()
    {
        var offenders = new List<string>();
        var scanned = 0;
        foreach (var project in ToolLayerProjects)
        {
            var csproj = VisionLayout.ProjectFile(VisionDirectory, project);
            Assert.True(File.Exists(csproj), $"工程不存在：{csproj}");
            scanned++;
            var text = Regex.Replace(File.ReadAllText(csproj), "<!--.*?-->", " ", RegexOptions.Singleline);
            var refs = Regex
                .Matches(text, "<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")
                .Select(m => m.Groups["path"].Value);
            foreach (var reference in refs)
            {
                if (reference.Contains("Vision.Controls", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{project} -> {reference}");
            }
        }

        Assert.True(scanned == ToolLayerProjects.Length, "扫描集合疑似被改。");
        Assert.Empty(offenders);
    }

    // ── 2. 新 UI 逻辑无 SDK/nativie/迁移源 token ─────────────────────

    [Fact]
    public void NewUiLogic_ContainsNoSdkOrLegacyTokens()
    {
        var roots = new[] { "ViewModels", "Presentation", "Views", "Contracts" };
        var offenders = new List<string>();
        var total = 0;
        foreach (var root in roots)
        {
            var dir = Path.Combine(VisionDirectory, "Vision.Controls", root);
            Assert.True(Directory.Exists(dir), $"目录不存在：{dir}");
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                total++;
                var code = StripComments(File.ReadAllText(file));
                var relative = Path.GetRelativePath(VisionDirectory, file).Replace('\\', '/');
                foreach (var token in ForbiddenTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                        offenders.Add($"{relative} -> {token}");
                }
            }
        }

        Assert.True(total >= 6, $"只扫到 {total} 个 .cs，扫描疑似失效。");
        Assert.Empty(offenders);
    }

    // ── 3. 非 HALCON 命名空间基线干净 ───────────────────────────────

    [Fact]
    public void NonHalconNamespaces_ContainNoSdkTokens()
    {
        // 扫描整个Controls，仅跳过构建输出；原生SDK边界留在Tools。
        var offenders = new List<string>();
        var total = 0;
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(VisionDirectory, "Vision.Controls"), "*", SearchOption.AllDirectories)
                     .Where(file => file.EndsWith(".cs") || file.EndsWith(".xaml")))
        {
            var relative = Path.GetRelativePath(Path.Combine(VisionDirectory, "Vision.Controls"), file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith(".vs/", StringComparison.OrdinalIgnoreCase))
                continue;
            total++;
            var code = StripComments(File.ReadAllText(file));
            foreach (var token in ForbiddenTokens)
            {
                if (code.Contains(token, StringComparison.Ordinal))
                    offenders.Add($"{relative} -> {token}");
            }
        }

        Assert.True(total >= 10, $"只扫到 {total} 个 .cs，扫描疑似失效。");
        Assert.Empty(offenders);
    }

    [Fact]
    public void ControlsSourceLayout_IsGrouped_AndContainsNoSpaceNamedSourceFiles()
    {
        var root = Path.Combine(VisionDirectory, "Vision.Controls");
        foreach (var folder in new[] { "Views/Flow", "Views/Editor", "Views/PropertyGrid", "Views/Imaging",
            "ViewModels/Editor", "ViewModels/Execution", "Models/Editor", "Services/Projects", "Contracts" })
            Assert.True(Directory.Exists(Path.Combine(root, folder)), $"缺少职责目录：{folder}");
        Assert.False(Directory.Exists(Path.Combine(root, "Compatibility")));
        Assert.True(File.Exists(Path.Combine(root, "Views", "Editor", "ToolEditorView.xaml")));
        Assert.True(File.Exists(Path.Combine(root, "ViewModels", "Execution", "DetectionToolSession.cs")));
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => !file.StartsWith("obj/") && !file.StartsWith("bin/") && !file.StartsWith(".vs/"))
            .Where(file => file.EndsWith(".cs") || file.EndsWith(".xaml")).ToArray();
        Assert.True(files.Length >= 60, "源码扫描疑似失效。");
        Assert.DoesNotContain(files, file => Path.GetFileName(file).Any(char.IsWhiteSpace));
        Assert.DoesNotContain(files, file => file.StartsWith("View/") || file.StartsWith("Ctr_Base/") || file.StartsWith("Ctr_Halcon/") || file.StartsWith("Converter/"));
    }

    [Fact]
    public void ExecutableToolContract_DoesNotContainTheConcreteSdkAdapters()
    {
        var root = Path.Combine(VisionDirectory, "Vision.Controls");
        var contract = StripComments(File.ReadAllText(Path.Combine(root, "Contracts", "IExecutableTool.cs")));
        Assert.Contains("interface IExecutableTool", contract);
        Assert.DoesNotContain("class HalconToolCardAdapter", contract);
        Assert.DoesNotContain("class OpenCvToolCardAdapter", contract);
        Assert.DoesNotContain("class AIToolCardAdapter", contract);
        foreach (var name in new[] { "HalconToolCardAdapter", "OpenCvToolCardAdapter", "AIToolCardAdapter" })
            Assert.True(File.Exists(Path.Combine(root, "Presentation", "Adapters", name + ".cs")));
    }
    [Fact]
    public void EditorToolbar_DeclaresItsButtonsDirectly_AndRetiresTheDuplicateBrowserView()
    {
        var root = Path.Combine(VisionDirectory, "Vision.Controls", "Views");
        System.Xml.Linq.XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var editor = System.Xml.Linq.XDocument.Load(Path.Combine(root, "Editor", "VisionToolEditorView.xaml"));
        var toolbar = editor.Descendants(ui + "Grid").Single(element => (string?)element.Attribute(x + "Name") == "TopMenuBar");
        var buttons = toolbar.Descendants(ui + "Button").ToArray();
        Assert.Equal(new[] { "OpenProjectButton", "SaveProjectButton", "SaveProjectAsButton", "LoadImageButton", "RunButton" },
            buttons.Select(button => (string?)button.Attribute(x + "Name")));
        Assert.All(buttons, button => Assert.Equal(ui + "WrapPanel", button.Parent!.Name));
        Assert.DoesNotContain(editor.Descendants(), element => element.Name == ui + "ContentControl" || element.Name == ui + "DataTemplate");
        Assert.False(File.Exists(Path.Combine(root, "Browser", "VisionBrowserView.xaml")));
        Assert.False(File.Exists(Path.Combine(root, "Browser", "VisionBrowserView.xaml.cs")));
    }

    [Fact]
    public void ImagePresets_AreFixedXamlItems_NotAViewModelGeneratedCollection()
    {
        var path = Path.Combine(VisionDirectory, "Vision.Controls", "Views", "Imaging", "VisionImageToolbarView.xaml");
        System.Xml.Linq.XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var image = System.Xml.Linq.XDocument.Load(path);
        var selector = image.Descendants(ui + "ListBox").Single(element => (string?)element.Attribute(x + "Name") == "ImageViewSelector");
        Assert.Null(selector.Attribute("ItemsSource"));
        Assert.Equal("Tag", (string?)selector.Attribute("SelectedValuePath"));
        Assert.Equal(new[] { "输入图像", "ROI 图像", "结果图像", "训练图像" }, selector.Elements(ui + "ListBoxItem").Select(item => (string?)item.Attribute("Content")));
        var training = selector.Elements(ui + "ListBoxItem").Single(item => (string?)item.Attribute("Content") == "训练图像");
        Assert.Equal("{Binding HasTrainingImage}", (string?)training.Attribute("IsEnabled"));
        Assert.Contains(training.Descendants(ui + "DataTrigger"), trigger => (string?)trigger.Attribute("Binding") == "{Binding SupportsTemplateInput}");
    }

    [Fact]
    public void SharedViewport_DelegatesResultPresentation_WithoutBackendResultBranches()
    {
        var root = Path.Combine(VisionDirectory, "Vision.Controls", "Views", "Imaging");
        foreach (var path in Directory.EnumerateFiles(root, "VisionImageViewport*.cs"))
        {
            var source = StripComments(File.ReadAllText(path));
            Assert.DoesNotContain("Tools.", source);
            Assert.DoesNotContain("Core.AI.", source);
        }
    }

    [Fact]
    public void FlowIntegration_LivesInControls_AndFlowNodeDoesNotDependOnControls()
    {
        var root = Path.Combine(VisionDirectory, "Vision.Controls");
        var actual = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Replace('\\', '/').Contains("/obj/") && !file.Replace('\\', '/').Contains("/bin/"))
            .Where(file => Regex.IsMatch(StripComments(File.ReadAllText(file)), @"\busing\s+FlowEditor\."))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).OrderBy(file => file).ToArray();
        Assert.Equal(new[] { "Services/Composition/VisionFlowRegistration.cs",
            "Services/Execution/VisionNodeEditorHandler.cs", "Services/Execution/VisionTaskToolNode.cs",
            "Services/Execution/VisionToolFlowNode.cs", "ViewModels/Execution/VisionTaskViewModel.cs",
            "ViewModels/Flow/VisionFlowViewModel.cs", "Views/Flow/VisionFlowView.xaml.cs" }, actual);
        var project = File.ReadAllText(Path.Combine(root, "Vision.Controls.csproj"));
        Assert.Contains("Vision.FlowNode", project);
        var nodes = File.ReadAllText(Path.Combine(VisionDirectory, "Vision.FlowNode", "Vision.FlowNode.csproj"));
        Assert.DoesNotContain("Vision.Controls", nodes);
        Assert.DoesNotContain("Vision.Workbench", nodes);
        Assert.DoesNotContain("Vision.Workbench.csproj", project);
    }

    [Fact]
    public void VisionHost_IsThinIndependentShell_AndFlowNodesIsNotTakenOver()
    {
        var host = Path.Combine(VisionDirectory, "Vision.Workbench");
        Assert.True(File.Exists(Path.Combine(host, "Vision.Workbench.csproj")));
        var app = StripComments(File.ReadAllText(Path.Combine(host, "App.xaml.cs")));
        Assert.Contains("RegisterVisionFlow", app);
        Assert.DoesNotContain("NodeParameterNames", app);
        Assert.DoesNotContain("ToolBrowserView", app);
        Assert.DoesNotContain("VisionTaskView", app);
        var project = File.ReadAllText(Path.Combine(host, "Vision.Workbench.csproj"));
        Assert.DoesNotContain("FlowNodes.Workbench", project);
        foreach (var name in new[] { "ToolRegistration.cs", "OpenCvToolCatalog.cs", "ToolBrowserView.xaml", "WorkbenchComposition.cs" })
            Assert.False(File.Exists(Path.Combine(host, name)));
        var flowHost = Path.Combine(RepoRoot, "FlowNodes", "FlowNodes.Workbench");
        Assert.DoesNotContain("RegisterVisionFlow", StripComments(File.ReadAllText(Path.Combine(flowHost, "App.xaml.cs"))));
        var navigation = File.ReadAllText(Path.Combine(flowHost, "ViewModels", "MainWindowViewModel.cs"));
        Assert.Contains("nameof(FlowChartContainerView)", navigation);
        Assert.DoesNotContain("nameof(VisionFlowView)", navigation);
        var definitions = Path.Combine(VisionDirectory, "Vision.FlowNode", "Definitions", "VisionNodeDefinitions.cs");
        Assert.True(File.Exists(definitions));
        Assert.DoesNotContain("Vision.Controls", StripComments(File.ReadAllText(definitions)));
    }

    private static string VisionDirectory => Path.Combine(RepoRoot, "Vision");

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DataManager", "DataManager.Core", "DataManager.Core.csproj")))
                dir = dir.Parent;
            return dir?.FullName
                ?? throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 向上找不到仓库根（DataManager/DataManager.Core/DataManager.Core.csproj）。");
        }
    }

    /// <summary>
    /// 测试相关入口 StripComments；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Replace；具体条件和先后次序见方法体。
    /// </summary>
    private static string StripComments(string code)
    {
        code = Regex.Replace(code, @"<!--.*?-->", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"(^|\s)//.*$", " ", RegexOptions.Multiline);
        return code;
    }
}
