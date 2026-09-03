namespace CommunicationNew.Protocols.OpcUa
{
    /// <summary>
    /// OPC UA 通信参数
    /// </summary>
    public class OpcUaParam : CommunicationNew.Core.CommunicationParam
    {
        /// <summary>端点URL（如 opc.tcp://127.0.0.1:4840）</summary>
        public string EndpointUrl { get; set; } = "opc.tcp://127.0.0.1:4840";

        /// <summary>安全模式（None/Sign/SignAndEncrypt）</summary>
        public string SecurityMode { get; set; } = "None";

        /// <summary>安全策略</summary>
        public string SecurityPolicy { get; set; } = "None";

        /// <summary>用户名（空=匿名）</summary>
        public string? UserName { get; set; }

        /// <summary>密码</summary>
        public string? Password { get; set; }

        /// <summary>超时时间（毫秒）</summary>
        public int OperationTimeout { get; set; } = 10000;

        /// <summary>应用名称</summary>
        public string ApplicationName { get; set; } = "CommunicationNew";

        /// <summary>服务器模式下节点命名空间URI</summary>
        public string ServerNamespaceUri { get; set; } = "http://communicationnew.org/UA";

        /// <summary>服务器模式下默认采样间隔（毫秒）</summary>
        public int DefaultSamplingInterval { get; set; } = 1000;
    }
}
