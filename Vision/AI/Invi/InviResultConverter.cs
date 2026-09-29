// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Invi 结果转换（计划 §6「Vision.AI.* 只负责……转换结果」）。
//
// 旧链把 native JSON 先转成 HALCON XLD 再显示；新链按任务类型直转 Core 公共结果：
//   · Detection → boxes（bd_rect + 类别 + 置信度；轮廓点不进检测结果）；
//   · Segmentation → items（外接框 + 轮廓多边形；掩膜位图 P6 不做，native 未给掩膜）；
//   · Classification → 首个结果为 Top，其余进 Scores（native 无全类别向量，按实际输出表达）。
// 置信度/面积阈值过滤归 Tool 层（P5），转换器只做忠实映射，不过滤。
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using Vision.AI.Invi.Native;
using Vision.Core.AI;
using Vision.Core.Geometry;

namespace Vision.AI.Invi
{
    /// <summary>Invi native DTO → Core 公共结果（纯映射，可无 Runtime 测试）。</summary>
    public static class InviResultConverter
    {
        /// <summary>转为检测结果（按 native 返回顺序；置信度排序归 Provider 保证→此处保持原序）。</summary>
        public static AIDetectionResult ToDetection(IReadOnlyList<InviResult> results)
        {
            var output = new AIDetectionResult();
            if (results == null)
                return output;

            foreach (var item in results)
            {
                if (item == null)
                    continue;
                output.Boxes.Add(new AIDetectionBox
                {
                    Box = ToRect(item.bd_rect),
                    Label = item.class_name ?? string.Empty,
                    ClassId = item.class_id,
                    Score = item.confidence,
                });
            }

            return output;
        }

        /// <summary>转为分割结果（外接框 + 轮廓多边形）。</summary>
        public static AISegmentationResult ToSegmentation(IReadOnlyList<InviResult> results)
        {
            var output = new AISegmentationResult();
            if (results == null)
                return output;

            foreach (var item in results)
            {
                if (item == null)
                    continue;
                var segmentation = new AISegmentationItem
                {
                    Label = item.class_name ?? string.Empty,
                    Score = item.confidence,
                    Bounds = ToRect(item.bd_rect),
                };
                if (item.contour_points != null)
                {
                    foreach (var point in item.contour_points)
                        segmentation.Polygon.Add(new Point2D(point.x, point.y));
                }

                output.Items.Add(segmentation);
            }

            return output;
        }

        /// <summary>转为分类结果（首个为 Top）。</summary>
        public static AIClassificationResult ToClassification(IReadOnlyList<InviResult> results)
        {
            var output = new AIClassificationResult();
            if (results == null || results.Count == 0)
                return output;

            foreach (var item in results)
            {
                if (item == null)
                    continue;
                output.Scores.Add(new AIClassScore
                {
                    Label = item.class_name ?? string.Empty,
                    ClassId = item.class_id,
                    Score = item.confidence,
                });
            }

            var top = results[0];
            if (top != null)
            {
                output.TopLabel = top.class_name ?? string.Empty;
                output.TopScore = top.confidence;
            }

            return output;
        }

        private static Rect2D ToRect(Rect rect) =>
            rect == null ? Rect2D.Empty : new Rect2D(rect.x, rect.y, rect.width, rect.height);
    }
}
