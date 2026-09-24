using System.Net;

namespace CommunicationNew.Protocols.Http;

/// <summary>把系统 <see cref="HttpListener"/> 适配为项目内部监听器边界。</summary>
internal sealed class SystemHttpServerListener : IHttpServerListener
{
    private readonly HttpListener _listener = new();

    /// <inheritdoc />
    public bool IsListening => _listener.IsListening;

    /// <inheritdoc />
    public void AddPrefix(string prefix)
    {
        // 重连会再次配置同一地址；保持幂等可避免 Prefixes.Add 抛出重复项异常。
        if (!_listener.Prefixes.Contains(prefix))
        {
            _listener.Prefixes.Add(prefix);
        }
    }

    /// <inheritdoc />
    public void Start() => _listener.Start();

    /// <inheritdoc />
    public void Stop()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
    }

    /// <inheritdoc />
    public Task<HttpListenerContext> GetContextAsync() => _listener.GetContextAsync();

    /// <inheritdoc />
    public void Dispose() => _listener.Close();
}
