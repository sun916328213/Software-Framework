// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusTcpServerDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;

namespace Communication.Runtime.Protocols.Modbus.ModbusTcp
{
    /// <summary>
    /// Modbus TCP 服务器（从站）兼容 adapter。
    /// 对外保留旧设备接口，对内通过 server adapter 隔离 FluentModbus 和监听资源。
    /// </summary>
    public class ModbusTcpServerDevice : CommunicationDeviceBase, IConnection, IAddressReader, IAddressWriter
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
            ProtocolType = global::Communication.Core.ProtocolType.ModbusTcp;
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>新连接 seam 暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

        /// <summary>
        /// 发起连接操作；对应入口为 ConnectAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIf、ThrowIfCancellationRequested、Start；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Stop、OnConnectionStateChanged、Release；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
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

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadAsync。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、EnsureConnected、FromResult、Read；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            EnsureConnected();
            return Task.FromResult(_adapter.Read(address, length));
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// 正文中的关键协作调用包括 ThrowIfNull、ThrowIfCancellationRequested、EnsureConnected、Write、FromResult；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行发送操作；对应入口为 SendAsync。
        /// </summary>
        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus TCP从站不支持原始字节发送");

        /// <summary>
        /// 执行发送操作；对应入口为 SendAndReceiveAsync。
        /// </summary>
        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus TCP从站不支持原始收发");

        /// <summary>
        /// 发起连接操作；对应入口为 ConnectAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("Modbus TCP 从站启动失败。");
            }
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectAsync。
        /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
        /// </summary>
        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureConnected。
        /// </summary>
        private void EnsureConnected()
        {
            if (_state != ConnectionState.Connected || !_adapter.IsRunning)
            {
                throw new InvalidOperationException("Modbus TCP 从站尚未启动。");
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
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
