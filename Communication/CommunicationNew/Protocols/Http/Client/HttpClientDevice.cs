using System.Net.Http.Headers;
using System.Text;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Http;

/// <summary>
/// HTTP 高层客户端 adapter。
/// 地址表示相对 URI；读取使用 GET，写入使用 POST，不经过字节流 transport。
/// </summary>
public class HttpClientDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter
{
    private new HttpParam Param => (HttpParam)base.Param;
    private readonly HttpMessageHandler? _injectedHandler;
    private HttpClient? _httpClient;
    private ConnectionState _state = ConnectionState.Disconnected;

    /// <summary>使用生产 HTTP handler 创建设备。</summary>
    public HttpClientDevice(HttpParam param) : this(param, null)
    {
    }

    /// <summary>
    /// 使用调用方提供的 HTTP handler 创建设备。
    /// handler 的所有权仍属于调用方，设备断开时不会释放它。
    /// </summary>
    public HttpClientDevice(HttpParam param, HttpMessageHandler? handler) : base(param)
    {
        _injectedHandler = handler;
        ProtocolType = Core.ProtocolType.Http;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _state;

    /// <inheritdoc />
    public override Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_state == ConnectionState.Connected)
        {
            return Task.FromResult(true);
        }

        _state = ConnectionState.Connecting;
        try
        {
            HttpMessageHandler handler;
            var disposeHandler = _injectedHandler is null;
            if (_injectedHandler is not null)
            {
                handler = _injectedHandler;
            }
            else
            {
                handler = CreateProductionHandler();
            }

            _httpClient = new HttpClient(handler, disposeHandler)
            {
                BaseAddress = new Uri(Param.BaseUrl),
                Timeout = TimeSpan.FromMilliseconds(TimeoutMs),
            };
            foreach (var header in Param.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"HTTP客户端就绪: {Param.BaseUrl}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _state = ConnectionState.Faulted;
            OnErrorOccurred(ex);
            OnConnectionStateChanged(false, ex.Message);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public override Task DisconnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _state = ConnectionState.Stopping;
        _httpClient?.Dispose();
        _httpClient = null;
        _state = ConnectionState.Disconnected;
        OnConnectionStateChanged(false, "HTTP客户端已关闭");
        return Task.CompletedTask;
    }

    /// <summary>发送 GET 请求；<paramref name="length"/> 为兼容旧接口而保留。</summary>
    public override Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken ct = default)
    {
        var client = GetConnectedClient();
        return ExecuteWithRetryAsync(async () =>
        {
            using var response = await client.GetAsync(address, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }, ct);
    }

    /// <summary>发送一次 POST 请求；写操作不会因响应失败而自动重试。</summary>
    public override async Task<bool> WriteAsync(
        string address,
        byte[] data,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var client = GetConnectedClient();
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);
        using var response = await client.PostAsync(address, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public override async Task<byte[]> SendAndReceiveAsync(
        byte[] data,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var client = GetConnectedClient();
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);
        using var response = await client.PostAsync("", content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        WriteAsync("", data, ct);

    /// <summary>发送自定义 HTTP 请求并返回字符串响应体。</summary>
    public async Task<string> RequestAsync(
        string method,
        string path,
        string? body = null,
        CancellationToken ct = default)
    {
        var client = GetConnectedClient();
        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, Param.ContentType);
        }

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("HTTP 客户端初始化失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    private HttpClientHandler CreateProductionHandler()
    {
        var handler = new HttpClientHandler();
        if (Param.IgnoreSslErrors)
        {
            // 仅显式配置时关闭证书校验，默认保持平台安全策略。
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }
        return handler;
    }

    private HttpClient GetConnectedClient() =>
        _httpClient is not null && _state == ConnectionState.Connected
            ? _httpClient
            : throw new InvalidOperationException("HTTP 客户端未连接。");

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }
        base.Dispose(disposing);
    }
}
