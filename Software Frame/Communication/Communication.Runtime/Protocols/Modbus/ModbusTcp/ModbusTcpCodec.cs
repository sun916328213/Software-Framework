// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusTcpCodec。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.Buffers.Binary、Communication.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.Buffers.Binary;
using Communication.Core.Abstractions;

namespace Communication.Runtime.Protocols.Modbus;

/// <summary>
/// 编解码 Modbus TCP 的 MBAP 头和 PDU。
/// codec 不持有连接状态；同一实例可以供多个 session 使用。
/// </summary>
public sealed class ModbusTcpCodec : IRequestCodec<ModbusTcpRequest, ModbusTcpResponse>
{
    private const int MbapPrefixLength = 6;
    private const int MbapHeaderLength = 7;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Encode。
    /// 正文中的关键协作调用包括 ThrowIfNull、WriteUInt16BigEndian、AsSpan、CopyTo；具体条件和先后次序见方法体。
    /// </remarks>
    public ReadOnlyMemory<byte> Encode(ModbusTcpRequest request, ushort correlationId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isWrite = request.FunctionCode is 15 or 16;
        var pduLength = isWrite ? 6 + request.Payload.Length : 5;
        var frame = new byte[MbapHeaderLength + pduLength];

        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0, 2), correlationId);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), (ushort)(1 + pduLength));
        frame[6] = request.UnitId;
        frame[7] = request.FunctionCode;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8, 2), request.StartAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(10, 2), request.Quantity);

        if (isWrite)
        {
            frame[12] = checked((byte)request.Payload.Length);
            request.Payload.Span.CopyTo(frame.AsSpan(13));
        }

        return frame;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryDecode。
    /// 正文中的关键协作调用包括 CopyTo、Slice、ReadUInt16BigEndian、ToArray、AsMemory；具体条件和先后次序见方法体。
    /// </remarks>
    public bool TryDecode(
        ReadOnlySequence<byte> buffer,
        ushort correlationId,
        out ModbusTcpResponse response,
        out SequencePosition consumed)
    {
        response = null!;
        consumed = buffer.Start;
        if (buffer.Length < MbapHeaderLength)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[MbapHeaderLength];
        buffer.Slice(0, MbapHeaderLength).CopyTo(header);

        var actualCorrelationId = BinaryPrimitives.ReadUInt16BigEndian(header[..2]);
        if (actualCorrelationId != correlationId)
        {
            throw new ModbusProtocolException(
                $"响应关联号 {actualCorrelationId} 与请求关联号 {correlationId} 不一致。");
        }

        var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(2, 2));
        if (protocolId != 0)
        {
            throw new ModbusProtocolException($"MBAP 协议标识必须为 0，实际为 {protocolId}。");
        }

        var remainingLength = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(4, 2));
        if (remainingLength is < 2 or > 254)
        {
            throw new ModbusProtocolException($"MBAP 长度 {remainingLength} 超出允许范围。");
        }

        var frameLength = MbapPrefixLength + remainingLength;
        if (buffer.Length < frameLength)
        {
            return false;
        }

        var frame = buffer.Slice(0, frameLength).ToArray();
        var unitId = frame[6];
        var functionCode = frame[7];
        if ((functionCode & 0x80) != 0)
        {
            var exceptionCode = frameLength > 8 ? frame[8] : (byte)0;
            throw new ModbusProtocolException(
                $"设备返回 Modbus 异常码 0x{exceptionCode:X2}。")
            {
                ExceptionCode = exceptionCode,
            };
        }

        ReadOnlyMemory<byte> data;
        if (functionCode is >= 1 and <= 4)
        {
            if (frameLength < 9)
            {
                throw new ModbusProtocolException("读响应缺少字节数量字段。");
            }

            var byteCount = frame[8];
            if (frameLength != 9 + byteCount)
            {
                throw new ModbusProtocolException(
                    $"读响应声明 {byteCount} 字节，实际帧长度不匹配。");
            }

            data = frame.AsMemory(9, byteCount);
        }
        else if (functionCode is 15 or 16)
        {
            if (frameLength != 12)
            {
                throw new ModbusProtocolException("写响应必须回显起始地址和数量。");
            }

            data = frame.AsMemory(8, 4);
        }
        else
        {
            throw new ModbusProtocolException($"不支持响应功能码 {functionCode}。");
        }

        consumed = buffer.GetPosition(frameLength);
        response = new ModbusTcpResponse(unitId, functionCode, data);
        return true;
    }
}
