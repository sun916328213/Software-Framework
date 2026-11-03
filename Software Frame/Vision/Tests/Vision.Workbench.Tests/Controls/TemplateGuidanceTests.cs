using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class TemplateGuidanceTests(WorkbenchAppFixture host)
{
    [Fact]
    public void OpenCvGuidance_ExplainsWorkflowAndSearchLimitations()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(OpenCvToolCatalog.Entries, [],
                _ => throw new InvalidOperationException(), Input));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = browser.ToolEntries.Single(e => e.Name == "OpenCV/TemplateMatch");
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                SelectTemplate(page);
                var steps = Text(page, "TemplateSteps");
                Assert.Contains("ROI", steps, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("矩形", steps);
                Assert.Contains("轴对齐", steps);
                Assert.Contains("模板", steps);
                Assert.Contains("参数", steps);
                Assert.Contains("搜索", steps);
                Assert.Contains("执行", steps);
                Assert.True(steps.IndexOf("ROI", StringComparison.OrdinalIgnoreCase) < steps.IndexOf("模板", StringComparison.Ordinal));
                Assert.True(steps.IndexOf("参数", StringComparison.Ordinal) < steps.IndexOf("执行", StringComparison.Ordinal));
                Assert.Contains("尚无取样", Text(page, "TemplateSize"));
                var guidance = Text(page, "TemplateSearchGuidance");
                Assert.Contains("角度", guidance);
                Assert.Contains("0", guidance);
                Assert.Contains("固定", guidance);
                Assert.Contains("1亿", guidance);
                Assert.Contains("内存", guidance);
                Assert.Contains("耗时", guidance);
                Assert.Contains("精化", guidance);
                Assert.Contains("图像", guidance);
                Assert.Contains("非", guidance);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void HalconGuidance_UsesTrainingOrExplicitLoad_NotOpenCvBudget()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(HalconToolCatalog.Entries, [],
                _ => throw new InvalidOperationException(), Input));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = browser.ToolEntries.Single(e => e.Name == "HALCON/ShapeMatch");
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                SelectTemplate(page);
                var guidance = Text(page, "TemplateSearchGuidance");
                Assert.Contains("训练", guidance);
                Assert.Contains("加载", guidance);
                Assert.Contains("显式", guidance);
                Assert.DoesNotContain("1亿", guidance);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void LoadedModelWithoutSample_ShowsEmptySample()
    {
        host.Invoke(() =>
        {
            var tool = new TemplateEditorWorkflowTests.FakeTemplateTool { AllowLoad = true };
            using var workspace = new VisionBrowserWorkspace(new(
                [new("HALCON/ShapeMatch", "HALCON", () => (tool, new object(), null))], [],
                _ => throw new InvalidOperationException(), Input, (_, _) => "model.fake"));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                SelectTemplate(page);
                browser.TemplateEditor!.LoadCommand.Execute(null);
                Drain();
                Assert.True(tool.HasModel);
                Assert.Null(tool.TemplateImage);
                Assert.Contains("尚无取样", Text(page, "TemplateSize"));
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void Size_FollowsCaptureClearSwitchBorrowingLocksAndDirectRefresh()
    {
        host.Invoke(() =>
        {
            var tool = new TemplateEditorWorkflowTests.FakeTemplateTool();
            using var workspace = new VisionBrowserWorkspace(new(
                [new("First", "Other", () => (tool, new object(), null)),
                 new("Second", "Other", () => (new TemplateEditorWorkflowTests.FakeTemplateTool(), new object(), null))], [],
                _ => throw new InvalidOperationException(), Input, (_, _) => null));
            using var browser = new VisionBrowserViewModel(workspace);
            using var borrower = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = borrower };
            var window = Show(page);
            try
            {
                SelectTemplate(page);
                Assert.Same(browser.TemplateEditor, borrower.TemplateEditor);
                Assert.Contains("尚无取样", Text(page, "TemplateSize"));
                browser.LoadImage();
                browser.RoiEditor.Clear();
                browser.RoiEditor.AddRectangle(32, 32, 20, 12);
                browser.SetTemplateFromRoiCommand.Execute(null);
                Drain();
                var sample = Assert.IsType<VisionImage>(tool.TemplateImage);
                Assert.Equal(20, sample.Width);
                Assert.Equal(12, sample.Height);
                AssertSize(page, 20, 12);

                browser.CurrentCard!.Model.IsReadOnly = true;
                Drain();
                Assert.False(browser.TemplateEditor!.ClearCommand.CanExecute(null));
                browser.TemplateEditor.ClearCommand.Execute(null);
                AssertSize(page, 20, 12);
                browser.CurrentCard.Model.IsReadOnly = false;
                browser.TemplateEditor.ClearCommand.Execute(null);
                Drain();
                Assert.Contains("尚无取样", Text(page, "TemplateSize"));

                tool.SetTemplate(VisionImage.FromBuffer(7, 9, PixelFormat.Mono8, new byte[63]));
                browser.TemplateEditor.RefreshFromTool();
                Drain();
                AssertSize(page, 7, 9);
                page.Session = browser;
                borrower.Dispose();
                Drain();
                Assert.False(tool.Disposed);
                AssertSize(page, 7, 9);
                browser.SelectedEntry = browser.ToolEntries[1];
                Drain();
                SelectTemplate(page);
                Assert.True(tool.Disposed);
                Assert.Contains("尚无取样", Text(page, "TemplateSize"));
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static VisionImage Input() => VisionImage.FromBuffer(64, 64, PixelFormat.Mono8, new byte[64 * 64]);

    private static Window Show(VisionToolEditorView page)
    {
        var window = new Window { Content = page, Width = 1000, Height = 700, ShowInTaskbar = false };
        window.Show();
        Drain();
        return window;
    }

    private static void SelectTemplate(VisionToolEditorView page)
    {
        Assert.IsType<TabItem>(page.DetailsPanel.FindName("TemplateTab")).IsSelected = true;
        Drain();
    }

    private static string Text(VisionToolEditorView page, string name) =>
        Assert.IsType<TextBlock>(page.DetailsPanel.FindName(name)).Text;

    private static void AssertSize(VisionToolEditorView page, int width, int height)
    {
        var text = Text(page, "TemplateSize");
        Assert.Contains(width.ToString(), text);
        Assert.Contains(height.ToString(), text);
        Assert.DoesNotContain("尚无取样", text);
    }

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
