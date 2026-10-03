// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：FeatureViewModelComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Mvvm、WPFControls.ProgramControl.Views。导入不代表所有类型都会在运行时加载。
using Prism.Mvvm;
using WPFControls.ProgramControl.Views;

namespace WPFControls.Composition;

// Public view identities remain stable while their internal ViewModels follow feature namespaces.
/// <summary>
/// FeatureViewModelComposition 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// </summary>
internal static class FeatureViewModelComposition
{
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Register()
    {
        // 页面与 ViewModel 不再总处于同一命名空间，不能完全依赖 Prism 的按名称约定查找。
        // 显式映射只决定页面如何取得展示状态，不改变导航名称，也不在这里创建生产设备。
        ViewModelLocationProvider.Register<AlarmInfoManagerView, AlarmInfoManagerViewModel>();
        ViewModelLocationProvider.Register<AlarmInfoQueryView, AlarmInfoQueryViewModel>();
        ViewModelLocationProvider.Register<ImageQueryView, ImageQueryViewModel>();
        ViewModelLocationProvider.Register<LogQueryView, LogQueryViewModel>();
        ViewModelLocationProvider.Register<SEMILogQueryView, SEMILogQueryViewModel>();
        ViewModelLocationProvider.Register<SEMIManagerView, SEMIManagerViewModel>();
        ViewModelLocationProvider.Register<PrintManagerView, PrintManagerViewModel>();
        ViewModelLocationProvider.Register<PrintView, PrintViewModel>();
        ViewModelLocationProvider.Register<ControlView, ControlViewModel>();
        ViewModelLocationProvider.Register<UserInfoManagerView, UserInfoManagerViewModel>();
    }
}
