using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Socket
{
    /// <summary>
    /// TCP 服务器设备
    /// </summary>
    public class TcpServerDevice : CommunicationDeviceBase
    {
        private new SocketParam Param => (SocketParam)base.Param;
        private TcpListener? _listener;
        private readonly List<TcpClient> _clients = new();
        private readonly Dictionary<TcpClient, Task> _clientTasks = new();
        private readonly object _clientLock = new();

        public TcpServerDevice(SocketParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Tcp;
        }
        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>已连接的客户端列表</summary>
        public IReadOnlyList<TcpClient> ConnectedClients
        {
            get { lock (_clientLock) return _clients.ToList().AsReadOnly(); }
        }

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Parse(Param.LocalIP), Param.Port);
                _listener.Start(Param.MaxConnections);
                IsConnected = true;
                OnConnectionStateChanged(true, $"TCP服务器已启动: {Param.LocalIP}:{Param.Port}");

                _receiveLoopCts?.Dispose();
                _receiveLoopCts = new CancellationTokenSource();
                _receiveLoopTask = AcceptLoopAsync(_receiveLoopCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                OnConnectionStateChanged(false, ex.Message);
                return false;
            }
        }

        public override async Task DisconnectAsync(CancellationToken ct = default)
        {
            _receiveLoopCts?.Cancel();
            _listener?.Stop();

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

            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            Task[] clientTasks;
            lock (_clientLock)
            {
                clientTasks = _clientTasks.Values.ToArray();
            }
            await Task.WhenAll(clientTasks).ConfigureAwait(false);

            IsConnected = false;
            OnConnectionStateChanged(false, "TCP服务器已停止");
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    lock (_clientLock) _clients.Add(client);
                    var ep = client.Client.RemoteEndPoint?.ToString();
                    OnConnectionStateChanged(true, $"客户端已连接: {ep}");

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
            try
            {
                using var stream = client.GetStream();
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    var read = await stream.ReadAsync(buffer, ct);
                    if (read == 0) break;

                    var data = buffer.AsSpan(0, read).ToArray();
                    var remoteAddr = client.Client.RemoteEndPoint?.ToString();
                    OnDataReceived(data, remoteAddr);
                }
            }
            catch (OperationCanceledException)
            {
                // 服务器停止时取消所有客户端读取属于正常退出。
            }
            catch (Exception ex) { OnErrorOccurred(ex); }
            finally
            {
                lock (_clientLock)
                {
                    _clients.Remove(client);
                    _clientTasks.Remove(client);
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
                OnConnectionStateChanged(false, "客户端断开");
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
            return Task.FromResult(Array.Empty<byte>());
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            return SendAsync(data, ct);
        }

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            throw new NotSupportedException("TCP服务器模式不支持SendAndReceive，请使用SendAsync广播");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _listener?.Stop();
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
        }
    }
}
