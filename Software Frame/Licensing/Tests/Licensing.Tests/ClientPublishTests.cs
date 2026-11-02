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
            var first = await Publish(keyFile, output);
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
            var second = await Publish(keyFile, output);
            Assert.NotEqual(0, second.ExitCode);
            Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(executable)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task<(int ExitCode, string Output)> Publish(string keyFile, string output,
        [CallerFilePath] string source = "")
    {
        string script = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "Build", "Publish-Client.ps1"));
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                     "-PublicKeyFile", keyFile, "-ProductId", "tests.package", "-OutputDirectory", output })
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(4)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }
}
