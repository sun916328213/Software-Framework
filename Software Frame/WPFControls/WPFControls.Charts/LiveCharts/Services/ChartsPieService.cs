// 【中文阅读说明】
// WPFControls.Charts 模块：图表组件，本文件属于 LiveCharts 后端，输入转换与绘图状态归服务，业务查询与设备操作归宿主。
// 本文件定义：ChartsPieType、ChartsPieService。原先由 ChartsPieModel 包装的 Series 集合改为服务私有持有。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、Extension.UI.Controls、LiveChartsCore、WPFControls.Charts.Models。
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControls.Charts.Models;

namespace WPFControls.Controls.Services.Charts
{
    /// <summary>
    /// 饼图展示类型。
    /// </summary>
    public enum ChartsPieType
    {
        /// <summary>
        /// 甜甜圈
        /// </summary>
        Doughnut,

        /// <summary>
        /// 多个仪表盘
        /// </summary>
        MultipleGauge,
    }

    /// <summary>
    /// LiveCharts 饼图服务：负责输入转换、原生 Series 的创建，以及本后端直接相关的绘图状态。
    /// 不负责查询数据库、连接设备、启动采集定时器或决定业务数据来源。
    /// </summary>
    public class ChartsPieService : ObservableObject
    {
        // 原先由 ChartsPieModel 包装的 Series 集合，改为服务私有持有。
        private ObservableCollection<ISeries> _series = new ObservableCollection<ISeries>();

        #region 暂时不使用

        /// <summary>
        /// 添加Series。
        /// </summary>
        internal void AddSeries(ChartPieData pieCharts)
        {
            _series.Add(new PieSeries<double>
            {
                Values = new List<double> { pieCharts.Value },
                Name = pieCharts.SeriesName,
            });
        }

        /// <summary>
        /// 清除。
        /// </summary>
        public void Clear()
        {
            _series.Clear();
        }
        #endregion

        /// <summary>
        /// 显示数据：清空当前 Series 后按指定类型重新生成。
        /// </summary>
        /// <param name="pieCharts">输入数据，null 视为空数据</param>
        public ObservableCollection<ISeries> ShowData(ChartPieData[] pieCharts, ChartsPieType chartsPieType = ChartsPieType.Doughnut)
        {
            _series.Clear();
            switch (chartsPieType)
            {
                case ChartsPieType.Doughnut:
                    return _series = SetDoughnutData(pieCharts);
                case ChartsPieType.MultipleGauge:
                    return _series = SetMultipleGaugeData(pieCharts);
                default:
                    return null;
            }
        }

        #region 甜甜圈
        /// <summary>
        /// 生成甜甜圈数据；非正数与 null 项不参与绘制。
        /// </summary>
        public ObservableCollection<ISeries> SetDoughnutData(params ChartPieData[] pieCharts)
        {
            ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
            pieCharts = (pieCharts ?? Array.Empty<ChartPieData>())
                .Where(s => s is not null && s.Value > 0)
                .ToArray();

            var data = pieCharts.Select(s => s.Value);//纯数据

            int index = 0;
            IEnumerable<ISeries> ser = data.AsPieSeries((value, item) =>
            {
                // 设置饼状图属性
                item.Name = pieCharts[index++].SeriesName;

                // 设置饼状图属性
                item.MaxRadialColumnWidth = 120;

                if (LangSwitchEx.Language == ELang.ZH)
                {
                    item.DataLabelsPaint = new SolidColorPaint(SKColors.White)
                    {
                        SKTypeface = SKFontManager.Default.MatchCharacter('汉')
                    };
                }

                item.DataLabelsPosition = PolarLabelsPosition.Middle;

                // 设置饼状图标签
                item.ToolTipLabelFormatter = point =>
                {
                    var pv = point.Coordinate.PrimaryValue;
                    var sv = point.StackedValue;

                    var a = $" {pv}/{sv.Total}{Environment.NewLine}{sv.Share:P2}";
                    return a;
                };
                /// 饼状图标签
                item.DataLabelsFormatter = point =>
                {
                    var pv = point.Coordinate.PrimaryValue;
                    var sv = point.StackedValue;

                    var a = $" {pv}/{sv.Total}{Environment.NewLine}{sv.Share:P2}";
                    return a;
                };

            });

            return series = new ObservableCollection<ISeries>(ser);
        }
        #endregion

        #region 多个仪表盘
        /// <summary>
        /// 生成多仪表盘数据；零值与 null 项不参与绘制。
        /// </summary>
        public ObservableCollection<ISeries> SetMultipleGaugeData(params ChartPieData[] pieCharts)
        {
            ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
            pieCharts = (pieCharts ?? Array.Empty<ChartPieData>())
                .Where(s => s is not null && s.Value != 0)
                .ToArray();

            /// 创建仪表盘
            List<GaugeItem> gaugeItems = new List<GaugeItem>();
            foreach (var item in pieCharts)
            {
                var gaugeItem = new GaugeItem(item.Value, s => SetMultipleGaugeStyle(item, s));

                gaugeItems.Add(gaugeItem);
            }
            /// 设置背景颜色
            gaugeItems.Add(new GaugeItem(GaugeItem.Background, s => s.Fill = null));

            IEnumerable<ISeries> d = GaugeGenerator.BuildSolidGauge(gaugeItems.ToArray());
            return series = new ObservableCollection<ISeries>(d);
        }

        /// <summary>
        /// 应用单个仪表盘的名称、标签与配色。
        /// </summary>
        public static void SetMultipleGaugeStyle(ChartPieData piedata, PieSeries<ObservableValue> series)
        {
            series.Name = piedata.SeriesName;
            series.DataLabelsPosition = PolarLabelsPosition.Start;

            series.DataLabelsFormatter =
                    point => $"{point.Context.Series.Name}:{point.Coordinate.PrimaryValue} min";
            series.InnerRadius = 10;// 内半径

            series.RelativeOuterRadius = 2;// 外半径 间隔

            series.DataLabelsSize = 14;
            /// 数据标签颜色
            series.DataLabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(new SKColor(0, 0, 0));

            //// 颜色
            if (piedata.Color != null)
            {
                SKColor color = new SKColor(piedata.Color.Color.R, piedata.Color.Color.G, piedata.Color.Color.B);
                // 设置饼状图的颜色
                series.Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(color);
            }

            series.RelativeInnerRadius = 2;// 内半径 间隔

            series.CornerRadius = 2;

            series.MaxRadialColumnWidth = 15;// 饼状图宽度
        }
        #endregion

        /// <summary>
        /// 获取当前 Series 集合。
        /// </summary>
        public ObservableCollection<ISeries> GetSeries()
        {
            return _series;
        }
    }
}
