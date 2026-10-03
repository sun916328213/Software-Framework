// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SplitType、DapperSplitTableAttribute、DapperSplitFieldAttribute。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、System。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using System;

namespace DataManager.Runtime.SQL.Dapper
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

        /// <summary>
        /// 初始化 DapperSplitTableAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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
