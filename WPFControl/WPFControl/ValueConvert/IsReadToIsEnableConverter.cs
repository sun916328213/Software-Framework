using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// IsReadToIsEnableConverter
    /// true转换为"false"，false转换为"true"
    /// </summary>
    public class IsReadToIsEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}