using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Roi;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class EditorInteractionSimplificationTests(WorkbenchAppFixture host)
{
    [Fact]
    public void ResultsTab_EnablesCanvasPickingWithoutModeButton_AndLeavingRestoresRoiEditing()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace();
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = WindowFor(page);
            try
            {
                browser.LoadImage(); browser.CurrentCard!.Run();
                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                var grid = Assert.Single(Descendants(page).OfType<DataGrid>());
                grid.SelectedItem = grid.Items[0]; Drain();
                Assert.True(viewport.SelectResultAt(new(15, 30), 0.1)); Drain();
                Assert.Equal(9, Assert.IsType<Vision.Controls.Presentation.VisionResultItem>(grid.SelectedItem).Id);
                Assert.False(browser.RoiEditor.IsManipulating);
                ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
                Assert.Equal("选择", viewport.Mode); Assert.False(viewport.SelectResultAt(new(15, 30), 0.1));
                Assert.DoesNotContain(Descendants(page).OfType<Button>(),
                    button => AutomationProperties.GetName(button) is "结果选择" or "平移图像");
                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
                Assert.Equal("结果选择", viewport.Mode);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void Workspace_DoesNotSeedRois_OnCreationLoadingOrExecution()
    {
        using var workspace = Workspace();
        using var browser = new VisionBrowserViewModel(workspace);
        Assert.Empty(browser.RoiEditor.Rois); Assert.Null(browser.RoiEditor.Selected);
        browser.LoadImage(); browser.CurrentCard!.Run();
        Assert.Empty(browser.RoiEditor.Rois); Assert.Null(browser.RoiEditor.Selected);
    }

    [Fact]
    public void AxisAlignedRectangle_CannotRotateNumericallyOrThroughAHandle()
    {
        var editor = new RoiEditorViewModel(); var roi = editor.AddRectangle(100, 100, 40, 20);
        Assert.False(editor.CanRotateSelected); Assert.Null(editor.RotationHandle);
        editor.SelectedAngle = 45;
        Assert.Equal(RoiKind.Rectangle, roi.Kind); Assert.Equal(0, roi.Pose.AngleDeg);
        Assert.False(editor.TryBeginManipulation(new(100, 70), 0.5));
        editor.Selected = roi; editor.MoveSelected(5, 6); editor.ResizeSelected(50, 30);
        Assert.Equal(RoiKind.Rectangle, roi.Kind); Assert.Equal(0, roi.Pose.AngleDeg);
    }

    [Fact]
    public void RotatedRectangle_AtZeroDegrees_RemainsRotatable_AndCancellationRestoresItsPose()
    {
        var editor = new RoiEditorViewModel(); var roi = editor.AddRotatedRectangle(100, 100, 40, 20, 30);
        editor.SelectedAngle = 0;
        Assert.Equal(RoiKind.RotatedRectangle, roi.Kind); Assert.True(editor.CanRotateSelected);
        Assert.True(editor.TryBeginManipulation(editor.RotationHandle!.Value, 0.5));
        editor.UpdateManipulation(new(130, 100)); Assert.Equal(90, roi.Pose.AngleDeg, 8);
        editor.EndManipulation(cancel: true);
        Assert.Equal(RoiKind.RotatedRectangle, roi.Kind); Assert.Equal(0, roi.Pose.AngleDeg);
    }

    [Fact]
    public void RoiTypeDropdown_OnlyAddsOnExplicitCommand_AndAngleEditingMatchesTheKind()
    {
        host.Invoke(() =>
        {
            var editor = new RoiEditorViewModel(); var view = new RoiEditorView { Editor = editor };
            var window = WindowFor(view);
            try
            {
                var selector = Assert.Single(Descendants(view).OfType<ComboBox>());
                Assert.Equal(new[] { "矩形", "旋转矩形", "圆", "椭圆", "多边形" }, selector.Items.Cast<string>());
                var add = Descendants(view).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "添加ROI");
                var kinds = new[] { RoiKind.Rectangle, RoiKind.RotatedRectangle, RoiKind.Circle, RoiKind.Ellipse, RoiKind.Polygon };
                for (var i = 0; i < kinds.Length; i++)
                {
                    selector.SelectedIndex = i; Drain(); Assert.Equal(i, editor.Count);
                    ((IInvokeProvider)new ButtonAutomationPeer(add).GetPattern(PatternInterface.Invoke)!).Invoke(); Drain();
                    Assert.Equal(i + 1, editor.Count); Assert.Equal(kinds[i], editor.Selected!.Kind);
                    var angle = Descendants(view).OfType<TextBox>().Single(t => BindingOperations.GetBindingExpression(t, TextBox.TextProperty)?.ParentBinding.Path.Path == "SelectedAngle");
                    Assert.Equal(kinds[i] is RoiKind.RotatedRectangle or RoiKind.Ellipse, angle.IsEnabled);
                }
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void RemovedToolbarModeControls_LeaveRoiAddingInTheRoiTab()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace(); using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser }; var window = WindowFor(page);
            try
            {
                browser.LoadImage();
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                Assert.Equal("选择", viewport.Mode);
                Assert.Null(page.FindName("RoiTypeSelector"));
                Assert.DoesNotContain(Descendants(page).OfType<Button>(),
                    button => AutomationProperties.GetName(button) is "结果选择" or "平移图像" or "编辑搜索域");
                ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
                var roiView = Assert.Single(Descendants(page).OfType<RoiEditorView>());
                var selector = Assert.Single(Descendants(roiView).OfType<ComboBox>());
                var add = Descendants(roiView).OfType<Button>().Single(button => AutomationProperties.GetName(button) == "添加ROI");
                var kinds = new[] { RoiKind.Rectangle, RoiKind.RotatedRectangle, RoiKind.Circle, RoiKind.Ellipse, RoiKind.Polygon };
                for (var i = 0; i < kinds.Length; i++)
                {
                    selector.SelectedIndex = i; Drain();
                    Assert.Equal(i, browser.RoiEditor.Count);
                    ((IInvokeProvider)new ButtonAutomationPeer(add).GetPattern(PatternInterface.Invoke)!).Invoke(); Drain();
                    Assert.Equal(i + 1, browser.RoiEditor.Count);
                    Assert.Equal(kinds[i], browser.RoiEditor.Selected!.Kind);
                    Assert.Equal("选择", viewport.Mode);
                }
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static VisionBrowserWorkspace Workspace() => new(new(
        [new("Regions", "Test", () => (new ResultSelectionTests.ResultTool(ResultSelectionTests.CreateResult), new object(), null))], [],
        _ => throw new InvalidOperationException(), () => VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000])));
    private static Window WindowFor(FrameworkElement content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
