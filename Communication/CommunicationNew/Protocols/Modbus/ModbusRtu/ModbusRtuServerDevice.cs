using System.IO.Ports;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Modbus.ModbusRtu
{
    /// <summary>
    /// Modbus RTU 服务器（从站）兼容 adapter。
    /// 对外保留旧设备接口，对内通过 server adapter 隔离 FluentModbus 和串口资源。
    /// </summary>
    public class ModbusRtuServerDevice : CommunicationDeviceBase, IConnection
    {
        private new ModbusRtuParam Param => (ModbusRtuParam)base.Param;
        private readonly IModbusRtuServerAdapter _adapter;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>使用 FluentModbus 生产 adapter 创建 RTU 从站。</summary>
        public ModbusRtuServerDevice(ModbusRtuParam param)
            : this(param, new FluentModbusRtuServerAdapter(param))
        {
        }

        /// <summary>使用指定 adapter 创建 RTU 从站，adapter 的所有权转移给设备。</summary>
        internal ModbusRtuServerDevice(
            ModbusRtuParam param,
            IModbusRtuServerAdapter adapter) : base(param)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            _adapter = adapter;
            ProtocolType = Core.ProtocolType.ModbusRtu;
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
                OnConnectionStateChanged(true, $"Modbus RTU从站已启动: {Param.PortName} (SlaveId={Param.SlaveId})");
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
                OnConnectionStateChanged(false, "Modbus RTU从站已停止");
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
            return Task.FromResult(_adapter.Read(address, length));
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
            => throw new NotSupportedException("Modbus RTU从站不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus RTU从站不支持原始收发");

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("Modbus RTU 从站启动失败。");
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        private void EnsureConnected()
        {
            if (_state != ConnectionState.Connected || !_adapter.IsRunning)
            {
                throw new InvalidOperationException("Modbus RTU 从站尚未启动。");
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
