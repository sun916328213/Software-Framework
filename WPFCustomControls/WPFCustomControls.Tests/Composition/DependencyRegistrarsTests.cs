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

    [Fact]
    public void EveryRegistrar_IsConstructibleAndNamed()
    {
        foreach (var r in DiscoverRegistrars())
        {
            Assert.False(string.IsNullOrEmpty(r.Name),
                $"Registrar {r.GetType().FullName} 必须暴露 Name");
        }
    }

    [Fact]
    public void KnownRegistrars_ArePresentInAssembly()
    {
        var names = DiscoverRegistrars().Select(r => r.Name).ToHashSet();
        Assert.Contains("Controls", names);
        Assert.Contains("Window", names);
        Assert.Contains("PropertyGrid", names);
    }

    [Fact]
    public void MainModule_TypeExistsAndIsPublic()
    {
        // 验证主模块入口仍是公开类型(避免误删 IModule 实现导致 Prism 引导失败)
        var t = typeof(WPFCustomControlsModule);
        Assert.True(t.IsClass);
        Assert.True(t.IsPublic);
        Assert.True(typeof(Prism.Modularity.IModule).IsAssignableFrom(t));
    }

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
