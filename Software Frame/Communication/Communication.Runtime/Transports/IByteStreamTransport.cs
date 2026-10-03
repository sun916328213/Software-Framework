// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Pipelines。导入不代表所有类型都会在运行时加载。
using System.IO.Pipelines;

namespace Communication.Runtime.Transports;

/// <summary>TCP、串口等连续字节流的传输 seam。</summary>
public interface IByteStreamTransport : IAsyncDisposable
{
    /// <summary>从远端进入 session 的连续字节。</summary>
    PipeReader Input { get; }

    /// <summary>从 session 发往远端的连续字节。</summary>
    PipeWriter Output { get; }

    /// <summary>传输是否已打开。</summary>
    bool IsOpen { get; }

    /// <summary>打开底层传输；释放开始后拒绝新的打开请求。</summary>
    /// <exception cref="ObjectDisposedException">传输已经开始释放。</exception>
    ValueTask OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>取消挂起 IO 并关闭底层传输。</summary>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
