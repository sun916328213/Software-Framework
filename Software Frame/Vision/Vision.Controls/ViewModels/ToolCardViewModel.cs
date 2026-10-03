// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolCardViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.ComponentModel、Vision.Controls.Models、Vision.Controls.Services、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Core.Imaging;
#nullable enable
namespace Vision.Controls.ViewModels;
/// <summary>工具卡片绑定和 Toolkit 命令；执行与资源释放由 Workspace 负责。</summary>
public sealed partial class ToolCardViewModel : ObservableObject, IDisposable
{
    private readonly ToolCardWorkspace _workspace;
    private bool _disposed;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanUseCard。
    /// </summary>
    private bool CanUseCard() => !_disposed;
    public ToolCardModel Model => _workspace.Model;
    /// <summary>
    /// 初始化 ToolCardViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public ToolCardViewModel(IExecutableTool tool)
    {
        _workspace = new ToolCardWorkspace(tool);
        Model.PropertyChanged += OnModelChanged;
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e.PropertyName);
    public string ToolName => Model.ToolName;
    public string StatusText { get => Model.StatusText; set => Model.StatusText = value; }
    public bool IsValid { get => Model.IsValid; set => Model.IsValid = value; }
    public long ElapsedMs { get => Model.ElapsedMs; set => Model.ElapsedMs = value; }
    public string ResultSummary { get => Model.ResultSummary; set => Model.ResultSummary = value; }
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetImage。
    /// 正文中的关键协作调用包括 CanUseCard、SetImage；具体条件和先后次序见方法体。
    /// </summary>
    public void SetImage(VisionImage? image) { if (CanUseCard()) _workspace.SetImage(image); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshValidation。
    /// 正文中的关键协作调用包括 nameof、CanUseCard、RefreshValidation；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCard))] public void RefreshValidation() { if (CanUseCard()) _workspace.RefreshValidation(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnsureLoaded。
    /// 正文中的关键协作调用包括 nameof、CanUseCard、EnsureLoaded；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCard))] public void EnsureLoaded() { if (CanUseCard()) _workspace.EnsureLoaded(); }
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 nameof、CanUseCard、Run；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseCard))] public void Run() { if (CanUseCard()) _workspace.Run(); }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 NotifyCanExecuteChanged、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnModelChanged;
        RefreshValidationCommand.NotifyCanExecuteChanged();
        EnsureLoadedCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        _workspace.Dispose();
    }
}
