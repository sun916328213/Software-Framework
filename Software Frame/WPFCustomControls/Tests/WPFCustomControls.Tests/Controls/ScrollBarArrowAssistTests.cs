// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ScrollBarArrowAssistTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Reflection、System.Windows、System.Windows.Controls、System.Windows.Media、WPFCustomControls.Controls。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
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

    /// <summary>
    /// 测试相关入口 AllProperties_AreRegistered；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetField、NotNull、IsType、GetValue；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Defaults_AreAsDocumented；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Equal、GetSize、GetMargin、Null；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Setters_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、SetSize、SetMargin、SetBrush、SetHighlightBrush；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 AllProperties_AreInheritable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetValue、GetField、GetMetadata、True；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 InheritedValue_FlowsToDescendant；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、SetSize、SetBrush、Equal、GetSize；具体条件和先后次序见方法体。
    /// </summary>
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
