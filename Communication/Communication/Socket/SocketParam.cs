using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Socket
{
    /// <summary>
    /// Socket 通讯参数
    /// </summary>
    public class SocketParam
    {
        /// <summary>
        /// 服务器 IP 地址
        /// </summary>
        public string ServerIP { get; set; } = "127.0.0.1";

        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// 连接超时时间（毫秒）
        /// </summary>
        public int ConnectTimeout { get; set; } = 5000;

        /// <summary>
        /// 接收超时时间（毫秒）
        /// </summary>
        public int ReceiveTimeout { get; set; } = 5000;

        /// <summary>
        /// 发送超时时间（毫秒）
        /// </summary>
        public int SendTimeout { get; set; } = 5000;

        /// <summary>
        /// 缓冲区大小
        /// </summary>
        public int BufferSize { get; set; } = 1024;

        /// <summary>
        /// 是否自动重连
        /// </summary>
        public bool AutoReconnect { get; set; } = false;

        /// <summary>
        /// 重连间隔（毫秒）
        /// </summary>
        public int ReconnectInterval { get; set; } = 3000;

        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectCount { get; set; } = 5;
    }
}