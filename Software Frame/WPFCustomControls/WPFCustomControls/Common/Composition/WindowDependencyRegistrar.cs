// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：WindowDependencyRegistrar。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、Prism.Ioc、WPFCustomControls.Window.Views。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using Prism.Ioc;
using WPFCustomControls.Window.Views;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers standalone window views.</summary>
/// <remarks>
/// WindowDependencyRegistrar 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IDependencyRegistrar。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class WindowDependencyRegistrar : IDependencyRegistrar
{
    public string Name => "Window";

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// 正文中的关键协作调用包括 IsRegistered、Register；具体条件和先后次序见方法体。
    /// </summary>
    public void Register(IContainerRegistry registry)
    {
        if (!registry.IsRegistered<FileLoading>())
            registry.Register<FileLoading>();
        if (!registry.IsRegistered<MessagePrompt>())
            registry.Register<MessagePrompt>();
        if (!registry.IsRegistered<ProgramLoading>())
            registry.Register<ProgramLoading>();
    }
}
