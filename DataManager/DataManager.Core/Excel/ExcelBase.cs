using DataManager.Excel.Definition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Excel
{
    public abstract class ExcelBase : IExcel
    {
        public abstract ExcelParam ExcelParam { get; set; }


        public abstract void Add<T>(T data) where T : class, new();
        public abstract Task AddAsync<T>(T data) where T : class, new();
        public abstract void AddRange<T>(IEnumerable<T> data) where T : class, new();
        public abstract Task AddRangeAsync<T>(IEnumerable<T> data) where T : class, new();


        public abstract void Write<T>(IEnumerable<T> datas) where T : class, new();
        public abstract Task WriteAsync<T>(IEnumerable<T> datas) where T : class, new();


        public abstract IEnumerable<T> Query<T>() where T : class, new();
        public abstract IEnumerable<T> Query<T>(Func<T, bool> predicate) where T : class, new();
        public abstract Task<IEnumerable<T>> QueryAsync<T>() where T : class, new();
        public abstract Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class, new();


        public abstract void Update<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
        public abstract Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata) where T : class, new();
    }
}