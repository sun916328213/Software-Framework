// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：HttpClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Http、System.Net.Http.Headers、System.Text、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Http;
using System.Net.Http.Headers;
using System.Text;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;

namespace Communication.Runtime.Protocols.Http;

/// <summary>
/// HTTP 高层客户端 adapter。
/// 地址表示相对 URI；读取使用 GET，写入使用 POST，不经过字节流 transport。
/// </summary>
public class HttpClientDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter
{
    private new HttpParam Param => (HttpParam)base.Param;
    private readonly HttpClientSession _clientSession;
    private HttpClient? _httpClient => _clientSession.Client;
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
        _clientSession = new HttpClientSession(handler);
        ProtocolType = global::Communication.Core.ProtocolType.Http;
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
            _clientSession.Open(Param.BaseUrl, TimeoutMs, Param.IgnoreSslErrors, Param.Headers);

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
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、OnConnectionStateChanged；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task DisconnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _state = ConnectionState.Stopping;
        _clientSession.Close();
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
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、GetConnectedClient、ConfigureAwait、PostAsync、EnsureSuccessStatusCode；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// 正文中的关键协作调用包括 WriteAsync；具体条件和先后次序见方法体。
    /// </remarks>
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

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("HTTP 客户端初始化失败。");
        }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetConnectedClient。
    /// </summary>
    private HttpClient GetConnectedClient() =>
        _httpClient is not null && _state == ConnectionState.Connected
            ? _httpClient
            : throw new InvalidOperationException("HTTP 客户端未连接。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clientSession.Close();
        }
        base.Dispose(disposing);
    }
}
