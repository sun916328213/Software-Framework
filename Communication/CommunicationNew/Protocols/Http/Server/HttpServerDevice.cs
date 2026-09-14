using System.Net;
using System.Text;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Http
{
    /// <summary>
    /// HTTP 服务器设备
    /// 基于 HttpListener 实现，支持路由注册和请求处理回调
    /// </summary>
    public class HttpServerDevice : CommunicationDeviceBase, IConnection
    {
        private new HttpParam Param => (HttpParam)base.Param;
        private readonly IHttpServerListener _listener;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> _handlers = new();
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>请求处理委托字典，key = "GET:/api/data"</summary>
        public IReadOnlyDictionary<string, Func<HttpListenerContext, Task>> Handlers => _handlers;

        /// <summary>使用系统 HTTP 监听器创建服务器。</summary>
        public HttpServerDevice(HttpParam param) : this(param, new SystemHttpServerListener())
        {
        }

        /// <summary>使用指定监听器创建服务器，监听器的所有权转移给该设备。</summary>
        internal HttpServerDevice(HttpParam param, IHttpServerListener listener) : base(param)
        {
            ArgumentNullException.ThrowIfNull(listener);
            _listener = listener;
            ProtocolType = Core.ProtocolType.Http;
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>新连接边界暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

        /// <summary>
        /// 注册路由处理器
        /// </summary>
        public void RegisterHandler(string method, string path, Func<HttpListenerContext, Task> handler)
        {
            _handlers[$"{method.ToUpperInvariant()}:{path}"] = handler;
        }

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
                _listener.AddPrefix(baseUri.GetLeftPart(UriPartial.Authority) + "/");

                // 预注册路由
                foreach (var route in Param.Routes)
                {
                    var key = $"{route.Method.ToUpperInvariant()}:{route.Path}";
                    if (!_handlers.ContainsKey(key))
                        _handlers[key] = null!; // 占位
                }

                _listener.Start();
                _receiveLoopCts?.Dispose();
                _receiveLoopCts = new CancellationTokenSource();
                _receiveLoopTask = ListenAsync(_receiveLoopCts.Token);
                _state = ConnectionState.Connected;
                OnConnectionStateChanged(true, $"HTTP服务器已启动: {Param.BaseUrl}");
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
                OnConnectionStateChanged(false, "HTTP服务器已停止");
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
                        await RejectOverloadedAsync(context, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { OnErrorOccurred(ex); }
            }
        }

        private static async Task RejectOverloadedAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers["Retry-After"] = "1";
                var message = Encoding.UTF8.GetBytes("{\"error\":\"Server Busy\"}");
                ctx.Response.ContentLength64 = message.Length;
                await ctx.Response.OutputStream.WriteAsync(message, ct).ConfigureAwait(false);
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
                var key = $"{method}:{path}";

                // 精确匹配
                if (_handlers.TryGetValue(key, out var handler) && handler != null)
                {
                    await handler(ctx);
                    return;
                }

                // 模糊匹配（前缀匹配）
                var matchKey = _handlers.Keys
                    .Where(k => k.StartsWith(method + ":") && path.StartsWith(k[(k.IndexOf(':') + 1)..]))
                    .OrderByDescending(k => k.Length)
                    .FirstOrDefault();

                if (matchKey != null && _handlers[matchKey] != null)
                {
                    await _handlers[matchKey](ctx);
                    return;
                }

                // 默认 404
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                var msg = Encoding.UTF8.GetBytes($"{{\"error\":\"Not Found\",\"path\":\"{path}\"}}");
                await ctx.Response.OutputStream.WriteAsync(msg, ct);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                ctx.Response.StatusCode = 500;
            }
            finally
            {
                ctx.Response.Close();
            }
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            // HTTP 服务端没有主动地址读写语义；该成员仅用于兼容旧设备接口。
            throw new NotSupportedException("HTTP 服务端不支持按地址读取。");
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            // 不能用 true 表示未发生的写入，否则调用方会把空操作误判为成功。
            throw new NotSupportedException("HTTP 服务端不支持按地址写入。");
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("HTTP服务器不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("HTTP服务器不支持原始收发");

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("HTTP 服务器启动失败。");
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
}
