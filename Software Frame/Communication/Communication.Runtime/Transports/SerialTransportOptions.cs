// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SerialTransportOptions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Ports。导入不代表所有类型都会在运行时加载。
using System.IO.Ports;

namespace Communication.Runtime.Transports;

/// <summary>创建串口字节流 transport 所需的不可变配置。</summary>
public sealed class SerialTransportOptions
{
    /// <summary>创建最小串口配置，其余属性使用平台常用默认值。</summary>
    public SerialTransportOptions(string portName, int baudRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate));
        }

        PortName = portName;
        BaudRate = baudRate;
    }

    /// <summary>串口名称，例如 COM1。</summary>
    public string PortName { get; }

    /// <summary>波特率。</summary>
    public int BaudRate { get; }

    /// <summary>数据位。</summary>
    public int DataBits { get; init; } = 8;

    /// <summary>停止位。</summary>
    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>校验位。</summary>
    public Parity Parity { get; init; } = Parity.None;

    /// <summary>硬件或软件握手方式。</summary>
    public Handshake Handshake { get; init; } = Handshake.None;

    /// <summary>读写缓冲区大小。</summary>
    public int BufferSize { get; init; } = 4096;

    /// <summary>同步端口操作的超时时间，单位为毫秒。</summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>是否启用 DTR。</summary>
    public bool DtrEnable { get; init; }

    /// <summary>是否启用 RTS。</summary>
    public bool RtsEnable { get; init; }
}
