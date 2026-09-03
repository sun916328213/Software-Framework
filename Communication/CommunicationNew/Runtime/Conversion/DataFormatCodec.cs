using System.Buffers.Binary;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Runtime.Conversion;

/// <summary>按照不可变的设备数据格式转换数值，避免进程级字节序状态互相污染。</summary>
public sealed class DataFormatCodec
{
    public DataFormatCodec(DataFormat format)
    {
        Format = format;
    }

    public DataFormat Format { get; }

    /// <summary>按设备格式读取 16 位有符号整数。</summary>
    public short DecodeInt16(ReadOnlySpan<byte> source) =>
        unchecked((short)DecodeUInt16(source));

    /// <summary>按设备格式读取 16 位无符号整数。</summary>
    public ushort DecodeUInt16(ReadOnlySpan<byte> source)
    {
        if (source.Length < sizeof(ushort))
        {
            throw new ArgumentException("16 位数值需要两个字节。", nameof(source));
        }

        return Format is DataFormat.BADC or DataFormat.DCBA
            ? BinaryPrimitives.ReadUInt16LittleEndian(source)
            : BinaryPrimitives.ReadUInt16BigEndian(source);
    }

    /// <summary>按设备格式写入 16 位有符号整数。</summary>
    public byte[] EncodeInt16(short value) => EncodeUInt16(unchecked((ushort)value));

    /// <summary>按设备格式写入 16 位无符号整数。</summary>
    public byte[] EncodeUInt16(ushort value)
    {
        var destination = new byte[sizeof(ushort)];
        if (Format is DataFormat.BADC or DataFormat.DCBA)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        }

        return destination;
    }

    public int DecodeInt32(ReadOnlySpan<byte> source)
    {
        if (source.Length < sizeof(int))
        {
            throw new ArgumentException("A 32-bit value requires four bytes.", nameof(source));
        }

        Span<byte> canonical = stackalloc byte[sizeof(int)];
        ToCanonical(source, canonical);
        return BinaryPrimitives.ReadInt32BigEndian(canonical);
    }

    public byte[] EncodeInt32(int value)
    {
        Span<byte> canonical = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(canonical, value);

        var destination = new byte[sizeof(int)];
        FromCanonical(canonical, destination);
        return destination;
    }

    /// <summary>按设备格式读取 32 位无符号整数。</summary>
    public uint DecodeUInt32(ReadOnlySpan<byte> source) =>
        unchecked((uint)DecodeInt32(source));

    /// <summary>按设备格式写入 32 位无符号整数。</summary>
    public byte[] EncodeUInt32(uint value) => EncodeInt32(unchecked((int)value));

    /// <summary>按设备格式读取单精度浮点数。</summary>
    public float DecodeSingle(ReadOnlySpan<byte> source) =>
        BitConverter.Int32BitsToSingle(DecodeInt32(source));

    /// <summary>按设备格式写入单精度浮点数。</summary>
    public byte[] EncodeSingle(float value) =>
        EncodeInt32(BitConverter.SingleToInt32Bits(value));

    private void ToCanonical(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        switch (Format)
        {
            case DataFormat.ABCD:
                source[..4].CopyTo(destination);
                break;
            case DataFormat.BADC:
                destination[0] = source[1];
                destination[1] = source[0];
                destination[2] = source[3];
                destination[3] = source[2];
                break;
            case DataFormat.CDAB:
                destination[0] = source[2];
                destination[1] = source[3];
                destination[2] = source[0];
                destination[3] = source[1];
                break;
            case DataFormat.DCBA:
                destination[0] = source[3];
                destination[1] = source[2];
                destination[2] = source[1];
                destination[3] = source[0];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Format));
        }
    }

    private void FromCanonical(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        switch (Format)
        {
            case DataFormat.ABCD:
                source.CopyTo(destination);
                break;
            case DataFormat.BADC:
                destination[0] = source[1];
                destination[1] = source[0];
                destination[2] = source[3];
                destination[3] = source[2];
                break;
            case DataFormat.CDAB:
                destination[0] = source[2];
                destination[1] = source[3];
                destination[2] = source[0];
                destination[3] = source[1];
                break;
            case DataFormat.DCBA:
                destination[0] = source[3];
                destination[1] = source[2];
                destination[2] = source[1];
                destination[3] = source[0];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Format));
        }
    }
}
