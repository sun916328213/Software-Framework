using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    public static  class HexEx
    {
        /// <summary>
        /// 将十六进制字符串转换为指定编码的字符串
        /// </summary>
        /// <param name="data">十六进制字符串</param>
        /// <returns>转换后的字符串</returns>
        public static string ToASC(this string data)
        {
            // 输入验证
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            // 如果十六进制字符串长度为奇数，前面补零
            if (data.Length % 2 != 0) data = "0" + data;

            // 初始化字节数组，用于存储转换后的字节
            byte[] buff = new byte[data.Length / 2];
            int index = 0;
            // 遍历十六进制字符串，每两个字符转换为一个字节
            for (int i = 0; i < data.Length; i += 2)
            {
                buff[index] = System.Convert.ToByte(data.Substring(i, 2), 16);
                ++index;
            }
            // 使用指定编码（默认UTF8）将字节数组转换为字符串
            return Encoding.UTF8.GetString(buff);
        }

        /// <summary>
        /// 十六进制转byte[]
        /// </summary>
        /// <param name="data">十六进制字符串</param>
        /// <returns>转换后的字节数组</returns>
        public static byte[] ToBytes(this string data)
        {
            // 如果十六进制字符串长度为奇数，前面补零
            if (data.Length % 2 != 0) data = "0" + data;

            // 初始化字节数组，用于存储转换后的字节
            byte[] buff = new byte[data.Length / 2];
            int index = 0;
            // 遍历十六进制字符串，每两个字符转换为一个字节
            for (int i = 0; i < data.Length; i += 2)
            {
                buff[index] = System.Convert.ToByte(data.Substring(i, 2), 16);
                ++index;
            }
            return buff;
        }
    }
}


