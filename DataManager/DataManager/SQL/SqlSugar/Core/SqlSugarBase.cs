using DataManager.SQL.Definition;
using Dm;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

        public SQLParam SQLParam { get; set; } = new();

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (value) Connect();
                else DisConnect();
            }
        }

        public virtual void Connect()
        {
            CreateDatabase();
            CreateTable();
            GetDbClient().Ado.CheckConnection();
            _isConnected = true;
        }

        public virtual void DisConnect()
        {
            _isConnected = false;
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

        public async Task AddAsync(T data)
        {
            if (IsSplitTableEntity())
            {
                await GetDbClient().Insertable(data).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Insertable(data).ExecuteCommandAsync();
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

        public async Task AddRangeAsync(IEnumerable<T> datas)
        {
            if (IsSplitTableEntity())
            {
                await GetDbClient().Insertable(datas.ToList()).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Insertable(datas.ToList()).ExecuteCommandAsync();
            }
        }

        public void Delete(Expression<Func<T, bool>> predicate)
        {
            if (IsSplitTableEntity())
            {
                GetDbClient().Deleteable<T>().Where(predicate).SplitTable().ExecuteCommand();
            }
            else
            {
                GetDbClient().Deleteable<T>().Where(predicate).ExecuteCommand();
            }
        }

        public void DeleteAll()
        {
            GetDbClient().DbMaintenance.TruncateTable(typeof(T).Name);
        }

        public async Task DeleteAllAsync()
        {
            await Task.Run(DeleteAll);
        }

        public async Task DeleteAsync(Expression<Func<T, bool>> predicate)
        {
            if (IsSplitTableEntity())
            {
                await GetDbClient().Deleteable<T>().Where(predicate).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Deleteable<T>().Where(predicate).ExecuteCommandAsync();
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

        public async Task<IEnumerable<T>> QueryAsync()
        {
            if (IsSplitTableEntity())
            {
                return await GetDbClient().Queryable<T>().SplitTable().ToListAsync();
            }
            
            return await GetDbClient().Queryable<T>().ToListAsync();
        }

        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            if (IsSplitTableEntity())
            {
                return await GetDbClient().Queryable<T>().Where(predicate).SplitTable().ToListAsync();
            }
            
            return await GetDbClient().Queryable<T>().Where(predicate).ToListAsync();
        }

        private bool IsSplitTableEntity()
        {
            return typeof(T).GetCustomAttributes(typeof(SplitTableAttribute), false).Any();
        }
 
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

        public async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata)
        {
            if (IsSplitTableEntity())
            {
                await GetDbClient().Updateable(updatedata).Where(predicate).SplitTable().ExecuteCommandAsync();
            }
            else
            {
                await GetDbClient().Updateable(updatedata).Where(predicate).ExecuteCommandAsync();
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
            return SQLParam.DBType switch
            {
                ESQLType.MySQL => $"Server={SQLParam.ServerIP};Port={SQLParam.Port};Database={database};Uid={SQLParam.UserName};Pwd={SQLParam.Password};",
                ESQLType.PostgreSQL => $"Host={SQLParam.ServerIP};Port={SQLParam.Port};Database={database};Username={SQLParam.UserName};Password={SQLParam.Password};",
                _ => throw new NotSupportedException("未支持的数据库类型")
            };
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
        /// 创建数据库
        /// </summary>
        private void CreateDatabase()
        {
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
        public async Task DropTableAsync(string tableName) => await Task.Run(() => DropTable(tableName));

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
        public async Task DropAllTablesAsync() => await Task.Run(() => DropAllTables());
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
        public async Task<bool> TableExistsAsync(string tableName) => await Task.Run(() => TableExists(tableName));

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