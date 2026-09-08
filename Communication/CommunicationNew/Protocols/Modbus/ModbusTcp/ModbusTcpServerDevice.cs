using CommunicationNew.Abstractions;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Modbus.ModbusTcp
{
    /// <summary>
    /// Modbus TCP 服务器（从站）兼容 adapter。
    /// 对外保留旧设备接口，对内通过 server adapter 隔离 FluentModbus 和监听资源。
    /// </summary>
    public class ModbusTcpServerDevice : CommunicationDeviceBase, IConnection
    {
        private new ModbusTcpParam Param => (ModbusTcpParam)base.Param;
        private readonly IModbusTcpServerAdapter _adapter;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>使用 FluentModbus 生产 adapter 创建 TCP 从站。</summary>
        public ModbusTcpServerDevice(ModbusTcpParam param)
            : this(param, new FluentModbusTcpServerAdapter(param))
        {
        }

        /// <summary>使用指定 adapter 创建 TCP 从站，adapter 的所有权转移给设备。</summary>
        internal ModbusTcpServerDevice(
            ModbusTcpParam param,
            IModbusTcpServerAdapter adapter) : base(param)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            _adapter = adapter;
            ProtocolType = Core.ProtocolType.ModbusTcp;
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>新连接 seam 暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

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

                ct.ThrowIfCancellationRequested();
                _state = ConnectionState.Connecting;
                _adapter.Start();
                _state = ConnectionState.Connected;
                OnConnectionStateChanged(
                    true,
                    $"Modbus TCP服务器已启动: 0.0.0.0:{Param.Port} (SlaveId={Param.SlaveId})");
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
                _adapter.Stop();
                _state = ConnectionState.Disconnected;
                OnConnectionStateChanged(false, "Modbus TCP服务器已停止");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public override Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            EnsureConnected();
            return Task.FromResult(_adapter.Read(address, length));
        }

        public override Task<bool> WriteAsync(
            string address,
            byte[] data,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            ct.ThrowIfCancellationRequested();
            EnsureConnected();
            _adapter.Write(address, data);
            return Task.FromResult(true);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus TCP从站不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus TCP从站不支持原始收发");

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("Modbus TCP 从站启动失败。");
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        private void EnsureConnected()
        {
            if (_state != ConnectionState.Connected || !_adapter.IsRunning)
            {
                throw new InvalidOperationException("Modbus TCP 从站尚未启动。");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _adapter.Dispose();
                _lifecycleGate.Dispose();
            }
            base.Dispose(disposing);
            _state = ConnectionState.Disposed;
        }
    }
}
