// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DeviceStatusToColorConvert。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Globalization、System.Windows.Data、WPFControls.Controls.Resources、Extension.Core.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Globalization;
using System.Windows.Data;
using WPFControls.Controls.Resources;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;


namespace WPFControls.ValueConvert
{
    /// <summary>
    /// DeviceStatusToColorConvert 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：IValueConverter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class DeviceStatusToColorConvert : IValueConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConvertBack。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
