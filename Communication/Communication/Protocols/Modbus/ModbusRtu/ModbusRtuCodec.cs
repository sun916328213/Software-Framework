using System.Buffers;
using System.Buffers.Binary;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.Modbus;

/// <summary>
/// 编解码 Modbus RTU 地址、PDU 与 CRC。
/// RTU 单请求在途，不使用 <see cref="IRequestCodec{TRequest,TResponse}"/> 的关联号。
/// </summary>
public sealed class ModbusRtuCodec : IRequestCodec<ModbusRtuRequest, ModbusRtuResponse>
{
    /// <inheritdoc />
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

    private static void WriteCrc(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        var crc = ComputeCrc(data);
        destination[0] = (byte)crc;
        destination[1] = (byte)(crc >> 8);
    }

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
