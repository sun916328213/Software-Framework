using Communication.Struct;

namespace Device.RFID.Definition
{
    public class RFIDParam: ModbusRTUParam
    {
        public string Name {  get; set; }
 
        public ERFIDType RFIDType { get; set; }= ERFIDType.晨控S650;
    }
}
