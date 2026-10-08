// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RoundedCornerClipAssistTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows、System.Windows.Controls、System.Windows.Media、WPFCustomControls.Controls、WPFCustomControls.Tests.Window。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPFCustomControls.Controls;
using WPFCustomControls.Tests.Window;

namespace WPFCustomControls.Tests.Controls;

/// <summary>
/// RoundedCornerClipAssistTests 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class RoundedCornerClipAssistTests
{
    /// <summary>
    /// 测试相关入口 EnabledBorder_ClipsChildWithoutClippingBorderSurface；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Measure、Arrange、SetIsEnabled、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void EnabledBorder_ClipsChildWithoutClippingBorderSurface()
    {
        StaFact.Run(() =>
        {
            var child = new Border();
            var border = new Border
            {
                Width = 100,
                Height = 50,
                BorderThickness = new Thickness(4),
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(8),
                Child = child
            };

            border.Measure(new Size(100, 50));
            border.Arrange(new Rect(0, 0, 100, 50));
            RoundedCornerClipAssist.SetIsEnabled(border, true);

            Assert.Null(border.Clip);
            var clip = Assert.IsAssignableFrom<Geometry>(child.Clip);
            var childOrigin = child.TranslatePoint(new Point(), border);
            var thickness = border.BorderThickness;
            Assert.Equal(
                new Rect(
                    thickness.Left - childOrigin.X,
                    thickness.Top - childOrigin.Y,
                    border.ActualWidth - thickness.Left - thickness.Right,
                    border.ActualHeight - thickness.Top - thickness.Bottom),
                clip.Bounds);
            Assert.True(
                clip.FillContains(new Point(-5, child.RenderSize.Height / 2)),
                "位于外层 Border 内、但超出 Child 布局槽的白色绘制区域不应被裁掉");
        });
    }

    /// <summary>
    /// 测试相关入口 AsymmetricCornerRadius_DoesNotRoundSquareCorners；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Measure、Arrange、SetIsEnabled、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AsymmetricCornerRadius_DoesNotRoundSquareCorners()
    {
        StaFact.Run(() =>
        {
            var child = new Border();
            var border = new Border
            {
                Width = 100,
                Height = 50,
                CornerRadius = new CornerRadius(20, 0, 0, 0),
                Child = child
            };

            border.Measure(new Size(100, 50));
            border.Arrange(new Rect(0, 0, 100, 50));
            RoundedCornerClipAssist.SetIsEnabled(border, true);

            Assert.NotNull(child.Clip);
            Assert.False(child.Clip.FillContains(new Point(1, 1)));
            Assert.True(child.Clip.FillContains(new Point(99, 1)));
        });
    }

    /// <summary>
    /// 测试相关入口 Disabling_RestoresChildOriginalClip；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Measure、Arrange、SetIsEnabled、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Disabling_RestoresChildOriginalClip()
    {
        StaFact.Run(() =>
        {
            var originalClip = new RectangleGeometry(new Rect(2, 2, 20, 20));
            var child = new Border { Clip = originalClip };
            var border = new Border
            {
                Width = 100,
                Height = 50,
                CornerRadius = new CornerRadius(8),
                Child = child
            };

            border.Measure(new Size(100, 50));
            border.Arrange(new Rect(0, 0, 100, 50));
            RoundedCornerClipAssist.SetIsEnabled(border, true);
            RoundedCornerClipAssist.SetIsEnabled(border, false);

            Assert.Same(originalClip, child.Clip);
        });
    }
}
