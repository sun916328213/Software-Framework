using DataManager.Excel.Definition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Excel
{
    public interface IExcel
    {
        /// <summary>
        /// Excel参数
        /// </summary>
        ExcelParam ExcelParam { get; set; }

        /// <summary>
        /// 数据添加
        /// </summary>
        void Add<T>(T data) where T : class, new();

        /// <summary>
        /// 数据添加 异步
        /// </summary>
        Task AddAsync<T>(T data) where T : class, new();

        /// <summary>
        /// 数据追加
        /// </summary>
        void AddRange<T>(IEnumerable<T> data) where T : class ,new();

        /// <summary>
        /// 数据追加 异步
        /// </summary>
        Task AddRangeAsync<T>(IEnumerable<T> data) where T : class, new();

        /// <summary>
        /// 数据写入
        /// </summary>
        void Write<T>(IEnumerable<T> datas) where T : class, new();

        /// <summary>
        /// 数据写 异步
        /// </summary>
        Task WriteAsync<T>(IEnumerable<T> datas) where T : class, new();

        /// <summary>
        /// 查找数据
        /// </summary>
        IEnumerable<T> Query<T>() where T : class, new();

        /// <summary>
        /// 查找数据 异步
        /// </summary>
        Task<IEnumerable<T>> QueryAsync<T>() where T : class, new();

        /// <summary>
        /// 查找数据
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <returns></returns>
        IEnumerable<T> Query<T>(Func<T, bool> predicate) where T : class, new();

        /// <summary>
        /// 查找数据 异步
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <returns></returns>
        Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class, new();

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        void Update<T>(Func<T, bool> predicate, T updatedata) where T : class, new();

        /// <summary>
        /// 更新数据 异步
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
    }
}