using System.Net;

namespace CommunicationNew.Protocols.Http;

/// <summary>
/// HTTP 服务端监听器的内部边界，用于隔离操作系统 URL ACL 与真实网络句柄。
/// </summary>
internal interface IHttpServerListener : IDisposable
{
    /// <summary>监听器是否正在接收请求。</summary>
    bool IsListening { get; }

    /// <summary>添加要监听的 URI 前缀。</summary>
    void AddPrefix(string prefix);

    /// <summary>启动监听。</summary>
    void Start();

    /// <summary>停止监听并解除正在等待的请求。</summary>
    void Stop();

    /// <summary>异步等待下一个 HTTP 上下文。</summary>
    Task<HttpListenerContext> GetContextAsync();
}
