namespace CommunicationNew.Protocols.Mitsubishi
{
    /// <summary>
    /// 三菱 MC 协议通信参数
    /// 支持 MC Binary (3E/4E帧) over TCP 和 MC RTU over Serial
    /// </summary>
    public class McParam : CommunicationNew.Core.CommunicationParam
    {
        /// <summary>服务器IP（TCP模式）</summary>
        public string ServerIP { get; set; } = "192.168.1.1";

        /// <summary>端口号（MC TCP默认 5000/4999）</summary>
        public int Port { get; set; } = 5000;

        /// <summary>串口名称（RTU模式，为空则使用TCP）</summary>
        public string? SerialPortName { get; set; }

        /// <summary>波特率（RTU模式）</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>网络编号 (0-15)</summary>
        public byte NetworkNo { get; set; } = 0;

        /// <summary>PLC站编号 (0-255)</summary>
        public byte StationNo { get; set; } = 0xFF;

        /// <summary>模块编号 (0=主模块)</summary>
        public ushort ModuleNo { get; set; } = 0x03FF;

        /// <summary>帧类型: "3E" 或 "4E"</summary>
        public string FrameType { get; set; } = "3E";

        /// <summary>超时时间</summary>
        public int CommunicationTimeout { get; set; } = 3000;
    }

    /// <summary>
    /// MC 协议设备类型
    /// </summary>
    public enum McDeviceType : byte
    {
        /// <summary>继电器 M</summary>
        M = 0x90,
        /// <summary>锁存继电器 L</summary>
        L = 0x92,
        /// <summary>链接继电器 B</summary>
        B = 0xA0,
        /// <summary>变址继电器 V</summary>
        V = 0xF4,
        /// <summary>链接特殊继电器 SB</summary>
        SB = 0xA1,
        /// <summary>步进继电器 S</summary>
        S = 0x98,
        /// <summary>定时器 T</summary>
        T = 0xC2,
        /// <summary>计数器 C</summary>
        C = 0xC0,
        /// <summary>数据寄存器 D</summary>
        D = 0xA8,
        /// <summary>链接寄存器 W</summary>
        W = 0xB4,
        /// <summary>文件寄存器 R/ZR</summary>
        R = 0xAF,
        /// <summary>链接特殊寄存器 SW</summary>
        SW = 0xB5,
    }

    /// <summary>
    /// MC 协议命令（读写）
    /// </summary>
    public static class McCommand
    {
        // 读命令
        public const string ReadBit = "0401";      // 位读
        public const string ReadWord = "0403";     // 字读
        public const string ReadBitBlock = "0401"; // 位块读
        public const string ReadWordBlock = "0403";// 字块读

        // 写命令
        public const string WriteBit = "1401";     // 位写
        public const string WriteWord = "1403";    // 字写
        public const string WriteBitBlock = "1401"; // 位块写
        public const string WriteWordBlock = "1402"; // 字块写
    }
}
