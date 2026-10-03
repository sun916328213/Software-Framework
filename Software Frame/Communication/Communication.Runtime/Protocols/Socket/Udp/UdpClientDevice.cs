// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：UdpClientDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Socket、System.Net、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Communication.Runtime。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Socket;
using System.Net;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Runtime;
using Communication.Runtime.Transports;

namespace Communication.Runtime.Protocols.Socket;

/// <summary>
/// UDP 客户端兼容 adapter。
/// 旧设备接口、事件与参数模型保留，数据报生命周期由 <see cref="IDatagramTransport"/> 管理。
/// </summary>
public class UdpClientDevice : CommunicationDeviceBase, IConnection, IRawSender, IRequestReplyClient, IReceiveSource<ReceivedPacket>
{
    private new SocketParam Param => (SocketParam)base.Param;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IDatagramTransport? _transport;
    private IPEndPoint? _remoteEndpoint;
    private TaskCompletionSource<byte[]>? _pendingResponse;
    private ConnectionState _state = ConnectionState.Disconnected;
    private long _receiveGeneration;

    /// <summary>使用 Socket 参数创建 UDP 客户端。</summary>
    public UdpClientDevice(SocketParam param) : base(param)
    {
        ProtocolType = global::Communication.Core.ProtocolType.Udp;
        EnableReceiveSource(ReceiveOverflowPolicy.DropOldest);
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _state;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Parse、OpenAsync、DisposeAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_state == ConnectionState.Connected)
            {
                return true;
            }

            _state = ConnectionState.Connecting;
            var localEndpoint = new IPEndPoint(IPAddress.Parse(Param.LocalIP), Param.LocalPort);
            var transport = new UdpDatagramTransport(localEndpoint);
            try
            {
                await transport.OpenAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await transport.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _remoteEndpoint = new IPEndPoint(IPAddress.Parse(Param.ServerIP), Param.Port);
            _transport = transport;
            _receiveLoopCts?.Dispose();
            _receiveLoopCts = new CancellationTokenSource();
            _receiveGeneration = BeginReceiveGeneration();
            _receiveLoopTask = ReceiveLoopAsync(transport, _receiveLoopCts.Token, _receiveGeneration);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"UDP已绑定到 {Param.ServerIP}:{Param.Port}");
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
            OnErrorOccurred(ex);
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
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、EndReceiveGeneration、CloseAsync、WaitForReceiveLoopAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task DisconnectAsync(CancellationToken ct = default)
    {
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
            var transport = _transport;
            _transport = null;
            if (transport is not null)
            {
                await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            if (transport is not null)
            {
                await transport.DisposeAsync().ConfigureAwait(false);
            }
            _state = ConnectionState.Disconnected;
            OnConnectionStateChanged(false, "UDP已关闭");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// 处理接收操作；对应入口为 ReceiveLoopAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ReceiveAsync、ToArray、OnPacketReceived、ToString；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task ReceiveLoopAsync(
        IDatagramTransport transport,
        CancellationToken ct,
        long receiveGeneration)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var datagram = await transport.ReceiveAsync(ct).ConfigureAwait(false);
                var data = datagram.Payload.ToArray();
                TaskCompletionSource<byte[]>? pendingResponse;
                lock (SyncRoot)
                {
                    // 在发布事件前固定关联对象，事件处理器新建的请求不能认领当前数据报。
                    pendingResponse = _pendingResponse;
                }
                OnPacketReceived(data, datagram.RemoteEndpoint.ToString());
                pendingResponse?.TrySetResult(data);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 主动断开通过取消令牌结束接收循环。
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            // 关闭 UDP 句柄会解除底层等待。
        }
        catch (Exception ex)
        {
            EndReceiveGeneration(receiveGeneration);
            _state = ConnectionState.Faulted;
            OnErrorOccurred(ex);
            OnConnectionStateChanged(false, ex.Message);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、GetConnectedTransport、ConfigureAwait、SendAsync、OnErrorOccurred；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var transport = GetConnectedTransport();
        try
        {
            await transport.SendAsync(new Datagram(data, _remoteEndpoint!), ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnErrorOccurred(ex);
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
    public override async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
    {
        var response = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (SyncRoot)
        {
            if (_pendingResponse is not null)
            {
                throw new InvalidOperationException("UDP 客户端一次只允许一个请求等待响应。");
            }
            _pendingResponse = response;
        }

        try
        {
            if (!await SendAsync(data, ct).ConfigureAwait(false))
            {
                throw new IOException("UDP 数据报发送失败。");
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

    /// <summary>UDP 数据报没有地址读取语义；该成员仅用于旧设备接口兼容。</summary>
    public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
        throw new NotSupportedException("UDP 不支持按地址读取，请使用被动接收或请求/回复能力。");

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
            throw new IOException("UDP 客户端打开失败。");
        }
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetConnectedTransport。
    /// </summary>
    private IDatagramTransport GetConnectedTransport() =>
        _transport is { IsOpen: true } transport && _state == ConnectionState.Connected
            ? transport
            : throw new InvalidOperationException("UDP 客户端未连接。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifecycleGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
