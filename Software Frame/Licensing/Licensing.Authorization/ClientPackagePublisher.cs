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
            if (log.Contains("Platform linker not found", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "客户包生成失败：找不到 Native AOT 链接器（link.exe）。\n" +
                    "工具已尝试直接定位 MSVC 与 Windows SDK（见上方 diagnostics 行）。仍然失败通常是这两种情况：\n" +
                    "  · 没装「使用 C++ 的桌面开发」工作负载（含 MSVC 生成工具与 Windows SDK）\n" +
                    "  · Visual Studio 安装已损坏：Installer 里提示需要修复，vswhere 查不到 C++ 组件\n" +
                    "请在 Visual Studio Installer 里修复或补装该工作负载，再重新打开本工具。\n\n" + log);
            throw new InvalidOperationException("客户包生成失败，该目录不可交付。\n" + log +
                "\n\n常见原因：输出目录非空；NuGet 包源不可用；缺少 Native AOT C++ 工具链。");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true); }
    }
}
