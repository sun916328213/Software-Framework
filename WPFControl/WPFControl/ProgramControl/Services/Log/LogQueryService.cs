using DataManager.Log;
using Extension.Core.ControlEx;
using Extension.Core.FileEx;
using Extension.Struct;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.CustomControl.Services;
using WPFControl.CustomControl.Services.Charts;
using WPFControl.Helper;
using WPFControl.Method;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 日志查询服务类
    /// 管理日志的查询和保存操作
    /// </summary>
    internal class LogQueryService 
    {
    
        /// <summary>
        /// 日志数据模型
        /// </summary>
        internal LogQueryModel Model { get;   set; }=new LogQueryModel();

        /// <summary>
        /// 饼图服务
        /// </summary>
        internal ChartsPieService Service_PieCharts { get; set; } = new ChartsPieService();
        
        LogManager logManager;
        /// <summary>
        /// 日志助手
        /// </summary>
        internal   ILog Service_Log => logManager[0];

        /// <summary>
        /// 分页数据源(查询或筛选后的完整结果)
        /// </summary>
        List<LogInfo> pageSource = new List<LogInfo>();

        
        public LogQueryService(IContainerProvider container)
        {
            logManager = container.Resolve<LogManager>();
        }

        internal async Task Query_Log()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
            {
                DateTime pStart = Model.DateTime_Start;
                DateTime pEnd = Model.DateTime_End.AddDays(1);
                Model.LogInfos = await  Service_Log.QueryAsync(  pStart, pEnd);
                pageSource = Model.LogInfos;
                Model.TotalCount = pageSource.Count;
                Model.CurrentPage = 1;
                Page_Log(1);

                //筛选出log中的报警 并在饼状图显示
                var d1 = Model.LogDatas.Where(s => s.Level != ELogLevel.Information && s.Level != ELogLevel.SysError);
                var d2 = d1.GroupBy(s => s.Info);
                List<PieData> series = new List<PieData>();
                foreach (var item in d2)
                {
                    series.Add(new PieData { SeriesName = item.Key, Value = item.Count() });
                }
                Service_PieCharts.ShowData(series.ToArray());

                return Model.LogInfos;
            });
        }

        public void Filter_Log()
        {
            TryCatchHelper.TryExecute(() =>
               {
                   var d = Model.LogInfos.ToList();

                   // 按DataGrid字段筛查(字段来自表头)
                   if (!string.IsNullOrEmpty(Model.FilterItem))
                   {
                       var prop = typeof(LogInfo).GetProperty(Model.FilterItem);
                       if (prop != null)
                       {
                           string val = Model.FilterText;
                           if (!string.IsNullOrEmpty(val))
                           {
                               d = d.Where(s =>
                               {
                                   var v = prop.GetValue(s)?.ToString();
                                   return v != null && v.IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0;
                               }).ToList();
                           }
                       }
                   }

                   pageSource = d;
                   Model.TotalCount = pageSource.Count;
                   Model.CurrentPage = 1;
                   Page_Log(1);
                   return d;
               });
        }

        /// <summary>
        /// 按等级筛查
        /// </summary>
        public void SelectFilter_Log()
        {
            TryCatchHelper.TryExecute(() =>
               {
                   var d = (Model.SelectItem == null)
                       ? Model.LogInfos.ToList()
                       : Model.LogInfos.Where(s => s.Level == Model.SelectItem).ToList();
                   pageSource = d;
                   Model.TotalCount = pageSource.Count;
                   Model.CurrentPage = 1;
                   Page_Log(1);
                   return d;
               });
        }

        /// <summary>
        /// 重置等级筛查(显示全部)
        /// </summary>
        public void SelectReset_Log()
        {
            TryCatchHelper.TryExecute(() =>
               {
                   Model.SelectItem = null;
                   pageSource = Model.LogInfos;
                   Model.TotalCount = pageSource.Count;
                   Model.CurrentPage = 1;
                   Page_Log(1);
               });
        }

        /// <summary>
        /// 重置字段筛查(清空字段与值并显示全部)
        /// </summary>
        public void FilterReset_Log()
        {
            TryCatchHelper.TryExecute(() =>
               {
                   Model.FilterItem = null;
                   Model.FilterText = null;
                   pageSource = Model.LogInfos;
                   Model.TotalCount = pageSource.Count;
                   Model.CurrentPage = 1;
                   Page_Log(1);
               });
        }

        /// <summary>
        /// 按页取数据
        /// </summary>
        /// <param name="page">页码(从1开始)</param>
        internal void Page_Log(int page)
        {
            int size = Model.PageSize < 1 ? 1 : Model.PageSize;
            var paged = pageSource.Skip((page - 1) * size).Take(size).ToList();
            Model.LogDatas = new ObservableCollection<LogInfo>(paged);
        }

        /// <summary>
        /// 保存日志 CSV格式
        /// </summary>
        internal async Task Save_Log()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
            {
                QuerySave querySave = new QuerySave();
                await querySave.QuerySaveCSVAsync(Model.LogDatas);
            }  );
        }
    }
}