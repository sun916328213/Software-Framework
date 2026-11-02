using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace Licensing.Tests;

/// <summary>
/// 部署审计脚本的行为测试。客户包已改为托管交付，规则是：
/// 必须含 Licensing.Core.dll（验签核心）与 Licensing.Client.dll（适配层），
/// 不得含厂商工具、签发身份、备份、调试符号、密钥文件或残留的原生组件。
/// </summary>
public sealed class DeploymentAuditTests
{
    [Fact]
    public Task CompleteManagedDeploymentPasses() => Check("complete", true);

    [Fact]
    public Task MissingManagedCoreIsRejected() => Check("missingCore", false);

    [Fact]
    public Task MissingTrustAnchorIsRejected() => Check("missingTrust", false);

    [Fact]
    public Task MissingAdapterIsRejectedWhenRequired() => Check("missingAdapter", false, requireAdapter: true);

    [Fact]
    public Task IssuerCredentialStoreIsRejected() => Check("issuer", false);

    [Fact]
    public Task EncryptedIssuerBackupIsRejected() => Check("backup", false);

    [Fact]
    public Task WorkbenchIsNotACustomerDeployment() => Check("workbench", false);

    [Fact]
    public Task VendorAuthorizationToolIsRejected() => Check("authorization", false);

    [Fact]
    public Task LeftoverNativeComponentIsRejected() => Check("native", false);

    [Fact]
    public Task HiddenKeyFileIsNotMissed() => Check("hiddenKey", false);

    [Fact]
    public Task DebugSymbolsAreRejected() => Check("pdb", false);

    private static async Task Check(string scenario, bool expectedSuccess, bool requireAdapter = false,
        [CallerFilePath] string source = "")
    {
        string root = Path.Combine(Path.GetTempPath(), "DeploymentAudit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            if (scenario != "missingCore")
                await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Core.dll"), "marker, never executed");
            if (scenario != "missingTrust")
                await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Trust.dll"), "marker, never executed");
            if (scenario is not ("missingAdapter" or "missingCore"))
                await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Client.dll"), "marker, never executed");
            if (scenario == "issuer") await File.WriteAllTextAsync(Path.Combine(root, "issuer.bin"), "forbidden marker");
            if (scenario == "backup") await File.WriteAllTextAsync(Path.Combine(root, "backup.sfib"), "forbidden marker");
            if (scenario == "workbench") await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Workbench.exe"), "not a customer host");
            if (scenario == "authorization") await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Authorization.exe"), "vendor tool");
            if (scenario == "native") await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Native.dll"), "stale native component");
            if (scenario == "pdb") await File.WriteAllTextAsync(Path.Combine(root, "Licensing.Core.pdb"), "debug symbols");
            if (scenario == "hiddenKey")
            {
                string hidden = Path.Combine(root, "private.key");
                await File.WriteAllTextAsync(hidden, "test marker, not a key");
                File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
            }
            string script = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "Build", "Test-ProtectedDeployment.ps1"));
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Directory", root })
                start.ArgumentList.Add(argument);
            if (requireAdapter) start.ArgumentList.Add("-RequireManagedAdapter");
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
            string output = await stdout + await stderr;
            Assert.True((process.ExitCode == 0) == expectedSuccess, output);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
