// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolResultText。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls、Vision.Core.AI、Vision.Core.Results。导入不代表所有类型都会在运行时加载。
using Vision.Controls;
// ─────────────────────────────────────────────────────────────────────────────
// P8 —— 结果摘要（计划 §P8「UI 根据 Tool/Result 展示」）。
//
// ToolCardViewModel 的结果栏只显示这里产出的一行文本：已知结果类型按域语义摘要，
// 未知类型回退 ToString（新 Tool 不强制改展示层）。纯静态、无 UI 类型，可单测.
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.AI;
using Vision.Core.Results;

#nullable enable

namespace Vision.Controls.Presentation
{
    /// <summary>ToolResult → 单行展示摘要。</summary>
    public static class ToolResultText
    {
        /// <summary>生成摘要；result 为 null 返回占位符。</summary>
        public static string Summarize(ToolResult? result)
        {
            if (result == null)
                return "—";
            if (!result.Success)
                return $"失败：{result.Message}";

            return result switch
            {
                AIClassificationResult classification =>
                    $"分类：{classification.TopLabel}（{classification.TopScore:0.###}，{classification.Scores.Count} 类）",
                AIDetectionResult detection =>
                    detection.Boxes.Count == 0
                        ? "检测：0 个目标"
                        : $"检测：{detection.Boxes.Count} 个目标，首个 {detection.Boxes[0].Label}（{detection.Boxes[0].Score:0.###}）@{detection.Boxes[0].Box.Center}",
                AISegmentationResult segmentation =>
                    $"分割：{segmentation.Items.Count} 个区域",
                AIAnomalyResult anomaly =>
                    anomaly.IsAnomaly ? $"异常：分数 {anomaly.Score:0.###}" : "正常",
                Vision.Tools.Halcon.Inspection.BlobResult halconBlob =>
                    halconBlob.Items.Count == 0
                        ? "Blob：0 个"
                        : $"Blob：{halconBlob.Items.Count} 个，首个面积 {halconBlob.Items[0].Area:0.###}@{halconBlob.Items[0].Center}",
                Vision.Tools.Halcon.Measurement.FindLineResult halconLine =>
                    $"直线：{halconLine.Start} → {halconLine.End}（{halconLine.EdgePointCount} 点）",
                Vision.Tools.Halcon.Matching.ShapeMatchResult shape =>
                    shape.Matches.Count == 0
                        ? "匹配：0 个"
                        : $"匹配：{shape.Matches.Count} 个，首个分数 {shape.Matches[0].Score:0.###}@{shape.Matches[0].Center}",
                Vision.Tools.OpenCv.Processing.ThresholdResult threshold =>
                    $"阈值：{threshold.WhitePixelCount} 前景（{threshold.ForegroundRatio:0.###%}）",
                Vision.Tools.OpenCv.Processing.MorphologyResult morphology =>
                    $"形态学：{morphology.WhitePixelCount} 前景（{morphology.ForegroundRatio:0.###%}）",
                Vision.Tools.OpenCv.Inspection.BlobResult cvBlob =>
                    cvBlob.Items.Count == 0
                        ? "Blob：0 个"
                        : $"Blob：{cvBlob.Items.Count} 个，首个面积 {cvBlob.Items[0].Area:0.###}@{cvBlob.Items[0].Center}",
                Vision.Tools.OpenCv.Matching.TemplateMatchResult template =>
                    template.Matches.Count == 0
                        ? "匹配：0 个"
                        : $"匹配：{template.Matches.Count} 个，首个分数 {template.Matches[0].Score:0.###}@{template.Matches[0].Center}",
                Vision.Tools.OpenCv.Measurement.FindLineResult cvLine =>
                    $"直线：{cvLine.Start} → {cvLine.End}（{cvLine.EdgePointCount} 点）",
                Vision.Tools.OpenCv.Measurement.FindCircleResult circle =>
                    $"圆：中心 {circle.Center}，半径 {circle.Radius:0.###}",
                _ => result.ToString() ?? result.GetType().Name,
            };
        }
    }
}
