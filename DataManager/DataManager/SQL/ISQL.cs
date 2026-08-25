using DataManager.SQL.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.SQL
{
    public interface ISQL
    {
        /// <summary>
        /// 数据库参数
        /// </summary>
        SQLParam SQLParam { get; set; }
        /// <summary>
        /// 是否连接成功
        /// </summary>
        bool IsConnected { get; set; }
        /// <summary>
        /// 连接
        /// </summary>
        void Connect();
        /// <summary>
        /// 断开数据
        /// </summary>
        void DisConnect();
    }
    public interface ISQL<T>:ISQL
    {
        /// <summary>
        /// 数据写入
        /// </summary>
        /// <param name="data">数据</param>
        void Add(T data);
        /// <summary>
        /// 数据写入 异步
        /// </summary>
        Task AddAsync(T data);
        /// <summary>
        /// 数据写入连续
        /// </summary>
        void AddRange(IEnumerable<T> datas);
        /// <summary>
        /// 数据写入连续 异步
        /// </summary>
        Task AddRangeAsync(IEnumerable<T> datas);
        /// <summary>
        /// 查找数据
        /// </summary>
        IEnumerable<T> Query();
        /// <summary>
        /// 查找数据 异步
        /// </summary>
        Task<IEnumerable<T>> QueryAsync();
        /// <summary>
        /// 查找数据
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <returns></returns>
        IEnumerable<T> Query(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 查找数据 异步
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <returns></returns>
        Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        void Delete(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 删除 异步
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        Task DeleteAsync(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 删除所有数据
        /// </summary>
        void DeleteAll();
        /// <summary> 
        /// 删除所有数据 异步
        /// </summary>
        Task DeleteAllAsync();
        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        void Update(Expression<Func<T, bool>> predicate, T updatedata);
        /// <summary>
        /// 更新数据 异步
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata);
    }

}
