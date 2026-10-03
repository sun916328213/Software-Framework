// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusRtuCodec。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.Buffers.Binary、Communication.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.Buffers.Binary;
using Communication.Core.Abstractions;

namespace Communication.Runtime.Protocols.Modbus;

/// <summary>
/// 编解码 Modbus RTU 地址、PDU 与 CRC。
/// RTU 单请求在途，不使用 <see cref="IRequestCodec{TRequest,TResponse}"/> 的关联号。
/// </summary>
public sealed class ModbusRtuCodec : IRequestCodec<ModbusRtuRequest, ModbusRtuResponse>
{
    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 ThrowIfNull、WriteUInt16BigEndian、AsSpan、CopyTo、WriteCrc；具体条件和先后次序见方法体。
    /// </remarks>
    public ReadOnlyMemory<byte> Encode(ModbusRtuRequest request, ushort correlationId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var isWrite = request.FunctionCode is 15 or 16;
        var frameLength = isWrite ? 9 + request.Payload.Length : 8;
        var frame = new byte[frameLength];
        frame[0] = request.UnitId;
        frame[1] = request.FunctionCode;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), request.StartAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), request.Quantity);
        if (isWrite)
        {
            frame[6] = checked((byte)request.Payload.Length);
            request.Payload.Span.CopyTo(frame.AsSpan(7));
        }
        WriteCrc(frame.AsSpan(0, frameLength - 2), frame.AsSpan(frameLength - 2));
        return frame;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryDecode。
    /// 正文中的关键协作调用包括 Min、CopyTo、Slice、ToArray、ValidateCrc；具体条件和先后次序见方法体。
    /// </remarks>
    public bool TryDecode(
        ReadOnlySequence<byte> buffer,
        ushort correlationId,
        out ModbusRtuResponse response,
        out SequencePosition consumed)
    {
        response = null!;
        consumed = buffer.Start;
        if (buffer.Length < 2)
        {
            return false;
        }

        Span<byte> prefix = stackalloc byte[3];
        var prefixLength = (int)Math.Min(buffer.Length, prefix.Length);
        buffer.Slice(0, prefixLength).CopyTo(prefix);
        var functionCode = prefix[1];
        int frameLength;
        if ((functionCode & 0x80) != 0)
        {
            frameLength = 5;
        }
        else if (functionCode is >= 1 and <= 4)
        {
            if (buffer.Length < 3)
            {
                return false;
            }
            frameLength = 5 + prefix[2];
        }
        else if (functionCode is 15 or 16)
        {
            frameLength = 8;
        }
        else
        {
            throw new ModbusProtocolException($"不支持响应功能码 {functionCode}。");
        }

        if (frameLength > 256)
        {
            throw new ModbusProtocolException($"RTU 帧长度 {frameLength} 超出协议上限。");
        }
        if (buffer.Length < frameLength)
        {
            return false;
        }

        var frame = buffer.Slice(0, frameLength).ToArray();
        ValidateCrc(frame);
        if ((functionCode & 0x80) != 0)
        {
            throw new ModbusProtocolException(
                $"设备返回 Modbus 异常码 0x{frame[2]:X2}。")
            {
                ExceptionCode = frame[2],
            };
        }

        var data = functionCode is >= 1 and <= 4
            ? frame.AsMemory(3, frame[2])
            : frame.AsMemory(2, 4);
        consumed = buffer.GetPosition(frameLength);
        response = new ModbusRtuResponse(frame[0], functionCode, data);
        return true;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateCrc。
    /// 正文中的关键协作调用包括 ComputeCrc；具体条件和先后次序见方法体。
    /// </summary>
    private static void ValidateCrc(ReadOnlySpan<byte> frame)
    {
        var actual = (ushort)(frame[^2] | (frame[^1] << 8));
        var expected = ComputeCrc(frame[..^2]);
        if (actual != expected)
        {
            throw new ModbusProtocolException(
                $"RTU CRC 不匹配，期望 0x{expected:X4}，实际 0x{actual:X4}。");
        }
    }

    /// <summary>
    /// 执行写入操作；对应入口为 WriteCrc。
    /// 正文中的关键协作调用包括 ComputeCrc；具体条件和先后次序见方法体。
    /// </summary>
    private static void WriteCrc(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        var crc = ComputeCrc(data);
        destination[0] = (byte)crc;
        destination[1] = (byte)(crc >> 8);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ComputeCrc。
    /// </summary>
    private static ushort ComputeCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (ushort)((crc >> 1) ^ 0xA001)
                    : (ushort)(crc >> 1);
            }
        }
        return crc;
    }
}
