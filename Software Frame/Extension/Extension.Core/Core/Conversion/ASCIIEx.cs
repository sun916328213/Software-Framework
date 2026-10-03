// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ASCIIEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    /// <summary>
    /// ASCII扩展类
    /// </summary>
    public static class ASCIIEx
    {
        /// <summary>
        /// 转十六进制
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="uppercase">数据大写/小写 默认大写</param>
        /// <returns></returns>
        public static string ToHex(this string data, bool uppercase = true)
        {
            // 将ASCII字符串转换为字节数组
            byte[] asciiBytes = Encoding.UTF8.GetBytes(data);

            // 使用StringBuilder高效拼接字符串
            StringBuilder hexBuilder = new StringBuilder(asciiBytes.Length * 2);

            // 转换每个字节为十六进制
            foreach (byte b in asciiBytes)
            {
                // 根据参数选择大小写格式
                string hexFormat = uppercase ? "X2" : "x2";
                hexBuilder.Append(b.ToString(hexFormat));
            }
            return hexBuilder.ToString();
        }

        /// <summary>
        /// 将ASCII字符串转换为所需的ushort数组 Modbus功能码10（写多个寄存器 
        /// </summary>
        /// <param name="asciiStr">输入的ASCII字符串（如"1234567"）</param>
        /// <returns>按大端模式打包的ushort数组</returns>
        public static ushort[] ToUshorts(this string asciiStr, EEndianType pEndianType = EEndianType.Big_Endian)
        {
            if (string.IsNullOrEmpty(asciiStr))
                throw new ArgumentNullException(nameof(asciiStr), "输入字符串不能为空");

            // 1. 将ASCII字符串转换为字节数组（每个字符对应1字节）
            byte[] asciiBytes = Encoding.UTF8.GetBytes(asciiStr);

            // 2. 补齐字节数为偶数（Modbus寄存器为16位，需2字节对齐）
            int padding = asciiBytes.Length % 2; // 若为奇数，补1个0字节
            byte[] paddedBytes = new byte[asciiBytes.Length + padding];
            Array.Copy(asciiBytes, paddedBytes, asciiBytes.Length);
            // 若需补位，末尾自动填充0（默认值）

            ushort[] result = new ushort[paddedBytes.Length / 2];
            // 3. 按大端模式（高字节在前）转换为ushort数组
            if (pEndianType == EEndianType.Big_Endian)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    int byteIndex = i * 2;
                    // 大端模式：高字节 << 8 + 低字节
                    result[i] = (ushort)((paddedBytes[byteIndex] << 8) | paddedBytes[byteIndex + 1]);
                }
            }
            else
            {
                // 3. 小端模式组合：低字节（前）作为 ushort 低位，高字节（后）作为 ushort 高位
                for (int i = 0; i < result.Length; i++)
                {
                    int byteStartIndex = i * 2; // 每个 ushort 对应 2 字节的起始索引
                    byte lowByte = paddedBytes[byteStartIndex]; // 小端：第 1 字节是低字节
                    byte highByte = paddedBytes[byteStartIndex + 1];// 小端：第 2 字节是高字节
                                                                    // 小端公式：低字节 | (高字节 << 8)
                    result[i] = (ushort)(lowByte | (highByte << 8));
                }
            }

            return result;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToShorts。
        /// 正文中的关键协作调用包括 IsNullOrEmpty、nameof、GetBytes、Copy；具体条件和先后次序见方法体。
        /// </summary>
        public static short[] ToShorts(this string asciiStr, EEndianType pEndianType = EEndianType.Big_Endian)
        {
            if (string.IsNullOrEmpty(asciiStr))
                throw new ArgumentNullException(nameof(asciiStr), "输入字符串不能为空");

            // 1. 将ASCII字符串转换为字节数组（每个字符对应1字节）
            byte[] asciiBytes = Encoding.UTF32.GetBytes(asciiStr);

            // 2. 补齐字节数为偶数（Modbus寄存器为16位，需2字节对齐）
            int padding = asciiBytes.Length % 2; // 若为奇数，补1个0字节
            byte[] paddedBytes = new byte[asciiBytes.Length + padding];
            Array.Copy(asciiBytes, paddedBytes, asciiBytes.Length);
            // 若需补位，末尾自动填充0（默认值）

            short[] result = new short[paddedBytes.Length / 2];
            // 3. 按大端模式（高字节在前）转换为short数组
            if (pEndianType == EEndianType.Big_Endian)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    int byteIndex = i * 2;
                    // 大端模式：高字节 << 8 + 低字节
                    result[i] = (short)((paddedBytes[byteIndex] << 8) | paddedBytes[byteIndex + 1]);
                }
            }
            else
            {
                // 3. 小端模式组合：低字节（前）作为 short 低位，高字节（后）作为short 高位
                for (int i = 0; i < result.Length; i++)
                {
                    int byteStartIndex = i * 2; // 每个 short 对应 2 字节的起始索引
                    byte lowByte = paddedBytes[byteStartIndex]; // 小端：第 1 字节是低字节
                    byte highByte = paddedBytes[byteStartIndex + 1];// 小端：第 2 字节是高字节
                                                                    // 小端公式：低字节 | (高字节 << 8)
                    result[i] = (short)(lowByte | (highByte << 8));
                }
            }

            return result;
        }
    }
}


