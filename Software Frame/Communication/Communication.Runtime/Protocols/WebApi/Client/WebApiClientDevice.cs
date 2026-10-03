// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：WebApiClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.WebApi、System.Net.Http.Headers、System.Text、System.Text.Json、Communication.Core.Abstractions、Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.WebApi;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;

namespace Communication.Runtime.Protocols.WebApi;

/// <summary>
/// 面向 JSON REST 接口的高层客户端 adapter。
/// 使用 HttpClient handler seam，不经过字节流 transport。
/// </summary>
public class WebApiClientDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter
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
        ProtocolType = global::Communication.Core.ProtocolType.WebApi;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _state;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、CreateProductionHandler、FromMilliseconds、Add；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、OnConnectionStateChanged；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// 正文中的关键协作调用包括 GetConnectedClient、CreateContent、ConfigureAwait、PostAsync、EnsureSuccessStatusCode；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// 正文中的关键协作调用包括 WriteAsync；具体条件和先后次序见方法体。
    /// </remarks>
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

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("WebApi 客户端初始化失败。");
        }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <summary>
    /// 执行发送操作；对应入口为 SendJsonAsync。
    /// 正文中的关键协作调用包括 GetConnectedClient、BuildPath、Serialize、ConfigureAwait、SendAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateProductionHandler。
    /// </summary>
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

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateContent。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </summary>
    private ByteArrayContent CreateContent(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(Param.ContentType);
        return content;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildPath。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetConnectedClient。
    /// </summary>
    private HttpClient GetConnectedClient() =>
        _httpClient is not null && _state == ConnectionState.Connected
            ? _httpClient
            : throw new InvalidOperationException("WebApi 客户端未连接。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </remarks>
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
