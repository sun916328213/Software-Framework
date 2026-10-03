// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：PackingCapacityStatisticsStore。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、WPFControls.Queries.Data、WPFControls.ProgramControl.Models、WPFControls.Integration.Queries、MachineApp.Infra.Persistence.Entities。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using WPFControls.Queries.Data;
using WPFControls.ProgramControl.Models;
using WPFControls.Integration.Queries;
using MachineApp.Infra.Persistence.Entities;

namespace MachineApp.Infra.Persistence.Adapters;

/// <summary>
/// PackingCapacityStatisticsStore 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：ICapacityStatisticsStore&lt;CapacityStatisticsRow&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class PackingCapacityStatisticsStore(Func<ISQL<CapacityStatisticsInfo>> getSql)
    : ICapacityStatisticsStore<CapacityStatisticsRow>
{
    // 通过页面行契约对接写入用例，但本适配器创建和查询得到的行均为 CapacityStatisticsInfo。
    // UpdateDay/Add 的强制转换依赖这一约定；不能把任意页面临时行直接作为数据库实体传入。
    // 小时字段的读写统一委托给同一个映射器，避免读取、统计与写回使用不同的列顺序。
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FindDay。
    /// 正文中的关键协作调用包括 Query、getSql；具体条件和先后次序见方法体。
    /// </summary>
    public CapacityStatisticsRow? FindDay(DateTime day) => getSql().Query(row => row.Time.Date == day)?.FirstOrDefault();
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDay。
    /// </summary>
    public CapacityStatisticsRow CreateDay(string id, DateTime day) => new CapacityStatisticsInfo { ID = id, Time = day };
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Time。
    /// </summary>
    public DateTime Time(CapacityStatisticsRow row) => row.Time;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Good。
    /// 正文中的关键协作调用包括 OKAt；具体条件和先后次序见方法体。
    /// </summary>
    public int Good(CapacityStatisticsRow row, int hour) => CapacityStatisticsFields.OKAt(row, hour);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Defect。
    /// 正文中的关键协作调用包括 NGAt；具体条件和先后次序见方法体。
    /// </summary>
    public int Defect(CapacityStatisticsRow row, int hour) => CapacityStatisticsFields.NGAt(row, hour);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetGood。
    /// 正文中的关键协作调用包括 SetOK；具体条件和先后次序见方法体。
    /// </summary>
    public void SetGood(CapacityStatisticsRow row, int hour, int value) => CapacityStatisticsFields.SetOK(row, hour, value);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetDefect。
    /// 正文中的关键协作调用包括 SetNG；具体条件和先后次序见方法体。
    /// </summary>
    public void SetDefect(CapacityStatisticsRow row, int hour, int value) => CapacityStatisticsFields.SetNG(row, hour, value);
    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 UpdateDay。
    /// 正文中的关键协作调用包括 Update、getSql；具体条件和先后次序见方法体。
    /// </summary>
    public void UpdateDay(DateTime day, CapacityStatisticsRow row) => getSql().Update(value => value.Time.Date == day, (CapacityStatisticsInfo)row);
    /// <summary>
    /// 执行添加操作；对应入口为 Add。
    /// 正文中的关键协作调用包括 Add、getSql；具体条件和先后次序见方法体。
    /// </summary>
    public void Add(CapacityStatisticsRow row) => getSql().Add((CapacityStatisticsInfo)row);
}
