using Device.Core.Devices;

namespace Device.Core.LabelPrinting;

/// <summary>
/// 标签模板打印软件的公共接口，不暴露厂商文档、图像或消息类型。
/// </summary>
public interface ILabelPrintEngine : IAsyncDisposable
{
    /// <summary>打印引擎在应用中的唯一名称。</summary>
    string Name { get; }

    /// <summary>创建打印引擎时使用的配置快照。</summary>
    LabelPrintEngineOptions Options { get; }

    /// <summary>当前打开的模板路径；没有模板时为 <see langword="null"/>。</summary>
    string? TemplatePath { get; }

    /// <summary>按需读取操作系统和打印软件可见的打印机名称。</summary>
    ValueTask<DeviceResult<IReadOnlyList<string>>> GetInstalledPrintersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>打开标签模板，并关闭此前打开的模板。</summary>
    ValueTask<DeviceResult> OpenTemplateAsync(
        string templatePath,
        CancellationToken cancellationToken = default);

    /// <summary>读取当前模板全部命名字段。</summary>
    ValueTask<DeviceResult<IReadOnlyList<LabelTemplateField>>> GetFieldsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>批量设置当前模板中的命名字段。</summary>
    ValueTask<DeviceResult> SetFieldsAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);

    /// <summary>将当前模板渲染为 PNG 预览图。</summary>
    ValueTask<DeviceResult<LabelPreview>> RenderPreviewAsync(
        CancellationToken cancellationToken = default);

    /// <summary>使用指定或默认打印机打印当前模板。</summary>
    ValueTask<DeviceResult> PrintAsync(
        int copies = 1,
        string? printerName = null,
        CancellationToken cancellationToken = default);

    /// <summary>关闭当前模板，但保留打印引擎以便后续继续使用。</summary>
    ValueTask<DeviceResult> CloseTemplateAsync(CancellationToken cancellationToken = default);
}
