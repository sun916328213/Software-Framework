using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Tools.AI.AnomalyDetection;
using Vision.Tools.AI.Classification;
using Vision.Tools.AI.Detection;
using Vision.Tools.AI.Segmentation;

namespace Vision.Controls.Catalog;

/// <summary>AI 单一注册源；Provider 仍由宿主注入，注册时不加载模型。</summary>
public static class AIToolCatalog
{
    private static readonly ToolRegistration[] Registrations =
    [
        ToolRegistration.AI("Classification", () => new ClassificationTool()),
        ToolRegistration.AI("Detection", () => new DetectionTool()),
        ToolRegistration.AI("Segmentation", () => new SegmentationTool()),
        ToolRegistration.AI("AnomalyDetection", () => new AnomalyDetectionTool())
    ];
    public static IReadOnlyList<ToolEntry> Entries { get; } = Registrations.Select(r => r.Entry).ToArray();
    public static IReadOnlyDictionary<string, Func<object>> Tools { get; } =
        Registrations.ToDictionary(r => r.Key, r => r.Factory, StringComparer.Ordinal);
    public static IReadOnlyList<string> ToolNames => Tools.Keys.ToArray();
    public static object Create(string name) => Tools[name]();
}
