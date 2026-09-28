using System;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// 分表类型枚举
    /// </summary>
    public enum SplitType
    {
        /// <summary>
        /// 按天分表
        /// </summary>
        Day,
        /// <summary>
        /// 按月分表
        /// </summary>
        Month,
        /// <summary>
        /// 按年分表
        /// </summary>
        Year
    }

    /// <summary>
    /// Dapper分表特性
    /// 标记实体需要分表
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DapperSplitTableAttribute : Attribute
    {
        /// <summary>
        /// 分表类型
        /// </summary>
        public SplitType SplitType { get; set; }

        public DapperSplitTableAttribute(SplitType splitType)
        {
            SplitType = splitType;
        }
    }

    /// <summary>
    /// Dapper分表字段特性
    /// 标记用于分表的日期字段
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DapperSplitFieldAttribute : Attribute
    {
    }
}
