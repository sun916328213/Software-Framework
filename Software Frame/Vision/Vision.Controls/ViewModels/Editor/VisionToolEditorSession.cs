using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Contracts;
using Vision.Controls.Models;
using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>单工具编辑会话，可选借用工程命令；没有目录/文件存储依赖。Dispose只退订与释放自己的属性适配，不释放卡片、工具、输入或结果。</summary>
public partial class VisionToolEditorSession : ObservableObject, IDisposable
{
    private readonly Action? _parametersChanged;
    private readonly Action? _cropInput;
    private readonly Action? _setTemplate;
    private readonly Action? _setCropRegion;
    private readonly bool _manageParameterEditor;
    private BlobParametersEditorViewModel? _ownedBlobEditor;
    private ToolCardModel? _observedCard;
    private TemplateEditorViewModel? _observedTemplate;
    private INotifyPropertyChanged? _observedParameters;
    private bool _disposed;
    private bool _showWorkspaceMessage;
    public VisionToolEditorModel Model { get; }
    public RoiEditorViewModel RoiEditor { get; }
    private readonly IVisionProjectCommands? _projectCommands;
    public virtual IVisionProjectCommands? ProjectCommands => _projectCommands;
    public bool HasProjectCommands => ProjectCommands != null;
    public VisionToolEditorSession(VisionToolEditorModel model, RoiEditorViewModel? roiEditor = null,
        Action? parametersChanged = null, Action? cropInput = null, Action? setTemplate = null,
        Action? setCropRegion = null, bool manageParameterEditor = true, IVisionProjectCommands? projectCommands = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        RoiEditor = roiEditor ?? new();
        _projectCommands = projectCommands;
        _parametersChanged = parametersChanged; _cropInput = cropInput;
        _setTemplate = setTemplate; _setCropRegion = setCropRegion;
        // 宿主已提供适配器时只借用，不能覆盖或释放宿主的编辑会话。
        _manageParameterEditor = manageParameterEditor && model.BlobEditor == null;
        if (_manageParameterEditor) RebuildParameterEditor();
        Model.PropertyChanged += OnModelChanged;
        RoiEditor.PropertyChanged += OnRoiChanged;
        ObserveCard();
        ObserveTemplate();
        ObserveParameters();
    }
    protected bool CanUseWorkspace() => !_disposed && !Model.IsClosed;
    protected bool CanEdit() => CanUseWorkspace() && CurrentCard?.Model.IsBusy != true && CurrentCard?.Model.IsReadOnly != true;
    public bool IsSessionActive => CanUseWorkspace();
    public bool IsEditingEnabled => CanEdit();
    public bool HasCurrentTool => CanUseWorkspace() && CurrentCard != null;
    public bool SupportsSearchRegion => CurrentCard?.SupportsSearchRegion == true;
    public string ToolIdentity => string.IsNullOrWhiteSpace(Model.ToolIdentity) ? CurrentCard?.ToolName ?? "未选择工具" : Model.ToolIdentity;
    public VisionImage? InputImage => Model.InputImage;
    public global::Vision.Core.Roi.Roi? ExecutionSearchRoi => Model.ExecutionSearchRoi;
    public ToolCardViewModel? CurrentCard { get => Model.CurrentCard; set => Model.CurrentCard = value; }
    public object? CurrentParameters { get => Model.CurrentParameters; set => Model.CurrentParameters = value; }
    public BlobParametersEditorViewModel? BlobEditor => Model.BlobEditor;
    public bool HasBlobEditor => BlobEditor != null;
    public object? ParameterEditor => (object?)BlobEditor ?? CurrentParameters;
    public bool UsesMetadataEditor => ParameterEditor != null;
    public Vision.Core.Geometry.ILineSearchRegion? LineSearch => CurrentParameters as Vision.Core.Geometry.ILineSearchRegion;
    public bool HasLineSearch => LineSearch != null;
    public bool UsesAreaRois => !HasLineSearch;
    public bool CanDrawAreaRoi => UsesAreaRois && IsEditingEnabled && Model.HasImage;
    public bool SupportsProvider => Model.SupportsProvider;
    public bool SupportsTemplateInput => Model.SupportsTemplateInput;
    public bool IsOpenCvTemplateMatch => CurrentParameters is Vision.Tools.OpenCv.Matching.TemplateMatchParameters;
    public bool SupportsCropRegion => Model.SupportsCropRegion;
    public string RoiPurpose
    {
        get
        {
            if (!HasCurrentTool) return "未选择工具。";
            if (CurrentParameters is Vision.Tools.OpenCv.Processing.CropParameters crop && SupportsCropRegion)
            {
                var region = $"当前裁剪参数：左上 ({crop.X:0.###}, {crop.Y:0.###})，宽 {crop.Width:0.###}，高 {crop.Height:0.###}。";
                if (InputImage is { IsEmpty: false } image && double.IsFinite(crop.X) && double.IsFinite(crop.Y) &&
                    double.IsFinite(crop.Width) && double.IsFinite(crop.Height) && crop.Width > 0 && crop.Height > 0)
                {
                    var left = Math.Clamp(Math.Floor(crop.X), 0, image.Width);
                    var top = Math.Clamp(Math.Floor(crop.Y), 0, image.Height);
                    var right = Math.Clamp(Math.Ceiling(crop.X + crop.Width), 0, image.Width);
                    var bottom = Math.Clamp(Math.Ceiling(crop.Y + crop.Height), 0, image.Height);
                    region += right > left && bottom > top
                        ? $"生效像素范围：左上 ({left}, {top})，宽 {right - left}，高 {bottom - top}。"
                        : "裁剪范围不在图像内。";
                }
                else region += "无有效图像或裁剪参数，无法确定生效像素范围。";
                if (!RoiEditor.HasSelection) return $"裁剪区域 | {region}请选择轴对齐矩形并显式应用。";
                var roi = RoiEditor.Selected!;
                if (roi.Kind != Vision.Core.Roi.RoiKind.Rectangle || Math.Abs(roi.Pose.AngleDeg) > 1e-9)
                    return $"裁剪区域 | {region}选中 ROI 未应用；请选择轴对齐矩形。";
                var bounds = roi.Bounds;
                var matches = bounds.X == crop.X && bounds.Y == crop.Y && bounds.Width == crop.Width && bounds.Height == crop.Height;
                return $"裁剪区域 | {region}选中矩形{(matches ? "已应用" : "未应用")}；使用按钮显式应用。";
            }
            if (CurrentParameters is Vision.Tools.OpenCv.Measurement.FindCircleParameters)
                return "圆心选择域：ROI 只筛选全图 Hough 候选圆心，不裁剪像素或约束完整圆周；半径由参数设置。";
            if (HasLineSearch) return "线搜索域：按测量路径和方向采样；不是面积 ROI。";
            if (SupportsSearchRegion && SupportsTemplateInput)
                return "搜索域限制检测范围；选中矩形需显式取样为模板，模板与搜索域独立。";
            if (SupportsSearchRegion) return "已确认的 ROI 用作搜索域；隐藏不禁用，清空后才使用全图。";
            if (SupportsTemplateInput) return "ROI 仅为标注；选中矩形需显式取样为模板。";
            if (SupportsCropRegion) return "ROI 仅为标注；选中矩形需显式应用为裁剪区域。";
            return "ROI 仅为标注，不参与工具执行。";
        }
    }
    public TemplateEditorViewModel? TemplateEditor => Model.TemplateEditor;
    public bool SupportsModelLoading => Model.SupportsModelLoading;
    public bool SupportsModelPanel => SupportsProvider || SupportsModelLoading;
    public IReadOnlyList<ProviderEntry> ProviderEntries => Model.ProviderEntries;
    public string SelectedProviderName { get => Model.SelectedProviderName; set => Model.SelectedProviderName = value; }
    public ImageViewKind SelectedImageView { get => Model.SelectedImageView; set => Model.SelectedImageView = value; }
    public bool ShowOutputImage { get => Model.ShowOutputImage; set => Model.ShowOutputImage = value; }
    public bool ShowRoiImage { get => Model.ShowRoiImage; set => Model.ShowRoiImage = value; }
    public bool ShowTrainingImage => SelectedImageView == ImageViewKind.Training;
    public VisionImage? TrainingImage => TemplateEditor?.TemplateImage;
    public bool HasTrainingImage => TrainingImage is { IsEmpty: false };
    public bool ShowOriginalImage => !ShowOutputImage && !ShowRoiImage && !ShowTrainingImage;
    public string ImageRecordText
    {
        get
        {
            if (ShowTrainingImage) return HasTrainingImage
                ? $"训练图像 | {TrainingImage!.Width}×{TrainingImage.Height} | 取样局部像素坐标；只读，不叠加输入ROI/结果"
                : "训练图像 | 尚无取样数据，不以输入图像替代";
            if (ShowRoiImage) return RoiImageInfo;
            if (ShowOutputImage && Presentation.VisionDisplay.OutputImage(CurrentCard?.LastResult) is { } output)
                return $"结果图像 | {output.Width}×{output.Height} | 算法输出像素";
            if (SelectedImageView == ImageViewKind.Result) return CurrentCard?.LastResult?.Success == true
                ? "结果图像 | 无独立输出图，使用输入坐标显示结果标注"
                : "结果图像 | 尚无有效结果，当前底图为输入图像";
            return SelectedImageView == ImageViewKind.Roi ? "ROI 图像 | 输入像素坐标与搜索域标注" : "输入图像 | 当前工具执行输入像素";
        }
    }
    public bool ShowRois { get => Model.ShowRois; set => Model.ShowRois = value; }
    public bool ShowResults { get => Model.ShowResults; set => Model.ShowResults = value; }
    public bool ShowResultBounds { get => Model.ShowResultBounds; set => Model.ShowResultBounds = value; }
    public bool ShowResultCenters { get => Model.ShowResultCenters; set => Model.ShowResultCenters = value; }
    public string ImageInfoText { get => Model.ImageInfoText; set => Model.ImageInfoText = value; }
    public string RoiImageInfo => ShowRoiImage ? Presentation.VisionDisplay.RoiImageInfo(CurrentCard?.LastResult) : string.Empty;
    public string CurrentOperationText
    {
        get
        {
            if (_showWorkspaceMessage || CurrentCard is not { } card) return ImageInfoText;
            if (card.Model.IsBusy) return $"{card.ToolName} | 执行中…";
            var parts = new List<string> { card.ToolName, card.StatusText };
            if (card.LastResult != null) { parts.Add(card.ResultSummary); parts.Add($"耗时 {card.ElapsedMs} ms"); }
            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
    protected void ShowWorkspaceMessage()
    {
        _showWorkspaceMessage = true;
        OnPropertyChanged(nameof(CurrentOperationText));
    }
    private void ObserveCard()
    {
        if (_observedCard != null) _observedCard.PropertyChanged -= OnCardChanged;
        _observedCard = CurrentCard?.Model;
        if (_observedCard != null) _observedCard.PropertyChanged += OnCardChanged;
        OnPropertyChanged(nameof(SupportsSearchRegion)); OnPropertyChanged(nameof(ToolIdentity));
        OnPropertyChanged(nameof(RoiImageInfo));
        _showWorkspaceMessage = false; OnPropertyChanged(nameof(CurrentOperationText));
    }
    private void ObserveParameters()
    {
        if (_observedParameters != null) _observedParameters.PropertyChanged -= OnParametersChanged;
        _observedParameters = CurrentParameters as INotifyPropertyChanged;
        if (_observedParameters != null) _observedParameters.PropertyChanged += OnParametersChanged;
        OnPropertyChanged(nameof(RoiPurpose));
    }
    private void OnParametersChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(RoiPurpose));
    private void ObserveTemplate()
    {
        if (_observedTemplate != null) _observedTemplate.PropertyChanged -= OnTemplateChanged;
        _observedTemplate = Model.TemplateEditor;
        if (_observedTemplate != null) _observedTemplate.PropertyChanged += OnTemplateChanged;
        NotifyTrainingImage();
    }
    private void OnTemplateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplateEditorViewModel.TemplateImage)) NotifyTrainingImage();
    }
    private void NotifyTrainingImage()
    {
        OnPropertyChanged(nameof(TrainingImage)); OnPropertyChanged(nameof(HasTrainingImage)); OnPropertyChanged(nameof(ImageRecordText));
    }
    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolCardModel.IsBusy) or nameof(ToolCardModel.StatusText) or
            nameof(ToolCardModel.LastResult) or nameof(ToolCardModel.ResultSummary) or nameof(ToolCardModel.ElapsedMs))
        { _showWorkspaceMessage = false; OnPropertyChanged(nameof(CurrentOperationText)); }
        if (e.PropertyName is nameof(ToolCardModel.IsBusy) or nameof(ToolCardModel.IsReadOnly)) RefreshCommands();
        if (e.PropertyName == nameof(ToolCardModel.LastResult))
        {
            OnPropertyChanged(nameof(RoiImageInfo)); OnPropertyChanged(nameof(ImageRecordText));
            if (CurrentCard?.LastResult?.Success == true) SelectedImageView = ImageViewKind.Result;
        }
    }
    private void OnRoiChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoiEditor.Selected) && UsesAreaRois && RoiEditor.Selected != null && IsEditingEnabled)
            SelectedImageView = ImageViewKind.Roi;
        if (e.PropertyName is nameof(RoiEditor.Selected) or nameof(RoiEditor.Count))
        { OnPropertyChanged(nameof(RoiPurpose)); RefreshCommands(); }
    }
    private void RebuildParameterEditor()
    {
        _ownedBlobEditor?.Dispose();
        _ownedBlobEditor = BlobParametersEditorViewModel.TryCreate(CurrentParameters, ParametersChanged);
        Model.BlobEditor = _ownedBlobEditor;
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(Model.ImageInfoText)) ShowWorkspaceMessage();
        if (e.PropertyName == nameof(Model.CurrentCard)) ObserveCard();
        if (e.PropertyName == nameof(Model.TemplateEditor)) ObserveTemplate();
        if (e.PropertyName == nameof(Model.SelectedImageView))
        { OnPropertyChanged(nameof(ShowTrainingImage)); OnPropertyChanged(nameof(ShowOriginalImage)); }
        if (e.PropertyName is nameof(Model.SelectedImageView) or nameof(Model.ShowOutputImage) or nameof(Model.ShowRoiImage) or nameof(Model.InputImage))
            OnPropertyChanged(nameof(ImageRecordText));
        if (e.PropertyName == nameof(Model.CurrentParameters))
        {
            ObserveParameters();
            if (_manageParameterEditor) RebuildParameterEditor();
        }
        if (e.PropertyName is nameof(Model.CurrentCard) or nameof(Model.SupportsCropRegion) or nameof(Model.SupportsTemplateInput) or nameof(Model.IsClosed) or nameof(Model.InputImage))
            OnPropertyChanged(nameof(RoiPurpose));
        if (e.PropertyName is nameof(Model.CurrentParameters) or nameof(Model.BlobEditor))
        {
            OnPropertyChanged(nameof(ParameterEditor)); OnPropertyChanged(nameof(UsesMetadataEditor)); OnPropertyChanged(nameof(HasBlobEditor)); OnPropertyChanged(nameof(IsOpenCvTemplateMatch));
            OnPropertyChanged(nameof(LineSearch)); OnPropertyChanged(nameof(HasLineSearch)); OnPropertyChanged(nameof(UsesAreaRois)); OnPropertyChanged(nameof(CanDrawAreaRoi));
            if (HasLineSearch) SelectedImageView = ImageViewKind.Roi;
        }
        if (e.PropertyName is nameof(Model.ShowOutputImage) or nameof(Model.ShowRoiImage))
        { OnPropertyChanged(nameof(ShowOriginalImage)); OnPropertyChanged(nameof(RoiImageInfo)); }
        if (e.PropertyName is nameof(Model.SupportsProvider) or nameof(Model.SupportsModelLoading))
            OnPropertyChanged(nameof(SupportsModelPanel));
        if (e.PropertyName is nameof(Model.HasImage) or nameof(Model.CurrentCard) or nameof(Model.IsClosed) or
            nameof(Model.SupportsTemplateInput) or nameof(Model.SupportsModelLoading) or nameof(Model.SupportsCropRegion)) RefreshCommands();
    }
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    public void SelectOriginalImage() { if (CanUseWorkspace()) SelectBaseImage(false); }
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    public void SelectOutputImage() { if (CanUseWorkspace()) SelectBaseImage(true); }
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    public void SelectRoiImage()
    {
        if (!CanUseWorkspace()) return;
        if (ShowTrainingImage) SelectedImageView = ImageViewKind.Roi;
        ShowRoiImage = true;
        OnPropertyChanged(nameof(ShowRoiImage)); OnPropertyChanged(nameof(ShowOutputImage)); OnPropertyChanged(nameof(ShowOriginalImage));
    }
    private void SelectBaseImage(bool output)
    {
        if (ShowTrainingImage) SelectedImageView = output ? ImageViewKind.Result : ImageViewKind.Input;
        ShowRoiImage = false; ShowOutputImage = output;
        OnPropertyChanged(nameof(ShowOutputImage)); OnPropertyChanged(nameof(ShowOriginalImage));
    }
    private bool CanCrop() => CanEdit() && Model.HasImage && RoiEditor.Selected?.Kind == Vision.Core.Roi.RoiKind.Rectangle && _cropInput != null;
    private bool CanSetTemplate() => CanEdit() && Model.HasImage && RoiEditor.Selected?.Kind == Vision.Core.Roi.RoiKind.Rectangle && SupportsTemplateInput && _setTemplate != null;
    private bool CanSetCropRegion() => CanEdit() && Model.HasImage && RoiEditor.Selected?.Kind == Vision.Core.Roi.RoiKind.Rectangle && SupportsCropRegion && _setCropRegion != null;
    [RelayCommand(CanExecute = nameof(CanCrop))]
    public void CropInput() { if (CanCrop()) _cropInput!(); }
    [RelayCommand(CanExecute = nameof(CanSetTemplate))]
    public void SetTemplateFromRoi() { if (CanSetTemplate()) _setTemplate!(); }
    [RelayCommand(CanExecute = nameof(CanSetCropRegion))]
    public void SetCropRegionFromRoi() { if (CanSetCropRegion()) _setCropRegion!(); }
    public void ParametersChanged()
    {
        if (!CanUseWorkspace()) return;
        OnPropertyChanged(nameof(RoiPurpose));
        if (_parametersChanged != null) _parametersChanged();
        else { CurrentCard?.ClearResult(); CurrentCard?.RefreshValidation(); Model.TemplateEditor?.RefreshFromTool(); }
    }
    protected virtual void RefreshCommands()
    {
        TemplateEditor?.RefreshAvailability();
        CropInputCommand.NotifyCanExecuteChanged(); SetTemplateFromRoiCommand.NotifyCanExecuteChanged(); SetCropRegionFromRoiCommand.NotifyCanExecuteChanged();
        SelectOriginalImageCommand.NotifyCanExecuteChanged(); SelectOutputImageCommand.NotifyCanExecuteChanged(); SelectRoiImageCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSessionActive)); OnPropertyChanged(nameof(HasCurrentTool)); OnPropertyChanged(nameof(IsEditingEnabled)); OnPropertyChanged(nameof(CanDrawAreaRoi));
    }
    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnModelChanged; RoiEditor.PropertyChanged -= OnRoiChanged;
        if (_observedCard != null) _observedCard.PropertyChanged -= OnCardChanged;
        if (_observedTemplate != null) _observedTemplate.PropertyChanged -= OnTemplateChanged;
        if (_observedParameters != null) _observedParameters.PropertyChanged -= OnParametersChanged;
        _observedParameters = null;
        _observedTemplate = null;
        if (_ownedBlobEditor != null)
        {
            _ownedBlobEditor.Dispose();
            if (ReferenceEquals(Model.BlobEditor, _ownedBlobEditor)) Model.BlobEditor = null;
            _ownedBlobEditor = null;
        }
        RefreshCommands();
    }
}
