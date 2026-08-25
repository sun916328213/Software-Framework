using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Communication.Socket.Definition
{
    public class SocketParam
    {
        /// <summary>
        /// 目标IP地址
        /// </summary>
        public string RemoteIP { get; set; }
        /// <summary>
        /// 目标端口
        /// </summary>
        public ushort RemotePort { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SocketParam(){ }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="remoteIP">目标IP地址</param>
        /// <param name="remotePort">目标端口</param>
        public SocketParam(string remoteIP,ushort remotePort)
        {
            RemoteIP=remoteIP;
            RemotePort=remotePort;
        }
    }
}
