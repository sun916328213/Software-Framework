using DataManager.SQL.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataManager.SQL
{
    /// <summary>
    /// P4-1：参数与状态只读（构造期注入，杜绝运行时替换导致的行为漂移）；可释放。
    /// </summary>
    public interface ISQL : IDisposable
    {
        /// <summary>
        /// 数据库参数（构造期确定，只读）
        /// </summary>
        SQLParam SQLParam { get; }
        /// <summary>
        /// 是否连接成功（仅 Connect/DisConnect 内部改写）
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// 连接
        /// </summary>
        void Connect();
        /// <summary>
        /// 断开数据
        /// </summary>
        void DisConnect();
    }

    /// <summary>
    /// P9-2：异步面统一提供可取消重载。
    /// 约定：旧签名（无 Token）保持不变，内部等价于传 <see cref="CancellationToken.None"/>，
    /// 现有调用点零修改继续编译；新签名多带一个 <see cref="CancellationToken"/> 参数。
    /// 取消语义：取消时向上抛 <see cref="OperationCanceledException"/>（<see cref="TaskCanceledException"/> 同族），
    /// 不吞掉、不包装成普通失败，也不返回空集合或 false；事务作用域内取消与异常一致——整体回滚。
    /// Token 属于单次调用的生命周期控制，不放进 <see cref="SQLParam"/>。
    /// </summary>
    public interface ISQL<T>:ISQL
    {
        /// <summary>
        /// 数据写入
        /// </summary>
        /// <param name="data">数据</param>
        void Add(T data);
        /// <summary>
        /// 数据写入 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        Task AddAsync(T data);
        /// <summary>
        /// 数据写入 异步（可取消）
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task AddAsync(T data, CancellationToken cancellationToken);
        /// <summary>
        /// 数据写入连续
        /// </summary>
        void AddRange(IEnumerable<T> datas);
        /// <summary>
        /// 数据写入连续 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        Task AddRangeAsync(IEnumerable<T> datas);
        /// <summary>
        /// 数据写入连续 异步（可取消）
        /// </summary>
        /// <param name="datas">数据集合</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task AddRangeAsync(IEnumerable<T> datas, CancellationToken cancellationToken);
        /// <summary>
        /// 查找数据
        /// </summary>
        IEnumerable<T> Query();
        /// <summary>
        /// 查找数据 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        Task<IEnumerable<T>> QueryAsync();
        /// <summary>
        /// 查找数据 异步（可取消）
        /// </summary>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task<IEnumerable<T>> QueryAsync(CancellationToken cancellationToken);
        /// <summary>
        /// 查找数据
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <returns></returns>
        IEnumerable<T> Query(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 查找数据 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <returns></returns>
        Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 查找数据 异步（可取消）
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        void Delete(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 删除 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        Task DeleteAsync(Expression<Func<T, bool>> predicate);
        /// <summary>
        /// 删除 异步（可取消）
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        /// <summary>
        /// 删除所有数据
        /// </summary>
        void DeleteAll();
        /// <summary> 
        /// 删除所有数据 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        Task DeleteAllAsync();
        /// <summary>
        /// 删除所有数据 异步（可取消）
        /// </summary>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task DeleteAllAsync(CancellationToken cancellationToken);
        /// <summary>
        /// 更新单个实体（按主键，各 provider 原生语义；与条件更新的非空合并语义不同，null 可能覆盖）。
        /// 分表实体按实体日期自动路由。
        /// </summary>
        void Update(T data);
        /// <summary>
        /// 更新单个实体 异步（语义同同步重载；等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        Task UpdateAsync(T data);
        /// <summary>
        /// 更新单个实体 异步（可取消）
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task UpdateAsync(T data, CancellationToken cancellationToken);
        /// <summary>
        /// P10：单实例事务。作用域内所有操作同连接原子提交，异常回滚。
        /// 矩阵：非分表三路全原子；Dapper/SqlSugar 分表同连接原子；
        /// EFCore 分表抛 NotSupportedException（fork 独立连接）；嵌套抛 InvalidOperationException。
        /// </summary>
        void ExecuteInTransaction(Action action);
        /// <summary>
        /// P10：单实例事务 异步（语义同同步重载；等价于传 <see cref="CancellationToken.None"/>）。
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> action);
        /// <summary>
        /// P9-2：单实例事务 异步（可取消）。
        /// 取消语义：进入事务前与提交前各检查一次 Token；任一环节取消（含作用域内操作抛出
        /// <see cref="OperationCanceledException"/>）都走回滚路径再向上抛，绝不半提交。
        /// </summary>
        /// <param name="action">事务体</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken);
        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        void Update(Expression<Func<T, bool>> predicate, T updatedata);
        /// <summary>
        /// 更新数据 异步（等价于传 <see cref="CancellationToken.None"/>）
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata);
        /// <summary>
        /// 更新数据 异步（可取消）
        /// </summary>
        /// <param name="predicate">条件检查器</param>
        /// <param name="updatedata">更新数据</param>
        /// <param name="cancellationToken">取消标记；取消时向上抛 <see cref="OperationCanceledException"/></param>
        Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata, CancellationToken cancellationToken);
    }

}
