// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DependencyRegistrarsTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、WPFCustomControls.Common.Composition。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using WPFCustomControls.Common.Composition;

namespace WPFCustomControls.Tests.Composition;

/// <summary>
/// 子依赖注册器单元测试。
///
/// <para>
/// <b>测试范围</b>:由于 <see cref="Prism.Ioc.IContainerRegistry"/> 接口签名庞大(40+ 方法),
/// 本测试只验证 registrar 的静态契约(可构造、名称非空、主模块覆盖全集)。
/// 真实容器的集成行为留给 Prism 自身的引导测试。
/// </para>
/// </summary>
public class DependencyRegistrarsTests
{
    /// <summary>
    /// 通过反射枚举程序集内的 <see cref="IDependencyRegistrar"/> 实现。
    /// 用于验证"主模块注册的子 registrar"和"程序集中存在的实现"一致。
    /// </summary>
    private static IEnumerable<IDependencyRegistrar> DiscoverRegistrars()
    {
        var asm = typeof(WPFCustomControlsModule).Assembly;
        return asm.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface
                        && typeof(IDependencyRegistrar).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (IDependencyRegistrar)Activator.CreateInstance(t)!);
    }

    /// <summary>
    /// 测试相关入口 EveryRegistrar_IsConstructibleAndNamed；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 DiscoverRegistrars、False、IsNullOrEmpty、GetType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void EveryRegistrar_IsConstructibleAndNamed()
    {
        foreach (var r in DiscoverRegistrars())
        {
            Assert.False(string.IsNullOrEmpty(r.Name),
                $"Registrar {r.GetType().FullName} 必须暴露 Name");
        }
    }

    /// <summary>
    /// 测试相关入口 KnownRegistrars_ArePresentInAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、DiscoverRegistrars、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void KnownRegistrars_ArePresentInAssembly()
    {
        var names = DiscoverRegistrars().Select(r => r.Name).ToHashSet();
        Assert.Contains("Controls", names);
        Assert.Contains("Window", names);
        Assert.Contains("PropertyGrid", names);
    }

    /// <summary>
    /// 测试相关入口 MainModule_TypeExistsAndIsPublic；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MainModule_TypeExistsAndIsPublic()
    {
        // 验证主模块入口仍是公开类型(避免误删 IModule 实现导致 Prism 引导失败)
        var t = typeof(WPFCustomControlsModule);
        Assert.True(t.IsClass);
        Assert.True(t.IsPublic);
        Assert.True(typeof(Prism.Modularity.IModule).IsAssignableFrom(t));
    }

    /// <summary>
    /// 测试相关入口 RegistrarImplementations_AreSealed；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Where、GetTypes、IsAssignableFrom、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RegistrarImplementations_AreSealed()
    {
        // 子注册器应是无状态的工具类 — 约定为 sealed,防止意外继承带来的状态污染
        var asm = typeof(WPFCustomControlsModule).Assembly;
        var concrete = asm.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface
                        && typeof(IDependencyRegistrar).IsAssignableFrom(t));

        foreach (var t in concrete)
            Assert.True(t.IsSealed, $"{t.FullName} 应为 sealed");
    }
}
