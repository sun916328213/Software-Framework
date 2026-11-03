using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Catalog;
using Vision.Controls.Models;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Core.Imaging;
using Vision.Tests.Tools.AI;
using Vision.Tools.AI;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class AiModelPanelTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("Classification")]
    [InlineData("Detection")]
    [InlineData("Segmentation")]
    [InlineData("AnomalyDetection")]
    public async Task ModelPage_EditsOriginalParameters_AndExplicitLoadOwnsSession(string task)
    {
        var provider = new FakeAIProvider();
        FakeAIModelSession? opened = null;
        provider.OpenSessionFunc = request => opened = new FakeAIModelSession(request.ModelPath, request.ModelType, provider.Capabilities);
        var workspace = host.Invoke(() => Create(task, provider));
        Window? window = null;
        try
        {
            host.Invoke(() =>
            {
                var dto = Assert.IsAssignableFrom<AIModelParameters>(workspace.Parameters);
                var page = Open(workspace, out window);
                var details = page.DetailsPanel;
                var tabs = Assert.IsType<TabControl>(details.FindName("DetailsTabs"));
                var modelTab = Assert.IsType<TabItem>(details.FindName("ModelTab"));
                tabs.SelectedItem = details.FindName("ParametersTab"); Drain();
                var model = Assert.IsType<VisionPropertyPanel>(details.FindName("ParametersPanel"));
                var load = LoadButton(details);
                Assert.Same(dto, model.TargetObject);
                var expectedNames = task is "Detection" or "Segmentation"
                    ? new[] { "DeviceId", "MaxResults", "MinArea", "ModelPath", "ScoreThreshold" }
                    : new[] { "DeviceId", "MaxResults", "ModelPath", "ScoreThreshold" };
                Assert.Equal(expectedNames, Names(model).OrderBy(x => x));
                var path = Text(model, nameof(AIModelParameters.ModelPath));
                var device = Text(model, nameof(AIModelParameters.DeviceId));
                path.Text = "first.model"; path.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                device.Text = "cpu"; device.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.Equal("first.model", dto.ModelPath); Assert.Equal("cpu", dto.DeviceId);
                dto.DeviceId = "gpu0"; device.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget(); Drain();
                Assert.Equal("gpu0", device.Text);
                device.Text = "cpu"; device.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.Same(dto, workspace.Editor.CurrentParameters);
                Assert.Equal(0, provider.OpenSessionCalls);
                Assert.Same(workspace.Model.CurrentCard!.EnsureLoadedCommand, load.Command);
                Assert.True(load.Command.CanExecute(load.CommandParameter));
                load.Command.Execute(load.CommandParameter); Drain();
                Assert.Equal(1, provider.OpenSessionCalls);
                Assert.Equal("first.model", provider.LastRequest!.ModelPath);
                Assert.Equal("cpu", provider.LastRequest.DeviceId);
                var card = workspace.Model.CurrentCard;
                workspace.SetExecutionImage(VisionImage.FromBuffer(64, 64, Vision.Core.Imaging.PixelFormat.Mono8, new byte[64 * 64]));
                card.Run(); Assert.True(card.LastResult!.Success);
                var oldSession = Assert.IsType<FakeAIModelSession>(opened);
                path.Text = "second.model"; path.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.Equal("second.model", dto.ModelPath); Assert.Null(card.LastResult);
                card.Run(); Assert.False(card.LastResult!.Success); Assert.Equal(1, oldSession.RunCalls);
                load.Command.Execute(load.CommandParameter); Drain();
                Assert.Equal(2, provider.OpenSessionCalls); Assert.Equal(1, provider.CloseSessionCalls);
                Assert.Same(oldSession, provider.LastClosed); Assert.Equal("second.model", provider.LastRequest!.ModelPath);
                card.Model.IsReadOnly = true; Drain();
                Assert.False(model.IsEnabled); Assert.False(load.IsEnabled);
                card.Model.IsReadOnly = false; Drain();
                Assert.True(model.IsEnabled); Assert.True(load.IsEnabled);
                tabs.SelectedItem = modelTab; Drain();
                Assert.Same(dto, workspace.Editor.CurrentParameters);
                tabs.SelectedItem = details.FindName("ParametersTab"); Drain();
                Assert.Same(dto, model.TargetObject);
                window!.Close(); window = null; Drain();
                Assert.False(workspace.Model.IsClosed);
                Assert.Equal(1, provider.CloseSessionCalls);
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => workspace.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task SwitchingSessionsAndTabs_KeepsEachOriginalModelAndOwner()
    {
        var firstProvider = new FakeAIProvider(); var secondProvider = new FakeAIProvider();
        var first = host.Invoke(() => Create("Detection", firstProvider));
        var second = host.Invoke(() => Create("Classification", secondProvider));
        Window? window = null;
        try
        {
            host.Invoke(() =>
            {
                var page = Open(first, out window);
                var tabs = Assert.IsType<TabControl>(page.DetailsPanel.FindName("DetailsTabs"));
                tabs.SelectedItem = page.DetailsPanel.FindName("ParametersTab"); Drain();
                var panel = Assert.IsType<VisionPropertyPanel>(page.DetailsPanel.FindName("ParametersPanel"));
                var a = Assert.IsAssignableFrom<AIModelParameters>(first.Parameters);
                var b = Assert.IsAssignableFrom<AIModelParameters>(second.Parameters);
                var input = Text(panel, "ModelPath"); input.Text = "a.model";
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                var button = LoadButton(page.DetailsPanel);
                button.Command.Execute(button.CommandParameter); Drain();
                page.Session = second.Editor; Drain();
                Assert.Same(b, panel.TargetObject);
                input = Text(panel, "ModelPath"); input.Text = "b.model";
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                button.Command.Execute(button.CommandParameter); Drain();
                tabs.SelectedItem = page.DetailsPanel.FindName("ModelTab"); Drain();
                tabs.SelectedItem = page.DetailsPanel.FindName("ParametersTab"); Drain();
                Assert.Same(b, panel.TargetObject); Assert.Equal("b.model", Text(panel, "ModelPath").Text);
                page.Session = first.Editor; Drain();
                Assert.Same(a, panel.TargetObject); Assert.Equal("a.model", Text(panel, "ModelPath").Text);
                Assert.Equal("a.model", a.ModelPath); Assert.Equal("b.model", b.ModelPath);
                Assert.Equal(1, firstProvider.OpenSessionCalls); Assert.Equal(1, secondProvider.OpenSessionCalls);
                Assert.Equal(0, firstProvider.CloseSessionCalls); Assert.Equal(0, secondProvider.CloseSessionCalls);
                window!.Close(); window = null; Drain();
                Assert.False(first.Model.IsClosed); Assert.False(second.Model.IsClosed);
                Assert.Equal(0, firstProvider.CloseSessionCalls); Assert.Equal(0, secondProvider.CloseSessionCalls);
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => first.DisposeAsync().AsTask());
            await host.Invoke(() => second.DisposeAsync().AsTask());
        }
    }

    private static VisionToolInstanceWorkspace Create(string task, FakeAIProvider provider)
    {
        var entry = AIToolCatalog.Entries.Single(e => e.Name == "AI/" + task);
        var catalog = new VisionBrowserCatalog([entry], [new ProviderEntry(provider.Name, true)], _ => provider, () => null);
        return new VisionToolInstanceWorkspace(catalog, entry);
    }

    private static VisionToolEditorView Open(VisionToolInstanceWorkspace workspace, out Window window)
    {
        var page = new VisionToolEditorView { Session = workspace.Editor };
        window = new Window { Content = page, Width = 1200, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return page;
    }

    private static Button LoadButton(DependencyObject root) => Assert.Single(Descendants(root).OfType<Button>(),
        button => System.Windows.Automation.AutomationProperties.GetName(button) == "加载模型");
    private static string[] Names(VisionPropertyPanel panel) => panel.Model.Groups.SelectMany(g => g.Items)
        .OfType<VisionPropertyItem>().Where(row => row.IsRowVisible).Select(row => row.PropertyName).ToArray();
    private static TextBox Text(VisionPropertyPanel panel, string property) => Assert.Single(Descendants(panel).OfType<TextBox>(),
        box => BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path?.Path == property);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

}
