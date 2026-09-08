using Opc.Ua;

namespace CommunicationNew.Protocols.OpcUa;

/// <summary>
/// 将公共字符串配置收敛为 SDK 可执行的安全配置，保证客户端与服务端使用同一套规则。
/// </summary>
internal sealed record OpcUaSecurityProfile(
    MessageSecurityMode Mode,
    string PolicyUri)
{
    /// <summary>当前配置是否要求安全通道。</summary>
    public bool UseSecurity => Mode != MessageSecurityMode.None;

    /// <summary>解析并校验 OPC UA 安全配置。</summary>
    public static OpcUaSecurityProfile Parse(OpcUaParam param)
    {
        ArgumentNullException.ThrowIfNull(param);

        if (!Enum.TryParse<MessageSecurityMode>(param.SecurityMode, true, out var mode) ||
            mode is not (MessageSecurityMode.None or
                MessageSecurityMode.Sign or
                MessageSecurityMode.SignAndEncrypt))
        {
            throw new NotSupportedException(
                $"不支持 OPC UA 安全模式：{param.SecurityMode}。");
        }

        var policyUri = ResolvePolicyUri(param.SecurityPolicy);
        var isNonePolicy = string.Equals(
            policyUri,
            SecurityPolicies.None,
            StringComparison.Ordinal);
        if ((mode == MessageSecurityMode.None) != isNonePolicy)
        {
            throw new InvalidOperationException(
                "SecurityMode=None 必须与 SecurityPolicy=None 成对；安全模式必须使用加密策略。");
        }

        if (mode == MessageSecurityMode.None && !string.IsNullOrWhiteSpace(param.UserName))
        {
            throw new InvalidOperationException(
                "SecurityMode=None 时拒绝发送用户名和密码，请使用安全端点配置。");
        }

        if (mode != MessageSecurityMode.None && param.MinimumCertificateKeySize < 2048)
        {
            throw new ArgumentOutOfRangeException(
                nameof(param.MinimumCertificateKeySize),
                param.MinimumCertificateKeySize,
                "安全端点的 RSA 证书密钥长度不能小于 2048 位。");
        }

        return new OpcUaSecurityProfile(mode, policyUri);
    }

    private static string ResolvePolicyUri(string policy)
    {
        if (Matches(policy, "Basic128Rsa15", SecurityPolicies.Basic128Rsa15) ||
            Matches(policy, "Basic256", SecurityPolicies.Basic256))
        {
            throw new NotSupportedException(
                $"OPC UA 安全策略 {policy} 已弃用，请使用 Basic256Sha256 或 AES 策略。");
        }

        if (Matches(policy, "None", SecurityPolicies.None))
        {
            return SecurityPolicies.None;
        }
        if (Matches(policy, "Basic256Sha256", SecurityPolicies.Basic256Sha256))
        {
            return SecurityPolicies.Basic256Sha256;
        }
        if (Matches(policy, "Aes128_Sha256_RsaOaep", SecurityPolicies.Aes128_Sha256_RsaOaep))
        {
            return SecurityPolicies.Aes128_Sha256_RsaOaep;
        }
        if (Matches(policy, "Aes256_Sha256_RsaPss", SecurityPolicies.Aes256_Sha256_RsaPss))
        {
            return SecurityPolicies.Aes256_Sha256_RsaPss;
        }

        throw new NotSupportedException($"不支持 OPC UA 安全策略：{policy}。");
    }

    private static bool Matches(string value, string name, string uri) =>
        string.Equals(value, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, uri, StringComparison.OrdinalIgnoreCase);
}
