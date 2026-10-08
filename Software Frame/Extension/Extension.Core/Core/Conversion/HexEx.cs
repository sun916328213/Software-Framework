// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：HexEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    /// <summary>
    /// HexEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
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

            return Encoding.UTF8.GetString(ToBytes(data));
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


