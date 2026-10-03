// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：VisionControlsModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、Prism.Modularity、Vision.Controls.Presentation、Vision.Controls.View、Vision.Core.Presentation。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using Prism.Modularity;
using Vision.Controls.Presentation;
using Vision.Controls.View;
using Vision.Core.Presentation;

namespace Vision.Controls;

/// <summary>Registers presentation adapters and independent tool editors without loading vendor runtimes.</summary>
/// <remarks>
/// VisionControlsModule 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IModule。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class VisionControlsModule : IModule
{
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// </summary>
    public void OnInitialized(IContainerProvider provider) { }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsRegistered、RegisterSingleton、RegisterControl；具体条件和先后次序见方法体。
    /// </summary>
    public void RegisterTypes(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.IsRegistered<IToolPresentation>())
            registry.RegisterSingleton<IToolPresentation>(_ => new WpfToolPresentation());
        RegisterControl<VisionBrowserView>(registry);
        RegisterControl<ToolEditorView>(registry);
        RegisterControl<ToolCardView>(registry);
        RegisterControl<RoiEditorView>(registry);
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterControl。
    /// 正文中的关键协作调用包括 IsRegistered、Register、RegisterForNavigation；具体条件和先后次序见方法体。
    /// </summary>
    private static void RegisterControl<T>(IContainerRegistry registry)
    {
        // Tool/Card/Editor are supplied by the host; each placement owns its bindings.
        if (!registry.IsRegistered<T>()) registry.Register<T>();
        if (!registry.IsRegistered(typeof(object), typeof(T).Name)) registry.RegisterForNavigation<T>();
    }
}
