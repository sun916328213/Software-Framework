using System.Buffers;
using System.Buffers.Binary;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>编解码 SLMP/MC 3E 与 4E 二进制批量读写帧。</summary>
public sealed class McBinaryCodec : IRequestCodec<McRequest, McResponse>
{
    private readonly McCodecOptions _options;

    /// <summary>使用指定路由和监视定时器配置创建 codec。</summary>
    public McBinaryCodec(McCodecOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Encode(McRequest request, ushort correlationId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var headerLength = _options.FrameFormat == McFrameFormat.ThreeE ? 9 : 13;
        var requestDataLength = checked((ushort)(12 + request.Payload.Length));
        var frame = new byte[headerLength + requestDataLength];
        var routeOffset = WriteRequestSubheader(frame, correlationId);
        WriteRoute(frame.AsSpan(routeOffset));
        var lengthOffset = routeOffset + 5;
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(lengthOffset, 2),
            requestDataLength);

        var dataOffset = headerLength;
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(dataOffset, 2),
            _options.MonitoringTimer);
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(dataOffset + 2, 2),
            request.IsWrite ? McCommand.BatchWrite : McCommand.BatchRead);
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(dataOffset + 4, 2),
            request.Address.IsBit ? McCommand.BitSubcommand : McCommand.WordSubcommand);
        frame[dataOffset + 6] = (byte)request.Address.Number;
        frame[dataOffset + 7] = (byte)(request.Address.Number >> 8);
        frame[dataOffset + 8] = (byte)(request.Address.Number >> 16);
        frame[dataOffset + 9] = request.Address.DeviceCode;
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(dataOffset + 10, 2),
            request.Quantity);
        request.Payload.Span.CopyTo(frame.AsSpan(dataOffset + 12));
        return frame;
    }

    /// <inheritdoc />
    public bool TryDecode(
        ReadOnlySequence<byte> buffer,
        ushort correlationId,
        out McResponse response,
        out SequencePosition consumed)
    {
        response = null!;
        consumed = buffer.Start;
        var headerLength = _options.FrameFormat == McFrameFormat.ThreeE ? 9 : 13;
        if (buffer.Length < headerLength)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[13];
        buffer.Slice(0, headerLength).CopyTo(header);
        var routeOffset = ValidateResponseSubheader(header, correlationId);
        ValidateRoute(header[routeOffset..]);
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(
            header.Slice(routeOffset + 5, 2));
        if (dataLength is < 2 or > 8194)
        {
            throw new McProtocolException($"MC 响应数据长度 {dataLength} 超出允许范围。");
        }

        var frameLength = headerLength + dataLength;
        if (buffer.Length < frameLength)
        {
            return false;
        }
        var frame = buffer.Slice(0, frameLength).ToArray();
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(
            frame.AsSpan(headerLength, 2));
        if (endCode != 0)
        {
            throw new McProtocolException($"PLC 返回 MC 结束码 0x{endCode:X4}。")
            {
                EndCode = endCode,
            };
        }

        consumed = buffer.GetPosition(frameLength);
        response = new McResponse(frame.AsMemory(headerLength + 2, dataLength - 2));
        return true;
    }

    private int WriteRequestSubheader(Span<byte> frame, ushort correlationId)
    {
        if (_options.FrameFormat == McFrameFormat.ThreeE)
        {
            frame[0] = 0x50;
            frame[1] = 0x00;
            return 2;
        }

        frame[0] = 0x54;
        frame[1] = 0x00;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.Slice(2, 2), correlationId);
        frame[4] = 0;
        frame[5] = 0;
        return 6;
    }

    private int ValidateResponseSubheader(ReadOnlySpan<byte> header, ushort correlationId)
    {
        if (_options.FrameFormat == McFrameFormat.ThreeE)
        {
            if (header[0] != 0xD0 || header[1] != 0x00)
            {
                throw new McProtocolException("响应不是 MC 3E 二进制响应帧。");
            }
            return 2;
        }

        if (header[0] != 0xD4 || header[1] != 0x00)
        {
            throw new McProtocolException("响应不是 MC 4E 二进制响应帧。");
        }
        var actualCorrelationId = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(2, 2));
        if (actualCorrelationId != correlationId)
        {
            throw new McProtocolException(
                $"4E 响应序列号 {actualCorrelationId} 与请求 {correlationId} 不一致。");
        }
        if (header[4] != 0 || header[5] != 0)
        {
            throw new McProtocolException("4E 响应保留字段必须为零。");
        }
        return 6;
    }

    private void WriteRoute(Span<byte> destination)
    {
        destination[0] = _options.NetworkNumber;
        destination[1] = _options.StationNumber;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(2, 2), _options.ModuleIoNumber);
        destination[4] = _options.ModuleStationNumber;
    }

    private void ValidateRoute(ReadOnlySpan<byte> route)
    {
        var moduleIo = BinaryPrimitives.ReadUInt16LittleEndian(route.Slice(2, 2));
        if (route[0] != _options.NetworkNumber ||
            route[1] != _options.StationNumber ||
            moduleIo != _options.ModuleIoNumber ||
            route[4] != _options.ModuleStationNumber)
        {
            throw new McProtocolException("MC 响应路由字段与请求配置不一致。");
        }
    }
}
