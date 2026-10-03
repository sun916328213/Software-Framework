// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：CapacityStatisticsInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Models;

namespace MachineApp.Infra.Persistence.Entities;

/// <summary>整机产能数据库实体。保留 CapacityStatisticsInfo 表名及原有字段。</summary>
public sealed class CapacityStatisticsInfo : CapacityStatisticsRow, ISQLDataBase { }
