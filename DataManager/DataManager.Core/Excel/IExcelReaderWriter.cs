using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Excel
{
    /// <summary>
    /// Excel 读接口（P1-4 ISP 拆分）。
    /// </summary>
    public interface IExcelReader
    {
        IEnumerable<T> Query<T>() where T : class, new();
        Task<IEnumerable<T>> QueryAsync<T>() where T : class, new();
        IEnumerable<T> Query<T>(Func<T, bool> predicate) where T : class, new();
        Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class, new();
    }

    /// <summary>
    /// Excel 写接口（P1-4 ISP 拆分）。
    /// </summary>
    public interface IExcelWriter
    {
        void Add<T>(T data) where T : class, new();
        Task AddAsync<T>(T data) where T : class, new();
        void AddRange<T>(IEnumerable<T> data) where T : class, new();
        Task AddRangeAsync<T>(IEnumerable<T> data) where T : class, new();
        void Write<T>(IEnumerable<T> datas) where T : class, new();
        Task WriteAsync<T>(IEnumerable<T> datas) where T : class, new();
        void Update<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
        Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
    }
}
