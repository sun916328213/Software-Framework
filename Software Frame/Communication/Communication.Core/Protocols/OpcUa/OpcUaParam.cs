// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：OpcUaParam。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Protocols.OpcUa
{
    /// <summary>
    /// OPC UA 通信参数
    /// </summary>
    public class OpcUaParam : Communication.Core.CommunicationParam
    {
        /// <summary>端点URL（如 opc.tcp://127.0.0.1:4840）</summary>
        public string EndpointUrl { get; set; } = "opc.tcp://127.0.0.1:4840";

        /// <summary>
        /// 期望的安全模式（None/Sign/SignAndEncrypt）。
        /// 生产环境建议使用 SignAndEncrypt。
        /// </summary>
        public string SecurityMode { get; set; } = "None";

        /// <summary>
        /// 期望的安全策略。支持 None、Basic256Sha256、
        /// Aes128_Sha256_RsaOaep 和 Aes256_Sha256_RsaPss。
        /// </summary>
        public string SecurityPolicy { get; set; } = "None";

        /// <summary>用户名（空=匿名）；None 安全模式下拒绝发送凭据。</summary>
        public string? UserName { get; set; }

        /// <summary>密码</summary>
        public string? Password { get; set; }

        /// <summary>超时时间（毫秒）</summary>
        public int OperationTimeout { get; set; } = 10000;

        /// <summary>应用名称</summary>
        public string ApplicationName { get; set; } = "CommunicationNew";

        /// <summary>
        /// OPC UA PKI 根目录。为空时使用当前用户 LocalApplicationData 下的 CommunicationNew/pki。
        /// 服务端即使只开放 None 端点，也需要在该目录持有应用实例证书。
        /// </summary>
        public string? PkiRootPath { get; set; }

        /// <summary>
        /// 是否自动接受未受信任的应用证书。默认关闭；仅建议在本地回环或测试环境显式开启。
        /// 生产环境应将对端证书放入 trusted 存储。
        /// </summary>
        public bool AutoAcceptUntrustedCertificates { get; set; }

        /// <summary>OPC UA 对端 RSA 证书允许的最小密钥长度，默认 2048 位。</summary>
        public ushort MinimumCertificateKeySize { get; set; } = 2048;

        /// <summary>服务器模式下节点命名空间URI</summary>
        public string ServerNamespaceUri { get; set; } = "http://communicationnew.org/UA";

        /// <summary>服务器模式下默认采样间隔（毫秒）</summary>
        public int DefaultSamplingInterval { get; set; } = 1000;
    }
}
