using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Xunit;

namespace Licensing.Tests;

public sealed class LicenseFormatTests
{
    [Fact]
    public void NewLicenseUsesExplicitVersionAndRoundTrips()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string text = LicenseManagerEx.Create(new LicenseInfo { Customer = "新授权" }, privateKey);
        Assert.StartsWith("SF2.", text);
        Assert.Equal(3, text.Split('.').Length);
        var result = LicenseManagerEx.Verify(text, publicKey);
        Assert.True(result.Ok);
        Assert.Equal("新授权", result.License!.Customer);
    }

    [Fact]
    public void OldSignedLicenseCannotBeUsedOrUpgradedByAddingPrefix()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LicenseInfo()));
        using var rsa = RsaEx.FromPrivateKey(privateKey);
        byte[] oldSignature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string legacy = Base64Url(payload) + "." + Base64Url(oldSignature);
        Assert.Equal(LicenseStatus.Malformed, LicenseManagerEx.Verify(legacy, publicKey).Status);
        Assert.Equal(LicenseStatus.InvalidSignature, LicenseManagerEx.Verify("SF2." + legacy, publicKey).Status);
    }

    [Fact]
    public void UnknownVersionIsRejected()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string current = LicenseManagerEx.Create(new LicenseInfo(), privateKey);
        Assert.Equal(LicenseStatus.Malformed, LicenseManagerEx.Verify("SF3." + current[4..], publicKey).Status);
        Assert.Equal(LicenseStatus.Malformed, LicenseManagerEx.Verify("sf2." + current[4..], publicKey).Status);
    }

    /// <summary>界面摘要显示 result.RemainingDays，日志文案必须引用同一个数，两处才不可能各说一个剩余时间。</summary>
    [Fact]
    public void VerifyMessage_AndTheDisplayedDayCountComeFromOneReading()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        DateTime expireAt = DateTime.UtcNow.AddDays(1).AddHours(6);
        string text = LicenseManagerEx.Create(new LicenseInfo { ExpireAtUtc = expireAt }, privateKey);

        var result = LicenseManagerEx.Verify(text, publicKey);

        Assert.True(result.Ok);
        Assert.Equal(2, result.RemainingDays);
        Assert.Equal($"License 有效，剩余约 {result.RemainingDays} 天。", result.Message);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
