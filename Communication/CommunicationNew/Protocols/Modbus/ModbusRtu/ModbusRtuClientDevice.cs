using System.IO.Ports;
using CommunicationNew.Core;
using NModbus;
using NModbus.IO;

namespace CommunicationNew.Protocols.Modbus.ModbusRtu
{
    /// <summary>
    /// Modbus RTU 客户端设备
    /// 支持 Function Code: 01(读线圈) 02(读离散输入) 03(读保持寄存器) 04(读输入寄存器)
    ///                   05(写单个线圈) 06(写单个寄存器) 15(写多个线圈) 16(写多个寄存器)
    /// </summary>
    public class ModbusRtuClientDevice : CommunicationDeviceBase
    {
        private new ModbusRtuParam Param => (ModbusRtuParam)base.Param;
        private System.IO.Ports.SerialPort? _serialPort;
        private IModbusMaster? _master;

        public ModbusRtuClientDevice(ModbusRtuParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.ModbusRtu;
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
                    ReadTimeout = TimeoutMs,
                    WriteTimeout = TimeoutMs,
                };
                _serialPort.Open();

                var factory = new ModbusFactory();
                // NModbus needs IStreamResource; use a simple adapter
                var streamResource = new SerialPortStreamResource(_serialPort);
                _master = factory.CreateRtuMaster(streamResource);

                IsConnected = true;
                OnConnectionStateChanged(true, $"Modbus RTU客户端已连接: {Param.PortName} (SlaveId={Param.SlaveId})");
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
            _master?.Dispose();
            if (_serialPort?.IsOpen == true) _serialPort.Close();
            _serialPort?.Dispose();
            _serialPort = null;
            _master = null;
            IsConnected = false;
            OnConnectionStateChanged(false, "Modbus RTU客户端已断开");
            return Task.CompletedTask;
        }

        public override async Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (_master == null || !IsConnected) throw new InvalidOperationException("设备未连接");

            var (region, startAddr, quantity) = ModbusAddress.Parse(address, length);

            return await ExecuteWithRetryAsync(async () =>
            {
                switch (region)
                {
                    case ModbusRegion.Coil:
                        return (await _master.ReadCoilsAsync(Param.SlaveId, startAddr, quantity)).ToBytes();
                    case ModbusRegion.DiscreteInput:
                        return (await _master.ReadInputsAsync(Param.SlaveId, startAddr, quantity)).ToBytes();
                    case ModbusRegion.InputRegister:
                        return (await _master.ReadInputRegistersAsync(Param.SlaveId, startAddr, quantity)).ToBytes();
                    case ModbusRegion.HoldingRegister:
                    default:
                        return (await _master.ReadHoldingRegistersAsync(Param.SlaveId, startAddr, quantity)).ToBytes();
                }
            }, ct);
        }

        public override async Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (_master == null || !IsConnected) throw new InvalidOperationException("设备未连接");

            var (region, startAddr, _) = ModbusAddress.Parse(address, data.Length);

            return await ExecuteWithRetryAsync(async () =>
            {
                switch (region)
                {
                    case ModbusRegion.Coil:
                        await _master.WriteMultipleCoilsAsync(Param.SlaveId, startAddr, data.ToBoolArray());
                        return true;
                    case ModbusRegion.HoldingRegister:
                    default:
                        await _master.WriteMultipleRegistersAsync(Param.SlaveId, startAddr, data.ToShortArray());
                        return true;
                }
            }, ct);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus RTU 不支持原始字节发送，请使用 ReadAsync/WriteAsync");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("Modbus RTU 不支持原始收发，请使用 ReadAsync/WriteAsync");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _master?.Dispose();
                if (_serialPort?.IsOpen == true) _serialPort.Close();
                _serialPort?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
