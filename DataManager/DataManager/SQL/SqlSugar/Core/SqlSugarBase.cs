using DataManager.SQL.Definition;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataManager.SQL.SqlSugarSQL
{
    public class SqlSugarBase<T> : ISQL<T> where T : class, new()
    {
        // 同 EFCoreBase/DapperBase：库层面保证 Npgsql Local 时间兼容
        static SqlSugarBase() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        private SqlSugarClient? _dbClient;
        private readonly object _syncLock = new object();

        public virtual string Name { get; set; } = string.Empty;

        public SQLParam SQLParam { get; private set; } = new();

        // P4-1：纯状态位。旧 setter 隐式 Connect/DisConnect 的副作用已删除，
        // 曾导致“读状态触发连接”的难追踪行为；现仅 Connect/DisConnect 内部改写。
        public bool IsConnected { get; private set; }

        public SqlSugarBase()
        {
        }

        public SqlSugarBase(SQLParam param)
        {
            SQLParam = param;
            Name = param.Name;
        }

        public virtual void Connect()
        {
            CreateDatabase();
            CreateTable();
            GetDbClient().Ado.CheckConnection();
            IsConnected = true;
        }

        public virtual void DisConnect()
        {
            IsConnected = false;
        }

        /// <summary>
        /// 释放底层 SqlSugarClient（P4-1：ISQL 继承 IDisposable 后补齐）。
        /// </summary>
        public void Dispose()
        {
            IsConnected = false;
            lock (_syncLock)
            {
                _dbClient?.Dispose();
                _dbClient = null;
            }
            GC.SuppressFinalize(this);
        }

        public void Add(T data)
        {
            if (IsSplitTableEntity())
            {
                GetDbClient().Insertable(data).SplitTable().ExecuteCommand();
            }
            else
            {
                GetDbClient().Insertable(data).ExecuteCommand();
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task AddAsync(T data) => AddAsync(data, CancellationToken.None);

        /// <remarks>
        /// P9-3：非分表走 IInsertable&lt;T&gt;.ExecuteCommandAsync(token)（原生可取消）；
        /// 分表 provider（SplitInsertable&lt;T&gt;）在 SqlSugar 5.1.4.207 无 Token 重载 ⇒ 明确边界：
        /// 进入前检查取消，语句本身不可中断，不伪装成已取消。
        /// </remarks>
        public async Task AddAsync(T data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                await GetDbClient().Insertable(data).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Insertable(data).ExecuteCommandAsync(cancellationToken);
            }
        }

        public void AddRange(IEnumerable<T> datas)
        {
            if (IsSplitTableEntity())
            {
                GetDbClient().Insertable(datas.ToList()).SplitTable().ExecuteCommand();
            }
            else
            {
                GetDbClient().Insertable(datas.ToList()).ExecuteCommand();
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task AddRangeAsync(IEnumerable<T> datas) => AddRangeAsync(datas, CancellationToken.None);

        /// <remarks>P9-3：非分表原生可取消；分表同 <see cref="AddAsync(T, CancellationToken)"/> 的边界说明。</remarks>
        public async Task AddRangeAsync(IEnumerable<T> datas, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                await GetDbClient().Insertable(datas.ToList()).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Insertable(datas.ToList()).ExecuteCommandAsync(cancellationToken);
            }
        }

        public void Delete(Expression<Func<T, bool>> predicate)
        {
            if (IsSplitTableEntity())
            {
                // SqlSugar 约束：条件不能直接挂在 Deleteable().SplitTable() 上；
                // 分表条件删除 = 跨表查出实体后按实体集合 + SplitTable 删。
                var list = GetDbClient().Queryable<T>().Where(predicate).SplitTable().ToList();
                if (list.Count > 0)
                    GetDbClient().Deleteable(list).SplitTable().ExecuteCommand();
            }
            else
            {
                GetDbClient().Deleteable<T>().Where(predicate).ExecuteCommand();
            }
        }

        /// <summary>
        /// 清空表（P4-1：改走 Deleteable，原生支持分表；不再用 TruncateTable——
        /// 行为差异：自增种子不再重置，DELETE 语义跨四库一致）。
        /// 分表：Deleteable&lt;T&gt;().SplitTable() 无参仅支持实体集合模式，需先查出再按集合删。
        /// </summary>
        public void DeleteAll()
        {
            if (IsSplitTableEntity())
            {
                var list = GetDbClient().Queryable<T>().SplitTable().ToList();
                if (list.Count > 0)
                    GetDbClient().Deleteable(list).SplitTable().ExecuteCommand();
                return;
            }
            GetDbClient().Deleteable<T>().ExecuteCommand();
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task DeleteAllAsync() => DeleteAllAsync(CancellationToken.None);

        /// <remarks>
        /// P9-3：查询阶段原生可取消（ToListAsync(token)）；分表删除 provider 无 Token 重载，
        /// 删除语句本身不可中断（边界明确）。
        /// </remarks>
        public async Task DeleteAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                var list = (await GetDbClient().Queryable<T>().SplitTable().ToListAsync(cancellationToken)).ToList();
                cancellationToken.ThrowIfCancellationRequested();
                if (list.Count > 0)
                    await GetDbClient().Deleteable(list).SplitTable().ExecuteCommandAsync();
                return;
            }
            await GetDbClient().Deleteable<T>().ExecuteCommandAsync(cancellationToken);
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task DeleteAsync(Expression<Func<T, bool>> predicate) =>
            DeleteAsync(predicate, CancellationToken.None);

        /// <remarks>P9-3：同 <see cref="DeleteAllAsync(CancellationToken)"/> 的取消边界说明。</remarks>
        public async Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                // 同步 Delete 注释：分表条件删除 = 跨表查出实体后按实体集合 + SplitTable 删。
                var list = (await GetDbClient().Queryable<T>().Where(predicate).SplitTable().ToListAsync(cancellationToken)).ToList();
                cancellationToken.ThrowIfCancellationRequested();
                if (list.Count > 0)
                    await GetDbClient().Deleteable(list).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Deleteable<T>().Where(predicate).ExecuteCommandAsync(cancellationToken);
            }
        }

        public IEnumerable<T> Query()
        {
            if (IsSplitTableEntity())
            {
                return GetDbClient().Queryable<T>().SplitTable().ToList();
            }
            
            return GetDbClient().Queryable<T>().ToList();
        }

        public IEnumerable<T> Query(Expression<Func<T, bool>> predicate)
        {
            if (IsSplitTableEntity())
            {
                return GetDbClient().Queryable<T>().Where(predicate).SplitTable().ToList();
            }
            
            return GetDbClient().Queryable<T>().Where(predicate).ToList();
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task<IEnumerable<T>> QueryAsync() => QueryAsync(CancellationToken.None);

        /// <remarks>P9-3：查询走 ISugarQueryable&lt;T&gt;.ToListAsync(token)，分表同样原生可取消。</remarks>
        public async Task<IEnumerable<T>> QueryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                return await GetDbClient().Queryable<T>().SplitTable().ToListAsync(cancellationToken);
            }
            
            return await GetDbClient().Queryable<T>().ToListAsync(cancellationToken);
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate) =>
            QueryAsync(predicate, CancellationToken.None);

        /// <remarks>P9-3：同 <see cref="QueryAsync(CancellationToken)"/>，条件查询亦原生可取消。</remarks>
        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                return await GetDbClient().Queryable<T>().Where(predicate).SplitTable().ToListAsync(cancellationToken);
            }
            
            return await GetDbClient().Queryable<T>().Where(predicate).ToListAsync(cancellationToken);
        }

        private bool IsSplitTableEntity()
        {
            return typeof(T).GetCustomAttributes(typeof(SplitTableAttribute), false).Any();
        }
 
        /// <summary>
        /// P9-3：单实体按主键更新（SqlSugar 原生语义；分表自动路由）。
        /// </summary>
        public void Update(T data)
        {
            if (IsSplitTableEntity())
            {
                GetDbClient().Updateable(data).SplitTable().ExecuteCommand();
            }
            else
            {
                GetDbClient().Updateable(data).ExecuteCommand();
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task UpdateAsync(T data) => UpdateAsync(data, CancellationToken.None);

        /// <remarks>
        /// P9-3：非分表走 IUpdateable&lt;T&gt;.ExecuteCommandAsync(token)（原生可取消）；
        /// 分表 provider（SplitTableUpdateByObjectProvider&lt;T&gt;）无 Token 重载 ⇒ 仅进入前检查取消。
        /// </remarks>
        public async Task UpdateAsync(T data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                await GetDbClient().Updateable(data).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Updateable(data).ExecuteCommandAsync(cancellationToken);
            }
        }

        /// <summary>
        /// P10：单实例事务（同 client 连接，分表同样原子；嵌套抛）。
        /// 不用 Ado.UseTran：它吞业务异常（回滚但不向外抛），此处显式 Begin/Commit/Rollback保证穿透。
        /// </summary>
        public void ExecuteInTransaction(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_inTransaction)
                throw new InvalidOperationException("嵌套事务不受支持。");
            var ado = GetDbClient().Ado;
            _inTransaction = true;
            try
            {
                ado.BeginTran();
                try
                {
                    action();
                    ado.CommitTran();
                }
                catch
                {
                    ado.RollbackTran();
                    throw;
                }
            }
            finally
            {
                _inTransaction = false;
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task ExecuteInTransactionAsync(Func<Task> action) =>
            ExecuteInTransactionAsync(action, CancellationToken.None);

        /// <remarks>
        /// P9-3：进入前与提交前各检查一次 Token；作用域内取消（OCE）与异常同样走回滚后向上抛。
        /// 回滚不受 Token 影响——回滚是取消的补救动作，绝不能被取消跳过。
        /// </remarks>
        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();
            if (_inTransaction)
                throw new InvalidOperationException("嵌套事务不受支持。");
            var ado = GetDbClient().Ado;
            _inTransaction = true;
            try
            {
                ado.BeginTran();
                try
                {
                    await action();
                    cancellationToken.ThrowIfCancellationRequested();
                    ado.CommitTran();
                }
                catch
                {
                    ado.RollbackTran();
                    throw;
                }
            }
            finally
            {
                _inTransaction = false;
            }
        }

        private bool _inTransaction;

        public void Update(Expression<Func<T, bool>> predicate, T updatedata)
        {
            if (IsSplitTableEntity())
            {
                GetDbClient().Updateable(updatedata).Where(predicate).SplitTable().ExecuteCommand();
            }
            else
            {
                GetDbClient().Updateable(updatedata).Where(predicate).ExecuteCommand();
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata) =>
            UpdateAsync(predicate, updatedata, CancellationToken.None);

        /// <remarks>P9-3：非分表原生可取消；分表同 <see cref="UpdateAsync(T, CancellationToken)"/> 的边界说明。</remarks>
        public async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
            {
                await GetDbClient().Updateable(updatedata).Where(predicate).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Updateable(updatedata).Where(predicate).ExecuteCommandAsync(cancellationToken);
            }
        }

        #region  私有方法
      

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        /// <returns></returns>
        private SqlSugarClient GetDbClient()
        {
            var client = _dbClient;
            if (client == null)
            {
                lock (_syncLock)
                {
                    client = _dbClient;
                    if (client == null)
                    {
                        client = new SqlSugarClient(CreateConnectionConfig());
                        // P9-4：统一命令超时（秒）。0 表示保持 provider 默认，不覆盖 Ado.CommandTimeOut。
                        if (SQLParam.CommandTimeoutOrNull is int timeoutSeconds)
                            client.Ado.CommandTimeOut = timeoutSeconds;
                        _dbClient = client;
                    }
                }
            }
            return client;
        }

        /// <summary>
        /// 获取数据库类型
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private DbType GetDbType()
        {
            return SQLParam.DBType switch
            {
                ESQLType.MySQL => DbType.MySqlConnector,
                ESQLType.PostgreSQL => DbType.PostgreSQL,
                ESQLType.SQLite => DbType.Sqlite,
                ESQLType.SQL_Server => DbType.SqlServer,
                _ => throw new NotSupportedException("未支持的数据库类型")
            };
        }

        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private string GetConnectionString(string database)
        {
            if (!string.IsNullOrWhiteSpace(SQLParam.ConnectionString))
                return SQLParam.ConnectionString;
            return SQLParam.DBType switch
            {
                ESQLType.MySQL => $"Server={SQLParam.ServerIP};Port={SQLParam.Port};Database={database};Uid={SQLParam.UserName};Pwd={SQLParam.Password};",
                ESQLType.PostgreSQL => $"Host={SQLParam.ServerIP};Port={SQLParam.Port};Database={database};Username={SQLParam.UserName};Password={SQLParam.Password};",
                ESQLType.SQLite => $"Data Source={SQLParam.EffectiveFilePath}",
                ESQLType.SQL_Server => BuildSqlServerConnectionString(database),
                _ => throw new NotSupportedException("未支持的数据库类型")
            };
        }

        private string BuildSqlServerConnectionString(string database)
        {
            var server = string.IsNullOrWhiteSpace(SQLParam.Port)
                ? SQLParam.ServerIP
                : $"{SQLParam.ServerIP},{SQLParam.Port}";
            var cs = $"Server={server};Database={database};User Id={SQLParam.UserName};Password={SQLParam.Password};";
            if (SQLParam.TrustServerCertificate)
                cs += "TrustServerCertificate=True;";
            return cs;
        }
 
        /// <summary>
        /// 创建数据库连接配置
        /// </summary>
        /// <returns></returns>
        private ConnectionConfig CreateConnectionConfig()
        {
            return new ConnectionConfig
            {
                DbType = GetDbType(),
                ConnectionString = GetConnectionString(SQLParam.DBName),
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
                MoreSettings = new ConnMoreSettings
                {
                    // PostgreSQL表名和列名自动转小写
                    PgSqlIsAutoToLower = true
                },
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityService = (property, column) =>
                    {
                        var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                        if (columnAttr != null) column.DbColumnName = columnAttr.Name;

                        if (property.GetCustomAttribute<KeyAttribute>() != null)
                            column.IsPrimarykey = true;

                        var identityAttr = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
                        if (identityAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                            column.IsIdentity = true;
                    }
                }
            };
        }

        /// <summary>
        /// 创建数据库（SQLite 无服务器：预建目录，文件由 CodeFirst 懒建）。
        /// </summary>
        private void CreateDatabase()
        {
            if (SQLParam.DBType == ESQLType.SQLite)
            {
                var file = SQLParam.EffectiveFilePath;
                var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                return;
            }
            var config = CreateConnectionConfig();
            // 切换到系统库连接
            config.ConnectionString = GetConnectionString(SQLParam.DBName);
            using var adminClient = new SqlSugarClient(config);
            adminClient.DbMaintenance.CreateDatabase(SQLParam.DBName);
        }

        #endregion

        #region ===================== 官方规范：CodeFirst 建表（核心优化） =====================
        /// <summary>
        /// 官方标准：创建实体默认表（全局唯一映射）
        /// </summary>
        public void CreateTable()
        {
            lock (_syncLock)
            {
                var db = GetDbClient();
                string entityName = typeof(T).Name;
                string defaultTable = GetDefaultTableName();

                // 全局映射仅添加1次，杜绝重复
                if (!db.MappingTables.Any(x => x.EntityName == entityName))
                {
                    db.MappingTables.Add(new MappingTable { EntityName = entityName, DbTableName = defaultTable });
                }

                // 官方建表API
                db.CodeFirst.SetStringDefaultLength(255).InitTables<T>();
            }
        }
        #endregion

        /// <summary>
        /// P9-1：列出该实体的全部分表（原生分表经 SugarTable 模板前缀匹配；非分表实体返回空）。
        /// </summary>
        public List<string> GetSplitTables()
        {
            if (!IsSplitTableEntity())
                return new List<string>();
            // 模板前缀本身常带分隔符（如 t_），缺时才补，避免双下划线永不命中。
            var prefix = GetSplitTablePrefix();
            var head = prefix.EndsWith("_") ? prefix : prefix + "_";
            return GetDbClient().DbMaintenance.GetTableInfoList(false)
                .Select(t => t.Name)
                .Where(n => n.StartsWith(head, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// 分表名前缀：SugarTable 模板取 '{' 之前部分；无模板用默认表名。
        /// </summary>
        private string GetSplitTablePrefix()
        {
            var sugarTable = typeof(T).GetCustomAttribute<SugarTable>();
            var template = sugarTable?.TableName;
            if (!string.IsNullOrEmpty(template))
            {
                var brace = template.IndexOf('{');
                if (brace > 0)
                    return template.Substring(0, brace);
                return template;
            }
            return GetDefaultTableName();
        }

        #region ===================== 官方规范：删表 =====================

        /// <summary>
        /// 删除表
        /// </summary>
        /// <param name="tableName"></param>
        public void DropTable(string tableName)
        {
            if (!TableExists(tableName)) return;
            GetDbClient().DbMaintenance.DropTable(tableName);
        }
        public async Task DropTableAsync(string tableName)
        {
            // 底层 DbMaintenance 仅同步 API：原地执行后返回已完成任务，不占用线程池（P4-1 去 Task.Run）。
            DropTable(tableName);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 删除所有表
        /// </summary>
        public void DropAllTables()
        {
            var db = GetDbClient();

            var tables = db.DbMaintenance.GetTableInfoList(false);
            foreach (var table in tables)
            {
                try { db.DbMaintenance.DropTable(table.Name); } catch { }
            }
        }
        public async Task DropAllTablesAsync()
        {
            DropAllTables();
            await Task.CompletedTask;
        }
        #endregion

        #region ===================== 官方规范：原生判断表是否存在 =====================
        /// <summary>
        /// 判断表是否存在
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public bool TableExists(string tableName) => GetDbClient().DbMaintenance.IsAnyTable(tableName, false);

        /// <summary>
        /// 判断默认表是否存在
        /// </summary>
        /// <returns></returns>
        public bool TableExists() => TableExists(GetDefaultTableName());

        /// <summary>
        /// 异步判断表是否存在
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public async Task<bool> TableExistsAsync(string tableName)
        {
            var exists = TableExists(tableName);
            await Task.CompletedTask;
            return exists;
        }

        /// <summary>
        /// 异步判断默认表是否存在
        /// </summary>
        /// <returns></returns>
        public Task<bool> TableExistsAsync() => TableExistsAsync(GetDefaultTableName());

        /// <summary>
        /// 获取默认表名
        /// </summary>
        /// <returns></returns>
        private string GetDefaultTableName()
        {
            var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
            return tableAttr != null ? tableAttr.Name : typeof(T).Name.ToLower();
        }
        #endregion
    }
}