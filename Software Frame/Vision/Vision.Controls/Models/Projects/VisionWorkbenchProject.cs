using System.Text.Json;
using Vision.Core.Roi;

#nullable enable
namespace Vision.Controls.Models;

/// <summary>工具工程清单：v1兼容外部图像路径，v2携带输入快照；可选托管取样/训练参数及模型引用，不含SDK模型、运行结果或流程位姿。</summary>
public sealed class VisionWorkbenchProject
{
    public int Version { get; set; } = 1;
    public string Tool { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public JsonElement? Parameters { get; set; }
    public List<Roi> Rois { get; set; } = [];
    public int SelectedRoiIndex { get; set; } = -1;
    public ImageViewKind ImageView { get; set; }
    public bool ShowResultBounds { get; set; }
    public bool ShowResultCenters { get; set; }
    public string ImageSource { get; set; } = string.Empty;
    public VisionProjectImage? InputImage { get; set; }
    // 可选的工具配方；不包含训练后的SDK句柄、结果或流程定位位姿。
    public Vision.Core.Imaging.VisionImage? TemplateImage { get; set; }
    public JsonElement? TrainingParameters { get; set; }
    public string ModelReference { get; set; } = string.Empty;
}
