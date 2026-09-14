using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPFCustomControls.Controls;
using WPFCustomControls.Tests.Window;

namespace WPFCustomControls.Tests.Controls;

/// <summary>
/// <see cref="ScrollBarArrowAssist"/> 附加属性回归：
/// 1) 五个 DP 都注册了，类型正确；
/// 2) 默认值：Size=10 / Margin=0 / 两个画刷=null（null 表示「跟随主题」）/ 高亮圆角=0；
/// 3) Get/Set 往返正确；
/// 4) 全部带 <see cref="FrameworkPropertyMetadataOptions.Inherits"/> —— 这是「设在祖先上就能生效」
///    这条便利性的唯一依据，掉了就静默失效（模板里的绑定会拿到默认值，界面上看不出报错）；
/// 5) 继承真的能流到子元素（不只是元数据上标了个 flag）。
/// </summary>
public class ScrollBarArrowAssistTests
{
    private static readonly string[] PropertyNames =
    {
        "SizeProperty", "MarginProperty", "BrushProperty",
        "HighlightBrushProperty", "HighlightCornerRadiusProperty",
    };

    [Fact]
    public void AllProperties_AreRegistered()
    {
        var t = typeof(ScrollBarArrowAssist);
        foreach (var name in PropertyNames)
        {
            var field = t.GetField(name, BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(field);
            Assert.IsType<DependencyProperty>(field!.GetValue(null));
        }
    }

    [Fact]
    public void Defaults_AreAsDocumented()
    {
        StaFact.Run(() =>
        {
            var target = new Border();

            Assert.Equal(10d, ScrollBarArrowAssist.GetSize(target));
            Assert.Equal(default(Thickness), ScrollBarArrowAssist.GetMargin(target));
            // null = 跟随主题（三角取 MaterialDesign.Brush.Foreground，高亮取 ScrollBar.ActiveBackground）
            Assert.Null(ScrollBarArrowAssist.GetBrush(target));
            Assert.Null(ScrollBarArrowAssist.GetHighlightBrush(target));
            Assert.Equal(default(CornerRadius), ScrollBarArrowAssist.GetHighlightCornerRadius(target));
        });
    }

    [Fact]
    public void Setters_RoundTrip()
    {
        StaFact.Run(() =>
        {
            var target = new Border();
            var margin = new Thickness(0, 3, 0, 1);
            var radius = new CornerRadius(4);

            ScrollBarArrowAssist.SetSize(target, 14d);
            ScrollBarArrowAssist.SetMargin(target, margin);
            ScrollBarArrowAssist.SetBrush(target, Brushes.OrangeRed);
            ScrollBarArrowAssist.SetHighlightBrush(target, Brushes.Transparent);
            ScrollBarArrowAssist.SetHighlightCornerRadius(target, radius);

            Assert.Equal(14d, ScrollBarArrowAssist.GetSize(target));
            Assert.Equal(margin, ScrollBarArrowAssist.GetMargin(target));
            Assert.Same(Brushes.OrangeRed, ScrollBarArrowAssist.GetBrush(target));
            Assert.Same(Brushes.Transparent, ScrollBarArrowAssist.GetHighlightBrush(target));
            Assert.Equal(radius, ScrollBarArrowAssist.GetHighlightCornerRadius(target));
        });
    }

    [Fact]
    public void AllProperties_AreInheritable()
    {
        var t = typeof(ScrollBarArrowAssist);
        foreach (var name in PropertyNames)
        {
            var dp = (DependencyProperty)t.GetField(name, BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var metadata = (FrameworkPropertyMetadata)dp.GetMetadata(typeof(DependencyObject));
            Assert.True(
                metadata.Inherits,
                $"{name} 必须开启 FrameworkPropertyMetadataOptions.Inherits —— 否则「设在祖先上」不生效");
        }
    }

    [Fact]
    public void InheritedValue_FlowsToDescendant()
    {
        StaFact.Run(() =>
        {
            var child = new Border();
            // child 通过逻辑树挂在 root 下，自己一个附加属性都没设
            var root = new Grid { Children = { child } };

            ScrollBarArrowAssist.SetSize(root, 7d);
            ScrollBarArrowAssist.SetBrush(root, Brushes.Teal);

            Assert.Equal(7d, ScrollBarArrowAssist.GetSize(child));
            Assert.Same(Brushes.Teal, ScrollBarArrowAssist.GetBrush(child));
        });
    }
}
