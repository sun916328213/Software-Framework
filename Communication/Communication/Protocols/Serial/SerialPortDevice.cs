using System.Buffers;
using System.Text;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;

namespace CommunicationNew.Protocols.Serial;

/// <summary>
/// 串口设备兼容 adapter。
/// 旧事件和原始收发接口保留，端口生命周期与连续字节流由 transport 管理。
/// </summary>
public class SerialPortDevice : CommunicationDeviceBase, IConnection, IRawSender, IRequestReplyClient, IReceiveSource<ReceivedPacket>
{
    private new SerialParam Param => (SerialParam)base.Param;
    private readonly IByteStreamTransport _transport;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly StringBuilder _receiveText = new();
    private TaskCompletionSource<byte[]>? _pendingResponse;
    private ConnectionState _state = ConnectionState.Disconnected;
    private long _receiveGeneration;

    /// <summary>使用 SerialParam 创建生产串口 transport。</summary>
    public SerialPortDevice(SerialParam param)
        : this(param, CreateTransport(param))
    {
    }

    /// <summary>
    /// 使用指定字节流 transport 创建串口设备；设备释放时会一并释放 transport。
    /// </summary>
    public SerialPortDevice(SerialParam param, IByteStreamTransport transport) : base(param)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        ProtocolType = Core.ProtocolType.Serial;
        EnableReceiveSource(ReceiveOverflowPolicy.Reject);
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
            await _transport.OpenAsync(ct).ConfigureAwait(false);
            _receiveLoopCts?.Dispose();
            _receiveLoopCts = new CancellationTokenSource();
            _receiveGeneration = BeginReceiveGeneration();
            _receiveLoopTask = ReceiveLoopAsync(_receiveLoopCts.Token, _receiveGeneration);
            _state = ConnectionState.Connected;
            OnConnectionStateChanged(
                true,
                $"串口 {Param.PortName} 已打开 ({Param.BaudRate},{Param.DataBits},{Param.Parity},{Param.StopBits})");
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
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            _state = ConnectionState.Disconnected;
            OnConnectionStateChanged(false, $"串口 {Param.PortName} 已关闭");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct, long receiveGeneration)
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
                    OnPacketReceived(data, Param.PortName);
                    PublishDelimitedFrames(data);
                    pendingResponse?.TrySetResult(data);
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
            // 关闭串口 BaseStream 会解除底层等待。
        }
        catch (Exception ex)
        {
            EndReceiveGeneration(receiveGeneration);
            _state = ConnectionState.Faulted;
            OnErrorOccurred(ex);
            OnConnectionStateChanged(false, ex.Message);
        }
    }

    private void PublishDelimitedFrames(byte[] data)
    {
        if (string.IsNullOrEmpty(Param.Delimiter))
        {
            return;
        }

        _receiveText.Append(Encoding.UTF8.GetString(data));
        while (true)
        {
            var text = _receiveText.ToString();
            var index = text.IndexOf(Param.Delimiter, StringComparison.Ordinal);
            if (index < 0)
            {
                return;
            }
            var frame = text[..index];
            OnDataReceived(Encoding.UTF8.GetBytes(frame), Param.PortName);
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
                throw new InvalidOperationException("串口一次只允许一个请求等待响应。");
            }
            _pendingResponse = response;
        }

        try
        {
            if (!await SendAsync(data, ct).ConfigureAwait(false))
            {
                throw new IOException("串口数据发送失败。");
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
    public ValueTask<ReceivedPacket> ReceiveAsync(CancellationToken cancellationToken = default) =>
        ReceivePacketAsync(cancellationToken);

    /// <summary>串口没有地址读取语义；该成员仅用于旧设备接口兼容。</summary>
    public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
        throw new NotSupportedException("串口不支持按地址读取，请使用被动接收或请求/回复能力。");

    /// <inheritdoc />
    public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
        SendAsync(data, ct);

    async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
    {
        if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new IOException($"串口 {Param.PortName} 打开失败。");
        }
    }

    ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
        new(DisconnectAsync(cancellationToken));

    private void EnsureConnected()
    {
        if (_state != ConnectionState.Connected || !_transport.IsOpen)
        {
            throw new InvalidOperationException("串口设备未连接。");
        }
    }

    private static SerialByteStreamTransport CreateTransport(SerialParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        return new SerialByteStreamTransport(new SerialTransportOptions(param.PortName, param.BaudRate)
        {
            DataBits = param.DataBits,
            StopBits = param.StopBits,
            Parity = param.Parity,
            Handshake = param.Handshake,
            BufferSize = param.BufferSize,
            TimeoutMs = param.TimeoutMs,
            DtrEnable = param.DtrEnable,
            RtsEnable = param.RtsEnable,
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _lifecycleGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
