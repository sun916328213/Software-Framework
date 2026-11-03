using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Core.Geometry;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Vision.Controls;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;

using Vision.Tools.OpenCv.Processing;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class P3EditorInteractionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void CropPage_SubmitsRoiToTool_ExecutesIndependentOutput_WithoutReplacingInput()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(OpenCvToolCatalog.Entries, [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = browser.ToolEntries.Single(e => e.Name == "OpenCV/Crop");
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                browser.LoadImage();
                var input = browser.InputImage;
                browser.RoiEditor.Clear(); browser.RoiEditor.AddRectangle(20, 20, 20, 20);
                Assert.True(browser.SupportsCropRegion);
                Assert.False(browser.SupportsTemplateInput);
                Assert.IsType<TabItem>(page.DetailsPanel.FindName("RoiTab")).IsSelected = true;
                Drain();
                Invoke(page, "使用选中矩形作为裁剪区域");
                var parameters = Assert.IsType<CropParameters>(browser.CurrentParameters);
                Assert.Equal(10, parameters.X); Assert.Equal(20, parameters.Width);
                Assert.IsType<TabItem>(page.DetailsPanel.FindName("ParametersTab")).IsSelected = true;
                Drain(); // 执行入口已集中到参数区，不再占据画布上方的一整行。
                Invoke(page, "执行");
                var result = Assert.IsType<CropResult>(browser.CurrentCard!.LastResult);
                Assert.True(result.Success, result.Message);
                Assert.Same(input, browser.InputImage);
                Assert.Equal(64, input!.Width);
                Assert.Equal(20, result.OutputImage.Width);
                Assert.Equal(10, result.InputOffset.X);
                Assert.All(result.OutputImage.Pixels, p => Assert.Equal(255, p));
                Assert.Same(result.OutputImage, VisionDisplay.OutputImage(result));
                browser.SelectOutputImage();
                Drain();
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                Assert.True(viewport.ShowOutput);
                Assert.Same(input, viewport.Image);
                Assert.Contains("偏移", browser.CurrentCard.ResultDetails);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void TemplatePage_UsesCapabilityButtonsAndSharedSession_AndUnloadDoesNotReleaseModel()
    {
        host.Invoke(() =>
        {
            var tool = new TemplateEditorWorkflowTests.FakeTemplateTool();
            using var workspace = new VisionBrowserWorkspace(new(
                [new("NoNameHeuristic", "Other", () => (tool, new object(), null))], [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput, (_, _) => null));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                browser.LoadImage();
                Assert.IsType<TabItem>(page.DetailsPanel.FindName("TemplateTab")).IsSelected = true;
                Drain();
                var train = Descendants(page).OfType<Button>().Single(b => Equals(b.Content, "训练模型"));
                Assert.False(train.IsEnabled);
                browser.RoiEditor.AddRectangle(32, 32, 100, 60); // 模板范围由用户显式添加，不依赖种子 ROI。
                Drain();
                Invoke(page, "选中矩形设为模板");
                Assert.True(train.IsEnabled);
                Invoke(page, "训练模型");
                Assert.True(browser.TemplateEditor!.HasModel);
                Assert.False(browser.TemplateEditor.NeedsTraining);
                Assert.Equal(1, tool.TrainCount);
                window.Content = null;
                Drain();
                Assert.False(tool.Disposed);
                Assert.True(browser.TemplateEditor.IsActive);
                window.Content = page;
                Drain();
                Assert.True(browser.TemplateEditor.HasModel);
                Invoke(page, "清除模板/模型");
                Assert.False(browser.TemplateEditor.HasModel);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Theory]
    [InlineData("OpenCV/TemplateMatch")]
    [InlineData("HALCON/ShapeMatch")]
    public void MatchingRuntimeParameters_UseSharedMetadataPanel_AndWriteThroughToOriginalDto(string name)
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                OpenCvToolCatalog.Entries.Concat(HalconToolCatalog.Entries).ToArray(), [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = browser.ToolEntries.Single(e => e.Name == name);
            var parameters = browser.CurrentParameters!;
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                Assert.True(browser.UsesMetadataEditor);
                Assert.Empty(Descendants(page).OfType<Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid>());
                var panel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(parameters, panel.TargetObject);
                var minimum = Descendants(panel).OfType<DecimalUpDown>().Single(c =>
                    BindingOperations.GetBinding(c, DecimalUpDown.ValueProperty)?.Path?.Path == "MinScore");
                minimum.SetCurrentValue(DecimalUpDown.ValueProperty, 0.7m);
                Drain();
                Assert.Equal(0.7, Assert.IsType<double>(parameters.GetType().GetProperty("MinScore")!.GetValue(parameters)));
                Assert.Same(parameters, browser.CurrentParameters);
                Assert.False(browser.TemplateEditor!.NeedsTraining);

                browser.SelectedEntry = browser.ToolEntries.Single(e => e.Name == "OpenCV/Crop");
                browser.LoadImage();
                browser.CurrentCard!.Run();
                var currentResult = browser.CurrentCard.LastResult;
                Assert.True(currentResult!.Success);
                // 模拟已卸载参数面板的迟到回调，不能清掉新工具的结果。
                var callback = typeof(ToolDetailsView).GetMethod("MetadataParametersChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var stalePanel = new VisionPropertyPanel { TargetObject = parameters };
                callback.Invoke(page.DetailsPanel, [stalePanel, EventArgs.Empty]);
                Assert.Same(currentResult, browser.CurrentCard.LastResult);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void LoadingTrainingParameters_UpdatesSharedPanel_WithoutMarkingLoadedModelDirty()
    {
        host.Invoke(() =>
        {
            var tool = new TemplateEditorWorkflowTests.FakeTemplateTool { AllowLoad = true };
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Editor", "Other", () => (tool, new object(), null))], [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput, (_, _) => "fake-model.fake"));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                browser.LoadImage();
                Assert.IsType<TabItem>(page.DetailsPanel.FindName("TemplateTab")).IsSelected = true;
                Drain();
                browser.RoiEditor.AddRectangle(32, 32, 100, 60);
                Drain();
                Invoke(page, "选中矩形设为模板");
                Invoke(page, "训练模型");
                var panel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(tool.Training, panel.TargetObject);
                var minimum = Descendants(panel).OfType<DecimalUpDown>().Single(c =>
                    BindingOperations.GetBinding(c, DecimalUpDown.ValueProperty)?.Path?.Path == "ScaleMin");
                minimum.SetCurrentValue(DecimalUpDown.ValueProperty, 0.8m);
                Drain();
                Assert.Equal(0.8, tool.Training.ScaleMin);
                Assert.True(browser.TemplateEditor!.NeedsTraining);
                var dirtyCount = tool.DirtyCount;
                Invoke(page, "加载模板/模型");
                Assert.Equal(0.75, tool.Training.ScaleMin);
                Assert.Equal(0.75m, minimum.Value);
                Assert.Equal(dirtyCount, tool.DirtyCount);
                Assert.False(browser.TemplateEditor.NeedsTraining);
                Assert.True(browser.TemplateEditor.SaveCommand.CanExecute(null));
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ModelPreview_DoesNotInventClosingEdge_ForOpenSdkContours()
    {
        host.Invoke(() =>
        {
            Point2D[] points = [new(0, 0), new(10, 0), new(10, 10)];
            var preview = new TemplateContourPreview { Contours = [new RegionContour(points, isClosed: false)] };
            preview.Measure(new Size(100, 100)); preview.Arrange(new Rect(0, 0, 100, 100));
            byte CenterGreen()
            {
                preview.UpdateLayout();
                var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(preview);
                var pixel = new byte[4];
                bitmap.CopyPixels(new Int32Rect(50, 50, 1, 1), pixel, 4, 0);
                return pixel[1];
            }
            Assert.Equal(30, CenterGreen());
            preview.Contours = [new RegionContour(points, isClosed: true)];
            Assert.True(CenterGreen() > 100);
        });
    }

    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 1400, Height = 900, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Invoke(DependencyObject page, string text)
    {
        var button = Descendants(page).OfType<Button>().Single(b => Equals(b.Content, text) || System.Windows.Automation.AutomationProperties.GetName(b) == text);
        Assert.True(button.IsEnabled);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        Drain();
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
