// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：DateTimeEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Diagnostics、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    /// <summary>
    /// DateTimeEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static  class DateTimeEx
    {
        /// <summary>
        /// 时间转换成字符串 yyyy/MM/dd HH:mm:ss
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToDateTime(this DateTime data)
        {
            return data.ToString("yyyy/MM/dd HH:mm:ss");
        }
 
        /// <summary>
        /// 时间转换成字符串 HH:mm:ss
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToTime(this DateTime data)
        {
            return data.ToString("HH:mm:ss");
        }

        /// <summary>
        /// 时间转换成字符串 HHmmssmsms
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToTimeMs(this DateTime data)
        {
            return data.Hour.ToString()+data.Minute.ToString() + data.Second.ToString() + data.Millisecond.ToString();
        }

        /// <summary>
        /// 时间转换成字符串 yyyy/MM/dd
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToDate(this DateTime data)
        {
            return data.ToString("yyyy/MM/dd");
        }

        /// <summary>
        /// 时间转换成字符串 yyyy/MM
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToYearMonth(this DateTime data)
        {
            return data.ToString("yyyy/MM");
        }

        /// <summary>
        /// 时间转换成字符串 yyyyMMdd
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToDateNOSapce(this DateTime data)
        {
            return data.ToString("yyyyMMdd");
        }


        /// <summary>
        /// 时间转换成字符串  YYYYMMDDHHMMSSFFF
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToYYYYMMDDHHMMSSFFF(this DateTime data)
        {
            return data.ToString("yyyyMMddhhmmssfff");
        }

        /// <summary>
        ///  时间转换成秒
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static double ToSeconds(this Stopwatch data)
        {
            return data.ElapsedMilliseconds / 1000.0;
        }
 
    }
}


