using CommunicationNew.Protocols.Modbus.ModbusRtu;

namespace Device.RFID.Definition
{
    public class RFIDParam: ModbusRtuParam
    {
        public string Name {  get; set; }
 
        public ERFIDType RFIDType { get; set; }= ERFIDType.晨控S650;

        /// <summary>
        /// 兼容旧 Communication.Struct.ModbusRTUParam 的 int 型站号；
        /// 新基类 CommunicationNew.Protocols.Modbus.ModbusRtu.ModbusRtuParam.SlaveId 为 byte。
        /// </summary>
        public int SlaveID
        {
            get => SlaveId;
            set => SlaveId = (byte)value;
        }
    }
}
