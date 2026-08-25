using System;
using System.Globalization;
using System.Windows.Data;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// 相等比较转换器
    /// 比较两个值是否相等，返回布尔值
    /// </summary>
    public class EqualsConverter : IMultiValueConverter
    {
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

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
