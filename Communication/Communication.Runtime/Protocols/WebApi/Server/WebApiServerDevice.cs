using System.Net;
using System.Text;
using System.Text.Json;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Http;

namespace CommunicationNew.Protocols.WebApi
{
    /// <summary>
    /// WebApi 服务器设备
    /// 基于 HttpListener，提供RESTful API服务端能力
    /// 支持路由注册、JSON请求/响应自动处理
    /// </summary>
    public class WebApiServerDevice : CommunicationDeviceBase, IConnection
    {
        private new WebApiParam Param => (WebApiParam)base.Param;
        private readonly IHttpServerListener _listener;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private readonly Dictionary<string, Func<WebApiRequest, Task<WebResponse>>> _handlers = new();
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>已注册的API处理器</summary>
        public IReadOnlyDictionary<string, Func<WebApiRequest, Task<WebResponse>>> Handlers => _handlers;

        /// <summary>使用系统 HTTP 监听器创建 WebApi 服务器。</summary>
        public WebApiServerDevice(WebApiParam param) : this(param, new SystemHttpServerListener())
        {
        }

        /// <summary>使用指定监听器创建服务器，监听器的所有权转移给该设备。</summary>
        internal WebApiServerDevice(WebApiParam param, IHttpServerListener listener) : base(param)
        {
            ArgumentNullException.ThrowIfNull(listener);
            _listener = listener;
            ProtocolType = Core.ProtocolType.WebApi;
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>新连接边界暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

        /// <summary>
        /// 注册API处理器
        /// </summary>
        /// <param name="method">HTTP方法</param>
        /// <param name="path">API路径（如 /users/{id}）</param>
        /// <param name="handler">处理函数</param>
        public void RegisterHandler(string method, string path, Func<WebApiRequest, Task<WebResponse>> handler)
        {
            _handlers[$"{method.ToUpperInvariant()}:{path}"] = handler;
        }

        /// <summary>注册GET处理器</summary>
        public void Get(string path, Func<WebApiRequest, Task<WebResponse>> handler)
            => RegisterHandler("GET", path, handler);

        /// <summary>注册POST处理器</summary>
        public void Post(string path, Func<WebApiRequest, Task<WebResponse>> handler)
            => RegisterHandler("POST", path, handler);

        /// <summary>注册PUT处理器</summary>
        public void Put(string path, Func<WebApiRequest, Task<WebResponse>> handler)
            => RegisterHandler("PUT", path, handler);

        /// <summary>注册DELETE处理器</summary>
        public void Delete(string path, Func<WebApiRequest, Task<WebResponse>> handler)
            => RegisterHandler("DELETE", path, handler);

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_state == ConnectionState.Connected)
                {
                    return true;
                }

                _state = ConnectionState.Connecting;
                var baseUri = new Uri(Param.BaseUrl);
                var apiPrefix = Param.ApiPrefix.Trim('/');
                _listener.AddPrefix($"{baseUri.GetLeftPart(UriPartial.Authority)}/{apiPrefix}/");

                // 从配置预注册路由
                foreach (var route in Param.Routes)
                    _handlers.TryAdd($"{route.Method.ToUpperInvariant()}:{route.Path}", null!);

                _listener.Start();
                _receiveLoopCts?.Dispose();
                _receiveLoopCts = new CancellationTokenSource();
                _receiveLoopTask = ListenAsync(_receiveLoopCts.Token);
                _state = ConnectionState.Connected;
                OnConnectionStateChanged(true, $"WebApi服务器已启动: {Param.BaseUrl}{Param.ApiPrefix}");
                return true;
            }
            catch (OperationCanceledException)
            {
                _state = ConnectionState.Disconnected;
                throw;
            }
            catch (Exception ex)
            {
                _state = ConnectionState.Faulted;
                OnErrorOccurred(ex);
                OnConnectionStateChanged(false, ex.Message);
                return false;
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public override async Task DisconnectAsync(CancellationToken ct = default)
        {
            await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_state == ConnectionState.Disconnected)
                {
                    return;
                }

                _state = ConnectionState.Stopping;
                _receiveLoopCts?.Cancel();
                _listener.Stop();
                // Stop 会解除 GetContextAsync；随后等待可保证断开返回时不再接收新请求。
                await WaitForReceiveLoopAsync().ConfigureAwait(false);
                await WaitForBackgroundOperationsAsync().ConfigureAwait(false);
                _state = ConnectionState.Disconnected;
                OnConnectionStateChanged(false, "WebApi服务器已停止");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (!TryStartBackgroundOperation(() => HandleRequestAsync(context, ct)))
                    {
                        await RejectOverloadedAsync(context).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { OnErrorOccurred(ex); }
            }
        }

        private static async Task RejectOverloadedAsync(HttpListenerContext ctx)
        {
            try
            {
                ctx.Response.Headers["Retry-After"] = "1";
                await WriteResponseAsync(ctx, (int)HttpStatusCode.ServiceUnavailable, new WebResponse
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                    Error = "Server Busy",
                }).ConfigureAwait(false);
            }
            finally
            {
                // 被拒绝的请求没有进入后台跟踪器，必须在接收循环内完成资源回收。
                ctx.Response.Close();
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                var method = ctx.Request.HttpMethod.ToUpperInvariant();
                var path = ctx.Request.Url?.AbsolutePath ?? "/";

                // 去除API前缀
                var apiPath = path.StartsWith(Param.ApiPrefix) ? path[Param.ApiPrefix.Length..] : path;
                if (!apiPath.StartsWith("/")) apiPath = "/" + apiPath;

                var key = $"{method}:{apiPath}";

                // 精确匹配
                if (_handlers.TryGetValue(key, out var handler) && handler != null)
                {
                    await ProcessHandlerAsync(ctx, handler, ct);
                    return;
                }

                // 路径参数匹配（{id} 等）
                foreach (var kv in _handlers)
                {
                    if (kv.Value == null) continue;
                    var parts = kv.Key.Split(':');
                    if (parts[0] != method) continue;
                    if (MatchRoute(parts[1], apiPath, out var pathParams))
                    {
                        await ProcessHandlerAsync(ctx, kv.Value, ct, pathParams);
                        return;
                    }
                }

                // 404
                await WriteResponseAsync(ctx, 404, new WebResponse { Success = false, Error = $"Not Found: {method} {apiPath}" });
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                await WriteResponseAsync(ctx, 500, new WebResponse { Success = false, Error = ex.Message });
            }
            finally
            {
                ctx.Response.Close();
            }
        }

        private async Task ProcessHandlerAsync(HttpListenerContext ctx, Func<WebApiRequest, Task<WebResponse>> handler, CancellationToken ct, Dictionary<string, string>? pathParams = null)
        {
            // 读取请求体
            byte[] bodyBytes = Array.Empty<byte>();
            if (ctx.Request.HasEntityBody && ctx.Request.InputStream != null)
            {
                using var ms = new MemoryStream();
                await ctx.Request.InputStream.CopyToAsync(ms, ct);
                bodyBytes = ms.ToArray();
            }

            var request = new WebApiRequest
            {
                Method = ctx.Request.HttpMethod,
                Path = ctx.Request.Url?.AbsolutePath ?? "/",
                QueryString = ctx.Request.QueryString,
                Headers = ctx.Request.Headers,
                Body = bodyBytes,
                PathParameters = pathParams ?? new()
            };

            var response = await handler(request);
            await WriteResponseAsync(ctx, response.StatusCode, response);
        }

        private static bool MatchRoute(string template, string path, out Dictionary<string, string> parameters)
        {
            parameters = new();
            var templateParts = template.Trim('/').Split('/');
            var pathParts = path.Trim('/').Split('/');

            if (templateParts.Length != pathParts.Length) return false;

            for (int i = 0; i < templateParts.Length; i++)
            {
                if (templateParts[i].StartsWith('{') && templateParts[i].EndsWith('}'))
                {
                    var paramName = templateParts[i][1..^1];
                    parameters[paramName] = pathParts[i];
                }
                else if (templateParts[i] != pathParts[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static async Task WriteResponseAsync(HttpListenerContext ctx, int statusCode, WebResponse response)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(response);
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
            => throw new NotSupportedException("WebApi 服务端不支持按地址读取。");

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("WebApi 服务端不支持按地址写入。");

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("WebApi服务器不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("WebApi服务器不支持原始收发");

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("WebApi 服务器启动失败。");
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _listener.Dispose();
                _lifecycleGate.Dispose();
            }
            base.Dispose(disposing);
            _state = ConnectionState.Disposed;
        }
    }

    /// <summary>WebApi 请求模型</summary>
    public class WebApiRequest
    {
        public string Method { get; set; } = "";
        public string Path { get; set; } = "";
        public System.Collections.Specialized.NameValueCollection QueryString { get; set; } = new();
        public System.Collections.Specialized.NameValueCollection Headers { get; set; } = new();
        public byte[] Body { get; set; } = Array.Empty<byte>();
        public Dictionary<string, string> PathParameters { get; set; } = new();

        /// <summary>获取JSON反序列化后的请求体</summary>
        public T? GetBody<T>() => Body.Length > 0 ? JsonSerializer.Deserialize<T>(Body) : default;
    }

    /// <summary>WebApi 响应模型</summary>
    public class WebResponse
    {
        public bool Success { get; set; } = true;
        public int StatusCode { get; set; } = 200;
        public object? Data { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }
}
