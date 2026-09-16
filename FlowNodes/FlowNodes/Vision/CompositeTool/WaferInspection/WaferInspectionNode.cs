using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Tools.Blob;
using Device.Camera.Definition;
using FlowEditor.Core;
using FlowNodes.Vision.SingleTool.Definition;
using HalconDotNet;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Vision.CompositeTool.WaferInspection
{
    /// <summary>一帧图像检出多个 Blob 时的取点规则。</summary>
    public enum E_WaferBlobSelect
    {
        /// <summary>取面积最大的斑点。</summary>
        LargestArea,

        /// <summary>取第一个斑点（Blob 工具输出顺序）。</summary>
        First,

        /// <summary>取中心距离上一点最近的斑点（跟踪同一条边缘）。</summary>
        NearestToLast
    }

    /// <summary>
    /// 硅片边缘检测复合节点（机器人带相机多点拍照场景）。
    /// <para>
    /// 执行流程：① 每次执行运行一次 Blob 工具提取硅片边缘，取斑点中心点（携带拍照时机器人 Z 坐标）
    /// 与最小外接矩形角度 → ② 多次执行的结果在本节点内累积整合 → ③ 交给
    /// <see cref="WaferEdgeCalculator"/> 计算相邻点距离方差、角度方差并识别偏离过大的数据 → ④ 输出判定结果。
    /// </para>
    /// <para>典型用法：把本节点放在循环体内（或由机器人每到一个拍照位触发一次执行），
    /// 采集满 <see cref="ExpectedCount"/> 个点后自动结算。</para>
    /// </summary>
    [FlowTool("硅片边缘检测", "CircleOutline", ToolGroup = "视觉", ToolSubGroup = "复合工具", SortIndex = 50,
        Description = "多次执行 Blob 提取硅片边缘并整合结果，计算相邻点距离方差与外接矩形角度方差，识别偏离过大的数据")]
    internal class WaferInspectionNode : HVisionToolNodeBase<HTool_Blob>
    {
        /// <summary>
        /// 宿主可注册的机器人 Z 坐标读取委托。
        /// 仅在 <see cref="AutoReadRobotZ"/> 为 true 时调用；未注册或调用失败时回退到 <see cref="RobotZ"/>。
        /// </summary>
        public static Func<double>? RobotZResolver { get; set; }

        private readonly object _sync = new object();
        private readonly List<WaferEdgePoint> _points = new List<WaferEdgePoint>();
        private WaferEdgePoint? _lastPoint;
        private bool _pendingReset;

        #region 1. 输入

        /// <summary>可选：直接接收"相机采集"节点的图像输出（ImageData），优先于基类输入图像端口。</summary>
        [NodeProperty(DisplayName = "相机图像", Group = "1.基础信息", Order = 3, IsInput = true, IsDisplay = false,
            Description = "可选，接收相机采集节点输出的 ImageData；未绑定时使用输入图像端口")]
        [JsonIgnore]
        public ImageData CameraImage { get; set; }

        #endregion

        #region 2. 采集参数

        /// <summary>拍照位机器人的 Z 坐标（可由上游节点/PLC 读取节点绑定写入）。</summary>
        [NodeProperty(DisplayName = "机器人Z坐标", Group = "2.采集参数", Order = 0, IsInput = true,
            Description = "拍照瞬间机器人的 Z 坐标，随斑点中心一起保存")]
        public double RobotZ { get; set; }

        /// <summary>是否通过宿主注册的 RobotZResolver 实时读取机器人 Z（读取失败回退到本属性值）。</summary>
        private bool _autoReadRobotZ;
        [NodeProperty(DisplayName = "自动读取机器人Z", Group = "2.采集参数", Order = 1,
            Description = "勾选后优先调用宿主注册的 RobotZResolver 读取实时 Z 坐标")]
        public bool AutoReadRobotZ
        {
            get => _autoReadRobotZ;
            set => SetProperty(ref _autoReadRobotZ, value);
        }

        /// <summary>期望采集的点位数量（机器人拍照次数）；&lt;= 0 表示不校验数量。</summary>
        private int _expectedCount = 8;
        [NodeProperty(DisplayName = "期望点位数量", Group = "2.采集参数", Order = 2, Min = 0, Step = 1,
            Description = "机器人需要拍照的点位数；采满后自动结算并判定")]
        public int ExpectedCount
        {
            get => _expectedCount;
            set => SetProperty(ref _expectedCount, value);
        }

        /// <summary>多次执行结果是否累积整合（取消勾选则每次执行只保留当前这一帧）。</summary>
        private bool _accumulate = true;
        [NodeProperty(DisplayName = "累积整合结果", Group = "2.采集参数", Order = 3,
            Description = "勾选后每次执行的 Blob 结果追加到点集合；取消则每次覆盖")]
        public bool Accumulate
        {
            get => _accumulate;
            set => SetProperty(ref _accumulate, value);
        }

        /// <summary>采满期望数量后，下一次执行是否自动清空并开启新一轮采集。</summary>
        private bool _autoRestartRound = true;
        [NodeProperty(DisplayName = "自动开启新一轮", Group = "2.采集参数", Order = 4,
            Description = "勾选后采满一轮并结算完成，下次执行自动清空点位重新开始")]
        public bool AutoRestartRound
        {
            get => _autoRestartRound;
            set => SetProperty(ref _autoRestartRound, value);
        }

        /// <summary>一帧图像检出多个斑点时的取点规则。</summary>
        private E_WaferBlobSelect _blobSelectMode = E_WaferBlobSelect.LargestArea;
        [NodeProperty(DisplayName = "斑点选取规则", Group = "2.采集参数", Order = 5,
            Description = "一帧检出多个 Blob 时，按哪种规则选取用于计算的斑点")]
        public E_WaferBlobSelect BlobSelectMode
        {
            get => _blobSelectMode;
            set => SetProperty(ref _blobSelectMode, value);
        }

        /// <summary>单帧期望的斑点数量（&lt;= 0 不校验）；不符时记录警告并计入 NG。</summary>
        private int _expectedBlobCountPerRun;
        [NodeProperty(DisplayName = "单帧期望斑点数量", Group = "2.采集参数", Order = 6, Min = 0, Step = 1,
            Description = "每次执行期望检出的斑点数量；0 表示不校验，不符说明存在过检/漏检")]
        public int ExpectedBlobCountPerRun
        {
            get => _expectedBlobCountPerRun;
            set => SetProperty(ref _expectedBlobCountPerRun, value);
        }

        #endregion

        #region 3. 判定参数

        /// <summary>相邻点间距是否计入机器人 Z 方向差值。</summary>
        private bool _useZInDistance;
        [NodeProperty(DisplayName = "距离计入Z", Group = "3.判定参数", Order = 0,
            Description = "勾选后相邻点距离按三维距离计算，否则只算图像平面距离")]
        public bool UseZInDistance
        {
            get => _useZInDistance;
            set => SetProperty(ref _useZInDistance, value);
        }

        /// <summary>相邻点距离方差上限（像素²）；&lt;= 0 表示不判定。</summary>
        private double _maxDistanceVariance;
        [NodeProperty(DisplayName = "距离方差上限", Group = "3.判定参数", Order = 1, Min = 0,
            Description = "相邻点间距方差超过该值判定 NG；0 表示不启用")]
        public double MaxDistanceVariance
        {
            get => _maxDistanceVariance;
            set => SetProperty(ref _maxDistanceVariance, value);
        }

        /// <summary>角度方差上限（度²）；&lt;= 0 表示不判定。</summary>
        private double _maxAngleVariance;
        [NodeProperty(DisplayName = "角度方差上限(度²)", Group = "3.判定参数", Order = 2, Min = 0,
            Description = "外接矩形角度方差超过该值判定 NG；0 表示不启用")]
        public double MaxAngleVariance
        {
            get => _maxAngleVariance;
            set => SetProperty(ref _maxAngleVariance, value);
        }

        /// <summary>单段距离相对均值的最大允许偏差（像素）；&lt;= 0 表示不判定。</summary>
        private double _maxDistanceDeviation;
        [NodeProperty(DisplayName = "距离偏差上限", Group = "3.判定参数", Order = 3, Min = 0,
            Description = "单段间距与均值偏差超过该值判定为偏离过大；0 表示不启用")]
        public double MaxDistanceDeviation
        {
            get => _maxDistanceDeviation;
            set => SetProperty(ref _maxDistanceDeviation, value);
        }

        /// <summary>单点角度相对均值的最大允许偏差（度）；&lt;= 0 表示不判定。</summary>
        private double _maxAngleDeviation;
        [NodeProperty(DisplayName = "角度偏差上限(度)", Group = "3.判定参数", Order = 4, Min = 0,
            Description = "单点角度与均值偏差超过该值判定为偏离过大；0 表示不启用")]
        public double MaxAngleDeviation
        {
            get => _maxAngleDeviation;
            set => SetProperty(ref _maxAngleDeviation, value);
        }

        /// <summary>西格玛倍数：偏差 &gt; SigmaK × 标准差 即判定偏离过大；&lt;= 0 表示不启用。</summary>
        private double _sigmaK = 3;
        [NodeProperty(DisplayName = "西格玛倍数", Group = "3.判定参数", Order = 5, Min = 0,
            Description = "按标准差自动识别偏离过大的数据，常用 3；0 表示不启用")]
        public double SigmaK
        {
            get => _sigmaK;
            set => SetProperty(ref _sigmaK, value);
        }

        /// <summary>判定 NG 时是否抛出异常走节点容错（重试/错误分支）；取消勾选则只置 IsOK=false 由下游分流。</summary>
        private bool _throwOnNg;
        [NodeProperty(DisplayName = "NG时抛异常", Group = "3.判定参数", Order = 6,
            Description = "勾选后 NG 抛异常交给容错策略处理；取消勾选则由下游按 IsOK 自行分流")]
        public bool ThrowOnNg
        {
            get => _throwOnNg;
            set => SetProperty(ref _throwOnNg, value);
        }

        #endregion

        #region 4. 输出结果

        private List<WaferEdgePoint> _pointsOut = new List<WaferEdgePoint>();
        /// <summary>整合后的硅片边缘点集合（含中心点、机器人 Z 与外接矩形角度），可绑定到下游节点。</summary>
        [NodeProperty(DisplayName = "点位集合", Group = "4.输出结果", Order = 0, IsOutput = true, ReadOnly = 1, IsDisplay = false)]
        [JsonIgnore]
        public List<WaferEdgePoint> Points
        {
            get => _pointsOut;
            set => SetProperty(ref _pointsOut, value);
        }

        private int _pointCount;
        /// <summary>已整合的点位数量。</summary>
        [NodeProperty(DisplayName = "点位数量", Group = "4.输出结果", Order = 1, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public int PointCount
        {
            get => _pointCount;
            set => SetProperty(ref _pointCount, value);
        }

        private WaferEdgePoint? _lastPointOut;
        /// <summary>本次执行采集到的点位。</summary>
        [NodeProperty(DisplayName = "本次点位", Group = "4.输出结果", Order = 2, IsOutput = true, ReadOnly = 1, IsDisplay = false)]
        [JsonIgnore]
        public WaferEdgePoint? LastPoint
        {
            get => _lastPointOut;
            set => SetProperty(ref _lastPointOut, value);
        }

        private double _distanceMean;
        /// <summary>相邻点距离均值。</summary>
        [NodeProperty(DisplayName = "距离均值", Group = "4.输出结果", Order = 3, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public double DistanceMean
        {
            get => _distanceMean;
            set => SetProperty(ref _distanceMean, value);
        }

        private double _distanceVariance;
        /// <summary>相邻点距离方差（总体方差）。</summary>
        [NodeProperty(DisplayName = "距离方差", Group = "4.输出结果", Order = 4, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public double DistanceVariance
        {
            get => _distanceVariance;
            set => SetProperty(ref _distanceVariance, value);
        }

        private double _angleMean;
        /// <summary>外接矩形角度均值（度）。</summary>
        [NodeProperty(DisplayName = "角度均值(度)", Group = "4.输出结果", Order = 5, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public double AngleMean
        {
            get => _angleMean;
            set => SetProperty(ref _angleMean, value);
        }

        private double _angleVariance;
        /// <summary>外接矩形角度方差（度²，总体方差）。</summary>
        [NodeProperty(DisplayName = "角度方差(度²)", Group = "4.输出结果", Order = 6, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public double AngleVariance
        {
            get => _angleVariance;
            set => SetProperty(ref _angleVariance, value);
        }

        private int _outlierCount;
        /// <summary>偏离过大的数据条数（段距离 + 点角度）。</summary>
        [NodeProperty(DisplayName = "偏离过大数量", Group = "4.输出结果", Order = 7, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public int OutlierCount
        {
            get => _outlierCount;
            set => SetProperty(ref _outlierCount, value);
        }

        private string _outlierDetail = string.Empty;
        /// <summary>偏离过大数据的明细。</summary>
        [NodeProperty(DisplayName = "偏离过大明细", Group = "4.输出结果", Order = 8, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public string OutlierDetail
        {
            get => _outlierDetail;
            set => SetProperty(ref _outlierDetail, value);
        }

        private bool _isCompleted;
        /// <summary>是否已采集完期望数量的点位（未完成时判定结果仅供参考）。</summary>
        [NodeProperty(DisplayName = "采集完成", Group = "4.输出结果", Order = 9, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        private bool _isOK;
        /// <summary>最终判定结果：true = OK，false = NG。</summary>
        [NodeProperty(DisplayName = "判定OK", Group = "4.输出结果", Order = 10, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public bool IsOK
        {
            get => _isOK;
            set => SetProperty(ref _isOK, value);
        }

        private string _ngReason = string.Empty;
        /// <summary>NG 原因（多条以；分隔）。</summary>
        [NodeProperty(DisplayName = "NG原因", Group = "4.输出结果", Order = 11, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public string NgReason
        {
            get => _ngReason;
            set => SetProperty(ref _ngReason, value);
        }

        private string _resultSummary = string.Empty;
        /// <summary>本次统计摘要（用于日志与界面展示）。</summary>
        [NodeProperty(DisplayName = "结果摘要", Group = "4.输出结果", Order = 12, IsOutput = true, ReadOnly = 1)]
        [JsonIgnore]
        public string ResultSummary
        {
            get => _resultSummary;
            set => SetProperty(ref _resultSummary, value);
        }

        private WaferInspectResult? _statistics;
        /// <summary>完整的统计分析结果对象（含距离/角度序列与分段明细）。</summary>
        [NodeProperty(DisplayName = "统计结果", Group = "4.输出结果", Order = 13, IsOutput = true, ReadOnly = 1, IsDisplay = false)]
        [JsonIgnore]
        public WaferInspectResult? Statistics
        {
            get => _statistics;
            set => SetProperty(ref _statistics, value);
        }

        #endregion

        #region 执行

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();

            HObject? image = ResolveInputImage();
            if (image == null || !image.IsInitialized())
            {
                string msg = "硅片检测失败：未获取到有效输入图像（请绑定相机采集节点的图像输出）";
                await NodeLog.ErrorAsync(msg);
                throw new InvalidOperationException(msg);
            }
            Image = image;

            // 新一轮：清空上一轮整合结果
            bool restart;
            lock (_sync)
            {
                restart = _pendingReset;
                if (restart || !Accumulate)
                {
                    _points.Clear();
                    _lastPoint = null;
                    _pendingReset = false;
                }
            }
            if (restart)
                await NodeLog.InfoAsync("上一轮已采满，自动开启新一轮采集");

            // ① 执行 Blob 工具提取硅片边缘
            RunTool();

            if (visionTool.ToolState == E_ToolState.Failed)
            {
                string msg = $"硅片检测失败：Blob 工具执行异常 - {visionTool.RunMessage}";
                await NodeLog.ErrorAsync(msg);
                throw new InvalidOperationException(msg);
            }

            var blobs = visionTool.Results?.OfType<HBlobResult>().ToList() ?? new List<HBlobResult>();
            if (blobs.Count == 0)
            {
                string msg = $"硅片检测失败：本次采集未检出任何硅片边缘 Blob（图像可能过曝/欠曝或阈值不合适）";
                await NodeLog.ErrorAsync(msg);
                throw new InvalidOperationException(msg);
            }

            if (ExpectedBlobCountPerRun > 0 && blobs.Count != ExpectedBlobCountPerRun)
                await NodeLog.WarnAsync($"本次 Blob 检出 {blobs.Count} 个斑点，与单帧期望数量 {ExpectedBlobCountPerRun} 不符，可能存在过检/漏检");

            // ② 整合结果：中心点（带机器人 Z）+ 最小外接矩形角度
            HBlobResult selected = SelectBlob(blobs);
            WaferEdgePoint point = await BuildPointAsync(selected, blobs.Count);

            lock (_sync)
            {
                point.Index = _points.Count + 1;
                _points.Add(point);
                _lastPoint = point;
                if (ExpectedCount > 0 && _points.Count >= ExpectedCount)
                    _pendingReset = AutoRestartRound;
            }

            LastPoint = point;
            await NodeLog.InfoAsync(
                $"采集第 {point.Index} 点：Blob {point.BlobName}/{point.BlobCount}，" +
                $"中心 (X={point.X:F3}, Y={point.Y:F3}, Z={point.Z:F3})，角度 {point.Angle:F3}°，" +
                $"外接矩形 {point.RectHalfLength1 * 2:F3}×{point.RectHalfLength2 * 2:F3}，面积 {point.Area:F3}，耗时 {sw.ElapsedMilliseconds} ms");

            // ③ 传递给计算工具做方差统计与偏离判定
            await RefreshStatisticsAsync();

            // ④ 结果判定
            if (!IsOK && ThrowOnNg && IsCompleted)
                throw new InvalidOperationException($"硅片检测 NG：{NgReason}");
        }

        /// <summary>整合后的点位集合重新统计（不执行视觉，仅重算方差与判定）。</summary>
        [NodeAction("重新统计", Order = 1, IconResource = "Calculator")]
        public void ReAnalyze()
        {
            // 按钮回调在 UI 线程，统计含异步日志写入，放到线程池避免阻塞界面
            _ = Task.Run(RefreshStatisticsAsync);
        }

        /// <summary>清空已整合的点位集合与统计结果。</summary>
        [NodeAction("清空采集", Order = 2, IconResource = "Delete")]
        public void ClearCollectedPoints()
        {
            lock (_sync)
            {
                _points.Clear();
                _lastPoint = null;
                _pendingReset = false;
            }

            Points = new List<WaferEdgePoint>();
            Statistics = null;
            LastPoint = null;
            PointCount = 0;
            DistanceMean = 0;
            DistanceVariance = 0;
            AngleMean = 0;
            AngleVariance = 0;
            OutlierCount = 0;
            OutlierDetail = string.Empty;
            IsCompleted = false;
            IsOK = false;
            NgReason = string.Empty;
            ResultSummary = string.Empty;
            ResultCount = 0;

            _ = NodeLog.InfoAsync("已清空硅片边缘采集结果");
        }

        /// <summary>把整合结果交给计算工具统计，刷新所有输出并记录日志。</summary>
        private async Task RefreshStatisticsAsync()
        {
            List<WaferEdgePoint> snapshot;
            lock (_sync)
                snapshot = new List<WaferEdgePoint>(_points);

            var options = new WaferStatOptions
            {
                ExpectedCount = ExpectedCount,
                UseZInDistance = UseZInDistance,
                MaxDistanceVariance = MaxDistanceVariance,
                MaxAngleVariance = MaxAngleVariance,
                MaxDistanceDeviation = MaxDistanceDeviation,
                MaxAngleDeviation = MaxAngleDeviation,
                SigmaK = SigmaK
            };

            WaferInspectResult result = WaferEdgeCalculator.Analyze(snapshot, options);

            Points = snapshot;
            Statistics = result;
            PointCount = result.Count;
            DistanceMean = result.DistanceMean;
            DistanceVariance = result.DistanceVariance;
            AngleMean = result.AngleMean;
            AngleVariance = result.AngleVariance;
            OutlierCount = result.OutlierCount;
            OutlierDetail = result.OutlierDetail;
            IsCompleted = result.IsCompleted;
            IsOK = result.IsOK;
            NgReason = string.Join("；", result.NgReasons);
            ResultSummary = result.Summary;

            string progress = ExpectedCount > 0 ? $"{result.Count}/{ExpectedCount}" : $"{result.Count}";

            if (!result.IsCompleted)
            {
                await NodeLog.InfoAsync($"采集中：已整合 {progress} 点，继续采集后结算");
                return;
            }

            await NodeLog.InfoAsync($"统计分析：{result.Summary}");

            if (result.IsOK)
            {
                await NodeLog.InfoAsync($"硅片检测 OK：{progress} 点全部通过");
                return;
            }

            await NodeLog.ErrorAsync($"硅片检测 NG：{NgReason}");
            foreach (string reason in result.NgReasons)
                await NodeLog.WarnAsync($"判定异常 - {reason}");

            if (result.OutlierCount > 0)
                await NodeLog.WarnAsync($"偏离过大明细：{result.OutlierDetail}");

            if (snapshot.Count > 0)
                await NodeLog.InfoAsync("点位明细：" + string.Join(" | ", snapshot.Select(p => p.ToString())));
        }

        private HObject? ResolveInputImage()
        {
            // CameraImage 为结构体，未绑定上游时 RawData 为空、宽高为 0，此时回退到基类输入图像端口
            ImageData cameraImage = CameraImage;
            if (cameraImage.RawData != null && cameraImage.Width > 0 && cameraImage.Height > 0)
                return ConvertToHObject(cameraImage);
            return Image;
        }

        private static HObject? ConvertToHObject(ImageData data)
        {
            if (data.RawData == null || data.RawData.Length == 0 || data.Width <= 0 || data.Height <= 0)
                return null;

            int pixelCount = data.Width * data.Height;
            int bytesPerPixel = pixelCount > 0 ? data.RawData.Length / pixelCount : 1;

            IntPtr ptr = Marshal.AllocHGlobal(data.RawData.Length);
            try
            {
                Marshal.Copy(data.RawData, 0, ptr, data.RawData.Length);
                HObject image;
                if (bytesPerPixel >= 3)
                {
                    // 彩色图按 BGR 交错生成（GenImageInterleaved 内部会拷贝数据，可安全释放 ptr）
                    HOperatorSet.GenImageInterleaved(out image, (HTuple)ptr, "bgr",
                        data.Width, data.Height, 0, "byte", data.Width, data.Height, 0, 0, 8, 0);
                }
                else
                {
                    HOperatorSet.GenImage1(out image, bytesPerPixel == 2 ? "uint2" : "byte",
                        data.Width, data.Height, ptr);
                }
                return image;
            }
            catch
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private HBlobResult SelectBlob(List<HBlobResult> blobs)
        {
            if (blobs.Count == 1) return blobs[0];

            switch (BlobSelectMode)
            {
                case E_WaferBlobSelect.First:
                    return blobs[0];

                case E_WaferBlobSelect.NearestToLast:
                    WaferEdgePoint? last = _lastPoint;
                    if (last == null)
                        return blobs.OrderByDescending(ParseArea).First();
                    return blobs
                        .OrderBy(b => (b.Center.X - last.X) * (b.Center.X - last.X)
                                    + (b.Center.Y - last.Y) * (b.Center.Y - last.Y))
                        .First();

                default:
                    return blobs.OrderByDescending(ParseArea).First();
            }
        }

        private async Task<WaferEdgePoint> BuildPointAsync(HBlobResult blob, int blobCount)
        {
            var point = new WaferEdgePoint
            {
                X = blob.Center.X,
                Y = blob.Center.Y,
                Z = ResolveRobotZ(),
                Area = ParseArea(blob),
                BlobCount = blobCount,
                BlobName = blob.Name ?? string.Empty
            };

            HRegion? region = blob.ResultRegion?.GetRegion();
            if (region == null || !region.IsInitialized())
            {
                await NodeLog.WarnAsync($"Blob {blob.Name} 无有效区域，无法计算最小外接矩形角度，角度按 0 处理");
                return point;
            }

            try
            {
                HOperatorSet.SmallestRectangle2(region, out HTuple row, out HTuple col,
                    out HTuple phi, out HTuple length1, out HTuple length2);
                point.Angle = WaferEdgeCalculator.NormalizeAngle(phi.D * 180.0 / Math.PI);
                point.RectHalfLength1 = length1.D;
                point.RectHalfLength2 = length2.D;
            }
            catch (Exception ex)
            {
                await NodeLog.WarnAsync($"计算 Blob {blob.Name} 最小外接矩形失败：{ex.Message}");
            }

            return point;
        }

        private double ResolveRobotZ()
        {
            if (!AutoReadRobotZ) return RobotZ;

            Func<double>? resolver = RobotZResolver;
            if (resolver == null)
                return RobotZ;

            try
            {
                return resolver();
            }
            catch (Exception ex)
            {
                _ = NodeLog.WarnAsync($"读取机器人 Z 坐标失败，回退使用配置值 {RobotZ}：{ex.Message}");
                return RobotZ;
            }
        }

        private static double ParseArea(HBlobResult blob)
            => double.TryParse(blob.Area, NumberStyles.Any, CultureInfo.InvariantCulture, out double area) ? area : 0;

        #endregion
    }
}
