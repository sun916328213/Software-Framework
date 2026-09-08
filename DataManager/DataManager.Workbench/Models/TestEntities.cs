using DataManager.SQL.Dapper;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataManager.Workbench.Models
{
    /// <summary>
    /// SQL 测试实体（同时兼容 EFCore / SqlSugar / Dapper 三种工具的 Day 分表）
    /// </summary>
    [SugarTable("workbench_entity_{year}{month}{day}")]
    [SplitTable(SqlSugar.SplitType.Day)]
    [DapperSplitTable(DataManager.SQL.Dapper.SplitType.Day)]
    public class TestEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Key]
        public long Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        [SugarColumn(ColumnDataType = "timestamp without time zone", IsNullable = true)]
        public DateTime? CreateTime { get; set; }
    }

    /// <summary>
    /// Excel 测试实体
    /// </summary>
    [Table("ExcelPerson")]
    public class ExcelPerson
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime CreateTime { get; set; } = new(2026, 1, 1, 12, 0, 0);
    }
}
