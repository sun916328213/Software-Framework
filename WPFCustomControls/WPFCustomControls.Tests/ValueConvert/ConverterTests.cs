using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WPFCustomControls.ValueConvert;

namespace WPFCustomControls.Tests.ValueConvert;

/// <summary>
/// ValueConvert 子模块所有 IValueConverter / IMultiValueConverter 的单元测试。
/// </summary>
public class ConverterTests
{
    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    // ── BoolInverseConverter ────────────────────────────────────

    [Fact]
    public void BoolInverse_TrueBecomesFalse()
    {
        var c = new BoolInverseConverter();
        var converted = c.Convert(true, typeof(bool), null!, Zh);
        var back = c.ConvertBack(false, typeof(bool), null!, Zh);
        Assert.False((bool)converted);
        Assert.True((bool)back);
    }

    [Fact]
    public void BoolInverse_NonBoolPassthrough()
    {
        // 非 bool 输入应原样返回,不抛异常
        var c = new BoolInverseConverter();
        const string input = "hello";
        Assert.Same(input, c.Convert(input, typeof(bool), null!, Zh));
        Assert.Same(input, c.ConvertBack(input, typeof(bool), null!, Zh));
    }

    // ── BoolInverseToVisibilityConverter ────────────────────────

    [Fact]
    public void BoolInverseToVisibility_TrueBecomesCollapsed()
    {
        var c = new BoolInverseToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, c.Convert(true, typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Visible, c.Convert(false, typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void BoolInverseToVisibility_NonTrueBecomesVisible()
    {
        var c = new BoolInverseToVisibilityConverter();
        // null / 非 true 走到 else 分支 → Visible
        Assert.Equal(Visibility.Visible, c.Convert(null, typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void BoolInverseToVisibility_ConvertBackRoundtrip()
    {
        var c = new BoolInverseToVisibilityConverter();
        var visToFalse = c.ConvertBack(Visibility.Visible, typeof(bool), null!, Zh);
        var collapsedToTrue = c.ConvertBack(Visibility.Collapsed, typeof(bool), null!, Zh);
        var hiddenToTrue = c.ConvertBack(Visibility.Hidden, typeof(bool), null!, Zh);
        Assert.False((bool)visToFalse);
        Assert.True((bool)collapsedToTrue);
        Assert.True((bool)hiddenToTrue);
    }

    // ── BorderClipConverter ────────────────────────────────────

    [Fact]
    public void BorderClip_SmallSize_ReturnsEmpty()
    {
        // 尺寸 < 1.0 → Geometry.Empty,防止渲染异常
        var c = new BorderClipConverter();
        var result = c.Convert(new object[] { 0.0, 100.0 }, typeof(Geometry), null!, Zh);
        Assert.Same(Geometry.Empty, result);
    }

    [Fact]
    public void BorderClip_NullValues_ReturnsUnsetValue()
    {
        var c = new BorderClipConverter();
        var result = c.Convert(null!, typeof(Geometry), null!, Zh);
        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void BorderClip_TooFewValues_ReturnsUnsetValue()
    {
        var c = new BorderClipConverter();
        var result = c.Convert(new object[] { 100.0 }, typeof(Geometry), null!, Zh);
        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void BorderClip_ValidRect_ReturnsFrozenGeometry()
    {
        var c = new BorderClipConverter();
        var result = c.Convert(new object[] { 100.0, 50.0 }, typeof(Geometry), null!, Zh);
        Assert.IsType<StreamGeometry>(result);
        Assert.True(((Geometry)result).IsFrozen);
    }

    [Fact]
    public void BorderClip_WithCornerRadius_ProducesPath()
    {
        var c = new BorderClipConverter();
        var radius = new CornerRadius(8, 8, 8, 8);
        var result = c.Convert(
            new object[] { 100.0, 50.0, radius, new Thickness(1) },
            typeof(Geometry), null!, Zh);

        Assert.IsType<StreamGeometry>(result);
    }

    [Fact]
    public void BorderClip_ConvertBack_Throws()
    {
        var c = new BorderClipConverter();
        Assert.Throws<NotImplementedException>(() =>
            c.ConvertBack(null!, new[] { typeof(Geometry) }, null!, Zh));
    }

    // ── IntZeroToVisibilityConverter ────────────────────────────

    [Fact]
    public void IntZeroToVisibility_ZeroBecomesVisible()
    {
        var c = new IntZeroToVisibilityConverter();
        Assert.Equal(Visibility.Visible, c.Convert(0, typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void IntZeroToVisibility_NonZeroBecomesCollapsed()
    {
        var c = new IntZeroToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, c.Convert(1, typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Collapsed, c.Convert(-1, typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void IntZeroToVisibility_NonIntBecomesCollapsed()
    {
        var c = new IntZeroToVisibilityConverter();
        // 非 int 走 else 分支 → Collapsed
        Assert.Equal(Visibility.Collapsed, c.Convert(null, typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Collapsed, c.Convert("0", typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void IntZeroToVisibility_ConvertBack_Throws()
    {
        var c = new IntZeroToVisibilityConverter();
        Assert.Throws<NotSupportedException>(() =>
            c.ConvertBack(Visibility.Visible, typeof(int), null!, Zh));
    }

    // ── StringNotEmptyToVisibilityConverter ─────────────────────

    [Fact]
    public void StringNotEmpty_EmptyBecomesCollapsed()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, c.Convert("", typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Collapsed, c.Convert(null, typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void StringNotEmpty_NonEmptyBecomesVisible()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        Assert.Equal(Visibility.Visible, c.Convert("hello", typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Visible, c.Convert(" ", typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void StringNotEmpty_NonStringBecomesCollapsed()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        // 非 string 走 IsNullOrEmpty → true → Collapsed
        Assert.Equal(Visibility.Collapsed, c.Convert(123, typeof(Visibility), null!, Zh));
    }

    [Fact]
    public void StringNotEmpty_ConvertBack_Throws()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        Assert.Throws<NotSupportedException>(() =>
            c.ConvertBack(Visibility.Visible, typeof(string), null!, Zh));
    }
}