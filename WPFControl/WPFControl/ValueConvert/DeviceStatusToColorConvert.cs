using System;
using System.Globalization;
using System.Windows.Data;
using WPFControl.Resources;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;


namespace WPFControl.ValueConvert
{
    public class DeviceStatusToColorConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case EDeviceStatus.Idle:
                    return ColorHelper.Color_Black;
                case EDeviceStatus.Ready:
                    return ColorHelper.Color_Blue;
                case EDeviceStatus.Running:
                    return ColorHelper.Color_Green;
                case EDeviceStatus.Paused:
                    return ColorHelper.Color_Orange;
                case EDeviceStatus.Error:
                    return ColorHelper.Color_Red;
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
