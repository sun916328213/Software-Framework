using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;

namespace Extension.Security.Protection
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
        /// <remarks>
        /// ⛔ 序 36（2026-09-18 修）：原实现**缺** <paramref name="expectedSha256Hex"/> 的空值短路，
        /// 传入 <c>null</c> 时会在 <c>expectedSha256Hex.Trim()</c> 处抛 <see cref="NullReferenceException"/>，
        /// 而该异常**不在** <see cref="IsHashUnavailable"/> 白名单里 ⇒ 直接外传。
        /// 现与同类的 <see cref="VerifyEntryAssembly"/> 对齐：空/空白期望值一律按"校验不通过"返回 <c>false</c>（fail-closed）。
        /// ⚠️ 行为变化仅限 <c>null</c>：由"抛 NRE"变为"返回 false"；空串/空白串原本就返回 false，结果不变。
        /// </remarks>
        public static bool VerifyFile(string path, string expectedSha256Hex)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256Hex)) return false;
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
