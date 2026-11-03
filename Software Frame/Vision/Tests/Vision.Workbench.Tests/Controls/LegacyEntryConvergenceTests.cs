using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.View;
using Vision.Core.Results;
using ValidationResult = Vision.Core.Results.ValidationResult;
using Vision.Core.Tools;
using Xunit;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class LegacyEntryConvergenceTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(ModelPreparationKind.Load, "加载模型")]
    [InlineData(ModelPreparationKind.Train, "训练模型")]
    public void UnrelatedToolType_DeclaresRealPreparation_WithoutCallingConfig(ModelPreparationKind kind, string label)
    {
        host.Invoke(() =>
        {
            var tool = new PreparedProbe(kind);
            var replacement = new Probe();
            var page = new ToolEditorView { Tool = tool };
            var window = Show(page);
            try
            {
                var button = Assert.IsType<Button>(page.FindName("ModelButton"));
                Assert.Equal(Visibility.Visible, button.Visibility); Assert.Equal(label, button.Content);
                Assert.Equal(0, tool.PreparationCalls); // 构造、绑定、加载页面都不训练/加载模型。
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, tool.PreparationCalls); Assert.Equal(0, tool.ConfigCalls);
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => Equals(b.Content, "校验"));
                page.Tool = replacement; Drain();
                Assert.Equal(Visibility.Collapsed, button.Visibility);
                Assert.Same(replacement, page.Tool);
                page.Tool = tool; Drain();
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.Equal(0, replacement.ConfigCalls);
            }
            finally { window.Close(); tool.Results.Dispose(); replacement.Results.Dispose(); Drain(); }
        });
    }
    [Fact]
    public void LegacyEditorRun_ValidatesBeforeExecution_WithoutStandaloneCardUi()
    {
        host.Invoke(() =>
        {
            var tool = new Probe { Valid = false };
            var page = new ToolEditorView { Tool = tool };
            var window = Show(page);
            try
            {
                var run = Descendants(page).OfType<Button>().Single(b => Equals(b.Content, "执行"));
                run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(0, tool.RunCalls);
                Assert.Contains("invalid", ((TextBlock)page.FindName("Status")).Text);
                tool.Valid = true;
                run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, tool.RunCalls);
                Assert.Null(typeof(ToolEditorView).Assembly.GetType("Vision.Controls.View.ToolCardView"));
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => Equals(b.Content, "校验"));
                Assert.Equal(0, tool.ResetCalls); // 视图只借用工具。
            }
            finally { window.Close(); tool.Results.Dispose(); Drain(); }
        });
    }
    [Fact]
    public void ReentrantToolReplacement_DoesNotRunReplacementOrPublishAnOldPreparationError()
    {
        host.Invoke(() =>
        {
            var original = new Probe(); var replacement = new Probe();
            var page = new ToolEditorView { Tool = original };
            var window = Show(page);
            var preparation = new PreparedProbe(ModelPreparationKind.Load);
            try
            {
                original.Validating = () => page.Tool = replacement;
                Descendants(page).OfType<Button>().Single(b => Equals(b.Content, "执行")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Same(replacement, page.Tool); Assert.Equal(0, original.RunCalls); Assert.Equal(0, replacement.RunCalls);
                preparation.AfterPreparation = () => { page.Tool = replacement; throw new InvalidOperationException("old preparation error"); };
                page.Tool = preparation;
                ((Button)page.FindName("ModelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Same(replacement, page.Tool); Assert.Equal(1, preparation.PreparationCalls);
                Assert.DoesNotContain("old preparation error", ((TextBlock)page.FindName("Status")).Text);
            }
            finally { window.Close(); original.Results.Dispose(); replacement.Results.Dispose(); preparation.Results.Dispose(); Drain(); }
        });
    }
    private class Probe : Tool, IEditableTool
    {
        public bool Valid { get; set; } = true;
        public Action? Validating { get; set; }
        public int RunCalls { get; private set; }
        public int ConfigCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public object EditableParameters { get; } = new Vision.Tools.OpenCv.Processing.ThresholdParameters();
        public ValidationResult Validate()
        {
            Validating?.Invoke();
            return Valid ? new() : new ValidationResult().AddError("INVALID", "invalid");
        }
        public override void Run() => RunCalls++;
        public override void Config() => ConfigCalls++;
        public override void Reset() => ResetCalls++;
        public override void Pause() { }
        public override void SaveSnapShot() { }
    }
    private sealed class PreparedProbe(ModelPreparationKind kind) : Probe, IModelPreparationTool
    {
        public ModelPreparationKind PreparationKind => kind;
        public int PreparationCalls { get; private set; }
        public Action? AfterPreparation { get; set; }
        public void PrepareModel() { PreparationCalls++; AfterPreparation?.Invoke(); }
    }
    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 900, Height = 600, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
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
