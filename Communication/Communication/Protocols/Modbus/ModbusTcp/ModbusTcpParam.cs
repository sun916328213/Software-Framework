namespace CommunicationNew.Protocols.Modbus.ModbusTcp
{
    /// <summary>
    /// Modbus TCP 通信参数
    /// </summary>
    public class ModbusTcpParam : CommunicationNew.Core.CommunicationParam
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
