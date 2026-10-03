// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：WpfUiDispatcher。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows、System.Windows.Threading、WPFCustomControls.PropertyGrid.Core.Composition。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;
using System.Windows.Threading;
using WPFCustomControls.PropertyGrid.Core.Composition;

namespace WPFCustomControls.PropertyGrid.Wpf;

/// <summary>WPF adapter for the platform-neutral UI dispatch seam.</summary>
/// <remarks>
/// WpfUiDispatcher 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IUiDispatcher。具体约束按这些契约及各成员实现解释。
/// </remarks>
internal sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// 初始化 WpfUiDispatcher 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private WpfUiDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CheckAccess。
    /// 正文中的关键协作调用包括 CheckAccess；具体条件和先后次序见方法体。
    /// </summary>
    public bool CheckAccess() => _dispatcher.CheckAccess();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Post。
    /// 正文中的关键协作调用包括 ThrowIfNull、CheckAccess、action、BeginInvoke；具体条件和先后次序见方法体。
    /// </summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateForCurrentApplication。
    /// </summary>
    public static IUiDispatcher CreateForCurrentApplication() =>
        Application.Current is { } application
            ? new WpfUiDispatcher(application.Dispatcher)
            : NullUiDispatcher.Instance;
}
