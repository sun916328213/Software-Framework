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
        private SqlSugarClient _dbClient;
        private readonly object _syncLock = new object();

        public virtual string Name { get; set; }

        public SQLParam SQLParam { get; set; }

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
            if (_dbClient == null)
            {
                lock (_syncLock)
                {
                    if (_dbClient == null)
                    {
                        _dbClient = new SqlSugarClient(CreateConnectionConfig());
                    }
                }
            }
            return _dbClient;
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

        #region  ===================== 官方规范：分表操作 暂时不使用=====================

        // 官方分表示例（按日分表，CreateTime为分表字段）

        //[SugarTable("TestModel_{year}{month}{day}")]
        //[SplitTable(SplitType.Day)] // 按日分表
        //public class TestModel_Day
        //{
        //    [SplitField]
        //    public DateTime CreateTime { get; set; }
        //}

        /// <summary>
        /// 【官方标准】初始化分表结构
        /// 功能：自动创建所有分表 + 同步实体表结构（程序启动时调用一次即可）
        /// 支持：按年/月/日分表，自动根据实体[SplitTable]特性创建
        /// </summary>
        private void InitSplitTable()
        {
            // 官方推荐：CopyNew() 创建新实例，线程安全，不污染全局客户端
            GetDbClient().CopyNew().CodeFirst.SplitTables().InitTables<T>();
        }

        /// <summary>
        /// 【分表插入】单条数据插入（自动路由到对应分表，表不存在自动创建）
        /// 对应文档：插入自动路由 + 自动建表
        /// </summary>
        private void SplitTableAdd(T data)
        {
            GetDbClient().CopyNew().Insertable(data).SplitTable().ExecuteCommand();
        }

        /// <summary>
        /// 【分表插入】批量数据插入（高性能，官方推荐）
        /// 自动根据[SplitField]时间字段路由到对应分表
        /// </summary>
        private void SplitTableAddRange(List<T> datas)
        {
            GetDbClient().CopyNew().Insertable(datas).SplitTable().ExecuteCommand();
        }

        /// <summary>
        /// 【分表查询】按时间范围查询（自动路由，性能最优）
        /// 示例：按月/按年分表时，仅查询指定时间范围内的分表
        /// </summary>
        private List<T> SplitTableQuery(DateTime startTime, DateTime endTime)
        {
            return GetDbClient().CopyNew()
                       .Queryable<T>()
                       .SplitTable(startTime, endTime) // 官方路由：仅扫描目标分表
                       .ToList();
        }

        /// <summary>
        /// 【分表查询】查询【最近N张分表】（官方性能优化方案）
        /// 例：Take(3) → 查询最近3张分表，避免全表扫描
        /// </summary>
        private List<T> SplitTableQueryByTake(int takeCount = 3)
        {
            return GetDbClient().CopyNew()
                       .Queryable<T>()
                       .SplitTable(tabs => tabs.Take(takeCount))
                       .ToList();
        }

        /// <summary>
        /// 【分表查询】查询【所有分表】数据
        /// 注意：仅主键/索引字段查询使用，无索引慎用
        /// </summary>
        private List<T> SplitTableQueryAll()
        {
            return GetDbClient().CopyNew()
                       .Queryable<T>()
                       .SplitTable() // 无参数：查询所有分表
                       .ToList();
        }

        /// <summary>
        /// 【分表更新】按实体更新（自动路由分表）
        /// </summary>
        private int SplitTableUpdate(T data)
        {
            return GetDbClient().CopyNew()
                       .Updateable(data)
                       .SplitTable()
                       .ExecuteCommand();
        }

        /// <summary>
        /// 【分表删除】按实体删除（自动路由分表）
        /// </summary>
        private int SplitTableDelete(T data)
        {
            return GetDbClient().CopyNew()
                       .Deleteable(data)
                       .SplitTable()
                       .ExecuteCommand();
        }

        /// <summary>
        /// 【官方工具】获取当前时间对应的【分表名】
        /// 例：2024年 → test_table_2024；2024年4月 → test_table_202404
        /// </summary>
        private string GetSplitTableName(DateTime time)
        {
            return GetDbClient().CopyNew().SplitHelper<T>().GetTableName(time);
        }

        /// <summary>
        /// 【官方工具】获取当前实体的【所有分表名】
        /// </summary>
        private List<string> GetAllSplitTableNames()
        {
            return GetDbClient().CopyNew()
                       .SplitHelper<T>()
                       .GetTables()
                       .Select(x => x.TableName)
                       .ToList();
        }
        #endregion
    }
}