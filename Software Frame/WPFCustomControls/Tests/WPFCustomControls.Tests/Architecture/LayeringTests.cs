// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LayeringTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、WPFCustomControls.PropertyGrid.Core.Services、WPFCustomControls.PropertyGrid.Services、WPFCustomControls.PropertyGrid.Core.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using WPFCustomControls.PropertyGrid.Core.Services;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.Tests.Architecture;

/// <summary>
/// LayeringTests 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class LayeringTests
{
    /// <summary>
    /// 测试相关入口 PropertyGridCore_DoesNotReferenceWpfOrPresentationAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、GetReferencedAssemblies、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PropertyGridCore_DoesNotReferenceWpfOrPresentationAssembly()
    {
        var references = typeof(PropertyGridBuilderService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("WPFCustomControls", references);
        Assert.DoesNotContain("PresentationCore", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("WindowsBase", references);
    }

    /// <summary>
    /// 测试相关入口 PropertyGridPresentationModels_AreOwnedByCoreAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、GetName；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PropertyGridPresentationModels_AreOwnedByCoreAssembly()
    {
        Assert.Equal(
            "WPFCustomControls.PropertyGrid.Core",
            typeof(PropertyGridHostViewModel).Assembly.GetName().Name);
    }

    /// <summary>
    /// 测试相关入口 WpfProject_DependsOnPropertyGridCore；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Select、GetReferencedAssemblies、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WpfProject_DependsOnPropertyGridCore()
    {
        var references = typeof(WPFCustomControlsModule).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.Contains("WPFCustomControls.PropertyGrid.Core", references);
    }

    /// <summary>
    /// 测试相关入口 LegacyPropertyGridNamespaceResolvesThroughMigrationAlias；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ResolvePersistedType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LegacyPropertyGridNamespaceResolvesThroughMigrationAlias()
    {
        Assert.Equal(typeof(PropertyGridHostViewModel),
            global::Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(
                "WPFCustomControls.PropertyGrid.ViewModels.PropertyGridHostViewModel, WPFCustomControls"));
    }

    /// <summary>
    /// 测试相关入口 CurrentPropertyGridTypes_AreForwardedToCore；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、GetType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CurrentPropertyGridTypes_AreForwardedToCore()
    {
        var legacyName =
            "WPFCustomControls.PropertyGrid.Core.ViewModels.PropertyGridHostViewModel, WPFCustomControls";

        Assert.Equal(typeof(PropertyGridHostViewModel), Type.GetType(legacyName, throwOnError: true));
    }
}
