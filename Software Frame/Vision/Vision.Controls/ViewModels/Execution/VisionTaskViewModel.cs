using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Core.Models;
using FlowEditor.Runtime;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>顺序视觉任务：列表决定依赖顺序，执行交给原 FlowEditor 引擎；选择实例只切换借用的编辑器。</summary>
public partial class VisionTaskViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly VisionBrowserCatalog _catalog;
    private readonly IUiDispatcher _ui;
    private readonly FlowChartExecutionEngine _engine = new();
    private readonly ObservableCollection<VisionTaskToolNode> _nodes = new();
    private VisionImage? _image;
    private CancellationTokenSource? _stop;
    private Task? _activeRun;
    private Task? _disposeTask;
    private bool _disposed;

    public VisionTaskViewModel(VisionBrowserCatalog catalog, IUiDispatcher ui)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        Nodes = new(_nodes);
        SelectedEntry = catalog.Tools.FirstOrDefault(e => e.IsDefault) ?? catalog.Tools.FirstOrDefault();
    }

    public ReadOnlyObservableCollection<VisionTaskToolNode> Nodes { get; }
    public IReadOnlyList<ToolEntry> ToolEntries => _catalog.Tools;
    public VisionBrowserViewModel? SelectedEditor => SelectedNode?.Editor;
    public bool IsEditingEnabled => CanModify();
    [ObservableProperty] private ToolEntry? _selectedEntry;
    [ObservableProperty] private VisionTaskToolNode? _selectedNode;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "从工具箱添加任务实例；选择不会重建工具。";

    partial void OnSelectedEntryChanged(ToolEntry? value) => RefreshCommands();
    partial void OnSelectedNodeChanged(VisionTaskToolNode? value)
    {
        OnPropertyChanged(nameof(SelectedEditor));
        RefreshCommands();
    }
    partial void OnIsRunningChanged(bool value) => RefreshCommands();
    private bool CanModify() => !_disposed && !IsRunning && _nodes.All(n => !n.IsBusy);
    private bool CanAdd() => CanModify() && SelectedEntry != null;
    private bool CanRemove() => CanModify() && SelectedNode != null;
    private bool CanMoveUp() => CanRemove() && _nodes.IndexOf(SelectedNode!) > 0;
    private bool CanMoveDown() => CanRemove() && _nodes.IndexOf(SelectedNode!) < _nodes.Count - 1;
    private bool CanRun() => CanModify() && _nodes.Count > 0;
    private bool CanStop() => !_disposed && IsRunning;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddTool()
    {
        try { Add(SelectedEntry!); }
        catch (Exception error) { StatusText = $"添加工具失败：{error.Message}"; }
    }

    /// <summary>工厂每次创建一个独立工作区，仍直接编辑其原参数 DTO。</summary>
    public VisionTaskToolNode Add(ToolEntry entry)
    {
        if (!CanModify()) throw new InvalidOperationException("任务执行中或已经关闭。");
        if (!_catalog.Tools.Contains(entry)) throw new ArgumentException("工具不属于当前目录。", nameof(entry));
        var node = new VisionTaskToolNode(_catalog, entry, _ui);
        try
        {
            var suffix = 1;
            while (_nodes.Any(n => n.Name == $"{entry.Name} {suffix}")) suffix++;
            node.Name = $"{entry.Name} {suffix}";
            if (_image != null) node.SetImage(_image);
            node.PropertyChanged += NodeChanged;
            _nodes.Add(node);
            SelectedNode = node;
            StatusText = $"已添加 {node.Name}";
            return node;
        }
        catch { node.Dispose(); throw; }
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void RemoveTool()
    {
        var node = SelectedNode!;
        var index = _nodes.IndexOf(node);
        node.PropertyChanged -= NodeChanged;
        _nodes.Remove(node);
        SelectedNode = _nodes.Count == 0 ? null : _nodes[Math.Min(index, _nodes.Count - 1)];
        node.Dispose();
        RefreshCommands();
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);
    private void Move(int delta)
    {
        var index = _nodes.IndexOf(SelectedNode!);
        _nodes.Move(index, index + delta);
        RefreshCommands();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void LoadImage()
    {
        try { if (_catalog.LoadImage() is { } image) SetImage(image); }
        catch (Exception error) { StatusText = $"导入图像失败：{error.Message}"; }
    }

    /// <summary>借用同一原始输入；处理工具的输出不会悄悄替换下一工具的输入。</summary>
    public void SetImage(VisionImage image)
    {
        if (!CanModify()) throw new InvalidOperationException("任务执行中或已经关闭。");
        ArgumentNullException.ThrowIfNull(image);
        foreach (var node in _nodes) node.SetImage(image);
        _image = image;
        StatusText = $"任务输入：{image.Width}×{image.Height}，{image.Format}";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    public Task RunTaskAsync()
    {
        if (!CanRun()) return Task.CompletedTask;
        return _activeRun = ExecuteTaskAsync();
    }

    private async Task ExecuteTaskAsync()
    {
        IsRunning = true;
        _stop = new CancellationTokenSource();
        try
        {
            var nodes = _nodes.ToArray();
            List<ConnectionModel> edges = [];
            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i].Editor.RoiEditor.EndManipulation(cancel: false);
                nodes[i].Editor.CurrentCard!.ClearResult();
                nodes[i].IsInEnabled = i != 0;
                nodes[i].IsOutEnabled = true;
                if (i == 0) continue;
                edges.Add(new()
                {
                    SourceNodeId = nodes[i - 1].Id,
                    SourcePortId = nodes[i - 1].OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    TargetNodeId = nodes[i].Id,
                    TargetPortId = nodes[i].InputPorts.Single(p => p.Name == "Next_IN").Id
                });
            }
            StatusText = "任务执行中…";
            await _engine.ExecuteAsync(nodes, edges, _stop.Token,
                message => _ui.Invoke(() => StatusText = message));
            StatusText = "任务执行完成";
        }
        catch (OperationCanceledException) { StatusText = "任务已停止"; }
        catch (Exception error) { StatusText = $"任务失败：{error.Message}"; }
        finally
        {
            _stop.Dispose();
            _stop = null;
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void StopTask()
    {
        _stop?.Cancel();
        StatusText = "正在停止；等待当前工具实际结束，不强行中断 SDK。";
    }

    private void NodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisionTaskToolNode.IsBusy)) RefreshCommands();
    }
    private void RefreshCommands()
    {
        var readOnly = !CanModify();
        foreach (var node in _nodes)
            if (node.Editor.CurrentCard is { } card) card.Model.IsReadOnly = readOnly;
        AddToolCommand.NotifyCanExecuteChanged(); RemoveToolCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged(); MoveDownCommand.NotifyCanExecuteChanged();
        LoadImageCommand.NotifyCanExecuteChanged(); RunTaskCommand.NotifyCanExecuteChanged(); StopTaskCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsEditingEnabled));
    }

    /// <summary>保留Dispatcher，等待整任务及各卡片的实际执行，并在全部清理后上报错误。</summary>
    public ValueTask DisposeAsync() => new(BeginDispose());

    public void Dispose()
    {
        var cleanup = BeginDispose();
        // 仅读取已完成任务，绝不在UI线程同步等待正在执行的SDK。
        if (cleanup.IsCompleted) cleanup.GetAwaiter().GetResult();
    }

    private Task BeginDispose()
    {
        if (_disposeTask != null) return _disposeTask;
        _disposed = true;
        List<Exception> errors = [];
        List<Task> cleanup = [];
        try { _stop?.Cancel(); }
        catch (Exception error) { errors.Add(error); }
        foreach (var node in _nodes)
        {
            node.PropertyChanged -= NodeChanged;
            try { cleanup.Add(node.DisposeAsync().AsTask()); }
            catch (Exception error) { errors.Add(error); }
        }
        if (_activeRun != null) cleanup.Add(_activeRun);
        RefreshCommands();
        return _disposeTask = CompleteDisposeAsync(cleanup, errors);
    }

    private static async Task CompleteDisposeAsync(List<Task> cleanup, List<Exception> errors)
    {
        // 所有清理已发起。一个失败不跳过其它节点，也不能跳过实际运行的等待。
        foreach (var task in cleanup)
        {
            try { await task; }
            catch (AggregateException error) { errors.AddRange(error.Flatten().InnerExceptions); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("视觉任务关闭失败。", errors);
    }
}
