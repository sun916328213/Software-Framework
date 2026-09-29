// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Invi native 结果 DTO 迁移（来源：DPLibrary_Halcon_F48/Tools/AI/Invi/InviType.cs
// 的 InviResult / Rect / PointD；InviType 枚举不迁移——任务类型改由 Core AIModelType
// 表达，native int 映射见 InviProvider 注释）。
// 字段名保持厂商 JSON 的小写原样（反序列化大小写不敏感，见 InviHelper）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace Vision.AI.Invi.Native
{
    /// <summary>Invi native 推理结果项（JSON DTO）。</summary>
    public sealed class InviResult
    {
        public int class_id { get; set; }

        public string class_name { get; set; } = string.Empty;

        public double area { get; set; }

        public double confidence { get; set; }

        public Rect bd_rect { get; set; } = new Rect();

        public List<PointD> contour_points { get; set; } = new List<PointD>();
    }

    /// <summary>外接矩形（像素）。</summary>
    public sealed class Rect
    {
        public int x { get; set; }

        public int y { get; set; }

        public int width { get; set; }

        public int height { get; set; }
    }

    /// <summary>轮廓点（像素）。</summary>
    public sealed class PointD
    {
        public int x { get; set; }

        public int y { get; set; }
    }
}
