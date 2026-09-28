using System.Xml.Linq;
using Xunit;

namespace CommunicationNew.Tests.Architecture;

/// <summary>
/// P11 物理分层守卫：校验各 csproj 的 ProjectReference / PackageReference 归属，
/// 把“Core 不引用 Runtime、Controls 不引用 Runtime、Workbench 组合 Core+Runtime+Controls”等约束变成编译期测试。
/// </summary>
public sealed class PhysicalLayeringTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Frame.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "未找到仓库根目录（Frame.sln）。");
        return dir.FullName;
    }

    private static XDocument LoadCsproj(params string[] relativeParts)
    {
        var path = Path.Combine([RepoRoot(), .. relativeParts]);
        Assert.True(File.Exists(path), $"缺少工程文件：{path}");
        return XDocument.Load(path);
    }

    private static HashSet<string> ProjectReferences(XDocument csproj) =>
        csproj.Descendants("ProjectReference")
            .Select(e => (e.Attribute("Include")?.Value ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> PackageReferences(XDocument csproj) =>
        csproj.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string TargetFramework(XDocument csproj) =>
        csproj.Descendants("TargetFramework").FirstOrDefault()?.Value ?? string.Empty;

    [Fact]
    public void Core_targets_pure_net9_and_references_no_runtime_or_sdk()
    {
        var csproj = LoadCsproj("Communication", "Communication.Core", "Communication.Core.csproj");
        Assert.Equal("net9.0", TargetFramework(csproj));

        var projects = ProjectReferences(csproj);
        Assert.DoesNotContain(projects, r => r.Contains("Communication.Runtime"));
        Assert.DoesNotContain(projects, r => r.Contains("Communication.Controls"));
        Assert.DoesNotContain(projects, r => r.EndsWith("Communication.csproj", StringComparison.OrdinalIgnoreCase));

        var packages = PackageReferences(csproj);
        Assert.DoesNotContain("FluentModbus", packages);
        Assert.DoesNotContain("OPCFoundation.NetStandard.Opc.Ua", packages);
        Assert.DoesNotContain("Mitsubishi.MxComponent.Interop", packages);
        Assert.DoesNotContain("Prism.Core", packages);
        Assert.DoesNotContain("System.IO.Pipelines", packages);
    }

    [Fact]
    public void Runtime_targets_pure_net9_and_references_only_core()
    {
        var csproj = LoadCsproj("Communication", "Communication.Runtime", "Communication.Runtime.csproj");
        Assert.Equal("net9.0", TargetFramework(csproj));

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Core"));
        Assert.DoesNotContain(projects, r => r.Contains("Communication.Controls"));
        Assert.DoesNotContain(projects, r => r.EndsWith($"Communication{Path.DirectorySeparatorChar}Communication.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Controls_is_core_only()
    {
        var csproj = LoadCsproj("Communication", "Communication.Controls", "Communication.Controls.csproj");
        var tfm = TargetFramework(csproj);
        Assert.StartsWith("net9.0-windows", tfm);

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Core"));
        Assert.DoesNotContain(projects, r => r.Contains("Communication.Runtime"));
        Assert.DoesNotContain(projects, r => r.EndsWith($"Communication{Path.DirectorySeparatorChar}Communication.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Workbench_composes_core_runtime_and_controls()
    {
        var csproj = LoadCsproj("Communication", "Communication.Workbench", "Communication.Workbench.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Core"));
        Assert.Contains(projects, r => r.Contains("Communication.Runtime"));
        Assert.Contains(projects, r => r.Contains("Communication.Controls"));
    }

    [Fact]
    public void Facade_keeps_package_identity_and_references_new_layers()
    {
        var csproj = LoadCsproj("Communication", "Communication", "Communication.csproj");
        Assert.Equal("CommunicationNew", csproj.Descendants("AssemblyName").FirstOrDefault()?.Value);
        Assert.Equal("CommunicationNew", csproj.Descendants("PackageId").FirstOrDefault()?.Value);

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Core"));
        Assert.Contains(projects, r => r.Contains("Communication.Runtime"));

        var packages = PackageReferences(csproj);
        Assert.DoesNotContain("FluentModbus", packages);
        Assert.DoesNotContain("OPCFoundation.NetStandard.Opc.Ua", packages);
        Assert.DoesNotContain("Mitsubishi.MxComponent.Interop", packages);
        Assert.DoesNotContain("Prism.Core", packages);
    }

    [Fact]
    public void Device_runtime_references_new_runtime_layer()
    {
        var csproj = LoadCsproj("Device", "Device.Runtime", "Device.Runtime.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Runtime"));
    }
}
