using Extension.UI.Controls;
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// 将HelpTool.Language.ELang枚举转换为对应的区域性标识符
    /// </summary>
    public class LanguageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ELang lang)
            {
                return lang == ELang.ZH ? "zh-CN" : "en-US";
            }
            return "zh-CN"; // 默认返回中文
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}