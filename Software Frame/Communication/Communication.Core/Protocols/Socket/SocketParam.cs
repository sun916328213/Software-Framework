// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SocketParam。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Protocols.Socket
{
    /// <summary>
    /// Socket 通信参数（TCP/UDP 通用）
    /// </summary>
    public class SocketParam : Communication.Core.CommunicationParam
    {
        /// <summary>服务器IP地址</summary>
        public string ServerIP { get; set; } = "127.0.0.1";

        /// <summary>端口号</summary>
        public int Port { get; set; } = 8080;

        /// <summary>本地绑定IP（服务器模式或UDP客户端绑定）</summary>
        public string LocalIP { get; set; } = "0.0.0.0";

        /// <summary>本地端口（0=自动分配）</summary>
        public int LocalPort { get; set; } = 0;

        /// <summary>接收缓冲区大小</summary>
        public int BufferSize { get; set; } = 4096;

        /// <summary>编码名称（默认UTF-8）</summary>
        public string Encoding { get; set; } = "UTF-8";

        /// <summary>分包结束符（用于TCP粘包处理，如 "\r\n"）</summary>
        public string? Delimiter { get; set; } = "\r\n";

        /// <summary>是否启用心跳</summary>
        public bool EnableHeartbeat { get; set; } = false;

        /// <summary>心跳间隔（毫秒）</summary>
        public int HeartbeatIntervalMs { get; set; } = 30000;

        /// <summary>心跳数据</summary>
        public byte[]? HeartbeatData { get; set; }

        /// <summary>最大连接数（仅服务器）</summary>
        public int MaxConnections { get; set; } = 100;
    }
}
