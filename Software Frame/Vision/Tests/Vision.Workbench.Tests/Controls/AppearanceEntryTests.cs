using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Extension.UI.Theme;
using Prism.Ioc;
using Prism.Regions;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using WPFCustomControls.Controls;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class AppearanceEntryTests(WorkbenchAppFixture host)
{
    [Fact]
    public void SharedAppearanceEntry_IsLazy_BorrowsHostTheme_AndUnsubscribesOnCloseAndUnload()
    {
        host.Invoke(() =>
        {
            var service = new ThemeService(); var replacement = new ThemeService(); var context = new object();
            var entry = new AppearancePickerView { Theme = service, DataContext = context };
            var window = new Window { Content = entry, Width = 400, Height = 300, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                var popup = Assert.IsType<Popup>(entry.FindName("AppearancePopup"));
                var content = Assert.IsType<ContentControl>(entry.FindName("PickerHost"));
                var button = Assert.IsType<Button>(entry.FindName("AppearanceButton"));
                Assert.Null(content.Content); Assert.Same(context, entry.DataContext);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain();
                Assert.True(popup.IsOpen);
                var picker = Assert.IsType<ThemePickerView>(content.Content);
                Assert.Same(service, picker.Theme); Assert.Same(service, picker.ViewModel.Theme);
                Assert.Equal(1, SubscriptionCount(service, picker.ViewModel));
                popup.IsOpen = false; Drain(); Assert.Equal(0, SubscriptionCount(service, picker.ViewModel));
                entry.Theme = replacement; Drain();
                Assert.Same(replacement, picker.ViewModel.Theme);
                Assert.Equal(0, SubscriptionCount(service, picker.ViewModel)); Assert.Equal(0, SubscriptionCount(replacement, picker.ViewModel));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain();
                Assert.Equal(1, SubscriptionCount(replacement, picker.ViewModel));
                window.Content = null; Drain();
                Assert.False(popup.IsOpen); Assert.Equal(0, SubscriptionCount(replacement, picker.ViewModel));
                Assert.Same(context, entry.DataContext);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void IndependentVisionHost_ProvidesOneSharedAppearanceEntry_WithoutLegacyBrowserNavigation()
    {
        host.Invoke(() =>
        {
            var regions = ContainerLocator.Container.Resolve<IRegionManager>();
            regions.RequestNavigate("VisionRegion", nameof(VisionFlowView));
            var page = Assert.IsType<VisionFlowView>(Assert.Single(regions.Regions["VisionRegion"].ActiveViews));
            var entry = Assert.IsType<AppearancePickerView>(page.ToolbarAccessory);
            var theme = Assert.IsType<global::Vision.Workbench.ViewModels.MainWindowViewModel>(Application.Current.MainWindow.DataContext).Theme;
            Assert.Same(theme, entry.Theme);
            Assert.DoesNotContain(Descendants(Application.Current.MainWindow).OfType<ThemePickerView>(), _ => true);
            Assert.Same(typeof(ThemePickerView).Assembly, entry.GetType().Assembly);
        });
    }

    [Fact]
    public void AppearanceSlot_ReservesSpace_AndRunRemainsAvailableOnRoiAndResultTabs()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Fake", "Test", () => (new FakeTool(), new object(), null))], [],
                _ => throw new InvalidOperationException(), () => null));
            using var browser = new VisionBrowserViewModel(workspace);
            var entry = new AppearancePickerView { Theme = new ThemeService() };
            var page = new VisionToolEditorView { Session = browser, ToolbarAccessory = entry };
            var window = new Window { Content = page, Width = 900, Height = 700, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                var toolbar = Assert.IsType<System.Windows.Controls.Grid>(page.FindName("TopMenuBar"));
                var run = Assert.Single(Descendants(toolbar).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "执行");
                var right = run.TransformToAncestor(page).TransformBounds(new Rect(run.RenderSize)).Right;
                var left = entry.TranslatePoint(new Point(), page).X;
                Assert.True(right <= left);
                foreach (var name in new[] { "RoiTab", "ResultsTab" })
                {
                    ((TabItem)page.DetailsPanel.FindName(name)).IsSelected = true; Drain();
                    Assert.True(run.IsVisible); Assert.Same(browser.CurrentCard!.RunCommand, run.Command);
                }
                Assert.Empty(Descendants(toolbar).OfType<Menu>());
                Assert.DoesNotContain(Descendants(toolbar).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) is "使用演示图" or "Benchmark");
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static int SubscriptionCount(ThemeService service, object target) =>
        (typeof(ThemeService).GetField("ThemeChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service) as Delegate)?
            .GetInvocationList().Count(handler => ReferenceEquals(handler.Target, target)) ?? 0;
    private sealed class FakeTool : IExecutableTool
    {
        public string Name => "Fake";
        public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new() { Success = true };
        public void EnsureLoaded() { }
        public void Dispose() { }
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
