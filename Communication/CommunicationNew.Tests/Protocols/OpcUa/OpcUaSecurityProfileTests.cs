using CommunicationNew.Protocols.OpcUa;
using Opc.Ua;
using Xunit;

namespace CommunicationNew.Tests.Protocols.OpcUa;

public sealed class OpcUaSecurityProfileTests
{
    [Fact]
    public void None_profile_is_supported_for_backward_compatibility()
    {
        var profile = OpcUaSecurityProfile.Parse(new OpcUaParam());

        Assert.Equal(MessageSecurityMode.None, profile.Mode);
        Assert.Equal(SecurityPolicies.None, profile.PolicyUri);
        Assert.False(profile.UseSecurity);
    }

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

    [Fact]
    public void Unsecured_profile_rejects_user_credentials()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpcUaSecurityProfile.Parse(new OpcUaParam { UserName = "operator" }));

        Assert.Contains("拒绝发送用户名和密码", exception.Message);
    }

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
