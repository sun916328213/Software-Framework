// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IRequestCodec。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers。导入不代表所有类型都会在运行时加载。
using System.Buffers;

namespace Communication.Core.Abstractions;

/// <summary>在类型化请求/响应与协议帧之间转换的无状态 codec。</summary>
public interface IRequestCodec<in TRequest, TResponse>
{
    /// <summary>编码一个带关联号的完整请求帧。</summary>
    ReadOnlyMemory<byte> Encode(TRequest request, ushort correlationId);

    /// <summary>
    /// 尝试从缓冲区读取一个完整响应。
    /// 半包返回 false 且不消费输入；完整帧通过 <paramref name="consumed"/> 返回消费位置。
    /// </summary>
    bool TryDecode(
        ReadOnlySequence<byte> buffer,
        ushort correlationId,
        out TResponse response,
        out SequencePosition consumed);
}
