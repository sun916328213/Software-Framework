using System;
using System.Text;

namespace Extension.Core.ConvertEx
{
    /// <summary>
    /// 十进制扩展类
    /// </summary>
    public static class DecimalEx
    {
        /// <summary>
        /// 十进制转换为二进制字符串
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <param name="length">期望的结果字符串长度，默认为0</param>
        /// <returns>二进制字符串</returns>
        public static string ToBinary(this int data, int length = 0)
        {
            return System.Convert.ToString(data, 2).PaddingLength(length);
        }

        /// <summary>
        /// 十进制转换为二进制字符串
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <param name="length">期望的结果字符串长度，默认为0</param>
        /// <returns>二进制字符串</returns>
        public static string ToBinary(this short data, int length = 0)
        {
            return System.Convert.ToString(data, 2).PaddingLength(length);
        }

        /// <summary>
        /// 十进制转换为十六进制字符串
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <param name="length">期望的结果字符串长度，默认为0</param>
        /// <returns>十六进制字符串</returns>
        public static string ToHex(this int data, int length = 0)
        {
            return System.Convert.ToString(data, 16).PaddingLength(length);
        }

        /// <summary>
        /// 十进制转换为ASCII字符串
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <returns>ASCII字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">当data不在有效范围内时抛出</exception>
        public static string ToASC(this int data)
        {
            // 十进制转十六进制字符串
            string hexData = data.ToString("X");

            // 确保长度为偶数
            if (hexData.Length % 2 != 0)
                hexData = "0" + hexData;

            // 按每两位分割并转换为字节
            byte[] bytes = new byte[hexData.Length / 2];
            for (int i = 0; i < hexData.Length; i += 2)
            {
                bytes[i / 2] = System.Convert.ToByte(hexData.Substring(i, 2), 16);
            }

            // 使用指定编码（默认UTF8）
            return Encoding.UTF8.GetString(bytes).Replace("\0", string.Empty); ;
        }

        /// <summary>
        /// 十进制转换为ASCII字符串
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <returns>ASCII字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">当data不在有效范围内时抛出</exception>
        public static string ToASC(this short[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            byte[] bytes = new byte[data.Length * 2];
            // 根据指定的字节序处理每个ushort
            for (int i = 0; i < data.Length; i++)
            {
                short value = data[i];
                int byteIndex = i * 2;

                if (endianType == EEndianType.Little_Endian)
                {
                    // 小端：低字节在前，高字节在后
                    bytes[byteIndex] = (byte)(value & 0xFF);         // 低8位
                    bytes[byteIndex + 1] = (byte)((value >> 8) & 0xFF); // 高8位
                }
                else
                {
                    // 大端：高字节在前，低字节在后
                    bytes[byteIndex] = (byte)((value >> 8) & 0xFF); // 高8位
                    bytes[byteIndex + 1] = (byte)(value & 0xFF);         // 低8位
                }
            }
            return Encoding.UTF8.GetString(bytes).Replace("\0", string.Empty); ;
        }

        /// <summary>
        /// 十进制转换为ASCII字符串
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <returns>ASCII字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">当data不在有效范围内时抛出</exception>
        public static string ToASC(this ushort[] data, EEndianType endianType = EEndianType.Big_Endian)
        {
            byte[] bytes = new byte[data.Length * 2];
            // 根据指定的字节序处理每个ushort
            for (int i = 0; i < data.Length; i++)
            {
                ushort value = data[i];
                int byteIndex = i * 2;

                if (endianType == EEndianType.Little_Endian)
                {
                    // 小端：低字节在前，高字节在后
                    bytes[byteIndex] = (byte)(value & 0xFF);         // 低8位
                    bytes[byteIndex + 1] = (byte)((value >> 8) & 0xFF); // 高8位
                }
                else
                {
                    // 大端：高字节在前，低字节在后
                    bytes[byteIndex] = (byte)((value >> 8) & 0xFF); // 高8位
                    bytes[byteIndex + 1] = (byte)(value & 0xFF);         // 低8位
                }
            }
            return Encoding.UTF8.GetString(bytes).Replace("\0", string.Empty); ;
        }

        /// <summary>
        /// 十进制转换为ASCII字符串 小端
        /// </summary>
        /// <param name="data">要转换的十进制数</param>
        /// <returns>ASCII字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">当data不在有效范围内时抛出</exception>
        public static string ToASC(this short[] data)
        {
            byte[] bytes = new byte[data.Length * 2];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes). Replace("\0", string.Empty);
        }      
    }
}


