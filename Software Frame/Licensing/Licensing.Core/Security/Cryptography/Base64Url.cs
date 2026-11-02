// 【中文阅读说明】
// 共享基础能力：Base64Url 无填充编解码。授权文本（SF2/SF3）与信任清单（SFM1）共用，
// 必须两侧字节级一致，因此集中在这里实现一次。
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
