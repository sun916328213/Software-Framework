// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IModbusRtuServerAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Modbus.ModbusRtu、Communication.Runtime.Protocols.Modbus。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Protocols.Modbus;
namespace Communication.Runtime.Protocols.Modbus.ModbusRtu;

/// <summary>
/// Modbus RTU 从站 SDK 的内部 seam；隐藏串口启动和数据区布局等 FluentModbus 细节。
/// </summary>
internal interface IModbusRtuServerAdapter : IDisposable
{
    /// <summary>从站是否正在处理串口请求。</summary>
    bool IsRunning { get; }

    /// <summary>启动从站。</summary>
    void Start();

    /// <summary>停止从站并释放串口等待。</summary>
    void Stop();

    /// <summary>按兼容地址格式读取从站数据区。</summary>
    byte[] Read(string address, int length);

    /// <summary>按兼容地址格式写入从站数据区。</summary>
    void Write(string address, byte[] data);
}
