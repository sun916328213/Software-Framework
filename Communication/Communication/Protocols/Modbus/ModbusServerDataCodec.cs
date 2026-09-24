namespace CommunicationNew.Protocols.Modbus;

/// <summary>在 FluentModbus 从站原始数据区与设备接口之间提取或写入数据窗口。</summary>
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

    /// <summary>从 Modbus 大端原始寄存器缓冲区读取连续寄存器。</summary>
    public static byte[] ReadRegisters(
        ReadOnlySpan<byte> storage,
        int startRegister,
        int quantity)
    {
        var (byteOffset, byteLength) = GetRegisterRange(
            storage.Length, startRegister, quantity);
        return storage.Slice(byteOffset, byteLength).ToArray();
    }

    /// <summary>
    /// 把大端字节写入原始寄存器缓冲区；奇数长度时将末字节作为高字节并以零补低字节。
    /// </summary>
    public static void WriteRegisters(
        Span<byte> storage,
        int startRegister,
        ReadOnlySpan<byte> data)
    {
        var quantity = (data.Length + 1) / 2;
        var (byteOffset, byteLength) = GetRegisterRange(
            storage.Length, startRegister, quantity);
        var target = storage.Slice(byteOffset, byteLength);
        data.CopyTo(target);
        if ((data.Length & 1) != 0)
        {
            target[^1] = 0;
        }
    }

    private static (int ByteOffset, int ByteLength) GetRegisterRange(
        int storageByteLength,
        int startRegister,
        int quantity)
    {
        if (startRegister < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startRegister));
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "寄存器数量必须大于零。");
        }

        var byteOffset = checked(startRegister * sizeof(ushort));
        var byteLength = checked(quantity * sizeof(ushort));
        if ((long)byteOffset + byteLength > storageByteLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "请求的寄存器范围超出从站数据区。");
        }
        return (byteOffset, byteLength);
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
