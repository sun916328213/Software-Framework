using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Vision.Core.Imaging;
using Vision.Core.Tools;

#nullable enable
namespace Vision.Controls.Services;

public sealed partial class VisionToolInstanceWorkspace
{
    private readonly IVisionProjectFileStore _projectFiles = new VisionProjectFileStore();
    private bool _fileOperation;
    private long _fileRevision;
    private string? _currentProjectPath;
    public string? CurrentProjectPath => _currentProjectPath;
    public string ProjectTitle => CurrentProjectPath == null ? $"{ToolIdentity} — 未保存工具配置" : Path.GetFileName(CurrentProjectPath);
    ICommand Contracts.IVisionProjectCommands.ImportProjectCommand => ImportProjectCommand;
    ICommand Contracts.IVisionProjectCommands.SaveProjectCommand => SaveProjectCommand;
    ICommand Contracts.IVisionProjectCommands.SaveProjectAsCommand => SaveProjectAsCommand;
    ICommand Contracts.IVisionProjectCommands.LoadImageCommand => LoadImageCommand;
    /// <summary>新配方不继承流程中旧ROI的定位基准；流程拥有者负责撤销其基准标记。</summary>
    internal event EventHandler? ConfigurationImported;

    private bool CanUseFiles() => !_closing && !IsBusy && !_prepared.Card.Model.IsReadOnly;
    private bool CanChooseProject() => CanUseFiles() && !_fileOperation && _catalog.PickProjectPath != null;
    private bool CanSaveProject() => CanUseFiles() && !_fileOperation && (CurrentProjectPath != null || _catalog.PickProjectPath != null);
    private bool CanLoadImage() => CanUseFiles() && !_fileOperation;
    private void RefreshFileCommands()
    {
        ImportProjectCommand.NotifyCanExecuteChanged(); SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged(); LoadImageCommand.NotifyCanExecuteChanged();
    }
    private void NotifyConfigurationChanged()
    {
        _fileRevision++;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    private void SetProjectPath(string path)
    {
        _currentProjectPath = Path.GetFullPath(path);
        OnPropertyChanged(nameof(CurrentProjectPath)); OnPropertyChanged(nameof(ProjectTitle));
    }
    private void CheckFileContext(long revision)
    {
        if (!CanUseFiles() || revision != _fileRevision)
            throw new InvalidOperationException("文件操作期间工具已运行、配置已改变、被锁定或关闭；本次操作未应用。");
    }
    private void FileOperation(Action<long> operation)
    {
        _ui.VerifyAccess();
        if (!CanUseFiles() || _fileOperation) return;
        _fileOperation = true; RefreshFileCommands();
        try { operation(_fileRevision); }
        catch (Exception error) { if (!_closing) Model.ImageInfoText = $"工具配置文件操作失败：{error.Message}"; }
        finally { _fileOperation = false; RefreshFileCommands(); }
    }
    [RelayCommand(CanExecute = nameof(CanChooseProject))]
    private void ImportProject() => FileOperation(revision =>
    {
        var path = _catalog.PickProjectPath?.Invoke(false);
        if (string.IsNullOrWhiteSpace(path)) return;
        CheckFileContext(revision);
        var document = _projectFiles.ReadProject(path);
        var project = VisionProjectCodec.Decode(document.Json);
        if (project.Tool != ToolIdentity) throw new InvalidDataException("配置工具与当前工具不同，不能替换节点工具。");
        // Provider具有SDK归属；先显式选择匹配Provider，不能在导入失败时偷偷切换到另一个实例。
        if (project.Provider != Model.SelectedProviderName) throw new InvalidDataException("配置Provider与当前工具不同，请先显式选择对应Provider。");
        var image = document.Image;
        if (!document.IsPackage && !string.IsNullOrWhiteSpace(project.ImageSource))
            image = _catalog.LoadImageByPath?.Invoke(Path.IsPathRooted(project.ImageSource) ? project.ImageSource
                : Path.GetFullPath(project.ImageSource, Path.GetDirectoryName(Path.GetFullPath(path))!))
                ?? throw new InvalidDataException("配置引用的输入图像不可读取。");
        var configuration = new VisionToolConfiguration
        {
            ToolIdentity = ToolIdentity, Parameters = project.Parameters, Provider = project.Provider,
            Rois = project.Rois, SelectedRoiIndex = project.SelectedRoiIndex,
            InputImage = image, UseLocalInputImage = true, ImageSha256 = image == null ? "" : VisionToolConfiguration.Hash(image),
            TemplateImage = project.TemplateImage, TrainingParameters = project.TrainingParameters, ModelReference = project.ModelReference
        };
        CheckFileContext(revision);
        ApplyConfiguration(configuration); // 原参数对象、卡片和会话不替换，不自动加载/训练模型。
        Model.SelectedImageView = project.ImageView;
        Model.ShowResultBounds = project.ShowResultBounds; Model.ShowResultCenters = project.ShowResultCenters;
        ConfigurationImported?.Invoke(this, EventArgs.Empty);
        SetProjectPath(path);
        Model.ImageInfoText = $"已打开当前工具配置：{path}；流程连接不变，定位跟随须重新设置基准。";
    });
    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private void SaveProject() => SaveConfiguration(false);
    [RelayCommand(CanExecute = nameof(CanChooseProject))]
    private void SaveProjectAs() => SaveConfiguration(true);
    private void SaveConfiguration(bool saveAs) => FileOperation(revision =>
    {
        var path = saveAs || CurrentProjectPath == null ? _catalog.PickProjectPath?.Invoke(true) : CurrentProjectPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        CheckFileContext(revision);
        var configuration = ExportConfiguration();
        var project = VisionProjectCodec.CreateProject(configuration,
            Model.SelectedImageView, Model.ShowResultBounds, Model.ShowResultCenters);
        _projectFiles.WriteProject(path, VisionProjectCodec.Encode(project), configuration.InputImage);
        SetProjectPath(path);
        Model.ImageInfoText = $"当前工具配置已保存：{path}；不含流程连接、运行结果或派生定位。";
    });
    [RelayCommand(CanExecute = nameof(CanLoadImage))]
    private void LoadImage() => FileOperation(revision =>
    {
        var image = _catalog.LoadImage();
        if (image == null) return;
        CheckFileContext(revision);
        var snapshot = VisionToolConfiguration.Snapshot(image);
        SetLocalImage(snapshot);
        NotifyConfigurationChanged();
        Model.ImageInfoText += "；已导入本工具本地输入；工具内执行使用此图，流程执行仍使用流程输入。";
    });
}
