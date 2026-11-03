using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class RoiInteractionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void SelectionGeometryAndNumericFields_StaySynchronized_AndReloadKeepsBorrowedEditor()
    {
        host.Invoke(() =>
        {
            var editor = new RoiEditorViewModel();
            var first = editor.AddRectangle(10, 20, 10, 20);
            var circle = editor.AddCircle(50, 50, 20);
            var viewport = new VisionImageViewport
            {
                Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Editor = editor, Image = VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000])
            };
            var fields = new RoiEditorView { Editor = editor };
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.Children.Add(viewport);
            Grid.SetColumn(fields, 1);
            content.Children.Add(fields);
            var window = new Window { Content = content, Width = 500, Height = 550, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                Drain();
                viewport.ActualPixels();
                Drain();
                var types = Assert.Single(Descendants(fields).OfType<ComboBox>());
                Assert.Null(types.ItemsSource);
                var choices = types.Items.Cast<string>().ToArray();
                Assert.Equal(new[] { "矩形", "旋转矩形", "圆", "椭圆", "多边形" }, choices);
                Assert.Equal("矩形", types.SelectedValue);
                types.SelectedItem = choices[2]; Drain();
                Assert.Equal("圆", editor.SelectedRoiType);
                editor.SelectedRoiType = "多边形"; Drain();
                Assert.Equal(choices[4], types.SelectedItem);
                Assert.Equal(2, editor.Count); // 改类型不隐式创建ROI。
                var x = Assert.IsType<TextBox>(fields.FindName("RoiX"));
                var width = Assert.IsType<TextBox>(fields.FindName("RoiWidth"));
                Assert.Equal("50", x.Text);
                Assert.True(IsYellow(viewport, 60, 50));
                Assert.True(editor.TryBeginManipulation(new(50, 50), 1));
                editor.UpdateManipulation(new(55, 57));
                Drain();
                Assert.Equal("55", x.Text);
                Assert.True(IsYellow(viewport, 65, 57));
                editor.EndManipulation(cancel: true);
                Drain();
                Assert.Equal("50", x.Text);
                Assert.True(IsYellow(viewport, 60, 50));
                x.Text = "45";
                x.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal(45, circle.Pose.X);
                width.Text = "-1";
                width.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.True(Validation.GetHasError(width));
                Assert.Equal(20, circle.Size.Width);
                width.Text = "30";
                width.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.False(Validation.GetHasError(width));
                Assert.Equal(new Size2D(30, 30), circle.Size);
                var list = Descendants(fields).OfType<ListBox>().Single();
                list.SelectedItem = first;
                Drain();
                Assert.Same(first, editor.Selected);
                Assert.Equal("10", x.Text);
                window.Content = null;
                Drain();
                editor.SelectedX = 25;
                window.Content = content;
                Drain();
                Assert.Same(editor, fields.Editor);
                Assert.Equal("25", x.Text);
                Assert.Equal(2, editor.Count);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void WorkspaceImageToolAndCloseChanges_CancelPendingRoiManipulation()
    {
        host.Invoke(() =>
        {
            using var browser = new TestBrowserViewModel();
            var editor = browser.RoiEditor;
            editor.Clear();
            var roi = editor.AddRectangle(20, 20, 10, 10);
            void Begin()
            {
                Assert.True(editor.TryBeginManipulation(new(20, 20), 0.1));
                editor.UpdateManipulation(new(25, 25));
                Assert.True(editor.IsManipulating);
            }
            Begin();
            browser.LoadImage();
            Assert.False(editor.IsManipulating);
            Assert.Equal(new Pose2D(20, 20, 0), roi.Pose);
            Begin();
            browser.SelectedEntry = browser.ToolEntries.Single(t => t.Name == "OpenCV/Threshold");
            Assert.False(editor.IsManipulating);
            Assert.Equal(new Pose2D(20, 20, 0), roi.Pose);
            Begin();
            browser.Dispose();
            Assert.False(editor.IsManipulating);
            Assert.Equal(new Pose2D(20, 20, 0), roi.Pose);
        });
    }

    private static bool IsYellow(FrameworkElement view, int x, int y)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[0] < 80 && pixel[1] > 200 && pixel[2] > 200;
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
