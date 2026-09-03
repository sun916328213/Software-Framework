using System.IO.Pipelines;

namespace CommunicationNew.Transports;

/// <summary>TCP、串口等连续字节流的传输 seam。</summary>
public interface IByteStreamTransport : IAsyncDisposable
{
    /// <summary>从远端进入 session 的连续字节。</summary>
    PipeReader Input { get; }

    /// <summary>从 session 发往远端的连续字节。</summary>
    PipeWriter Output { get; }

    /// <summary>传输是否已打开。</summary>
    bool IsOpen { get; }

    /// <summary>打开底层传输。</summary>
    ValueTask OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>取消挂起 IO 并关闭底层传输。</summary>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
