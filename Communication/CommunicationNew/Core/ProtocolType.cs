namespace CommunicationNew.Core
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
