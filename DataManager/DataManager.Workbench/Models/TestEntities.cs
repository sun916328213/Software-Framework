using DataManager.Controls.Models;
using DataManager.SQL.Dapper;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataManager.Workbench.Models
{
    /// <summary>
    /// SQL 演示实体（宿主定义，实现 Controls 的行契约；分表映射随实体走）。
    /// </summary>
    [SugarTable("workbench_entity_{year}{month}{day}")]
    [SplitTable(SqlSugar.SplitType.Day)]
    [DapperSplitTable(DataManager.SQL.Dapper.SplitType.Day)]
    public class TestEntity : ISqlEntity
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
    /// Excel 演示实体（宿主定义，实现 Controls 的行契约）。
    /// </summary>
    [Table("ExcelPerson")]
    public class ExcelPerson : IExcelTestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime CreateTime { get; set; } = new(2026, 1, 1, 12, 0, 0);
    }
}
