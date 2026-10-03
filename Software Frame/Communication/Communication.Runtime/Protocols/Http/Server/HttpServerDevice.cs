// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：HttpServerDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Http、System.Net、System.Text、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Http;
using System.Net;
using System.Text;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;

namespace Communication.Runtime.Protocols.Http
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
            ProtocolType = global::Communication.Core.ProtocolType.Http;
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

        /// <summary>
        /// 发起连接操作；对应入口为 ConnectAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIf、AddPrefix、GetLeftPart；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Stop、WaitForReceiveLoopAsync、WaitForBackgroundOperationsAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ListenAsync。
        /// 正文中的关键协作调用包括 GetContextAsync、TryStartBackgroundOperation、HandleRequestAsync、ConfigureAwait、RejectOverloadedAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RejectOverloadedAsync。
        /// 正文中的关键协作调用包括 GetBytes、ConfigureAwait、WriteAsync、Close；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 HandleRequestAsync。
        /// 正文中的关键协作调用包括 ToUpperInvariant、TryGetValue、handler、FirstOrDefault、OrderByDescending；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadAsync。
        /// </summary>
        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            // HTTP 服务端没有主动地址读写语义；该成员仅用于兼容旧设备接口。
            throw new NotSupportedException("HTTP 服务端不支持按地址读取。");
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// </summary>
        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            // 不能用 true 表示未发生的写入，否则调用方会把空操作误判为成功。
            throw new NotSupportedException("HTTP 服务端不支持按地址写入。");
        }

        /// <summary>
        /// 执行发送操作；对应入口为 SendAsync。
        /// </summary>
        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("HTTP服务器不支持原始字节发送");

        /// <summary>
        /// 执行发送操作；对应入口为 SendAndReceiveAsync。
        /// </summary>
        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("HTTP服务器不支持原始收发");

        /// <summary>
        /// 发起连接操作；对应入口为 ConnectAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("HTTP 服务器启动失败。");
            }
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectAsync。
        /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
        /// </summary>
        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
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
