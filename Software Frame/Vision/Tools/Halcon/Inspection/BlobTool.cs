// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：BlobFeatureLogic、BlobFeatureRange、BlobParameters、BlobItem、BlobResult、BlobTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：HalconDotNet、Vision.Core.Geometry、Vision.Core.Results、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P3 —— BlobTool（Inspection）。
// 迁移源：DPLibrary_Halcon_F48/Tools/Blob/HTool_Blob.cs（+ BlobClasslb.cs 的特征枚举）。
//
// 标准化内容（计划 §7）：
//   · Parameters：阈值区间 + 特征筛选（显式建模；旧散落属性收口）；
//   · Result：稳定结果对象（中心用 Core Point2D，面积用 double，不再只暴露 HTuple/HObject）；
//   · Validate：运行前验证阈值/特征/范围一致性；
//   · Run：Threshold → Connection → FillUp → SelectShape → AreaCenter（算法与旧一致）；
//   · Dispose：基类无原生资源，默认实现即可（区域随 HROI 生命周期，旧 HBlobResult.Dispose 语义由调用方保持）。
//
// ⛔ 命名清理：HTool_ 前缀去掉（计划 §P3「清理 HTool_ / 下划线风格」）。
//    旧 HBlobResult（含 HTuple features/values、HROI ResultRegion）保留在迁移源内，
//    P9 退场前由旧流程继续使用；新 Result 不引用它。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Inspection;

/// <summary>Blob 特征筛选关系（旧 <c>E_OpearationType</c> 的重命名：and/or → And/Or）。</summary>
public enum BlobFeatureLogic
{
    /// <summary>满足全部特征区间。</summary>
    And,
    /// <summary>满足任一特征区间。</summary>
    Or,
}

/// <summary>Blob 特征区间（旧 <c>Scope</c> 的重命名：Name/Min/Max → Feature/Min/Max）。</summary>
public sealed class BlobFeatureRange
{
    /// <summary>特征名（如 area、circularity；与 HALCON SelectShape 特征名一致）。</summary>
    public string Feature { get; set; } = "area";

    /// <summary>下限。</summary>
    public double Min { get; set; }

    /// <summary>上限。</summary>
    public double Max { get; set; } = 99999;
}

/// <summary>Blob 运行参数。</summary>
public sealed class BlobParameters
{
    /// <summary>最小灰度（0~255）。</summary>
    public int MinGray { get; set; }

    /// <summary>最大灰度（0~255）。</summary>
    public int MaxGray { get; set; } = 255;

    /// <summary>特征筛选（为空表示不过滤形状特征）。</summary>
    public List<BlobFeatureRange> Features { get; set; } = [new BlobFeatureRange()];

    /// <summary>多特征之间的逻辑关系。</summary>
    public BlobFeatureLogic Logic { get; set; } = BlobFeatureLogic.And;
}

/// <summary>单个 Blob。</summary>
public sealed class BlobItem
{
    /// <summary>序号（从 1 开始，与旧 HBlobResult.Name 一致）。</summary>
    public int Index { get; set; }

    /// <summary>面积（像素）。</summary>
    public double Area { get; set; }

    /// <summary>中心（图像坐标：X=列，Y=行）。</summary>
    public Point2D Center { get; set; }
}

/// <summary>Blob 结果（稳定领域类型；不含 HTuple/HObject）。</summary>
public sealed class BlobResult : ToolResult
{
    /// <summary>检出的全部 Blob。</summary>
    public List<BlobItem> Items { get; set; } = [];
}

/// <summary>
/// Blob 工具（Inspection）。
/// </summary>
public sealed class BlobTool : HalconToolBase<BlobParameters, BlobResult>
{
    /// <inheritdoc/>
    public override string Name => "Blob";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 IsValidThresholdRange、AddError、nameof、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(BlobParameters parameters)
    {
        var result = new ValidationResult();
        if (!HalconHelper.IsValidThresholdRange(parameters.MinGray, parameters.MaxGray, out var error))
            result.AddError("THRESHOLD_RANGE", error, nameof(BlobParameters.MinGray));

        if (parameters.Features == null)
            return result.AddError("FEATURES_MISSING", "特征列表不能为 null；不筛选时请使用空列表。", nameof(BlobParameters.Features));

        for (var i = 0; i < parameters.Features.Count; i++)
        {
            var feature = parameters.Features[i];
            if (feature == null)
            {
                result.AddError("FEATURE_MISSING", $"第 {i + 1} 个特征配置为空。", $"{nameof(BlobParameters.Features)}[{i}]");
                continue;
            }
            if (string.IsNullOrWhiteSpace(feature.Feature))
                result.AddError("FEATURE_EMPTY", $"第 {i + 1} 个特征名为空。", $"{nameof(BlobParameters.Features)}[{i}]");
            if (feature.Min > feature.Max)
                result.AddError("FEATURE_RANGE", $"特征 {feature.Feature} 下限 {feature.Min} 大于上限 {feature.Max}。", $"{nameof(BlobParameters.Features)}[{i}]");
        }

        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 ThrowIfNull、Threshold、Connection、FillUp、Append；具体条件和先后次序见方法体。
    /// </remarks>
    protected override BlobResult RunCore(HObject? image, BlobParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(image);
        var result = new BlobResult();

        HObject? threshold = null;
        HObject? connected = null;
        HObject? filled = null;
        HObject? selected = null;
        HTuple? area = null;
        HTuple? row = null;
        HTuple? column = null;

        try
        {
            HOperatorSet.Threshold(image, out threshold, parameters.MinGray, parameters.MaxGray);
            HOperatorSet.Connection(threshold, out connected);
            HOperatorSet.FillUp(connected, out filled);

            if (parameters.Features.Count > 0)
            {
                var names = new HTuple();
                var min = new HTuple();
                var max = new HTuple();
                try
                {
                    foreach (var feature in parameters.Features)
                    {
                        names.Append(feature.Feature);
                        min.Append(feature.Min);
                        max.Append(feature.Max);
                    }

                    HOperatorSet.SelectShape(filled, out selected,
                        names, parameters.Logic == BlobFeatureLogic.And ? "and" : "or", min, max);
                }
                finally
                {
                    names.Dispose();
                    min.Dispose();
                    max.Dispose();
                }
            }
            else
            {
                selected = filled;
                filled = null;
            }

            HOperatorSet.AreaCenter(selected, out area, out row, out column);
            var count = selected.CountObj();
            for (var i = 0; i < count; i++)
            {
                result.Items.Add(new BlobItem
                {
                    Index = i + 1,
                    Area = Math.Round(area[i].D, 3),
                    Center = new Point2D(Math.Round(column[i].D, 3), Math.Round(row[i].D, 3)),
                });
            }

            return result;
        }
        finally
        {
            threshold?.Dispose();
            connected?.Dispose();
            filled?.Dispose();
            selected?.Dispose();
            area?.Dispose();
            row?.Dispose();
            column?.Dispose();
        }
    }
}
