// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ConverterTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Globalization、System.Windows、System.Windows.Media、WPFCustomControls.ValueConvert。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
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

    /// <summary>
    /// 验证 BoolInverse_TrueBecomesFalse 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、ConvertBack、False、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BoolInverse_TrueBecomesFalse()
    {
        var c = new BoolInverseConverter();
        var converted = c.Convert(true, typeof(bool), null!, Zh);
        var back = c.ConvertBack(false, typeof(bool), null!, Zh);
        Assert.False((bool)converted);
        Assert.True((bool)back);
    }

    /// <summary>
    /// 验证 BoolInverse_NonBoolPassthrough 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Same、Convert、ConvertBack；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 BoolInverseToVisibility_TrueBecomesCollapsed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BoolInverseToVisibility_TrueBecomesCollapsed()
    {
        var c = new BoolInverseToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, c.Convert(true, typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Visible, c.Convert(false, typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 BoolInverseToVisibility_NonTrueBecomesVisible 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BoolInverseToVisibility_NonTrueBecomesVisible()
    {
        var c = new BoolInverseToVisibilityConverter();
        // null / 非 true 走到 else 分支 → Visible
        Assert.Equal(Visibility.Visible, c.Convert(null, typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 BoolInverseToVisibility_ConvertBackRoundtrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ConvertBack、False、True；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 BorderClip_SmallSize_ReturnsEmpty 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BorderClip_SmallSize_ReturnsEmpty()
    {
        // 尺寸 < 1.0 → Geometry.Empty,防止渲染异常
        var c = new BorderClipConverter();
        var result = c.Convert(new object[] { 0.0, 100.0 }, typeof(Geometry), null!, Zh);
        Assert.Same(Geometry.Empty, result);
    }

    /// <summary>
    /// 验证 BorderClip_NullValues_ReturnsUnsetValue 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BorderClip_NullValues_ReturnsUnsetValue()
    {
        var c = new BorderClipConverter();
        var result = c.Convert(null!, typeof(Geometry), null!, Zh);
        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    /// <summary>
    /// 验证 BorderClip_TooFewValues_ReturnsUnsetValue 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BorderClip_TooFewValues_ReturnsUnsetValue()
    {
        var c = new BorderClipConverter();
        var result = c.Convert(new object[] { 100.0 }, typeof(Geometry), null!, Zh);
        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    /// <summary>
    /// 验证 BorderClip_ValidRect_ReturnsFrozenGeometry 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、IsType、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BorderClip_ValidRect_ReturnsFrozenGeometry()
    {
        var c = new BorderClipConverter();
        var result = c.Convert(new object[] { 100.0, 50.0 }, typeof(Geometry), null!, Zh);
        Assert.IsType<StreamGeometry>(result);
        Assert.True(((Geometry)result).IsFrozen);
    }

    /// <summary>
    /// 验证 BorderClip_WithCornerRadius_ProducesPath 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Convert、IsType；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 BorderClip_ConvertBack_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、ConvertBack；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BorderClip_ConvertBack_Throws()
    {
        var c = new BorderClipConverter();
        Assert.Throws<NotImplementedException>(() =>
            c.ConvertBack(null!, new[] { typeof(Geometry) }, null!, Zh));
    }

    // ── IntZeroToVisibilityConverter ────────────────────────────

    /// <summary>
    /// 验证 IntZeroToVisibility_ZeroBecomesVisible 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void IntZeroToVisibility_ZeroBecomesVisible()
    {
        var c = new IntZeroToVisibilityConverter();
        Assert.Equal(Visibility.Visible, c.Convert(0, typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 IntZeroToVisibility_NonZeroBecomesCollapsed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void IntZeroToVisibility_NonZeroBecomesCollapsed()
    {
        var c = new IntZeroToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, c.Convert(1, typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Collapsed, c.Convert(-1, typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 IntZeroToVisibility_NonIntBecomesCollapsed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void IntZeroToVisibility_NonIntBecomesCollapsed()
    {
        var c = new IntZeroToVisibilityConverter();
        // 非 int 走 else 分支 → Collapsed
        Assert.Equal(Visibility.Collapsed, c.Convert(null, typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Collapsed, c.Convert("0", typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 IntZeroToVisibility_ConvertBack_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、ConvertBack；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void IntZeroToVisibility_ConvertBack_Throws()
    {
        var c = new IntZeroToVisibilityConverter();
        Assert.Throws<NotSupportedException>(() =>
            c.ConvertBack(Visibility.Visible, typeof(int), null!, Zh));
    }

    // ── StringNotEmptyToVisibilityConverter ─────────────────────

    /// <summary>
    /// 验证 StringNotEmpty_EmptyBecomesCollapsed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StringNotEmpty_EmptyBecomesCollapsed()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, c.Convert("", typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Collapsed, c.Convert(null, typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 StringNotEmpty_NonEmptyBecomesVisible 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StringNotEmpty_NonEmptyBecomesVisible()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        Assert.Equal(Visibility.Visible, c.Convert("hello", typeof(Visibility), null!, Zh));
        Assert.Equal(Visibility.Visible, c.Convert(" ", typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 StringNotEmpty_NonStringBecomesCollapsed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Convert；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StringNotEmpty_NonStringBecomesCollapsed()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        // 非 string 走 IsNullOrEmpty → true → Collapsed
        Assert.Equal(Visibility.Collapsed, c.Convert(123, typeof(Visibility), null!, Zh));
    }

    /// <summary>
    /// 验证 StringNotEmpty_ConvertBack_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、ConvertBack；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StringNotEmpty_ConvertBack_Throws()
    {
        var c = new StringNotEmptyToVisibilityConverter();
        Assert.Throws<NotSupportedException>(() =>
            c.ConvertBack(Visibility.Visible, typeof(string), null!, Zh));
    }
}