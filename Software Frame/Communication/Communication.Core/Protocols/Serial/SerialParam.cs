// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SerialParam。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Ports。导入不代表所有类型都会在运行时加载。
using System.IO.Ports;

namespace Communication.Core.Protocols.Serial
{
    /// <summary>
    /// 串口通信参数
    /// </summary>
    public class SerialParam : Communication.Core.CommunicationParam
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
