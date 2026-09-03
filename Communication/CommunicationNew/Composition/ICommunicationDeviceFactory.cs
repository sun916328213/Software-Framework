using CommunicationNew.Core;

namespace CommunicationNew.Composition;

/// <summary>根据配置创建通信设备的组合 seam。</summary>
public interface ICommunicationDeviceFactory
{
    /// <summary>创建与参数类型匹配的设备。</summary>
    ICommunicationDevice Create(CommunicationParam parameter);
}
