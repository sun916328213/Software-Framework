// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMILogQueryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Services、DataManager.Core.Log、WPFControls.Queries.Logs、WPFControls.Controls.Diagnostics、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Presentation.Log。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Services;
using DataManager.Core.Log;
using WPFControls.Queries.Logs;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Presentation.Log;

namespace WPFControls.ProgramControl.Services.Log;

/// <summary>
/// SEMILogQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
internal class SEMILogQueryService
{
    private readonly SemiLogQueryPresentation _presentation;
    internal readonly ILogManager logManager;
    internal SEMILogQueryModel Model { get => _presentation.Model; set => _presentation.Model = value; }
    /// <summary>
    /// 初始化 SEMILogQueryService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public SEMILogQueryService(ILogManager manager, ILogHistoryQueryService<string> query, TryCatchHelper? errors = null)
    {
        logManager = manager;
        _presentation = new SemiLogQueryPresentation(query, errors ?? new());
    }
    /// <summary>
    /// 执行查询操作；对应入口为 Query_Log。
    /// 正文中的关键协作调用包括 Query_Log；具体条件和先后次序见方法体。
    /// </summary>
    internal Task Query_Log() => _presentation.Query_Log();
    /// <summary>
    /// 执行保存操作；对应入口为 Save_Log。
    /// 正文中的关键协作调用包括 Save_Log；具体条件和先后次序见方法体。
    /// </summary>
    internal Task Save_Log() => _presentation.Save_Log();
}