// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：PackingCapacityStatisticsReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、WPFControls.Queries.Data、WPFControls.ProgramControl.Models、WPFControls.Integration.Queries、MachineApp.Infra.Persistence.Entities。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using WPFControls.Queries.Data;
using WPFControls.ProgramControl.Models;
using WPFControls.Integration.Queries;
using MachineApp.Infra.Persistence.Entities;

namespace MachineApp.Infra.Persistence.Adapters;

/// <summary>
/// PackingCapacityStatisticsReader 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：ICapacityStatisticsReader&lt;CapacityStatisticsRow&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class PackingCapacityStatisticsReader(Func<ISQL<CapacityStatisticsInfo>> getSql) : ICapacityStatisticsReader<CapacityStatisticsRow>
{
    // 查询按日期包含起止日，保留原条件；不在适配器里额外修改时区、排序或范围解释。
    // 每条数据库记录投影出 24 个小时的 OK/NG 只读序列，供查询用例汇总，实体仍是宿主实际类型。
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// 正文中的关键协作调用包括 ToArray、Select、Query、getSql、AsReadOnly；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<CapacityStatisticsEntry<CapacityStatisticsRow>> Read(DateTime start, DateTime end) =>
        (getSql().Query(row => row.Time.Date >= start && row.Time.Date <= end) ?? [])
        .Select(row => new CapacityStatisticsEntry<CapacityStatisticsRow>(row, row.Time,
            Array.AsReadOnly(Enumerable.Range(0, 24).Select(hour => CapacityStatisticsFields.OKAt(row, hour)).ToArray()),
            Array.AsReadOnly(Enumerable.Range(0, 24).Select(hour => CapacityStatisticsFields.NGAt(row, hour)).ToArray())))
        .ToArray();
}
