namespace CommunicationNew.Protocols.Http
{
    /// <summary>
    /// HTTP 通信参数
    /// </summary>
    public class HttpParam : CommunicationNew.Core.CommunicationParam
    {
        /// <summary>基础URL（客户端模式：API基地址；服务器模式：监听前缀）</summary>
        public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

        /// <summary>默认请求头</summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>内容类型</summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>默认HTTP方法</summary>
        public string DefaultMethod { get; set; } = "GET";

        /// <summary>是否启用KeepAlive</summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>最大并发连接数</summary>
        public int MaxConnections { get; set; } = 100;

        /// <summary>TLS忽略证书错误</summary>
        public bool IgnoreSslErrors { get; set; } = false;

        /// <summary>服务器模式路由配置</summary>
        public List<HttpRoute> Routes { get; set; } = new();
    }

    /// <summary>HTTP路由配置</summary>
    public class HttpRoute
    {
        /// <summary>HTTP方法 (GET/POST/PUT/DELETE)</summary>
        public string Method { get; set; } = "GET";

        /// <summary>路径模板 (如 /api/data)</summary>
        public string Path { get; set; } = "";

        /// <summary>处理函数标识</summary>
        public string HandlerName { get; set; } = "";
    }
}
