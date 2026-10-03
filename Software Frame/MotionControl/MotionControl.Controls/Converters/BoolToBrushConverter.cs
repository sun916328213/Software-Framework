// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：BoolToBrushConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Globalization、System.Windows.Data、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotionControl.Controls.Converters;

/// <summary>布尔状态转指示灯刷子：true=绿色，false=灰色（可通过 <see cref="TrueBrush"/> 覆盖）。</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    /// <summary>true 时使用的刷子。</summary>
    public Brush TrueBrush { get; set; } = Brushes.LimeGreen;

    /// <summary>false 时使用的刷子。</summary>
    public Brush FalseBrush { get; set; } = Brushes.LightGray;

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Convert。
    /// </remarks>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? TrueBrush : FalseBrush;

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ConvertBack。
    /// </remarks>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
