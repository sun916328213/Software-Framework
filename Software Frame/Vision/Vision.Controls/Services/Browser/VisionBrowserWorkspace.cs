// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：VisionBrowserWorkspace。宿主能力契约VisionBrowserCatalog在Contracts/Hosting维护。
// 直接导入的协作命名空间包括：System.ComponentModel、Vision.Controls.Models、Vision.Controls.ViewModels、Vision.Core.AI、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;
using System.IO;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>
/// VisionBrowserWorkspace 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class VisionBrowserWorkspace : IDisposable
{
    private readonly IVisionProjectFileStore _projectFiles;
    private bool _restoringProject;
    private readonly VisionBrowserCatalog _catalog;
    private VisionImage? _image;
    private Action<IAIProvider>? _providerSetter;
    private ICropRegionTool? _cropTarget;
    private readonly List<IDisposable> _ownedProviders = [];
    private bool _disposed;
    public bool CanSetTemplate => Model.SupportsTemplateInput;
    public VisionBrowserPageModel Model { get; }
    public RoiEditorViewModel RoiEditor { get; } = new();
    /// <summary>
    /// 初始化 VisionBrowserWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof、FirstOrDefault；具体条件和先后次序见方法体。
    /// </summary>
    public VisionBrowserWorkspace(VisionBrowserCatalog catalog) : this(catalog, null) { }

    public VisionBrowserWorkspace(VisionBrowserCatalog catalog, IVisionProjectFileStore? projectFiles)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _projectFiles = projectFiles ?? new VisionProjectFileStore();
        Model = new() { ToolEntries = catalog.Tools, ProviderEntries = catalog.Providers };
        Model.PropertyChanged += OnModelChanged;
        Model.SelectedProviderName = catalog.Providers.FirstOrDefault(p => p.IsAvailable)?.Name ?? catalog.Providers.FirstOrDefault()?.Name ?? string.Empty;
        RoiEditor.PropertyChanged += OnRoiChanged;
        Model.SelectedEntry = catalog.Tools.FirstOrDefault(t => t.IsDefault) ?? catalog.Tools.FirstOrDefault();
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 nameof、SelectEntry、ApplyProvider；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_restoringProject) return;
        if (e.PropertyName == nameof(Model.SelectedEntry)) SelectEntry(Model.SelectedEntry);
        if (e.PropertyName == nameof(Model.SelectedProviderName)) ApplyProvider();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SelectEntry。
    /// 正文中的关键协作调用包括 ReleaseProviders、Create、SetImage、RefreshValidation、ApplyProvider；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void OnRoiChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && e.PropertyName == nameof(RoiEditor.Selected) && Model.CurrentCard?.SupportsSearchRegion == true)
        {
            Model.CurrentCard.ClearResult();
        }
    }
    private Vision.Core.Roi.SearchRegion? CaptureSearchRegion()
    {
        RoiEditor.EndManipulation(cancel: false);
        if (RoiEditor.Rois.Count == 0) return null;
        if (!RoiEditor.HasSelection) throw new InvalidOperationException("请选择一个搜索 ROI；清空 ROI 列表才使用全图检测。");
        return new Vision.Core.Roi.SearchRegion(RoiEditor.Selected!);
    }
    private void SelectEntry(ToolEntry? entry)
    {
        RoiEditor.EndManipulation(cancel: true);
        RoiEditor.MaxRois = int.MaxValue;
        Model.TemplateEditor?.Dispose();
        Model.TemplateEditor = null;
        Model.SupportsProvider = false;
        Model.SupportsTemplateInput = false;
        Model.SupportsModelLoading = false;
        Model.SupportsCropRegion = false;
        _cropTarget = null;
        Model.BlobEditor?.Dispose();
        Model.BlobEditor = null;
        Model.CurrentCard?.Dispose();
        ReleaseProviders();
        Model.CurrentCard = null;
        Model.CurrentParameters = null;
        _providerSetter = null;
        if (entry == null) return;
        try
        {
            var (tool, parameters, setter) = entry.Create();
            _providerSetter = setter;
            Model.CurrentParameters = parameters;
            Model.CurrentCard = new ToolCardViewModel(tool, CaptureSearchRegion);
            RoiEditor.MaxRois = Model.CurrentCard.SupportsSearchRegion ? 1 : int.MaxValue;
            Model.CurrentCard.SetImage(_image);
            ApplyProvider();
            Model.CurrentCard.RefreshValidation();
            Model.BlobEditor = BlobParametersEditorViewModel.TryCreate(parameters, ParametersChanged);
            Model.SupportsProvider = setter != null;
            if (tool.TemplateTool is { } templateTool)
                Model.TemplateEditor = new(templateTool, ParametersChanged, _catalog.PickTemplateModelPath,
                    () => Model.CurrentCard is { Model.IsBusy: false, Model.IsReadOnly: false });
            _cropTarget = tool.CropRegionTool;
            Model.SupportsCropRegion = _cropTarget != null;
            Model.SupportsTemplateInput = entry.SetTemplate != null || Model.TemplateEditor != null;
            Model.SupportsModelLoading = entry.SupportsModelLoading;
        }
        catch (Exception ex) { Model.CompareText = $"工具加载失败：{ex.Message}"; }
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplyProvider。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、CreateProvider、Add、_providerSetter；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void ApplyProvider()
    {
        if (_providerSetter == null || string.IsNullOrEmpty(Model.SelectedProviderName)) return;
        try
        {
            var provider = _catalog.CreateProvider(Model.SelectedProviderName);
            if (provider is IDisposable disposable) _ownedProviders.Add(disposable);
            _providerSetter(provider);
            Model.CurrentCard?.ClearResult();
            Model.CurrentCard?.RefreshValidation();
        }
        catch (Exception ex) { Model.CompareText = $"Provider 切换失败：{ex.Message}"; }
    }
    /// <summary>
    /// 执行加载操作；对应入口为 LoadImage。
    /// 正文中的关键协作调用包括 ThrowIf、LoadImage、SetImage；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void LoadImage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var image = _catalog.LoadImage();
            if (image == null) return;
            SetImage(image);
            Model.ImageInfoText = $"已加载：{image.Width}×{image.Height}，{image.Format}；{image.Source}";
        }
        catch (Exception ex) { Model.ImageInfoText = $"加载失败：{ex.Message}"; }
    }
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetImage。
    /// </summary>
    internal void SetImage(VisionImage image)
    {
        RoiEditor.EndManipulation(cancel: true);
        _image = image;
        Model.InputImage = image;
        Model.CurrentCard?.SetImage(image);
        Model.CompareText = "—";
    }

    public void SaveProject()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(Model.CurrentProjectPath) || Model.IsLegacyProject) { SaveProjectAs(); return; }
        SaveTo(Model.CurrentProjectPath);
    }

    public void SaveProjectAs()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_catalog.PickProjectPath == null) { Model.ImageInfoText = "当前宿主未提供工程保存入口。"; return; }
        if (Model.CurrentCard == null || Model.CurrentCard.Model.IsBusy) return;
        var target = Model.CurrentCard;
        try
        {
            var path = _catalog.PickProjectPath(true);
            if (_disposed || !ReferenceEquals(target, Model.CurrentCard) || target.Model.IsBusy) return;
            if (!string.IsNullOrWhiteSpace(path)) SaveTo(Path.GetFullPath(path));
        }
        catch (Exception error) { Model.ImageInfoText = $"另存为失败：{error.Message}"; }
    }

    private void SaveTo(string path)
    {
        if (Model.CurrentCard == null || Model.CurrentCard.Model.IsBusy) return;
        try
        {
            _projectFiles.WriteProject(path, ExportProjectJson(), _image);
            Model.CurrentProjectPath = Path.GetFullPath(path);
            Model.IsLegacyProject = false;
            Model.ImageInfoText = $"工程已保存（含当前输入快照）：{path}";
        }
        catch (Exception error) { Model.ImageInfoText = $"保存工程失败：{error.Message}"; }
    }

    public string ExportProjectJson()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = Model.CurrentParameters ?? throw new InvalidOperationException("请先选择工具再保存工程。");
        if (Model.CurrentCard == null) throw new InvalidOperationException("请先选择工具再保存工程。");
        RoiEditor.EndManipulation(cancel: false);
        var configuration = new Vision.Core.Tools.VisionToolConfiguration
        {
            ToolIdentity = Model.SelectedEntry?.Name ?? string.Empty,
            Provider = Model.SelectedProviderName,
            Parameters = VisionProjectCodec.EncodeParameters(parameters),
            Rois = RoiEditor.Rois.ToList(),
            SelectedRoiIndex = RoiEditor.HasSelection ? RoiEditor.Rois.IndexOf(RoiEditor.Selected!) : -1,
            TemplateImage = Vision.Core.Tools.VisionToolConfiguration.Snapshot(Model.TemplateEditor?.TemplateImage),
            TrainingParameters = Model.TemplateEditor?.TrainingParameters is { } training ? VisionProjectCodec.EncodeParameters(training) : null,
            ModelReference = Model.TemplateEditor?.ModelReferencePath ?? ""
        };
        return VisionProjectCodec.Encode(VisionProjectCodec.CreateProject(configuration,
            Model.SelectedImageView, Model.ShowResultBounds, Model.ShowResultCenters, Model.InputImage?.Source ?? string.Empty));
    }

    public void ImportProject()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_catalog.PickProjectPath == null) { Model.ImageInfoText = "当前宿主未提供工程导入入口。"; return; }
        try
        {
            var path = _catalog.PickProjectPath(false);
            if (string.IsNullOrWhiteSpace(path)) return;
            var fullPath = Path.GetFullPath(path);
            var document = _projectFiles.ReadProject(fullPath);
            ImportDocument(document, fullPath);
            Model.CurrentProjectPath = fullPath;
            Model.IsLegacyProject = !document.IsPackage;
        }
        catch (Exception ex) { Model.ImageInfoText = $"导入工程失败：{ex.Message}"; }
    }

    public void ImportProjectJson(string json)
    {
        ImportDocument(new(json), null);
        Model.CurrentProjectPath = null; // 原始JSON入口不是文件打开，不沿用旧工程路径。
        Model.IsLegacyProject = true;
    }

    private void ImportDocument(VisionProjectDocument document, string? path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Model.CurrentCard?.Model.IsBusy == true) throw new InvalidOperationException("执行中不能导入工程。");
        var project = VisionProjectCodec.Decode(document.Json);
        if (project.InputImage != null && document.Image == null)
            throw new InvalidDataException("工程声明了输入快照，但没有提供包内像素。");
        var entry = _catalog.Tools.FirstOrDefault(t => string.Equals(t.Name, project.Tool, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"工程引用的工具不存在：{project.Tool}。");
        var provider = project.Provider ?? string.Empty;
        if (provider.Length > 0 && !_catalog.Providers.Any(p => p.Name == provider))
            throw new InvalidDataException($"工程引用的 Provider 不存在：{provider}。");
        if (provider.Length == 0) provider = Model.SelectedProviderName;

        using var prepared = new PreparedProjectSelection(entry, _catalog, project.Parameters, provider,
            _image, CaptureSearchRegion, ParametersChanged);
        if (prepared.TemplateTarget is { } template)
        {
            if (project.TemplateImage != null) template.SetTemplate(Vision.Core.Tools.VisionToolConfiguration.Snapshot(project.TemplateImage)!);
            if (template.TrainingParameters is { } training) VisionProjectCodec.ApplyParameters(project.TrainingParameters, training);
            prepared.TemplateEditor?.RefreshFromTool();
            prepared.TemplateEditor?.RestoreReference(project.ModelReference);
        }
        else if (project.TemplateImage != null || project.TrainingParameters != null || project.ModelReference.Length > 0)
            throw new InvalidDataException("工具不支持工程中的模板配置。");
        var maxRois = prepared.SupportsSearchRegion ? 1 : int.MaxValue;
        if (project.Rois.Count > maxRois)
            throw new InvalidDataException("当前工具仅允许一个搜索 ROI，不能静默丢弃工程中的其它 ROI。");
        var image = document.IsPackage ? document.Image : _image;
        var note = document.IsPackage ? image == null ? "工程未包含输入图像。" : $"已恢复包内输入：{image.Width}×{image.Height}，{image.Format}"
            : image == null ? "工程未记录图像路径，请重新打开图片。" : "工程未记录图像路径，保留当前输入图像。";
        if (!document.IsPackage && !string.IsNullOrWhiteSpace(project.ImageSource) && _catalog.LoadImageByPath != null)
        {
            try
            {
                var source = Path.IsPathRooted(project.ImageSource) ? project.ImageSource
                    : path != null ? Path.GetFullPath(project.ImageSource, Path.GetDirectoryName(path)!) : project.ImageSource;
                image = _catalog.LoadImageByPath(source) ?? image;
                note = image == null ? "工程图像未加载，请重新打开图片。" : $"已加载：{image.Width}×{image.Height}，{image.Format}；{image.Source}";
            }
            catch (Exception error) { note = $"工程图像加载失败，保留当前输入：{error.Message}"; }
        }

        // 所有预期失败发生在提交前。旧会话保留到候选状态完整发布后再释放。
        var previousTemplate = Model.TemplateEditor;
        var previousBlob = Model.BlobEditor;
        var previousCard = Model.CurrentCard;
        var previousProviders = _ownedProviders.ToArray();
        _restoringProject = true;
        try
        {
            RoiEditor.EndManipulation(cancel: true);
            _providerSetter = prepared.ProviderSetter;
            _cropTarget = prepared.CropTarget;
            _ownedProviders.Clear();
            if (prepared.Provider is IDisposable owned) _ownedProviders.Add(owned);
            prepared.TransferOwnership();
            Model.SelectedEntry = entry;
            Model.SelectedProviderName = provider;
            Model.CurrentParameters = prepared.Parameters;
            Model.CurrentCard = prepared.Card;
            Model.BlobEditor = prepared.BlobEditor;
            Model.TemplateEditor = prepared.TemplateEditor;
            Model.SupportsProvider = prepared.ProviderSetter != null;
            Model.SupportsTemplateInput = entry.SetTemplate != null || prepared.TemplateEditor != null;
            Model.SupportsModelLoading = entry.SupportsModelLoading;
            Model.SupportsCropRegion = _cropTarget != null;
            _image = image;
            Model.InputImage = image;
            prepared.Card.SetImage(image);
            RoiEditor.Clear();
            RoiEditor.MaxRois = maxRois;
            foreach (var roi in project.Rois) RoiEditor.Rois.Add(roi);
            if (project.SelectedRoiIndex >= 0) RoiEditor.Selected = RoiEditor.Rois[project.SelectedRoiIndex];
            Model.SelectedImageView = project.ImageView;
            Model.ShowResultBounds = project.ShowResultBounds;
            Model.ShowResultCenters = project.ShowResultCenters;
            Model.CompareText = "—";
            Model.ImageInfoText = note;
        }
        finally
        {
            _restoringProject = false;
            List<string> errors = [];
            void Release(Action release) { try { release(); } catch (Exception error) { errors.Add(error.Message); } }
            Release(() => previousTemplate?.Dispose());
            Release(() => previousBlob?.Dispose());
            Release(() => previousCard?.Dispose());
            foreach (var owned in previousProviders) Release(owned.Dispose);
            if (errors.Count > 0) Model.ImageInfoText += $"；旧会话清理失败：{string.Join("；", errors)}";
        }
    }

    public void ParametersChanged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Model.CurrentCard?.ClearResult();
        Model.CurrentCard?.RefreshValidation();
        Model.TemplateEditor?.RefreshFromTool();
    }

    public void CropInput()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            if (_image == null || RoiEditor.Selected == null) return;
            SetImage(VisionImageCrop.Crop(_image, RoiEditor.Selected));
            RoiEditor.Clear();
            Model.ImageInfoText = $"裁剪输入：{_image!.Width}×{_image.Height}；结果使用此裁剪图的局部坐标，重新加载可恢复原图。";
        }
        catch (Exception ex) { Model.ImageInfoText = $"裁剪失败：{ex.Message}"; }
    }

    public void SetTemplateFromRoi()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanSetTemplate || _image == null || RoiEditor.Selected == null || Model.CurrentParameters == null) return;
        try
        {
            var template = VisionImageCrop.Crop(_image, RoiEditor.Selected);
            if (Model.TemplateEditor is { } editor)
            {
                if (!editor.Capture(template)) { Model.ImageInfoText = editor.Status; return; }
            }
            else
            {
                Model.SelectedEntry!.SetTemplate!(Model.CurrentParameters, template);
                ParametersChanged();
            }
            Model.ImageInfoText = $"模板已设置：{template.Width}×{template.Height}；搜索图保持不变。";
        }
        catch (Exception ex) { Model.ImageInfoText = $"模板设置失败：{ex.Message}"; }
    }
    /// <summary>
    /// 将选中的轴对齐矩形写入当前工具的裁剪配置，不执行工具。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void SetCropRegionFromRoi()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cropTarget == null || RoiEditor.Selected == null) return;
        try
        {
            var roi = RoiEditor.Selected;
            if (roi.Kind != Vision.Core.Roi.RoiKind.Rectangle || Math.Abs(roi.Pose.AngleDeg) > 1e-9)
                throw new InvalidOperationException("请选中轴对齐矩形。");
            _cropTarget.SetCropRegion(roi.Bounds);
            ParametersChanged();
            Model.ImageInfoText = "裁剪区域已设置；执行产生独立输出，输入图保持不变。";
        }
        catch (Exception error) { Model.ImageInfoText = $"设置裁剪区域失败：{error.Message}"; }
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 ReleaseProviders；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception> errors = [];
        void Release(Action release)
        {
            try { release(); }
            catch (Exception error) { errors.Add(error); }
        }
        Release(() => RoiEditor.EndManipulation(cancel: true));
        Release(() => Model.TemplateEditor?.Dispose());
        Model.TemplateEditor = null;
        Model.IsClosed = true;
        Model.SupportsProvider = false;
        Model.SupportsTemplateInput = false;
        Model.SupportsModelLoading = false;
        Model.SupportsCropRegion = false;
        _cropTarget = null;
        Model.PropertyChanged -= OnModelChanged;
        RoiEditor.PropertyChanged -= OnRoiChanged;
        Release(() => Model.BlobEditor?.Dispose());
        Model.BlobEditor = null;
        Release(() => Model.CurrentCard?.Dispose());
        Release(ReleaseProviders); // 工具释放失败也不能跳过创建其会话的Provider。
        Model.CurrentCard = null;
        Model.CurrentParameters = null;
        Model.HasImage = false;
        _image = null;
        Model.InputImage = null;
        _providerSetter = null;
        if (errors.Count > 0) throw new AggregateException("视觉工作区关闭失败。", errors);
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseProviders。
    /// 正文中的关键协作调用包括 Dispose、Clear；具体条件和先后次序见方法体。
    /// </summary>
    private void ReleaseProviders()
    {
        // 先释放卡片会话，再释放创建该会话的 Provider。
        foreach (var provider in _ownedProviders) provider.Dispose();
        _ownedProviders.Clear();
    }
}
