using System.IO.Ports;

namespace CommunicationNew.Transports;

/// <summary>把平台 SerialPort 适配到内部连接 seam。</summary>
internal sealed class SystemSerialPortConnection : ISerialPortConnection
{
    private readonly SerialPort _serialPort;

    public SystemSerialPortConnection(SerialTransportOptions options)
    {
        _serialPort = new SerialPort(
            options.PortName,
            options.BaudRate,
            options.Parity,
            options.DataBits,
            options.StopBits)
        {
            Handshake = options.Handshake,
            ReadBufferSize = options.BufferSize,
            WriteBufferSize = options.BufferSize,
            ReadTimeout = options.TimeoutMs,
            WriteTimeout = options.TimeoutMs,
            DtrEnable = options.DtrEnable,
            RtsEnable = options.RtsEnable,
        };
    }

    public bool IsOpen => _serialPort.IsOpen;
    public Stream Stream => _serialPort.BaseStream;
    public void Open() => _serialPort.Open();
    public void Close() => _serialPort.Close();
    public void Dispose() => _serialPort.Dispose();
}
