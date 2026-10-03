// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ExcelEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MiniExcelLibs、System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using MiniExcelLibs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace Extension.Integration.MiniExcel
{
    /// <summary>
    /// ExcelEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// </summary>
    public class ExcelEx
    {
        /// <summary>
        /// 保存到Excel文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        public static void SaveToExcel<T>(string filePath, IEnumerable<T> data)
        {
            MiniExcelLibs.MiniExcel.SaveAs(filePath, data);
        }


        /// <summary>
        /// 保存到Excel文件寮傛
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static async Task SaveToExcelAsync<T>(string filePath, IEnumerable<T> data)
        {
           await MiniExcelLibs.MiniExcel.SaveAsAsync(filePath, data);
        }
    }
}
