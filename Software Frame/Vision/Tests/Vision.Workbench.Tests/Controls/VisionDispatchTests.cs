using System.IO;
using System.Text.Json.Nodes;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Controls.Views;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.Halcon.Measurement;


namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionDispatchTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task RuleWindow_RejectsRunningGraph_WithoutLeavingRegisteredWindow()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try { host.Invoke(() =>
        {
            var node = new SwitchNode(); vm.Chart.LoadNodeModel(node); vm.Chart.Model.IsRunning = true;
            var count = Application.Current.Windows.Count;
            Assert.Throws<InvalidOperationException>(() => new DispatchRuleEditorWindow(node));
            Assert.Equal(count, Application.Current.Windows.Count);
            node.EditDispatchRules(); // 原UI启动器在运行锁时应提示返回，不抛入Dispatcher。
            Assert.Equal(count, Application.Current.Windows.Count); Assert.Contains("执行中", vm.Chart.Model.StatusText);
            Assert.Throws<InvalidOperationException>(() => node.ApplyRuleConfiguration([("true", "OK")], true, true));
            vm.Chart.Model.IsRunning = false;
        }); }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task OriginalToolbox_ContainsRecommendedDispatch_WithStrictFreshInput_AndPreservesVisionApi()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
            });
            await host.Invoke(() => vm.EditorReady);
            host.Invoke(() =>
            {
                var dispatch = Palette(vm); Assert.Equal("条件分发", dispatch.DisplayName);
                Assert.Same(vm.ToolboxCategories, vm.Editor!.ToolboxCategories);
                var toolbox = Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<FlowToolboxView>());
                Assert.NotNull(toolbox.AddNodeCommand); Assert.True(toolbox.AddNodeCommand.CanExecute(dispatch));
                toolbox.AddNodeCommand.Execute(dispatch);
                var first = Assert.IsType<SwitchNode>(Assert.Single(vm.Chart.Nodes).Model);
                Assert.True(first.RequireInputConnection); Assert.Equal(new SwitchNode().RulesText, first.RulesText);
                Assert.Same(first, vm.PropertyGridVM.TargetObject); Assert.Null(vm.SelectedEditor);
                Assert.Null(vm.AddPaletteAt(dispatch)); Assert.Single(vm.Chart.Nodes); // 旧VisionToolNode返回契约不变，不暗中添加。
                var second = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(dispatch, new Point(2400, 1700)));
                Assert.NotSame(first, second); Assert.Equal(2400, second.X); Assert.True(vm.Chart.Model.CanvasWidth > second.X);
                vm.Chart.Model.IsRunning = true;
                Assert.False(toolbox.AddNodeCommand.CanExecute(dispatch)); Assert.Null(vm.AddNodePaletteAt(dispatch));
                Assert.Throws<InvalidOperationException>(() => second.RequireInputConnection = false);
                vm.Chart.Model.IsRunning = false;
                Assert.Equal(2, vm.Chart.Nodes.Count);
                vm.ToolboxSearchText = "条件分发"; Assert.Single(Assert.Single(vm.ToolboxCategories).Items);
                vm.ToolboxSearchText = "条件分支"; Assert.Empty(vm.ToolboxCategories);
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealTolerance_OriginalPaletteAndRuleDialog_DispatchesOnlyMatchedBranch_AndReopens(bool accepted)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase tolerance = null!, yes = null!, no = null!; SwitchNode dispatch = null!;
        Window window = null!; var path = Artifact($"dispatch-{accepted}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1600, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                var (_, _, tol, legacy, ok, ng) = VisionToleranceTests.Setup(vm, accepted);
                tolerance = tol; yes = ok; no = ng;
                foreach (var edge in vm.Chart.Connections.Where(e => e.SourceNodeId == legacy.Id || e.TargetNodeId == legacy.Id).ToArray())
                    vm.Chart.Connections.Remove(edge);
                vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, legacy)));
                vm.AddPaletteCommand.Execute(Palette(vm));
                dispatch = Assert.IsType<SwitchNode>(vm.Chart.SelectedNode!.Model);
                dispatch.X = 300; dispatch.Y = 310;
                Assert.Empty(vm.Chart.Nodes.Select(n => n.Model).OfType<ConditionNode>());
                OpenRules(dispatch, editor =>
                {
                    editor.Rules.Clear(); editor.Rules.Add(new() { Match = "true", Branch = "OK" }); editor.Rules.Add(new() { Match = "false", Branch = "NG" });
                    Snapshot(editor, "original-rule-editor.png"); Click(editor, "保存");
                });
                Wire(vm, tolerance, "Next_Out", dispatch, "Next_IN");
                Wire(vm, tolerance, "布尔结果", dispatch, "输入值");
                Wire(vm, dispatch, "OK", yes, "Next_IN"); Wire(vm, dispatch, "NG", no, "Next_IN");
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(tolerance, dispatch, yes, no, accepted); Assert.False(vm.Chart.Model.IsModified);
                Assert.True(vm.Chart.SaveDocument(path));
                Snapshot((FrameworkElement)window.Content, $"dispatch-{accepted}.png"); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(reopened.Chart.LoadDocument(path));
                dispatch = reopened.Chart.Nodes.Select(n => n.Model).OfType<SwitchNode>().Single();
                tolerance = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Tolerance");
                yes = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Name == "OK分支");
                no = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Name == "NG分支");
                Assert.True(dispatch.RequireInputConnection); Assert.Equal("true=OK\nfalse=NG", dispatch.RulesText);
                Assert.Null(tolerance.BooleanOutput);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(tolerance, dispatch, yes, no, accepted);
                var input = dispatch.InputPorts.Single(p => p.PropertyName == nameof(SwitchNode.InputValue));
                reopened.Chart.Connections.Remove(reopened.Chart.Connections.Single(e => e.TargetNodeId == dispatch.Id && e.TargetPortId == input.Id));
                dispatch.InputValue = accepted; // 断线后即使仍有上轮/常量也不能走分支。
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Contains("缺少输入连接", dispatch.LastMatchedBranch); Assert.Null(yes.Result); Assert.Null(no.Result);
                Wire(reopened, tolerance, "布尔结果", dispatch, "输入值");
                dispatch.RulesText = "not-a-bool=OUT1";
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Contains("未命中", dispatch.LastMatchedBranch); Assert.Null(yes.Result); Assert.Null(no.Result);
                dispatch.RulesText = "true=OK\nfalse=NG"; tolerance.BooleanOutputField = "";
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(tolerance.BooleanOutput); Assert.Null(yes.Result); Assert.Null(no.Result); });
        }
        finally
        {
            host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task OriginalRuleEditor_CancelInvalidSaveAndSelectionSwitch_DoNotWriteWrongNode_AndRunLocksSave()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var a = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(Palette(vm)));
                var b = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(Palette(vm)));
                var original = a.RulesText; vm.Chart.Model.IsModified = false;
                OpenRules(a, editor =>
                {
                    editor.Rules[0].Match = "true"; Click(editor, "取消");
                });
                Assert.Equal(original, a.RulesText); Assert.False(vm.Chart.Model.IsModified);
                OpenRules(a, editor =>
                {
                    editor.Rules[0].Match = "true"; editor.Rules[1].Match = "true"; Click(editor, "保存");
                    Assert.True(editor.IsVisible); Assert.Contains("重复", ((TextBlock)editor.FindName("ValidationText")).Text);
                    Assert.Equal(original, a.RulesText); Click(editor, "取消");
                });
                OpenRules(a, editor =>
                {
                    vm.Chart.SelectedNode = vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, b));
                    editor.Rules.Clear(); editor.Rules.Add(new() { Match = "true", Branch = "PASS" }); editor.Rules.Add(new() { Match = "false", Branch = "FAIL" });
                    Click(editor, "保存");
                });
                Assert.Equal("true=PASS\nfalse=FAIL", a.RulesText); Assert.Equal(original, b.RulesText);
                Assert.True(vm.Chart.Model.IsModified);
                OpenRules(a, editor =>
                {
                    var before = a.RulesText; editor.Rules[0].Branch = "OTHER"; vm.Chart.Model.IsRunning = true;
                    try
                    {
                        Click(editor, "保存"); Assert.True(editor.IsVisible); Assert.Equal(before, a.RulesText);
                        Assert.Contains("执行中", ((TextBlock)editor.FindName("ValidationText")).Text);
                    }
                    finally { vm.Chart.Model.IsRunning = false; Click(editor, "取消"); }
                });
            });
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacySwitchWithoutRequiredFlag_RetainsConstantSemantics_WhileNewPaletteIsStrict()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Artifact("legacy-switch-constant.flow"); SwitchNode restored = null!;
        try
        {
            host.Invoke(() =>
            {
                var legacy = new SwitchNode { InputValue = true, RulesText = "true=YES\nfalse=NO" };
                vm.Chart.LoadNodeModel(legacy); Assert.True(vm.Chart.SaveDocument(path));
                var root = JsonNode.Parse(File.ReadAllText(path))!;
                foreach (var node in root["nodes"]!.AsArray()) node!["properties"]!.AsObject().Remove(nameof(SwitchNode.RequireInputConnection));
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(reopened.Chart.LoadDocument(path));
                restored = reopened.Chart.Nodes.Select(n => n.Model).OfType<SwitchNode>().Single();
                Assert.False(restored.RequireInputConnection);
                var current = Assert.IsType<SwitchNode>(reopened.AddNodePaletteAt(Palette(reopened)));
                Assert.True(current.RequireInputConnection);
            });
            await host.Invoke(() => restored.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Assert.Equal("YES", restored.LastMatchedBranch));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static NodePaletteItem Palette(VisionFlowViewModel vm) =>
        Assert.Single(vm.ToolboxCategories.Where(c => c.Name == "流程控制").SelectMany(c => c.Items), item => item.DisplayName == "条件分发");
    private static void OpenRules(SwitchNode node, Action<DispatchRuleEditorWindow> interact)
    {
        Exception? failure = null;
        var operation = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var editor = Application.Current.Windows.OfType<DispatchRuleEditorWindow>().Single();
            try { interact(editor); }
            catch (Exception error) { failure = error; editor.Close(); }
        }));
        try { node.EditDispatchRules(); }
        finally { if (operation.Status == DispatcherOperationStatus.Pending) operation.Abort(); }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static void Click(DispatchRuleEditorWindow editor, string text) =>
        Assert.Single(VisionFlowR0Tests.Descendants(editor).OfType<Button>(), b => Equals(b.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wire(VisionFlowViewModel vm, FlowEditor.Controls.Nodes.NodeModel source, string output, FlowEditor.Controls.Nodes.NodeModel target, string input) =>
        vm.Chart.Connections.Add(new()
        {
            SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == output).Id,
            TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == input).Id
        });
    private static void Check(VisionToolNodeBase tolerance, SwitchNode dispatch, VisionToolNodeBase yes, VisionToolNodeBase no, bool accepted)
    {
        Assert.Equal(accepted, Assert.IsType<ToleranceResult>(tolerance.Result).IsWithinTolerance);
        Assert.Equal(accepted ? "OK" : "NG", dispatch.LastMatchedBranch);
        Assert.Equal(accepted ? 5 : 13, Assert.IsType<GeometryResult>((accepted ? yes : no).Result).Distance);
        Assert.Null((accepted ? no : yes).Result);
    }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "dispatch-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
