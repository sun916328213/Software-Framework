using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;

using Xunit;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class EditorRefinementTests(WorkbenchAppFixture host)
{
    public static IEnumerable<object[]> CatalogNames() => HalconToolCatalog.Entries.Concat(OpenCvToolCatalog.Entries).Concat(AIToolCatalog.Entries).Concat(CommonToolCatalog.Entries).Select(entry => new object[] { entry.Name });

    [Theory]
    [MemberData(nameof(CatalogNames))]
    public void EveryCatalogTool_UsesOnePropertyPanel_WithoutLoadingProviders(string name)
    {
        host.Invoke(() =>
        {
            var entries = HalconToolCatalog.Entries.Concat(OpenCvToolCatalog.Entries).Concat(AIToolCatalog.Entries).Concat(CommonToolCatalog.Entries).ToArray();
            using var workspace = new VisionBrowserWorkspace(new(entries, [], _ => throw new InvalidOperationException("不得隐式加载 Provider"), TestBrowserViewModel.CreateInput));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = entries.Single(entry => entry.Name == name);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                var panel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(browser.ParameterEditor, panel.TargetObject);
                Assert.NotEmpty(Descendants(panel).OfType<Expander>()); // 不接受“面板存在但没有参数”。
                Assert.DoesNotContain(Descendants(page), element => element.GetType().Name is "PropertyGrid" or "BlobParametersView");
                Assert.Null(browser.CurrentCard!.LastResult);
                Assert.Null(page.FindName("DrawMode")); Assert.Null(page.FindName("CompareBlobMenu"));
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), button => AutomationProperties.GetName(button) is "校验" or "合成图");
                var options = ((ListBox)page.ImageToolbar.FindName("ImageViewSelector")).Items.Cast<ListBoxItem>().ToArray();
                Assert.Equal(new[] { "输入图像", "ROI 图像", "结果图像", "训练图像" }, options.Select(option => option.Content));
                Assert.Equal(new[] { ImageViewKind.Input, ImageViewKind.Roi, ImageViewKind.Result, ImageViewKind.Training }, options.Select(option => (ImageViewKind)option.Tag));
                Assert.Same(browser.LineSearch, Assert.Single(Descendants(page).OfType<VisionImageViewport>()).LineSearch);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void ImageViews_AreLayerPresets_NotInputCropping_AndDoNotChangeMouseMode()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new([], [], _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage(); var input = browser.InputImage;
            var page = new VisionToolEditorView { Session = browser };
            var viewport = (VisionImageViewport)page.ImagePanel.FindName("Viewport");
            viewport.Mode = "平移";
            foreach (var view in new[] { ImageViewKind.Input, ImageViewKind.Roi, ImageViewKind.Result, ImageViewKind.Training })
            {
                browser.SelectedImageView = view; Drain();
                Assert.Same(input, browser.InputImage); Assert.Equal("平移", viewport.Mode);
                Assert.Equal(view == ImageViewKind.Roi, browser.ShowRois);
                Assert.Equal(view == ImageViewKind.Result, browser.ShowResults);
                Assert.Equal(view == ImageViewKind.Result, browser.ShowOutputImage);
            }
            var regionCount = browser.RoiEditor.Count;
            browser.ShowRois = false;
            Assert.Equal(regionCount, browser.RoiEditor.Count); Assert.Same(input, browser.InputImage);
        });
    }
    [Fact]
    public void LegacyHost_UsesTheSameBlobAdapter_AndRebindsWithoutOwningTheTool()
    {
        host.Invoke(() =>
        {
            var tool = new EditableProbe();
            var messages = new List<string>();
            tool.AttachPresentation(new Vision.Controls.Presentation.WpfToolPresentation(messages.Add));
            var page = new ToolEditorView { Tool = tool };
            var window = new Window { Content = page, Width = 900, Height = 600, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                Assert.IsType<BlobParametersEditorViewModel>(page.ParameterEditor);
                Assert.Equal(2, Descendants(page).OfType<VisionPropertyPanel>().Count());
                Assert.DoesNotContain(Descendants(page), element => element.GetType().Name == "PropertyGrid");
                tool.Cmd_BindingPort.OnBtClick(tool, EventArgs.Empty);
                Assert.Single(messages); // 元数据面板不能覆盖宿主的展示适配器。
                var replacement = new Vision.Tools.OpenCv.Measurement.FindLineParameters();
                tool.ReplaceParameters(replacement); Drain();
                Assert.Same(replacement, page.ParameterEditor);
                window.Content = null; Drain();
                Assert.Null(page.ParameterEditor);
                Assert.False(tool.ResetCalled);
                window.Content = page; Drain();
                Assert.Same(replacement, page.ParameterEditor);
            }
            finally { window.Close(); tool.Results.Dispose(); Drain(); }
        });
    }
    private sealed class EditableProbe : Vision.Core.Tools.Tool, Vision.Core.Tools.IEditableTool
    {
        private object _parameters = new Vision.Tools.OpenCv.Inspection.BlobParameters();
        public object EditableParameters => _parameters;
        public bool ResetCalled { get; private set; }
        public void ReplaceParameters(object parameters) { _parameters = parameters; OnPropertyChanged(nameof(EditableParameters)); }
        public Vision.Core.Results.ValidationResult Validate() => new();
        public override void Run() { }
        public override void Reset() => ResetCalled = true;
        public override void Pause() { }
        public override void Config() { }
        public override void SaveSnapShot() { }
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
