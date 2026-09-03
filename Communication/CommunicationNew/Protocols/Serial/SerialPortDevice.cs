﻿﻿﻿using System.IO.Ports;
using System.Text;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Serial
{
    /// <summary>
    /// 串口通信设备
    /// </summary>
    public class SerialPortDevice : CommunicationDeviceBase
    {
        private new SerialParam Param => (SerialParam)base.Param;
        private System.IO.Ports.SerialPort? _serialPort;
        private readonly StringBuilder _recvBuffer = new();

        public SerialPortDevice(SerialParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.Serial;
        }
        public override DeviceRole Role => DeviceRole.Client;

        public override Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _serialPort = new System.IO.Ports.SerialPort(
                    Param.PortName, Param.BaudRate, Param.Parity, Param.DataBits)
                {
                    StopBits = Param.StopBits,
                    Handshake = Param.Handshake,
                    ReadBufferSize = Param.BufferSize,
                    WriteBufferSize = Param.BufferSize,
                    ReadTimeout = TimeoutMs,
                    WriteTimeout = TimeoutMs,
                    DtrEnable = Param.DtrEnable,
                    RtsEnable = Param.RtsEnable
                };

                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.ErrorReceived += OnSerialErrorReceived;
                _serialPort.Open();

                IsConnected = true;
                OnConnectionStateChanged(true, $"串口 {Param.PortName} 已打开 ({Param.BaudRate},{Param.DataBits},{Param.Parity},{Param.StopBits})");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                OnConnectionStateChanged(false, ex.Message);
                return Task.FromResult(false);
            }
        }

        public override Task DisconnectAsync(CancellationToken ct = default)
        {
            if (_serialPort != null)
            {
                _serialPort.DataReceived -= OnSerialDataReceived;
                _serialPort.ErrorReceived -= OnSerialErrorReceived;
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            IsConnected = false;
            OnConnectionStateChanged(false, $"串口 {Param.PortName} 已关闭");
            return Task.CompletedTask;
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            var bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead <= 0) return;

            var buffer = new byte[bytesToRead];
            _serialPort.Read(buffer, 0, bytesToRead);
            OnDataReceived(buffer, Param.PortName);

            if (!string.IsNullOrEmpty(Param.Delimiter))
            {
                _recvBuffer.Append(Encoding.UTF8.GetString(buffer));
                while (true)
                {
                    var text = _recvBuffer.ToString();
                    var idx = text.IndexOf(Param.Delimiter, StringComparison.Ordinal);
                    if (idx < 0) break;
                    var frame = text[..idx];
                    OnDataReceived(Encoding.UTF8.GetBytes(frame), Param.PortName);
                    _recvBuffer.Remove(0, idx + Param.Delimiter.Length);
                }
            }
        }

        private void OnSerialErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            OnErrorOccurred(new InvalidOperationException($"串口错误: {e.EventType}"));
        }

        public override async Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return false;
            try
            {
                await Task.Run(() => _serialPort.Write(data, 0, data.Length), ct);
                return true;
            }
            catch (Exception ex) { OnErrorOccurred(ex); return false; }
        }

        public override async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return Array.Empty<byte>();

            _recvBuffer.Clear();
            await SendAsync(data, ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            while (!cts.IsCancellationRequested)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    await Task.Delay(50, ct); // 等待数据完整到达
                    var len = _serialPort.BytesToRead;
                    var buffer = new byte[len];
                    _serialPort.Read(buffer, 0, len);
                    return buffer;
                }
                await Task.Delay(10, ct);
            }
            throw new TimeoutException($"串口 {Param.PortName} 等待回复超时");
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return Task.FromResult(Array.Empty<byte>());
            var toRead = Math.Min(length, _serialPort.BytesToRead);
            if (toRead <= 0) return Task.FromResult(Array.Empty<byte>());
            var buffer = new byte[toRead];
            _serialPort.Read(buffer, 0, toRead);
            return Task.FromResult(buffer);
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            return SendAsync(data, ct);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= OnSerialDataReceived;
                    _serialPort.ErrorReceived -= OnSerialErrorReceived;
                    if (_serialPort.IsOpen) _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
