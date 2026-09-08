using System.Net;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Socket;

/// <summary>
/// UDP 服务器兼容 adapter。
/// 旧事件和发送方法保留，数据报边界由 <see cref="IDatagramTransport"/> 保证。
/// </summary>
public class UdpServerDevice : CommunicationDeviceBase, IConnection
{
    private new SocketParam Param => (SocketParam)base.Param;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IDatagramTransport? _transport;
    private ConnectionState _state = ConnectionState.Disconnected;

    /// <summary>使用 Socket 参数创建 UDP 服务器。</summary>
    public UdpServerDevice(SocketParam param) : base(param)
    {
        ProtocolType = Core.ProtocolType.Udp;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Server;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _state;

    /// <inheritdoc />
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
            _receiveLoopTask = ReceiveLoopAsync(transport, _receiveLoopCts.Token);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"UDP服务器已启动: {Param.LocalIP}:{Param.Port}");
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

    /// <inheritdoc />
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

    private async Task ReceiveLoopAsync(IDatagramTransport transport, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var datagram = await transport.ReceiveAsync(ct).ConfigureAwait(false);
                OnDataReceived(datagram.Payload.ToArray(), datagram.RemoteEndpoint.ToString());
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
    public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
        SendAsync(data, IPAddress.Broadcast.ToString(), Param.Port, ct);

    /// <inheritdoc />
    public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    /// <inheritdoc />
    public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
        SendAsync(data, ct);

    /// <inheritdoc />
    public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
        throw new NotSupportedException("UDP服务器模式不支持 SendAndReceive。");

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("UDP 服务器打开失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    private IDatagramTransport GetConnectedTransport() =>
        _transport is { IsOpen: true } transport && _state == ConnectionState.Connected
            ? transport
            : throw new InvalidOperationException("UDP 服务器未启动。");

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifecycleGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
