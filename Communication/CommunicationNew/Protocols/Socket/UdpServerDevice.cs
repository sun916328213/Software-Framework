﻿﻿﻿using System.Net;
using System.Net.Sockets;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Socket
{
    /// <summary>
    /// UDP 服务器设备
    /// </summary>
    public class UdpServerDevice : CommunicationDeviceBase
    {
        private new SocketParam Param => (SocketParam)base.Param;
        private UdpClient? _udpClient;

        public UdpServerDevice(SocketParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Udp;
        }
        public override DeviceRole Role => DeviceRole.Server;

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                var localEP = new IPEndPoint(IPAddress.Parse(Param.LocalIP), Param.Port);
                _udpClient = new UdpClient(localEP);
                IsConnected = true;
                OnConnectionStateChanged(true, $"UDP服务器已启动: {Param.LocalIP}:{Param.Port}");

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
            _udpClient?.Dispose();
            await WaitForReceiveLoopAsync().ConfigureAwait(false);
            _udpClient = null;
            IsConnected = false;
            OnConnectionStateChanged(false, "UDP服务器已停止");
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync(ct);
                    OnDataReceived(result.Buffer, result.RemoteEndPoint?.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                // 服务器停止通过取消令牌结束接收循环。
            }
            catch (Exception ex) { OnErrorOccurred(ex); }
        }

        /// <summary>向指定远程端点发送数据</summary>
        public async Task<bool> SendAsync(byte[] data, string remoteIP, int remotePort, CancellationToken ct = default)
        {
            if (_udpClient == null) return false;
            try
            {
                var ep = new IPEndPoint(IPAddress.Parse(remoteIP), remotePort);
                await _udpClient.SendAsync(data, ep, ct);
                return true;
            }
            catch (Exception ex) { OnErrorOccurred(ex); return false; }
        }

        public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            // UDP服务器模式下，无固定远程端点，需使用带地址的重载
            // 此方法广播到子网
            if (_udpClient == null) return false;
            try
            {
                await _udpClient.SendAsync(data, new IPEndPoint(IPAddress.Broadcast, Param.Port), ct);
                return true;
            }
            catch (Exception ex) { OnErrorOccurred(ex); return false; }
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
            throw new NotSupportedException("UDP服务器模式不支持SendAndReceive");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _udpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
