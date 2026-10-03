// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：MxComponentWordCodec。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、System。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using System;

namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>
/// 在 MX Component 的有符号 16 位字与 MC 小端字节负载之间转换。
/// 显式移位避免结果依赖宿主机端序。
/// </summary>
internal static class MxComponentWordCodec
{
    /// <summary>把 MX 返回的字数组编码为 MC 小端字节负载。</summary>
    public static byte[] ToPayload(short[] words)
    {
        if (words is null)
        {
            throw new ArgumentNullException(nameof(words));
        }

        var payload = new byte[words.Length * 2];
        for (var index = 0; index < words.Length; index++)
        {
            var value = unchecked((ushort)words[index]);
            payload[index * 2] = (byte)value;
            payload[index * 2 + 1] = (byte)(value >> 8);
        }
        return payload;
    }

    /// <summary>把 MC 小端字节负载解码为 MX 写入所需的字数组。</summary>
    public static short[] FromPayload(byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }
        if ((payload.Length & 1) != 0)
        {
            throw new ArgumentException("MX 字负载必须包含完整的 16 位字。", nameof(payload));
        }

        var words = new short[payload.Length / 2];
        for (var index = 0; index < words.Length; index++)
        {
            var value = payload[index * 2] | payload[index * 2 + 1] << 8;
            words[index] = unchecked((short)value);
        }
        return words;
    }
}
