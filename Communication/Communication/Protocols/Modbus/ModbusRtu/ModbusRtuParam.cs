using System.IO.Ports;

namespace CommunicationNew.Protocols.Modbus.ModbusRtu
{
    /// <summary>
    /// Modbus RTU 通信参数
    /// </summary>
    public class ModbusRtuParam : CommunicationNew.Core.CommunicationParam
    {
        /// <summary>串口名称</summary>
        public string PortName { get; set; } = "COM1";

        /// <summary>波特率</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>数据位</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>停止位</summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>校验位</summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>从站ID（客户端模式下为目标从站）</summary>
        public byte SlaveId { get; set; } = 1;

        /// <summary>服务器模式下的数据区大小</summary>
        public int HoldingRegisterSize { get; set; } = 1000;
        public int InputRegisterSize { get; set; } = 1000;
        public int CoilSize { get; set; } = 1000;
        public int DiscreteInputSize { get; set; } = 1000;
    }
}
