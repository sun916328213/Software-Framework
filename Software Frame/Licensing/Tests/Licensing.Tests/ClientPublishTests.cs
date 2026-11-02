using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Extension.Security.Cryptography;
using Xunit;

namespace Licensing.Tests;

public sealed class ClientPublishTests
{
    [Fact]
    [Trait("Category", "Packaging")]
    public async Task ClientPackageEmbedsOnlyPublicKeyIncludesRuntimeAndRefusesOverwrite()
    {
        string root = Path.Combine(Path.GetTempPath(), "PublishClient-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(root, "客户工具 output");
        Directory.CreateDirectory(root);
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string keyFile = Path.Combine(root, "public key.key");
        try
        {
            await File.WriteAllTextAsync(keyFile, publicKey);
            var first = await Publish(keyFile, output, Fingerprint(publicKey));
            Assert.DoesNotContain(privateKey, first.Output);
            Assert.True(first.ExitCode == 0, first.Output);
            foreach (string file in new[] { "Licensing.Activation.exe", "Licensing.Activation.dll", "Licensing.Trust.dll", "coreclr.dll", "PresentationFramework.dll", "Licensing.Client.dll", "Licensing.Core.dll", "DEPLOYMENT.txt" })
                Assert.True(File.Exists(Path.Combine(output, file)), file);
            var files = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).ToArray();
            Assert.DoesNotContain(files, file => Path.GetExtension(file) is ".pdb" or ".key" or ".pem");
            Assert.DoesNotContain(files, file => Path.GetFileName(file).StartsWith("Licensing.Authorization", StringComparison.OrdinalIgnoreCase));
            Assert.False(File.Exists(Path.Combine(output, "Extension.Licensing.dll")));
            // 托管交付：验签核心随包发布，原生 AOT 组件不再存在。
            Assert.True(File.Exists(Path.Combine(output, "Licensing.Core.dll")));
            Assert.False(File.Exists(Path.Combine(output, "Licensing.Native.dll")));
            Assert.False(File.Exists(Path.Combine(output, "Extension.Core.dll")));

            // 不加载程序集：测试宿主已引用同名程序集，Assembly.Load 第二份副本会让宿主挂起。
            // 嵌入资源是原样存储的，公钥与产品标识在字节里就是明文，直接查即可。
            byte[] trustBytes = await File.ReadAllBytesAsync(Path.Combine(output, "Licensing.Trust.dll"));
            string trustText = Encoding.ASCII.GetString(trustBytes);
            Assert.Contains(publicKey, trustText);
            Assert.Contains("tests.package", trustText);
            Assert.DoesNotContain(privateKey, trustText);

            // 公钥必须只在 Trust 里，激活工具本身不应再嵌一份。
            byte[] activationBytes = await File.ReadAllBytesAsync(Path.Combine(output, "Licensing.Activation.dll"));
            Assert.DoesNotContain(publicKey, Encoding.ASCII.GetString(activationBytes));

            string executable = Path.Combine(output, "Licensing.Activation.exe");
            byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(executable));
            var second = await Publish(keyFile, output, Fingerprint(publicKey));
            Assert.NotEqual(0, second.ExitCode);
            Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(executable)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>格式完全合法但不属于仓库钉住的签发身份：不能静默发成客户包。</summary>
    [Fact]
    public async Task PublishRefusesATrustAnchorThatIsNotPinnedInTheRepository()
    {
        string root = Path.Combine(Path.GetTempPath(), "PublishPin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string keyFile = Path.Combine(root, "public.key");
            var (_, publicKey) = RsaEx.GenerateKeyPair();
            await File.WriteAllTextAsync(keyFile, publicKey);
            var run = await Publish(keyFile, Path.Combine(root, "out"), string.Empty);
            Assert.NotEqual(0, run.ExitCode);
            Assert.Contains("is not the expected trust anchor", run.Output);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task<(int ExitCode, string Output)> Publish(string keyFile, string output, string overrideFingerprint,
        [CallerFilePath] string source = "")
    {
        string script = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "Build", "Publish-Client.ps1"));
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        var arguments = new List<string> { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                     "-PublicKeyFile", keyFile, "-ProductId", "tests.package", "-OutputDirectory", output };
        // 打包用例测的是交付内容，不是指纹钉；钉本身由下面的用例单独验证。
        if (overrideFingerprint.Length > 0)
            arguments.AddRange(new[] { "-OverrideExpectedFingerprint", overrideFingerprint });
        foreach (string argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(4)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string Fingerprint(string publicKeyBase64) =>
        Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(publicKeyBase64)));
}
