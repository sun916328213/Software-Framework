// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SemiLogQueryPresentation。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、WPFControls.Queries.Logs、WPFControls.Controls.Diagnostics、WPFControls.Method、WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using WPFControls.Queries.Logs;
using WPFControls.Controls.Diagnostics;
using WPFControls.Method;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Presentation.Log;

/// <summary>
/// SemiLogQueryPresentation 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
internal sealed class SemiLogQueryPresentation(ILogHistoryQueryService<string> query, TryCatchHelper errors)
{
    internal SEMILogQueryModel Model { get; set; } = new();
    /// <summary>
    /// 执行查询操作；对应入口为 Query_Log。
    /// 正文中的关键协作调用包括 TryExecuteAsync、QueryAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    internal Task Query_Log() => errors.TryExecuteAsync(async () =>
    {
        Model.SEMILogDatas = new ObservableCollection<string>(await query.QueryAsync(Model.DateTime_Start, Model.DateTime_End));
    });
    /// <summary>
    /// 执行保存操作；对应入口为 Save_Log。
    /// 正文中的关键协作调用包括 TryExecuteAsync、QuerySaveCSVAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    internal Task Save_Log() => errors.TryExecuteAsync(async () =>
        await new QuerySave().QuerySaveCSVAsync(Model.SEMILogDatas));
}
