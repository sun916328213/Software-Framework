using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Extension.Core.ConvertEx
{
    public enum EEndianType
    {
        Big_Endian,
        Little_Endian
    }
    public static class ByteEx
    {
        #region byte转其他类型
        /// <summary>
        /// 将字节数组转换为short类型
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static short ToShort(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            if (endianType == EEndianType.Big_Endian)
            {
                return BinaryPrimitives.ReadInt16BigEndian(data);
            }
            return BinaryPrimitives.ReadInt16LittleEndian(data);
        }

        /// <summary>
        /// 将字节数组转换为ushort类型
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static ushort ToUShort(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            if (endianType == EEndianType.Big_Endian)
            {
                return BinaryPrimitives.ReadUInt16BigEndian(data);
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(data);
        }

        /// <summary>
        /// 将字节数组转换为int类型
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static int ToInt(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            if (endianType == EEndianType.Big_Endian)
            {
                return BinaryPrimitives.ReadInt32BigEndian(data);
            }
            return BinaryPrimitives.ReadInt32LittleEndian(data);
        }

        /// <summary>
        /// 将字节数组转换为int类型
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static uint ToUInt(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            if (endianType == EEndianType.Big_Endian)
            {
                return BinaryPrimitives.ReadUInt32BigEndian(data);
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(data);
        }

        /// <summary>
        /// 将字节数组转换为float类型
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static float ToFloat(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            uint raw;
            if (endianType == EEndianType.Big_Endian)
                raw = BinaryPrimitives.ReadUInt32BigEndian(data);
            else
                raw = BinaryPrimitives.ReadUInt32LittleEndian(data);

            // 直接将uint的内存布局解读为float（零分配、零拷贝、无栈操作）
            //return Unsafe.As<uint, float>(ref raw);

            byte[] bytes = BitConverter.GetBytes(raw);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// 将字节数组转换为double类型
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static double ToDouble(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            ulong raw;
            if (endianType == EEndianType.Big_Endian)
                raw = BinaryPrimitives.ReadUInt64BigEndian(data);
            else
                raw = BinaryPrimitives.ReadUInt64LittleEndian(data);
 
            //return Unsafe.As<ulong, double>(ref raw);
            return BitConverter.Int64BitsToDouble((long)raw);
        }

        /// <summary>
        /// 将字节数组转换为ASCII字符串
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static string ToASCII(this byte[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            if (endianType == EEndianType.Big_Endian)
            {
                data.AsSpan().Reverse();
            }
            return Encoding.UTF8.GetString(data);
        }

        public static ushort ToUInt16(this byte[] value, int startIndex, bool isLE = true)
        {
            // 1. 边界检查
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (startIndex < 0 || startIndex + 2 > value.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "数组长度不足或索引越界");

            // 2. 转换逻辑
            if (isLE)
            {
                // 小端 Little Endian: 低字节在前 (Low Byte First)
                return (ushort)(value[startIndex] | (value[startIndex + 1] << 8));
            }
            else
            {
                // 大端 Big Endian: 高字节在前 (High Byte First)
                return (ushort)((value[startIndex] << 8) | value[startIndex + 1]);
            }
        }

        /// <summary>
        /// 将Byte数组转为Bool数组
        /// 逻辑：Hex显示的 1为True，0为False
        /// 例如：0x10 -> [true, false], 0x01 -> [false, true]
        /// </summary>
        public static bool[] ToHexBools(this byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<bool>();

            int count = data.Length;
            // 1. 预分配内存，避免动态扩容
            bool[] result = new bool[count * 2];

            unsafe
            {
                // 2. 防止GC移动内存，获取原始指针
                fixed (byte* pSource = data)
                fixed (bool* pDest = result)
                {
                    byte* ptrSrc = pSource;
                    bool* ptrDest = pDest;

                    // 3. 指针遍历
                    for (int i = 0; i < count; i++)
                    {
                        // 逻辑：检查高4位是否为1 (对应Hex左边那位)
                        // 0x10 是二进制 0001 0000
                        *ptrDest = (*ptrSrc & 0x10) != 0;

                        // 逻辑：检查低4位是否为1 (对应Hex右边那位)
                        // 0x01 是二进制 0000 0001
                        *(ptrDest + 1) = (*ptrSrc & 0x01) != 0;

                        // 指针移动
                        ptrSrc++;      // 源指针移动 1 byte
                        ptrDest += 2;  // 目标指针移动 2 bools (2 bytes)
                    }
                }
            }

            return result;
        }
        #endregion

        #region 其他类型转byte

        /// <summary>
        /// 将short类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static byte[] ToBytes(this short data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[2];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteInt16BigEndian(bufferSpan, data);
            else
                BinaryPrimitives.WriteInt16LittleEndian(bufferSpan, data);
            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将ushort类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static byte[] ToBytes(this ushort data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[2];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteUInt16BigEndian(bufferSpan, data);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(bufferSpan, data);
            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将int类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
         public static byte[] ToBytes(this int data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[4];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteInt32BigEndian(bufferSpan, data);
            else
                BinaryPrimitives.WriteInt32LittleEndian(bufferSpan, data);
            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将uint类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
          public static byte[] ToBytes(this uint data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[4];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteUInt32BigEndian(bufferSpan, data);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(bufferSpan, data);
            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将long类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
          public static byte[] ToBytes(this long data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[8];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteInt64BigEndian(bufferSpan, data);
            else
                BinaryPrimitives.WriteInt64LittleEndian(bufferSpan, data);
            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将ulong类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static byte[] ToBytes(this ulong data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[8];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteUInt64BigEndian(bufferSpan, data);
            else
                BinaryPrimitives.WriteUInt64LittleEndian(bufferSpan, data);
            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将float类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static byte[] ToBytes(this float data, EEndianType endianType = EEndianType.Big_Endian)
        {
            Span<byte> bufferSpan = stackalloc byte[4];

            byte[] temp = BitConverter.GetBytes(data);
            uint raw = BitConverter.ToUInt32(temp, 0);

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteInt64BigEndian(bufferSpan, raw);
            else
                BinaryPrimitives.WriteInt64LittleEndian(bufferSpan, raw);

            return bufferSpan.ToArray();
        }

        /// <summary>
        /// 将double类型转换为字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="endianType">大小端</param>
        /// <returns></returns>
        public static byte[] ToBytes(this double data, EEndianType endianType = EEndianType.Big_Endian)
        {
            long raw = BitConverter.DoubleToInt64Bits(data);

            Span<byte> bufferSpan = stackalloc  byte[8];

            if (endianType == EEndianType.Big_Endian)
                BinaryPrimitives.WriteInt64BigEndian(bufferSpan, raw);
            else
                BinaryPrimitives.WriteInt64LittleEndian(bufferSpan, raw);

            return bufferSpan.ToArray();
        }

        public static byte[] ToBytes(this string data, Encoding encoding , EEndianType endianType = EEndianType.Big_Endian)
        {
            // 2. 编码转字节数组（核心逻辑）
            byte[] buffer = encoding.GetBytes(data);

            // 3. 2字节对齐：简化计算（Modbus 16位寄存器必须偶数长度）
            int totalLength = buffer.Length + (buffer.Length % 2); // 等价于原逻辑，更简洁
            Span<byte> resultSpan = totalLength <= 1024
                ? stackalloc byte[totalLength] // 小长度：栈分配（零GC）
                : new byte[totalLength];       // 大长度：堆分配

            // 4. 拷贝原数据到Span（自动补0，无需手动赋值）
            buffer.AsSpan().CopyTo(resultSpan);

            // 5. 用BinaryPrimitives处理字节序（替代手动交换字节）
            if (endianType == EEndianType.Little_Endian)
            {
                // 按Modbus寄存器粒度（2字节）遍历，处理字节序
                for (int i = 0; i < resultSpan.Length; i += 2)
                {
                    // 仅处理完整的16位寄存器（避免越界）
                    if (i + 1 >= resultSpan.Length) break;

                    // 步骤1：按Modbus默认大端读取16位值（先读为UInt16）
                    ushort registerValue = BinaryPrimitives.ReadUInt16BigEndian(resultSpan.Slice(i, 2));
                    // 步骤2：按目标小端写回（覆盖原位置）
                    BinaryPrimitives.WriteUInt16LittleEndian(resultSpan.Slice(i, 2), registerValue);
                }
            }
            // 大端无需处理（BinaryPrimitives.ReadUInt16BigEndian是Modbus默认）

            // 6. Span转数组返回（栈Span会自动拷贝，堆Span直接转）
            return resultSpan.ToArray();
        }
        #endregion

        #region Byte的Bit操作

        #endregion
    }
}
