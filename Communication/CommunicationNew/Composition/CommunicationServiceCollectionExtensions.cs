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
        services.AddSingleton(CommunicationDefaults.CreateFactory());
        services.AddSingleton<CommunicationManager>();
        return services;
    }
}
