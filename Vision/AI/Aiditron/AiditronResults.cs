// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Aiditron 中性缺陷 DTO + 结果转换（计划 §6「转换结果」）。
//
// Session 先把 Legacy 结果（DefectInfo + RectInfo）收敛到本 DTO，再由转换器
// 直转 Core 公共结果：Legacy HObject 不出 Session，转换器纯映射可无 SDK 测试。
// 轮廓多边形 P6 不带（Legacy 链把轮廓点烧进了 XLD，只剩外接矩形；显示与多边形
// 随 P9 Controls 统一处理）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using Vision.Core.AI;
using Vision.Core.Geometry;

namespace Vision.AI.Aiditron
{
    /// <summary>谛创缺陷中性表达（Legacy→Core 的收敛点）。</summary>
    public sealed class AiditronDefect
    {
        public string Label { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double Area { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>Aiditron DTO → Core 公共结果（纯映射）。</summary>
    public static class AiditronResultConverter
    {
        /// <summary>转为检测结果（外接框由中心 + 宽高换算）。</summary>
        public static AIDetectionResult ToDetection(IReadOnlyList<AiditronDefect> defects)
        {
            var output = new AIDetectionResult();
            if (defects == null)
                return output;

            foreach (var defect in defects)
            {
                if (defect == null)
                    continue;
                output.Boxes.Add(new AIDetectionBox
                {
                    Box = ToBox(defect),
                    Label = defect.Label ?? string.Empty,
                    Score = defect.Confidence,
                });
            }

            return output;
        }

        /// <summary>转为分割结果（只有外接框；Polygon 为空见文件头说明）。</summary>
        public static AISegmentationResult ToSegmentation(IReadOnlyList<AiditronDefect> defects)
        {
            var output = new AISegmentationResult();
            if (defects == null)
                return output;

            foreach (var defect in defects)
            {
                if (defect == null)
                    continue;
                output.Items.Add(new AISegmentationItem
                {
                    Label = defect.Label ?? string.Empty,
                    Score = defect.Confidence,
                    Bounds = ToBox(defect),
                });
            }

            return output;
        }

        /// <summary>转为分类结果（首个为 Top；谛创分割+分类链按 NG 有效，首个即主缺陷）。</summary>
        public static AIClassificationResult ToClassification(IReadOnlyList<AiditronDefect> defects)
        {
            var output = new AIClassificationResult();
            if (defects == null || defects.Count == 0)
                return output;

            for (var i = 0; i < defects.Count; i++)
            {
                var defect = defects[i];
                if (defect == null)
                    continue;
                output.Scores.Add(new AIClassScore { Label = defect.Label ?? string.Empty, ClassId = i, Score = defect.Confidence });
            }

            var top = defects[0];
            if (top != null)
            {
                output.TopLabel = top.Label ?? string.Empty;
                output.TopScore = top.Confidence;
            }

            return output;
        }

        private static Rect2D ToBox(AiditronDefect defect) => new(
            defect.CenterX - defect.Width / 2,
            defect.CenterY - defect.Height / 2,
            defect.Width,
            defect.Height);
    }
}
