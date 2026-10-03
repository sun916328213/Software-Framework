// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：InverseBooleanToVisibilityConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Globalization、System.Windows、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFControls.Controls.Converters
{
    /// <summary>
    /// 布尔值取反转可见性转换器
    /// false -> Visible, true -> Collapsed
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        private static InverseBooleanToVisibilityConverter? _instance;
        
        public static InverseBooleanToVisibilityConverter Instance
        {
            get => _instance ??= new InverseBooleanToVisibilityConverter();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConvertBack。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return false;
        }
    }
}
