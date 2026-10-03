// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：CommunicationServiceCollectionExtensions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Core、Microsoft.Extensions.DependencyInjection。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Communication.Runtime.Composition;

/// <summary>CommunicationNew 的 .NET 依赖注入注册入口。</summary>
public static class CommunicationServiceCollectionExtensions
{
    /// <summary>注册内置设备 factory 和设备管理器。</summary>
    public static IServiceCollection AddCommunicationNew(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var factory = CommunicationDefaults.CreateDefaultFactory();

        // 两个接口指向同一个实例：Core 是新 seam，Composition 注册保留旧调用方兼容性。
        services.AddSingleton<Communication.Core.ICommunicationDeviceFactory>(factory);

        services.AddSingleton<CommunicationManager>();
        // P11：Core 契约解析到同一 manager 单例。
        services.AddSingleton<ICommunicationManager>(
            provider => provider.GetRequiredService<CommunicationManager>());
        return services;
    }
}
