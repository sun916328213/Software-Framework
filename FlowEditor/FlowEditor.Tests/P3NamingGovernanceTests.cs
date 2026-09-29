using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FlowEditor.Core;
using Xunit;

namespace FlowEditorTest;

/// <summary>
/// P3 验收：命名与目录治理。锁定已统一的命名（Tests/Smoke），并阻止新的分层违规
/// （Infrastructure 大杂烩、Controls 反向依赖、样板节点丢稳定标识）。
/// </summary>
public sealed class P3NamingGovernanceTests
{
    private static string FlowEditorDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FlowEditor.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("无法定位 FlowEditor 解决方案目录。");
    }

    private static string WorkspaceDirectory() =>
        Directory.GetParent(FlowEditorDirectory())?.FullName
        ?? throw new DirectoryNotFoundException("无法定位工作区目录。");

    private static IReadOnlyList<string> SlnProjectPaths(string slnPath)
    {
        var source = File.ReadAllText(slnPath);
        return Regex.Matches(source, "Project\\([^\r\n]+?= [^\r\n]+?, \\\"(?<path>[^\\\"]+\\.csproj)\\\"")
            .Select(m => m.Groups["path"].Value)
            .ToList();
    }

    [Fact]
    public void TestAndSmoke_Naming_Matches_RepoConvention()
    {
        var solutionDirectory = FlowEditorDirectory();
        var testCsproj = Path.Combine(solutionDirectory, "FlowEditor.Tests", "FlowEditor.Tests.csproj");
        var smokeCsproj = Path.Combine(solutionDirectory, "FlowEditor.Runtime.Smoke", "FlowEditor.Runtime.Smoke.csproj");
        Assert.True(File.Exists(testCsproj), $"测试项目路径不存在: {testCsproj}");
        Assert.True(File.Exists(smokeCsproj), $"冒烟项目路径不存在: {smokeCsproj}");
        // 旧的不一致命名不得回潮。
        Assert.False(Directory.Exists(Path.Combine(solutionDirectory, "FlowEditor.Test")));
        Assert.False(File.Exists(Path.Combine(solutionDirectory, "FlowEditor.Runtime.Smoke", "FlowEditor.Runtime.Test.csproj")));
    }

    [Fact]
    public void Solutions_ReferenceExistingProjects_AfterRename()
    {
        foreach (var sln in new[]
                 {
                     Path.Combine(FlowEditorDirectory(), "FlowEditor.sln"),
                     Path.Combine(WorkspaceDirectory(), "Frame.sln")
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

    [Fact]
    public void NoInfrastructureCatchAll_InSolutions()
    {
        // 计划红线：不要创建 FlowNodes.Infrastructure 大杂烩；基础设施归属具体领域插件或底层组件。
        foreach (var sln in new[]
                 {
                     Path.Combine(FlowEditorDirectory(), "FlowEditor.sln"),
                     Path.Combine(WorkspaceDirectory(), "Frame.sln")
                 })
        {
            Assert.DoesNotContain("Infrastructure", File.ReadAllText(sln), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Controls_MayDependOnRuntime_ButNotReverse()
    {
        // 分层红线：Controls/Editor 可以依赖 Runtime；Runtime 不能反向依赖 Controls。
        var solutionDirectory = FlowEditorDirectory();
        var wpfProject = File.ReadAllText(Path.Combine(solutionDirectory, "FlowEditor", "FlowEditor.csproj"));
        var runtimeProject = File.ReadAllText(Path.Combine(solutionDirectory, "FlowEditor.Runtime", "FlowEditor.Runtime.csproj"));
        Assert.Contains("FlowEditor.Runtime.csproj", wpfProject, StringComparison.Ordinal);
        Assert.DoesNotContain("FlowEditor.csproj", runtimeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWPF", runtimeProject, StringComparison.Ordinal);
    }

    [Fact]
    public void SampleNodes_CarryStableIds()
    {
        // P2 样板节点的稳定标识不得丢失（否则旧流程兼容链断裂）。
        try
        {
            foreach (var type in new[]
                     {
                         typeof(FlowEditor.Nodes.FlowControl.LoopNode),
                         typeof(FlowEditor.Nodes.FlowControl.ConditionNode)
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
