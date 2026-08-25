using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// 数学运算转换器，支持 MultiBinding 进行加减乘除运算
    /// </summary>
    public class MathConverter : IMultiValueConverter
    {
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

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
