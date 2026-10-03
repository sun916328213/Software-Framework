// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：BoolConverter、ByteConverter、Int16Converter、UInt16Converter、Int32Converter、UInt32Converter、Int64Converter、FloatConverter、DoubleConverter、StringConverter、ByteArrayConverter、Format64。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、System.Buffers.Binary、System.Text、Communication.Core.Abstractions、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using System.Buffers.Binary;
using System.Text;
using Communication.Core.Abstractions;
using Communication.Core.Interfaces;

namespace Communication.Core.Converters;

/// <summary>布尔转换器，非零字节表示 true。</summary>
public sealed class BoolConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.Bool;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length > offset && data[offset] != 0;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// </summary>
    public byte[] Encode(object? value) => value is true ? new byte[] { 1 } : new byte[] { 0 };
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 1;
}

/// <summary>单字节转换器。</summary>
public sealed class ByteConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.Byte;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length > offset ? data[offset] : (byte)0;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 ToByte；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) =>
        new[] { value is byte byteValue ? byteValue : Convert.ToByte(value ?? 0) };
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 1;
}

/// <summary>按照设备数据格式转换 Int16。</summary>
public sealed class Int16Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    /// <summary>
    /// 初始化 Int16Converter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public Int16Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.Int16;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 DecodeInt16、AsSpan；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 2 ? _codec.DecodeInt16(data.AsSpan(offset, 2)) : (short)0;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 EncodeInt16、ToInt16；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) =>
        _codec.EncodeInt16(value is short typed ? typed : Convert.ToInt16(value ?? 0));
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 2;
}

/// <summary>按照设备数据格式转换 UInt16。</summary>
public sealed class UInt16Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    /// <summary>
    /// 初始化 UInt16Converter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public UInt16Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.UInt16;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 DecodeUInt16、AsSpan；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 2 ? _codec.DecodeUInt16(data.AsSpan(offset, 2)) : (ushort)0;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 EncodeUInt16、ToUInt16；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) =>
        _codec.EncodeUInt16(value is ushort typed ? typed : Convert.ToUInt16(value ?? 0));
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 2;
}

/// <summary>按照设备数据格式转换 Int32。</summary>
public sealed class Int32Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    /// <summary>
    /// 初始化 Int32Converter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public Int32Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.Int32;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 DecodeInt32、AsSpan；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 4 ? _codec.DecodeInt32(data.AsSpan(offset, 4)) : 0;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 EncodeInt32、ToInt32；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) =>
        _codec.EncodeInt32(value is int typed ? typed : Convert.ToInt32(value ?? 0));
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 4;
}

/// <summary>按照设备数据格式转换 UInt32。</summary>
public sealed class UInt32Converter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    /// <summary>
    /// 初始化 UInt32Converter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public UInt32Converter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.UInt32;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 DecodeUInt32、AsSpan；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 4 ? _codec.DecodeUInt32(data.AsSpan(offset, 4)) : 0u;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 EncodeUInt32、ToUInt32；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) =>
        _codec.EncodeUInt32(value is uint typed ? typed : Convert.ToUInt32(value ?? 0));
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 4;
}

/// <summary>
/// 按照设备数据格式转换 Int64。
/// 64 位值按两个连续 32 位块应用所选格式，避免把未定义的 64 位字序隐式加入四格式枚举。
/// </summary>
public sealed class Int64Converter : IValueConverter
{
    private readonly DataFormat _format;
    /// <summary>
    /// 初始化 Int64Converter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public Int64Converter(DataFormat format = DataFormat.ABCD) => _format = format;
    public DataPointType TargetType => DataPointType.Int64;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 CopyToCanonical、AsSpan、ReadInt64BigEndian；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0)
    {
        if (data.Length < offset + 8) return 0L;
        Span<byte> canonical = stackalloc byte[8];
        Format64.CopyToCanonical(data.AsSpan(offset, 8), canonical, _format);
        return BinaryPrimitives.ReadInt64BigEndian(canonical);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 WriteInt64BigEndian、ToInt64、CopyFromCanonical；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value)
    {
        Span<byte> canonical = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(
            canonical,
            value is long typed ? typed : Convert.ToInt64(value ?? 0));
        return Format64.CopyFromCanonical(canonical, _format);
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 8;
}

/// <summary>按照设备数据格式转换单精度浮点数。</summary>
public sealed class FloatConverter : IValueConverter
{
    private readonly DataFormatCodec _codec;
    /// <summary>
    /// 初始化 FloatConverter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public FloatConverter(DataFormat format = DataFormat.ABCD) =>
        _codec = new DataFormatCodec(format);
    public DataPointType TargetType => DataPointType.Float;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 DecodeSingle、AsSpan；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) =>
        data.Length >= offset + 4 ? _codec.DecodeSingle(data.AsSpan(offset, 4)) : 0f;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 EncodeSingle、ToSingle；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) =>
        _codec.EncodeSingle(value is float typed ? typed : Convert.ToSingle(value ?? 0));
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 4;
}

/// <summary>
/// 按照设备数据格式转换双精度浮点数。
/// 64 位值沿用 <see cref="Int64Converter"/> 的两个 32 位块规则。
/// </summary>
public sealed class DoubleConverter : IValueConverter
{
    private readonly DataFormat _format;
    /// <summary>
    /// 初始化 DoubleConverter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public DoubleConverter(DataFormat format = DataFormat.ABCD) => _format = format;
    public DataPointType TargetType => DataPointType.Double;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 CopyToCanonical、AsSpan、Int64BitsToDouble、ReadInt64BigEndian；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0)
    {
        if (data.Length < offset + 8) return 0d;
        Span<byte> canonical = stackalloc byte[8];
        Format64.CopyToCanonical(data.AsSpan(offset, 8), canonical, _format);
        return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(canonical));
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 ToDouble、WriteInt64BigEndian、DoubleToInt64Bits、CopyFromCanonical；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value)
    {
        var typed = value is double doubleValue ? doubleValue : Convert.ToDouble(value ?? 0);
        Span<byte> canonical = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(canonical, BitConverter.DoubleToInt64Bits(typed));
        return Format64.CopyFromCanonical(canonical, _format);
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => 8;
}

/// <summary>UTF-8 字符串转换器，解码时在首个零字符处截断。</summary>
public sealed class StringConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.String;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// 正文中的关键协作调用包括 GetString、IndexOf；具体条件和先后次序见方法体。
    /// </summary>
    public object Decode(byte[] data, int offset = 0)
    {
        var text = Encoding.UTF8.GetString(data, offset, data.Length - offset);
        var terminator = text.IndexOf('\0');
        return terminator >= 0 ? text[..terminator] : text;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 GetBytes；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) => Encoding.UTF8.GetBytes(value?.ToString() ?? "");
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => -1;
}

/// <summary>原始字节数组直通转换器。</summary>
public sealed class ByteArrayConverter : IValueConverter
{
    public DataPointType TargetType => DataPointType.ByteArray;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decode。
    /// </summary>
    public object Decode(byte[] data, int offset = 0) => data[offset..];
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 Empty；具体条件和先后次序见方法体。
    /// </summary>
    public byte[] Encode(object? value) => value as byte[] ?? Array.Empty<byte>();
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetByteCount。
    /// </summary>
    public int GetByteCount() => -1;
}

/// <summary>
/// Format64 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
/// </summary>
internal static class Format64
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CopyToCanonical。
    /// 正文中的关键协作调用包括 TransformBlock、Slice；具体条件和先后次序见方法体。
    /// </summary>
    public static void CopyToCanonical(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        DataFormat format)
    {
        TransformBlock(source[..4], destination[..4], format);
        TransformBlock(source.Slice(4, 4), destination.Slice(4, 4), format);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CopyFromCanonical。
    /// 正文中的关键协作调用包括 TransformBlock、AsSpan、Slice；具体条件和先后次序见方法体。
    /// </summary>
    public static byte[] CopyFromCanonical(ReadOnlySpan<byte> source, DataFormat format)
    {
        var destination = new byte[8];
        TransformBlock(source[..4], destination.AsSpan(0, 4), format);
        TransformBlock(source.Slice(4, 4), destination.AsSpan(4, 4), format);
        return destination;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TransformBlock。
    /// 正文中的关键协作调用包括 CopyTo、nameof；具体条件和先后次序见方法体。
    /// </summary>
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
