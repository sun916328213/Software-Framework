// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：IDependencyRegistrar。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using Prism.Ioc;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers one feature area with the application container.</summary>
/// <remarks>
/// IDependencyRegistrar 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// </remarks>
public interface IDependencyRegistrar
{
    string Name { get; }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// </summary>
    void Register(IContainerRegistry registry);
}
