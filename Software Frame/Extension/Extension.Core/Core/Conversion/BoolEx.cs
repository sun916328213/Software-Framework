// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：BoolEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    /// <summary>
    /// BoolEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class BoolEx
    {
        /// <summary>
        ///  Bool转short
        /// </summary>
        /// <param name="data">布尔值</param>
        /// <returns>转换后的short值，true为1，false为0</returns>
        public static short ToShort(this bool data)
        {
            return data ? (short)1 : (short)0;
        }

        /// <summary>
        ///  Bool转int
        /// </summary>
        /// <param name="data">布尔值</param>
        /// <returns>转换后的int值，true为1，false为0</returns>
        public static int ToInt(this bool data)
        {
            return data ? 1 : 0;
        }

        /// <summary>
        ///  Bool转byte
        /// </summary>
        /// <param name="data">布尔值</param>
        /// <returns>转换后的byte值，true为1，false为0</returns>
        public static byte ToByte(this bool data)
        {
            return data ? (byte)1 : (byte)0;
        }
 
        /// <summary>
        ///  Bool[]转short
        /// </summary>
        /// <param name="data">布尔数组</param>
        /// <returns>转换后的short值</returns>
        public static short ToShortArray(this bool[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            if (data.Length > 16)
                throw new ArgumentException("布尔数组长度不能超过16位");
                
            short result = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i])
                    result |= (short)(1 << i);
            }
            return result;
        }

        /// <summary>
        ///  List<bool[]>转short[]
        /// </summary>
        /// <param name="data">布尔数组列表</param>
        /// <returns>转换后的short数组</returns>
        public static short[] ToShortArray(this List<bool[]> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            short[] result = new short[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                result[i] = data[i].ToShortArray();
            }
            return result;
        }
    }
}

