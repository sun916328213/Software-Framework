using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.WebApi;

/// <summary>
/// 面向 JSON REST 接口的高层客户端 adapter。
/// 使用 HttpClient handler seam，不经过字节流 transport。
/// </summary>
public class WebApiClientDevice : CommunicationDeviceBase, IConnection
{
    private new WebApiParam Param => (WebApiParam)base.Param;
    private readonly HttpMessageHandler? _injectedHandler;
    private HttpClient? _httpClient;
    private ConnectionState _state = ConnectionState.Disconnected;

    /// <summary>使用生产 HTTP handler 创建 WebApi 客户端。</summary>
    public WebApiClientDevice(WebApiParam param) : this(param, null)
    {
    }

    /// <summary>
    /// 使用调用方提供的 HTTP handler 创建客户端；handler 所有权仍属于调用方。
    /// </summary>
    public WebApiClientDevice(WebApiParam param, HttpMessageHandler? handler) : base(param)
    {
        _injectedHandler = handler;
        ProtocolType = Core.ProtocolType.WebApi;
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
            var disposeHandler = _injectedHandler is null;
            var handler = _injectedHandler ?? CreateProductionHandler();
            _httpClient = new HttpClient(handler, disposeHandler)
            {
                BaseAddress = new Uri(Param.BaseUrl),
                Timeout = TimeSpan.FromMilliseconds(Param.OperationTimeoutMs),
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            foreach (var header in Param.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
            if (!string.IsNullOrEmpty(Param.BearerToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Param.BearerToken);
            }
            if (!string.IsNullOrEmpty(Param.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add(Param.ApiKeyHeaderName, Param.ApiKey);
            }

            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"WebApi客户端就绪: {Param.BaseUrl}{Param.ApiPrefix}");
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
        OnConnectionStateChanged(false, "WebApi客户端已关闭");
        return Task.CompletedTask;
    }

    /// <summary>发送 GET 请求并返回 JSON 响应体。</summary>
    public override Task<byte[]> ReadAsync(
        string address,
        int length,
        CancellationToken ct = default)
    {
        var client = GetConnectedClient();
        var path = BuildPath(address);
        return ExecuteWithRetryAsync(async () =>
        {
            using var response = await client.GetAsync(path, ct).ConfigureAwait(false);
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
        using var content = CreateContent(data);
        using var response = await client
            .PostAsync(BuildPath(address), content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public override async Task<byte[]> SendAndReceiveAsync(
        byte[] data,
        CancellationToken ct = default)
    {
        var client = GetConnectedClient();
        using var content = CreateContent(data);
        using var response = await client
            .PostAsync(Param.ApiPrefix, content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        WriteAsync("", data, ct);

    /// <summary>发送 GET 请求并反序列化 JSON 响应。</summary>
    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var bytes = await ReadAsync(path, 0, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes);
    }

    /// <summary>序列化请求对象，发送一次 POST 并反序列化响应。</summary>
    public Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken ct = default) =>
        SendJsonAsync<TRequest, TResponse>(HttpMethod.Post, path, body, ct);

    /// <summary>序列化请求对象，发送一次 PUT 并反序列化响应。</summary>
    public Task<TResponse?> PutAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken ct = default) =>
        SendJsonAsync<TRequest, TResponse>(HttpMethod.Put, path, body, ct);

    /// <summary>发送一次 DELETE 请求，不自动重试。</summary>
    public async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        var client = GetConnectedClient();
        using var response = await client.DeleteAsync(BuildPath(path), ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("WebApi 客户端初始化失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    private async Task<TResponse?> SendJsonAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest body,
        CancellationToken ct)
    {
        var client = GetConnectedClient();
        using var request = new HttpRequestMessage(method, BuildPath(path))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                Param.ContentType),
        };
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TResponse>(bytes);
    }

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

    private ByteArrayContent CreateContent(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);
        return content;
    }

    private string BuildPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Param.ApiPrefix;
        }
        return path.StartsWith('/')
            ? $"{Param.ApiPrefix}{path}"
            : $"{Param.ApiPrefix}/{path}";
    }

    private HttpClient GetConnectedClient() =>
        _httpClient is not null && _state == ConnectionState.Connected
            ? _httpClient
            : throw new InvalidOperationException("WebApi 客户端未连接。");

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
