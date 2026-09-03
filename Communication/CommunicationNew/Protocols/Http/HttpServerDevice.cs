using System.Net;
using System.Text;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Http
{
    /// <summary>
    /// HTTP 服务器设备
    /// 基于 HttpListener 实现，支持路由注册和请求处理回调
    /// </summary>
    public class HttpServerDevice : CommunicationDeviceBase
    {
        private new HttpParam Param => (HttpParam)base.Param;
        private HttpListener? _listener;
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> _handlers = new();

        /// <summary>请求处理委托字典，key = "GET:/api/data"</summary>
        public IReadOnlyDictionary<string, Func<HttpListenerContext, Task>> Handlers => _handlers;

        public HttpServerDevice(HttpParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Http;
        }
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>
        /// 注册路由处理器
        /// </summary>
        public void RegisterHandler(string method, string path, Func<HttpListenerContext, Task> handler)
        {
            _handlers[$"{method.ToUpperInvariant()}:{path}"] = handler;
        }

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _listener = new HttpListener();
                var baseUri = new Uri(Param.BaseUrl);
                _listener.Prefixes.Add($"{baseUri.Scheme}://+:{baseUri.Port}/");

                // 预注册路由
                foreach (var route in Param.Routes)
                {
                    var key = $"{route.Method.ToUpperInvariant()}:{route.Path}";
                    if (!_handlers.ContainsKey(key))
                        _handlers[key] = null!; // 占位
                }

                _listener.Start();
                IsConnected = true;
                OnConnectionStateChanged(true, $"HTTP服务器已启动: {Param.BaseUrl}");

                _receiveLoopCts?.Dispose();
                _receiveLoopCts = new CancellationTokenSource();
                _receiveLoopTask = ListenAsync(_receiveLoopCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                OnConnectionStateChanged(false, ex.Message);
                return false;
            }
        }

        public override async Task DisconnectAsync(CancellationToken ct = default)
        {
            _receiveLoopCts?.Cancel();
            _listener?.Stop();
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            await WaitForBackgroundOperationsAsync().ConfigureAwait(false);
            IsConnected = false;
            OnConnectionStateChanged(false, "HTTP服务器已停止");
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    StartBackgroundOperation(() => HandleRequestAsync(context, ct));
                }
                catch (OperationCanceledException) { break; }
                catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { OnErrorOccurred(ex); }
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
            // 服务器模式下不支持主动读
            return Task.FromResult(Array.Empty<byte>());
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            // 可以用于推送数据到所有连接的客户端
            return Task.FromResult(true);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("HTTP服务器不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("HTTP服务器不支持原始收发");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _listener?.Close();
            }
            base.Dispose(disposing);
        }
    }
}
