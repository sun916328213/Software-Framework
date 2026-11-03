using Vision.Controls.Models;
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
    Func<string, bool, string?>? PickTemplateModelPath = null,
    Func<bool, string?>? PickProjectPath = null,
    Func<string, VisionImage?>? LoadImageByPath = null);
