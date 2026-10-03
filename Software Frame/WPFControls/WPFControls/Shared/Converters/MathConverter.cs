// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：MathConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Globalization、System.Windows、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFControls.Controls.Converters
{
    /// <summary>
    /// 数学运算转换器，支持 MultiBinding 进行加减乘除运算
    /// </summary>
    public class MathConverter : IMultiValueConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// 正文中的关键协作调用包括 ToDouble；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return DependencyProperty.UnsetValue;

            try
            {
                // 获取第一个值（基础值）
                double baseValue = System.Convert.ToDouble(values[0]);
                
                // 获取第二个值（运算值）
                double operand = System.Convert.ToDouble(values[1]);

                // 执行加法运算（因为第二个值已经是负数，所以加法等同于减法）
                double result = baseValue + operand;

                return result;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
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
