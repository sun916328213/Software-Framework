using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Core.Definition
{
    public class CommunicationParam
    {
        /// <summary>
        /// 设备协议类型
        /// </summary>
        public enum DeviceProtocolType
        {
            ModbusTCP,
            ModbusRTU,
            OPCUA,
            MC3E,
            MC,
            PROFINET,
            ETHERNETIP,
            BACnet,
            S7Net,
        }

        /// <summary>
        /// 数据类型
        /// </summary>
        public enum DataType
        {
            BIT,
            BYTE,
            INT16,
            UINT16,
            INT32,
            UINT32,
            SINGLE,
            DOUBLE,
            UTF32,
            ASCII
        }
    }
}
