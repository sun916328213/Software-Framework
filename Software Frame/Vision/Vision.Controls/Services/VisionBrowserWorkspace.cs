// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：VisionBrowserCatalog、VisionBrowserWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel、Vision.Controls.Models、Vision.Controls.ViewModels、Vision.Core.AI、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>宿主提供具体后端能力；Controls 仅使用标准图像和工具契约。</summary>
public sealed record VisionBrowserCatalog(
    IReadOnlyList<ToolEntry> Tools,
    IReadOnlyList<ProviderEntry> Providers,
    Func<string, IAIProvider> CreateProvider,
    Func<VisionImage?> LoadImage,
    Func<VisionImage, string> Compare);

/// <summary>
/// VisionBrowserWorkspace 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class VisionBrowserWorkspace : IDisposable
{
    private readonly VisionBrowserCatalog _catalog;
    private VisionImage? _image;
    private Action<IAIProvider>? _providerSetter;
    private readonly List<IDisposable> _ownedProviders = [];
    private bool _disposed;
    public VisionBrowserPageModel Model { get; }
    public RoiEditorViewModel RoiEditor { get; } = new();
    /// <summary>
    /// 初始化 VisionBrowserWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof、FirstOrDefault、AddRectangle；具体条件和先后次序见方法体。
    /// </summary>
    public VisionBrowserWorkspace(VisionBrowserCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Model = new() { ToolEntries = catalog.Tools, ProviderEntries = catalog.Providers };
        Model.PropertyChanged += OnModelChanged;
        Model.SelectedProviderName = catalog.Providers.FirstOrDefault(p => p.IsAvailable)?.Name ?? catalog.Providers.FirstOrDefault()?.Name ?? string.Empty;
        RoiEditor.AddRectangle(32, 32, 100, 60);
        Model.SelectedEntry = catalog.Tools.FirstOrDefault();
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 nameof、SelectEntry、ApplyProvider；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Model.SelectedEntry)) SelectEntry(Model.SelectedEntry);
        if (e.PropertyName == nameof(Model.SelectedProviderName)) ApplyProvider();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SelectEntry。
    /// 正文中的关键协作调用包括 ReleaseProviders、Create、SetImage、RefreshValidation、ApplyProvider；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void SelectEntry(ToolEntry? entry)
    {
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
            Model.CurrentCard = new ToolCardViewModel(tool);
            Model.CurrentCard.SetImage(_image);
            Model.CurrentCard.RefreshValidation();
            ApplyProvider();
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
            Model.ImageInfoText = $"已加载：{image.Width}×{image.Height}";
        }
        catch (Exception ex) { Model.ImageInfoText = $"加载失败：{ex.Message}"; }
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 UseDemoImage。
    /// 正文中的关键协作调用包括 ThrowIf、SetImage、FromBuffer；具体条件和先后次序见方法体。
    /// </summary>
    public void UseDemoImage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var pixels = new byte[64 * 64];
        for (var y = 10; y < 30; y++) for (var x = 10; x < 30; x++) pixels[y * 64 + x] = 255;
        SetImage(VisionImage.FromBuffer(64, 64, PixelFormat.Mono8, pixels, source: "workbench-demo"));
        Model.ImageInfoText = "合成图：64×64 白矩形";
    }
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetImage。
    /// </summary>
    private void SetImage(VisionImage image) { _image = image; Model.CurrentCard?.SetImage(image); Model.HasImage = true; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CompareBlob。
    /// 正文中的关键协作调用包括 ThrowIf、Compare；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void CompareBlob()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_image == null) { Model.CompareText = "无图像：先加载图像或使用合成图。"; return; }
        try { Model.CompareText = _catalog.Compare(_image); }
        catch (Exception ex) { Model.CompareText = $"对比异常：{ex.Message}"; }
    }
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunBenchmark。
    /// 正文中的关键协作调用包括 ThrowIf、Run；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void RunBenchmark()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Model.CurrentCard == null) { Model.BenchmarkText = "未选中 Tool。"; return; }
        try
        {
            for (var i = 0; i < 5; i++) Model.CurrentCard.Run();
            Model.BenchmarkText = $"Benchmark：5 次，末次 {Model.CurrentCard.ElapsedMs} ms（{Model.CurrentCard.ResultSummary}）";
        }
        catch (Exception ex) { Model.BenchmarkText = $"Benchmark 异常：{ex.Message}"; }
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 ReleaseProviders；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.IsClosed = true;
        Model.PropertyChanged -= OnModelChanged;
        Model.CurrentCard?.Dispose();
        ReleaseProviders();
        Model.CurrentCard = null;
        Model.CurrentParameters = null;
        Model.HasImage = false;
        _image = null;
        _providerSetter = null;
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
