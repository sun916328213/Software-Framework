using Extension.Security.Cryptography;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Extension.Security.Licensing;

/// <summary>V2 机器码：基于 Windows 安装标识和系统卷序列号，不依赖网卡连接状态。</summary>
public static class MachineCodeEx
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetVolumeInformation(
        string rootPathName, StringBuilder? volumeNameBuffer, int volumeNameSize,
        out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer, int fileSystemNameSize);

    /// <summary>获取 V2 机器码。关键标识不可读取时拒绝生成，避免不同机器共用占位符。</summary>
    public static string GetMachineCode()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("V2 机器码仅支持 Windows。");

        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var crypto = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
        var value = crypto?.GetValue("MachineGuid") as string;
        if (!Guid.TryParse(value, out var machineGuid) || machineGuid == Guid.Empty)
            throw new InvalidOperationException("无法读取有效的 Windows 机器标识，请联系供应商。");

        string? root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrEmpty(root))
            throw new InvalidOperationException("无法确定 Windows 系统卷，请联系供应商。");
        if (!GetVolumeInformation(root, null, 0, out uint serial, out _, out _, null, 0))
            throw new InvalidOperationException("无法读取 Windows 系统卷标识，请联系供应商。",
                new Win32Exception(Marshal.GetLastWin32Error()));

        string hash = $"software-frame-machine-v2|{machineGuid:D}|{serial:X8}".Sha256Hex().ToUpperInvariant();
        return $"V2-{hash[..4]}-{hash[4..8]}-{hash[8..12]}-{hash[12..16]}";
    }

    /// <summary>验证新方案的机器码格式；不读取本机标识。</summary>
    public static bool IsV2Format(string? value)
    {
        if (value == null || value.Length != 22 || !value.StartsWith("V2-", StringComparison.Ordinal)) return false;
        for (int i = 3; i < value.Length; i++)
        {
            if (i is 7 or 12 or 17) { if (value[i] != '-') return false; }
            else if (!(value[i] is >= '0' and <= '9' or >= 'A' and <= 'F')) return false;
        }
        return true;
    }

    /// <summary>校验机器码是否属于本机。</summary>
    public static bool IsCurrentMachine(string machineCode)
    {
        if (string.IsNullOrWhiteSpace(machineCode)) return false;
        return string.Equals(GetMachineCode(), machineCode.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
