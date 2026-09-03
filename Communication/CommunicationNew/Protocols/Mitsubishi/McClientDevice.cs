﻿﻿﻿using System.Net.Sockets;
using System.Text;
using CommunicationNew.Core;

namespace CommunicationNew.Protocols.Mitsubishi
{
    /// <summary>
    /// 三菱 MC 协议客户端设备
    /// 支持 MC Binary 3E/4E 帧格式 over TCP
    /// 地址格式: "D100" / "M10" / "T5" / "C20" / "W0" / "R100"
    /// </summary>
    public class McClientDevice : CommunicationDeviceBase
    {
        private new McParam Param => (McParam)base.Param;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;

        public McClientDevice(McParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.MitsubishiMc;
        }
        public override DeviceRole Role => DeviceRole.Client;

        public override async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(System.Net.IPAddress.Parse(Param.ServerIP), Param.Port, ct);
                _stream = _tcpClient.GetStream();
                _stream.ReadTimeout = Param.CommunicationTimeout;
                _stream.WriteTimeout = Param.CommunicationTimeout;

                IsConnected = true;
                OnConnectionStateChanged(true, $"MC协议客户端已连接: {Param.ServerIP}:{Param.Port} (帧类型:{Param.FrameType})");
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
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _stream = null;
            _tcpClient = null;
            IsConnected = false;
            OnConnectionStateChanged(false, "MC协议客户端已断开");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 读取数据
        /// address: 设备地址，如 "D100", "M10"
        /// length: 字数(Word)或位数量(Bit)
        /// </summary>
        public override async Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (_stream == null || !IsConnected) throw new InvalidOperationException("设备未连接");

            return await ExecuteWithRetryAsync(async () =>
            {
                var (deviceType, deviceNo, isBit) = ParseAddress(address);
                var isWord = !isBit;

                // 构建MC请求帧
                var request = BuildReadRequest(deviceType, deviceNo, (ushort)length, isWord);
                var response = await SendFrameAsync(request, ct);

                // 解析响应
                return ParseReadResponse(response, (ushort)length, isWord);
            }, ct);
        }

        /// <summary>
        /// 写入数据
        /// address: 设备地址
        /// data: 字数据(大端序)或位数据
        /// </summary>
        public override async Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (_stream == null || !IsConnected) throw new InvalidOperationException("设备未连接");

            return await ExecuteWithRetryAsync(async () =>
            {
                var (deviceType, deviceNo, isBit) = ParseAddress(address);
                var isWord = !isBit;
                var length = isWord ? (ushort)(data.Length / 2) : (ushort)data.Length;

                var request = BuildWriteRequest(deviceType, deviceNo, length, data, isWord);
                var response = await SendFrameAsync(request, ct);

                // 检查响应是否成功
                return response.Length >= 2 && response[0] == 0x00 && response[1] == 0x00;
            }, ct);
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
            var buffer = new byte[4096];
            var read = await _stream.ReadAsync(buffer, cts.Token);
            return buffer.AsSpan(0, read).ToArray();
        }

        // ===== MC 协议帧构建 =====

        private byte[] BuildReadRequest(byte deviceType, int deviceNo, ushort count, bool isWord)
        {
            // 命令: 0401(位读) / 0403(字读)
            var command = isWord ? McCommand.ReadWord : McCommand.ReadBit;

            // 子命令: 0000(单点/连续)
            var subCommand = new byte[] { 0x00, 0x00 };

            // 设备地址(3字节)
            var deviceAddr = BuildDeviceAddress(deviceNo);

            // 设备代码(1字节)
            var deviceCode = deviceType;

            // 读取点数(2字节)
            var countBytes = BitConverter.GetBytes(count);

            // 数据部
            var dataPart = new List<byte>();
            dataPart.AddRange(HexToBytes(command));  // 命令 2字节
            dataPart.AddRange(subCommand);           // 子命令 2字节
            dataPart.AddRange(deviceAddr);           // 起始地址 3字节
            dataPart.Add(deviceCode);                // 设备代码 1字节
            dataPart.AddRange(countBytes);           // 点数 2字节

            return BuildFrame(dataPart.ToArray());
        }

        private byte[] BuildWriteRequest(byte deviceType, int deviceNo, ushort count, byte[] writeData, bool isWord)
        {
            // 命令: 1401(位写) / 1403(字写)
            var command = isWord ? McCommand.WriteWord : McCommand.WriteBit;

            var subCommand = new byte[] { 0x00, 0x01 }; // 连续写入

            var deviceAddr = BuildDeviceAddress(deviceNo);
            var deviceCode = deviceType;
            var countBytes = BitConverter.GetBytes(count);

            var dataPart = new List<byte>();
            dataPart.AddRange(HexToBytes(command));
            dataPart.AddRange(subCommand);
            dataPart.AddRange(deviceAddr);
            dataPart.Add(deviceCode);
            dataPart.AddRange(countBytes);
            dataPart.AddRange(writeData); // 写入数据

            return BuildFrame(dataPart.ToArray());
        }

        private byte[] BuildFrame(byte[] dataPart)
        {
            var frame = new List<byte>();

            if (Param.FrameType == "4E")
            {
                // 4E帧: 子标题(2) + 网络编号(1) + PLC编号(1) + 请求目标模块IO(2) + 请求目标模块站号(1)
                frame.AddRange(new byte[] { 0x54, 0x00 }); // 子标题
                frame.Add(Param.NetworkNo);                 // 网络编号
                frame.Add(Param.StationNo);                 // PLC编号
                frame.AddRange(BitConverter.GetBytes(Param.ModuleNo)); // 模块IO
                frame.Add(0x00);                             // 模块站号
            }
            // 3E帧: 子标题(2) + 网络编号(1) + PLC编号(1) + 请求目标模块IO(2) + 请求目标模块站号(1)

            // 请求数据长度(2字节, 小端)
            frame.AddRange(BitConverter.GetBytes((ushort)dataPart.Length));

            // 保留(2字节)
            frame.AddRange(new byte[] { 0x00, 0x00 });

            // 数据部
            frame.AddRange(dataPart);

            return frame.ToArray();
        }

        private async Task<byte[]> SendFrameAsync(byte[] request, CancellationToken ct)
        {
            await _stream!.WriteAsync(request, ct);

            // 读取响应
            var headerBuffer = new byte[Param.FrameType == "4E" ? 15 : 9];
            await _stream.ReadExactlyAsync(headerBuffer, ct);

            // 从头部提取数据长度
            var dataLength = BitConverter.ToUInt16(headerBuffer, headerBuffer.Length - 4);

            var dataBuffer = new byte[dataLength];
            await _stream.ReadExactlyAsync(dataBuffer, ct);

            // 跳过结束码(2字节)后的数据
            if (dataBuffer.Length >= 2)
            {
                var endCode = (dataBuffer[0] << 8) | dataBuffer[1];
                if (endCode != 0)
                {
                    throw new InvalidOperationException($"MC协议错误码: 0x{endCode:X4}");
                }
                return dataBuffer[2..];
            }
            return dataBuffer;
        }

        private byte[] ParseReadResponse(byte[] responseData, ushort count, bool isWord)
        {
            // 响应数据部 = 结束码(2) + 数据
            // responseData 已经去掉了结束码
            return responseData;
        }

        // ===== 地址解析 =====

        private (byte deviceType, int deviceNo, bool isBit) ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("地址不能为空");

            var addr = address.Trim().ToUpperInvariant();

            // 解析设备类型
            (byte type, bool isBit) = addr switch
            {
                _ when addr.StartsWith("M")  => ((byte)McDeviceType.M, true),
                _ when addr.StartsWith("L")  => ((byte)McDeviceType.L, true),
                _ when addr.StartsWith("B")  => ((byte)McDeviceType.B, true),
                _ when addr.StartsWith("S")  => ((byte)McDeviceType.S, true),
                _ when addr.StartsWith("X")  => ((byte)0x9C, true),
                _ when addr.StartsWith("Y")  => ((byte)0x9D, true),
                _ when addr.StartsWith("F")  => ((byte)0x93, true),
                _ when addr.StartsWith("V")  => ((byte)McDeviceType.V, true),
                _ when addr.StartsWith("SB") => ((byte)McDeviceType.SB, true),
                _ when addr.StartsWith("SM") => ((byte)0x91, true),
                _ when addr.StartsWith("T")  => ((byte)McDeviceType.T, false),
                _ when addr.StartsWith("C")  => ((byte)McDeviceType.C, false),
                _ when addr.StartsWith("D")  => ((byte)McDeviceType.D, false),
                _ when addr.StartsWith("W")  => ((byte)McDeviceType.W, false),
                _ when addr.StartsWith("R")  => ((byte)McDeviceType.R, false),
                _ when addr.StartsWith("ZR") => ((byte)McDeviceType.R, false),
                _ when addr.StartsWith("SW") => ((byte)McDeviceType.SW, false),
                _ when addr.StartsWith("SD") => ((byte)0xA9, false),
                _ => throw new NotSupportedException($"不支持的设备类型: {addr}")
            };

            // 提取设备编号
            var numStr = addr.SkipWhile(c => !char.IsDigit(c)).ToArray();
            var deviceNo = numStr.Length > 0 ? int.Parse(new string(numStr)) : 0;

            return (type, deviceNo, isBit);
        }

        private static byte[] BuildDeviceAddress(int deviceNo)
        {
            // MC地址为3字节，小端
            var b0 = (byte)(deviceNo & 0xFF);
            var b1 = (byte)((deviceNo >> 8) & 0xFF);
            var b2 = (byte)((deviceNo >> 16) & 0xFF);
            return new[] { b0, b1, b2 };
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream?.Dispose();
                _tcpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
