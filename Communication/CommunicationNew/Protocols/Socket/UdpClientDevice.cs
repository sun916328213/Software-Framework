﻿﻿﻿using System.Net;
using System.Net.Sockets;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Socket
{
    /// <summary>
    /// UDP 客户端设备
    /// </summary>
    public class UdpClientDevice : CommunicationDeviceBase
    {
        private new SocketParam Param => (SocketParam)base.Param;
        private UdpClient? _udpClient;
        private IPEndPoint? _remoteEndPoint;

        public UdpClientDevice(SocketParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Udp;
        }
        public override DeviceRole Role => DeviceRole.Client;

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _udpClient = new UdpClient(Param.LocalPort);
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(Param.ServerIP), Param.Port);
                _udpClient.Connect(_remoteEndPoint);
                IsConnected = true;
                OnConnectionStateChanged(true, $"UDP已绑定到 {Param.ServerIP}:{Param.Port}");

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
            OnConnectionStateChanged(false, "UDP已关闭");
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
                // 主动断开通过取消令牌结束接收循环。
            }
            catch (Exception ex) { OnErrorOccurred(ex); }
        }

        public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            if (_udpClient == null || !IsConnected) return false;
            try
            {
                await _udpClient.SendAsync(data, ct);
                return true;
            }
            catch (Exception ex) { OnErrorOccurred(ex); return false; }
        }

        public override async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            if (_udpClient == null || !IsConnected) return Array.Empty<byte>();
            await SendAsync(data, ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);
            var result = await _udpClient.ReceiveAsync(cts.Token);
            return result.Buffer;
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

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
                _udpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
