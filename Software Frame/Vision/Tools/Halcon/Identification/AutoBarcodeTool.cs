// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AutoBarcodeParameters、AutoBarcodeTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Identification;

/// <summary>
/// AutoBarcodeParameters 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </summary>
public sealed class AutoBarcodeParameters
{
    public string Symbology { get; set; } = string.Empty;
    public List<double> Scales { get; set; } = [1, 1.5, 2];
    public double? Row1 { get; set; }
    public double? Column1 { get; set; }
    public double? Row2 { get; set; }
    public double? Column2 { get; set; }
}
/// <summary>
/// AutoBarcodeTool 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolBase&lt;AutoBarcodeParameters, BarcodeResult&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class AutoBarcodeTool : HalconToolBase<AutoBarcodeParameters, BarcodeResult>
{
    public override string Name => "AutoBarcode";
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 Any、IsFinite、AddError、nameof、Count；具体条件和先后次序见方法体。
    /// </summary>
    protected override ValidationResult ValidateCore(AutoBarcodeParameters p)
    {
        var result = new ValidationResult();
        if (p.Scales.Count == 0 || p.Scales.Any(s => !double.IsFinite(s) || s <= 0))
            result.AddError("SCALES_INVALID", "需要至少一个正数缩放比例。", nameof(p.Scales));
        var count = new[] { p.Row1, p.Column1, p.Row2, p.Column2 }.Count(v => v.HasValue);
        if (count != 0 && (count != 4 || p.Row2 <= p.Row1 || p.Column2 <= p.Column1))
            result.AddError("ROI_INVALID", "请完整配置有效的矩形搜索区域。", nameof(p.Row1));
        return result;
    }
    /// <summary>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ThrowIfNull、CropRectangle1、ZoomImageFactor、Run；具体条件和先后次序见方法体。
    /// </summary>
    protected override BarcodeResult RunCore(HObject? image, AutoBarcodeParameters p)
    {
        ArgumentNullException.ThrowIfNull(image);
        HObject? cropped = null;
        try
        {
            if (p.Row1.HasValue)
                HOperatorSet.CropRectangle1(image, out cropped, p.Row1.Value, p.Column1!.Value, p.Row2!.Value, p.Column2!.Value);
            using var reader = new BarcodeTool { Parameters = new() { Symbology = p.Symbology } };
            BarcodeResult? last = null;
            foreach (var scale in p.Scales)
            {
                HOperatorSet.ZoomImageFactor(cropped ?? image, out var zoomed, scale, scale, "constant");
                using (zoomed)
                {
                    last = reader.Run(zoomed);
                    if (!last.Success) return last;
                    if (last.Codes.Count == 0) continue;
                    foreach (var code in last.Codes)
                        code.Center = new Point2D(code.Center.X/scale+(p.Column1??0), code.Center.Y/scale+(p.Row1??0));
                    return last;
                }
            }
            return last ?? new BarcodeResult();
        }
        finally { cropped?.Dispose(); }
    }
}
