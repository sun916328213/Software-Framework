// 【中文阅读说明】
// WPFControls.Charts 模块：图表组件，本文件属于 LiveCharts 后端，输入转换与绘图状态归服务，业务查询与设备操作归宿主。
// 本文件定义：ChartsLineType、ChartsLineService。原先由 ChartsLineModel 包装的绘图状态改为服务私有持有。
// 直接导入的协作命名空间包括：LiveChartsCore、LiveChartsCore.Defaults、LiveChartsCore.Kernel.Sketches、WPFControls.Charts.Models。
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControls.Charts.Models;

namespace WPFControls.Controls.Services.Charts
{
    /// <summary>
    /// 折线图展示类型。
    /// </summary>
    public enum ChartsLineType
    {
        /// <summary>
        /// 基础折线图
        /// </summary>
        BaseLine,
    }

    /// <summary>
    /// LiveCharts 折线图服务：负责输入转换、原生 Series 与坐标轴的创建，以及本后端直接相关的绘图状态。
    /// 不负责查询数据库、连接设备、启动采集定时器或决定业务数据来源。
    /// </summary>
    public class ChartsLineService
    {
        private readonly Action<Action> _execute;

        // 以下状态原先由 ChartsLineModel 持有；减少类不等于删除状态，语义与默认值保持不变。
        private ObservableCollection<ISeries> _series = new ObservableCollection<ISeries>();
        private readonly ObservableCollection<ICartesianAxis> _axisX = new ObservableCollection<ICartesianAxis>();
        private readonly ObservableCollection<ChartLineData> _dataCollection = new ObservableCollection<ChartLineData>();
        private double _pointSize = 10;
        private double _axisFontSize = 14;

        /// <summary>
        /// 初始化 ChartsLineService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ChartsLineService(Action<Action>? execute = null)
        {
            _execute = execute ?? ExecuteWithDiagnostics;

            _axisX.Add(new Axis
            {
                Labels = new List<string>(),
            });
        }

        // Hosts can supply their own error executor without a reference back to WPFControls.
        private static void ExecuteWithDiagnostics(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                try { System.Diagnostics.Trace.TraceError($"[Charts] {error.Message}"); }
                catch { /* Diagnostics must not replace the original operation result. */ }
            }
        }

        /// <summary>
        /// 设置点大小，后续新建的曲线使用该值。
        /// </summary>
        public void SetPointSize(double pointSize)
        {
            _pointSize = pointSize;
        }

        /// <summary>
        /// 设置X轴标签字体大小，并立即应用到当前已存在的轴。
        /// </summary>
        public void SetAxisFontSize(double fontSize = 14)
        {
            _axisFontSize = fontSize;
            foreach (var axis in _axisX)
            {
                axis.TextSize = fontSize;
            }
        }

        /// <summary>
        /// 获取当前 Series 集合。
        /// </summary>
        public ObservableCollection<ISeries> GetSeries()
        {
            return _series;
        }

        /// <summary>
        /// 获取X轴集合。
        /// </summary>
        public ObservableCollection<ICartesianAxis> GetAxisX()
        {
            return _axisX;
        }

        #region 暂时不使用

        /// <summary>
        /// 添加Series。
        /// </summary>
        public void AddSeries(ChartLineData lineCharts)
        {
            _execute(() =>
            {
                // 创建一个新的输入对象并复制所有属性
                var newSeries = new ChartLineData
                {
                    SeriesName = lineCharts.SeriesName,
                    Value = lineCharts.Value
                };

                _dataCollection.Add(newSeries);
                _series.Add(new LineSeries<double>
                {
                    Values = newSeries.Value,
                    Name = lineCharts.SeriesName,
                    Fill = null,
                    GeometrySize = _pointSize,
                });
            });
        }

        /// <summary>
        /// 添加数据 速度较慢
        /// </summary>
        /// <param name="axisx">x轴信息</param>
        /// <param name="lineCharts">Series信息</param>
        public void AddXY(List<string> axisx, ChartLineData lineCharts)
        {
            _execute(() =>
            {
                var ret = _series.FirstOrDefault(s => s.Name == lineCharts.SeriesName);
                if (ret == null) AddSeries(lineCharts);

                var iLineSeries = _dataCollection.FirstOrDefault(s => s.SeriesName == lineCharts.SeriesName);

                _axisX[0].Labels = axisx;
                for (int i = 0; i < lineCharts.Value.Count; i++)
                {
                    if (iLineSeries != null) iLineSeries.Value.Add(lineCharts.Value[i]);
                }
            });
        }

        /// <summary>
        /// 清除曲线与坐标标签。
        /// </summary>
        public void Clear()
        {
            _execute(() =>
            {
                _series.Clear();
                _axisX[0].Labels = new List<string>();
            });
        }
        #endregion

        /// <summary>
        /// 设置AxisX标签，并重新应用字体大小。
        /// </summary>
        public void SetAxisX(List<string> axisX)
        {
            _axisX[0].Labels = axisX;
            foreach (var axis in _axisX)
            {
                axis.TextSize = _axisFontSize;
            }
        }

        /// <summary>
        /// 显示数据：清空当前曲线后按指定类型重新生成。
        /// </summary>
        /// <param name="lineCharts">输入数据，null 视为空数据</param>
        public ObservableCollection<ISeries> ShowData(ChartLineData[] lineCharts, ChartsLineType chartsLineType = ChartsLineType.BaseLine)
        {
            switch (chartsLineType)
            {
                case ChartsLineType.BaseLine:
                    _series.Clear();
                    _series = SetBaseLineData(lineCharts);
                    return _series;
                default: return null;
            }
        }

        /// <summary>
        /// 设置基础线样式。
        /// </summary>
        public ObservableCollection<ISeries> SetBaseLineData(ChartLineData[] lineCharts)
        {
            ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
            foreach (var item in lineCharts ?? Array.Empty<ChartLineData>())
            {
                if (item is null) continue;

                LineSeries<double> lineseries = new LineSeries<double>();
                // 设置数据 - 直接使用ObservableCollection，这样当数据变化时图表会自动更新
                lineseries.Values = item.Value ?? new ObservableCollection<double>();
                // 设置标签
                lineseries.Name = item.SeriesName;

                lineseries.DataLabelsSize = 14;

                // 设置标签颜色
                lineseries.DataLabelsPaint = new SolidColorPaint(new SKColor(0, 0, 0)); // 黑色

                // 格式化标签显示内容
                lineseries.DataLabelsFormatter = (point) => point.Coordinate.PrimaryValue.ToString();

                // 标签显示位置
                lineseries.DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right;

                //// 颜色
                if (item.Color != null)
                {
                    SKColor color = new SKColor(item.Color.Color.R, item.Color.Color.G, item.Color.Color.B);

                    float width = 3F;
                    // 设置折线的颜色
                    lineseries.Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(color, width);
                    //设置数据点颜色
                    lineseries.GeometryStroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(color, width);

                    lineseries.GeometrySize = 14;
                }

                //裁剪模式
                lineseries.ClippingMode = LiveChartsCore.Measure.ClipMode.None;

                // 设置曲线平滑度
                lineseries.LineSmoothness = 0.5;

                lineseries.DataPadding = new LiveChartsCore.Drawing.LvcPoint(0.5, -20);

                series.Add(lineseries);
            }
            return series;
        }
    }
}
