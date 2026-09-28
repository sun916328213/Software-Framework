using CommunicationNew.Abstractions;

namespace CommunicationNew.Runtime.Conversion;

/// <summary>
/// 旧命名空间中的端序转换兼容 adapter；算法由 Core 中的当前实现统一持有。
/// </summary>
[Obsolete("请使用 CommunicationNew.Core.Converters.DataFormatCodec；旧类型将在后续主版本移除。")]
public sealed class DataFormatCodec
{
    private readonly Core.Converters.DataFormatCodec _inner;

    public DataFormatCodec(DataFormat format)
    {
        _inner = new Core.Converters.DataFormatCodec(format);
    }

    public DataFormat Format => _inner.Format;

    public short DecodeInt16(ReadOnlySpan<byte> source) => _inner.DecodeInt16(source);

    public ushort DecodeUInt16(ReadOnlySpan<byte> source) => _inner.DecodeUInt16(source);

    public byte[] EncodeInt16(short value) => _inner.EncodeInt16(value);

    public byte[] EncodeUInt16(ushort value) => _inner.EncodeUInt16(value);

    public int DecodeInt32(ReadOnlySpan<byte> source) => _inner.DecodeInt32(source);

    public byte[] EncodeInt32(int value) => _inner.EncodeInt32(value);

    public uint DecodeUInt32(ReadOnlySpan<byte> source) => _inner.DecodeUInt32(source);

    public byte[] EncodeUInt32(uint value) => _inner.EncodeUInt32(value);

    public float DecodeSingle(ReadOnlySpan<byte> source) => _inner.DecodeSingle(source);

    public byte[] EncodeSingle(float value) => _inner.EncodeSingle(value);
}
