using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Runtime;

namespace CommunicationNew.Protocols.Socket
{
    /// <summary>
    /// TCP 服务器设备
    /// </summary>
    public class TcpServerDevice : CommunicationDeviceBase, IConnection, IRawSender,
        IReceiveSource<ReceivedPacket>, IPeerConnectionSource
    {
        private new SocketParam Param => (SocketParam)base.Param;
        private readonly ITcpServerListener _listener;
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private readonly List<TcpClient> _clients = new();
        private readonly Dictionary<TcpClient, Task> _clientTasks = new();
        private readonly object _clientLock = new();
        private ConnectionState _state = ConnectionState.Disconnected;
        private long _receiveGeneration;

        /// <inheritdoc />
        public event EventHandler<PeerConnectionChangedEventArgs>? PeerConnectionChanged;

        /// <summary>使用系统 TCP 监听器创建服务器。</summary>
        public TcpServerDevice(SocketParam param)
            : this(param, new SystemTcpServerListener(IPAddress.Parse(param.LocalIP), param.Port))
        {
        }

        /// <summary>使用指定监听器创建服务器，监听器的所有权转移给该设备。</summary>
        internal TcpServerDevice(SocketParam param, ITcpServerListener listener) : base(param)
        {
            ArgumentNullException.ThrowIfNull(listener);
            _listener = listener;
            ProtocolType = Core.ProtocolType.Tcp;
            EnableReceiveSource(ReceiveOverflowPolicy.Reject);
        }

        /// <inheritdoc />
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>新连接边界暴露的显式生命周期状态。</summary>
        public ConnectionState State => _state;

        /// <summary>实际监听的端口号（Port=0 时由系统分配，Connect 后可读回）。</summary>
        public int LocalPort => _listener.LocalPort;

        /// <summary>已连接的客户端列表</summary>
        public IReadOnlyList<TcpClient> ConnectedClients
        {
            get { lock (_clientLock) return _clients.ToList().AsReadOnly(); }
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
                _listener.Start(Param.MaxConnections);
                _receiveLoopCts?.Dispose();
                _receiveLoopCts = new CancellationTokenSource();
                _receiveGeneration = BeginReceiveGeneration();
                _receiveLoopTask = AcceptLoopAsync(_receiveLoopCts.Token);
                _state = ConnectionState.Connected;
                OnConnectionStateChanged(true, $"TCP服务器已启动: {Param.LocalIP}:{Param.Port}");
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
                _listener.Stop();

                TcpClient[] clients;
                lock (_clientLock)
                {
                    clients = _clients.ToArray();
                    _clients.Clear();
                }
                foreach (var client in clients)
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred(ex);
                    }
                }

                // Stop 解除 accept，释放客户端解除 read；两类循环都退出后才报告已断开。
                await WaitForReceiveLoopAsync().ConfigureAwait(false);
                Task[] clientTasks;
                lock (_clientLock)
                {
                    clientTasks = _clientTasks.Values.ToArray();
                }
                await Task.WhenAll(clientTasks).ConfigureAwait(false);

                _state = ConnectionState.Disconnected;
                OnConnectionStateChanged(false, "TCP服务器已停止");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    int activePeerCount;
                    lock (_clientLock)
                    {
                        _clients.Add(client);
                        activePeerCount = _clients.Count;
                    }
                    var ep = client.Client.RemoteEndPoint?.ToString();
                    // 客户端上线不是服务端监听器的生命周期变化。
                    // 若复用 ConnectionStateChanged，单个客户端断开会让调用方误以为整个服务端已停止。
                    PeerConnectionChanged?.Invoke(
                        this,
                        new PeerConnectionChangedEventArgs(true, ep, activePeerCount));

                    var clientTask = HandleClientAsync(client, ct);
                    lock (_clientLock) _clientTasks[client] = clientTask;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { OnErrorOccurred(ex); }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            var encoding = System.Text.Encoding.GetEncoding(Param.Encoding);
            var buffer = new byte[Param.BufferSize];
            var remoteAddress = client.Client.RemoteEndPoint?.ToString();
            try
            {
                using var stream = client.GetStream();
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    var read = await stream.ReadAsync(buffer, ct);
                    if (read == 0) break;

                    var data = buffer.AsSpan(0, read).ToArray();
                    var remoteAddr = client.Client.RemoteEndPoint?.ToString();
                    OnPacketReceived(data, remoteAddr);
                }
            }
            catch (OperationCanceledException)
            {
                // 服务器停止时取消所有客户端读取属于正常退出。
            }
            catch (Exception ex) { OnErrorOccurred(ex); }
            finally
            {
                int activePeerCount;
                bool removed;
                lock (_clientLock)
                {
                    removed = _clients.Remove(client);
                    _clientTasks.Remove(client);
                    activePeerCount = _clients.Count;
                }
                try
                {
                    client.Close();
                    client.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // 服务器断开流程可能已先释放客户端。
                }
                if (removed)
                {
                    // 只报告对端下线；只要监听器未停止，IsConnected 就必须保持 true。
                    PeerConnectionChanged?.Invoke(
                        this,
                        new PeerConnectionChangedEventArgs(false, remoteAddress, activePeerCount));
                }
            }
        }

        /// <summary>向所有已连接客户端发送数据</summary>
        public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            TcpClient[] clients;
            lock (_clientLock)
            {
                clients = _clients.ToArray();
            }

            var succeeded = true;
            foreach (var client in clients)
            {
                try
                {
                    if (client.Connected)
                    {
                        await client.GetStream().WriteAsync(data, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    OnErrorOccurred(ex);
                }
            }
            return succeeded;
        }

        /// <summary>广播数据给所有客户端</summary>
        public async Task<bool> BroadcastAsync(byte[] data, CancellationToken ct = default)
        {
            return await SendAsync(data, ct);
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            // 原始 TCP 服务端没有地址空间；保留该成员仅为兼容旧设备接口。
            throw new NotSupportedException("TCP 服务端不支持按地址读取，请使用被动接收能力。");
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            return SendAsync(data, ct);
        }

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            throw new NotSupportedException("TCP服务器模式不支持SendAndReceive，请使用SendAsync广播");
        }

        /// <inheritdoc />
        public ValueTask<ReceivedPacket> ReceiveAsync(CancellationToken cancellationToken = default) =>
            ReceivePacketAsync(cancellationToken);

        async ValueTask IConnection.ConnectAsync(CancellationToken cancellationToken)
        {
            if (!await ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("TCP 服务器启动失败。");
            }
        }

        ValueTask IConnection.DisconnectAsync(CancellationToken cancellationToken) =>
            new(DisconnectAsync(cancellationToken));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _listener.Dispose();
                _lifecycleGate.Dispose();
                lock (_clientLock)
                {
                    foreach (var c in _clients)
                    {
                        c.Dispose();
                    }
                    _clients.Clear();
                }
            }
            base.Dispose(disposing);
            _state = ConnectionState.Disposed;
        }
    }
}
