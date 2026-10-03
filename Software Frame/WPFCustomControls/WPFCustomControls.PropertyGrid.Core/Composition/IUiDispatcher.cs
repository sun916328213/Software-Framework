// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：IUiDispatcher、UiDispatcher、NullUiDispatcher。成员说明可结合下方具体实现阅读。
namespace WPFCustomControls.PropertyGrid.Core.Composition;

/// <summary>
/// 跨线程 UI 调度抽象。Core 子集零 WPF 依赖,WPF 实现由主项目注册。
/// ViewModel 通过构造函数接收该依赖，安全地从工作线程 marshal 到 UI 线程，
/// 无需直接引用 <c>System.Windows.Application</c>。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>当前线程是否就是 UI 线程（无需 marshal）。</summary>
    bool CheckAccess();

    /// <summary>把指定动作 marshal 到 UI 线程异步执行。</summary>
    void Post(Action action);
}

/// <summary>
/// 旧版进程级调度器入口。新代码应通过构造函数注入 <see cref="IUiDispatcher"/>；
/// 保留该类型仅用于兼容已经编译的调用者。
/// </summary>
public static class UiDispatcher
{
    private static IUiDispatcher _current = new NullUiDispatcher();

    /// <summary>当前注册的调度器,默认 <see cref="NullUiDispatcher"/>（同步执行）。</summary>
    public static IUiDispatcher Current => _current;

    /// <summary>由主项目在启动时注册 WPF 实现；测试代码可不调用。</summary>
    public static void Register(IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _current = dispatcher;
    }

    /// <summary>恢复为 <see cref="NullUiDispatcher"/>，主要用于测试清理。</summary>
    public static void Reset() => _current = new NullUiDispatcher();
}

/// <summary>默认空实现：<see cref="CheckAccess"/> 永远为 true、<see cref="Post"/> 同步执行。
/// 仅供无 UI 主线程的环境（如单元测试）使用。</summary>
public sealed class NullUiDispatcher : IUiDispatcher
{
    public static NullUiDispatcher Instance { get; } = new();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CheckAccess。
    /// </summary>
    public bool CheckAccess() => true;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Post。
    /// 正文中的关键协作调用包括 action；具体条件和先后次序见方法体。
    /// </summary>
    public void Post(Action action) => action();
}
