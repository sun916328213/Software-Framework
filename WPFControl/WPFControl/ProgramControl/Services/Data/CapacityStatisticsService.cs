using DataManager.Excel;
using DataManager.Excel.MiniExcel;
using DataManager.SQL;
using Extension.Core.ControlEx;
using Extension.Core.ConvertEx;
using Extension.Core.FileEx;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Prism.Ioc;
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
    /// 产能统计服务类
    /// </summary>
    public class CapacityStatisticsService
    {
        // 改为 Lazy 注入：避免注册时强求 ISQL<CapacityStatisticsInfo>（该依赖在 Startup 脚本里才注入），
        // 实际取值时再由容器解析，保证解析顺序无关。
            ISQL<CapacityStatisticsInfo> StatsSQL=> containerProvider.Resolve<ISQL<CapacityStatisticsInfo>>();


        public CapacityStatisticsModel Model = new CapacityStatisticsModel();

       
        IContainerProvider containerProvider;
        public CapacityStatisticsService(IContainerProvider container)
        {
            containerProvider = container;
            Model.StartTime = DateTime.Now.Date;
            Model.EndTime = DateTime.Now.Date;

        }

        /// <summary>
        /// 记录一条产能统计结果，自动累加到对应天/小时的 OK/NG 计数
        /// </summary>
        /// <param name="time">发生时间</param>
        /// <param name="isOK">true=良品 false=不良品</param>
        public bool SaveStatistics(DateTime time, bool isOK)
        {
            return TryCatchHelper.TryExecuteWithResult(() =>
            {
                var day = time.Date;
                var hour = time.Hour; // 0..23
                var id = day.ToString("yyyyMMdd");

                var existing = StatsSQL.Query(s => s.Time.Date == day)?.FirstOrDefault();
                if (existing != null)
                {
                    if (isOK) SetOK(existing, hour, OKAt(existing, hour) + 1);
                    else SetNG(existing, hour, NGAt(existing, hour) + 1);
                    StatsSQL.Update(s => s.Time.Date == day, existing);
                }
                else
                {
                    var row = new CapacityStatisticsInfo { ID = id, Time = day };
                    if (isOK) SetOK(row, hour, 1); else SetNG(row, hour, 1);
                    StatsSQL.Add(row);
                }
            });
        }

        /// <summary>
        /// 按小时单点更新产能统计(适用于PLC 10s循环读取累计值,每小时只更新该小时列)
        /// 每条数据按其 Time 字段定位"当天 + 对应小时列",只更新该列的 OK/NG 值,不影响其它23列
        /// </summary>
        /// <param name="datas">产能统计数据列表(每条 Time 决定归属小时,OK/NG 取该小时列的值)</param>
        /// <param name="seconds">归属时间偏移量(秒),避免整点时刻数据归属错误</param>    
        public bool SaveStatistics(CapacityStatisticsInfo data, int seconds = -10)
        {
            return TryCatchHelper.TryExecuteWithResult(() =>
            {
                if (data == null) return;
                var curtime = data.Time.AddSeconds(seconds); // 取前seconds秒的时间作为归属时间,避免整点时刻数据归属错误

                var day = curtime.Date;
                var hour = curtime.Hour; // 0..23
                var id = day.ToString("yyyyMMdd");

                var existing = StatsSQL.Query(s => s.Time.Date == day)?.FirstOrDefault();
                if (existing != null)
                {
                    // 仅更新该小时列,其它列保持不变
                    SetOK(existing, hour, OKAt(data, hour));
                    SetNG(existing, hour, NGAt(data, hour));
                    StatsSQL.Update(s => s.Time.Date == day, existing);
                }
                else
                {
                    // 当天首条:新建行,只填该小时列,其它列默认0
                    var row = new CapacityStatisticsInfo { ID = id, Time = day };
                    SetOK(row, hour, OKAt(data, hour));
                    SetNG(row, hour, NGAt(data, hour));
                    StatsSQL.Add(row);
                }
            });
        }

        /// <summary>
        /// 保存产能统计数据到 Excel 文件
        /// </summary>
        public async Task<bool> SaveStatistics(List<CapacityStatisticsInfo> data)
        {
            if (data == null || data.Count == 0) return false;
 
            var dd1=FileDialogEx.SaveFile($"产能统计_{DateTime.Now:yyyyMMdd}.xlsx");
            if(string.IsNullOrEmpty(dd1)) return false;
            await ExcelEx.SaveToExcelAsync(dd1, data).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// 获取指定时间区间的产能统计数据
        /// </summary>
        public void GetStatistics()
        {
            var startDay = Model.StartTime;
            var endDay = Model.EndTime.AddDays(1);

            var data = StatsSQL.Query(s => s.Time.Date >= startDay && s.Time.Date <= endDay)?.ToList()
                       ?? new List<CapacityStatisticsInfo>();

            // 原始数据集合
            Model.CapacityStatisticsDatas.Clear();
            foreach (var item in data)
                Model.CapacityStatisticsDatas.Add(item);

            int good = data.Sum(SumOK);
            int defect = data.Sum(SumNG);
            int total = good + defect;

            Model.TotalOutput = total;
            Model.GoodCount = good;
            Model.DefectCount = defect;
            Model.YieldRate = total > 0 ? Math.Round(good * 100.0 / total, 2) : 0;

            // 运行时长 = 有产出的工时数
            int activeHours = 0;
            foreach (var d in data)
                for (int h = 0; h < 24; h++)
                    if (OKAt(d, h) + NGAt(d, h) > 0) activeHours++;
            Model.RunTime = activeHours;

            double totalHours = (endDay - startDay).TotalHours + 24;
            Model.UtilizationRate = totalHours > 0 ? Math.Round(Model.RunTime * 100.0 / totalHours, 2) : 0;

            BuildTrend(Model, data, startDay, endDay);

        }

        /// <summary>
        /// 构建产量趋势折线图(区间<=2天按小时,否则按天)
        /// </summary>
        private void BuildTrend(CapacityStatisticsModel model, List<CapacityStatisticsInfo> data, DateTime startDay, DateTime endDay)
        {
            var axisX = new ObservableCollection<string>();
            var outputSeries = new LineSeries { SeriesName = "Total", Color = ColorHelper.Color_Blue };
            var goodSeries = new LineSeries { SeriesName = "OK", Color = ColorHelper.Color_Green };
            var failSeries = new LineSeries { SeriesName = "NG", Color = ColorHelper.Color_Red };
           
            bool hourly = (endDay - startDay).TotalDays <= 2;

            if (hourly)
            {
                // 按小时汇总(0-23点),多天数据合并到同一小时
                for (int h = 0; h < 24; h++)
                {
                    axisX.Add($"{h:00}:00");
                    outputSeries.Value.Add(data.Sum(d => OKAt(d, h) + NGAt(d, h)));
                    goodSeries.Value.Add(data.Sum(d => OKAt(d, h)));
                    failSeries.Value.Add(data.Sum(d => NGAt(d, h)));
                }
            }
            else
            {
                // 按天汇总
                var byDay = data.GroupBy(d => d.Time.Date).ToDictionary(g => g.Key, g => g.ToList());
                var day = startDay;
                while (day <= endDay)
                {
                    axisX.Add(day.ToString("MM-dd"));
                    if (byDay.TryGetValue(day, out var list))
                    {
                        outputSeries.Value.Add(list.Sum(d => SumOK(d) + SumNG(d)));
                        goodSeries.Value.Add(list.Sum(SumOK));
                        failSeries.Value.Add(list.Sum( SumNG ));
                    }
                    else
                    {   
                        outputSeries.Value.Add(0);
                        goodSeries.Value.Add(0);
                        failSeries.Value.Add(0);
                    }
                    day = day.AddDays(1);
                }
            }

            model.AxisX = axisX;
            model.OutputTrend = new ObservableCollection<LineSeries> {   goodSeries, failSeries, outputSeries };
        }

        #region OK/NG 计数读写(直接访问字段)

        private static int OKAt(CapacityStatisticsData d, int hour) => hour switch
        {
            0 => d.OKCOunt1, 1 => d.OKCOunt2, 2 => d.OKCOunt3, 3 => d.OKCOunt4,
            4 => d.OKCOunt5, 5 => d.OKCOunt6, 6 => d.OKCOunt7, 7 => d.OKCOunt8,
            8 => d.OKCOunt9, 9 => d.OKCOunt10, 10 => d.OKCOunt11, 11 => d.OKCOunt12,
            12 => d.OKCOunt13, 13 => d.OKCOunt14, 14 => d.OKCOunt15, 15 => d.OKCOunt16,
            16 => d.OKCOunt17, 17 => d.OKCOunt18, 18 => d.OKCOunt19, 19 => d.OKCOunt20,
            20 => d.OKCOunt21, 21 => d.OKCOunt22, 22 => d.OKCOunt23, 23 => d.OKCOunt24,
            _ => 0
        };

        private static int NGAt(CapacityStatisticsData d, int hour) => hour switch
        {
            0 => d.NGCount1, 1 => d.NGCount2, 2 => d.NGCount3, 3 => d.NGCount4,
            4 => d.NGCount5, 5 => d.NGCount6, 6 => d.NGCount7, 7 => d.NGCount8,
            8 => d.NGCount9, 9 => d.NGCount10, 10 => d.NGCount11, 11 => d.NGCount12,
            12 => d.NGCount13, 13 => d.NGCount14, 14 => d.NGCount15, 15 => d.NGCount16,
            16 => d.NGCount17, 17 => d.NGCount18, 18 => d.NGCount19, 19 => d.NGCount20,
            20 => d.NGCount21, 21 => d.NGCount22, 22 => d.NGCount23, 23 => d.NGCount24,
            _ => 0
        };

        private static void SetOK(CapacityStatisticsData d, int hour, int value)
        {
            switch (hour)
            {
                case 0: d.OKCOunt1 = value; break;
                case 1: d.OKCOunt2 = value; break;
                case 2: d.OKCOunt3 = value; break;
                case 3: d.OKCOunt4 = value; break;
                case 4: d.OKCOunt5 = value; break;
                case 5: d.OKCOunt6 = value; break;
                case 6: d.OKCOunt7 = value; break;
                case 7: d.OKCOunt8 = value; break;
                case 8: d.OKCOunt9 = value; break;
                case 9: d.OKCOunt10 = value; break;
                case 10: d.OKCOunt11 = value; break;
                case 11: d.OKCOunt12 = value; break;
                case 12: d.OKCOunt13 = value; break;
                case 13: d.OKCOunt14 = value; break;
                case 14: d.OKCOunt15 = value; break;
                case 15: d.OKCOunt16 = value; break;
                case 16: d.OKCOunt17 = value; break;
                case 17: d.OKCOunt18 = value; break;
                case 18: d.OKCOunt19 = value; break;
                case 19: d.OKCOunt20 = value; break;
                case 20: d.OKCOunt21 = value; break;
                case 21: d.OKCOunt22 = value; break;
                case 22: d.OKCOunt23 = value; break;
                case 23: d.OKCOunt24 = value; break;
            }
        }

        private static void SetNG(CapacityStatisticsData d, int hour, int value)
        {
            switch (hour)
            {
                case 0: d.NGCount1 = value; break;
                case 1: d.NGCount2 = value; break;
                case 2: d.NGCount3 = value; break;
                case 3: d.NGCount4 = value; break;
                case 4: d.NGCount5 = value; break;
                case 5: d.NGCount6 = value; break;
                case 6: d.NGCount7 = value; break;
                case 7: d.NGCount8 = value; break;
                case 8: d.NGCount9 = value; break;
                case 9: d.NGCount10 = value; break;
                case 10: d.NGCount11 = value; break;
                case 11: d.NGCount12 = value; break;
                case 12: d.NGCount13 = value; break;
                case 13: d.NGCount14 = value; break;
                case 14: d.NGCount15 = value; break;
                case 15: d.NGCount16 = value; break;
                case 16: d.NGCount17 = value; break;
                case 17: d.NGCount18 = value; break;
                case 18: d.NGCount19 = value; break;
                case 19: d.NGCount20 = value; break;
                case 20: d.NGCount21 = value; break;
                case 21: d.NGCount22 = value; break;
                case 22: d.NGCount23 = value; break;
                case 23: d.NGCount24 = value; break;
            }
        }

        private static int SumOK(CapacityStatisticsData d)
            => Enumerable.Range(0, 24).Sum(h => OKAt(d, h));

        private static int SumNG(CapacityStatisticsData d)
            => Enumerable.Range(0, 24).Sum(h => NGAt(d, h));

        #endregion
    }
}
