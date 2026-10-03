// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ICommunicationParameterSource。成员说明可结合下方具体实现阅读。
namespace Communication.Core;

/// <summary>
/// P11 Core seam：设备暴露其创建参数快照，替代跨程序集反射 protected Param 字段。
/// Runtime 的 <c>CommunicationDeviceBase</c> 实现此接口；Controls 只依赖此接口。
/// </summary>
public interface ICommunicationParameterSource
{
    /// <summary>返回设备创建参数的只读快照（调用方可安全深拷贝/持久化）。</summary>
    CommunicationParam GetParameterSnapshot();
}
