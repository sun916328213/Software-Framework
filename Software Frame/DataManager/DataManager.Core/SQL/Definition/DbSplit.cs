// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SplitPeriod、DbSplitTableAttribute、DbSplitFieldAttribute。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、System、System.Reflection。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using System;
using System.Reflection;

namespace DataManager.Core.SQL.Definition
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

        /// <summary>
        /// 初始化 DbSplitTableAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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
