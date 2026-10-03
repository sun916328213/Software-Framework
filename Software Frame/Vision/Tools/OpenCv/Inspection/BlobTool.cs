// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：BlobFeatureLogic、BlobFeatureRange、BlobParameters、BlobItem、BlobResult、BlobTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：OpenCvSharp、Vision.Core.Geometry、Vision.Core.Imaging、Vision.Core.Results、Vision.Tools.OpenCv.Internal。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P4 —— BlobTool（Inspection）。
// 对标 HALCON 侧 BlobTool（P3）：阈值区间 + 特征筛选 + And/Or，同语义、业务容差对比。
//
// 实现：InRange → ConnectedComponentsWithStats。特征限定为 stats 原生量
// （area / width / height；HALCON 的 circularity 等需轮廓，P4 不做，由 P10 对比测试注明口径）。
// Result 形状与 HALCON 侧对齐：序号 / 面积 / 中心（X=列，Y=行），另加外接矩形。
// ─────────────────────────────────────────────────────────────────────────────

using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Internal;

namespace Vision.Tools.OpenCv.Inspection;

/// <summary>Blob 特征筛选关系（与 HALCON 侧同语义）。</summary>
public enum BlobFeatureLogic
{
    /// <summary>满足全部特征区间。</summary>
    And,
    /// <summary>满足任一特征区间。</summary>
    Or,
}

/// <summary>Blob 特征区间（P4 仅支持 area / width / height）。</summary>
public sealed class BlobFeatureRange
{
    /// <summary>特征名：area / width / height。</summary>
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

    /// <summary>特征筛选（为空表示不过滤）。</summary>
    public List<BlobFeatureRange> Features { get; set; } = [new BlobFeatureRange()];

    /// <summary>多特征之间的逻辑关系。</summary>
    public BlobFeatureLogic Logic { get; set; } = BlobFeatureLogic.And;
}

/// <summary>单个 Blob。</summary>
public sealed class BlobItem
{
    /// <summary>序号（从 1 开始，与 HALCON 侧一致）。</summary>
    public int Index { get; set; }

    /// <summary>面积（像素）。</summary>
    public double Area { get; set; }

    /// <summary>质心（X=列，Y=行）。</summary>
    public Point2D Center { get; set; }

    /// <summary>外接矩形（左上角 + 宽高，像素）。</summary>
    public Rect2D Bounds { get; set; }
}

/// <summary>Blob 结果（稳定领域类型；不含 Mat）。</summary>
public sealed class BlobResult : ToolResult
{
    /// <summary>检出的全部 Blob（按连通域标号顺序）。</summary>
    public List<BlobItem> Items { get; set; } = [];
}

/// <summary>
/// Blob 工具（Inspection）：连通域分析。
/// </summary>
public sealed class BlobTool : OpenCvToolBase<BlobParameters, BlobResult>
{
    private static readonly string[] SupportedFeatures = ["area", "width", "height"];

    /// <inheritdoc/>
    public override string Name => "Blob";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof、Contains；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(BlobParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.MinGray < 0 || parameters.MinGray > 255)
            result.AddError("THRESHOLD_RANGE", $"最小灰度 {parameters.MinGray} 超出 0~255。", nameof(BlobParameters.MinGray));
        if (parameters.MaxGray < 0 || parameters.MaxGray > 255)
            result.AddError("THRESHOLD_RANGE", $"最大灰度 {parameters.MaxGray} 超出 0~255。", nameof(BlobParameters.MaxGray));
        if (parameters.MinGray > parameters.MaxGray)
            result.AddError("THRESHOLD_RANGE", $"最小灰度 {parameters.MinGray} 大于最大灰度 {parameters.MaxGray}。", nameof(BlobParameters.MinGray));

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
            if (!SupportedFeatures.Contains(feature.Feature))
                result.AddError("FEATURE_UNSUPPORTED", $"不支持的特征 {feature.Feature}（P4 仅支持 area/width/height）。", $"{nameof(BlobParameters.Features)}[{i}]");
            if (feature.Min > feature.Max)
                result.AddError("FEATURE_RANGE", $"特征 {feature.Feature} 下限 {feature.Min} 大于上限 {feature.Max}。", $"{nameof(BlobParameters.Features)}[{i}]");
        }

        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 FromVisionImage、InRange、ConnectedComponentsWithStats、Get、PassesFilter；具体条件和先后次序见方法体。
    /// </remarks>
    protected override BlobResult RunCore(VisionImage image, BlobParameters parameters)
    {
        using var src = MatConverter.FromVisionImage(image);
        using var binary = new Mat();
        Cv2.InRange(src, new Scalar(parameters.MinGray), new Scalar(parameters.MaxGray), binary);

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);

        var result = new BlobResult();
        var index = 0;
        for (var label = 1; label < count; label++)
        {
            var area = stats.Get<int>(label, (int)ConnectedComponentsTypes.Area);
            var left = stats.Get<int>(label, (int)ConnectedComponentsTypes.Left);
            var top = stats.Get<int>(label, (int)ConnectedComponentsTypes.Top);
            var width = stats.Get<int>(label, (int)ConnectedComponentsTypes.Width);
            var height = stats.Get<int>(label, (int)ConnectedComponentsTypes.Height);
            var cx = centroids.Get<double>(label, 0);
            var cy = centroids.Get<double>(label, 1);

            if (!PassesFilter(area, width, height, parameters))
                continue;

            result.Items.Add(new BlobItem
            {
                Index = ++index,
                Area = area,
                Center = new Point2D(Math.Round(cx, 3), Math.Round(cy, 3)),
                Bounds = new Rect2D(left, top, width, height),
            });
        }

        return result;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PassesFilter。
    /// 正文中的关键协作调用包括 Count；具体条件和先后次序见方法体。
    /// </summary>
    private static bool PassesFilter(int area, int width, int height, BlobParameters parameters)
    {
        if (parameters.Features.Count == 0)
            return true;

        var hits = parameters.Features.Count(feature =>
        {
            var value = feature.Feature switch
            {
                "width" => (double)width,
                "height" => (double)height,
                _ => (double)area,
            };
            return value >= feature.Min && value <= feature.Max;
        });

        return parameters.Logic == BlobFeatureLogic.And
            ? hits == parameters.Features.Count
            : hits > 0;
    }
}
