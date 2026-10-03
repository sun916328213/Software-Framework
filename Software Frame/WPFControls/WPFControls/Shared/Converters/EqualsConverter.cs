// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：EqualsConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Globalization、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPFControls.Controls.Converters
{
    /// <summary>
    /// 相等比较转换器
    /// 比较两个值是否相等，返回布尔值
    /// </summary>
    public class EqualsConverter : IMultiValueConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// 正文中的关键协作调用包括 Equals；具体条件和先后次序见方法体。
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var val1 = values[0];
            var val2 = values[1];
            
            // 如果任一为 null，直接比较
            if (val1 == null || val2 == null)
            {
                return val1 == val2;
            }

            // 使用 Equals 方法比较
            return val1.Equals(val2);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConvertBack。
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
