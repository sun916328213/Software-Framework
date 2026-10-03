// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：VisionBrowserViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.ComponentModel、Vision.Controls.Models、Vision.Controls.Services。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using Vision.Controls.Models;
using Vision.Controls.Services;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>
/// VisionBrowserViewModel 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ObservableObject、IDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class VisionBrowserViewModel : ObservableObject, IDisposable
{
    private readonly VisionBrowserWorkspace _workspace;
    private readonly bool _ownsWorkspace;
    private bool _disposed;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanUseWorkspace。
    /// </summary>
    private bool CanUseWorkspace() => !_disposed && !Model.IsClosed;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanCompare。
    /// 正文中的关键协作调用包括 CanUseWorkspace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanCompare() => CanUseWorkspace() && Model.HasImage;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanBenchmark。
    /// 正文中的关键协作调用包括 CanUseWorkspace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanBenchmark() => CanUseWorkspace() && Model.CurrentCard != null;
    public VisionBrowserPageModel Model => _workspace.Model;
    /// <summary>
    /// 初始化 VisionBrowserViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public VisionBrowserViewModel(VisionBrowserWorkspace workspace, bool ownsWorkspace = false)
    {
        _workspace = workspace;
        _ownsWorkspace = ownsWorkspace;
        Model.PropertyChanged += OnModelChanged;
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof、RefreshCommands；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName is nameof(Model.HasImage) or nameof(Model.CurrentCard) or nameof(Model.IsClosed))
            RefreshCommands();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshCommands。
    /// 正文中的关键协作调用包括 NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void RefreshCommands()
    {
        LoadImageCommand.NotifyCanExecuteChanged();
        UseDemoImageCommand.NotifyCanExecuteChanged();
        CompareBlobCommand.NotifyCanExecuteChanged();
        RunBenchmarkCommand.NotifyCanExecuteChanged();
    }
    public IReadOnlyList<ToolEntry> ToolEntries => Model.ToolEntries;
    public IReadOnlyList<ProviderEntry> ProviderEntries => Model.ProviderEntries;
    public RoiEditorViewModel RoiEditor => _workspace.RoiEditor;
    public ToolCardViewModel? CurrentCard { get => Model.CurrentCard; set => Model.CurrentCard = value; }
    public object? CurrentParameters { get => Model.CurrentParameters; set => Model.CurrentParameters = value; }
    public ToolEntry? SelectedEntry { get => Model.SelectedEntry; set => Model.SelectedEntry = value; }
    public string SelectedProviderName { get => Model.SelectedProviderName; set => Model.SelectedProviderName = value; }
    public string ImageInfoText { get => Model.ImageInfoText; set => Model.ImageInfoText = value; }
    public string CompareText { get => Model.CompareText; set => Model.CompareText = value; }
    public string BenchmarkText { get => Model.BenchmarkText; set => Model.BenchmarkText = value; }
    /// <summary>
    /// 执行加载操作；对应入口为 LoadImage。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、LoadImage；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    public void LoadImage() { if (CanUseWorkspace()) _workspace.LoadImage(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 UseDemoImage。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、UseDemoImage；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    public void UseDemoImage() { if (CanUseWorkspace()) _workspace.UseDemoImage(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CompareBlob。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、CompareBlob；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCompare))]
    public void CompareBlob() { if (CanUseWorkspace()) _workspace.CompareBlob(); }
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunBenchmark。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、RunBenchmark；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBenchmark))]
    public void RunBenchmark() { if (CanUseWorkspace()) _workspace.RunBenchmark(); }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 RefreshCommands、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnModelChanged;
        RefreshCommands();
        if (_ownsWorkspace) _workspace.Dispose();
    }
}
