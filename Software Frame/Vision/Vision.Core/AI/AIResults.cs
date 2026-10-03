// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AIClassScore、AIClassificationResult、AIDetectionBox、AIDetectionResult、AISegmentationItem、AISegmentationResult、AIAnomalyResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / AI / Common Results」，
// §6 结果表：ClassificationResult / DetectionResult + boxes / SegmentationResult + region/contour / AnomalyResult）。
//
//   这一组是「**公共**结果」：三家 Provider 各自把自家 SDK 的原始输出转成这里的类型，
//   Tool 与 UI 只认它们。§7「Result」原文：「返回稳定结果对象，例如 BlobResult / MatchResult / DetectionResult」。
//
// ⛔ 两条硬约束：
//   ① 不得出现厂商类型（HObject / Mat / HTuple）—— §5.1「避免直接返回 HTuple/HObject/Mat」；
//   ② 不得出现**接口类型的集合**（如 List<IVisionRegion>）—— System.Text.Json 对接口集合
//      既写不全（按声明类型写）又读不回（无多态配置时直接失败/丢数据），
//      这是仓库已经踩过的坑（见 WPFControls.Tests/SerializationContractTests 的口径）。
//      P5 若确实需要「区域集合」，必须**同时**补 [JsonPolymorphic]/[JsonDerivedType] 或换成具体类型。
namespace Vision.Core.AI
{
    /// <summary>分类结果里的一个类别得分。</summary>
    public sealed class AIClassScore
    {
        /// <summary>类别标签。</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>类别索引（模型输出顺序）。</summary>
        public int ClassId { get; set; }

        /// <summary>置信度（0~1）。</summary>
        public double Score { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() => $"{Label}(#{ClassId})={Score:0.####}";
    }

    /// <summary>分类结果（计划 §6 表中 ClassificationTool 的产物）。</summary>
    public sealed class AIClassificationResult : Results.ToolResult
    {
        /// <summary>全部类别得分（按模型输出顺序）。</summary>
        public System.Collections.Generic.List<AIClassScore> Scores { get; set; }
            = new System.Collections.Generic.List<AIClassScore>();

        /// <summary>最高分类别标签；无结果时为空串。</summary>
        public string TopLabel { get; set; } = string.Empty;

        /// <summary>最高分。</summary>
        public double TopScore { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() => $"{base.ToString()} top={TopLabel}@{TopScore:0.####}";
    }

    /// <summary>检测结果里的一个框。</summary>
    public sealed class AIDetectionBox
    {
        /// <summary>轴对齐外接框（图像坐标系）。</summary>
        public Geometry.Rect2D Box { get; set; }

        /// <summary>类别标签。</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>类别索引。</summary>
        public int ClassId { get; set; }

        /// <summary>置信度（0~1）。</summary>
        public double Score { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() => $"{Label}(#{ClassId})@{Score:0.####} {Box}";
    }

    /// <summary>检测结果（计划 §6 表中 DetectionTool 的产物）。</summary>
    public sealed class AIDetectionResult : Results.ToolResult
    {
        /// <summary>检出的框（按置信度降序由 Provider 保证）。</summary>
        public System.Collections.Generic.List<AIDetectionBox> Boxes { get; set; }
            = new System.Collections.Generic.List<AIDetectionBox>();

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() => $"{base.ToString()} boxes={Boxes.Count}";
    }

    /// <summary>分割结果里的一个区域（用具体类型表达 region + contour）。</summary>
    public sealed class AISegmentationItem
    {
        /// <summary>类别标签。</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>置信度（0~1）。</summary>
        public double Score { get; set; }

        /// <summary>轴对齐外接框。</summary>
        public Geometry.Rect2D Bounds { get; set; }

        /// <summary>轮廓多边形（图像坐标系）；为空表示只有外接框可用。</summary>
        public System.Collections.Generic.List<Geometry.Point2D> Polygon { get; set; }
            = new System.Collections.Generic.List<Geometry.Point2D>();

        /// <summary>位图掩膜（逐像素）；为 null 表示 Provider 未给出掩膜。</summary>
        public Imaging.VisionImage Mask { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </remarks>
        public override string ToString() =>
            $"{Label}@{Score:0.####} {Bounds} poly={Polygon.Count} mask={(Mask == null ? "no" : "yes")}";
    }

    /// <summary>分割结果（计划 §6 表中 SegmentationTool 的产物）。</summary>
    public sealed class AISegmentationResult : Results.ToolResult
    {
        /// <summary>分割出的区域。</summary>
        public System.Collections.Generic.List<AISegmentationItem> Items { get; set; }
            = new System.Collections.Generic.List<AISegmentationItem>();

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() => $"{base.ToString()} items={Items.Count}";
    }

    /// <summary>异常检测结果（计划 §6 表中 AnomalyDetectionTool 的产物）。</summary>
    public sealed class AIAnomalyResult : Results.ToolResult
    {
        /// <summary>是否判为异常。</summary>
        public bool IsAnomaly { get; set; }

        /// <summary>异常分数（越大越异常；具体量纲由 Provider 决定，只保证同模型内可比）。</summary>
        public double Score { get; set; }

        /// <summary>异常区域外接框；无异常或无定位能力时为 <see cref="Geometry.Rect2D.Empty"/>。</summary>
        public Geometry.Rect2D AnomalyBounds { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() =>
            $"{base.ToString()} anomaly={IsAnomaly} score={Score:0.####} {AnomalyBounds}";
    }
}
