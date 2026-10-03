// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusRtuParam。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Ports。导入不代表所有类型都会在运行时加载。
using System.IO.Ports;

namespace Communication.Core.Protocols.Modbus.ModbusRtu
{
    /// <summary>
    /// Modbus RTU 通信参数
    /// </summary>
    public class ModbusRtuParam : Communication.Core.CommunicationParam
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
