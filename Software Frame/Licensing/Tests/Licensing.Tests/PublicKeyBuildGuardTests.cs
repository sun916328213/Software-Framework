using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using Extension.Security.Cryptography;
using Xunit;

namespace Licensing.Tests;

public sealed class PublicKeyBuildGuardTests
{
    [Theory]
    [InlineData("public", true)]
    [InlineData("private", false)]
    [InlineData("pem", false)]
    [InlineData("garbage", false)]
    [InlineData("weak", false)]
    [InlineData("trailing", false)]
    [InlineData("wrongPin", false)]
    [InlineData("missingPin", false)]
    [InlineData("missingProduct", false)]
    [InlineData("defaultProduct", false)]
    [InlineData("badProduct", false)]
    public async Task ClientBuildTargetAcceptsOnlyThePinnedPublicDerKey(string kind, bool expectedSuccess)
    {
        string root = Path.Combine(Path.GetTempPath(), "PublicKeyGuard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair(kind == "weak" ? 1024 : 2048);
        string keyFile = Path.Combine(root, "input.key");
        try
        {
            string content = kind switch
            {
                "public" or "missingProduct" or "defaultProduct" or "badProduct" or "missingPin" => publicKey,
                "private" => privateKey,
                "weak" => publicKey,
                "trailing" => Convert.ToBase64String(Convert.FromBase64String(publicKey).Concat(new byte[] { 0 }).ToArray()),
                "pem" => "-----BEGIN PRIVATE KEY-----\n" + privateKey + "\n-----END PRIVATE KEY-----",
                _ => "not a key"
            };
            await File.WriteAllTextAsync(keyFile, content);
            string product = kind switch
            {
                "missingProduct" => "", "defaultProduct" => "SoftwareFrame.Default", "badProduct" => "../bad",
                _ => "tests.publish"
            };
            // 格式合法不等于身份正确：指纹钉住的是构建真正嵌入的那把钥，尾随字节那把用自己的摘要也骗不过去。
            string pin = kind switch
            {
                "wrongPin" => new string('0', 64),
                "missingPin" => "",
                _ => Fingerprint(publicKey)
            };
            var info = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in new[] { "msbuild", ClientProject(), "-nologo", "-target:ValidateLicensePublicKey",
                         "-property:Configuration=Release", "-property:LicenseProductId=" + product,
                         "-property:LicensePublicKeyFile=" + keyFile, "-property:LicenseExpectedPublicKeyFingerprint=" + pin })
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
            string output = await stdout + await stderr;
            Assert.DoesNotContain(privateKey, output);
            Assert.Equal(expectedSuccess, process.ExitCode == 0);
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>
    /// 入库公钥、文件名里的指纹、Trust.props 钉住的指纹三者必须自洽，
    /// 否则「克隆即拿到正确验证凭据」这个前提本身没人检查过。
    /// </summary>
    [Fact]
    public void RepositoryPinsTheCommittedTrustAnchor()
    {
        string build = BuildDirectory();
        var document = XDocument.Load(Path.Combine(build, "Trust.props"));
        string declared = document.Descendants("LicensePublicKeyFile").Single().Value;
        string pinned = document.Descendants("LicenseExpectedPublicKeyFingerprint").Single().Value;

        const string prefix = "$(MSBuildThisFileDirectory)";
        Assert.StartsWith(prefix, declared);
        string keyPath = Path.GetFullPath(Path.Combine(build, declared[prefix.Length..].Replace('\\', Path.DirectorySeparatorChar)));
        Assert.True(File.Exists(keyPath), $"入库公钥不存在：{keyPath}");

        byte[] der = Convert.FromBase64String(File.ReadAllText(keyPath).Trim());
        string actual = Convert.ToHexString(SHA256.HashData(der));
        Assert.Equal(pinned, actual);
        Assert.Equal($"public-{pinned}.key", Path.GetFileName(keyPath));
    }

    private static string Fingerprint(string publicKeyBase64) =>
        Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(publicKeyBase64)));

    // 公钥校验目标现在挂在承载信任锚的 Licensing.Trust 上。
    private static string ClientProject([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(source)!, "..", "..", "Licensing.Trust", "Licensing.Trust.csproj"));

    private static string BuildDirectory([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(source)!, "..", "..", "Build"));
}
