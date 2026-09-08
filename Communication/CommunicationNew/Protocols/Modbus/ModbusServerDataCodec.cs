using System.Buffers.Binary;

namespace CommunicationNew.Protocols.Modbus;

/// <summary>在 FluentModbus 从站寄存器存储与旧设备接口的大端字节之间转换。</summary>
internal static class ModbusServerDataCodec
{
    /// <summary>从从站位存储中提取任意起始位置的压缩位窗口。</summary>
    public static byte[] ReadBits(ReadOnlySpan<byte> storage, int startBit, int quantity)
    {
        ValidateBitRange(storage.Length, startBit, quantity);
        var result = new byte[(quantity + 7) / 8];
        for (var index = 0; index < quantity; index++)
        {
            var sourceBit = startBit + index;
            if ((storage[sourceBit / 8] & (1 << (sourceBit % 8))) != 0)
            {
                result[index / 8] |= (byte)(1 << (index % 8));
            }
        }
        return result;
    }

    /// <summary>把压缩位字节写入任意起始位置，同时保留请求范围之外的相邻位。</summary>
    public static void WriteBits(
        Span<byte> storage,
        int startBit,
        ReadOnlySpan<byte> packedBits)
    {
        var quantity = checked(packedBits.Length * 8);
        ValidateBitRange(storage.Length, startBit, quantity);
        for (var index = 0; index < quantity; index++)
        {
            var targetBit = startBit + index;
            var mask = (byte)(1 << (targetBit % 8));
            if ((packedBits[index / 8] & (1 << (index % 8))) != 0)
            {
                storage[targetBit / 8] |= mask;
            }
            else
            {
                storage[targetBit / 8] &= (byte)~mask;
            }
        }
    }

    /// <summary>把有符号寄存器的原始 16 位模式编码为 Modbus 大端字节。</summary>
    public static byte[] EncodeRegisters(ReadOnlySpan<short> registers)
    {
        var bytes = new byte[registers.Length * 2];
        for (var index = 0; index < registers.Length; index++)
        {
            BinaryPrimitives.WriteInt16BigEndian(
                bytes.AsSpan(index * 2, sizeof(short)),
                registers[index]);
        }
        return bytes;
    }

    /// <summary>
    /// 把旧接口的大端字节解码为寄存器；奇数长度沿用旧行为，将末字节作为高字节并以零补低字节。
    /// </summary>
    public static ushort[] DecodeRegisters(ReadOnlySpan<byte> bytes)
    {
        var registers = new ushort[(bytes.Length + 1) / 2];
        var completeRegisterCount = bytes.Length / 2;
        for (var index = 0; index < completeRegisterCount; index++)
        {
            registers[index] = BinaryPrimitives.ReadUInt16BigEndian(
                bytes.Slice(index * 2, sizeof(ushort)));
        }
        if ((bytes.Length & 1) != 0)
        {
            registers[^1] = (ushort)(bytes[^1] << 8);
        }
        return registers;
    }

    private static void ValidateBitRange(int storageByteLength, int startBit, int quantity)
    {
        if (startBit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startBit));
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "位数量必须大于零。");
        }
        if ((long)startBit + quantity > (long)storageByteLength * 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "请求的位范围超出从站数据区。");
        }
    }
}
