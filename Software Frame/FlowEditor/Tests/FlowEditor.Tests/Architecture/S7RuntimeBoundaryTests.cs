// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：S7RuntimeBoundaryTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection.Metadata、System.Reflection.PortableExecutable、System.Xml.Linq、Xunit。导入不代表所有类型都会在运行时加载。
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Xunit;

namespace FlowEditor.Tests.Architecture;

/// <summary>
/// S7RuntimeBoundaryTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class S7RuntimeBoundaryTests
{
    /// <summary>
    /// 测试相关入口 ExecutionRuntimeProjectAndEvaluatedClosureHaveNoDesktopOrNodePresentation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine、Push、TryPop、Add；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("FlowEditor/FlowEditor.Runtime/FlowEditor.Runtime.csproj")]
    [InlineData("ScriptEditor/ScriptEditor.Runtime/ScriptEditor.Runtime.csproj")]
    [InlineData("Communication/Communication.Runtime/Communication.Runtime.csproj")]
    public void ExecutionRuntimeProjectAndEvaluatedClosureHaveNoDesktopOrNodePresentation(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Software-Framework.sln"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Missing repository.");
        var pending = new Stack<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(Path.Combine(root, "Software Frame", relative));
        while (pending.TryPop(out var path))
        {
            if (!seen.Add(path)) continue;
            var project = XDocument.Load(path);
            Assert.DoesNotContain(project.Descendants("UseWPF"), e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(Path.GetFileNameWithoutExtension(path), new[] { "FlowNodes", "Extension", "WPFControls" });
            Assert.DoesNotContain(".Controls", Path.GetFileNameWithoutExtension(path));
            foreach (var reference in project.Descendants("ProjectReference"))
                pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference.Attribute("Include")!.Value)));
            var assetsPath = Path.Combine(Path.GetDirectoryName(path)!, "obj", "project.assets.json");
            using var assets = System.Text.Json.JsonDocument.Parse(File.ReadAllText(assetsPath));
            foreach (var framework in assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
                if (framework.Value.TryGetProperty("frameworkReferences", out var references))
                    Assert.DoesNotContain(references.EnumerateObject(), r => r.Name.StartsWith("Microsoft.WindowsDesktop", StringComparison.Ordinal));
            foreach (var library in assets.RootElement.GetProperty("libraries").EnumerateObject())
                Assert.DoesNotContain(".Controls/", library.Name);
            var configuration = typeof(S7RuntimeBoundaryTests).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
                .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            var dll = Path.Combine(Path.GetDirectoryName(path)!, "bin", configuration, "net9.0", Path.GetFileNameWithoutExtension(path) + ".dll");
            using var stream = File.OpenRead(dll);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var names = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToArray();
            Assert.DoesNotContain(names, n => n.Contains(".Controls", StringComparison.Ordinal) ||
                n is "FlowNodes" or "WPFControls" or "Extension" or "WindowsBase" or "PresentationFramework" or "PresentationCore");
        }
        Assert.True(seen.Count >= 2);
    }

    /// <summary>
    /// 测试相关入口 HeadlessEngineTestsDoNotLoadNodePresentationOrWpf；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 DoesNotContain、GetAssemblies、GetName；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HeadlessEngineTestsDoNotLoadNodePresentationOrWpf()
    {
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a =>
            a.GetName().Name is "FlowNodes" or "FlowEditor.Controls" or "PresentationFramework" or "PresentationCore" or "WindowsBase");
    }
}
