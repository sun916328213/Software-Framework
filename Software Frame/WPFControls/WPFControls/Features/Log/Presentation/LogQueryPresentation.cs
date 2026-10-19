// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogQueryPresentation。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Queries.Logs、DataManager.Core.Log.Definition、System、System.Collections.Generic、System.Collections.ObjectModel、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls.Queries.Logs;
using DataManager.Core.Log.Definition;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControls.Charts.Models;
using WPFControls.Controls.Services.Charts;
using WPFControls.Controls.Diagnostics;
using WPFControls.Method;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Presentation.Log
{
    /// <summary>
    /// 日志查询服务类
    /// 管理日志的查询和保存操作
    /// </summary>
    internal class LogQueryPresentation 
    {
        private readonly TryCatchHelper _errors;

    
        /// <summary>
        /// 日志数据模型
        /// </summary>
        internal LogQueryModel Model { get;   set; }=new LogQueryModel();

        /// <summary>
        /// 饼图服务
        /// </summary>
        internal ChartsPieService Service_PieCharts { get; set; } = new ChartsPieService();
        
        private readonly ILogHistoryQueryService<LogInfo> _query;

        /// <summary>
        /// 分页数据源(查询或筛选后的完整结果)
        /// </summary>
        List<LogInfo> pageSource = new List<LogInfo>();

        
        /// <summary>
        /// 初始化 LogQueryPresentation 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public LogQueryPresentation(ILogHistoryQueryService<LogInfo> query, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            _query = query;
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query_Log。
        /// 正文中的关键协作调用包括 TryExecuteAsync、ToList、QueryAsync、Page_Log、Summarize；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        internal async Task Query_Log()
        {
            await _errors.TryExecuteAsync(async () =>
            {

                Model.LogInfos = (await _query.QueryAsync(Model.DateTime_Start, Model.DateTime_End)).ToList();
                pageSource = Model.LogInfos;
                Model.TotalCount = pageSource.Count;
                Model.CurrentPage = 1;
                Page_Log(1);

                //筛选出log中的报警 并在饼状图显示
                var summaries = _query.Summarize(Model.LogDatas.ToList(),
                    row => row.Level != ELogLevel.Information && row.Level != ELogLevel.SysError, row => row.Info);
                Service_PieCharts.ShowData(summaries.Select(row => new ChartPieData { SeriesName = row.Info, Value = row.Count }).ToArray());
                return Model.LogInfos;
            });
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Filter_Log。
        /// 正文中的关键协作调用包括 TryExecute、ToList、Filter、Page_Log；具体条件和先后次序见方法体。
        /// </summary>
        public void Filter_Log()
        {
            _errors.TryExecute(() =>
               {
                   var d = _query.Filter(Model.LogInfos, Model.FilterItem, Model.FilterText).ToList();

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
            _errors.TryExecute(() =>
               {
                   var d = (Model.SelectItem == null)
                       ? Model.LogInfos.ToList()
                       : _query.Select(Model.LogInfos, s => s.Level == Model.SelectItem).ToList();
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
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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

            var paged = _query.Page(pageSource, page, Model.PageSize);
            Model.LogDatas = new ObservableCollection<LogInfo>(paged);
        }

        /// <summary>
        /// 保存日志 CSV格式
        /// </summary>
        internal async Task Save_Log()
        {
            await _errors.TryExecuteAsync(async () =>
            {
                QuerySave querySave = new QuerySave();
                await querySave.QuerySaveCSVAsync(Model.LogDatas);
            }  );
        }
    }
}