// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：IExcelReader、IExcelWriter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、System、System.Collections.Generic、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Core.Excel
{
    /// <summary>
    /// Excel 读接口（P1-4 ISP 拆分）。
    /// </summary>
    public interface IExcelReader
    {
        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// </summary>
        IEnumerable<T> Query<T>() where T : class, new();
        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// </summary>
        Task<IEnumerable<T>> QueryAsync<T>() where T : class, new();
        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// </summary>
        IEnumerable<T> Query<T>(Func<T, bool> predicate) where T : class, new();
        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// </summary>
        Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class, new();
    }

    /// <summary>
    /// Excel 写接口（P1-4 ISP 拆分）。
    /// </summary>
    public interface IExcelWriter
    {
        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// </summary>
        void Add<T>(T data) where T : class, new();
        /// <summary>
        /// 执行添加操作；对应入口为 AddAsync。
        /// </summary>
        Task AddAsync<T>(T data) where T : class, new();
        /// <summary>
        /// 执行添加操作；对应入口为 AddRange。
        /// </summary>
        void AddRange<T>(IEnumerable<T> data) where T : class, new();
        /// <summary>
        /// 执行添加操作；对应入口为 AddRangeAsync。
        /// </summary>
        Task AddRangeAsync<T>(IEnumerable<T> data) where T : class, new();
        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// </summary>
        void Write<T>(IEnumerable<T> datas) where T : class, new();
        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// </summary>
        Task WriteAsync<T>(IEnumerable<T> datas) where T : class, new();
        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 Update。
        /// </summary>
        void Update<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateAsync。
        /// </summary>
        Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
    }
}
