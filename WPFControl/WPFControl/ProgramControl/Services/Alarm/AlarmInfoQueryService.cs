using DataManager.Excel;
using Extension.Core.Conversion;
using LiveChartsCore;
using Mapster;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.CustomControl.Services.Charts;
using WPFControl.ProgramControl.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControl.ProgramControl.Services
{
    internal class AlarmInfoQueryService
    {
        internal AlarmInfoQueryModel Model { get; set; } = new AlarmInfoQueryModel();

        internal AlarmInfoManagerService _AlarmInfoManagerService;
        internal IExcel _ExcelManager => _AlarmInfoManagerService._ExcelManager;

        internal ChartsPieService _ChartsPieService;

        /// <summary>
        /// 饼状图数据
        /// </summary>
        public ObservableCollection<PieData> PieData { get; set; }


        public AlarmInfoQueryService(IContainerProvider container)
        {
            _AlarmInfoManagerService = container.Resolve<AlarmInfoManagerService>();
            _ChartsPieService = container.Resolve<ChartsPieService>();
        }

        public async Task QueryAsync()
        {
            List<AlarmInfoDTO> alarms = new List<AlarmInfoDTO>();

            // 遍历日期范围，查询每天的报警数据
            for (DateTime index = Model.DateTime_Start; index <= Model.DateTime_End; index = index.AddDays(1))
            {
                _ExcelManager.ExcelParam.FileName = index.ToDateNOSapce() + ".xlsx";
                var data = await _ExcelManager.QueryAsync<AlarmInfoDTO>();

                alarms.AddRange(data);
            }
            var d1 = alarms.Where(x => string.IsNullOrEmpty(x.Info) == false);

            Model.Datas = new ObservableCollection<AlarmInfoDTO>(d1);

            var d2 = d1.Adapt<IEnumerable<AlarmInfo>>();
            List<AlarmInfoPie> alarmInfos = d2.GroupBy(x => x.Info).
                Select(x => new AlarmInfoPie()
                {
                    Info = x.Key,
                    Count = x.Count(),
                    Duration = x.Sum(y => y.Duration).ToString() + " s",
                    Percentage_of_Times = (x.Count() / alarms.Count()).ToString("P"),
                }).ToList();

            PieData = ShowPieDate(alarmInfos);
        }

        /// <summary>
        /// 显示饼状图
        /// </summary>
        /// <param name="alarmInfos"></param>
        /// <returns></returns>
        private ObservableCollection<PieData> ShowPieDate(List<AlarmInfoPie> alarmInfos)
        {
            List<PieData> pieDatas = new List<PieData>();
            foreach (var item in alarmInfos)
            {
                var pieData = new PieData()
                {
                    Value = item.Count,
                    SeriesName = item.Info,
                };
                pieDatas.Add(pieData);
            }
            return new ObservableCollection<PieData>(pieDatas);
        }

        public void Save()
        {
            var data = Model.Datas.ToList();
            _ExcelManager.Write(data);
        }
    }
}

