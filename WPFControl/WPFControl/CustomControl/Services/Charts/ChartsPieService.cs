using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.Helper;

namespace WPFControl.CustomControl.Services.Charts
{
    public enum ChartsPieType
    {
        /// <summary>
        ///    甜甜圈
        /// </summary>
        Doughnut,
        
       /// <summary>
       /// 多个仪表盘
       /// </summary>
        MultipleGauge,
    }

    public class ChartsPieService : ObservableObject
    {
        /// <summary>
        /// ChartsPie模型
        /// </summary>
        private ChartsPieModel Model { get; set; } = new ChartsPieModel();


        #region 暂时不使用

        /// <summary>
        /// 添加Series
        /// </summary>
        /// <param name="pieCharts"></param>
        /// <returns></returns>
        internal void AddSeries(PieData pieCharts)
        {
            Model.Series.Add(new PieSeries<double>
            {
                Values = new List<double> { pieCharts.Value },
                Name = pieCharts.SeriesName,
            });
        }
 
        /// <summary>
        /// 清除
        /// </summary>
        public void Clear()
        {
            Model.Series.Clear();

        }
        #endregion

        /// <summary>
        /// 显示数据
        /// </summary>
        /// <param name="pieCharts">PieSeries数据集合</param>
        public ObservableCollection<ISeries> ShowData(PieData[] pieCharts, ChartsPieType chartsPieType = ChartsPieType.Doughnut)
        {

            Model.Series.Clear();
            switch (chartsPieType)
            {
                case ChartsPieType.Doughnut:
                  return  Model.Series =SetDoughnutData(pieCharts);
                case ChartsPieType.MultipleGauge:
                    return Model.Series = SetMultipleGaugeData(pieCharts);
                default:
                    return null;
            }
        }


        #region 甜甜圈
        public ObservableCollection<ISeries> SetDoughnutData(params PieData[] pieCharts)
        {
            ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
            pieCharts = pieCharts.Where(s => s.Value > 0).ToArray();

            var data = pieCharts.Select(s => s.Value);//纯数据

            int index=0;
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
        public ObservableCollection<ISeries> SetMultipleGaugeData(params PieData[] pieCharts)
        {
            ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
            pieCharts = pieCharts.Where(s => s.Value != 0).ToArray();
 
         
            /// 创建仪表盘
            List<GaugeItem> gaugeItems = new List<GaugeItem>();
            foreach (var item in pieCharts)
            {
                var gaugeItem = new GaugeItem(item.Value, s => SetMultipleGaugeStyle(item, s));
               
                gaugeItems.Add(gaugeItem);
            }
            /// 设置背景颜色
            gaugeItems.Add(new GaugeItem(GaugeItem.Background,s=>s.Fill=null));
            

            IEnumerable<ISeries> d = GaugeGenerator.BuildSolidGauge(gaugeItems.ToArray());
            return series=new ObservableCollection<ISeries>(d);
        }


        public static void SetMultipleGaugeStyle(PieData piedata, PieSeries<ObservableValue> series)
        {
            series.Name = piedata.SeriesName;
            series.DataLabelsPosition = PolarLabelsPosition.Start;


            series.DataLabelsFormatter =
                    point => $"{point.Context.Series.Name}:{point.Coordinate.PrimaryValue} min";
            series.InnerRadius = 10;// 内半径

            series.RelativeOuterRadius = 2;// 外半径 间隔

            series.DataLabelsSize = 14;
            /// 数据标签颜色
            series.DataLabelsPaint=  new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(new SKColor(0,0,0));

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
        /// 获取饼状图数据
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<ISeries> GetSeries()
        {
            return Model.Series;
        }
    }
}