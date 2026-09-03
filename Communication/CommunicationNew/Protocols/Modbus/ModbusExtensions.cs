namespace CommunicationNew.Protocols.Modbus
{
    /// <summary>Modbus 数据转换扩展方法</summary>
    internal static class ModbusExtensions
    {
        public static byte[] ToBytes(this bool[] bools)
        {
            var bytes = new byte[(bools.Length + 7) / 8];
            for (int i = 0; i < bools.Length; i++)
                if (bools[i]) bytes[i / 8] |= (byte)(1 << (i % 8));
            return bytes;
        }

        public static byte[] ToBytes(this ushort[] registers)
        {
            var bytes = new byte[registers.Length * 2];
            for (int i = 0; i < registers.Length; i++)
            {
                bytes[i * 2] = (byte)(registers[i] >> 8);
                bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
            }
            return bytes;
        }

        public static byte[] ToBytes(this short[] registers)
        {
            var bytes = new byte[registers.Length * 2];
            for (int i = 0; i < registers.Length; i++)
            {
                var u = (ushort)registers[i];
                bytes[i * 2] = (byte)(u >> 8);
                bytes[i * 2 + 1] = (byte)(u & 0xFF);
            }
            return bytes;
        }

        public static bool[] ToBoolArray(this byte[] bytes)
        {
            var bools = new bool[bytes.Length * 8];
            for (int i = 0; i < bools.Length; i++)
                bools[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
            return bools;
        }

        public static ushort[] ToShortArray(this byte[] bytes)
        {
            var count = bytes.Length / 2 + bytes.Length % 2;
            var registers = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                int hi = i * 2 < bytes.Length ? bytes[i * 2] : 0;
                int lo = i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0;
                registers[i] = (ushort)((hi << 8) | lo);
            }
            return registers;
        }
    }
}
