using System.IO.Ports;
using NModbus.IO;

namespace CommunicationNew.Protocols.Modbus.ModbusRtu
{
    /// <summary>
    /// 串口 IStreamResource 适配器 —— 将 System.IO.Ports.SerialPort 适配为 NModbus.IO.IStreamResource
    /// </summary>
    internal class SerialPortStreamResource : IStreamResource
    {
        private readonly System.IO.Ports.SerialPort _serialPort;

        public SerialPortStreamResource(System.IO.Ports.SerialPort serialPort)
        {
            _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
        }

        public int InfiniteTimeout => -1;

        public int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }

        public void DiscardInBuffer() => _serialPort.DiscardInBuffer();

        public int Read(byte[] buffer, int offset, int count) => _serialPort.Read(buffer, offset, count);

        public void Write(byte[] buffer, int offset, int count) => _serialPort.Write(buffer, offset, count);

        public void Dispose() => _serialPort.Dispose();
    }
}
