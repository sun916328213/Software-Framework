using System;
using System.Globalization;
using System.Windows.Data;

namespace WPFCustomControls.ValueConvert;

/// <summary>布尔值取反转换器。</summary>
public class BoolInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
