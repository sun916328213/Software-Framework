namespace CommunicationNew.Core;

/// <summary>
/// 根据通信参数创建设备的稳定 factory seam。
/// 调用方只依赖此接口，协议注册与默认实现由 Composition 层提供。
/// </summary>
public interface ICommunicationDeviceFactory
{
    /// <summary>创建与参数类型匹配的通信设备。</summary>
    ICommunicationDevice Create(CommunicationParam parameter);
}
