// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：DoubleArray2DToStringConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Globalization、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Vision.Controls.Converters
{
    /// <summary>
    /// DoubleArray2DToStringConverter 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：IValueConverter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class DoubleArray2DToStringConverter : IValueConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// 正文中的关键协作调用包括 Join、Select；具体条件和先后次序见方法体。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double[][] arr)
            {
                // 把二维数组格式化成字符串，比如每行用 ; 分隔
                return string.Join(" | ", arr.Select(row => string.Join(", ", row)));
            }
            return string.Empty;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConvertBack。
        /// 正文中的关键协作调用包括 ToArray、Select、Split、Parse、Trim；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                try
                {
                    return str
                        .Split('|')
                        .Select(row => row.Split(',')
                                          .Select(v => double.Parse(v.Trim()))
                                          .ToArray())
                        .ToArray();
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
