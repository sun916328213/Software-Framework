// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：SqlConfigEntity、ExcelConfigEntity。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls.Models、DataManager.Runtime.SQL.Dapper、SqlSugar、System.ComponentModel.DataAnnotations、System.ComponentModel.DataAnnotations.Schema。导入不代表所有类型都会在运行时加载。
using DataManager.Controls.Models;
using DataManager.Runtime.SQL.Dapper;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MachineApp.Infra.Persistence.Entities
{
    /// <summary>
    /// SQL 配置页（DataManager.Controls 的 <c>SqlView</c>）的行实体。
    ///
    /// 为什么由宿主定义：<c>SqlViewModel&lt;T&gt;</c> / <c>SqlWorkspace&lt;T&gt;</c> 是泛型，
    /// T 由宿主提供（DataManager.Controls 里只约束 <see cref="ISqlEntity"/>）；
    /// ORM 映射随实体走，所以表名与分表特性写在这里。
    /// 这也决定了注册必须落在本项目的 App.RegisterTypes —— WPFControls 是被本项目引用的库，
    /// 模块里看不到本命名空间的类型。
    ///
    /// ⚠️ 表名与 DataManager.Workbench 的 <c>workbench_entity_*</c> 刻意区分开：
    /// 两个宿主若连同一个库，测试数据不应落到同一张表。
    /// </summary>
    [SugarTable("packing_sql_entity_{year}{month}{day}")]
    [SplitTable(SqlSugar.SplitType.Day)]
    [DapperSplitTable(DataManager.Runtime.SQL.Dapper.SplitType.Day)]
    public class SqlConfigEntity : ISqlEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Key]
        public long Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "timestamp without time zone", IsNullable = true)]
        public DateTime? CreateTime { get; set; }
    }

    /// <summary>
    /// Excel 配置页（DataManager.Controls 的 <c>ExcelView</c>）的行实体。
    /// 表名与 DataManager.Workbench 的 <c>ExcelPerson</c> 区分开。
    /// </summary>
    [Table("PackingExcelRow")]
    public class ExcelConfigEntity : IExcelTestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime CreateTime { get; set; } = new(2026, 1, 1, 12, 0, 0);
    }
}
