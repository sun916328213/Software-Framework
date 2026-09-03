using System.Net;
using CommunicationNew.Core;
using FluentModbus;

namespace CommunicationNew.Protocols.Modbus.ModbusTcp
{
    /// <summary>
    /// Modbus TCP 服务器设备 —— 基于 FluentModbus 实现
    /// </summary>
    public class ModbusTcpServerDevice : CommunicationDeviceBase
    {
        private new ModbusTcpParam Param => (ModbusTcpParam)base.Param;
        private ModbusTcpServer? _server;

        public ModbusTcpServerDevice(ModbusTcpParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.ModbusTcp;
        }

        public override DeviceRole Role => DeviceRole.Server;

        public override Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _server = new ModbusTcpServer();
                _server.Start(new IPEndPoint(IPAddress.Any, Param.Port));

                IsConnected = true;
                OnConnectionStateChanged(true, $"Modbus TCP服务器已启动: 0.0.0.0:{Param.Port} (SlaveId={Param.SlaveId})");
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
            _server?.Stop();
            _server = null;
            IsConnected = false;
            OnConnectionStateChanged(false, "Modbus TCP服务器已停止");
            return Task.CompletedTask;
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (_server == null) throw new InvalidOperationException("设备未启动");
            var (region, startAddr, quantity) = ModbusAddress.Parse(address, length);

            return Task.FromResult(region switch
            {
                ModbusRegion.Coil => _server.GetCoils(Param.SlaveId).Slice(startAddr / 8, (quantity + 7) / 8).ToArray(),
                ModbusRegion.DiscreteInput => _server.GetDiscreteInputs(Param.SlaveId).Slice(startAddr / 8, (quantity + 7) / 8).ToArray(),
                ModbusRegion.InputRegister => _server.GetInputRegisters(Param.SlaveId).Slice(startAddr, quantity).ToArray().ToBytes(),
                _ => _server.GetHoldingRegisters(Param.SlaveId).Slice(startAddr, quantity).ToArray().ToBytes(),
            });
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (_server == null) throw new InvalidOperationException("设备未启动");
            var (region, startAddr, _) = ModbusAddress.Parse(address, data.Length);

            switch (region)
            {
                case ModbusRegion.Coil:
                    var coilBuf = _server.GetCoils(Param.SlaveId);
                    for (int i = 0; i < data.Length; i++)
                    {
                        var byteIdx = (startAddr + i) / 8;
                        var bitIdx = (startAddr + i) % 8;
                        if (data[i] != 0)
                            coilBuf[byteIdx] |= (byte)(1 << bitIdx);
                        else
                            coilBuf[byteIdx] &= (byte)~(1 << bitIdx);
                    }
                    break;
                case ModbusRegion.HoldingRegister:
                default:
                    var regs = data.ToShortArray();
                    var regBuf = _server.GetHoldingRegisters(Param.SlaveId);
                    for (int i = 0; i < regs.Length; i++)
                        regBuf[startAddr + i] = (short)regs[i];
                    break;
            }
            return Task.FromResult(true);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus TCP从站不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus TCP从站不支持原始收发");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _server?.Stop();
            }
            base.Dispose(disposing);
        }
    }
}
