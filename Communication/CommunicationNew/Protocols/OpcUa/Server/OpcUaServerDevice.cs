using CommunicationNew.Core;

using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.OpcUa
{
    /// <summary>
    /// OPC UA 服务器设备
    /// 对外保留 byte[] 节点接口，对内由高层 adapter 管理 StandardServer 与地址空间。
    /// </summary>
    public class OpcUaServerDevice : CommunicationDeviceBase, IConnection
    {
        private new OpcUaParam Param => (OpcUaParam)base.Param;
        private readonly IOpcUaServerAdapter _adapter;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>使用 OPC Foundation SDK adapter 创建服务端。</summary>
        public OpcUaServerDevice(OpcUaParam param)
            : this(param, new OpcFoundationServerAdapter(param))
        {
        }

        /// <summary>使用指定高层 adapter 创建服务端，adapter 的所有权转移给设备。</summary>
        internal OpcUaServerDevice(OpcUaParam param, IOpcUaServerAdapter adapter) : base(param)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            _adapter = adapter;
            ProtocolType = Core.ProtocolType.OpcUa;
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>新连接 seam 暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

        /// <summary>已注册的节点列表</summary>
        public IReadOnlyCollection<string> Nodes => _adapter.Nodes;

        /// <summary>注册一个变量节点</summary>
        public void RegisterNode(string nodeIdString, byte[] initialValue = null!)
        {
            _adapter.RegisterNode(nodeIdString, initialValue ?? Array.Empty<byte>());
        }

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
                await _adapter.StartAsync(ct).ConfigureAwait(false);
                _state = ConnectionState.Connected;
                OnConnectionStateChanged(true, $"OPC UA服务器已启动: {Param.EndpointUrl}");
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
                await _adapter.StopAsync(ct).ConfigureAwait(false);
                _state = ConnectionState.Disconnected;
                OnConnectionStateChanged(false, "OPC UA服务器已停止");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            EnsureConnected();
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            var value = _adapter.Read(address);
            return Task.FromResult(value.AsSpan(0, Math.Min(length, value.Length)).ToArray());
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            ct.ThrowIfCancellationRequested();
            EnsureConnected();
            _adapter.Write(address, data);
            return Task.FromResult(true);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA服务器不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA服务器不支持原始收发");

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("OPC UA Server 启动失败。");
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        private void EnsureConnected()
        {
            if (_state != ConnectionState.Connected || !_adapter.IsRunning)
            {
                throw new InvalidOperationException("OPC UA Server 尚未启动。");
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
