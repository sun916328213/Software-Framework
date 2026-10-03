// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：TestEntity、ExcelPerson。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Workbench、DataManager.Controls.Models、DataManager.Runtime.SQL.Dapper、SqlSugar、System.ComponentModel.DataAnnotations。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Workbench;
using DataManager.Controls.Models;
using DataManager.Runtime.SQL.Dapper;
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
    [DapperSplitTable(DataManager.Runtime.SQL.Dapper.SplitType.Day)]
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
