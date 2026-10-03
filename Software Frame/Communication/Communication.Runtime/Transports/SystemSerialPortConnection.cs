// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SystemSerialPortConnection。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Ports。导入不代表所有类型都会在运行时加载。
using System.IO.Ports;

namespace Communication.Runtime.Transports;

/// <summary>把平台 SerialPort 适配到内部连接 seam。</summary>
internal sealed class SystemSerialPortConnection : ISerialPortConnection
{
    private readonly SerialPort _serialPort;

    /// <summary>
    /// 初始化 SystemSerialPortConnection 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public SystemSerialPortConnection(SerialTransportOptions options)
    {
        _serialPort = new SerialPort(
            options.PortName,
            options.BaudRate,
            options.Parity,
            options.DataBits,
            options.StopBits)
        {
            Handshake = options.Handshake,
            ReadBufferSize = options.BufferSize,
            WriteBufferSize = options.BufferSize,
            ReadTimeout = options.TimeoutMs,
            WriteTimeout = options.TimeoutMs,
            DtrEnable = options.DtrEnable,
            RtsEnable = options.RtsEnable,
        };
    }

    public bool IsOpen => _serialPort.IsOpen;
    public Stream Stream => _serialPort.BaseStream;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Open。
    /// 正文中的关键协作调用包括 Open；具体条件和先后次序见方法体。
    /// </summary>
    public void Open() => _serialPort.Open();
    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    public void Close() => _serialPort.Close();
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose() => _serialPort.Dispose();
}
