using CommunicationNew.Protocols.Serial;
using Device.RFID.Definition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Device.RFID.Abstract
{
    internal abstract class RFIDBase_Serial : IRFID
    {
        public RFIDParam Param { get; set; }

        /// <summary>
        /// 底层串口设备。CommunicationNew 的 SerialPortDevice 在构造时绑定 SerialParam，
        /// 因此这里在 <see cref="Connect"/> 时按 Param 的串口字段创建。
        /// </summary>
        private SerialPortDevice _serialPort;

        /// <summary>底层串口设备；未连接时访问会抛出异常。</summary>
        protected SerialPortDevice SerialPort => _serialPort
            ?? throw new InvalidOperationException("串口尚未连接。");

        public bool IsConnected => _serialPort?.IsConnected ?? false;

        public string Name
        {
            get => Param.Name;
            set => Param.Name = value;
        }

        public bool Connect()
        {
            if (Param == null) throw new Exception("Serial Param not set");
            _serialPort?.Dispose();
            _serialPort = new SerialPortDevice(CreateSerialParam(Param));
            return Task.Run(() => _serialPort.ConnectAsync()).GetAwaiter().GetResult();
        }

        public bool DisConnect()
        {
            if (_serialPort == null) return true;
            Task.Run(() => _serialPort.DisconnectAsync()).GetAwaiter().GetResult();
            return true;
        }

        /// <summary>
        /// 发送并按请求/回复收取一帧。旧实现是 SerialHelper.WriteWithReply，
        /// 现在统一由基类封装，具体 RFID 只需调用本方法。
        /// </summary>
        protected byte[] SendAndReceive(byte[] buffer) =>
            Task.Run(() => SerialPort.SendAndReceiveAsync(buffer)).GetAwaiter().GetResult();

        /// <summary>
        /// RFIDParam 继承自 ModbusRtuParam（不再是旧版 SerialParam 的子类），
        /// 因此按串口字段投影出一个 SerialParam。
        /// Delimiter 置 null：旧 SerialHelper 不做分隔符分包，保持原语义。
        /// </summary>
        private static SerialParam CreateSerialParam(RFIDParam param) => new SerialParam
        {
            DeviceName = param.Name ?? string.Empty,
            PortName = param.PortName,
            BaudRate = param.BaudRate,
            DataBits = param.DataBits,
            StopBits = param.StopBits,
            Parity = param.Parity,
            TimeoutMs = param.TimeoutMs,
            Delimiter = null,
        };

        public abstract string Read(int page);
 

        public abstract string Read(int startPage, int length);
 

        public abstract void Write(int page, string data, int makeuplength );



        public abstract void Write(int startPage, List<string> data, int makeuplength );
       
    }
}
