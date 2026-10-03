// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ConnectionState。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>连接 session 的显式生命周期状态。</summary>
public enum ConnectionState
{
    /// <summary>没有活动连接。</summary>
    Disconnected,
    /// <summary>正在建立传输连接。</summary>
    Connecting,
    /// <summary>连接可接受请求。</summary>
    Connected,
    /// <summary>连接中断后正在恢复。</summary>
    Reconnecting,
    /// <summary>正在取消操作并关闭传输。</summary>
    Stopping,
    /// <summary>连接或协议处理失败，需要调用方恢复或断开。</summary>
    Faulted,
    /// <summary>session 已释放，不能再次使用。</summary>
    Disposed,
}
