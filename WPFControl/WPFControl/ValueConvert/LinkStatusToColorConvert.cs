using WPFControl.ProgramControl.Models;
using WPFControl.Resources;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WPFControl.ValueConvert
{
    public class LinkStatusToColorConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case ELinkStatus.Offline:
                    return  ColorHelper.Color_Red;
                case ELinkStatus.Online:
                    return ColorHelper.Color_Green;
                default:
                    return ColorHelper.Color_Red;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
