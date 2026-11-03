using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Matching;
using Vision.Tools.OpenCv.Measurement;
using Vision.Tools.OpenCv.Processing;

namespace Vision.Controls.Catalog;

/// <summary>OpenCV 单一注册源；演示参数只作用于浏览条目，不改变原始工具默认值。</summary>
public static class OpenCvToolCatalog
{
    private static readonly ToolRegistration[] Registrations =
    [
        ToolRegistration.OpenCv("Threshold", "Processing", () => new ThresholdTool()),
        ToolRegistration.OpenCv("Morphology", "Processing", () => new MorphologyTool()),
        ToolRegistration.OpenCv("Blob", "Inspection", () => new BlobTool(), configure: p => p.MinGray = 128, isDefault: true),
        ToolRegistration.OpenCv("TemplateMatch", "Matching", () => new TemplateMatchTool(), configure: p => { p.AngleStartDeg = -180; p.AngleExtentDeg = 360; }, setTemplate: (p, image) => p.Template = image),
        ToolRegistration.OpenCv("FindLine", "Measurement", () => new FindLineTool()),
        ToolRegistration.OpenCv("FindCircle", "Measurement", () => new FindCircleTool()),
        ToolRegistration.OpenCv("Crop", "Processing", () => new CropTool()),
        ToolRegistration.OpenCv("ImageConvert", "Processing", () => new ImageConvertTool()),
        ToolRegistration.OpenCv("ImageFilter", "Processing", () => new ImageFilterTool()),
        ToolRegistration.OpenCv("GrayStatistics", "Inspection", () => new GrayStatisticsTool()),
        ToolRegistration.OpenCv("IntensityTransform", "Processing", () => new IntensityTransformTool()),
        ToolRegistration.OpenCv("AdaptiveThreshold", "Processing", () => new AdaptiveThresholdTool()),
        ToolRegistration.OpenCv("ColorSegment", "Processing", () => new ColorSegmentTool()),
        ToolRegistration.OpenCv("ImageFile", "Acquisition", () => new Vision.Tools.OpenCv.Acquisition.ImageFileTool())
    ];
    public static IReadOnlyList<ToolEntry> Entries { get; } = Registrations.Select(r => r.Entry).ToArray();
    public static IReadOnlyDictionary<string, Func<object>> Tools { get; } =
        Registrations.ToDictionary(r => r.Key, r => r.Factory, StringComparer.Ordinal);
    public static IReadOnlyList<string> ToolNames => Tools.Keys.ToArray();
    public static object Create(string name) => Tools[name]();
}
