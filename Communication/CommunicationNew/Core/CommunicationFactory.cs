using CommunicationNew.Composition;

namespace CommunicationNew.Core;

/// <summary>
/// 旧静态设备工厂的兼容入口。
/// 新代码应注入 <see cref="ICommunicationDeviceFactory"/> 或 <see cref="CommunicationManager"/>。
/// </summary>
public static class CommunicationFactory
{
    private static readonly ICommunicationDeviceFactory DefaultFactory =
        CommunicationDefaults.CreateFactory();

    /// <summary>使用内置注册创建设备。</summary>
    public static ICommunicationDevice Create(CommunicationParam parameter) =>
        DefaultFactory.Create(parameter);
}
