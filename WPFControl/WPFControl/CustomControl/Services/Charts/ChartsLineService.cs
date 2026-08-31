using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.Helper;

namespace WPFControl.CustomControl.Services.Charts
{
    public enum ChartsLineType
    {
        /// <summary>
        /// 基础折线图
        /// </summary>
        BaseLine,
       
    }

    public class ChartsLineService
    {
        /// <summary>
        /// ChartsLine模型
        /// </summary>
        private ChartsLineModel Model { get; set; } = new ChartsLineModel();

        public ChartsLineService()
        {
            Model.Axis_X.Add(new Axis
            {
                Labels = new List<string>(),
            });
        }

        /// <summary>
        /// 设置点大小
        /// </summary>
        /// <param name="pointSize"></param>
        public void SetPointSize(double pointSize)
        {
            Model.PointSize = pointSize;
        }

        /// <summary>
        /// 设置X轴标签字体大小 
        /// </summary>
        public void SetAxisFontSize(double fontSize = 14)
        {
            Model.AxisFontSize = fontSize;
            // 同步应用到当前所有已存在的轴，使其立即生效
            foreach (var axis in Model.Axis_X)
            {
                axis.TextSize = fontSize;
            }
        }

        /// <summary>
        /// 获取Series
        /// </summary>
        /// <returns>数据系列数组</returns>
        public ObservableCollection<ISeries>   GetSeries()
        {
            return Model.Series;
        }

        /// <summary>
        /// 获取AxisX
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<ICartesianAxis> GetAxisX()
        {
            return Model.Axis_X;
        }

        #region 暂时不使用

        /// <summary>
        /// 添加Series
        /// </summary>
        /// <param name="lineCharts"></param>
        /// <returns></returns>
        public void AddSeries(LineSeries lineCharts)
        {
            TryCatchHelper.TryExecute(() =>
            {
                // 创建一个新的CLineSeries对象并复制所有属性
                var newSeries = new LineSeries()
                {
                    SeriesName = lineCharts.SeriesName,
                    Value = lineCharts.Value
                };

                Model.DataCollection.Add(newSeries);
                Model.Series.Add(new LineSeries<ObservableValue>
                {
                    Values = (IReadOnlyCollection<ObservableValue>)Model.DataCollection[Model.DataCollection.Count - 1].Value,
                    Name = lineCharts.SeriesName,
                    Fill = null,
                    GeometrySize = Model.PointSize,
                });
            });
        }

        /// <summary>
        /// 添加数据 速度较慢
        /// </summary>
        /// <param name="axisx">x轴信息</param>
        /// <param name="lineCharts">Series信息</param>
        public void AddXY(List<string> axisx, LineSeries lineCharts)
        {
            TryCatchHelper.TryExecute(() =>
            {
                var ret = Model.Series.FirstOrDefault(s => s.Name == lineCharts.SeriesName);
                if (ret == null) AddSeries(lineCharts);


                var iLineSeries = Model.DataCollection.FirstOrDefault(s => s.SeriesName == lineCharts.SeriesName);

                Model.Axis_X[0].Labels = axisx;
                for (int i = 0; i < lineCharts.Value.Count; i++)
                {

                    if (iLineSeries != null) iLineSeries.Value.Add(lineCharts.Value[i]);
                }
                 
            });
        }

        /// <summary>
        /// 清除
        /// </summary>
        public void Clear()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.Series.Clear();
                Model.Axis_X[0].Labels = new List<string>();
            });
        }
        #endregion


        /// <summary>
        /// 设置AxisX属性
        /// </summary>
        /// <param name="axisX"></param>
        public void SetAxisX(List<string> axisX)
        {
            Model.Axis_X[0].Labels = axisX;
            foreach (var axis in Model.Axis_X)
            {
                axis.TextSize = Model.AxisFontSize;
            }
        }


        /// <summary>
        /// 显示
        /// </summary>
        /// <param name="lineCharts">Series数据</param>
        public ObservableCollection<ISeries> ShowData(LineSeries[] lineCharts, ChartsLineType chartsLineType = ChartsLineType.BaseLine)
        {

            switch (chartsLineType)
            {
                case ChartsLineType.BaseLine:
                    Model.Series.Clear();
                    Model.Series = SetBaseLineData(lineCharts);
                    return Model.Series;
                default: return null;
            }
        }

     
        /// <summary>
        /// 设置基础线样式
        /// </summary>
        /// <param name="lineCharts"></param>
        public ObservableCollection<ISeries> SetBaseLineData(LineSeries[] lineCharts)
        {
            ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
            foreach (var item in lineCharts)
            {
                LineSeries<double> lineseries = new LineSeries<double>();
                // 设置数据 - 直接使用ObservableCollection，这样当数据变化时图表会自动更新
                lineseries.Values = item.Value;
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
