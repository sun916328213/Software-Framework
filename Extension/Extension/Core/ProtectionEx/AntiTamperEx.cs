using System;
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
            catch
            {
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
            catch
            {
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
    }
}
