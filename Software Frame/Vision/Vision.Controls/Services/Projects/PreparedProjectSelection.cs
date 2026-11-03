using System.Text.Json;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Roi;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>提交前独占候选资源；不发布到当前页面，不执行算法/加载模型。</summary>
internal sealed class PreparedProjectSelection : IDisposable
{
    private readonly IExecutableTool _tool;
    private bool _released;
    public object Parameters { get; }
    public Action<IAIProvider>? ProviderSetter { get; }
    public IAIProvider? Provider { get; private set; }
    public ToolCardViewModel Card { get; private set; } = null!;
    public BlobParametersEditorViewModel? BlobEditor { get; private set; }
    public TemplateEditorViewModel? TemplateEditor { get; private set; }
    public ICropRegionTool? CropTarget { get; private set; }
    public bool SupportsSearchRegion { get; private set; }
    public Vision.Core.Presentation.ILineSearchFixtureTool? LineSearchFixtureTool => _tool.LineSearchFixtureTool;
    public Vision.Core.Presentation.ISearchRegionFixtureTool? SearchRegionFixtureTool => _tool.SearchRegionTool as Vision.Core.Presentation.ISearchRegionFixtureTool;
    public Vision.Core.Presentation.ITemplateTool? TemplateTarget => _tool.TemplateTool;

    public PreparedProjectSelection(ToolEntry entry, VisionBrowserCatalog catalog, JsonElement? parameters,
        string provider, VisionImage? image, Func<SearchRegion?> captureRegion, Action changed, Action? prepareInput = null)
    {
        (_tool, Parameters, ProviderSetter) = entry.Create();
        try
        {
            VisionProjectCodec.ApplyParameters(parameters, Parameters);
            CropTarget = _tool.CropRegionTool;
            SupportsSearchRegion = _tool.SearchRegionTool != null;
            if (ProviderSetter != null && provider.Length > 0)
            {
                Provider = catalog.CreateProvider(provider);
                ProviderSetter(Provider);
            }
            Card = new(_tool, captureRegion, prepareInput);
            Card.SetImage(image);
            Card.RefreshValidation();
            BlobEditor = BlobParametersEditorViewModel.TryCreate(Parameters, changed);
            if (_tool.TemplateTool is { } template)
                TemplateEditor = new(template, changed, catalog.PickTemplateModelPath, () => !Card.Model.IsBusy && !Card.Model.IsReadOnly);
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("工程准备失败，候选资源清理也失败。", error, cleanup); }
            throw;
        }
    }
    /// <summary>配置已写入原DTO后重建借用编辑适配，不改写DTO或重复拥有算法参数。</summary>
    public void RebuildBlobEditor(Action changed)
    {
        var candidate = BlobParametersEditorViewModel.TryCreate(Parameters, changed);
        BlobEditor?.Dispose();
        BlobEditor = candidate;
    }
    public void TransferOwnership() => _released = true;
    public void Dispose()
    {
        if (_released) return;
        _released = true;
        List<Exception> errors = [];
        void Release(Action release) { try { release(); } catch (Exception error) { errors.Add(error); } }
        Release(() => TemplateEditor?.Dispose());
        Release(() => BlobEditor?.Dispose());
        Release(() => { if (Card != null) Card.Dispose(); else _tool.Dispose(); });
        Release(() => (Provider as IDisposable)?.Dispose());
        if (errors.Count > 0) throw new AggregateException("候选资源清理失败。", errors);
    }
}
