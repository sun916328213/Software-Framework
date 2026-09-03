using System.Buffers;

namespace CommunicationNew.Abstractions;

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
