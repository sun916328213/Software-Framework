using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace Extension.Core.SecurityEx
{
    /// <summary>
    /// 机器码扩展（MAC + 机器名 + 磁盘卷序列号 + CPU/OS 信息做 SHA256，隐私友好：只存哈希）
    /// </summary>
    public static class MachineCodeEx
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder? lpVolumeNameBuffer, int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder? lpFileSystemNameBuffer, int nFileSystemNameSize);

        /// <summary>
        /// 获取本机机器码（格式 XXXX-XXXX-XXXX-XXXX，同一台机器多次调用结果稳定）
        /// </summary>
        public static string GetMachineCode()
        {
            string raw = CollectFingerprint();
            string hex = raw.Sha256Hex();
            // 取前 16 个字符，分 4 组显示
            string part = hex[..16].ToUpperInvariant();
            return $"{part[..4]}-{part.Substring(4, 4)}-{part.Substring(8, 4)}-{part.Substring(12, 4)}";
        }

        /// <summary>
        /// 校验机器码是否属于本机
        /// </summary>
        public static bool IsCurrentMachine(string machineCode)
        {
            if (string.IsNullOrWhiteSpace(machineCode)) return false;
            return string.Equals(GetMachineCode(), machineCode.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 采集机器指纹原文（内部使用，不对外持久化原文，只持久化哈希后的机器码）
        /// </summary>
        private static string CollectFingerprint()
        {
            var sb = new StringBuilder();
            sb.Append(Environment.MachineName).Append('|');

            try
            {
                var macs = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                    .Select(n => n.GetPhysicalAddress().ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToArray();
                sb.Append(string.Join(",", macs)).Append('|');
            }
            catch (Exception ex) when (ex is NetworkInformationException
                                          or Win32Exception
                                          or PlatformNotSupportedException)
            {
                // 采集不到 MAC 时用占位符降级，保证机器码仍然可生成（只是区分度下降）。
                // 必须留痕：若大量机器都落到 NOMAC，机器码会重复，授权会误判为"换机"。
                Debug.WriteLine($"[MachineCodeEx] 采集 MAC 失败，使用占位符：{ex.Message}");
                sb.Append("NOMAC|");
            }

            sb.Append(GetSystemDriveSerial()).Append('|');
            sb.Append(Environment.ProcessorCount).Append('|');
            sb.Append(RuntimeInformation.OSDescription).Append('|');
            sb.Append(RuntimeInformation.OSArchitecture);
            return sb.ToString();
        }

        private static string GetSystemDriveSerial()
        {
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                if (GetVolumeInformation(root, null, 0, out uint serial,
                        out _, out _, null, 0))
                    return serial.ToString("X8");
            }
            catch (Exception ex) when (ex is Win32Exception
                                          or ExternalException
                                          or ArgumentException
                                          or PlatformNotSupportedException)
            {
                // 拿不到卷序列号（无盘/容器/权限受限）时用占位符降级，保证机器码仍可生成。
                // 留痕：若大量机器都落到 NOSERIAL，机器码会重复，授权会误判为"换机"。
                Debug.WriteLine($"[MachineCodeEx] 获取系统盘序列号失败，使用占位符：{ex.Message}");
            }
            return "NOSERIAL";
        }
    }
}
