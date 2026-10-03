// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ISerialPortConnection。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Transports;

/// <summary>隔离平台 SerialPort 的内部测试 seam，不暴露给库调用方。</summary>
internal interface ISerialPortConnection : IDisposable
{
    bool IsOpen { get; }
    Stream Stream { get; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Open。
    /// </summary>
    void Open();
    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// </summary>
    void Close();
}
