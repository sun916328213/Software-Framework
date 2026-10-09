// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：TcpClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Socket、System.Buffers、System.Text、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Socket;
using System.Buffers;
using System.Text;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Runtime;
using Communication.Runtime.Transports;

namespace Communication.Runtime.Protocols.Socket;

/// <summary>
/// TCP 客户端兼容 adapter。
/// 旧事件和原始收发接口保留，连接与连续字节流由 <see cref="IByteStreamTransport"/> 管理。
/// </summary>
public class TcpClientDevice : CommunicationDeviceBase, IConnection, IRawSender, IRequestReplyClient, IReceiveSource<ReceivedPacket>
{
    private new SocketParam Param => (SocketParam)base.Param;
    private readonly IByteStreamTransport _transport;
    private readonly ReconnectSupervisor? _reconnectSupervisor;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private DelimitedFrameReader? _frameReader;
    private TaskCompletionSource<byte[]>? _pendingResponse;
    private ConnectionState _state = ConnectionState.Disconnected;
    private Encoding _encoding = Encoding.UTF8;
    private volatile bool _desiredConnected;
    private long _receiveGeneration;

    /// <summary>使用 SocketParam 创建生产 TCP transport。</summary>
    public TcpClientDevice(SocketParam param)
        : this(param, new TcpByteStreamTransport(param.ServerIP, param.Port))
    {
    }

    /// <summary>
    /// 使用指定字节流 transport 创建 TCP 客户端；设备释放时会一并释放 transport。
    /// </summary>
    public TcpClientDevice(SocketParam param, IByteStreamTransport transport) : base(param)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        if (param.AutoReconnect)
        {
            _reconnectSupervisor = new ReconnectSupervisor(
                new ConnectionReconnectOptions(TimeSpan.FromMilliseconds(param.ReconnectIntervalMs)),
                TimeProvider.System,
                cancellationToken => new ValueTask<bool>(ConnectAsync(cancellationToken)),
                () => _desiredConnected,
                new CommunicationDiagnosticContext("tcp", transport.GetType().Name));
        }
        ProtocolType = global::Communication.Core.ProtocolType.Tcp;
        EnableReceiveSource(ReceiveOverflowPolicy.Reject);
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _state;

    /// <summary>失败诊断用的端点描述（host:port）。</summary>
    private string EndpointText => $"{Param.ServerIP}:{Param.Port}";

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、GetEncoding、CloseAsync、OpenAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        _desiredConnected = true;
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // T13：清掉上一次尝试留下的失败原因，避免把旧失败误当成这一次的 cause。
            BeginFailureScope();
            if (_state == ConnectionState.Connected)
            {
                return true;
            }

            var resetTransport = _state == ConnectionState.Faulted;
            _state = ConnectionState.Connecting;
            _encoding = Encoding.GetEncoding(Param.Encoding);
            // T15：分帧读取器按连接代际重建，避免把上一个连接遗留的半帧拼到新连接上。
            _frameReader = string.IsNullOrEmpty(Param.Delimiter)
                ? null
                : DelimitedFrameReader.ForDelimiter(_encoding.GetBytes(Param.Delimiter));
            if (resetTransport)
            {
                // 远端关闭后 TcpClient 可能仍报告 Connected；必须丢弃旧 Pipe 和句柄。
                await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            await _transport.OpenAsync(ct).ConfigureAwait(false);
            _receiveLoopCts?.Dispose();
            _receiveLoopCts = new CancellationTokenSource();
            _receiveGeneration = BeginReceiveGeneration();
            _receiveLoopTask = ReceiveLoopAsync(_receiveLoopCts.Token, _receiveGeneration);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"已连接 {Param.ServerIP}:{Param.Port}");
            return true;
        }
        catch (OperationCanceledException)
        {
            EndReceiveGeneration(_receiveGeneration);
            _state = ConnectionState.Disconnected;
            throw;
        }
        catch (Exception ex)
        {
            EndReceiveGeneration(_receiveGeneration);
            _state = ConnectionState.Faulted;
            RecordFailure(ex);
            OnConnectionStateChanged(false, ex.Message);
            return false;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、StopAsync、WaitAsync、EndReceiveGeneration、CloseAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task DisconnectAsync(CancellationToken ct = default)
    {
        _desiredConnected = false;
        if (_reconnectSupervisor is not null)
        {
            await _reconnectSupervisor.StopAsync().ConfigureAwait(false);
        }

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EndReceiveGeneration(_receiveGeneration);
            if (_state == ConnectionState.Disconnected)
            {
                return;
            }

            _state = ConnectionState.Stopping;
            _receiveLoopCts?.Cancel();
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            _state = ConnectionState.Disconnected;
            OnConnectionStateChanged(false, "已断开连接");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// 处理接收操作；对应入口为 ReceiveLoopAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ReadAsync、ToArray、TrySetResult、PublishReceived；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct, long receiveGeneration)
    {
        // T15：循环结束的原因必须被如实带出去。改造前 finally 里写死「远端连接已断开」，
        // 于是「可靠收件箱满」「分帧缓冲超限」这些本地故障也会被说成对端断开 —— 归因错误。
        string? stopReason = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _transport.Input.ReadAsync(ct).ConfigureAwait(false);
                var buffer = result.Buffer;
                if (!buffer.IsEmpty)
                {
                    var data = buffer.ToArray();
                    TaskCompletionSource<byte[]>? pendingResponse;
                    lock (SyncRoot)
                    {
                        // 在发布事件前固定关联对象，事件处理器新建的请求不能认领当前旧数据。
                        pendingResponse = _pendingResponse;
                    }
                    // 请求关联是可靠内部路径，必须先完成，不能等待用户事件处理器。
                    pendingResponse?.TrySetResult(data);
                    PublishReceived(data, EndpointText);
                }
                _transport.Input.AdvanceTo(buffer.End);
                if (result.IsCompleted)
                {
                    // 这一条才是真正的「对端下线」：Pipe 被对端正常关闭。
                    stopReason = "远端连接已断开";
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 主动断开通过取消令牌结束接收循环。
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            // 关闭网络流会解除底层等待。
        }
        catch (Exception ex)
        {
            stopReason = ex.Message;
            OnErrorOccurred(ex);
        }
        finally
        {
            if (!ct.IsCancellationRequested && _state == ConnectionState.Connected)
            {
                EndReceiveGeneration(receiveGeneration);
                _state = ConnectionState.Faulted;
                OnConnectionStateChanged(false, stopReason ?? "接收循环已结束");
                // Runtime supervisor 持有恢复循环；手工断开会把期望状态改为离线。
                _reconnectSupervisor?.Start();
            }
        }
    }

    /// <summary>
    /// 发布一段接收字节（T15）：可靠收件箱<b>永远</b>拿到未分帧的原始块；
    /// 旧 <c>DataReceived</c> 事件在配置了分包结束符时只承载<b>完整帧</b>，否则承载原始块。
    /// </summary>
    /// <remarks>
    /// 改造前是「原始块进收件箱 + 原始块进事件 + 帧进事件」：订阅者会同时收到原始块与它的各个帧，
    /// 同一批数据投递两遍。现在原始块只走收件箱、帧只走事件，两条路径各自闭合。
    /// </remarks>
    private void PublishReceived(byte[] data, string endpoint)
    {
        var reader = _frameReader;
        if (reader is null)
        {
            // 没有分包结束符：事件与收件箱都拿原始块（与改造前一致）。
            OnPacketReceived(data, endpoint);
            return;
        }

        OnPacketReceived(data, endpoint, raiseDataReceivedEvent: false);
        reader.Append(data, frame => OnDataReceived(frame, endpoint));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、EnsureConnected、ConfigureAwait、WriteAsync、OnErrorOccurred；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureConnected();
        try
        {
            BeginFailureScope();
            await _transport.Output.WriteAsync(data, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure(ex);
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、SendAsync、WaitAsync、FromMilliseconds、ReferenceEquals；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </remarks>
    public override async Task<byte[]> SendAndReceiveAsync(
        byte[] data,
        CancellationToken ct = default)
    {
        var response = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (SyncRoot)
        {
            if (_pendingResponse is not null)
            {
                throw new InvalidOperationException("TCP 客户端一次只允许一个请求等待响应。");
            }
            _pendingResponse = response;
        }

        try
        {
            if (!await SendAsync(data, ct).ConfigureAwait(false))
            {
                // T13：发送失败同样要带上底层原因与端点。
                throw CreateFailureException("TCP 数据发送", EndpointText);
            }
            return await response.Task
                .WaitAsync(TimeSpan.FromMilliseconds(TimeoutMs), ct)
                .ConfigureAwait(false);
        }
        finally
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(_pendingResponse, response))
                {
                    _pendingResponse = null;
                }
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理接收操作；对应入口为 ReceiveAsync。
    /// 正文中的关键协作调用包括 ReceivePacketAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask<ReceivedPacket> ReceiveAsync(CancellationToken cancellationToken = default) =>
        ReceivePacketAsync(cancellationToken);

    /// <summary>原始 TCP 没有地址读取语义；该成员仅用于旧设备接口兼容。</summary>
    public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
        throw new NotSupportedException("原始 TCP 不支持按地址读取，请使用被动接收或请求/回复能力。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 SendAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
        SendAsync(data, ct);

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            // T13：把 bool 失败展开成带 cause 与端点的异常，而不是一句没有信息的固定文案。
            throw CreateFailureException("TCP 客户端连接", EndpointText);
        }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnsureConnected。
    /// </summary>
    private void EnsureConnected()
    {
        if (_state != ConnectionState.Connected || !_transport.IsOpen)
        {
            throw new InvalidOperationException("TCP 客户端未连接。");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 GetResult、GetAwaiter、AsTask、DisposeAsync、Dispose；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reconnectSupervisor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _lifecycleGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
