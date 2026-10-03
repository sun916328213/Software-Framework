// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：IntZeroToVisibilityConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Globalization、System.Windows、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFCustomControls.ValueConvert;

/// <summary>
/// int 值为 0（或 null）时返回 Visible，否则 Collapsed。用于集合为空时的占位提示。
/// </summary>
public class IntZeroToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Convert。
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ConvertBack。
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
