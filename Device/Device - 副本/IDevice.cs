using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device
{
    public interface IDevice
    {
        /// <summary>
        /// 设备名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 连接设备
        /// </summary>
        bool Connect();
        /// <summary>
        /// 断开设备
        /// </summary>
        bool DisConnect();
    }
}
