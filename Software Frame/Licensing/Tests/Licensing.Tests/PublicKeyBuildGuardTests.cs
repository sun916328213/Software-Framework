using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    [InlineData("missingProduct", false)]
    [InlineData("defaultProduct", false)]
    [InlineData("badProduct", false)]
    public async Task ClientBuildTargetAcceptsOnlyPublicDerKey(string kind, bool expectedSuccess)
    {
        string root = Path.Combine(Path.GetTempPath(), "PublicKeyGuard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair(kind == "weak" ? 1024 : 2048);
        string keyFile = Path.Combine(root, "input.key");
        try
        {
            string content = kind switch
            {
                "public" or "missingProduct" or "defaultProduct" or "badProduct" => publicKey,
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
            var info = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in new[] { "msbuild", ClientProject(), "-nologo", "-target:ValidateLicensePublicKey",
                         "-property:Configuration=Release", "-property:LicenseProductId=" + product, "-property:LicensePublicKeyFile=" + keyFile })
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

    // 公钥校验目标现在挂在承载信任锚的 Licensing.Trust 上。
    private static string ClientProject([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(source)!, "..", "..", "Licensing.Trust", "Licensing.Trust.csproj"));
}
