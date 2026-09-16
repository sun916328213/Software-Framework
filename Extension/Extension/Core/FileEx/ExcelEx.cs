using MiniExcelLibs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace Extension.Core.FileEx
{
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
            MiniExcel.SaveAs(filePath, data);
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
           await MiniExcel.SaveAsAsync(filePath, data);
        }
    }
}
