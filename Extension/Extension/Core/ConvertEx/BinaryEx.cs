using System;
using System.Collections.Generic;
using System.Text;

namespace Extension.Core.ConvertEx
{
    /// <summary>
    /// 二进制扩展类
    /// </summary>
    public static  class BinaryEx
    {
        /// <summary>  
        /// 将二进制字符串转换为十进制整数  
        /// </summary>  
        /// <param name="data">二进制字符串</param>  
        /// <returns>转换后的十进制整数</returns>  
        public static int ToDec(this string data)
        {
            int value = System.Convert.ToInt32(data, 2);
            return value;
        }

        /// <summary>
        /// 将二进制字符串转换为十六进制字符串
        /// </summary>
        /// <param name="data">二进制字符串</param>
        /// <returns>转换后的十六进制字符串</returns>
        public static string ToHex(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            // 处理每4位二进制对应1位十六进制
            int padLength = (4 - data.Length % 4) % 4;
            string paddedBinary = data.PadLeft(data.Length + padLength, '0');

            StringBuilder result = new System.Text.StringBuilder(paddedBinary.Length / 4);

            for (int i = 0; i < paddedBinary.Length; i += 4)
            {
                string chunk = paddedBinary.Substring(i, 4);
                result.Append(System.Convert.ToByte(chunk, 2).ToString("X"));
            }

            return result.ToString();
        }

        /// <summary>
        /// 将二进制字符串转换为ASCII字符串
        /// </summary>
        /// <param name="data">二进制字符串</param>
        /// <returns>转换后的ASCII字符串</returns>
        public static string ToASC(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            // 处理长度非8的倍数的情况，自动补零（或截断，根据需求选择）
            int padLength = (8 - data.Length % 8) % 8;
            string paddedData = data.PadRight(data.Length + padLength, '0');

            var bytes = new byte[paddedData.Length / 8];
            for (int i = 0; i < paddedData.Length; i += 8)
            {
                string byteStr = paddedData.Substring(i, 8);
                bytes[i / 8] = System.Convert.ToByte(byteStr, 2);
            }

            // 使用指定编码（默认UTF8）
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
