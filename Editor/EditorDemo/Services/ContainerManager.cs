using Prism.Ioc;

namespace EditorDemo.Services;

public static class ContainerManager
{
    private static IContainerProvider? _container;

    public static void Initialize(IContainerProvider container)
    {
        _container = container;
    }

    public static T Resolve<T>()
    {
        if (_container == null)
            throw new InvalidOperationException("ContainerManager 尚未初始化，请在 App.OnInitialized 中调用 ContainerManager.Initialize(Container)");
        return _container.Resolve<T>();
    }

    public static object Resolve(Type type)
    {
        if (_container == null)
            throw new InvalidOperationException("ContainerManager 尚未初始化");
        return _container.Resolve(type);
    }

    public static bool IsInitialized => _container != null;
}
