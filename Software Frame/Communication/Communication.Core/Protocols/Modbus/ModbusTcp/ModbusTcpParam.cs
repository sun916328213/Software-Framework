// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusTcpParam。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Protocols.Modbus.ModbusTcp
{
    /// <summary>
    /// Modbus TCP 通信参数
    /// </summary>
    public class ModbusTcpParam : Communication.Core.CommunicationParam
    {
        /// <summary>服务器IP</summary>
        public string ServerIP { get; set; } = "127.0.0.1";

        /// <summary>端口号（Modbus TCP 默认 502）</summary>
        public int Port { get; set; } = 502;

        /// <summary>从站ID</summary>
        public byte SlaveId { get; set; } = 1;

        /// <summary>服务器模式数据区大小</summary>
        public int HoldingRegisterSize { get; set; } = 1000;
        public int InputRegisterSize { get; set; } = 1000;
        public int CoilSize { get; set; } = 1000;
        public int DiscreteInputSize { get; set; } = 1000;
    }
}
