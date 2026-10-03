// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ExcelBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Excel.Definition、System、System.Collections.Generic、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Excel.Definition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Core.Excel
{
    /// <summary>
    /// ExcelBase 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：IExcel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class ExcelBase : IExcel
    {
        public abstract ExcelParam ExcelParam { get; set; }


        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// </summary>
        public abstract void Add<T>(T data) where T : class, new();
        /// <summary>
        /// 执行添加操作；对应入口为 AddAsync。
        /// </summary>
        public abstract Task AddAsync<T>(T data) where T : class, new();
        /// <summary>
        /// 执行添加操作；对应入口为 AddRange。
        /// </summary>
        public abstract void AddRange<T>(IEnumerable<T> data) where T : class, new();
        /// <summary>
        /// 执行添加操作；对应入口为 AddRangeAsync。
        /// </summary>
        public abstract Task AddRangeAsync<T>(IEnumerable<T> data) where T : class, new();


        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// </summary>
        public abstract void Write<T>(IEnumerable<T> datas) where T : class, new();
        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// </summary>
        public abstract Task WriteAsync<T>(IEnumerable<T> datas) where T : class, new();


        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// </summary>
        public abstract IEnumerable<T> Query<T>() where T : class, new();
        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// </summary>
        public abstract IEnumerable<T> Query<T>(Func<T, bool> predicate) where T : class, new();
        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// </summary>
        public abstract Task<IEnumerable<T>> QueryAsync<T>() where T : class, new();
        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// </summary>
        public abstract Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class, new();


        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 Update。
        /// </summary>
        public abstract void Update<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateAsync。
        /// </summary>
        public abstract Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
    }
}