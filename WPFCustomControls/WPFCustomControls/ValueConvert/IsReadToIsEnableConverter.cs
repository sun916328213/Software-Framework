using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFCustomControls.ValueConvert
{
    /// <summary>
    /// IsReadToIsEnableConverter（从 WPFControl 迁移，随 DeviceManagerView 下沉）。
    /// true 转换成 false，false 转换成 true。
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
