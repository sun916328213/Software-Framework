// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：PropertiesConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls、System、System.Collections.Generic、System.ComponentModel、System.Globalization、System.Linq。导入不代表所有类型都会在运行时加载。
using Vision.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Data;

namespace Vision.Controls.Converter
{
    /// <summary>
    /// PropertiesConverter 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：IValueConverter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class PropertiesConverter : IValueConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// 正文中的关键协作调用包括 ToList、Select、Where、GetProperties、GetType；具体条件和先后次序见方法体。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var props = value.GetType()
                .GetProperties()
                .Where(p => p.CanRead &&
                            p.GetCustomAttributes(typeof(BrowsableAttribute), true)
                             .All(a => ((BrowsableAttribute)a).Browsable))
                .Select(p => new KeyValuePair<string, string>
                (
                    p.Name,
                    GetStringValue(p, value)
                ))
                .ToList();

            return props;
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetStringValue。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static string GetStringValue(PropertyInfo p, object value)
        {
            try
            {
                return p.GetValue(value)?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConvertBack。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
