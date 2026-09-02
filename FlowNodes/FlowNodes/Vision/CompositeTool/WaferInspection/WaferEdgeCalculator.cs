using System.Globalization;

namespace FlowNodes.Vision.CompositeTool.WaferInspection
{
    /// <summary>
    /// 硅片边缘点集合的统计判定参数。所有上限类参数 &lt;= 0 表示不参与判定。
    /// </summary>
    public class WaferStatOptions
    {
        /// <summary>期望点位数量；&lt;= 0 表示不校验数量（此时达到 1 点即视为完成）。</summary>
        public int ExpectedCount { get; set; }

        /// <summary>相邻点距离是否计入机器人 Z 方向差值（false 时只算图像平面距离）。</summary>
        public bool UseZInDistance { get; set; }

        /// <summary>距离方差上限（像素²）。</summary>
        public double MaxDistanceVariance { get; set; }

        /// <summary>角度方差上限（度²）。</summary>
        public double MaxAngleVariance { get; set; }

        /// <summary>单段距离相对均值的最大允许偏差（绝对值，像素）。</summary>
        public double MaxDistanceDeviation { get; set; }

        /// <summary>单点角度相对均值的最大允许偏差（绝对值，度）。</summary>
        public double MaxAngleDeviation { get; set; }

        /// <summary>西格玛倍数：偏差 &gt; SigmaK × 标准差 时判定偏离过大（&lt;= 0 时停用）。</summary>
        public double SigmaK { get; set; }
    }

    /// <summary>相邻两个采集点之间的一段间距及其偏离情况。</summary>
    public class WaferEdgeSegment
    {
        /// <summary>段起点点位序号（从 1 开始）。</summary>
        public int StartIndex { get; set; }

        /// <summary>段终点点位序号（从 1 开始）。</summary>
        public int EndIndex { get; set; }

        /// <summary>本段距离（像素，启用 Z 时为三维距离）。</summary>
        public double Distance { get; set; }

        /// <summary>本段距离与距离均值的偏差绝对值。</summary>
        public double Deviation { get; set; }

        /// <summary>是否判定为偏离过大。</summary>
        public bool IsOutlier { get; set; }

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "#{0}-#{1} 距离={2:F3} 偏差={3:F3}{4}",
                StartIndex, EndIndex, Distance, Deviation, IsOutlier ? " [偏离过大]" : "");
    }

    /// <summary>硅片边缘点集合的统计分析结果。</summary>
    public class WaferInspectResult
    {
        /// <summary>参与统计的点位数量。</summary>
        public int Count { get; set; }

        /// <summary>是否已达到期望点位数量（未完成时统计结果仅供参考）。</summary>
        public bool IsCompleted { get; set; }

        /// <summary>相邻点距离序列（长度 = Count - 1）。</summary>
        public List<double> Distances { get; set; } = new List<double>();

        /// <summary>各点外接矩形角度序列（单位：度）。</summary>
        public List<double> Angles { get; set; } = new List<double>();

        /// <summary>相邻点分段明细（含偏离判定）。</summary>
        public List<WaferEdgeSegment> Segments { get; set; } = new List<WaferEdgeSegment>();

        /// <summary>各点角度与角度均值的偏差绝对值。</summary>
        public List<double> AngleDeviations { get; set; } = new List<double>();

        /// <summary>角度偏离过大的点位序号（从 1 开始）。</summary>
        public List<int> AngleOutlierIndexes { get; set; } = new List<int>();

        public double DistanceMean { get; set; }
        public double DistanceVariance { get; set; }
        public double DistanceStdDev { get; set; }
        public double DistanceMin { get; set; }
        public double DistanceMax { get; set; }

        public double AngleMean { get; set; }
        public double AngleVariance { get; set; }
        public double AngleStdDev { get; set; }
        public double AngleMin { get; set; }
        public double AngleMax { get; set; }

        /// <summary>偏离过大的数据条数（段距离 + 点角度）。</summary>
        public int OutlierCount { get; set; }

        /// <summary>偏离过大数据的文字明细。</summary>
        public string OutlierDetail { get; set; } = string.Empty;

        /// <summary>不合格原因列表（为空即判定 OK）。</summary>
        public List<string> NgReasons { get; set; } = new List<string>();

        /// <summary>最终判定结果。</summary>
        public bool IsOK { get; set; }

        /// <summary>统计摘要。</summary>
        public string Summary { get; set; } = string.Empty;

        public override string ToString() => Summary;
    }

    /// <summary>
    /// 硅片边缘计算工具：对整合后的点位集合计算相邻点距离方差与外接矩形角度方差，
    /// 并识别偏离过大的数据（数量不符 / 方差超标 / 单条数据偏离均值为过大）。
    /// </summary>
    /// <remarks>
    /// 方差统一采用总体方差（除以 n），标准差为其平方根；样本方差可按需另行换算。
    /// </remarks>
    public static class WaferEdgeCalculator
    {
        /// <summary>对点位集合执行统计分析。</summary>
        /// <param name="points">整合后的采集点位（按采集顺序）。</param>
        /// <param name="options">判定参数，传 null 时使用默认参数（只统计不判定）。</param>
        public static WaferInspectResult Analyze(IReadOnlyList<WaferEdgePoint>? points, WaferStatOptions? options)
        {
            var opt = options ?? new WaferStatOptions();
            var result = new WaferInspectResult();

            if (points == null || points.Count == 0)
            {
                result.NgReasons.Add("未采集到任何硅片边缘点");
                result.Summary = BuildSummary(result, opt, "无采集点");
                result.IsOK = false;
                return result;
            }

            result.Count = points.Count;
            result.IsCompleted = opt.ExpectedCount <= 0 || points.Count >= opt.ExpectedCount;
            result.Angles = points.Select(p => p.Angle).ToList();

            // ── 角度统计 ──
            var angleStat = Calc(result.Angles);
            result.AngleMean = angleStat.Mean;
            result.AngleVariance = angleStat.Variance;
            result.AngleStdDev = angleStat.StdDev;
            result.AngleMin = angleStat.Min;
            result.AngleMax = angleStat.Max;

            // ── 相邻点距离统计 ──
            if (points.Count >= 2)
            {
                for (int i = 0; i < points.Count - 1; i++)
                    result.Distances.Add(Distance(points[i], points[i + 1], opt.UseZInDistance));

                var distStat = Calc(result.Distances);
                result.DistanceMean = distStat.Mean;
                result.DistanceVariance = distStat.Variance;
                result.DistanceStdDev = distStat.StdDev;
                result.DistanceMin = distStat.Min;
                result.DistanceMax = distStat.Max;
            }

            // ── 偏离过大的数据识别 ──
            foreach (var d in result.Distances)
            {
                double dev = Math.Abs(d - result.DistanceMean);
                bool outlier = IsOutlier(dev, opt.MaxDistanceDeviation, opt.SigmaK * result.DistanceStdDev);
                int start = result.Segments.Count + 1;
                result.Segments.Add(new WaferEdgeSegment
                {
                    StartIndex = start,
                    EndIndex = start + 1,
                    Distance = d,
                    Deviation = dev,
                    IsOutlier = outlier
                });
            }

            for (int i = 0; i < result.Angles.Count; i++)
            {
                double dev = Math.Abs(result.Angles[i] - result.AngleMean);
                result.AngleDeviations.Add(dev);
                if (IsOutlier(dev, opt.MaxAngleDeviation, opt.SigmaK * result.AngleStdDev))
                    result.AngleOutlierIndexes.Add(i + 1);
            }

            result.OutlierCount = result.Segments.Count(s => s.IsOutlier) + result.AngleOutlierIndexes.Count;
            result.OutlierDetail = BuildOutlierDetail(result);

            // ── 判定 ──
            if (points.Count < 2)
                result.NgReasons.Add("采集点不足：至少需要 2 个点才能计算距离方差");

            if (result.IsCompleted && opt.ExpectedCount > 0 && points.Count != opt.ExpectedCount)
                result.NgReasons.Add($"点位数量异常：期望 {opt.ExpectedCount} 点，实际 {points.Count} 点");

            if (opt.MaxDistanceVariance > 0 && points.Count >= 2 && result.DistanceVariance > opt.MaxDistanceVariance)
                result.NgReasons.Add($"距离方差超标：{result.DistanceVariance:F4} > 上限 {opt.MaxDistanceVariance:F4}");

            if (opt.MaxAngleVariance > 0 && result.AngleVariance > opt.MaxAngleVariance)
                result.NgReasons.Add($"角度方差超标：{result.AngleVariance:F4} > 上限 {opt.MaxAngleVariance:F4}");

            foreach (var seg in result.Segments.Where(s => s.IsOutlier))
                result.NgReasons.Add($"第 {seg.StartIndex}-{seg.EndIndex} 点间距偏离过大：{seg.Distance:F3}" +
                                     $"（均值 {result.DistanceMean:F3}，偏差 {seg.Deviation:F3}）");

            foreach (int idx in result.AngleOutlierIndexes)
                result.NgReasons.Add($"第 {idx} 点角度偏离过大：{result.Angles[idx - 1]:F3}°" +
                                     $"（均值 {result.AngleMean:F3}°，偏差 {result.AngleDeviations[idx - 1]:F3}°）");

            result.IsOK = result.NgReasons.Count == 0;
            result.Summary = BuildSummary(result, opt, result.IsOK ? "OK" : "NG");
            return result;
        }

        /// <summary>两点间距；<paramref name="useZ"/> 为 true 时计入机器人 Z 方向差值。</summary>
        public static double Distance(WaferEdgePoint a, WaferEdgePoint b, bool useZ)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            if (!useZ) return Math.Sqrt(dx * dx + dy * dy);
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>把角度归一化到 [0,180)（外接矩形方向 180° 等价）。</summary>
        public static double NormalizeAngle(double degree)
        {
            double a = degree % 180.0;
            if (a < 0) a += 180.0;
            return a;
        }

        /// <summary>偏离判定：绝对阈值与西格玛阈值满足其一即成立（阈值 &lt;= 0 的项不参与）。</summary>
        private static bool IsOutlier(double deviation, double absThreshold, double sigmaThreshold)
        {
            if (absThreshold > 0 && deviation > absThreshold) return true;
            if (sigmaThreshold > 0 && deviation > sigmaThreshold) return true;
            return false;
        }

        private readonly struct StatResult
        {
            public double Mean { get; init; }
            public double Variance { get; init; }
            public double StdDev { get; init; }
            public double Min { get; init; }
            public double Max { get; init; }
        }

        /// <summary>计算均值、总体方差、标准差与极值；空序列返回全 0。</summary>
        private static StatResult Calc(List<double> values)
        {
            if (values == null || values.Count == 0)
                return new StatResult();

            double sum = 0;
            foreach (var v in values) sum += v;
            double mean = sum / values.Count;

            double squareSum = 0;
            foreach (var v in values) squareSum += (v - mean) * (v - mean);

            // 总体方差（除以 n），单点时方差为 0
            double variance = squareSum / values.Count;
            return new StatResult
            {
                Mean = mean,
                Variance = variance,
                StdDev = Math.Sqrt(variance),
                Min = values.Min(),
                Max = values.Max()
            };
        }

        private static string BuildOutlierDetail(WaferInspectResult result)
        {
            var items = new List<string>();
            foreach (var seg in result.Segments.Where(s => s.IsOutlier))
                items.Add(seg.ToString());
            foreach (int idx in result.AngleOutlierIndexes)
                items.Add(string.Format(CultureInfo.InvariantCulture, "#{0} 角度={1:F3} 偏差={2:F3}° [偏离过大]",
                    idx, result.Angles[idx - 1], result.AngleDeviations[idx - 1]));
            return items.Count == 0 ? "无" : string.Join("; ", items);
        }

        private static string BuildSummary(WaferInspectResult result, WaferStatOptions opt, string verdict)
        {
            string countText = opt.ExpectedCount > 0
                ? $"{result.Count}/{opt.ExpectedCount}"
                : $"{result.Count}";

            return string.Format(CultureInfo.InvariantCulture,
                "点数 {0} | 间距 均值={1:F3} 方差={2:F4} σ={3:F4} 范围=[{4:F3},{5:F3}]" +
                " | 角度 均值={6:F3}° 方差={7:F4} σ={8:F4} 范围=[{9:F3},{10:F3}]" +
                " | 偏离过大={11} | 判定={12}{13}",
                countText,
                result.DistanceMean, result.DistanceVariance, result.DistanceStdDev, result.DistanceMin, result.DistanceMax,
                result.AngleMean, result.AngleVariance, result.AngleStdDev, result.AngleMin, result.AngleMax,
                result.OutlierCount, verdict,
                result.IsCompleted ? string.Empty : "（采集中）");
        }
    }
}
