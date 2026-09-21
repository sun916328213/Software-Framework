using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPFCustomControls.Controls;
using WPFCustomControls.Tests.Window;

namespace WPFCustomControls.Tests.Controls;

public sealed class RoundedCornerClipAssistTests
{
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
