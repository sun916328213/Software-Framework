// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ICommunicationDeviceFactory。成员说明可结合下方具体实现阅读。
namespace Communication.Core;

/// <summary>
/// 根据通信参数创建设备的稳定 factory seam。
/// 调用方只依赖此接口，协议注册与默认实现由 Composition 层提供。
/// </summary>
public interface ICommunicationDeviceFactory
{
    /// <summary>创建与参数类型匹配的通信设备。</summary>
    ICommunicationDevice Create(CommunicationParam parameter);
}
