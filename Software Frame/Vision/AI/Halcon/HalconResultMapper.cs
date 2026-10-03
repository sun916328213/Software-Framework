// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：HalconResultMapper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Generic、Vision.Core.AI、Vision.Core.Geometry。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P6 —— HALCON 结果映射（计划 §6「转换结果」）。
//
// 只吃已抽取的基元（id / 置信度 / 框坐标 / 分数），不碰 HDict / HObject / HTuple，
// 因此可无 HALCON Runtime 测试。HDict→基元的抽取留在 Session.Run（需 Runtime）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using Vision.Core.AI;
using Vision.Core.Geometry;

namespace Vision.AI.Halcon
{
    /// <summary>HALCON DL 结果基元 → Core 公共结果（纯映射）。</summary>
    public static class HalconResultMapper
    {
        /// <summary>映射分类结果（names 为空时用 id 字符串作标签）。</summary>
        public static AIClassificationResult MapClassification(long[] classIds, double[] confidences, string[]? classNames = null)
        {
            var output = new AIClassificationResult();
            if (classIds == null || confidences == null)
                return output;

            var count = System.Math.Min(classIds.Length, confidences.Length);
            for (var i = 0; i < count; i++)
            {
                var label = classNames != null && classIds[i] >= 0 && classIds[i] < classNames.Length
                    ? classNames[classIds[i]]
                    : $"class_{classIds[i]}";
                output.Scores.Add(new AIClassScore { Label = label, ClassId = (int)classIds[i], Score = confidences[i] });
            }

            if (output.Scores.Count > 0)
            {
                var top = output.Scores[0];
                foreach (var score in output.Scores)
                {
                    if (score.Score > top.Score)
                        top = score;
                }

                output.TopLabel = top.Label;
                output.TopScore = top.Score;
            }

            return output;
        }

        /// <summary>映射检测结果（框坐标为 row1/col1/row2/col2 像素）。</summary>
        public static AIDetectionResult MapDetection(double[] row1, double[] col1, double[] row2, double[] col2,
            long[] classIds, double[] confidences, string[]? classNames = null)
        {
            var output = new AIDetectionResult();
            if (row1 == null || col1 == null || row2 == null || col2 == null || classIds == null || confidences == null)
                return output;

            var count = row1.Length;
            count = System.Math.Min(count, col1.Length);
            count = System.Math.Min(count, row2.Length);
            count = System.Math.Min(count, col2.Length);
            count = System.Math.Min(count, classIds.Length);
            count = System.Math.Min(count, confidences.Length);

            for (var i = 0; i < count; i++)
            {
                var label = classNames != null && classIds[i] >= 0 && classIds[i] < classNames.Length
                    ? classNames[classIds[i]]
                    : $"class_{classIds[i]}";
                output.Boxes.Add(new AIDetectionBox
                {
                    Box = new Rect2D(col1[i], row1[i], col2[i] - col1[i] + 1, row2[i] - row1[i] + 1),
                    Label = label,
                    ClassId = (int)classIds[i],
                    Score = confidences[i],
                });
            }

            return output;
        }

        /// <summary>映射分割结果（P6-v1 只有外接框；多边形/掩膜随 P7 区域语义补齐）。</summary>
        public static AISegmentationResult MapSegmentation(IReadOnlyList<(Rect2D Bounds, long ClassId, double Score)> items,
            string[]? classNames = null)
        {
            var output = new AISegmentationResult();
            if (items == null)
                return output;

            foreach (var (bounds, classId, score) in items)
            {
                var label = classNames != null && classId >= 0 && classId < classNames.Length
                    ? classNames[classId]
                    : $"class_{classId}";
                output.Items.Add(new AISegmentationItem { Label = label, Score = score, Bounds = bounds });
            }

            return output;
        }

        /// <summary>映射异常检测结果。</summary>
        public static AIAnomalyResult MapAnomaly(double score, bool isAnomaly, Rect2D bounds) =>
            new() { IsAnomaly = isAnomaly, Score = score, AnomalyBounds = bounds };
    }
}
