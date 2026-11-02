using System.Security.Cryptography;
using System.Text;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Xunit;

namespace Licensing.Tests;

public sealed class ProductIsolationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ProductIsolation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SameKeyCannotMixProductsEvenIfLicenseFileIsCopied()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var a = new LicenseFileStore(publicKey, _root, productId: "product.A");
        var b = new LicenseFileStore(publicKey, _root, productId: "product.B");
        Assert.NotEqual(a.LicensePath, b.LicensePath);
        string source = Write(LicenseManagerEx.Create(new LicenseInfo { ProductId = "product.A" }, privateKey));
        Assert.True(a.ImportFile(source).Ok);
        Assert.Equal(LicenseStatus.ProductMismatch, b.ImportFile(source).Status);
        Directory.CreateDirectory(Path.GetDirectoryName(b.LicensePath)!);
        File.Copy(a.LicensePath, b.LicensePath);
        Assert.Equal(LicenseStatus.ProductMismatch, b.Load().Status);
        Assert.True(a.Load().Ok);
    }

    [Fact]
    public void TimeStateAndRecoveryRequestsAreBoundToProduct()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var a = new LicenseFileStore(publicKey, _root, productId: "product.A");
        var b = new LicenseFileStore(publicKey, _root, productId: "product.B");
        Assert.True(a.ImportFile(Write(LicenseManagerEx.Create(new LicenseInfo
            { ProductId = "product.A", ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, privateKey))).Ok);
        Assert.True(b.ImportFile(Write(LicenseManagerEx.Create(new LicenseInfo
            { ProductId = "product.B", ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, privateKey))).Ok);
        File.Copy(a.LicensePath + ".time", b.LicensePath + ".time", true);
        Assert.Equal(LicenseStatus.TimeStateInvalid, b.Load().Status);
        Assert.True(a.Load().Ok);
        string request = a.CreateTimeRecoveryRequest();
        Assert.Equal("product.A", LicenseRecoveryRequest.GetProductId(request));
        Assert.Throws<ArgumentException>(() => LicenseRecoveryRequest.AttachTo(new LicenseInfo
            { ProductId = "product.B", ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, request));
    }

    [Fact]
    public void ProductionSignerRequiresExplicitProductAndV2Machine()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        Assert.Throws<ArgumentException>(() => LicenseManagerEx.CreateBound(new LicenseInfo(), privateKey));
        Assert.Throws<ArgumentException>(() => LicenseManagerEx.CreateBound(new LicenseInfo { ProductId = "product.A" }, privateKey));
        Assert.Throws<ArgumentException>(() => LicenseManagerEx.CreateBound(new LicenseInfo
            { ProductId = "product.A", MachineCode = "AAAA-BBBB-CCCC-DDDD" }, privateKey));
        var info = new LicenseInfo { ProductId = "product.A", MachineCode = MachineCodeEx.GetMachineCode() };
        string text = LicenseManagerEx.CreateBound(info, privateKey);
        Assert.True(LicenseManagerEx.Verify(text, publicKey, expectedProductId: "product.A").Ok);
        Assert.Equal(LicenseStatus.ProductMismatch,
            LicenseManagerEx.Verify(text, publicKey, expectedProductId: "product.a").Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("with space")]
    [InlineData("产品")]
    public void InvalidProductIdCannotSelectStoragePath(string product)
    {
        var (_, key) = RsaEx.GenerateKeyPair();
        Assert.Throws<ArgumentException>(() => new LicenseFileStore(key, _root, productId: product));
    }

    [Fact]
    public void MissingProductFieldIsRejectedEvenWithValidSignature()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string signed = "SF2." + payload;
        using var rsa = RsaEx.FromPrivateKey(privateKey);
        string signature = Convert.ToBase64String(rsa.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(LicenseStatus.Malformed, LicenseManagerEx.Verify(signed + "." + signature, publicKey).Status);
    }

    private string Write(string text)
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "source.lic");
        File.WriteAllText(file, text);
        return file;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
