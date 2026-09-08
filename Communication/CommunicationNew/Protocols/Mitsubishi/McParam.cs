namespace CommunicationNew.Protocols.Mitsubishi
{
    /// <summary>
    /// 三菱 MC 客户端参数。
    /// 当前支持 SLMP 3E/4E 二进制帧 over TCP，以及可选的 MX Component 逻辑站后端。
    /// </summary>
    public class McParam : CommunicationNew.Core.CommunicationParam
    {
        private int? _communicationTimeout;

        /// <summary>MC 客户端使用的通信后端；默认使用无需 MX Component 的 SLMP。</summary>
        public McClientBackend Backend { get; set; } = McClientBackend.Slmp;

        /// <summary>可选的 net48 桥接程序绝对路径；为空时直接调用已引用的 64 位 MX Interop。</summary>
        public string? MxBridgeExecutablePath { get; set; }

        /// <summary>MX Component Communication Setup Utility 中配置的逻辑站号，范围为 0 到 1023。</summary>
        public int MxLogicalStationNumber { get; set; }

        /// <summary>MX Component 逻辑站访问密码；为空表示不使用密码。</summary>
        public string MxPassword { get; set; } = string.Empty;

        /// <summary>等待 MX 桥接进程建立命名管道的超时时间（毫秒）。</summary>
        public int MxBridgeStartupTimeoutMs { get; set; } = 10000;

        /// <summary>服务器IP（TCP模式）</summary>
        public string ServerIP { get; set; } = "192.168.1.1";

        /// <summary>端口号（MC TCP默认 5000/4999）</summary>
        public int Port { get; set; } = 5000;

        /// <summary>旧配置兼容字段；当前 MC adapter 不支持串口，设置后会显式拒绝创建。</summary>
        public string? SerialPortName { get; set; }

        /// <summary>旧串口配置兼容字段；当前 MC adapter 不会使用该值。</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>网络编号 (0-15)</summary>
        public byte NetworkNo { get; set; } = 0;

        /// <summary>PLC站编号 (0-255)</summary>
        public byte StationNo { get; set; } = 0xFF;

        /// <summary>模块编号 (0=主模块)</summary>
        public ushort ModuleNo { get; set; } = 0x03FF;

        /// <summary>帧类型: "3E" 或 "4E"</summary>
        public string FrameType { get; set; } = "3E";

        /// <summary>
        /// MC 协议兼容超时覆盖值；未显式设置时自动跟随通用 <see cref="CommunicationNew.Core.CommunicationParam.TimeoutMs"/>。
        /// </summary>
        public int CommunicationTimeout
        {
            get => _communicationTimeout ?? TimeoutMs;
            set => _communicationTimeout = value;
        }
    }

    /// <summary>三菱 MC 客户端可并存的后端类型。</summary>
    public enum McClientBackend
    {
        /// <summary>由 CommunicationNew 自行编解码 3E/4E 报文的 TCP SLMP 后端。</summary>
        Slmp,

        /// <summary>调用 MX Component V5；默认直接调用，也可配置隔离桥接进程。</summary>
        MxComponent,
    }

    /// <summary>
    /// MC 协议设备类型
    /// </summary>
    public enum McDeviceType : byte
    {
        /// <summary>特殊继电器 SM</summary>
        SM = 0x91,
        /// <summary>特殊寄存器 SD</summary>
        SD = 0xA9,
        /// <summary>继电器 M</summary>
        M = 0x90,
        /// <summary>锁存继电器 L</summary>
        L = 0x92,
        /// <summary>报警器 F</summary>
        F = 0x93,
        /// <summary>链接继电器 B</summary>
        B = 0xA0,
        /// <summary>变址继电器 V</summary>
        V = 0x94,
        /// <summary>链接特殊继电器 SB</summary>
        SB = 0xA1,
        /// <summary>步进继电器 S</summary>
        S = 0x98,
        /// <summary>输入继电器 X</summary>
        X = 0x9C,
        /// <summary>输出继电器 Y</summary>
        Y = 0x9D,
        /// <summary>定时器线圈 TC</summary>
        TC = 0xC0,
        /// <summary>定时器触点 TS</summary>
        TS = 0xC1,
        /// <summary>定时器当前值 TN；T 为兼容别名</summary>
        TN = 0xC2,
        /// <summary>定时器当前值兼容别名</summary>
        T = TN,
        /// <summary>计数器线圈 CC</summary>
        CC = 0xC3,
        /// <summary>计数器触点 CS</summary>
        CS = 0xC4,
        /// <summary>计数器当前值 CN</summary>
        CN = 0xC5,
        /// <summary>旧计数器值，保留原始数值以维持二进制兼容；新代码应使用 CS、CC 或 CN。</summary>
        [Obsolete("C 的旧设备码不明确，请根据语义使用 CS、CC 或 CN。")]
        C = 0xC0,
        /// <summary>数据寄存器 D</summary>
        D = 0xA8,
        /// <summary>链接寄存器 W</summary>
        W = 0xB4,
        /// <summary>文件寄存器 R</summary>
        R = 0xAF,
        /// <summary>文件寄存器 ZR</summary>
        ZR = 0xB0,
        /// <summary>链接特殊寄存器 SW</summary>
        SW = 0xB5,
    }

    /// <summary>
    /// MC 批量读写命令及位/字子命令。
    /// 强类型请求会根据地址语义选择子命令，调用方通常不需要直接使用这些常量。
    /// </summary>
    public static class McCommand
    {
        /// <summary>批量读取设备命令。</summary>
        public const ushort BatchRead = 0x0401;

        /// <summary>批量写入设备命令。</summary>
        public const ushort BatchWrite = 0x1401;

        /// <summary>按字访问的子命令。</summary>
        public const ushort WordSubcommand = 0x0000;

        /// <summary>按位访问的子命令。</summary>
        public const ushort BitSubcommand = 0x0001;

        [Obsolete("请使用 BatchRead，并由地址类型选择 BitSubcommand。")]
        public const string ReadBit = "0401";

        [Obsolete("请使用 BatchRead，并由地址类型选择 WordSubcommand。")]
        public const string ReadWord = "0401";

        [Obsolete("请使用 BatchRead，并由地址类型选择 BitSubcommand。")]
        public const string ReadBitBlock = "0401";

        [Obsolete("请使用 BatchRead，并由地址类型选择 WordSubcommand。")]
        public const string ReadWordBlock = "0401";

        [Obsolete("请使用 BatchWrite，并由地址类型选择 BitSubcommand。")]
        public const string WriteBit = "1401";

        [Obsolete("请使用 BatchWrite，并由地址类型选择 WordSubcommand。")]
        public const string WriteWord = "1401";

        [Obsolete("请使用 BatchWrite，并由地址类型选择 BitSubcommand。")]
        public const string WriteBitBlock = "1401";

        [Obsolete("请使用 BatchWrite，并由地址类型选择 WordSubcommand。")]
        public const string WriteWordBlock = "1401";
    }
}
