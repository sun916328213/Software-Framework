// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：UdpServerDevice。成员说明可结合下方具体实现阅读。
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
/// UDP 服务器兼容 adapter。
/// 旧事件和发送方法保留，数据报边界由 <see cref="IDatagramTransport"/> 保证。
/// </summary>
public class UdpServerDevice : CommunicationDeviceBase, IConnection, IRawSender, IReceiveSource<ReceivedPacket>
{
    private new SocketParam Param => (SocketParam)base.Param;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IDatagramTransport? _transport;
    private ConnectionState _state = ConnectionState.Disconnected;
    private long _receiveGeneration;

    /// <summary>使用 Socket 参数创建 UDP 服务器。</summary>
    public UdpServerDevice(SocketParam param) : base(param)
    {
        ProtocolType = global::Communication.Core.ProtocolType.Udp;
        EnableReceiveSource(ReceiveOverflowPolicy.DropOldest);
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Server;

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
            var endpoint = new IPEndPoint(IPAddress.Parse(Param.LocalIP), Param.Port);
            var transport = new UdpDatagramTransport(endpoint);
            try
            {
                await transport.OpenAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await transport.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _transport = transport;
            _receiveLoopCts?.Dispose();
            _receiveLoopCts = new CancellationTokenSource();
            _receiveGeneration = BeginReceiveGeneration();
            _receiveLoopTask = ReceiveLoopAsync(transport, _receiveLoopCts.Token, _receiveGeneration);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"UDP服务器已启动: {Param.LocalIP}:{Param.Port}");
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
            OnConnectionStateChanged(false, "UDP服务器已停止");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// 处理接收操作；对应入口为 ReceiveLoopAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ReceiveAsync、OnPacketReceived、ToArray、ToString；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
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
                OnPacketReceived(datagram.Payload.ToArray(), datagram.RemoteEndpoint.ToString());
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 服务器停止通过取消令牌结束接收循环。
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

    /// <summary>向指定远端端点发送一个完整数据报。</summary>
    public async Task<bool> SendAsync(
        byte[] data,
        string remoteIP,
        int remotePort,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var transport = GetConnectedTransport();
        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(remoteIP), remotePort);
            await transport.SendAsync(new Datagram(data, endpoint), ct).ConfigureAwait(false);
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
    /// 执行发送操作；对应入口为 SendAsync。
    /// 正文中的关键协作调用包括 SendAsync、ToString；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        SendAsync(data, IPAddress.Broadcast.ToString(), Param.Port, ct);

    /// <summary>UDP 数据报没有地址读取语义；该成员仅用于旧设备接口兼容。</summary>
    public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
        throw new NotSupportedException("UDP 服务端不支持按地址读取，请使用被动接收能力。");

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 SendAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
        SendAsync(data, ct);

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAndReceiveAsync。
    /// </remarks>
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("UDP服务器模式不支持 SendAndReceive。");

    /// <inheritdoc />
    /// <remarks>
    /// 处理接收操作；对应入口为 ReceiveAsync。
    /// 正文中的关键协作调用包括 ReceivePacketAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask<ReceivedPacket> ReceiveAsync(CancellationToken cancellationToken = default) =>
        ReceivePacketAsync(cancellationToken);

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("UDP 服务器打开失败。");
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
            : throw new InvalidOperationException("UDP 服务器未启动。");

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
