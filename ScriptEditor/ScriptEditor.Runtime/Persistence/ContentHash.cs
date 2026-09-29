using System.Security.Cryptography;
using System.Text;

namespace ScriptEditor.Persistence;

/// <summary>内容哈希（纯函数）。</summary>
public static class ContentHash
{
    /// <summary>SHA256 十六进制短哈希（默认 16 字符），用于镜像清单比对与调试源缓存键。</summary>
    public static string Sha256Short(string content, int length = 16)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        var hex = Convert.ToHexString(bytes);
        return hex[..Math.Min(length, hex.Length)];
    }
}
