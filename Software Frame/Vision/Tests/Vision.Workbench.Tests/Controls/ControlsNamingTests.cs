using System.Windows;
using System.Windows.Controls;
using Vision.Controls.View;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class ControlsNamingTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("Vision.Controls.View.VisionToolEditorView")]
    [InlineData("Vision.Controls.ViewModels.VisionToolEditorSession")]
    [InlineData("Vision.Controls.Models.VisionToolEditorModel")]
    [InlineData("Vision.Controls.VisionMultiSelectComboBox")]
    public void CanonicalEditorTypes_AreExportedWithoutRetiredDebugBridges(string name)
    {
        Assert.NotNull(typeof(VisionToolEditorView).Assembly.GetType(name));
        Assert.Null(typeof(VisionToolEditorView).Assembly.GetType("Vision.Controls.View.ToolDebugView"));
        Assert.Null(typeof(VisionToolEditorView).Assembly.GetType("Vision.Controls.ViewModels.ToolDebugSession"));
        Assert.Null(typeof(VisionToolEditorView).Assembly.GetType("Vision.Controls.Models.ToolDebugModel"));
        Assert.Null(typeof(VisionToolEditorView).Assembly.GetType("Vision.Controls.Ctr_MultiSelectComboBox"));
    }
    [Fact]
    public void CanonicalEditor_BorrowsTheSameStateAndSession_WithoutDebugBridge()
    {
        host.Invoke(() =>
        {
            var model = new Vision.Controls.Models.VisionToolEditorModel();
            using var session = new Vision.Controls.ViewModels.VisionToolEditorSession(model);
            var view = new VisionToolEditorView { Session = session };
            Assert.IsAssignableFrom<VisionToolEditorView>(view);
            Assert.IsAssignableFrom<Vision.Controls.ViewModels.VisionToolEditorSession>(session);
            Assert.IsAssignableFrom<Vision.Controls.Models.VisionToolEditorModel>(model);
            Assert.Same(model, session.Model);
            Assert.Same(session, ((VisionToolEditorView)view).Session);
        });
    }
    [Theory]
    [InlineData("Views/Editor/VisionToolEditorView.xaml")]
    [InlineData("Themes/LegacyHalconResources.xaml")]
    [InlineData("Views/Editor/ToolEditorView.xaml")]
    public void CanonicalPackUris_Resolve(string path)
    {
        host.Invoke(() => Assert.NotNull(Application.GetResourceStream(new Uri($"pack://application:,,,/Vision.Controls;component/{path}"))));
    }
    [Fact]
    public void RetiredNativeControls_AreNotExportedOrPackaged_ButHostThemeStillLoads()
    {
        host.Invoke(() =>
        {
            var assembly = typeof(VisionToolEditorView).Assembly;
            foreach (var name in new[] { "Vision.Controls.Ctr_Halcon.View.HSmartWindowView",
                "Vision.Controls.Ctr_Halcon.View.Ctr_ROI", "Vision.Controls.Ctr_Halcon.Helper.HSmartWindowHelper" })
                Assert.Null(assembly.GetType(name));
            using var stream = assembly.GetManifestResourceStream("Vision.Controls.g.resources");
            Assert.NotNull(stream);
            using var resources = new System.Resources.ResourceReader(stream);
            var names = resources.Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();
            Assert.NotEmpty(names);
            Assert.DoesNotContain(names, name => name.Contains("hsmartwindowview", StringComparison.OrdinalIgnoreCase)
                || name.Contains("ctr_roi", StringComparison.OrdinalIgnoreCase));
            // 只退役旧URI转发，正式主题的资源和自身合并层级保持。
            var theme = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
                new Uri("/Vision.Controls;component/Themes/LegacyHalconResources.xaml", UriKind.Relative)));
            Assert.Equal(2, theme.MergedDictionaries.Count);
            Assert.True(theme.Contains("FocusVisual"));
            Assert.True(theme.Contains(typeof(ScrollViewer)));
        });
    }
    [Theory]
    [InlineData("ToolCatalogView")]
    [InlineData("ProjectToolbarView")]
    [InlineData("ToolCardView")]
    [InlineData("VisionTaskView")]
    [InlineData("ToolDiagnosticsView")]
    [InlineData("ToolDebugView")]
    [InlineData("VisionBrowserView")]
    public void RetiredDuplicateViews_AreNotExportedRegisteredOrPackaged(string name)
    {
        host.Invoke(() =>
        {
            var assembly = typeof(VisionToolEditorView).Assembly;
            Assert.Null(assembly.GetType("Vision.Controls.View." + name));
            var registry = (Prism.Ioc.IContainerRegistry)Prism.Ioc.ContainerLocator.Container;
            Assert.False(registry.IsRegistered(typeof(object), name));
            using var stream = assembly.GetManifestResourceStream("Vision.Controls.g.resources");
            Assert.NotNull(stream);
            using var resources = new System.Resources.ResourceReader(stream);
            var keys = resources.Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();
            Assert.NotEmpty(keys);
            Assert.DoesNotContain(keys, key => key.Contains(name, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void CompatibilityLayer_HasNoExportedAliasesOrPackResources()
    {
        var assembly = typeof(VisionToolEditorView).Assembly;
        foreach (var name in new[] { "Vision.Controls.Ctr_PropertyPanel",
            "Vision.Controls.Ctr_MultiSelectComboBox", "Vision.Controls.Ctr_Base.Ctr_ToolPortBindingPanal" })
            Assert.Null(assembly.GetType(name));
        using var stream = assembly.GetManifestResourceStream("Vision.Controls.g.resources");
        Assert.NotNull(stream);
        using var resources = new System.Resources.ResourceReader(stream);
        var keys = resources.Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();
        Assert.NotEmpty(keys);
        Assert.DoesNotContain(keys, key => key.StartsWith("view/", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("ctr_base/", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("ctr_halcon/", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("compatibility/", StringComparison.OrdinalIgnoreCase)
            || key.Equals("themes/generic.baml", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(assembly.GetType("Vision.Controls.View.ToolEditorView"));
        Assert.NotNull(assembly.GetType("Vision.Controls.ViewModels.DetectionToolSession"));
    }

    [Fact]
    public void CanonicalEditor_HasNoBrowserContainerOrNestedToolEditor()
    {
        host.Invoke(() =>
        {
            var view = new VisionToolEditorView();
            Assert.Null(view.FindName("EditorArea"));
            Assert.NotNull(view.FindName("TopMenuBar"));
        });
    }
}
