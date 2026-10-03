// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：WPFCustomControlsModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、Prism.Modularity、System、System.Windows、WPFCustomControls.Common.Composition。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Windows;
using WPFCustomControls.Common.Composition;

namespace WPFCustomControls;

/// <summary>
/// WPFCustomControls composition root. Feature registration is delegated to focused registrars;
/// this module only orchestrates startup and shared resource initialization.
/// </summary>
/// <remarks>
/// WPFCustomControlsModule 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IModule。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class WPFCustomControlsModule : IModule
{
    private static readonly IDependencyRegistrar[] Registrars =
    {
        new ControlsDependencyRegistrar(),
        new WindowDependencyRegistrar(),
        new PropertyGridDependencyRegistrar(),
    };

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 ThrowIfNull、Register；具体条件和先后次序见方法体。
    /// </summary>
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        ArgumentNullException.ThrowIfNull(containerRegistry);

        foreach (var registrar in Registrars)
            registrar.Register(containerRegistry);
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// 正文中的关键协作调用包括 MergeResourceDictionaries；具体条件和先后次序见方法体。
    /// </summary>
    public void OnInitialized(IContainerProvider containerProvider)
    {
        MergeResourceDictionaries();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MergeResourceDictionaries。
    /// 正文中的关键协作调用包括 Equals、Add、WriteLine；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static void MergeResourceDictionaries()
    {
        try
        {
            if (Application.Current is not { } application)
                return;

            // 幂等：同一字典只合一次，避免重复 StaticResource 绑定到旧实例。
            var uri = new Uri("pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml");
            foreach (var existing in application.Resources.MergedDictionaries)
            {
                if (existing.Source is not null && existing.Source.Equals(uri))
                    return;
            }

            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = uri
            });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"WPFCustomControls 资源合并失败: {exception.Message}");
        }
    }
}
