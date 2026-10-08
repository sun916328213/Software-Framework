// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：OpcUaSecurityProfileTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.OpcUa、Communication.Runtime.Protocols.OpcUa、Opc.Ua、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.OpcUa;
using Communication.Runtime.Protocols.OpcUa;
using Opc.Ua;
using Xunit;

namespace Communication.Tests.Protocols.OpcUa;

/// <summary>
/// OpcUaSecurityProfileTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class OpcUaSecurityProfileTests
{
    /// <summary>
    /// 测试相关入口 None_profile_is_supported_for_backward_compatibility；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void None_profile_is_supported_for_backward_compatibility()
    {
        var profile = OpcUaSecurityProfile.Parse(new OpcUaParam());

        Assert.Equal(MessageSecurityMode.None, profile.Mode);
        Assert.Equal(SecurityPolicies.None, profile.PolicyUri);
        Assert.False(profile.UseSecurity);
    }

    /// <summary>
    /// 测试相关入口 SignAndEncrypt_accepts_modern_security_policies；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、Equal、True；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("Basic256Sha256", SecurityPolicies.Basic256Sha256)]
    [InlineData("Aes128_Sha256_RsaOaep", SecurityPolicies.Aes128_Sha256_RsaOaep)]
    [InlineData("Aes256_Sha256_RsaPss", SecurityPolicies.Aes256_Sha256_RsaPss)]
    public void SignAndEncrypt_accepts_modern_security_policies(
        string policy,
        string expectedPolicyUri)
    {
        var profile = OpcUaSecurityProfile.Parse(new OpcUaParam
        {
            SecurityMode = "SignAndEncrypt",
            SecurityPolicy = policy,
        });

        Assert.Equal(MessageSecurityMode.SignAndEncrypt, profile.Mode);
        Assert.Equal(expectedPolicyUri, profile.PolicyUri);
        Assert.True(profile.UseSecurity);
    }

    /// <summary>
    /// 测试相关入口 Deprecated_security_policies_are_rejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Parse、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("Basic128Rsa15")]
    [InlineData("Basic256")]
    public void Deprecated_security_policies_are_rejected(string policy)
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            OpcUaSecurityProfile.Parse(new OpcUaParam
            {
                SecurityMode = "SignAndEncrypt",
                SecurityPolicy = policy,
            }));

        Assert.Contains("已弃用", exception.Message);
    }

    /// <summary>
    /// 测试相关入口 Unsecured_profile_rejects_user_credentials；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Parse、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Unsecured_profile_rejects_user_credentials()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpcUaSecurityProfile.Parse(new OpcUaParam { UserName = "operator" }));

        Assert.Contains("拒绝发送用户名和密码", exception.Message);
    }

    /// <summary>
    /// 测试相关入口 Security_mode_and_policy_must_be_consistent；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Parse、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("None", "Basic256Sha256")]
    [InlineData("SignAndEncrypt", "None")]
    public void Security_mode_and_policy_must_be_consistent(
        string securityMode,
        string securityPolicy)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpcUaSecurityProfile.Parse(new OpcUaParam
            {
                SecurityMode = securityMode,
                SecurityPolicy = securityPolicy,
            }));

        Assert.Contains("必须", exception.Message);
    }

    /// <summary>
    /// 测试相关入口 Secure_profile_requires_at_least_2048_bit_rsa_key；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Parse、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Secure_profile_requires_at_least_2048_bit_rsa_key()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            OpcUaSecurityProfile.Parse(new OpcUaParam
            {
                SecurityMode = "SignAndEncrypt",
                SecurityPolicy = "Basic256Sha256",
                MinimumCertificateKeySize = 1024,
            }));

        Assert.Equal("MinimumCertificateKeySize", exception.ParamName);
    }
}
