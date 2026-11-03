// 【中文阅读说明】
// 共享基础能力：Base64Url 无填充编解码。授权文本（SF2）的载荷段与签名段都走这里，
// 签发方和验证方必须字节级一致，所以只实现一次；宿主和工具不要再各自补一份副本。
using System;

namespace Extension.Security.Cryptography
{
    /// <summary>RFC 4648 §5 无填充 Base64Url。</summary>
    public static class Base64Url
    {
        public static string Encode(ReadOnlySpan<byte> data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] Decode(string text)
        {
            string padded = text.Replace('-', '+').Replace('_', '/');
            int remainder = padded.Length % 4;
            if (remainder == 2) padded += "==";
            else if (remainder == 3) padded += "=";
            return Convert.FromBase64String(padded);
        }
    }
}
