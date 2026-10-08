// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：P3NamingGovernanceTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Reflection、System.Text.RegularExpressions、System.Xml.Linq、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// P3 验收：命名与目录治理。锁定已统一的命名（Tests/Smoke），并阻止新的分层违规
/// （Infrastructure 大杂烩、Controls 反向依赖、样板节点丢稳定标识）。
/// </summary>
[Collection("NodeTypeRegistry")]
public sealed class P3NamingGovernanceTests
{
    /// <summary>
    /// 测试相关入口 FlowEditorDirectory；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine；具体条件和先后次序见方法体。
    /// </summary>
    private static string FlowEditorDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FlowEditor.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("无法定位 FlowEditor 解决方案目录。");
    }

    /// <summary>
    /// 测试相关入口 WorkspaceDirectory；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetParent、FlowEditorDirectory；具体条件和先后次序见方法体。
    /// </summary>
    private static string WorkspaceDirectory() =>
        Directory.GetParent(FlowEditorDirectory())?.FullName
        ?? throw new DirectoryNotFoundException("无法定位工作区目录。");

    /// <summary>
    /// 测试相关入口 SlnProjectPaths；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ReadAllText、ToList、Select、Matches；具体条件和先后次序见方法体。
    /// </summary>
    private static IReadOnlyList<string> SlnProjectPaths(string slnPath)
    {
        var source = File.ReadAllText(slnPath);
        return Regex.Matches(source, "Project\\([^\r\n]+?= [^\r\n]+?, \\\"(?<path>[^\\\"]+\\.csproj)\\\"")
            .Select(m => m.Groups["path"].Value)
            .ToList();
    }

    /// <summary>
    /// 测试相关入口 TestProjects_Naming_Matches_RepoConvention；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FlowEditorDirectory、Combine、True、Exists、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TestProjects_Naming_Matches_RepoConvention()
    {
        var solutionDirectory = FlowEditorDirectory();
        var workbenchTestsCsproj = Path.Combine(solutionDirectory, "Tests", "FlowEditor.Workbench.Tests", "FlowEditor.Workbench.Tests.csproj");
        var logicTestsCsproj = Path.Combine(solutionDirectory, "Tests", "FlowEditor.Tests", "FlowEditor.Tests.csproj");
        Assert.True(File.Exists(workbenchTestsCsproj), $"测试项目路径不存在: {workbenchTestsCsproj}");
        Assert.True(File.Exists(logicTestsCsproj), $"逻辑测试项目路径不存在: {logicTestsCsproj}");
        // 架构只认两条线（Xxx.Tests + Xxx.Workbench.Tests）：碎片工程与旧命名不得回潮。
        Assert.False(Directory.Exists(Path.Combine(solutionDirectory, "FlowEditor.Test")));
        Assert.False(Directory.Exists(Path.Combine(solutionDirectory, "FlowEditor.Runtime.Tests")));
        Assert.False(Directory.Exists(Path.Combine(solutionDirectory, "FlowEditor.Runtime.Smoke")));
        Assert.False(File.Exists(Path.Combine(solutionDirectory, "FlowEditor.Tests", "FlowEditor.Test.csproj")));
    }

    /// <summary>集中维护的架构文档必须存在，并可从模块解决方案访问。</summary>
    [Fact]
    public void ArchitectureDocuments_ReferenceCentralDocumentation()
    {
        var solutionDirectory = FlowEditorDirectory();
        var solution = File.ReadAllText(Path.Combine(solutionDirectory, "FlowEditor.sln"));
        const string document = @"..\..\Documentation\Refactoring\Software-Architecture-Analysis-and-Refactoring-Plan-2026-10-03.md";
        Assert.True(File.Exists(Path.GetFullPath(document, solutionDirectory)), $"架构文档缺失: {document}");
        Assert.Contains($"\t\t{document} = {document}", solution, StringComparison.Ordinal);
        foreach (Match section in Regex.Matches(solution, @"ProjectSection\(SolutionItems\)[^\r\n]*\r?\n(?<items>[\s\S]*?)EndProjectSection"))
            foreach (Match item in Regex.Matches(section.Groups["items"].Value, @"(?m)^[ \t]*[^\r\n=]+ = (?<path>[^\r\n]+)"))
                Assert.True(File.Exists(Path.GetFullPath(item.Groups["path"].Value.Trim(), solutionDirectory)),
                    $"解决方案文档引用不存在: {item.Groups["path"].Value}");
    }

    /// <summary>
    /// 测试相关入口 Solutions_ReferenceExistingProjects_AfterRename；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、FlowEditorDirectory、WorkspaceDirectory、GetDirectoryName、SlnProjectPaths；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Solutions_ReferenceExistingProjects_AfterRename()
    {
        foreach (var sln in new[]
                 {
                     Path.Combine(FlowEditorDirectory(), "FlowEditor.sln"),
                     Path.Combine(WorkspaceDirectory(), "..", "Software-Framework.sln")
                 })
        {
            var baseDir = Path.GetDirectoryName(sln)!;
            foreach (var relative in SlnProjectPaths(sln))
                Assert.True(File.Exists(Path.GetFullPath(relative, baseDir)), $"{sln} 引用了不存在的项目: {relative}");
            var source = File.ReadAllText(sln);
            Assert.DoesNotContain("FlowEditor.Test\\", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FlowEditor.Runtime.Test.csproj", source, StringComparison.Ordinal);
        }
    }

    /// <summary>WPFControls 直接使用脚本控件时必须引用现存的 Controls 项目。</summary>
    [Fact]
    public void WpfHost_ReferencesCurrentScriptEditorControlsProject()
    {
        var expected = @"..\..\ScriptEditor\ScriptEditor.Controls\ScriptEditor.Controls.csproj";
        var project = Path.Combine(WorkspaceDirectory(), "WPFControls", "WPFControls", "WPFControls.csproj");
        var references = XDocument.Load(project)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .ToList();
        Assert.Contains(expected, references);
        Assert.True(File.Exists(Path.GetFullPath(expected, Path.GetDirectoryName(project)!)));
    }

    /// <summary>
    /// 测试相关入口 NoInfrastructureCatchAll_InSolutions；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、FlowEditorDirectory、WorkspaceDirectory、DoesNotContain、ReadAllText；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NoInfrastructureCatchAll_InSolutions()
    {
        // 计划红线：不要创建 FlowNodes.Infrastructure 大杂烩；基础设施归属具体领域插件或底层组件。
        foreach (var sln in new[]
                 {
                     Path.Combine(FlowEditorDirectory(), "FlowEditor.sln"),
                     Path.Combine(WorkspaceDirectory(), "..", "Software-Framework.sln")
                 })
        {
            Assert.DoesNotContain("FlowNodes.Infrastructure", File.ReadAllText(sln), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 测试相关入口 Controls_MayDependOnRuntime_ButNotReverse；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FlowEditorDirectory、ReadAllText、Combine、Contains、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Controls_MayDependOnRuntime_ButNotReverse()
    {
        // 分层红线：Controls/Editor 可以依赖 Runtime；Runtime 不能反向依赖 Controls。
        var solutionDirectory = FlowEditorDirectory();
        var wpfProject = File.ReadAllText(Path.Combine(solutionDirectory, "FlowEditor.Controls", "FlowEditor.Controls.csproj"));
        var runtimeProject = File.ReadAllText(Path.Combine(solutionDirectory, "FlowEditor.Runtime", "FlowEditor.Runtime.csproj"));
        Assert.Contains("FlowEditor.Runtime.csproj", wpfProject, StringComparison.Ordinal);
        Assert.DoesNotContain("FlowEditor.Controls.csproj", runtimeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWPF", runtimeProject, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 SampleNodes_CarryStableIds；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetCustomAttribute、NotNull、False、IsNullOrWhiteSpace、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SampleNodes_CarryStableIds()
    {
        // P2 样板节点的稳定标识不得丢失（否则旧流程兼容链断裂）。
        try
        {
            foreach (var type in new[]
                     {
                         typeof(FlowEditor.Controls.Nodes.FlowControl.LoopNode),
                         typeof(FlowEditor.Controls.Nodes.FlowControl.ConditionNode)
                     })
            {
                var attr = type.GetCustomAttribute<NodeTypeIdAttribute>();
                Assert.NotNull(attr);
                Assert.False(string.IsNullOrWhiteSpace(attr.TypeId));
                Assert.Equal(attr.TypeId, NodeTypeRegistry.Register(type));
                Assert.Same(type, NodeTypeRegistry.TryResolve(attr.TypeId));
            }
        }
        finally
        {
            NodeTypeRegistry.Clear();
        }
    }
}
