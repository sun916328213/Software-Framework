using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Extension.Core.SecurityEx;

namespace Extension.Core.ProtectionEx
{
    /// <summary>
    /// 防篡改：程序集完整性校验 + ILDasm 抑制检查
    /// 说明：
    /// 1) 真正的防反编译靠混淆器（ConfuserEx/NetReactor）+ 加壳 + NativeAOT，本库只做运行时兜底；
    /// 2) 如需让 ildasm 直接拒绝反编译，请在主程序 csproj/AssemblyInfo 中加：
    ///    [assembly: System.Runtime.CompilerServices.SuppressIldasmAttribute]
    /// </summary>
    public static class AntiTamperEx
    {
        /// <summary>
        /// 计算当前入口程序集文件的 SHA256
        /// </summary>
        public static string GetEntryAssemblyHash()
        {
            string? path = Assembly.GetEntryAssembly()?.Location
                ?? Environment.ProcessPath
                ?? throw new InvalidOperationException("无法定位入口程序集路径。");
            return HashEx.Sha256File(path);
        }

        /// <summary>
        /// 校验入口程序集是否被篡改（发布时先算好哈希硬编码/放 License Extra 里）
        /// </summary>
        public static bool VerifyEntryAssembly(string expectedSha256Hex)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256Hex)) return false;
            try
            {
                string actual = GetEntryAssemblyHash();
                return string.Equals(actual, expectedSha256Hex.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (IsHashUnavailable(ex))
            {
                // 无法读取/计算哈希时按"校验不通过"处理（fail-closed），
                // 但必须留痕：否则"文件被杀软占用""无读取权限"这类环境问题会被误判成程序集被篡改。
                Debug.WriteLine($"[AntiTamperEx] 校验入口程序集哈希失败，按不通过处理：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 校验任意文件哈希
        /// </summary>
        public static bool VerifyFile(string path, string expectedSha256Hex)
        {
            try
            {
                if (!File.Exists(path)) return false;
                return string.Equals(HashEx.Sha256File(path), expectedSha256Hex.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (IsHashUnavailable(ex))
            {
                Debug.WriteLine($"[AntiTamperEx] 校验文件 '{path}' 哈希失败，按不通过处理：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 主程序集是否声明了 SuppressIldasmAttribute（能挡住裸 ildasm，不防 dnSpy）
        /// </summary>
        public static bool IsIldasmSuppressed(Assembly? assembly = null)
        {
            assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
            return assembly.GetCustomAttributesData().Any(a =>
                a.AttributeType.FullName == "System.Runtime.CompilerServices.SuppressIldasmAttribute");
        }

        /// <summary>
        /// 判断异常是否属于"文件不可读/哈希不可计算"这类环境问题。
        /// 这类异常按校验不通过处理；其余异常（如参数为空、实现缺陷）继续抛出，避免掩盖真实缺陷。
        /// </summary>
        private static bool IsHashUnavailable(Exception ex) =>
            ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.Security.SecurityException
                or NotSupportedException;
    }
}
