using System;
using System.Reflection;

namespace DataManager.SQL.Definition
{
    /// <summary>
    /// 分表周期（工具无关的共享定义；Dapper 的 SplitType 与之逐项对应）。
    /// </summary>
    public enum SplitPeriod
    {
        Day,
        Month,
        Year
    }

    /// <summary>
    /// 共享分表特性：标记实体需要按时间分表，可被 EFCore / Dapper 识别。
    /// 新代码请用此特性；Dapper 的旧特性（DapperSplitTableAttribute）继续兼容。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DbSplitTableAttribute : Attribute
    {
        public SplitPeriod Period { get; }

        public DbSplitTableAttribute(SplitPeriod period)
        {
            Period = period;
        }
    }

    /// <summary>
    /// 共享分表字段特性：标记用于分表路由的日期字段（找不到时回退到首个 DateTime 属性）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DbSplitFieldAttribute : Attribute
    {
    }
}
