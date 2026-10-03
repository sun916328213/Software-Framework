// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：RegisterEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Reflection、System.Runtime.InteropServices、System.Text。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    /// <summary>
    /// Short 扩展类
    /// </summary>
    public static class RegisterEx
    {
        #region short 转二进制相关
    
        /// <summary>
        /// short 转二进制 格式=bool[] 
        /// </summary>
        /// <param name="data">short 数据</param>
        /// <param name="length">数组长度</param>
        /// <returns></returns>
        public static bool[] ToBoolArray(this short data, int length = 16)
        {
            bool[] result = new bool[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (data & (1 << i)) != 0;
            }
            return result;
        }

        /// <summary>
        /// Short[]转二进制  格式=List<bool[]> Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static List<bool[]> ToList_BoolArray(this short[] data, int length = 16)
        {
            List<bool[]> result = new List<bool[]>(data.Length);
            foreach (var item in data)
            {
                bool[] iData = new bool[length];
                for (int i = 0; i < length; i++)
                {
                    iData[i] = (item & (1 << i)) != 0;
                }
                result.Add(iData);
            }
            return result;
        }

        /// <summary>
        ///  Short 转二进制数组 Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToBinaryArray(this short data)
        {
            return DecimalEx.ToBinary(data, 16);
        }
        
        /// <summary>
        ///  Short[]转二进制数组 Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string[] ToBinaryArray(this short[] data)
        {
            string[] result = new string[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = DecimalEx.ToBinary(data[i], 16);
            }
            return result;
        }
        
        #endregion
        
        #region short[] 转其他类型数组
        
        /// <summary>
        ///  Short[]转 int[] Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static int[] ToIntArray(this short[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<int>();
            
            int intLength = data.Length / 2;
            if (data.Length % 2 != 0)
                throw new ArgumentException("数组长度必须是 2 的倍数", nameof(data));
            
            int[] result = new int[intLength];
                                     
            for (int i = 0; i < intLength; i++)
            {
                byte[] bytes = new byte[4];
                Buffer.BlockCopy(data, 2 * i * 2, bytes, 0, 4);
                result[i] = BitConverter.ToInt32(bytes, 0);
            }
            return result;
        }


        /// <summary>
        ///  Short[]转 string[] Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string[] ToStringArray(this short[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<string>();
        
            string[] result = new string[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = Encoding.UTF8.GetString(BitConverter.GetBytes(data[i]));
            }
            return result;
        }



        /// <summary>
        ///  Short[]转 float[] Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static float[] ToFloatArray(this short[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<float>();
            
            if (data.Length % 2 != 0)
                throw new ArgumentException("数组长度必须是 2 的倍数", nameof(data));
            
            int floatLength = data.Length / 2;
            float[] result = new float[floatLength];
                                    
            for (int i = 0; i < floatLength; i++)
            {
                byte[] bytes = new byte[4];
                Buffer.BlockCopy(data, 2 * i * 2, bytes, 0, 4);
                result[i] = BitConverter.ToSingle(bytes, 0);
            }
            return result;
        }


        /// <summary>
        ///  Short[]转 double[] Short=2byte=16bit
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static double[] ToDoubleArray(this short[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<double>();
            
            if (data.Length % 4 != 0)
                throw new ArgumentException("数组长度必须是 4 的倍数", nameof(data));
            
            int doubleLength = data.Length / 4;
            double[] result = new double[doubleLength];
                                    
            for (int i = 0; i < doubleLength; i++)
            {
                byte[] bytes = new byte[8];
                Buffer.BlockCopy(data, 4 * i * 2, bytes, 0, 8);
                result[i] = BitConverter.ToDouble(bytes, 0);
            }
            return result;
        }
                        
        #endregion

        #region 其他类型转 short[]

        /// <summary>
        /// int 转 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this int data)
        {
            return new short[]
            {
                (short)data,
                (short)(data >> 16)
            };
        }

        /// <summary>
        /// 整型数组转短整型数组
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this int[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<short>();

            short[] result = new short[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                int value = data[i];
                result[i * 2] = (short)value;
                result[i * 2 + 1] = (short)(value >> 16);
            }
            return result;
        }

        /// <summary>
        /// string 转 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return Array.Empty<short>();
        
            byte[] bytes = Encoding.UTF32.GetBytes(data);
            int shortCount = bytes.Length / 2;
            short[] result = new short[shortCount];
                    
            for (int i = 0; i < shortCount; i++)
            {
                result[i] = BitConverter.ToInt16(bytes, i * 2);
            }

            return result.Where(x => x != 0).ToArray();
        }

        /// <summary>
        /// string[] 转 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this string[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<short>();

            var list = new List<short>();
            foreach (var str in data)
            {
                list.AddRange(str.ToShortArray());
            }
            return list.ToArray();
        }

        /// <summary>
        /// float 转 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this float data)
        {
            byte[] bytes = BitConverter.GetBytes(data);
            short[] shorts = new short[2];
            Buffer.BlockCopy(bytes, 0, shorts, 0, 4);
            return shorts;
        }

        /// <summary>
        /// float[] 转 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this float[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<short>();

            short[] result = new short[data.Length * 2];
            Buffer.BlockCopy(data, 0, result, 0, data.Length * 4);
            return result;
        }

        /// <summary>
        /// double 转为 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this double data)
        {
            byte[] bytes = BitConverter.GetBytes(data);
            short[] shorts = new short[4];
            Buffer.BlockCopy(bytes, 0, shorts, 0, 8);
            return shorts;
        }

        /// <summary>
        ///  double[] 转为 short[]
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static short[] ToShortArray(this double[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<short>();

            short[] result = new short[data.Length * 4];
            Buffer.BlockCopy(data, 0, result, 0, data.Length * 8);
            return result;
        }

        #endregion
    }
}


