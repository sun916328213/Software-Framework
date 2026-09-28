using System.Buffers.Binary;
using System.Text;
using CommunicationNew.Abstractions;
using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Converters;

/// <summary>布尔转换器，非零字节表示 true。</summary>
public sealed class BoolConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.Bool;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length > offset && data[offset] != 0;
    public byte[] Encode(object? value) => value is true ? new byte[] { 1 } : new byte[] { 0 };
    public int GetByteCount() => 1;
}

/// <summary>单字节转换器。</summary>
public sealed class ByteConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.Byte;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length > offset ? data[offset] : (byte)0;
    public byte[] Encode(object? value) =>
        new[] { value is byte byteValue ? byteValue : Convert.ToByte(value ?? 0) };
    public int GetByteCount() => 1;
}

/// <summary>按照设备数据格式转换 Int16。</summary>
public sealed class Int16Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    public Int16Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.Int16;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 2 ? _codec.DecodeInt16(data.AsSpan(offset, 2)) : (short)0;
    public byte[] Encode(object? value) =>
        _codec.EncodeInt16(value is short typed ? typed : Convert.ToInt16(value ?? 0));
    public int GetByteCount() => 2;
}

/// <summary>按照设备数据格式转换 UInt16。</summary>
public sealed class UInt16Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    public UInt16Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.UInt16;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 2 ? _codec.DecodeUInt16(data.AsSpan(offset, 2)) : (ushort)0;
    public byte[] Encode(object? value) =>
        _codec.EncodeUInt16(value is ushort typed ? typed : Convert.ToUInt16(value ?? 0));
    public int GetByteCount() => 2;
}

/// <summary>按照设备数据格式转换 Int32。</summary>
public sealed class Int32Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    public Int32Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.Int32;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 4 ? _codec.DecodeInt32(data.AsSpan(offset, 4)) : 0;
    public byte[] Encode(object? value) =>
        _codec.EncodeInt32(value is int typed ? typed : Convert.ToInt32(value ?? 0));
    public int GetByteCount() => 4;
}

/// <summary>按照设备数据格式转换 UInt32。</summary>
public sealed class UInt32Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    public UInt32Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.UInt32;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 4 ? _codec.DecodeUInt32(data.AsSpan(offset, 4)) : 0u;
    public byte[] Encode(object? value) =>
        _codec.EncodeUInt32(value is uint typed ? typed : Convert.ToUInt32(value ?? 0));
    public int GetByteCount() => 4;
}

/// <summary>
/// 按照设备数据格式转换 Int64。
/// 64 位值按两个连续 32 位块应用所选格式，避免把未定义的 64 位字序隐式加入四格式枚举。
/// </summary>
public sealed class Int64Converter : IValueConverter
{
    private readonly DataFormat _format;
    public Int64Converter(DataFormat format = DataFormat.ABCD) => _format = format;
    public DataPointType TargetType => DataPointType.Int64;

    public object Decode(byte[] data, int offset = 0)
    {
        if (data.Length < offset + 8) return 0L;
        Span<byte> canonical = stackalloc byte[8];
        Format64.CopyToCanonical(data.AsSpan(offset, 8), canonical, _format);
        return BinaryPrimitives.ReadInt64BigEndian(canonical);
    }

    public byte[] Encode(object? value)
    {
        Span<byte> canonical = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(
            canonical,
            value is long typed ? typed : Convert.ToInt64(value ?? 0));
        return Format64.CopyFromCanonical(canonical, _format);
    }

    public int GetByteCount() => 8;
}

/// <summary>按照设备数据格式转换单精度浮点数。</summary>
public sealed class FloatConverter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    public FloatConverter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.Float;
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 4 ? _codec.DecodeSingle(data.AsSpan(offset, 4)) : 0f;
    public byte[] Encode(object? value) =>
        _codec.EncodeSingle(value is float typed ? typed : Convert.ToSingle(value ?? 0));
    public int GetByteCount() => 4;
}

/// <summary>
/// 按照设备数据格式转换双精度浮点数。
/// 64 位值沿用 <see cref="Int64Converter"/> 的两个 32 位块规则。
/// </summary>
public sealed class DoubleConverter : IValueConverter
{
    private readonly DataFormat _format;
    public DoubleConverter(DataFormat format = DataFormat.ABCD) => _format = format;
    public DataPointType TargetType => DataPointType.Double;

    public object Decode(byte[] data, int offset = 0)
    {
        if (data.Length < offset + 8) return 0d;
        Span<byte> canonical = stackalloc byte[8];
        Format64.CopyToCanonical(data.AsSpan(offset, 8), canonical, _format);
        return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(canonical));
    }

    public byte[] Encode(object? value)
    {
        var typed = value is double doubleValue ? doubleValue : Convert.ToDouble(value ?? 0);
        Span<byte> canonical = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(canonical, BitConverter.DoubleToInt64Bits(typed));
        return Format64.CopyFromCanonical(canonical, _format);
    }

    public int GetByteCount() => 8;
}

/// <summary>UTF-8 字符串转换器，解码时在首个零字符处截断。</summary>
public sealed class StringConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.String;

    public object Decode(byte[] data, int offset = 0)
    {
        var text = Encoding.UTF8.GetString(data, offset, data.Length - offset);
        var terminator = text.IndexOf('\0');
        return terminator >= 0 ? text[..terminator] : text;
    }

    public byte[] Encode(object? value) => Encoding.UTF8.GetBytes(value?.ToString() ?? "");
    public int GetByteCount() => -1;
}

/// <summary>原始字节数组直通转换器。</summary>
public sealed class ByteArrayConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.ByteArray;
    public object Decode(byte[] data, int offset = 0) => data[offset..];
    public byte[] Encode(object? value) => value as byte[] ?? Array.Empty<byte>();
    public int GetByteCount() => -1;
}

internal static class Format64
{
    public static void CopyToCanonical(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        DataFormat format)
    {
        TransformBlock(source[..4], destination[..4], format);
        TransformBlock(source.Slice(4, 4), destination.Slice(4, 4), format);
    }

    public static byte[] CopyFromCanonical(ReadOnlySpan<byte> source, DataFormat format)
    {
        var destination = new byte[8];
        TransformBlock(source[..4], destination.AsSpan(0, 4), format);
        TransformBlock(source.Slice(4, 4), destination.AsSpan(4, 4), format);
        return destination;
    }

    private static void TransformBlock(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        DataFormat format)
    {
        switch (format)
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
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }
}
