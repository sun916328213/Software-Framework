using Extension.Struct;
using System;
using System.Globalization;
using System.Windows.Data;
using WPFControl.Resources;

namespace WPFControl.ValueConvert
{
    public class SecsStatusToColorConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case ESecsMode.Offline:
                    return ColorHelper.Color_Red;
                case ESecsMode.Online_Local:
                    return ColorHelper.Color_Orange;
                case ESecsMode.Online_Remote:
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
