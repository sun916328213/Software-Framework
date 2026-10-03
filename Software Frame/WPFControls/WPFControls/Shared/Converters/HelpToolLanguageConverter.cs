// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：HelpToolLanguageConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.UI.Controls、System、System.Globalization、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.UI.Controls;
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPFControls.Controls.Converters
{
    /// <summary>
    /// 将HelpTool.Language.ELang枚举转换为对应的区域性标识符
    /// </summary>
    public class HelpToolLanguageConverter : IValueConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Convert。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ELang lang)
            {
                return lang == ELang.ZH ? "zh-CN" : "en-US";
            }
            return "zh-CN"; // 默认返回中文
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