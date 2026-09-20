using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    public static class StringEx
    {

        /// <summary>
        /// 填充长度
        /// </summary>
        /// <param name="data">原始字符串</param>
        /// <param name="length">期望的字符串长度，默认为0</param>
        /// <returns>调整长度后的字符串</returns>
        public static string PaddingLength(this string data, int length = 0)
        {
            if (length == 0) length = data.Length; // 若length为0，使用实际长度
            if (data.Length >= length)
            {
                // 截断末尾，保留最后length位（右对齐）
                string value1 = data.Substring(data.Length - length, length);
                return value1;
            }
            else
            {
                // 补前导零，使总长度为length
                string value1 = data.PadLeft(length, '0');
                return value1;
            }
        }

    }
}


