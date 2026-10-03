// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DataQueryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Queries.Data、WPFControls.Controls.Diagnostics、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Presentation.Data。导入不代表所有类型都会在运行时加载。
using WPFControls.Queries.Data;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Presentation.Data;

namespace WPFControls.ProgramControl.Services;

/// <summary>连接数据查询契约与页面展示状态。</summary>
public class DataQueryService<T, TDTO> where T : ISQLDataBase
{
    private readonly DataQueryPresentation<T, TDTO> _presentation;
    internal readonly DataQueryModel<T, TDTO> Model;
    /// <summary>
    /// 初始化 DataQueryService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public DataQueryService(DataQueryModel<T, TDTO> model, ITableQueryService<T> query, TryCatchHelper? errors = null)
    {
        // model 承载页面绑定状态，query 承载查询能力；包装器不自行选择 SQL 类型或重新解析容器。
        // 展示适配负责把查询结果交给现有页面，数据筛选规则和空结果处理保持在原有实现中。
        Model = model;
        _presentation = new DataQueryPresentation<T, TDTO>(Model, query, errors ?? new());
    }
    /// <summary>
    /// 执行查询操作；对应入口为 Query_DB。
    /// 正文中的关键协作调用包括 Query_DB；具体条件和先后次序见方法体。
    /// </summary>
    public void Query_DB() => _presentation.Query_DB();
    /// <summary>
    /// 执行保存操作；对应入口为 Save_DB。
    /// 正文中的关键协作调用包括 Save_DB；具体条件和先后次序见方法体。
    /// </summary>
    public Task Save_DB() => _presentation.Save_DB();
}
