// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ProtocolType。成员说明可结合下方具体实现阅读。
namespace Communication.Core
{
    /// <summary>
    /// 支持的通信协议类型
    /// </summary>
    public enum ProtocolType
    {
        /// <summary>Socket TCP</summary>
        Tcp,
        /// <summary>Socket UDP</summary>
        Udp,
        /// <summary>Modbus RTU (串口)</summary>
        ModbusRtu,
        /// <summary>Modbus TCP</summary>
        ModbusTcp,
        /// <summary>OPC UA</summary>
        OpcUa,
        /// <summary>HTTP</summary>
        Http,
        /// <summary>Web API (RESTful)</summary>
        WebApi,
        /// <summary>三菱 MC 协议</summary>
        MitsubishiMc,
        /// <summary>串口通信</summary>
        Serial,
    }
}
