// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：DoubleEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Globalization、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    /// <summary>
    /// DoubleEx 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public static class DoubleEx
    {

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToArrString。
        /// 正文中的关键协作调用包括 Join、Select；具体条件和先后次序见方法体。
        /// </summary>
        public static string ToArrString(this double[][] arr)
        {
            if (arr == null) return string.Empty;
            return string.Join(" | ", arr.Select(row => string.Join(", ", row)));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToDoubles。
        /// 正文中的关键协作调用包括 ToArray、Select、Split、Parse、Trim；具体条件和先后次序见方法体。
        /// </summary>
        public static double[][] ToDoubles(this string str)
        {
            return str
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)   // 行
                .Select(r => r.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(v => double.Parse(v.Trim(), CultureInfo.InvariantCulture))
                                  .ToArray())
                .ToArray();
        }

    }
}
