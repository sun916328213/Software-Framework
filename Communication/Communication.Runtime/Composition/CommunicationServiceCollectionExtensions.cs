using CommunicationNew.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CommunicationNew.Composition;

/// <summary>CommunicationNew 的 .NET 依赖注入注册入口。</summary>
public static class CommunicationServiceCollectionExtensions
{
    /// <summary>注册内置设备 factory 和设备管理器。</summary>
    public static IServiceCollection AddCommunicationNew(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var factory = CommunicationDefaults.CreateDefaultFactory();

        // 两个接口指向同一个实例：Core 是新 seam，Composition 注册保留旧调用方兼容性。
        services.AddSingleton<CommunicationNew.Core.ICommunicationDeviceFactory>(factory);
#pragma warning disable CS0618 // 旧依赖注入解析仅作为迁移期兼容入口。
        services.AddSingleton<ICommunicationDeviceFactory>(
            (ICommunicationDeviceFactory)factory);
#pragma warning restore CS0618
        services.AddSingleton<CommunicationManager>();
        // P11：Core 契约解析到同一 manager 单例。
        services.AddSingleton<ICommunicationManager>(
            provider => provider.GetRequiredService<CommunicationManager>());
        return services;
    }
}
