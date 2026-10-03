// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ContentHash。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Security.Cryptography、System.Text。导入不代表所有类型都会在运行时加载。
using System.Security.Cryptography;
using System.Text;

namespace ScriptEditor.Runtime.Persistence;

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
