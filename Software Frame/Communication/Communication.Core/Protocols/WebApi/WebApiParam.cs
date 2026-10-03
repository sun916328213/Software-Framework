// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：WebApiParam、WebApiRoute。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Protocols.WebApi
{
    /// <summary>
    /// WebApi 通信参数
    /// </summary>
    public class WebApiParam : Communication.Core.CommunicationParam
    {
        /// <summary>基础URL</summary>
        public string BaseUrl { get; set; } = "http://127.0.0.1:5000";

        /// <summary>API前缀（如 /api/v1）</summary>
        public string ApiPrefix { get; set; } = "/api";

        /// <summary>内容类型（固定JSON）</summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>默认请求头</summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>Bearer Token（认证）</summary>
        public string? BearerToken { get; set; }

        /// <summary>API Key（认证）</summary>
        public string? ApiKey { get; set; }

        /// <summary>ApiKey的Header名称</summary>
        public string ApiKeyHeaderName { get; set; } = "X-API-Key";

        /// <summary>超时时间</summary>
        public int OperationTimeoutMs { get; set; } = 30000;

        /// <summary>是否忽略SSL错误</summary>
        public bool IgnoreSslErrors { get; set; } = false;

        /// <summary>服务器模式API路由</summary>
        public List<WebApiRoute> Routes { get; set; } = new();
    }

    /// <summary>WebApi 路由配置</summary>
    public class WebApiRoute
    {
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
