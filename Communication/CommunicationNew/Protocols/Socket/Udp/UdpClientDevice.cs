using System.Net;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Socket;

/// <summary>
/// UDP 客户端兼容 adapter。
/// 旧设备接口、事件与参数模型保留，数据报生命周期由 <see cref="IDatagramTransport"/> 管理。
/// </summary>
public class UdpClientDevice : CommunicationDeviceBase, IConnection
{
    private new SocketParam Param => (SocketParam)base.Param;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IDatagramTransport? _transport;
    private IPEndPoint? _remoteEndpoint;
    private TaskCompletionSource<byte[]>? _pendingResponse;
    private ConnectionState _state = ConnectionState.Disconnected;

    /// <summary>使用 Socket 参数创建 UDP 客户端。</summary>
    public UdpClientDevice(SocketParam param) : base(param)
    {
        ProtocolType = Core.ProtocolType.Udp;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

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
            _receiveLoopTask = ReceiveLoopAsync(transport, _receiveLoopCts.Token);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"UDP已绑定到 {Param.ServerIP}:{Param.Port}");
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
            OnConnectionStateChanged(false, "UDP已关闭");
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
                var data = datagram.Payload.ToArray();
                TaskCompletionSource<byte[]>? pendingResponse;
                lock (SyncRoot)
                {
                    // 在发布事件前固定关联对象，事件处理器新建的请求不能认领当前数据报。
                    pendingResponse = _pendingResponse;
                }
                OnDataReceived(data, datagram.RemoteEndpoint.ToString());
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
            _state = ConnectionState.Faulted;
            OnErrorOccurred(ex);
            OnConnectionStateChanged(false, ex.Message);
        }
    }

    /// <inheritdoc />
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
    public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    /// <inheritdoc />
    public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
        SendAsync(data, ct);

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("UDP 客户端打开失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    private IDatagramTransport GetConnectedTransport() =>
        _transport is { IsOpen: true } transport && _state == ConnectionState.Connected
            ? transport
            : throw new InvalidOperationException("UDP 客户端未连接。");

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
