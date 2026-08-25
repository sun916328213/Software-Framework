using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DPLibrary_Base_F48
{
    public class DoubleArray2DToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double[][] arr)
            {
                // 把二维数组格式化成字符串，比如每行用 ; 分隔
                return string.Join(" | ", arr.Select(row => string.Join(", ", row)));
            }
            return string.Empty;
        }

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
