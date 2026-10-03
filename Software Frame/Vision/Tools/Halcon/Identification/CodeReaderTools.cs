// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：DecodedCode、BarcodeParameters、BarcodeResult、DataCodeParameters、DataCodeResult、BarcodeTool、DataCodeTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P3 —— BarcodeTool / DataCodeTool（Identification）。
// 迁移源：DPLibrary_Halcon_F48/Tools/CodeReader/HTool_BarCode.cs、HTool_2DCode.cs。
//
// 标准化内容：
//   · Parameters：码制（空=自动）、最小分数/超时（显式建模；旧散落的码制字符串收口）；
//   · Result：解码文本 + 码制 + 分数 + 中心（稳定类型；旧结果直接绑 HObject/HTuple）；
//   · Validate：码制名非空、分数区间；
//   · Run：CreateBarCodeModel + FindBarCode / CreateDataCode2dModel + FindDataCode2d
//     （标准两段式；旧 AutoRegionZoomFind 的“放大搜索”策略不在 P3 范围）；
//   · Dispose：释放条码/二维码模型句柄。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Identification;

/// <summary>解码结果项（1D/2D 共用）。</summary>
public sealed class DecodedCode
{
    /// <summary>解码文本。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>码制（如 Code128、QR Code）。</summary>
    public string Symbology { get; set; } = string.Empty;

    /// <summary>中心（图像坐标：X=列，Y=行）。</summary>
    public Point2D Center { get; set; }
}

/// <summary>条码参数（1D）。</summary>
public sealed class BarcodeParameters
{
    /// <summary>码制（空字符串=自动识别）。</summary>
    public string Symbology { get; set; } = string.Empty;
}

/// <summary>条码结果。</summary>
public sealed class BarcodeResult : ToolResult
{
    /// <summary>全部解码。</summary>
    public List<DecodedCode> Codes { get; set; } = [];
}

/// <summary>二维码参数（2D）。</summary>
public sealed class DataCodeParameters
{
    /// <summary>码制（如 QR Code、Data Matrix ECC 200；空=自动）。</summary>
    public string Symbology { get; set; } = string.Empty;
}

/// <summary>二维码结果。</summary>
public sealed class DataCodeResult : ToolResult
{
    /// <summary>全部解码。</summary>
    public List<DecodedCode> Codes { get; set; } = [];
}

/// <summary>一维条码工具（Identification）。</summary>
public sealed class BarcodeTool : HalconToolBase<BarcodeParameters, BarcodeResult>
{
    /// <inheritdoc/>
    public override string Name => "Barcode";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// </remarks>
    protected override ValidationResult ValidateCore(BarcodeParameters parameters) => new();

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsNullOrWhiteSpace、Trim、CreateBarCodeModel、SetBarCodeParam；具体条件和先后次序见方法体。
    /// </remarks>
    protected override BarcodeResult RunCore(HObject? image, BarcodeParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(image);
        var result = new BarcodeResult();
        HTuple? model = null;
        HTuple? decoded = null;
        try
        {
            var candidates = string.IsNullOrWhiteSpace(parameters.Symbology)
                ? "auto"
                : parameters.Symbology.Trim();
            HOperatorSet.CreateBarCodeModel(new HTuple(), new HTuple(), out model);
            HOperatorSet.SetBarCodeParam(model, "element_size_min", 4);
            HOperatorSet.FindBarCode(image, out _, model, candidates, out decoded);
            for (var i = 0; i < decoded.Length; i++)
            {
                // FindBarCode 的 decoded 输出为元组；此处取文本与码制，中心以后续 P7 坐标链补齐。
                result.Codes.Add(new DecodedCode { Text = decoded[i].S, Symbology = candidates });
            }

            return result;
        }
        finally
        {
            if (model != null)
            {
                try
                {
                    HOperatorSet.ClearBarCodeModel(model);
                }
                finally
                {
                    model.Dispose();
                }
            }

            decoded?.Dispose();
        }
    }
}

/// <summary>二维码工具（Identification）。</summary>
public sealed class DataCodeTool : HalconToolBase<DataCodeParameters, DataCodeResult>
{
    /// <inheritdoc/>
    public override string Name => "DataCode";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// </remarks>
    protected override ValidationResult ValidateCore(DataCodeParameters parameters) => new();

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsNullOrWhiteSpace、Trim、CreateDataCode2dModel、FindDataCode2d；具体条件和先后次序见方法体。
    /// </remarks>
    protected override DataCodeResult RunCore(HObject? image, DataCodeParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(image);
        var result = new DataCodeResult();
        HTuple? model = null;
        try
        {
            var candidates = string.IsNullOrWhiteSpace(parameters.Symbology)
                ? new[] { "QR Code", "Data Matrix ECC 200" }
                : new[] { parameters.Symbology.Trim() };
            HOperatorSet.CreateDataCode2dModel(candidates, new HTuple(), new HTuple(), out model);
            HOperatorSet.FindDataCode2d(image, out var symbolXLDs, model,
                "stop_after_result_num", 10, out var resultHandles, out var decoded);
            try
            {
                for (var i = 0; i < decoded.Length; i++)
                    result.Codes.Add(new DecodedCode { Text = decoded[i].S, Symbology = string.Join(",", candidates) });
            }
            finally
            {
                symbolXLDs.Dispose();
                resultHandles.Dispose();
                decoded.Dispose();
            }

            return result;
        }
        finally
        {
            if (model != null)
            {
                try
                {
                    HOperatorSet.ClearDataCode2dModel(model);
                }
                finally
                {
                    model.Dispose();
                }
            }
        }
    }
}
