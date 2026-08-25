using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// 布尔值到GridLength的转换器
    /// true转换为"*"，false转换为"0" 
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return booleanValue ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            }
            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}