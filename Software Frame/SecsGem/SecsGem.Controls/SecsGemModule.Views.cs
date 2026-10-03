// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、SecsGem.Controls.Views、SecsGem.Controls.Services、SecsGem.Runtime、SecsGem.Runtime.Manager、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using SecsGem.Controls.Views;
using SecsGem.Controls.Services;
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using System.Windows.Threading;

namespace SecsGem.Controls;

/// <summary>
/// SecsGemModule 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public partial class SecsGemModule
{
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterViews。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterSingleton、Resolve、nameof、Register；具体条件和先后次序见方法体。
    /// </summary>
    partial void RegisterViews(IContainerRegistry registry)
    {
        if (!registry.IsRegistered<SecsWorkspace>())
            registry.RegisterSingleton<SecsWorkspace>(provider => new SecsWorkspace(provider.Resolve<SecsGemRuntime>(), provider.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher));
        if (!registry.IsRegistered(typeof(object), nameof(SecsView)))
            registry.Register(typeof(object), typeof(SecsView), nameof(SecsView));
        if (!registry.IsRegistered(typeof(object), nameof(SecsOverviewView)))
            registry.RegisterSingleton(typeof(object), typeof(SecsOverviewView), nameof(SecsOverviewView));
    }
}
