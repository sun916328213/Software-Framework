// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AssemblyBoundaryTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、System.Runtime.CompilerServices、System.Security.Cryptography、System.Text.Json、System.Xml.Linq、Extension.Core.Collections。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Extension.Core.Collections;
using Extension.Core.Conversion;
using Xunit;

namespace Extension.Core.Tests;

/// <summary>
/// AssemblyBoundaryTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class AssemblyBoundaryTests
{
    /// <summary>
    /// 验证 Root 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetFullPath、Combine、GetDirectoryName；具体条件和先后次序见方法体。
    /// </summary>
    private static string Root([CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", "..", "..", ".."));

    /// <summary>
    /// 验证 Manifest 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、ReadAllText、Combine、Root、Clone；具体条件和先后次序见方法体。
    /// </summary>
    private static JsonElement Manifest()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Build/Baselines/Architecture-S5-migrations.json")));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 验证 CoreBuildsWithoutPackageProjectOrDesktopDependencies 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Load、Combine、Root、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CoreBuildsWithoutPackageProjectOrDesktopDependencies()
    {
        var project = XDocument.Load(Path.Combine(Root(), "Software Frame/Extension/Extension.Core/Extension.Core.csproj"));
        Assert.Equal("net9.0", project.Descendants("TargetFramework").Single().Value);
        foreach (var element in new[] { "PackageReference", "ProjectReference", "FrameworkReference", "UseWPF", "UseWindowsForms" })
            Assert.Empty(project.Descendants(element));
        var dependencies = typeof(ConcurrentList<>).Assembly.GetReferencedAssemblies().Select(name => name.Name!);
        Assert.All(dependencies, name => Assert.True(name == "mscorlib" || name == "netstandard" || name == "Microsoft.Win32.Primitives" || name.StartsWith("System.", StringComparison.Ordinal), name));
    }

    /// <summary>
    /// 验证 AllExtractedPublicNamesAreOwnedByCore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Order、Select、EnumerateArray、GetProperty、Manifest；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AllExtractedPublicNamesAreOwnedByCore()
    {
        var expected = Manifest().GetProperty("movedPublicTypes").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal);
        var actual = typeof(ConcurrentList<>).Assembly.GetExportedTypes().Select(type => type.FullName!).Order(StringComparer.Ordinal);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// 验证 ExtractedFilesMatchReviewedHashesAndOldFilesAreAbsent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、ReadAllText、Combine、Root、EnumerateArray；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ExtractedFilesMatchReviewedHashesAndOldFilesAreAbsent()
    {
        using var consolidation = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Build/Baselines/WPFControls-Consolidation-migrations.json")));
        foreach (var move in Manifest().GetProperty("fileMoves").EnumerateArray())
        {
            Assert.False(File.Exists(Path.Combine(Root(), move.GetProperty("oldPath").GetString()!)));
            var bytes = File.ReadAllBytes(Path.Combine(Root(), move.GetProperty("newPath").GetString()!));
            var expected = move.TryGetProperty("newSha256", out var updated) ? updated.GetString() : move.GetProperty("sha256").GetString();
            var path = move.GetProperty("newPath").GetString();
            foreach (var change in consolidation.RootElement.GetProperty("reviewedSourceChanges").EnumerateArray())
                if (change.GetProperty("path").GetString() == path)
                {
                    Assert.Equal(expected, change.GetProperty("beforeSha256").GetString());
                    expected = change.GetProperty("afterSha256").GetString();
                }
            Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    /// <summary>
    /// 验证 FoundationWorksInAProcessWithoutExtensionOrDesktopAssemblies 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、FromJson、ToJson、DoesNotContain、GetAssemblies；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FoundationWorksInAProcessWithoutExtensionOrDesktopAssemblies()
    {
        using var values = new ConcurrentList<int>(new[] { 3, 5, 8 });
        Assert.Equal(new[] { 3, 5, 8 }, values.ToJson().FromJson<int[]>());
        var forbidden = new[] { "Extension", "PresentationFramework", "PresentationCore", "WindowsBase", "Prism", "halcondotnet", "OpenCvSharp", "MaterialDesignThemes.Wpf" };
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => forbidden.Contains(assembly.GetName().Name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证 FoundationConsumersReferenceCoreDirectly 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Where、EnumerateArray、GetProperty、Manifest、GetBoolean；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FoundationConsumersReferenceCoreDirectly()
    {
        foreach (var entry in Manifest().GetProperty("changedProjects").EnumerateArray().Where(value => value.GetProperty("coreOnly").GetBoolean()))
        {
            var project = XDocument.Load(Path.Combine(Root(), entry.GetProperty("path").GetString()!));
            var references = project.Descendants("ProjectReference").Select(value => Path.GetFileName(value.Attribute("Include")!.Value.Replace('\\', '/'))).ToArray();
            Assert.Contains("Extension.Core.csproj", references);
            Assert.DoesNotContain("Extension.csproj", references);
        }
    }

    /// <summary>
    /// 验证 MissingOldAssemblyDoesNotFallBackToCore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DoesNotContain、GetAssemblies、GetName、Null、ResolvePersistedType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MissingOldAssemblyDoesNotFallBackToCore()
    {
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Extension");
        Assert.Null(Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(
            "Extension.Core.Collections.ConcurrentList`1[[System.Int32, System.Private.CoreLib]], Extension"));
        Assert.Same(typeof(ConcurrentList<int>), Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(typeof(ConcurrentList<int>).AssemblyQualifiedName));
    }
}
