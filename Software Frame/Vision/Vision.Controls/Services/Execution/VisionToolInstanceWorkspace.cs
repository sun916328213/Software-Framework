using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Controls.Contracts;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Core.Tools;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>
/// 单实例的原卡片/编辑及配置文件适配，不切换工具，不保存流程文档。
/// 节点拥有本会话，视图只借用Editor；PreparedProjectSelection仍负责唯一工具/Provider/结果归属。
/// </summary>
public sealed partial class VisionToolInstanceWorkspace : ObservableObject, IVisionToolSession, IVisionProjectCommands
{
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private readonly PreparedProjectSelection _prepared;
    private readonly VisionBrowserCatalog _catalog;
    private readonly ToolEntry _entry;
    private readonly List<IDisposable> _extraProviders = [];
    private bool _closing;
    private bool _running;
    private TaskCompletionSource? _stopped;
    private Task? _dispose;
    private Action? _prepareInput;
    private VisionImage? _configuredImage;
    private VisionImage? _flowImage;
    private bool _useLocalInputImage;
    public VisionImage? FlowInputImage => _flowImage;
    private Pose2D? _configuredFixture;
    private Pose2D? ExecutionFixture => !_running && _useLocalInputImage ? _configuredFixture : Fixture;
    public VisionToolEditorModel Model { get; }
    public RoiEditorViewModel RoiEditor { get; } = new();
    public VisionToolEditorSession Editor { get; }
    public string ToolIdentity => _entry.Name;
    public object Parameters => _prepared.Parameters;
    public VisionImage? Image => Model.InputImage;
    public ToolResult? Result => _prepared.Card.LastResult;
    public bool SupportsOutputImage => _entry.ProducesOutputImage;
    public bool SupportsFixture => _prepared.SupportsSearchRegion || _prepared.LineSearchFixtureTool != null;
    public bool SupportsFixtureOutput => _entry.ProducesFixture;
    public IReadOnlyList<string> NumericOutputFields => _entry.NumericResultFields;
    public IReadOnlyList<NumericParameterDefinition> NumericInputs => _prepared.Card.NumericInputs;
    public void SetExecutionNumericParameter(NumericParameterValue? value) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        _prepared.Card.SetExecutionNumericParameter(value);
    });
    public IReadOnlyList<string> LineOutputFields => _entry.LineResultFields;
    public IReadOnlyList<LineParameterDefinition> LineInputs => _prepared.Card.LineInputs;
    public void SetExecutionLineParameter(LineParameterValue? value) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        _prepared.Card.SetExecutionLineParameter(value);
    });
    public IReadOnlyList<string> CircleOutputFields => _entry.CircleResultFields;
    public IReadOnlyList<CircleParameterDefinition> CircleInputs => _prepared.Card.CircleInputs;
    public void SetExecutionCircleParameter(CircleParameterValue? value) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        _prepared.Card.SetExecutionCircleParameter(value);
    });
    public IReadOnlyList<string> PointOutputFields => _entry.PointResultFields;
    public IReadOnlyList<PointParameterDefinition> PointInputs => _prepared.Card.PointInputs;
    public void SetExecutionPointParameter(PointParameterValue? value) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        _prepared.Card.SetExecutionPointParameter(value);
    });
    public IReadOnlyList<string> BooleanOutputFields => _entry.BooleanResultFields;
    public bool IsBusy => _running || _prepared.Card.Model.IsBusy;
    private Pose2D _referenceFixture;
    public Pose2D ReferenceFixture
    {
        get => _referenceFixture;
        set => _ui.Invoke(() =>
        {
            if (_closing || IsBusy) throw new InvalidOperationException("执行中或已关闭，不能修改基准定位。");
            if (_referenceFixture == value) return;
            _referenceFixture = value; _prepared.Card.ClearResult();
        });
    }
    private Pose2D? _fixture;
    public Pose2D? Fixture
    {
        get => _fixture;
        set => _ui.Invoke(() =>
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (IsBusy) throw new InvalidOperationException("工具执行中不能修改定位。");
            if (value != null && !SupportsFixture)
                throw new InvalidOperationException("此工具未声明搜索域能力，不能应用定位 ROI。");
            if (_fixture == value && _configuredFixture == value) return;
            _fixture = value; _configuredFixture = value;
            _prepared.Card.ClearResult();
        });
    }
    public event EventHandler? StateChanged;
    /// <summary>真实配方编辑通知；运行结果/选择变化不属于配方修改。</summary>
    public event EventHandler? ConfigurationChanged;
    private string _roiConfiguration = "[]";

    public VisionToolInstanceWorkspace(VisionBrowserCatalog catalog, ToolEntry entry) : this(catalog, entry, null) { }

    public VisionToolInstanceWorkspace(VisionBrowserCatalog catalog, ToolEntry entry, string? providerName)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        var provider = providerName ?? catalog.Providers.FirstOrDefault(p => p.IsAvailable)?.Name ?? catalog.Providers.FirstOrDefault()?.Name ?? string.Empty;
        Model = new() { ToolIdentity = entry.Name, ProviderEntries = catalog.Providers, SelectedProviderName = provider };
        _prepared = new(entry, catalog, null, provider, null, CaptureSearchRegion, ParametersChanged, PrepareInstanceInputs);
        try
        {
            Model.CurrentCard = _prepared.Card;
            Model.CurrentParameters = _prepared.Parameters;
            Model.BlobEditor = _prepared.BlobEditor;
            Model.TemplateEditor = _prepared.TemplateEditor;
            Model.SupportsProvider = _prepared.ProviderSetter != null;
            Model.SupportsTemplateInput = entry.SetTemplate != null || Model.TemplateEditor != null;
            Model.SupportsCropRegion = _prepared.CropTarget != null;
            Model.SupportsModelLoading = entry.SupportsModelLoading;
            RoiEditor.MaxRois = _prepared.SupportsSearchRegion ? 1 : int.MaxValue;
            Editor = new(Model, RoiEditor, ParametersChanged,
                () => Report(() => { if (Image != null && RoiEditor.Selected != null) { SetLocalImage(VisionImageCrop.Crop(Image, RoiEditor.Selected)); RoiEditor.Clear(); } }),
                () => Report(SetTemplate), () => Report(SetCropRegion), manageParameterEditor: false, projectCommands: this);
            Model.PropertyChanged += ModelChanged;
            _prepared.Card.Model.PropertyChanged += CardChanged;
            RoiEditor.PropertyChanged += RoiChanged;
        }
        catch (Exception error)
        {
            try { _prepared.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("工具实例准备及清理失败。", error, cleanup); }
            throw;
        }
    }
    private void Report(Action action)
    {
        try { action(); }
        catch (Exception error) { Model.ImageInfoText = error.Message; }
    }
    private void SetTemplate()
    {
        if (Image == null || RoiEditor.Selected == null) return;
        var template = VisionImageCrop.Crop(Image, RoiEditor.Selected);
        if (Model.TemplateEditor is { } editor) { editor.Capture(template); Model.ImageInfoText = editor.Status; }
        else { _entry.SetTemplate?.Invoke(Parameters, template); ParametersChanged(); }
    }
    private void SetCropRegion()
    {
        if (_prepared.CropTarget == null || RoiEditor.Selected == null) return;
        var roi = RoiEditor.Selected;
        if (roi.Kind != RoiKind.Rectangle || Math.Abs(roi.Pose.AngleDeg) > 1e-9)
            throw new InvalidOperationException("请选中轴对齐矩形。");
        _prepared.CropTarget.SetCropRegion(roi.Bounds);
        ParametersChanged();
    }
    private void PrepareInstanceInputs()
    {
        if (!_running && _useLocalInputImage)
            SetExecutionImage(_configuredImage); // 工具内明确导图后，本地执行不解析流程来源/覆盖槽。
        else _prepareInput?.Invoke(); // 流程执行或未导入本地图的调试，沿原显式绑定准备。
        if (_prepared.SearchRegionFixtureTool != null) _prepared.Card.SetExecutionSearchFixture(ReferenceFixture, ExecutionFixture);
        if (_prepared.LineSearchFixtureTool is not { } line) return;
        if (ExecutionFixture != null && RoiEditor.Rois.Count > 0) throw new InvalidOperationException("线搜索定位不使用面积ROI，请清除无关面积ROI。");
        _ = LineSearchGeometry.FollowFixture(line.LineSearchRegion, ReferenceFixture, ExecutionFixture);
        Model.ExecutionSearchRoi = ExecutionFixture is { } pose ? LineSearchGeometry.EditingFrame(line.LineSearchRegion).FollowFixture(ReferenceFixture, pose) : null;
        _prepared.Card.SetExecutionSearchFixture(ReferenceFixture, ExecutionFixture);
    }
    private SearchRegion? CaptureSearchRegion()
    {
        RoiEditor.EndManipulation(cancel: false);
        if (ExecutionFixture != null && !_prepared.SupportsSearchRegion)
            throw new InvalidOperationException("此工具未声明搜索域能力，不能应用定位 ROI。");
        if (RoiEditor.Rois.Count > 0 && !RoiEditor.HasSelection)
            throw new InvalidOperationException("请选择一个搜索 ROI；清空 ROI 列表才使用全图检测。");
        var region = SearchRegionPreparation.Capture(RoiEditor.Selected, ReferenceFixture, ExecutionFixture);
        Model.ExecutionSearchRoi = ExecutionFixture is { } pose && RoiEditor.Selected is { } baseline ? baseline.FollowFixture(ReferenceFixture, pose) : null;
        return region;
    }
    private void ParametersChanged()
    {
        _prepared.Card.ClearResult(); _prepared.Card.RefreshValidation();
        NotifyConfigurationChanged();
    }
    /// <summary>节点属性已写入原DTO后刷新专用适配；不替换工具、卡片或参数对象。</summary>
    public void RefreshParameterEditors()
    {
        if (_closing || !Editor.IsEditingEnabled) return;
        _prepared.RebuildBlobEditor(ParametersChanged);
        Model.BlobEditor = _prepared.BlobEditor;
        Model.TemplateEditor?.RefreshFromTool();
        ParametersChanged();
    }
    private void RoiChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_closing || e.PropertyName is not (nameof(RoiEditor.Selected) or nameof(RoiEditor.Count))) return;
        _prepared.Card.ClearResult(); _prepared.Card.RefreshValidation();
        var configuration = System.Text.Json.JsonSerializer.Serialize(RoiEditor.Rois, VisionToolConfiguration.JsonOptions);
        if (configuration == _roiConfiguration) return;
        _roiConfiguration = configuration;
        NotifyConfigurationChanged();
    }
    private void CardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolCardModel.IsBusy) or nameof(ToolCardModel.LastResult) or nameof(ToolCardModel.IsReadOnly))
        { _fileRevision++; RefreshFileCommands(); }
        if (Result == null && (e.PropertyName == nameof(ToolCardModel.LastResult) || !_prepared.Card.Model.IsBusy)) Model.ExecutionSearchRoi = null;
        if (e.PropertyName is nameof(ToolCardModel.LastResult) or nameof(ToolCardModel.IsBusy)) StateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_closing || e.PropertyName != nameof(Model.SelectedProviderName) || _prepared.ProviderSetter == null) return;
        Report(() =>
        {
            var candidate = _catalog.CreateProvider(Model.SelectedProviderName);
            // Setter也可能先修改工具再失败；沿用原浏览工作区的归属，保留候选到关闭后统一清理。
            if (candidate is IDisposable owned) _extraProviders.Add(owned);
            _prepared.ProviderSetter(candidate);
            ParametersChanged();
        });
    }
    public void SetImage(VisionImage? image) => _ui.Invoke(() =>
    {
        SetConfiguredImage(image);
        _flowImage = image; _useLocalInputImage = false;
    });
    internal void SetTaskImage(VisionImage? image) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        if (IsBusy) throw new InvalidOperationException("执行中不能更换任务输入。");
        _flowImage = image;
        if (!_useLocalInputImage) SetConfiguredImage(image);
        else SetExecutionImage(image);
    });
    private void SetLocalImage(VisionImage? image)
    {
        SetConfiguredImage(image);
        _useLocalInputImage = true;
    }
    private void SetConfiguredImage(VisionImage? image)
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        if (IsBusy) throw new InvalidOperationException("工具执行中不能更换输入。");
        _fileRevision++;
        _configuredImage = image;
        _prepared.Card.SetImage(image);
        Model.InputImage = image;
        Model.ImageInfoText = image == null ? "无图像" : $"{image.Width}×{image.Height}，{image.Format}";
    }
    public void SetPreparation(Action? prepare) => _prepareInput = prepare;
    public void SetExecutionImage(VisionImage? image) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        _prepared.Card.SetImage(image);
        Model.InputImage = image;
        _fileRevision++;
        ExecutionImageIdentity = image == null ? null : VisionToolConfiguration.ImageIdentity(image);
        Model.ImageInfoText = image == null ? "无本次执行输入" : $"{image.Width}×{image.Height}，{image.Format}";
    });
    public void SetExecutionFixture(Pose2D? pose) => _ui.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closing, this);
        if (pose != null && !SupportsFixture) throw new InvalidOperationException("此工具未声明搜索ROI定位能力。");
        _fixture = pose;
    });
    public string? ExecutionImageIdentity { get; private set; }
    public VisionImage? ConfiguredImage => _configuredImage;

    public VisionToolConfiguration ExportConfiguration()
    {
        _ui.VerifyAccess();
        RoiEditor.EndManipulation(cancel: false);
        var input = VisionToolConfiguration.Snapshot(_configuredImage);
        return new()
        {
            ToolIdentity = ToolIdentity, Parameters = VisionProjectCodec.EncodeParameters(Parameters),
            Provider = Model.SelectedProviderName, Rois = RoiEditor.Rois.Select(CloneRoi).ToList(),
            SelectedRoiIndex = RoiEditor.HasSelection ? RoiEditor.Rois.IndexOf(RoiEditor.Selected!) : -1,
            ReferenceFixture = ReferenceFixture, Fixture = _prepared.LineSearchFixtureTool == null && _prepared.SearchRegionFixtureTool == null ? Fixture : null,
            InputImage = input, UseLocalInputImage = _useLocalInputImage,
            ImageSha256 = input == null ? "" : VisionToolConfiguration.Hash(input),
            TemplateImage = VisionToolConfiguration.Snapshot(_prepared.TemplateTarget?.TemplateImage),
            TrainingParameters = _prepared.TemplateTarget?.TrainingParameters is { } training ? VisionProjectCodec.EncodeParameters(training) : null,
            ModelReference = Model.TemplateEditor?.ModelReferencePath ?? ""
        };
    }
    private static Roi CloneRoi(Roi roi) => System.Text.Json.JsonSerializer.Deserialize<Roi>(
        System.Text.Json.JsonSerializer.Serialize(roi, VisionToolConfiguration.JsonOptions), VisionToolConfiguration.JsonOptions)!;

    public void ApplyConfiguration(VisionToolConfiguration configuration)
    {
        _ui.VerifyAccess();
        configuration.Validate();
        if (_closing || IsBusy) throw new InvalidOperationException("执行中或已关闭，不能恢复配置。");
        if (configuration.ToolIdentity != ToolIdentity) throw new InvalidOperationException("工具身份与配置不符。");
        if (configuration.Provider.Length > 0 && !_catalog.Providers.Any(p => p.Name == configuration.Provider))
            throw new InvalidOperationException("配置引用的Provider不存在。");
        if (_prepared.SupportsSearchRegion && configuration.Rois.Count > 1) throw new InvalidOperationException("此工具最多一个搜索ROI。");
        var applyParameters = VisionProjectCodec.PrepareParameters(configuration.Parameters, Parameters);
        var applyTraining = _prepared.TemplateTarget?.TrainingParameters is { } trainingParameters
            ? VisionProjectCodec.PrepareParameters(configuration.TrainingParameters, trainingParameters) : () => { };
        var inputSnapshot = VisionToolConfiguration.Snapshot(configuration.InputImage);
        var templateSnapshot = VisionToolConfiguration.Snapshot(configuration.TemplateImage);
        if (_prepared.TemplateTarget is not { } && (templateSnapshot != null || configuration.TrainingParameters != null || configuration.ModelReference.Length > 0))
            throw new InvalidOperationException("当前工具不支持模板配置。");
        // 模板像素格式由原工具校验；失败时尚未改写参数/ROI/输入。
        if (_prepared.TemplateTarget is { } target)
        {
            if (templateSnapshot != null) target.SetTemplate(templateSnapshot);
            else target.ClearModel();
            applyTraining();
        }
        applyParameters();
        _prepared.RebuildBlobEditor(ParametersChanged);
        Model.BlobEditor = _prepared.BlobEditor;
        Model.SelectedProviderName = configuration.Provider;
        RoiEditor.Clear();
        foreach (var roi in configuration.Rois)
            RoiEditor.Rois.Add(CloneRoi(roi));
        RoiEditor.Selected = configuration.SelectedRoiIndex < 0 ? null : RoiEditor.Rois[configuration.SelectedRoiIndex];
        ReferenceFixture = configuration.ReferenceFixture;
        Fixture = configuration.Fixture;
        SetConfiguredImage(inputSnapshot);
        _useLocalInputImage = configuration.UseLocalInputImage;
        if (!_useLocalInputImage) _flowImage = inputSnapshot;
        if (_prepared.TemplateTarget != null)
        {
            Model.TemplateEditor?.RefreshFromTool();
            Model.TemplateEditor?.RestoreReference(configuration.ModelReference);
        }
        ParametersChanged();
    }

    public void SetReadOnly(bool value) => _ui.Invoke(() => _prepared.Card.Model.IsReadOnly = value || _closing);
    public void ClearResult() => _ui.Invoke(() =>
    {
        Model.ExecutionSearchRoi = null; // 尚无结果的后台调用也必须撤下失效定位叠加。
        _prepared.Card.ClearResult();
    });
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Task execution = Task.CompletedTask;
        _ui.Invoke(() =>
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (IsBusy) throw new InvalidOperationException("同一视觉实例不能重入执行。");
            _prepared.Card.ClearResult(); // 即使调用前已取消，也撤下旧成功结果/端口，不误用上帧。
            cancellationToken.ThrowIfCancellationRequested();
            _running = true;
            RefreshFileCommands();
            _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            try { execution = _prepared.Card.RunAsync(); }
            catch { _running = false; RefreshFileCommands(); _stopped.TrySetResult(); throw; }
        });
        try
        {
            await execution.ConfigureAwait(false);
            _ui.Invoke(() =>
            {
                if (_closing || cancellationToken.IsCancellationRequested)
                {
                    _prepared.Card.ClearResult();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new ObjectDisposedException(nameof(VisionToolInstanceWorkspace));
                }
                if (Result?.Success != true) throw new InvalidOperationException($"{ToolIdentity}：{_prepared.Card.StatusText}");
            });
        }
        finally
        {
            _ui.Invoke(() =>
            {
                _running = false;
                RefreshFileCommands();
                try { StateChanged?.Invoke(this, EventArgs.Empty); }
                finally { _stopped!.TrySetResult(); }
            });
        }
    }
    public ValueTask DisposeAsync()
    {
        _ui.Invoke(() =>
        {
            if (_dispose != null) return;
            _closing = true;
            Model.IsClosed = true;
            RefreshFileCommands();
            Exception? error = null;
            try { _prepared.Card.BeginClose(); }
            catch (Exception closeError) { error = closeError; }
            _dispose = ReleaseAsync(_stopped?.Task ?? Task.CompletedTask, _prepared.Card.ExecutionTask, error);
        });
        return new(_dispose!);
    }
    private async Task ReleaseAsync(Task stopped, Task cardExecution, Exception? closeError)
    {
        List<Exception> errors = [];
        if (closeError != null) errors.Add(closeError);
        try { await Task.WhenAll(stopped, cardExecution).ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        _ui.Invoke(() =>
        {
            Model.PropertyChanged -= ModelChanged;
            _prepared.Card.Model.PropertyChanged -= CardChanged;
            RoiEditor.PropertyChanged -= RoiChanged;
            void Release(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            Release(() => RoiEditor.EndManipulation(cancel: true));
            Release(Editor.Dispose);
            Release(_prepared.Dispose);
            foreach (var provider in _extraProviders) Release(provider.Dispose);
            _extraProviders.Clear();
            Release(() => StateChanged?.Invoke(this, EventArgs.Empty));
        });
        if (errors.Count > 0) throw new AggregateException($"{ToolIdentity}关闭失败。", errors);
    }
}
