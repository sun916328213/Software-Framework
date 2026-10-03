// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：BoolInverseConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.Globalization、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPFCustomControls.ValueConvert;

/// <summary>布尔值取反转换器。</summary>
public class BoolInverseConverter : IValueConverter
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Convert。
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ConvertBack。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
