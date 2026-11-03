using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Core.Imaging;
using Vision.FlowNode;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using WPFCustomControls.PropertyGrid.Views;
using Cv = Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowR0Tests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task VisionNodePropertyPanel_DoesNotExposeBottomActionButtons()
    {
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create());
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                vm.Select(node);
                Assert.Same(node, vm.PropertyGridVM.TargetObject);
                Assert.Empty(vm.PropertyGridVM.ExtraActions);
                Assert.NotEmpty(vm.PropertyGridVM.Groups);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task EditorEntryPoints_BorrowExactNode_AndWindowCloseDoesNotDisposeSession()
    {
        var creates = 0;
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(() => creates++));
        Window owner = null!; VisionFlowView page = null!;
        try
        {
            host.Invoke(() =>
            {
                page = new() { DataContext = vm };
                owner = new() { Content = page, Width = 1500, Height = 850, ShowInTaskbar = false, Left = -10000 };
                owner.Show();
                var entry = vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob");
                var a = vm.Add(entry); var b = vm.Add(entry);
                var workspace = (VisionToolInstanceWorkspace)a.Session;
                var originalCard = workspace.Editor.CurrentCard;
                vm.Select(a); Idle(page);
                Assert.Same(a, vm.PropertyGridVM.TargetObject);
                var grid = Assert.IsType<PropertyGridHost>(page.FindName("NodeProperties"));
                Assert.Same(vm.PropertyGridVM, grid.DataContext);
                Assert.Null(page.FindName("ToolEditor"));
                var loadMenu = Assert.IsType<MenuItem>(page.FindName("LoadTaskImageMenuItem"));
                var captureMenu = Assert.IsType<MenuItem>(page.FindName("CaptureFixtureMenuItem"));
                Assert.Same(vm.LoadImageCommand, loadMenu.Command);
                Assert.Same(vm.CaptureFixtureReferenceCommand, captureMenu.Command);
                Assert.Empty(vm.PropertyGridVM.ExtraActions);
                a.OpenToolEditorCommand.Execute(null);
                var first = Assert.Single(owner.OwnedWindows.Cast<Window>());
                var editor = Assert.IsType<VisionToolEditorView>(first.Content);
                Assert.Same(workspace.Editor, editor.Session);
                foreach (var name in new[] { "OpenProjectButton", "SaveProjectButton", "SaveProjectAsButton", "LoadImageButton", "RunButton" })
                    Assert.True(Assert.IsType<Button>(editor.FindName(name)).IsVisible);
                Assert.Same(workspace, editor.Session!.ProjectCommands);
                Assert.Same(owner, first.Owner);
                vm.Select(b); Idle(page);
                Assert.Same(workspace.Editor, editor.Session); // A窗口不因选B改绑。
                Assert.Same(first, page.OpenToolEditor(a));
                a.ExecuteAction(Assert.Single(a.ActionDescriptors).Id);
                Assert.Single(owner.OwnedWindows.Cast<Window>());
                vm.Chart.Model.IsRunning = true;
                Assert.False(grid.IsEditingEnabled);
                Assert.True(grid.IsEnabled);
                Assert.True(Descendants(grid).OfType<TextBox>().Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "搜索属性").IsEnabled);
                Assert.False(editor.Session!.IsEditingEnabled);
                Assert.True(editor.ImagePanel.ImageViewport.IsEnabled);
                first.Close();
                Assert.True(workspace.Editor.IsSessionActive);
                Assert.Same(originalCard, workspace.Editor.CurrentCard);
                vm.Chart.Model.IsRunning = false;
                vm.Select(a);
                Assert.Empty(vm.PropertyGridVM.ExtraActions);
                a.OpenToolEditorCommand.Execute(null);
                var reopened = Assert.Single(owner.OwnedWindows.Cast<Window>());
                Assert.NotSame(first, reopened);
                Assert.Same(workspace.Editor, Assert.IsType<VisionToolEditorView>(reopened.Content).Session);
                Assert.Same(originalCard, workspace.Editor.CurrentCard);
                page.OpenToolEditor(b);
                Assert.Equal(2, owner.OwnedWindows.Count);
                vm.Select(b);
                vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, b)));
                Assert.Single(owner.OwnedWindows.Cast<Window>());
                Assert.Equal(2, creates); Assert.Null(vm.PropertyGridVM.TargetObject);
                vm.Select(a); Idle(page);
                Snapshot(page, "task-properties.png");
                Snapshot((FrameworkElement)reopened.Content, "blob-editor.png");
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() => Assert.Empty(owner.OwnedWindows.Cast<Window>()));
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() => owner?.Close());
        }
    }

    [Fact]
    public async Task QuickParameters_UseOriginalDto_RefreshBothWays_AndOnlyActualEditsMarkDirty()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        VisionFlowViewModel? restored = null;
        try
        {
            VisionToolNodeBase a = null!, b = null!;
            host.Invoke(() =>
            {
                var entry = OpenCvToolCatalog.Entries.Single(t => t.Name == "OpenCV/Blob");
                a = vm.Add(entry); b = vm.Add(entry);
                vm.SetImage(VisionImage.FromBuffer(16, 16, Vision.Core.Imaging.PixelFormat.Mono8, Enumerable.Repeat((byte)255, 256).ToArray()));
                vm.Select(a);
                Assert.Same(a, vm.PropertyGridVM.TargetObject);
                Assert.Equal(new[] { "MinGray", "MaxGray", "MinArea" }, vm.PropertyGridVM.Groups.Single(g => g.GroupName == "工具关键参数").Items.Select(i => i.PropertyName));
                vm.Chart.Model.IsModified = false;
                vm.Select(b); vm.Select(a);
                Assert.False(vm.Chart.Model.IsModified);
            });
            await host.Invoke(() => ((VisionToolInstanceWorkspace)a.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.NotNull(a.Result);
                Assert.False(vm.Chart.Model.IsModified); // 结果刷新不脏配方。
                var workspace = (VisionToolInstanceWorkspace)a.Session;
                var dto = Assert.IsType<Cv.BlobParameters>(a.Session.Parameters);
                var card = workspace.Editor.CurrentCard;
                var row = vm.PropertyGridVM.Groups.Single(g => g.GroupName == "工具关键参数").Items.Single(i => i.PropertyName == "MinGray");
                row.Value = 90;
                Assert.Equal(90, dto.MinGray);
                Assert.Equal(90, workspace.Editor.BlobEditor!.MinGray);
                Assert.Same(dto, a.Session.Parameters); Assert.Same(card, workspace.Editor.CurrentCard);
                Assert.True(vm.Chart.Model.IsModified); Assert.Null(a.Result); Assert.Null(card!.TableRows);
                vm.Chart.Model.IsModified = false;
                workspace.Editor.BlobEditor.MinGray = 120;
                Assert.Equal(120, row.Value);
                Assert.True(vm.Chart.Model.IsModified);
                vm.PropertyGridVM.SearchText = "节点名称"; // 隐藏关键参数组后编辑完整页，再显示不能漏掉后续真实编辑。
                workspace.Editor.BlobEditor.MinGray = 160;
                vm.PropertyGridVM.SearchText = "";
                vm.Chart.Model.IsModified = false;
                row.Value = 120;
                Assert.Equal(120, workspace.Editor.BlobEditor.MinGray);
                Assert.True(vm.Chart.Model.IsModified);
                vm.Select(b);
                workspace.Editor.BlobEditor.MinGray = 150; // A完整页的回调仍写A，不写当前选中的B。
                Assert.Equal(150, dto.MinGray); Assert.Equal(128, ((Cv.BlobParameters)b.Session.Parameters).MinGray);
                workspace.RoiEditor.AddRectangle(8, 8, 6, 6);
                vm.Chart.Model.IsModified = false;
                workspace.RoiEditor.SelectedX = 9;
                Assert.True(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsModified = false;
                workspace.RoiEditor.Selected = null;
                Assert.False(vm.Chart.Model.IsModified);
                var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "r0-acceptance");
                System.IO.Directory.CreateDirectory(folder);
                Assert.True(vm.Chart.SaveDocument(System.IO.Path.Combine(folder, "quick-parameters.flow")));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            restored = host.Invoke(VisionFlowToolboxTests.Create);
            host.Invoke(() =>
            {
                Assert.True(restored.Chart.LoadDocument(System.IO.Path.Combine(AppContext.BaseDirectory, "r0-acceptance", "quick-parameters.flow")));
                var copied = Assert.IsAssignableFrom<VisionToolNodeBase>(restored.Chart.Nodes[0].Model);
                Assert.Equal(150, Assert.IsType<Cv.BlobParameters>(copied.Session.Parameters).MinGray);
                Assert.Equal(9, Assert.Single(((VisionToolInstanceWorkspace)copied.Session).RoiEditor.Rois).Pose.X);
                Assert.Null(copied.Result);
                restored.Select(copied);
                Assert.Equal(150, restored.PropertyGridVM.Groups.Single(g => g.GroupName == "工具关键参数").Items.Single(i => i.PropertyName == "MinGray").Value);
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            if (restored != null) await host.Invoke(() => restored.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task AllCatalogEntries_UseExplicitExistingParameterNames_WithoutOpeningOrRunningSdk()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                foreach (var entry in vm.ToolEntries)
                {
                    var node = vm.Add(entry);
                    Assert.Same(node, vm.PropertyGridVM.TargetObject);
                    Assert.Null(node.Result);
                    var keys = vm.PropertyGridVM.Groups.Where(g => g.GroupName == "工具关键参数").SelectMany(g => g.AllItems).ToArray();
                    Assert.Equal(entry.NodeParameterNames, keys.Select(k => k.PropertyName));
                    Assert.All(keys, k => Assert.False(k.IsPortConfigurable));
                    Assert.DoesNotContain(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), i => i.PropertyName is "Session" or "Parameters" or "CurrentCard");
                }
                Assert.Equal(28, vm.Chart.Nodes.Count);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task RoutedDoubleClick_UsesSameLauncher_AndNeverCreatesSecondNode()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        Window owner = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                owner = new() { Content = page, Width = 1400, Height = 850, ShowInTaskbar = false, Left = -10000 };
                owner.Show();
                var node = vm.Add(OpenCvToolCatalog.Entries.First()); Idle(page);
                var nodeView = Descendants(page).OfType<FlowEditor.Controls.Views.NodeView>().Single();
                var title = Assert.IsAssignableFrom<UIElement>(nodeView.FindName("TitleBar"));
                var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Mouse.PreviewMouseDownEvent };
                typeof(MouseButtonEventArgs).GetProperty(nameof(click.ClickCount))!.SetValue(click, 2);
                title.RaiseEvent(click);
                Assert.True(click.Handled);
                Assert.Single(owner.OwnedWindows.Cast<Window>());
                Assert.Same(node, Assert.Single(owner.OwnedWindows.Cast<Window>()).DataContext);
                Assert.Single(vm.Chart.Nodes);
            });
        }
        finally { host.Invoke(() => owner?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task NativeNodeProperties_SelectSourcesById_KeepTargetScope_AndRejectLockedOrMissingSources()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var crop = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Crop"));
                var a = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                var b = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                vm.Select(a);
                var row = NodeRow(vm, "ImageSourceNodeId");
                Assert.Contains(row.EnumOptions!, o => Equals(o.Value, crop.Id) && o.DisplayName.Contains(crop.Name));
                row.Value = crop.Id;
                Assert.Equal(crop.Id, vm.SelectedImageSource);
                Assert.True(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsModified = false;
                crop.Name = "重复名称";
                Assert.Equal(crop.Id, row.Value);
                Assert.Contains(row.EnumOptions!, o => Equals(o.Value, crop.Id) && o.DisplayName.Contains("重复名称"));
                vm.Select(b);
                row.Value = ""; // 借用旧行也只能指向A，不能借全局SelectedNode误改B。
                Assert.Empty(vm.Chart.Connections);
                Assert.Equal("", vm.SelectedImageSource);
                vm.Select(a); row = NodeRow(vm, "ImageSourceNodeId");
                row.Value = crop.Id;
                vm.Chart.Model.IsModified = false;
                row.Value = "missing-source";
                Assert.Equal(crop.Id, row.Value);
                Assert.Contains("不存在", vm.StatusText);
                Assert.False(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsRunning = true;
                row.Value = "";
                Assert.Equal(crop.Id, row.Value);
                Assert.False(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsRunning = false;
                vm.Select(crop);
                Assert.DoesNotContain(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "FixtureSourceNodeId");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task KeyParameterVisibility_FollowsOriginalMode_FromNodeAndFullEditor()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/FindLine"));
                var workspace = (VisionToolInstanceWorkspace)node.Session;
                var dto = Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(node.Session.Parameters);
                var group = vm.PropertyGridVM.Groups.Single(g => g.GroupName == "工具关键参数");
                Assert.Contains(group.Items, r => r.PropertyName == "Threshold");
                var mode = NodeRow(vm, "SearchMode");
                mode.Value = Vision.Core.Geometry.LineSearchKind.EdgeBand;
                Assert.DoesNotContain(group.Items, r => r.PropertyName is "Threshold" or "CaliperCount");
                Assert.Contains(group.Items, r => r.PropertyName == "CannyLow");
                vm.PropertyGridVM.SearchText = "边缘阈值";
                Assert.DoesNotContain(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "Threshold");
                dto.SearchMode = Vision.Core.Geometry.LineSearchKind.Calipers;
                workspace.Editor.ParametersChanged();
                Assert.Contains(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "Threshold");
                vm.PropertyGridVM.SearchText = "";
                Assert.Same(dto, workspace.Editor.CurrentParameters);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task OriginalFlowContainer_CommandsAndTabs_OwnTheirVisionSessions_AndGroupLoadIsAtomic()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 900, ShowInTaskbar = false, Left = -10000 };
                window.Show(); Idle(page);
                Assert.Single(Descendants(page).OfType<FlowEditor.Controls.Views.FlowChartContainerView>());
                var bar = Assert.Single(Descendants(page).OfType<FlowEditor.Controls.Views.FlowCommandBarView>());
                Assert.Same(vm.Editor, bar.DataContext);
                var save = Descendants(bar).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "保存当前页面流程 (Ctrl+S)");
                Assert.Same(vm.Chart.SaveCommand, save.Command);
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => Equals(b.Content, "执行流程") || Equals(b.Content, "保存任务"));
            });
            await host.Invoke(() => vm.EditorReady);
            VisionToolNodeBase first = null!, second = null!;
            FlowEditor.Controls.ViewModels.FlowChartViewModel original = null!;
            host.Invoke(() =>
            {
                original = vm.Chart;
                first = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                vm.SetImage(VisionImage.FromBuffer(16, 16, Vision.Core.Imaging.PixelFormat.Mono8, Enumerable.Repeat((byte)255, 256).ToArray()));
                vm.Editor!.NewFlowCommand.Execute(null);
                Assert.NotSame(original, vm.Chart);
                original.Model.IsModified = false;
                second = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Threshold"));
                vm.SetImage(VisionImage.FromBuffer(16, 16, Vision.Core.Imaging.PixelFormat.Mono8, new byte[256]));
                NodeRow(vm, "MinGray").Value = 128;
                Assert.NotSame(first.Session.ConfiguredImage, second.Session.ConfiguredImage);
                Assert.False(original.Model.IsModified);
                Assert.True(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsRunning = true;
                Assert.False(vm.Editor.LoadAllCommand.CanExecute(null)); Assert.False(vm.Editor.SaveAllCommand.CanExecute(null));
                vm.Chart.Model.IsRunning = false;
            });
            await host.Invoke(() => vm.Editor!.RunAllCommand.ExecuteAsync(null));
            await host.Invoke(() => Task.WhenAll(vm.Editor!.Tabs.Select(t => t.RunCommand.ExecutionTask ?? Task.CompletedTask)));
            string path = "";
            host.Invoke(() =>
            {
                Assert.Single(Assert.IsType<Cv.BlobResult>(first.Result).Items);
                Assert.Equal(0, Assert.IsType<Vision.Tools.OpenCv.Processing.ThresholdResult>(second.Result).WhitePixelCount);
                var service = Prism.Ioc.ContainerLocator.Container.Resolve<FlowEditor.Controls.Services.FlowChartContainerDocumentService>();
                var document = service.BuildContainerDocument(vm.Editor!.Tabs);
                var configuration = Vision.Core.Tools.VisionToolConfiguration.FromState(document.Flows[1].Nodes[0].Properties);
                configuration.Version = 99;
                document.Flows[1].Nodes[0].Properties["Configuration"] = System.Text.Json.JsonSerializer.SerializeToElement(configuration, Vision.Core.Tools.VisionToolConfiguration.JsonOptions);
                path = System.IO.Path.Combine(AppContext.BaseDirectory, "r0-acceptance", "invalid.flows");
                Prism.Ioc.ContainerLocator.Container.Resolve<FlowEditor.Controls.Interfaces.IFlowDocumentStore>().Save(path, document);
            });
            await Assert.ThrowsAnyAsync<Exception>(() => host.Invoke(() => vm.Editor!.LoadFlowGroup(path)));
            host.Invoke(() =>
            {
                Assert.Equal(2, vm.Editor!.Tabs.Count);
                Assert.Same(original, vm.Editor.Tabs[0]);
                Assert.True(((VisionToolInstanceWorkspace)first.Session).Editor.IsSessionActive);
                var service = Prism.Ioc.ContainerLocator.Container.Resolve<FlowEditor.Controls.Services.FlowChartContainerDocumentService>();
                Prism.Ioc.ContainerLocator.Container.Resolve<FlowEditor.Controls.Interfaces.IFlowDocumentStore>().Save(path, service.BuildContainerDocument(vm.Editor.Tabs));
            });
            await host.Invoke(() => vm.Editor!.LoadFlowGroup(path));
            host.Invoke(() =>
            {
                Assert.Equal(2, vm.Editor!.Tabs.Count);
                Assert.False(((VisionToolInstanceWorkspace)first.Session).Editor.IsSessionActive);
                Assert.False(((VisionToolInstanceWorkspace)second.Session).Editor.IsSessionActive);
                Assert.All(vm.Editor.Tabs, tab => Assert.NotEmpty(tab.Model.DocumentProperties));
                Assert.All(vm.Editor.Tabs.SelectMany(t => t.Nodes), n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
                Assert.Equal(255, ((VisionToolNodeBase)vm.Editor.Tabs[0].Nodes[0].Model).Session.ConfiguredImage!.Pixels[0]);
                vm.Editor.ActiveTab = vm.Editor.Tabs[1];
                Assert.Same(vm.Editor.ActiveTab, vm.Chart);
                Assert.Equal(0, ((VisionToolNodeBase)vm.Chart.Nodes[0].Model).Session.ConfiguredImage!.Pixels[0]);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); host.Invoke(() => window?.Close()); }
    }

    [Fact]
    public async Task NativeContainer_ReloadAndDataContextChange_DoNotDisposeBorrowedTaskOrRebindOldWindow()
    {
        var a = host.Invoke(VisionFlowToolboxTests.Create); var b = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = a };
                window = new() { Content = page, Width = 1400, Height = 900, ShowInTaskbar = false, Left = -10000 };
                window.Show(); Idle(page);
                var first = a.Add(a.ToolEntries.First()); var editor = page.OpenToolEditor(first)!;
                var original = a.Editor;
                page.DataContext = b; Idle(page);
                Assert.False(editor.IsVisible);
                Assert.True(((VisionToolInstanceWorkspace)first.Session).Editor.IsSessionActive);
                Assert.Same(b.Editor, ((FlowEditor.Controls.Views.FlowChartContainerView)page.FindName("FlowHost")).DataContext);
                Assert.Empty(b.Chart.Nodes);
                page.DataContext = a; Idle(page);
                Assert.Same(original, a.Editor);
                Assert.Same(first, Assert.Single(a.Chart.Nodes).Model);
                window.Content = null; Idle(page);
                Assert.True(((VisionToolInstanceWorkspace)first.Session).Editor.IsSessionActive);
                window.Content = page; Idle(page);
                Assert.Same(original, a.Editor);
                Assert.Same(first.Session.Parameters, a.SelectedEditor!.CurrentParameters);
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => a.DisposeAsync().AsTask()); await host.Invoke(() => b.DisposeAsync().AsTask());
        }
    }

    private static PropertyItemViewModel NodeRow(VisionFlowViewModel vm, string property) =>
        vm.PropertyGridVM.Groups.SelectMany(g => g.Items).Single(r => r.PropertyName == property);

    private static void Idle(FrameworkElement element) { element.UpdateLayout(); element.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Snapshot(FrameworkElement element, string name)
    {
        Idle(element);
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "r0-acceptance"); System.IO.Directory.CreateDirectory(folder);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(System.IO.Path.Combine(folder, name)); encoder.Save(stream);
    }
}
