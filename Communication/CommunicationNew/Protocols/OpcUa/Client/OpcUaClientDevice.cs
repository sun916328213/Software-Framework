using CommunicationNew.Core;

using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.OpcUa
{
    /// <summary>
    /// OPC UA 客户端设备
    /// 提供 Read/Write 节点能力。地址格式: "ns=2;s=MyVariable"
    /// 旧 byte[] 读写接口仅表示 OPC UA Byte/ByteString；Session 生命周期由高层 adapter 管理。
    /// </summary>
    public class OpcUaClientDevice : CommunicationDeviceBase, IConnection
    {
        private new OpcUaParam Param => (OpcUaParam)base.Param;
        private readonly IOpcUaClientAdapter _adapter;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>使用 OPC Foundation SDK adapter 创建客户端。</summary>
        public OpcUaClientDevice(OpcUaParam param)
            : this(param, new OpcFoundationClientAdapter(param))
        {
        }

        /// <summary>使用指定高层 adapter 创建客户端，adapter 的所有权转移给设备。</summary>
        internal OpcUaClientDevice(OpcUaParam param, IOpcUaClientAdapter adapter) : base(param)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            _adapter = adapter;
            ProtocolType = Core.ProtocolType.OpcUa;
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Client;

        /// <summary>新连接 seam 暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

        /// <summary>端点URL</summary>
        public string EndpointUrl => Param.EndpointUrl;

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
                await _adapter.ConnectAsync(ct).ConfigureAwait(false);
                _state = ConnectionState.Connected;
                OnConnectionStateChanged(true, $"OPC UA客户端已连接: {Param.EndpointUrl}");
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
                await _adapter.DisconnectAsync(ct).ConfigureAwait(false);
                _state = ConnectionState.Disconnected;
                OnConnectionStateChanged(false, "OPC UA客户端已断开");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public override async Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            EnsureConnected();
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            var value = await _adapter.ReadAsync(address, ct).ConfigureAwait(false);
            return value.AsSpan(0, Math.Min(length, value.Length)).ToArray();
        }

        public override async Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            EnsureConnected();
            await _adapter.WriteAsync(address, data, ct).ConfigureAwait(false);
            return true;
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA 不支持原始字节发送，请使用 ReadAsync/WriteAsync");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA 不支持原始收发，请使用 ReadAsync/WriteAsync");

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("OPC UA 客户端连接失败。");
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        private void EnsureConnected()
        {
            if (_state != ConnectionState.Connected || !_adapter.IsConnected)
            {
                throw new InvalidOperationException("OPC UA 客户端尚未连接。");
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _lifecycleGate.Dispose();
            }
            base.Dispose(disposing);
            _state = ConnectionState.Disposed;
        }
    }
}
