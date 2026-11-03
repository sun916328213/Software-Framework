using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Extension.Core.Collections;
using Extension.Core.Conversion;
using Extension.Security.Licensing;
using Xunit;

namespace Extension.Core.Tests;

/// <summary>
/// 当前工作区的架构基线。历史迁移清单不可用，不能再验证历史文件哈希或完整的原始类型清单。
/// </summary>
public sealed class AssemblyBoundaryTests
{
    private static string Root([CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", "..", "..", ".."));

    [Fact]
    public void CoreTargetsNet9WithoutPackageOrDesktopFrameworkDependencies()
    {
        var project = XDocument.Load(Path.Combine(Root(), "Software Frame/Extension/Extension.Core/Extension.Core.csproj"));
        Assert.Equal("net9.0", project.Descendants("TargetFramework").Single().Value);
        foreach (var element in new[] { "PackageReference", "FrameworkReference", "UseWPF", "UseWindowsForms" })
            Assert.Empty(project.Descendants(element));

        // 只允许明确审核过的授权核心引用；在拆分前此列表为空。
        var projectReferences = project.Descendants("ProjectReference")
            .Select(x => Path.GetFileName(x.Attribute("Include")!.Value.Replace('\\', '/')));
        Assert.All(projectReferences, name => Assert.Equal("Licensing.Core.csproj", name));
        var dependencies = typeof(ConcurrentList<>).Assembly.GetReferencedAssemblies().Select(name => name.Name!);
        Assert.All(dependencies, name => Assert.True(
            name is "mscorlib" or "netstandard" or "Microsoft.Win32.Primitives" or "Microsoft.Win32.Registry" or "Licensing.Core" ||
            name.StartsWith("System.", StringComparison.Ordinal), name));
    }

    [Fact]
    public void FoundationPublicTypesRemainResolvable()
    {
        var core = typeof(ConcurrentList<>).Assembly;
        Assert.Equal("Extension.Core", core.GetName().Name);
        Assert.NotNull(core.GetType("Extension.Core.Collections.ConcurrentList`1"));
        Assert.NotNull(typeof(LicenseInfo).Assembly.GetType("Extension.Security.Licensing.LicenseInfo"));
        Assert.NotNull(typeof(LicenseManagerEx).Assembly.GetType("Extension.Security.Licensing.LicenseManagerEx"));
        Assert.NotNull(typeof(MachineCodeEx).Assembly.GetType("Extension.Security.Licensing.MachineCodeEx"));
    }

    [Fact]
    public void PreviousAssemblyNamesResolveThroughTypeForwarders()
    {
        Assert.Equal("Licensing.Core", typeof(LicenseInfo).Assembly.GetName().Name);
        Assert.Same(typeof(LicenseInfo), Type.GetType("Extension.Security.Licensing.LicenseInfo, Extension.Core", throwOnError: true));
        Assert.Same(typeof(LicenseManagerEx), Type.GetType("Extension.Security.Licensing.LicenseManagerEx, Extension.Core", throwOnError: true));
        Assert.Same(typeof(MachineCodeEx), Type.GetType("Extension.Security.Licensing.MachineCodeEx, Extension.Core", throwOnError: true));
    }

    [Fact]
    public void FoundationWorksInAProcessWithoutExtensionOrDesktopAssemblies()
    {
        using var values = new ConcurrentList<int>(new[] { 3, 5, 8 });
        Assert.Equal(new[] { 3, 5, 8 }, values.ToJson().FromJson<int[]>());
        var forbidden = new[] { "Extension", "PresentationFramework", "PresentationCore", "WindowsBase", "Prism", "halcondotnet", "OpenCvSharp", "MaterialDesignThemes.Wpf" };
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => forbidden.Contains(assembly.GetName().Name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingOldAssemblyDoesNotFallBackToCore()
    {
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Extension");
        Assert.Null(Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(
            "Extension.Core.Collections.ConcurrentList`1[[System.Int32, System.Private.CoreLib]], Extension"));
        Assert.Same(typeof(ConcurrentList<int>), Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(typeof(ConcurrentList<int>).AssemblyQualifiedName));
    }
}
