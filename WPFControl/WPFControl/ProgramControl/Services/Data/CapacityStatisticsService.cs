using DataManager.SQL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFControl.Resources;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 产能统计服务（抽象基类）：按时间区间查询数据库原始记录，通过 <see cref="IsNG"/>
    /// 判定每条记录良品/不良品，再聚合为产能统计结果。
    /// 框架层泛型实现，不依赖具体业务表类型；良品判定规则由使用方在子类中实现。
    /// </summary>
    /// <typeparam name="T">数据库记录类型</typeparam>
    public abstract class CapacityStatisticsService<T> where T : ISQLDataBase
    {
        internal readonly SQLManager SQLManager;

        ISQL<T> Service_SQL => (ISQL<T>)SQLManager[0];

        protected CapacityStatisticsService(SQLManager sqlManager)
        {
            SQLManager = sqlManager;
        }

        /// <summary>
        /// 判定单条记录是否为不良品（由使用方在子类中实现）
        /// </summary>
        /// <param name="data">数据库记录</param>
        /// <returns>true 表示不良品，false 表示良品</returns>
        protected abstract bool IsNG(T data);

        /// <summary>
        /// 获取指定时间区间的产能统计数据
        /// </summary>
        public CapacityStatisticsModel GetStatistics(DateTime start, DateTime end)
        {
            var model = new CapacityStatisticsModel();

            TryCatchHelper.TryExecute(() =>
            {
                var startDay = start.Date;
                var endDay = end.AddDays(1).Date;

                var data = Service_SQL.Query(s => s.Time.Date >= startDay && s.Time.Date <= endDay)?.ToList()
                           ?? new List<T>();

                model.TotalOutput = data.Count;
                model.TodayOutput = data.Count;
                model.GoodCount = data.Count(d => !IsNG(d));
                model.DefectCount = data.Count - model.GoodCount;
                model.YieldRate = model.TotalOutput > 0 ? Math.Round(model.GoodCount * 100.0 / model.TotalOutput, 2) : 0;

                if (data.Count > 0)
                {
                    var min = data.Min(d => d.Time);
                    var max = data.Max(d => d.Time);
                    model.RunTime = Math.Round((max - min).TotalHours, 2);
                    var totalHours = (endDay - startDay).TotalHours;
                    model.UtilizationRate = totalHours > 0 ? Math.Round(model.RunTime * 100.0 / totalHours, 2) : 0;
                }
                else
                {
                    model.RunTime = 0;
                    model.UtilizationRate = 0;
                }

                BuildTrend(model, data, startDay, endDay);

                model.PieData = new ObservableCollection<PieData>
                {
                    new PieData { SeriesName = "良品", Value = model.GoodCount, Color = ColorHelper.Color_Green },
                    new PieData { SeriesName = "不良品", Value = model.DefectCount, Color = ColorHelper.Color_Red }
                };

                model.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            });

            return model;
        }

        /// <summary>
        /// 生成产量趋势：区间<=2天按小时分组，否则按天分组
        /// </summary>
        private void BuildTrend(CapacityStatisticsModel model, List<T> data, DateTime startDay, DateTime endDay)
        {
            var axisX = new ObservableCollection<string>();
            var outputSeries = new LineSeries { SeriesName = "产量", Color = ColorHelper.Color_Green };
            var goodSeries = new LineSeries { SeriesName = "良品", Color = ColorHelper.Color_Blue };

            bool hourly = (endDay - startDay).TotalDays <= 2;

            if (hourly)
            {
                var byHour = data.GroupBy(d => d.Time.Hour).ToDictionary(g => g.Key, g => g.ToList());
                for (int h = 0; h < 24; h++)
                {
                    axisX.Add($"{h:00}:00");
                    if (byHour.TryGetValue(h, out var list))
                    {
                        outputSeries.Value.Add(list.Count);
                        goodSeries.Value.Add(list.Count(d => !IsNG(d)));
                    }
                    else
                    {
                        outputSeries.Value.Add(0);
                        goodSeries.Value.Add(0);
                    }
                }
            }
            else
            {
                var byDay = data.GroupBy(d => d.Time.Date).ToDictionary(g => g.Key, g => g.ToList());
                var day = startDay;
                while (day < endDay)
                {
                    axisX.Add(day.ToString("MM-dd"));
                    if (byDay.TryGetValue(day, out var list))
                    {
                        outputSeries.Value.Add(list.Count);
                        goodSeries.Value.Add(list.Count(d => !IsNG(d)));
                    }
                    else
                    {
                        outputSeries.Value.Add(0);
                        goodSeries.Value.Add(0);
                    }
                    day = day.AddDays(1);
                }
            }

            model.AxisX = axisX;
            model.OutputTrend = new ObservableCollection<LineSeries> { outputSeries, goodSeries };
        }
    }
}