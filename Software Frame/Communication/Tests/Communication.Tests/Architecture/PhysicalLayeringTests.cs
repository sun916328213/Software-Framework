// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PhysicalLayeringTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Xml.Linq、Xunit。导入不代表所有类型都会在运行时加载。
using System.Xml.Linq;
using Xunit;

namespace Communication.Tests.Architecture;

/// <summary>
/// P11 物理分层守卫：校验各 csproj 的 ProjectReference / PackageReference 归属，
/// 把“Core 不引用 Runtime、Controls 不引用 Runtime、Workbench 组合 Core+Runtime+Controls”等约束变成编译期测试。
/// </summary>
public sealed class PhysicalLayeringTests
{
    /// <summary>
    /// 测试相关入口 RepoRoot；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine、True；具体条件和先后次序见方法体。
    /// </summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DataManager", "DataManager.Core", "DataManager.Core.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "未找到仓库根目录（DataManager/DataManager.Core/DataManager.Core.csproj）。");
        return dir.FullName;
    }

    /// <summary>
    /// 测试相关入口 LoadCsproj；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、RepoRoot、True、Exists、Load；具体条件和先后次序见方法体。
    /// </summary>
    private static XDocument LoadCsproj(params string[] relativeParts)
    {
        var path = Path.Combine([RepoRoot(), .. relativeParts]);
        Assert.True(File.Exists(path), $"缺少工程文件：{path}");
        return XDocument.Load(path);
    }

    /// <summary>
    /// 测试相关入口 ProjectReferences；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、Descendants、Replace、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    private static HashSet<string> ProjectReferences(XDocument csproj) =>
        csproj.Descendants("ProjectReference")
            .Select(e => (e.Attribute("Include")?.Value ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 测试相关入口 PackageReferences；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、Descendants、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    private static HashSet<string> PackageReferences(XDocument csproj) =>
        csproj.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 测试相关入口 TargetFramework；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FirstOrDefault、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static string TargetFramework(XDocument csproj) =>
        csproj.Descendants("TargetFramework").FirstOrDefault()?.Value ?? string.Empty;

    /// <summary>
    /// 测试相关入口 Core_targets_pure_net9_and_references_no_runtime_or_sdk；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、Equal、TargetFramework、ProjectReferences、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Runtime_targets_pure_net9_and_references_only_core；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、Equal、TargetFramework、ProjectReferences、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Controls_composes_core_and_runtime；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、TargetFramework、StartsWith、ProjectReferences、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Controls_composes_core_and_runtime()
    {
        var csproj = LoadCsproj("Communication", "Communication.Controls", "Communication.Controls.csproj");
        var tfm = TargetFramework(csproj);
        Assert.StartsWith("net9.0-windows", tfm);

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Core"));
        Assert.Contains(projects, r => r.Contains("Communication.Runtime"));
        Assert.DoesNotContain(projects, r => r.EndsWith($"Communication{Path.DirectorySeparatorChar}Communication.csproj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 测试相关入口 Workbench_composes_core_runtime_and_controls；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、ProjectReferences、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Workbench_composes_core_runtime_and_controls()
    {
        var csproj = LoadCsproj("Communication", "Communication.Workbench", "Communication.Workbench.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Core"));
        Assert.Contains(projects, r => r.Contains("Communication.Runtime"));
        Assert.Contains(projects, r => r.Contains("Communication.Controls"));
    }

    /// <summary>
    /// 测试相关入口 Compat_facade_project_is_removed；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、RepoRoot、False、Exists；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Compat_facade_project_is_removed()
    {
        // 2026-09-29：兼容门面 Communication/Communication（AssemblyName/PackageId=CommunicationNew）
        // 已删除；消费者改为直接引用 Communication.Core / Communication.Runtime。
        // 保留此断言防止门面被重新引入（架构清晰前不再考虑兼容层）。
        var path = Path.Combine([RepoRoot(), "Communication", "Communication", "Communication.csproj"]);
        Assert.False(File.Exists(path), $"兼容门面不应重新引入：{path}");
    }

    /// <summary>
    /// 测试相关入口 Device_runtime_references_new_runtime_layer；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、ProjectReferences、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Device_runtime_references_new_runtime_layer()
    {
        var csproj = LoadCsproj("Device", "Device.Runtime", "Device.Runtime.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("Communication.Runtime"));
    }
}
