using Communication.Communication.SerialPort.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Protocols.Modbus.RTU.Definition
{
    public class ModbusRTUParam : SerialParam
    {
        /// <summary>
        /// 站号
        /// </summary>
        public int SlaveID { get; set; }
    }
}
