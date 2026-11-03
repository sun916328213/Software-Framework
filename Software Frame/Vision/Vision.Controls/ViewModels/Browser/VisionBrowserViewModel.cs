using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Vision.Controls.Contracts;
using Vision.Controls.Models;
using Vision.Controls.Services;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>完整编辑器：在无目录的调试会话之上增加工具目录、工程与宿主命令。</summary>
public partial class VisionBrowserViewModel : VisionToolEditorSession, IVisionProjectCommands
{
    private readonly VisionBrowserWorkspace _workspace;
    private readonly bool _ownsWorkspace;
    private bool _disposed;
    public new VisionBrowserPageModel Model => _workspace.Model;
    public override IVisionProjectCommands ProjectCommands => this;
    ICommand IVisionProjectCommands.ImportProjectCommand => ImportProjectCommand;
    ICommand IVisionProjectCommands.SaveProjectCommand => SaveProjectCommand;
    ICommand IVisionProjectCommands.SaveProjectAsCommand => SaveProjectAsCommand;
    ICommand IVisionProjectCommands.LoadImageCommand => LoadImageCommand;
    public VisionBrowserViewModel(VisionBrowserWorkspace workspace, bool ownsWorkspace = false)
        : base(workspace.Model, workspace.RoiEditor, workspace.ParametersChanged, workspace.CropInput,
            workspace.SetTemplateFromRoi, workspace.SetCropRegionFromRoi, manageParameterEditor: false)
    {
        _workspace = workspace; _ownsWorkspace = ownsWorkspace;
        Model.ToolIdentity = Model.SelectedEntry?.Name ?? string.Empty;
        Model.PropertyChanged += OnBrowserModelChanged;
    }
    private void OnBrowserModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Model.CurrentProjectPath)) OnPropertyChanged(nameof(ProjectTitle));
        if (e.PropertyName == nameof(Model.SelectedEntry))
        { Model.ToolIdentity = Model.SelectedEntry?.Name ?? string.Empty; RefreshCommands(); }
    }
    private bool CanSaveProject() => CanEdit() && CurrentCard != null;
    private bool CanImportProject() => CanEdit();
    public IReadOnlyList<ToolEntry> ToolEntries => Model.ToolEntries;
    public ToolEntry? SelectedEntry { get => Model.SelectedEntry; set => Model.SelectedEntry = value; }
    public string? CurrentProjectPath => Model.CurrentProjectPath;
    public string ProjectTitle => string.IsNullOrEmpty(CurrentProjectPath) ? "未保存工程" : System.IO.Path.GetFileName(CurrentProjectPath);
    protected override void RefreshCommands()
    {
        base.RefreshCommands();
        LoadImageCommand.NotifyCanExecuteChanged(); SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged(); ImportProjectCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand(CanExecute = nameof(CanEdit))]
    public void LoadImage()
    {
        if (!CanEdit()) return;
        var previous = InputImage; _workspace.LoadImage();
        if (!ReferenceEquals(previous, InputImage)) ShowWorkspaceMessage();
    }
    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    public void SaveProject() { if (CanSaveProject()) _workspace.SaveProject(); }
    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    public void SaveProjectAs() { if (CanSaveProject()) _workspace.SaveProjectAs(); }
    [RelayCommand(CanExecute = nameof(CanImportProject))]
    public void ImportProject() { if (CanImportProject()) _workspace.ImportProject(); }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnBrowserModelChanged;
        base.Dispose();
        if (_ownsWorkspace) _workspace.Dispose();
    }
}
