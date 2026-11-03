using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Tools.Halcon.Acquisition;
using Vision.Tools.Halcon.Calibration;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Inspection;
using Vision.Tools.Halcon.Matching;
using Vision.Tools.Halcon.Measurement;

namespace Vision.Controls.Catalog;

/// <summary>HALCON 单一注册源：兼容原始工具目录，同时提供 Controls 可执行条目。</summary>
public static class HalconToolCatalog
{
    private static readonly ToolRegistration[] Registrations =
    [
        ToolRegistration.Halcon("Blob", "Inspection", () => new BlobTool()),
        ToolRegistration.Halcon("ShapeMatch", "Matching", () => new ShapeMatchTool()),
        ToolRegistration.Halcon("FindLine", "Measurement", () => new FindLineTool()),
        ToolRegistration.Halcon("Barcode", "Identification", () => new BarcodeTool()),
        ToolRegistration.Halcon("DataCode", "Identification", () => new DataCodeTool()),
        ToolRegistration.Halcon("NPointsCalibration", "Calibration", () => new NPointsCalibrationTool()),
        ToolRegistration.Halcon("Fixture", "Coordinate", () => new FixtureTool()),
        ToolRegistration.Halcon("ImageFile", "Acquisition", () => new ImageFileTool())
    ];
    public static IReadOnlyList<ToolEntry> Entries { get; } = Registrations.Select(r => r.Entry).ToArray();
    public static IReadOnlyDictionary<string, Func<object>> Tools { get; } =
        Registrations.ToDictionary(r => r.Key, r => r.Factory, StringComparer.Ordinal);
    public static IReadOnlyList<string> ToolNames => Tools.Keys.ToArray();
    public static object Create(string name) => Tools[name]();
}
