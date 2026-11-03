using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Presentation;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Presentation;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>共享模板编辑会话。Workspace 拥有本适配器，工具卡片拥有底层模型。</summary>
public sealed partial class TemplateEditorViewModel : ObservableObject, IDisposable
{
    private ITemplateTool? _tool;
    private readonly Action _changed;
    private readonly Func<string, bool, string?>? _pickPath;
    private readonly Func<bool>? _canEdit;
    private bool _operating;
    private bool CanUseModel() => IsActive && !_operating && (_canEdit?.Invoke() ?? true);
    public bool RequiresTraining { get; }
    public object? TrainingParameters => _tool?.TrainingParameters;
    /// <summary>借用工具拥有的真实取样像素；不是搜索输入，也不转移模型所有权。</summary>
    public VisionImage? TemplateImage => _tool?.TemplateImage;
    public string TemplateSizeText => TemplateImage is { IsEmpty: false } image
        ? $"取样图像：{image.Width}×{image.Height}（{image.Format}）" : "尚无取样图像；已加载模型不一定包含取样。";
    public bool IsActive => _tool != null;
    [ObservableProperty] private bool _hasModel;
    [ObservableProperty] private bool _needsTraining;
    [ObservableProperty] private BitmapSource? _previewImage;
    [ObservableProperty] private IReadOnlyList<RegionContour> _contours = [];
    [ObservableProperty] private string _status = "尚未设置模板。";
    [ObservableProperty] private string _previewError = string.Empty;
    public bool HasContours => Contours.Count > 0;
    private bool CanTrain() => CanUseModel() && RequiresTraining && _tool?.TemplateImage != null;
    private bool CanSave() => CanUseModel() && HasModel && !NeedsTraining && _pickPath != null;
    private bool CanLoad() => CanUseModel() && _pickPath != null;
    private bool CanClear() => CanUseModel() && (HasModel || _tool?.TemplateImage != null);

    public TemplateEditorViewModel(ITemplateTool tool, Action changed, Func<string, bool, string?>? pickPath)
        : this(tool, changed, pickPath, null) { }
    public TemplateEditorViewModel(ITemplateTool tool, Action changed, Func<string, bool, string?>? pickPath, Func<bool>? canEdit)
    {
        _tool = tool; _changed = changed; _pickPath = pickPath; _canEdit = canEdit;
        RequiresTraining = tool.RequiresTraining;
        Refresh();
        if (HasModel) Status = NeedsTraining ? "模型需要重新训练。" : "模板/模型可用。";
        else if (tool.TemplateImage != null) Status = "已有取样图，尚未训练。";
    }
    public bool Capture(VisionImage image) => Execute(t => t.SetTemplate(image), "已取样；搜索图保持不变。");
    public void TrainingParametersChanged()
    {
        if (RequiresTraining) Execute(t => t.MarkTrainingDirty(), "训练参数已变化，需要重新训练。");
    }
    [RelayCommand(CanExecute = nameof(CanTrain))]
    public void Train() { if (CanTrain()) Execute(t => t.Train(), "训练完成。"); }
    [RelayCommand(CanExecute = nameof(CanSave))]
    public void Save()
    {
        if (!CanSave()) return;
        PickAndExecute(save: true);
    }
    [RelayCommand(CanExecute = nameof(CanLoad))]
    public void Load()
    {
        if (!CanLoad()) return;
        PickAndExecute(save: false);
    }
    private string _modelReferencePath = "";
    public string ModelReferencePath { get => _modelReferencePath; private set => SetProperty(ref _modelReferencePath, value); }
    public string ModelStateText => !IsActive ? "模型会话已关闭"
        : NeedsTraining ? "取样或训练设置已改变，需要训练；旧模型不可作为本次结果使用"
        : HasModel ? RequiresTraining ? "已有可用训练模型" : "灰度取样模板可直接匹配，无需训练"
        : "模型未准备；请显式取样、训练或加载。AI推理模型加载不等于训练。";
    /// <summary>只恢复引用提示，不加载SDK模型；缺失文件必须明确显示。</summary>
    public void RestoreReference(string path)
    {
        ModelReferencePath = path;
        if (!string.IsNullOrWhiteSpace(path))
            Status = System.IO.File.Exists(path)
                ? HasModel && !NeedsTraining && !RequiresTraining ? "已恢复内嵌取样模板，可直接匹配；引用文件尚未读取。" : "已恢复模型引用；尚未加载，请显式加载或训练。"
                : HasModel && !NeedsTraining && !RequiresTraining ? "模型引用不存在；当前内嵌取样模板仍可直接匹配。" : "模型引用不存在；请重新选择或训练。";
    }
    private void PickAndExecute(bool save)
    {
        try
        {
            var path = _pickPath!(_tool!.ModelFileFilter, save);
            if (string.IsNullOrEmpty(path) || !CanUseModel()) return; // 对话框取消/期间切工具不修改模型和结果。
            var succeeded = save ? Execute(t => t.SaveModel(path), "模型已保存。", changesModel: false)
                : Execute(t => t.LoadModel(path), "模型已加载。");
            if (succeeded) ModelReferencePath = path;
        }
        catch (Exception error) { if (IsActive) Status = $"文件操作失败：{error.Message}"; }
    }
    [RelayCommand(CanExecute = nameof(CanClear))]
    public void Clear() { if (CanClear()) Execute(t => t.ClearModel(), "模板和模型已清除。"); }
    private bool Execute(Action<ITemplateTool> action, string success, bool changesModel = true)
    {
        if (!CanUseModel()) return false;
        _operating = true;
        try
        {
            if (changesModel) _changed();
            if (_tool == null || _canEdit?.Invoke() == false) return false;
            action(_tool);
            if (changesModel) ModelReferencePath = "";
            Status = success;
            return true;
        }
        catch (Exception error) { Status = $"操作失败：{error.Message}"; return false; }
        finally
        {
            _operating = false;
            if (_tool != null)
            {
                if (changesModel) _changed();
                Refresh();
            }
        }
    }
    public void RefreshFromTool() { if (IsActive) Refresh(); }
    private void Refresh()
    {
        HasModel = _tool?.HasModel == true;
        NeedsTraining = _tool?.NeedsTraining == true;
        PreviewError = string.Empty;
        try { PreviewImage = _tool?.TemplateImage is { } image ? VisionDisplay.ToBitmap(image) : null; }
        catch (Exception error) { PreviewImage = null; PreviewError = $"预览不可用：{error.Message}"; }
        Contours = _tool?.ModelContours ?? [];
        OnPropertyChanged(nameof(HasContours));
        OnPropertyChanged(nameof(TemplateImage));
        OnPropertyChanged(nameof(TemplateSizeText));
        OnPropertyChanged(nameof(ModelStateText));
        RefreshAvailability();
    }
    public void RefreshAvailability()
    {
        TrainCommand.NotifyCanExecuteChanged(); SaveCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged(); ClearCommand.NotifyCanExecuteChanged();
    }
    public void Dispose()
    {
        if (_tool == null) return;
        _tool = null; // 不释放借用的工具；卡片随后释放它拥有的模型。
        Refresh();
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(TrainingParameters));
    }
}
