using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Struct
{
    public class SerialParam
    {
        /// <summary>  
        /// 端口名称  
        /// </summary>  
        public string PortName { get; set; }
        /// <summary>  
        /// 波特率  
        /// </summary>  
        public int BaudRate { get; set; } = 9600;
        /// <summary>  
        /// 校验位  
        /// </summary>  
        public Parity Parity { get; set; } = Parity.None;
        /// <summary>  
        /// 数据位  
        /// </summary>  
        public int DataBits { get; set; } = 8;
        /// <summary>  
        /// 停止位  
        /// </summary>  
        public StopBits StopBits { get; set; } = StopBits.One;
        /// <summary>  
        /// 超时(ms)  
        /// </summary>  
        public int? TimeoutMs { get; set; } = 500;
        /// <summary>  
        /// 缓冲区大小  
        /// </summary>  
        public int? BufferSize { get; set; } = 9 * 1024;
    }

    public class ModbusRTUParam : SerialParam
    {
        /// <summary>
        /// 站号
        /// </summary>
        public int SlaveID { get; set; }
    }
}
