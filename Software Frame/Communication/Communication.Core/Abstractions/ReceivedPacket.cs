// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ReceivedPacket。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>从原始传输接收的一段字节或一个完整数据报。</summary>
public sealed class ReceivedPacket
{
    /// <summary>
    /// 创建不可变接收快照。构造函数复制输入数据，避免旧事件订阅者修改同一缓冲区。
    /// </summary>
    public ReceivedPacket(ReadOnlySpan<byte> data, string? remoteAddress = null)
    {
        Data = data.ToArray();
        RemoteAddress = remoteAddress;
    }

    /// <summary>
    /// 接收数据。UDP 保持一个完整数据报；TCP/Serial 表示未分帧的连续字节块。
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>远端端点或串口名称；无法确定时为 <see langword="null"/>。</summary>
    public string? RemoteAddress { get; }
}
