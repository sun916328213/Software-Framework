using System.Buffers;
using System.Text;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Socket;

/// <summary>
/// TCP 客户端兼容 adapter。
/// 旧事件和原始收发接口保留，连接与连续字节流由 <see cref="IByteStreamTransport"/> 管理。
/// </summary>
public class TcpClientDevice : CommunicationDeviceBase, IConnection
{
    private new SocketParam Param => (SocketParam)base.Param;
    private readonly IByteStreamTransport _transport;
    private readonly ReconnectSupervisor? _reconnectSupervisor;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly StringBuilder _receiveText = new();
    private TaskCompletionSource<byte[]>? _pendingResponse;
    private ConnectionState _state = ConnectionState.Disconnected;
    private Encoding _encoding = Encoding.UTF8;
    private volatile bool _desiredConnected;

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
        ProtocolType = Core.ProtocolType.Tcp;
    }

    /// <inheritdoc />
    public override DeviceRole Role => DeviceRole.Client;

    /// <summary>新连接 seam 暴露的显式状态。</summary>
    public ConnectionState State => _state;

    /// <inheritdoc />
    public override async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        _desiredConnected = true;
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_state == ConnectionState.Connected)
            {
                return true;
            }

            var resetTransport = _state == ConnectionState.Faulted;
            _state = ConnectionState.Connecting;
            _encoding = Encoding.GetEncoding(Param.Encoding);
            if (resetTransport)
            {
                // 远端关闭后 TcpClient 可能仍报告 Connected；必须丢弃旧 Pipe 和句柄。
                await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            await _transport.OpenAsync(ct).ConfigureAwait(false);
            _receiveLoopCts?.Dispose();
            _receiveLoopCts = new CancellationTokenSource();
            _receiveLoopTask = ReceiveLoopAsync(_receiveLoopCts.Token);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(true, $"已连接 {Param.ServerIP}:{Param.Port}");
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
        _desiredConnected = false;
        if (_reconnectSupervisor is not null)
        {
            await _reconnectSupervisor.StopAsync().ConfigureAwait(false);
        }

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
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

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
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
                    OnDataReceived(data, $"{Param.ServerIP}:{Param.Port}");
                    PublishDelimitedFrames(data);
                }
                _transport.Input.AdvanceTo(buffer.End);
                if (result.IsCompleted)
                {
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
            OnErrorOccurred(ex);
        }
        finally
        {
            if (!ct.IsCancellationRequested && _state == ConnectionState.Connected)
            {
                _state = ConnectionState.Faulted;
                OnConnectionStateChanged(false, "远端连接已断开");
                // Runtime supervisor 持有恢复循环；手工断开会把期望状态改为离线。
                _reconnectSupervisor?.Start();
            }
        }
    }

    private void PublishDelimitedFrames(byte[] data)
    {
        if (string.IsNullOrEmpty(Param.Delimiter))
        {
            return;
        }

        _receiveText.Append(_encoding.GetString(data));
        while (true)
        {
            var text = _receiveText.ToString();
            var index = text.IndexOf(Param.Delimiter, StringComparison.Ordinal);
            if (index < 0)
            {
                return;
            }
            var frame = text[..index];
            OnDataReceived(_encoding.GetBytes(frame), $"{Param.ServerIP}:{Param.Port}");
            _receiveText.Remove(0, index + Param.Delimiter.Length);
        }
    }

    /// <inheritdoc />
    public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureConnected();
        try
        {
            await _transport.Output.WriteAsync(data, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnErrorOccurred(ex);
            return false;
        }
    }

    /// <inheritdoc />
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
                throw new IOException("TCP 数据发送失败。");
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
            throw new IOException("TCP 客户端连接失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    private void EnsureConnected()
    {
        if (_state != ConnectionState.Connected || !_transport.IsOpen)
        {
            throw new InvalidOperationException("TCP 客户端未连接。");
        }
    }

    /// <inheritdoc />
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
