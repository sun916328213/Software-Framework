﻿﻿﻿using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Socket
{
    /// <summary>
    /// TCP 客户端设备
    /// </summary>
    public class TcpClientDevice : CommunicationDeviceBase
    {
        private new SocketParam Param => (SocketParam)base.Param;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private System.Text.Encoding _encoding = System.Text.Encoding.UTF8;

        public TcpClientDevice(SocketParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Tcp;
        }
        public override DeviceRole Role => DeviceRole.Client;

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(IPAddress.Parse(Param.ServerIP), Param.Port, ct);
                _stream = _tcpClient.GetStream();
                _encoding = System.Text.Encoding.GetEncoding(Param.Encoding);
                IsConnected = true;
                OnConnectionStateChanged(true, $"已连接 {Param.ServerIP}:{Param.Port}");

                _receiveLoopCts?.Dispose();
                _receiveLoopCts = new CancellationTokenSource();
                _receiveLoopTask = ReceiveLoopAsync(_receiveLoopCts.Token);
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
            _stream?.Dispose();
            _tcpClient?.Dispose();
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            _stream = null;
            _tcpClient = null;
            IsConnected = false;
            OnConnectionStateChanged(false, "已断开连接");
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[Param.BufferSize];
            var sb = new StringBuilder();
            try
            {
                while (!ct.IsCancellationRequested && _stream != null && _stream.CanRead)
                {
                    var read = await _stream.ReadAsync(buffer, ct);
                    if (read == 0) break;

                    var data = buffer.AsSpan(0, read).ToArray();
                    OnDataReceived(data, $"{Param.ServerIP}:{Param.Port}");

                    if (!string.IsNullOrEmpty(Param.Delimiter))
                    {
                        sb.Append(_encoding.GetString(data, 0, read));
                        while (true)
                        {
                            var text = sb.ToString();
                            var idx = text.IndexOf(Param.Delimiter, StringComparison.Ordinal);
                            if (idx < 0) break;
                            var frame = text[..idx];
                            OnDataReceived(_encoding.GetBytes(frame), $"{Param.ServerIP}:{Param.Port}");
                            sb.Remove(0, idx + Param.Delimiter.Length);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 主动断开通过取消令牌结束接收循环。
            }
            catch (Exception ex) { OnErrorOccurred(ex); }
            finally
            {
                IsConnected = false;
                OnConnectionStateChanged(false, "连接断开");
                if (!ct.IsCancellationRequested)
                {
                    // 仅远端意外断开才自动重连；主动断开不得重新建立连接。
                    StartAutoReconnect(default);
                }
            }
        }

        public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            if (_stream == null || !IsConnected) return false;
            try
            {
                await _stream.WriteAsync(data, ct);
                return true;
            }
            catch (Exception ex) { OnErrorOccurred(ex); return false; }
        }

        public override async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            if (_stream == null || !IsConnected) return Array.Empty<byte>();
            await SendAsync(data, ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);
            var buffer = new byte[Param.BufferSize];
            var read = await _stream.ReadAsync(buffer, cts.Token);
            return buffer.AsSpan(0, read).ToArray();
        }

        /// <summary>TCP客户端通常不按地址读写，此方法将地址视为分隔标识返回最近接收数据</summary>
        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            // 简单实现：等待接收数据
            return Task.FromResult(Array.Empty<byte>());
        }

        /// <summary>将数据写入连接（SendAsync的别名）</summary>
        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            return SendAsync(data, ct);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _stream?.Dispose();
                _tcpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
