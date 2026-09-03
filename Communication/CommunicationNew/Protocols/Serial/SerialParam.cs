using System.IO.Ports;

namespace CommunicationNew.Protocols.Serial
{
    /// <summary>
    /// 串口通信参数
    /// </summary>
    public class SerialParam : CommunicationNew.Core.CommunicationParam
    {
        /// <summary>串口名称（如 COM1）</summary>
        public string PortName { get; set; } = "COM1";

        /// <summary>波特率</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>数据位</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>停止位</summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>校验位</summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>握手协议</summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>读/写缓冲区大小</summary>
        public int BufferSize { get; set; } = 4096;

        /// <summary>分包结束符（如 "\r\n"），为null则不自动分包</summary>
        public string? Delimiter { get; set; } = "\r\n";

        /// <summary>DTR使能</summary>
        public bool DtrEnable { get; set; } = false;

        /// <summary>RTS使能</summary>
        public bool RtsEnable { get; set; } = false;
    }
}
