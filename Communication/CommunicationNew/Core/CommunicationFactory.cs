using CommunicationNew.Composition;

namespace CommunicationNew.Core;

/// <summary>
/// 旧静态设备工厂的兼容入口。
/// 新代码应注入 <see cref="ICommunicationDeviceFactory"/> 或 <see cref="CommunicationManager"/>。
/// </summary>
[Obsolete("请注入 CommunicationNew.Core.ICommunicationDeviceFactory；静态工厂将在后续主版本移除。")]
public static class CommunicationFactory
{
    private static readonly ICommunicationDeviceFactory DefaultFactory =
        CommunicationDefaults.CreateDefaultFactory();

    /// <summary>使用内置注册创建设备。</summary>
    public static ICommunicationDevice Create(CommunicationParam parameter) =>
        DefaultFactory.Create(parameter);
}
