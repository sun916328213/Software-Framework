using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignColors;

namespace WPFCustomControls.ValueConvert;

/// <summary>
/// MaterialDesign 配色枚举（<c>PrimaryColor</c> / <c>SecondaryColor</c> / <c>MaterialDesignColor</c>）
/// → 该色板的主色（500 中调）纯色画刷，用于配色预览的小色块。
/// <para>
/// 取色走 MaterialDesign 自带的 <see cref="SwatchHelper.Lookup"/>，不自己抄调色板，
/// 保证预览色与 BundledTheme 实际应用的色完全一致（同一份数据源）。
/// 三个枚举都是同一批色板名，统一按名字转成 <see cref="MaterialDesignColor"/> 再查表，
/// 免去枚举之间的强转。
/// </para>
/// 认不出的一律返回 <see cref="Brushes.Transparent"/>，不抛异常（绑定场景宁可无色也不要崩）。
/// </summary>
public sealed class ThemeColorToBrushConverter : IValueConverter
{
    /// <summary>共享的已冻结画刷缓存：下拉项会有几十个，避免反复 new。</summary>
    private static readonly Dictionary<Color, SolidColorBrush> Cache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = Resolve(value);
        return color is null ? Brushes.Transparent : GetBrush(color.Value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>
    /// 枚举值 → 色板中调颜色；认不出返回 null。
    /// 除转换器自用外，<c>ThemePickerViewModel</c> 也用它筛掉没有颜色的占位值
    /// （如 <c>PrimaryColor.Inherit</c>），保证列出来的选项都能画出预览。
    /// </summary>
    public static Color? Resolve(object? value)
    {
        if (value is null)
            return null;

        // 三个枚举名一一对应，按名字转最省事，也不用关心底层数值是否对齐。
        var name = value.ToString();
        if (string.IsNullOrEmpty(name) || !Enum.TryParse<MaterialDesignColor>(name, out var swatch))
            return null;

        return SwatchHelper.Lookup.TryGetValue(swatch, out var color) ? color : null;
    }

    private static SolidColorBrush GetBrush(Color color)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(color, out var cached))
                return cached;

            var brush = new SolidColorBrush(color);
            brush.Freeze(); // 冻结后可跨线程共享，也不怕被 BAML 复用
            Cache[color] = brush;
            return brush;
        }
    }
}
