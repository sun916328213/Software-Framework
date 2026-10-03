// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogQueryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Services、DataManager.Core.Log、DataManager.Core.Log.Definition、WPFControls.Queries.Logs、WPFControls.Controls.Diagnostics、WPFControls.Controls.Services.Charts。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Services;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using WPFControls.Queries.Logs;
using WPFControls.Controls.Diagnostics;
using WPFControls.Controls.Services.Charts;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Presentation.Log;

namespace WPFControls.ProgramControl.Services.Log;

/// <summary>连接日志查询契约与页面展示状态。</summary>
internal class LogQueryService
{
    private readonly LogQueryPresentation _presentation;
    private readonly ILogManager _manager;
    internal LogQueryModel Model { get => _presentation.Model; set => _presentation.Model = value; }
    internal ChartsPieService Service_PieCharts { get => _presentation.Service_PieCharts; set => _presentation.Service_PieCharts = value; }
    internal ILog Service_Log => (_manager.TryGet("RunLog", out var log) ? log : null)!;
    /// <summary>
    /// 初始化 LogQueryService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public LogQueryService(ILogManager manager, ILogHistoryQueryService<LogInfo> query, TryCatchHelper? errors = null)
    {
        _manager = manager;
        _presentation = new LogQueryPresentation(query, errors);
    }
    /// <summary>
    /// 执行查询操作；对应入口为 Query_Log。
    /// 正文中的关键协作调用包括 Query_Log；具体条件和先后次序见方法体。
    /// </summary>
    internal Task Query_Log() => _presentation.Query_Log();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Filter_Log。
    /// 正文中的关键协作调用包括 Filter_Log；具体条件和先后次序见方法体。
    /// </summary>
    public void Filter_Log() => _presentation.Filter_Log();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SelectFilter_Log。
    /// 正文中的关键协作调用包括 SelectFilter_Log；具体条件和先后次序见方法体。
    /// </summary>
    public void SelectFilter_Log() => _presentation.SelectFilter_Log();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SelectReset_Log。
    /// 正文中的关键协作调用包括 SelectReset_Log；具体条件和先后次序见方法体。
    /// </summary>
    public void SelectReset_Log() => _presentation.SelectReset_Log();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FilterReset_Log。
    /// 正文中的关键协作调用包括 FilterReset_Log；具体条件和先后次序见方法体。
    /// </summary>
    public void FilterReset_Log() => _presentation.FilterReset_Log();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Page_Log。
    /// 正文中的关键协作调用包括 Page_Log；具体条件和先后次序见方法体。
    /// </summary>
    internal void Page_Log(int page) => _presentation.Page_Log(page);
    /// <summary>
    /// 执行保存操作；对应入口为 Save_Log。
    /// 正文中的关键协作调用包括 Save_Log；具体条件和先后次序见方法体。
    /// </summary>
    internal Task Save_Log() => _presentation.Save_Log();
}