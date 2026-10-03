// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ReadOnlySpanEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    /// <summary>
    /// ReadOnlySpanEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class ReadOnlySpanEx
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="byteCount"></param>
        /// <param name="isLE"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToUInt。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </remarks>
        public static uint ToUInt(this ReadOnlySpan<byte> value, int startIndex, int byteCount, bool isLE = true)
        {
            if (byteCount < 1 || byteCount > 4)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            if (startIndex < 0 || startIndex + byteCount > value.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            uint result = 0;

            if (isLE)
            {
                for (int i = 0; i < byteCount; i++)
                    result |= (uint)value[startIndex + i] << (8 * i);
            }
            else
            {
                for (int i = 0; i < byteCount; i++)
                    result = (result << 8) | value[startIndex + i];
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="isLE"></param>
        /// <returns></returns>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToUInt。
        /// 正文中的关键协作调用包括 ToUInt；具体条件和先后次序见方法体。
        /// </remarks>
        public static uint ToUInt(this ReadOnlySpan<byte> value, bool isLE = true)
        {
            return value.ToUInt(0, value.Length, isLE);

        }

    }
}


