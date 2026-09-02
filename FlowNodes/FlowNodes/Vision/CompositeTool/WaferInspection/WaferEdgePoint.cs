using System.Globalization;

namespace FlowNodes.Vision.CompositeTool.WaferInspection
{
    /// <summary>
    /// 硅片边缘单次采集结果：Blob 中心点（携带拍照时机器人的 Z 坐标）+ 最小外接矩形角度。
    /// 机器人携带相机在多个位置拍照，每执行一次 Blob 工具产生一个点位，
    /// 由 <see cref="WaferInspectionNode"/> 累积整合后交给 <see cref="WaferEdgeCalculator"/> 统计。
    /// </summary>
    public class WaferEdgePoint
    {
        /// <summary>采集序号（从 1 开始，按执行先后递增）。</summary>
        public int Index { get; set; }

        /// <summary>Blob 中心 X（图像列坐标 Column，单位：像素）。</summary>
        public double X { get; set; }

        /// <summary>Blob 中心 Y（图像行坐标 Row，单位：像素）。</summary>
        public double Y { get; set; }

        /// <summary>拍照瞬间机器人的 Z 坐标（与中心点绑定保存，用于高度一致性分析）。</summary>
        public double Z { get; set; }

        /// <summary>最小外接矩形角度，单位：度，已归一化到 [0,180)（矩形方向按 180° 等价处理）。</summary>
        public double Angle { get; set; }

        /// <summary>Blob 面积（单位：像素）。</summary>
        public double Area { get; set; }

        /// <summary>最小外接矩形半长（沿角度方向，单位：像素）。</summary>
        public double RectHalfLength1 { get; set; }

        /// <summary>最小外接矩形半宽（单位：像素）。</summary>
        public double RectHalfLength2 { get; set; }

        /// <summary>本次 Blob 工具检出的斑点总数（用于监控过检/漏检）。</summary>
        public int BlobCount { get; set; }

        /// <summary>选中斑点在 Blob 工具中的序号（Name）。</summary>
        public string BlobName { get; set; } = string.Empty;

        /// <summary>采集时间。</summary>
        public DateTime CaptureTime { get; set; } = DateTime.Now;

        /// <summary>点位摘要（用于日志与结果展示）。</summary>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture,
                "#{0} X={1:F3} Y={2:F3} Z={3:F3} A={4:F3}° Area={5:F3}",
                Index, X, Y, Z, Angle, Area);
    }
}
