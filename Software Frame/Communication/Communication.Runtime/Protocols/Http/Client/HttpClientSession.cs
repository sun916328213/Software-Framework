namespace Communication.Runtime.Protocols.Http;

/// <summary>拥有客户端实例，调用方注入的handler继续由调用方释放。</summary>
internal sealed class HttpClientSession(HttpMessageHandler? injectedHandler)
{
    internal HttpClient? Client { get; private set; }

    /// <summary>创建客户端并按协议规定的顺序设置请求头。</summary>
    internal void Open(string baseUrl, int timeoutMs, bool ignoreSslErrors,
        IEnumerable<KeyValuePair<string, string>> headers, Action<HttpClient>? beforeHeaders = null)
    {
        var handler = injectedHandler;
        if (handler is null)
        {
            var production = new HttpClientHandler();
            if (ignoreSslErrors)
                production.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            handler = production;
        }
        Client = new HttpClient(handler, disposeHandler: injectedHandler is null)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };
        beforeHeaders?.Invoke(Client);
        foreach (var header in headers)
            Client.DefaultRequestHeaders.Add(header.Key, header.Value);
    }

    /// <summary>释放客户端，保留外部注入的handler。</summary>
    internal void Close()
    {
        Client?.Dispose();
        Client = null;
    }
}
