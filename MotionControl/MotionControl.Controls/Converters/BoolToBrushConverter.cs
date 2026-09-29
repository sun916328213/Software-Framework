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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? TrueBrush : FalseBrush;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
