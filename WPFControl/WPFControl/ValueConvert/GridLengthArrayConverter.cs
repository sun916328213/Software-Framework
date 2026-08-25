using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// GridLength数组到GridLength的转换器
    /// 用于从GridLength数组中获取指定索引的元素
    /// </summary>
    public class GridLengthArrayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength[] gridLengths && parameter is string indexStr && int.TryParse(indexStr, out int index))
            {
                if (index >= 0 && index < gridLengths.Length)
                {
                    return gridLengths[index];
                }
            }
            return new GridLength(1, GridUnitType.Auto);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}