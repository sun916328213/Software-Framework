using System.Diagnostics;
using System.IO;
using System.Text;

namespace Licensing.Authorization;

internal static class ClientPackagePublisher
{
    /// <summary>
    /// 从 Visual Studio 启动本工具时，这些变量会注入子进程，把 dotnet publish / ILCompiler
    /// 引向 VS 自带的 MSBuild 或不匹配的工具链，表现为 "Platform linker not found"。
    /// 移除后由 dotnet 自行定位 SDK 与 MSVC（已在干净环境验证可成功）。
    /// </summary>
    private static readonly string[] InheritedBuildVariables =
    [
        "VCToolsInstallDir", "VCToolsVersion", "VCINSTALLDIR", "VSINSTALLDIR", "VisualStudioVersion",
        "WindowsSdkDir", "WindowsSdkBinPath", "WindowsSdkVerBinPath", "WindowsSDKVersion",
        "INCLUDE", "LIB", "LIBPATH",
        "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBuildLoadMicrosoftTargetsReadOnly",
        "DOTNET_HOST_PATH", "DOTNET_ROOT", "DOTNET_ROOT(x86)", "VSLANG"
    ];

    internal static async Task PublishAsync(string moduleRoot, string publicKey, string productId, string output,
        CancellationToken cancellationToken)
    {
        string script = Path.Combine(moduleRoot, "Build", "Publish-Client.ps1");
        if (!File.Exists(script) || !File.Exists(Path.Combine(moduleRoot, "Licensing.Activation", "Licensing.Activation.csproj")))
            throw new DirectoryNotFoundException("生成客户包需要完整授权模块源码和 .NET SDK，请在开发机操作。");
        string temporary = Path.Combine(Path.GetTempPath(), "IssuerPublish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string keyFile = Path.Combine(temporary, "public.key");
            await File.WriteAllTextAsync(keyFile, publicKey, cancellationToken);
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // 子进程输出统一按 UTF-8 解码，否则中文日志会变成乱码。
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string name in InheritedBuildVariables) start.Environment.Remove(name);
            // 从 Git Bash 等精简环境启动本工具时，ProgramFiles 系变量可能为空，
            // 子进程里的 NuGet 会在静态构造阶段直接抛错；这里补上标准值。
            foreach (var (name, fallback) in new[]
                     {
                         ("ProgramFiles", @"C:\Program Files"),
                         ("ProgramFiles(x86)", @"C:\Program Files (x86)"),
                         ("ProgramData", @"C:\ProgramData"),
                         ("ALLUSERSPROFILE", @"C:\ProgramData"),
                         ("CommonProgramFiles", @"C:\Program Files\Common Files"),
                     })
            {
                if (!start.Environment.TryGetValue(name, out string? current) || string.IsNullOrEmpty(current))
                    start.Environment[name] = fallback;
            }
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                         "-PublicKeyFile", keyFile, "-ProductId", productId, "-OutputDirectory", output })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(cancellationToken); }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                await Task.WhenAll(stdout, stderr);
                throw;
            }
            string log = await stdout + await stderr;
            if (process.ExitCode == 0) return;
            // 交付前最常见的真实失败：本机签发身份与仓库钉住的信任锚不是同一把。
            // 必须原样顶出来并给出下一步，否则这行英文会被下面的通用文案淹掉，让人以为是工具链坏了。
            if (log.Contains("is not the expected trust anchor", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "客户包生成被拒绝：本机签发身份的公钥与仓库钉住的信任锚不是同一把。\n" +
                    "这是设计如此 —— 不允许发布一把「没人评审过」的验证凭据。\n" +
                    "换身份后的正确顺序：把新公钥写入 Build\\Keys\\public-<指纹>.key，更新 Build\\Trust.props 的文件名与指纹，提交，再重新生成客户包。\n" +
                    "只想用临时身份自测：改 .local\\client.props 并同时提供 LicenseExpectedPublicKeyFingerprint（该配置不会被发布）。\n\n" + log);
            throw new InvalidOperationException(
                "客户包生成失败，该目录不可交付。\n" + log +
                "\n\n常见原因：输出目录非空；NuGet 包源不可用；产品标识或公钥参数不合法。");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true); }
    }
}
