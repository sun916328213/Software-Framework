namespace CommunicationNew.Core;

/// <summary>
/// P11 Core seam：设备暴露其创建参数快照，替代跨程序集反射 protected Param 字段。
/// Runtime 的 <c>CommunicationDeviceBase</c> 实现此接口；Controls 只依赖此接口。
/// </summary>
public interface ICommunicationParameterSource
{
    /// <summary>返回设备创建参数的只读快照（调用方可安全深拷贝/持久化）。</summary>
    CommunicationParam GetParameterSnapshot();
}
