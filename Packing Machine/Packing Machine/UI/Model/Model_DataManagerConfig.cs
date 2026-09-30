using DataManager.Controls.Models;
using DataManager.SQL.Dapper;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Packing_Machine.UI.Model
{
    /// <summary>
    /// SQL 配置页（DataManager.Controls 的 <c>SqlView</c>）的行实体。
    ///
    /// 为什么由宿主定义：<c>SqlViewModel&lt;T&gt;</c> / <c>SqlWorkspace&lt;T&gt;</c> 是泛型，
    /// T 由宿主提供（DataManager.Controls 里只约束 <see cref="ISqlEntity"/>）；
    /// ORM 映射随实体走，所以表名与分表特性写在这里。
    /// 这也决定了注册必须落在本项目的 App.RegisterTypes —— WPFControl 是被本项目引用的库，
    /// 模块里看不到本命名空间的类型。
    ///
    /// ⚠️ 表名与 DataManager.Workbench 的 <c>workbench_entity_*</c> 刻意区分开：
    /// 两个宿主若连同一个库，测试数据不应落到同一张表。
    /// </summary>
    [SugarTable("packing_sql_entity_{year}{month}{day}")]
    [SplitTable(SqlSugar.SplitType.Day)]
    [DapperSplitTable(DataManager.SQL.Dapper.SplitType.Day)]
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
