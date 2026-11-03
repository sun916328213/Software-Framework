#nullable enable
using System.ComponentModel;
using System.Windows;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Controls.ViewModels;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.FlowNode;
using WPFCustomControls.Workbench;

namespace Vision.Controls.ViewModels;

/// <summary>组合通用流程画布和借用编辑器；不建执行引擎、不管理视觉文档格式。</summary>
public partial class VisionFlowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly VisionBrowserCatalog _catalog;
    // 仅投影原目录元数据；浏览/搜索不会创建工具或加载SDK。
    private readonly Dictionary<NodePaletteItem, ToolEntry> _palette = [];
    private readonly FlowEditor.Controls.Services.ToolboxService _toolbox;
    public string ToolboxSearchText
    {
        get => _toolbox.SearchText;
        set { _toolbox.SearchText = value; OnPropertyChanged(); }
    }
    public IReadOnlyList<ToolboxCategory> ToolboxCategories => _toolbox.Categories;
    // 移出画布的实例仍由流程拥有：不能在视图/节点投影卸载时释放正在运行的工具。
    private readonly HashSet<VisionToolNodeBase> _owned = [];
    private readonly HashSet<Task> _retiring = [];
    private readonly List<Exception> _retirementErrors = [];
    private VisionImage? _image;
    private Task? _dispose;
    private bool _closing;
    private readonly bool _documentReady;
    private readonly HashSet<FlowChartViewModel> _graphs = [];
    public FlowChartViewModel Chart { get; private set; }
    public FlowChartContainerViewModel? Editor { get; private set; }
    public Task EditorReady { get; private set; } = Task.CompletedTask;
    public WPFCustomControls.PropertyGrid.Core.ViewModels.PropertyGridHostViewModel PropertyGridVM { get; private set; }
    public bool Owns(VisionToolNodeBase node) => !_closing && _owned.Contains(node);
    /// <summary>接入与FlowNodes工作台相同的原生容器；旧Chart仍是首次活动页，不新建执行/文件命令。</summary>
    public void AttachEditor(FlowChartContainerViewModel editor)
    {
        if (Editor != null) return;
        PropertyGridVM.PropertyChanged -= PropertyGridChanged;
        PropertyGridVM.TargetObject = null;
        PropertyGridVM = editor.PropertyGridVM;
        PropertyGridVM.PropertyChanged += PropertyGridChanged;
        editor.ReplaceWithFlow(Chart);
        Editor = editor;
        Editor.PropertyChanged += EditorChanged;
        Editor.Tabs.CollectionChanged += EditorTabsChanged;
        EditorReady = editor.UseToolboxAsync(_toolbox);
        OnPropertyChanged(nameof(Editor)); OnPropertyChanged(nameof(PropertyGridVM));
        RefreshImageSources();
    }
    private void EditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FlowChartContainerViewModel.ActiveTab) || Editor?.ActiveTab is not { } active) return;
        Chart = active;
        _image = VisionTaskDocumentStore.GetInput(Chart.Model.DocumentProperties) ??
            Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().FirstOrDefault()?.Session.FlowInputImage;
        SelectedEditor = Chart.SelectedNode?.Model is VisionToolNodeBase node && node.Session is VisionToolInstanceWorkspace workspace ? workspace.Editor : null;
        OnPropertyChanged(nameof(Chart)); OnPropertyChanged(nameof(StatusText));
        RefreshImageSources(); RefreshLocks();
    }
    private void ObserveGraph(FlowChartViewModel graph, bool attach)
    {
        if (attach)
        {
            if (!_graphs.Add(graph)) return;
            graph.PropertyChanged += ChartChanged; graph.Nodes.CollectionChanged += NodesChanged;
            graph.Connections.CollectionChanged += ConnectionsChanged; graph.Model.PropertyChanged += ModelChanged;
        }
        else
        {
            _graphs.Remove(graph);
            graph.PropertyChanged -= ChartChanged; graph.Nodes.CollectionChanged -= NodesChanged;
            graph.Connections.CollectionChanged -= ConnectionsChanged; graph.Model.PropertyChanged -= ModelChanged;
        }
    }
    private void EditorTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var live = Editor!.Tabs.ToHashSet();
        foreach (var graph in _graphs.Except(live).ToArray()) ObserveGraph(graph, false);
        foreach (var graph in live) ObserveGraph(graph, true);
        NodesChanged(null, new(NotifyCollectionChangedAction.Reset));
    }
    public event Action<VisionToolNodeBase>? EditorRequested;
    public event Action<VisionToolNodeBase>? EditorClosing;
    private void RequestEditor(VisionToolNodeBase node) { if (!_closing && _owned.Contains(node)) EditorRequested?.Invoke(node); }
    private void CloseEditor(VisionToolNodeBase node)
    {
        try { EditorClosing?.Invoke(node); }
        catch (Exception error) { _retirementErrors.Add(error); Chart.Model.StatusText = $"编辑窗口关闭失败：{error.Message}"; }
    }
    public IReadOnlyList<ToolEntry> ToolEntries => _catalog.Tools;
    [ObservableProperty] private ToolEntry? _selectedEntry;
    [ObservableProperty] private VisionToolEditorSession? _selectedEditor;
    public string StatusText => Chart.Model.StatusText;
    public bool IsEditingEnabled => CanModify();

    public VisionFlowViewModel(FlowChartViewModel chart, VisionBrowserCatalog catalog) : this(chart, catalog, false) { }

    public VisionFlowViewModel(FlowChartViewModel chart, VisionBrowserCatalog catalog, bool documentReady)
        : this(chart, catalog, documentReady, VisionFlowRegistration.CreateToolbox(catalog)) { }

    public VisionFlowViewModel(FlowChartViewModel chart, VisionBrowserCatalog catalog, bool documentReady,
        FlowEditor.Controls.Services.ToolboxService toolbox)
    {
        PropertyGridVM = new(new VisionNodePropertyGridBuilder(
            node => catalog.Tools.SingleOrDefault(t => t.Name == node.ToolIdentity)?.NodeParameterNames ?? [],
            node =>
            {
                if (!_refreshingProperties && CanModify() && node.Session is VisionToolInstanceWorkspace workspace)
                    workspace.RefreshParameterEditors();
            }), new WPFCustomControls.PropertyGrid.Services.PortDialogService());
        _documentReady = documentReady;
        Chart = chart ?? throw new ArgumentNullException(nameof(chart));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _toolbox = toolbox ?? throw new ArgumentNullException(nameof(toolbox));
        _toolbox.Refresh();
        foreach (var item in _toolbox.Items)
            if (catalog.Tools.SingleOrDefault(entry => entry.Name == item.ToolId) is { } entry)
                _palette.Add(item, entry);
        SelectedEntry = catalog.Tools.FirstOrDefault(t => t.IsDefault) ?? catalog.Tools.FirstOrDefault();
        Chart.Model.SupportsDocumentEditing = documentReady; // 仅在宿主已装配严格配置物化适配时开放文档能力。
        PropertyGridVM.PropertyChanged += PropertyGridChanged;
        ObserveGraph(Chart, true);
    }
    private bool CanModify() => !_closing && _graphs.All(g => !g.Model.IsRunning && !g.Model.IsLoopRunning) && _retiring.Count == 0 && _owned.All(n => !n.Session.IsBusy);
    private bool CanAdd() => CanModify() && SelectedEntry != null;
    private bool CanRun() => CanModify() && Chart.RunCommand.CanExecute(null);
    partial void OnSelectedEntryChanged(ToolEntry? value) => AddToolCommand.NotifyCanExecuteChanged();
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddTool()
    {
        try { Add(SelectedEntry!); }
        catch (Exception error) { Chart.Model.StatusText = $"添加失败：{error.Message}"; }
    }
    private bool CanAddPalette(NodePaletteItem? item) => CanModify() && item != null && _toolbox.Items.Contains(item);
    [RelayCommand(CanExecute = nameof(CanAddPalette))]
    private void AddPalette(NodePaletteItem? item) => AddNodePaletteAt(item);
    public VisionToolNodeBase? AddPaletteAt(NodePaletteItem? item, Point? position = null)
    {
        if (!CanAddPalette(item) || !_palette.ContainsKey(item!)) return null;
        try
        {
            return Add(_palette[item!], position);
        }
        catch (Exception error) { Chart.Model.StatusText = $"添加失败：{error.Message}"; return null; }
    }
    /// <summary>原工具箱/拖放的NodeModel入口；旧AddPaletteAt仍保留仅视觉返回契约。</summary>
    public FlowEditor.Controls.Nodes.NodeModel? AddNodePaletteAt(NodePaletteItem? item, Point? position = null)
    {
        if (!CanAddPalette(item)) return null;
        if (_palette.ContainsKey(item!)) return AddPaletteAt(item, position);
        try
        {
            var node = item!.CreateNode();
            if (node is FlowEditor.Controls.Nodes.FlowControl.SwitchNode dispatch)
            {
                dispatch.RequireInputConnection = true;
            }
            var name = node.Name; var suffix = 1;
            while (Chart.Nodes.Any(n => n.Name == $"{name} {suffix}")) suffix++;
            node.Name = $"{name} {suffix}";
            node.X = 60; node.Y = 50 + Chart.Nodes.Count * 150;
            if (position is { } point) { node.X = Math.Max(0, point.X); node.Y = Math.Max(0, point.Y); }
            var projection = Chart.LoadNodeModel(node);
            Chart.Model.IsModified = true; Chart.SelectedNode = projection; RefreshLocks();
            return node;
        }
        catch (Exception error) { Chart.Model.StatusText = $"添加失败：{error.Message}"; return null; }
    }
    private VisionToolNodeBase CreateToolNode(ToolEntry entry)
    {
        if (!CanModify()) throw new InvalidOperationException("流程或工具正在执行，或会话已关闭。");
        if (!_catalog.Tools.Contains(entry)) throw new ArgumentException("工具不属于当前目录。", nameof(entry));
        var item = _palette.Single(pair => pair.Value == entry).Key;
        var node = (VisionToolNodeBase)item.CreateNode();
        try
        {
            var suffix = 1;
            while (Chart.Nodes.Select(n => n.Model).Any(n => n.Name == $"{entry.Name} {suffix}")) suffix++;
            node.Name = $"{entry.Name} {suffix}";
            node.X = 60; node.Y = 50 + Chart.Nodes.Count * 150;
            if (_image != null) SetTaskInput(node, _image);
            return node;
        }
        catch { node.Dispose(); throw; }
    }
    public VisionToolNodeBase Add(ToolEntry entry) => Add(entry, null);
    private VisionToolNodeBase Add(ToolEntry entry, Point? position)
    {
        var node = CreateToolNode(entry);
        if (position is { } point) { node.X = Math.Max(0, point.X); node.Y = Math.Max(0, point.Y); }
        try
        {
            _owned.Add(node);
            ObserveSessionEvents(node, true);
            ObserveNodeEvents(node, true);
            var projection = Chart.LoadNodeModel(node);
            Chart.Model.IsModified = true;
            Chart.SelectedNode = projection;
            RefreshLocks();
            return node;
        }
        catch
        {
            _owned.Remove(node);
            ObserveSessionEvents(node, false);
            ObserveNodeEvents(node, false);
            node.Dispose(); throw;
        }
    }
    public void Select(VisionToolNodeBase node)
    {
        var projection = Chart.Nodes.FirstOrDefault(n => ReferenceEquals(n.Model, node));
        if (projection != null) Chart.SelectedNode = projection;
    }
    private static void SetTaskInput(VisionToolNodeBase node, VisionImage image)
    {
        if (node.Session is VisionToolInstanceWorkspace workspace) workspace.SetTaskImage(image);
        else node.Session.SetImage(image);
    }
    public void SetImage(VisionImage image)
    {
        if (!CanModify()) throw new InvalidOperationException("执行中或已关闭，不能更换任务输入。");
        ArgumentNullException.ThrowIfNull(image);
        foreach (var node in Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>()) SetTaskInput(node, image);
        _image = image;
        VisionTaskDocumentStore.SetInput(Chart.Model, image);
    }
    private void LoadTaskImage(VisionToolNodeBase node)
    {
        if (!CanModify()) return;
        var graph = _graphs.SingleOrDefault(g => g.Nodes.Any(n => ReferenceEquals(n.Model, node)));
        if (graph == null) return;
        try
        {
            if (_catalog.LoadImage() is not { } image) return;
            foreach (var tool in graph.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>()) SetTaskInput(tool, image);
            VisionTaskDocumentStore.SetInput(graph.Model, image); graph.Model.IsModified = true;
            if (ReferenceEquals(graph, Chart)) _image = image;
        }
        catch (Exception error) { graph.Model.StatusText = $"加载任务图像失败：{error.Message}"; }
    }
    [RelayCommand(CanExecute = nameof(CanModify))]
    private void LoadImage()
    {
        try { if (_catalog.LoadImage() is { } image) SetImage(image); }
        catch (Exception error) { Chart.Model.StatusText = $"加载失败：{error.Message}"; }
    }
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunFlowAsync()
    {
        foreach (var node in _owned) node.Session.ClearResult();
        await Chart.RunCommand.ExecuteAsync(null); // 原执行器/原停止与调试；没有第二份连接或调度逻辑。
    }
    private void ChartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Chart) && e.PropertyName == nameof(Chart.SelectedNode))
        {
            PropertyGridVM.TargetObject = Chart.SelectedNode?.Model;
            var selected = Chart.SelectedNode?.Model as VisionToolNodeBase;
            foreach (var node in Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>()) node.IsEditorSelected = ReferenceEquals(node, selected);
            SelectedEditor = selected?.Session is VisionToolInstanceWorkspace instance ? instance.Editor : null;
            RefreshImageSources();
        }
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Chart.Model) && e.PropertyName == nameof(Chart.Model.DocumentProperties))
            _image = VisionTaskDocumentStore.GetInput(Chart.Model.DocumentProperties) ?? _owned.FirstOrDefault()?.Session.FlowInputImage;
        if (e.PropertyName == nameof(Chart.Model.StatusText)) OnPropertyChanged(nameof(StatusText));
        if (e.PropertyName is nameof(Chart.Model.IsRunning) or nameof(Chart.Model.IsLoopRunning))
        {
            if (e.PropertyName == nameof(Chart.Model.IsRunning) && sender is FlowEditor.Controls.Models.FlowChartModel { IsRunning: true } model)
                foreach (var node in _graphs.Where(g => ReferenceEquals(g.Model, model)).SelectMany(g => g.Nodes).Select(n => n.Model).OfType<VisionToolNodeBase>())
                    node.Session.ClearResult();
            RefreshLocks();
        }
    }
    private void ObserveNodeEvents(VisionToolNodeBase node, bool observe)
    {
        if (observe)
        {
            node.EditorRequested += RequestEditor;
            node.PropertyChanged += NodeMetadataChanged;
            node.TaskImageRequested += LoadTaskImage;
        }
        else
        {
            node.EditorRequested -= RequestEditor;
            node.PropertyChanged -= NodeMetadataChanged;
            node.TaskImageRequested -= LoadTaskImage;
        }
    }
    // 与节点编辑事件分组，保留移除时“退订节点事件→关窗→退订会话事件”的边界。
    private void ObserveSessionEvents(VisionToolNodeBase node, bool observe)
    {
        if (observe)
        {
            node.Session.StateChanged += InstanceChanged;
            if (node.Session is VisionToolInstanceWorkspace instance)
            { instance.ConfigurationChanged += ConfigurationChanged; instance.ConfigurationImported += ConfigurationImported; }
        }
        else
        {
            node.Session.StateChanged -= InstanceChanged;
            if (node.Session is VisionToolInstanceWorkspace instance)
            { instance.ConfigurationChanged -= ConfigurationChanged; instance.ConfigurationImported -= ConfigurationImported; }
        }
    }
    private void NodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_closing) return;
        var live = _graphs.SelectMany(g => g.Nodes).Select(n => n.Model).OfType<VisionToolNodeBase>().ToHashSet();
        foreach (var node in live)
            if (_owned.Add(node)) { ObserveSessionEvents(node, true); ObserveNodeEvents(node, true); }
        foreach (var node in _owned.Where(n => !live.Contains(n)).ToArray())
        {
            _owned.Remove(node);
            ObserveNodeEvents(node, false);
            CloseEditor(node);
            ObserveSessionEvents(node, false);
            if (node.Session is VisionToolInstanceWorkspace workspace && ReferenceEquals(SelectedEditor, workspace.Editor))
                SelectedEditor = null;
            var cleanup = node.DisposeAsync().AsTask();
            _retiring.Add(cleanup);
            _ = ObserveRetirementAsync(cleanup);
        }
        if (Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().FirstOrDefault(n => n.IsEditorSelected) is { } selected)
            Chart.SelectedNode = Chart.Nodes.Single(n => ReferenceEquals(n.Model, selected));
        if (Chart.SelectedNode != null && !Chart.Nodes.Contains(Chart.SelectedNode)) Chart.SelectedNode = null;
        if (Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().FirstOrDefault() is { } first)
            _image = VisionTaskDocumentStore.GetInput(Chart.Model.DocumentProperties) ?? first.Session.FlowInputImage;
        RefreshImageSources();
        RefreshLocks();
    }
    private async Task ObserveRetirementAsync(Task cleanup)
    {
        try { await cleanup; }
        catch (Exception error)
        {
            _retirementErrors.Add(error);
            Chart.Model.StatusText = $"节点退役清理失败：{error}";
        }
        finally { _retiring.Remove(cleanup); RefreshLocks(); }
    }
    private bool _refreshingProperties;
    private void PropertyGridChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PropertyGridVM.SearchText)) RefreshPropertyValues();
    }
    private void RefreshPropertyValues()
    {
        _refreshingProperties = true;
        try { PropertyGridVM.RefreshValues(); }
        finally { _refreshingProperties = false; }
    }
    private void ConfigurationImported(object? sender, EventArgs e)
    {
        foreach (var node in _owned.Where(n => ReferenceEquals(n.Session, sender))) node.ForgetFixtureReference();
    }
    private void ConfigurationChanged(object? sender, EventArgs e)
    {
        if (_closing || _refreshingProperties) return;
        foreach (var graph in _graphs.Where(g => g.Nodes.Any(n => n.Model is VisionToolNodeBase tool && ReferenceEquals(tool.Session, sender)))) graph.Model.IsModified = true;
        if (Chart.SelectedNode?.Model is VisionToolNodeBase node && ReferenceEquals(node.Session, sender))
        {
            RefreshPropertyValues();
        }
    }
    private void InstanceChanged(object? sender, EventArgs e) => RefreshLocks();
    private void RefreshLocks()
    {
        var locked = !CanModify();
        foreach (var graph in _graphs) graph.Model.SupportsDocumentEditing = _documentReady && !locked;
        foreach (var node in _owned) { node.Session.SetReadOnly(locked); node.SetPropertyEditingEnabled(!locked); }
        OnPropertyChanged(nameof(IsEditingEnabled)); OnPropertyChanged(nameof(CanFollowFixture));
        CaptureFixtureReferenceCommand.NotifyCanExecuteChanged();
        AddToolCommand.NotifyCanExecuteChanged(); AddPaletteCommand.NotifyCanExecuteChanged(); LoadImageCommand.NotifyCanExecuteChanged(); RunFlowCommand.NotifyCanExecuteChanged();
    }
    public sealed record ImageSourceOption(string NodeId, string Name);
    public IReadOnlyList<ImageSourceOption> ImageSources { get; private set; } = [new("", "任务输入图像")];
    public string? SelectedImageSource
    {
        get
        {
            if (Chart.SelectedNode?.Model is not VisionToolNodeBase node) return "";
            var input = node.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.Image));
            return Chart.Connections.FirstOrDefault(c => c.TargetNodeId == node.Id && c.TargetPortId == input.Id)?.SourceNodeId ?? "";
        }
        set
        {
            if (value == null || !CanModify() || Chart.SelectedNode?.Model is not VisionToolNodeBase node) return;
            try
            {
                var source = value.Length == 0 ? null : Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Id == value);
                VisionImageBinding.Select(node, source);
                Chart.Model.IsModified = true;
            }
            catch (Exception error) { Chart.Model.StatusText = $"图像来源绑定失败：{error.Message}"; }
            OnPropertyChanged(nameof(SelectedImageSource));
        }
    }
    private void ConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedImageSource)); OnPropertyChanged(nameof(SelectedFixtureSource)); CaptureFixtureReferenceCommand.NotifyCanExecuteChanged();
    }
    private void NodeMetadataChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisionToolNodeBase.Name))
        {
            foreach (var graph in _graphs.Where(g => g.Nodes.Any(n => ReferenceEquals(n.Model, sender)))) graph.Model.IsModified = true;
            RefreshImageSources();
        }
    }
    private void RefreshImageSources()
    {
        foreach (var node in _owned) node.RefreshPropertySources();
        ImageSources = new[] { new ImageSourceOption("", "任务输入图像") }.Concat(Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>()
            .Where(n => n.Session.SupportsOutputImage && !ReferenceEquals(n, Chart.SelectedNode?.Model))
            .Select(n => new ImageSourceOption(n.Id, n.Name + " / 输出图像"))).ToArray();
        FixtureSources = new[] { new ImageSourceOption("", "不使用定位来源") }.Concat(Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>()
            .Where(n => n.Session.SupportsFixtureOutput && !ReferenceEquals(n, Chart.SelectedNode?.Model))
            .Select(n => new ImageSourceOption(n.Id, n.Name + " / 定位候选"))).ToArray();
        OnPropertyChanged(nameof(ImageSources)); OnPropertyChanged(nameof(SelectedImageSource));
        OnPropertyChanged(nameof(FixtureSources)); OnPropertyChanged(nameof(SelectedFixtureSource));
        OnPropertyChanged(nameof(CanFollowFixture)); OnPropertyChanged(nameof(SelectedFixtureIndex));
        OnPropertyChanged(nameof(ReferenceX)); OnPropertyChanged(nameof(ReferenceY)); OnPropertyChanged(nameof(ReferenceAngle));
        CaptureFixtureReferenceCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<ImageSourceOption> FixtureSources { get; private set; } = [new("", "不使用定位来源")];
    private VisionToolNodeBase? FixtureTarget => Chart.SelectedNode?.Model as VisionToolNodeBase;
    public bool CanFollowFixture => IsEditingEnabled && FixtureTarget?.Session.SupportsFixture == true;
    public string? SelectedFixtureSource
    {
        get
        {
            if (FixtureTarget is not { } node || !node.Session.SupportsFixture) return "";
            var input = node.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundFixtures));
            return Chart.Connections.FirstOrDefault(c => c.TargetNodeId == node.Id && c.TargetPortId == input.Id)?.SourceNodeId ?? "";
        }
        set
        {
            if (value == null || !CanFollowFixture || FixtureTarget is not { } target) return;
            try
            {
                var source = value.Length == 0 ? null : Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Id == value);
                VisionFixtureBinding.Select(target, source); Chart.Model.IsModified = true;
            }
            catch (Exception error) { Chart.Model.StatusText = $"定位来源绑定失败：{error.Message}"; }
            OnPropertyChanged(nameof(SelectedFixtureSource)); CaptureFixtureReferenceCommand.NotifyCanExecuteChanged();
        }
    }
    public int SelectedFixtureIndex
    {
        get => FixtureTarget?.FixtureIndex ?? 0;
        set { if (CanFollowFixture && FixtureTarget is { } node) { node.FixtureIndex = value; Chart.Model.IsModified = true; } }
    }
    public double ReferenceX { get => FixtureTarget?.ReferenceFixture.X ?? 0; set { if (CanFollowFixture && FixtureTarget is { } n) SetReference(new(value, n.ReferenceFixture.Y, n.ReferenceFixture.AngleDeg)); } }
    public double ReferenceY { get => FixtureTarget?.ReferenceFixture.Y ?? 0; set { if (CanFollowFixture && FixtureTarget is { } n) SetReference(new(n.ReferenceFixture.X, value, n.ReferenceFixture.AngleDeg)); } }
    public double ReferenceAngle { get => FixtureTarget?.ReferenceFixture.AngleDeg ?? 0; set { if (CanFollowFixture && FixtureTarget is { } n) SetReference(new(n.ReferenceFixture.X, n.ReferenceFixture.Y, value)); } }
    private void SetReference(Vision.Core.Geometry.Pose2D pose)
    {
        FixtureTarget!.ReferenceFixture = pose; Chart.Model.IsModified = true;
        OnPropertyChanged(nameof(ReferenceX)); OnPropertyChanged(nameof(ReferenceY)); OnPropertyChanged(nameof(ReferenceAngle));
    }
    private bool CanCaptureFixtureReference() => CanFollowFixture && !string.IsNullOrEmpty(SelectedFixtureSource);
    [RelayCommand(CanExecute = nameof(CanCaptureFixtureReference))]
    private void CaptureFixtureReference()
    {
        try
        {
            VisionFixtureBinding.CaptureReference(FixtureTarget!); Chart.Model.IsModified = true;
            Chart.Model.StatusText = "定位基准已设置；ROI仍在该基准图像坐标中编辑。";
            OnPropertyChanged(nameof(ReferenceX)); OnPropertyChanged(nameof(ReferenceY)); OnPropertyChanged(nameof(ReferenceAngle));
        }
        catch (Exception error) { Chart.Model.StatusText = $"设置定位基准失败：{error.Message}"; }
    }

    public ValueTask DisposeAsync() => new(_dispose ??= CloseAsync());
    private async Task CloseAsync()
    {
        _closing = true;
        RefreshLocks();
        foreach (var node in _owned) CloseEditor(node);
        PropertyGridVM.TargetObject = null;
        if (Editor != null) { Editor.PropertyChanged -= EditorChanged; Editor.Tabs.CollectionChanged -= EditorTabsChanged; }
        if (Chart.LoopStopCommand.CanExecute(null)) Chart.LoopStopCommand.Execute(null);
        if (Chart.StopCommand.CanExecute(null)) Chart.StopCommand.Execute(null);
        var pending = _owned.Select(n => n.DisposeAsync().AsTask()).ToList();
        pending.AddRange(_retiring);
        pending.AddRange(_graphs.Select(g => WorkbenchLifetime.WaitForCommandsAsync(g)));
        if (Editor != null) pending.Add(Editor.DisposeAsync().AsTask());
        if (RunFlowCommand.ExecutionTask is { } running) pending.Add(running);
        List<Exception> errors = [];
        foreach (var task in pending)
        {
            try { await task; }
            catch (Exception error) { errors.AddRange(error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : [error]); }
        }
        foreach (var error in _retirementErrors) if (!errors.Contains(error)) errors.Add(error);
        if (Editor != null) { Editor.PropertyChanged -= EditorChanged; Editor.Tabs.CollectionChanged -= EditorTabsChanged; }
        foreach (var node in _owned) { ObserveSessionEvents(node, false); ObserveNodeEvents(node, false); }
        _owned.Clear();
        PropertyGridVM.PropertyChanged -= PropertyGridChanged;
        SelectedEditor = null;
        foreach (var graph in _graphs.ToArray())
        {
            ObserveGraph(graph, false);
            graph.Nodes.Clear(); graph.Connections.Clear(); graph.UndoRedo.Clear(); graph.ReleaseLogRoute();
        }
        if (errors.Count > 0) throw new AggregateException("视觉流程关闭失败。", errors);
    }
}
