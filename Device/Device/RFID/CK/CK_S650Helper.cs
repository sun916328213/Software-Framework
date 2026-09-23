using System.Threading.Tasks;
using Device.RFID.Abstract;

namespace Device.RFID.CK
{
    internal class CK_S650Helper : RFIDBase_ModbusRTU
    {

        /// <summary>
        /// RFID寄存器开始地址
        /// </summary>
        protected override ushort StartAddress { get; set; } = 5;

        protected override bool ConnectRefactor()
        {
            return Task.Run(() => ModbusClient.ConnectAsync()).GetAwaiter().GetResult();
        }

        protected override bool DisConnectRefactor()
        {
            Task.Run(() => ModbusClient.DisconnectAsync()).GetAwaiter().GetResult();
            return true;
        }
    }
}
