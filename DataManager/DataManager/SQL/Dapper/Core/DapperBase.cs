using Dapper;
using Dommel;
using DataManager.SQL.Definition;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using MySqlConnector;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// Dapper + Dommel 数据库操作类
    /// 提供类似SqlSugar的简洁API
    /// 支持分表功能（通过特性标记）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class DapperBase<T> : ISQL<T> where T : class
    {
        private IDbConnection _connection;
        private SQLParam _sqlParam;

        public SQLParam SQLParam 
        { 
            get => _sqlParam; 
            set => _sqlParam = value;
        }
        
        public bool IsConnected { get; set; }
        public virtual string Name { get; set; }

        public DapperBase()
        {
        }

        public DapperBase(SQLParam sqlParam)
        {
            SQLParam = sqlParam;
        }

        /// <summary>
        /// 获取数据库连接（自动连接）
        /// </summary>
        private IDbConnection GetConnection()
        {
            if (_connection?.State == ConnectionState.Open)
                return _connection;

            _connection = CreateConnection();
            _connection.Open();
            IsConnected = true;
            return _connection;
        }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        private IDbConnection CreateConnection()
        {
            var connectionString = SqlDialectHelper.BuildConnectionString(SQLParam);
            return SqlDialectHelper.CreateConnection(SQLParam.DBType, connectionString);
        }

        public void Connect()
        {
            // 1. 检查数据库是否存在，不存在则创建
            if (!SqlDialectHelper.IsDatabaseExists(SQLParam))
            {
                SqlDialectHelper.CreateDatabase(SQLParam);
            }

            // 2. 连接目标数据库
            var conn = CreateConnection();
            conn.Open();
            _connection = conn;
            IsConnected = true;

            // 3. 创建表
            CreateTableIfNotExists();
        }


        /// <summary>
        /// 创建表（如果不存在）
        /// </summary>
        private void CreateTableIfNotExists()
        {
            try
            {
                var conn = GetConnection();
                var tableName = IsSplitTableEntity() 
                    ? GetSplitTableName(DateTime.Now) 
                    : (typeof(T).GetCustomAttribute<TableAttribute>()?.Name ?? typeof(T).Name);
                
                var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
                
                // 检查表是否存在
                var checkSql = SqlDialectHelper.BuildCheckTableExistsSql(actualTableName, SQLParam.DBName, SQLParam.DBType);
                bool tableExists = conn.ExecuteScalar<int?>(checkSql) != null;
                
                // 表不存在则创建
                if (!tableExists)
                {
                    conn.Execute(GenerateCreateTableSql(actualTableName));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"警告: 创建表失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 生成CREATE TABLE SQL
        /// </summary>
        private string GenerateCreateTableSql(string tableName)
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();

            var columns = new List<string>();

            foreach (var prop in properties)
            {
                var keyAttr = prop.GetCustomAttribute<KeyAttribute>();
                
                // 处理主键
                if (keyAttr != null)
                {
                    columns.Add(SqlDialectHelper.BuildPrimaryKeyDefinition(prop.Name, prop.PropertyType, SQLParam.DBType));
                    continue;
                }
                
                // 处理普通列
                var columnDef = SqlNamingHelper.FormatColumnName(prop.Name, SQLParam.DBType);
                columnDef += $" {SqlDialectHelper.GetDbType(prop.PropertyType, SQLParam.DBType)}";
                
                // 可空检查
                var isNullable = IsNullableType(prop.PropertyType);
                if (!isNullable)
                {
                    columnDef += " NOT NULL";
                }

                columns.Add(columnDef);
            }

            var columnsSql = string.Join(", ", columns);
            var wrappedTableName = SqlDialectHelper.WrapTableName(tableName, SQLParam.DBType);
            var suffix = SqlDialectHelper.GetCreateTableSuffix(SQLParam.DBType);

            return $"CREATE TABLE IF NOT EXISTS {wrappedTableName} ({columnsSql}){suffix}";
        }

        /// <summary>
        /// 判断是否为可空类型
        /// </summary>
        private bool IsNullableType(Type type)
        {
            return !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public void DisConnect()
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
            IsConnected = false;
        }

        public void Add(T data)
        {
            var conn = GetConnection();
            
            // 如果是分表实体，自动根据数据中的日期字段路由到对应分表
            if (IsSplitTableEntity())
            {
                var date = GetSplitFieldValue(data);
                var tableName = GetSplitTableName(date);
                var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
                InsertToTable(conn, actualTableName, data);
            }
            else
            {
                conn.Insert(data);
            }
        }

        /// <summary>
        /// 在分表中更新数据
        /// </summary>
        private void UpdateInSplitTable(IDbConnection conn, string tableName, T data)
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();
            
            // 获取主键字段
            var keyProperty = properties.FirstOrDefault(p => p.GetCustomAttributes(typeof(KeyAttribute), false).Any());
            if (keyProperty == null)
            {
                throw new InvalidOperationException("实体必须包含主键字段");
            }
            
            // 构建SET子句（排除主键）
            var setClauses = properties
                .Where(p => p.Name != keyProperty.Name)
                .Select(p => $"{SqlNamingHelper.ConvertColumnName(p.Name, SQLParam.DBType)} = @{p.Name}")
                .ToList();
            
            var actualTableName = SqlNamingHelper.FormatTableName(tableName, SQLParam.DBType);
            var whereClause = $"{SqlNamingHelper.ConvertColumnName(keyProperty.Name, SQLParam.DBType)} = @{keyProperty.Name}";
            var sql = $"UPDATE {actualTableName} SET {string.Join(", ", setClauses)} WHERE {whereClause}";
            
            conn.Execute(sql, data);
        }

        private async Task UpdateInSplitTableAsync(IDbConnection conn, string tableName, T data)
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();
            
            var keyProperty = properties.FirstOrDefault(p => p.GetCustomAttributes(typeof(KeyAttribute), false).Any());
            if (keyProperty == null)
            {
                throw new InvalidOperationException("实体必须包含主键字段");
            }
            
            var setClauses = properties
                .Where(p => p.Name != keyProperty.Name)
                .Select(p => $"{SqlNamingHelper.ConvertColumnName(p.Name, SQLParam.DBType)} = @{p.Name}")
                .ToList();
            
            var actualTableName = SqlNamingHelper.FormatTableName(tableName, SQLParam.DBType);
            var whereClause = $"{SqlNamingHelper.ConvertColumnName(keyProperty.Name, SQLParam.DBType)} = @{keyProperty.Name}";
            var sql = $"UPDATE {actualTableName} SET {string.Join(", ", setClauses)} WHERE {whereClause}";
            
            await conn.ExecuteAsync(sql, data);
        }

        /// <summary>
        /// 插入到指定表（分表专用）
        /// </summary>
        private void InsertToTable(IDbConnection conn, string tableName, T data)
        {
            // 检查并创建表
            var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
            var checkSql = SqlDialectHelper.BuildCheckTableExistsSql(actualTableName, SQLParam.DBName, SQLParam.DBType);
            bool tableExists = conn.ExecuteScalar<int?>(checkSql) != null;
            if (!tableExists) conn.Execute(GenerateCreateTableSql(actualTableName));
            
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();
            
            // 使用SqlNamingHelper统一处理列名
            var columns = SqlNamingHelper.FormatColumnList(
                properties.Select(p => p.Name).ToArray(), 
                SQLParam.DBType);
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            var sql = $"INSERT INTO {SqlNamingHelper.FormatTableName(tableName, SQLParam.DBType)} ({columns}) VALUES ({parameters})";
            
            conn.Execute(sql, data);
        }

        public async Task AddAsync(T data)
        {
            var conn = GetConnection();
            await conn.InsertAsync(data);
        }

        public void AddRange(IEnumerable<T> datas)
        {
            var conn = GetConnection();
            
            // 如果是分表实体，自动根据每条数据的日期路由
            if (IsSplitTableEntity())
            {
                foreach (var data in datas)
                {
                    var date = GetSplitFieldValue(data);
                    var tableName = GetSplitTableName(date);
                    var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
                    InsertToTable(conn, actualTableName, data);
                }
            }
            else
            {
                foreach (var data in datas)
                {
                    conn.Insert(data);
                }
            }
        }

        public async Task AddRangeAsync(IEnumerable<T> datas)
        {
            var conn = GetConnection();
            
            // 如果是分表实体，自动根据每条数据的日期路由
            if (IsSplitTableEntity())
            {
                foreach (var data in datas)
                {
                    var date = GetSplitFieldValue(data);
                    var tableName = GetSplitTableName(date);
                    await InsertToTableAsync(conn, tableName, data);
                }
            }
            else
            {
                foreach (var data in datas)
                {
                    await conn.InsertAsync(data);
                }
            }
        }

        /// <summary>
        /// 异步插入到指定表（分表专用）
        /// </summary>
        private async Task InsertToTableAsync(IDbConnection conn, string tableName, T data)
        {
            // 检查并创建表
            var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
            var checkSql = SqlDialectHelper.BuildCheckTableExistsSql(actualTableName, SQLParam.DBName, SQLParam.DBType);
            bool tableExists = conn.ExecuteScalar<int?>(checkSql) != null;
            if (!tableExists) conn.Execute(GenerateCreateTableSql(actualTableName));
            
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();
            
            // 使用SqlNamingHelper统一处理列名
            var columns = SqlNamingHelper.FormatColumnList(
                properties.Select(p => p.Name).ToArray(), 
                SQLParam.DBType);
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
            
            await conn.ExecuteAsync(sql, data);
        }

        public IEnumerable<T> Query()
        {
            var conn = GetConnection();
            
            // 如果是分表实体，查询所有分表
            if (IsSplitTableEntity())
            {
                var allData = new List<T>();
                var tables = GetSplitTables();
                
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var data = conn.Query<T>($"SELECT * FROM {actualTableName}");
                    allData.AddRange(data);
                }
                
                return allData;
            }
            
            return conn.GetAll<T>();
        }

        public async Task<IEnumerable<T>> QueryAsync()
        {
            var conn = GetConnection();
            
            // 如果是分表实体，查询所有分表
            if (IsSplitTableEntity())
            {
                var allData = new List<T>();
                var tables = GetSplitTables();
                
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var data = await conn.QueryAsync<T>($"SELECT * FROM {actualTableName}");
                    allData.AddRange(data);
                }
                
                return allData;
            }
            
            return await conn.GetAllAsync<T>();
        }

        public IEnumerable<T> Query(Expression<Func<T, bool>> predicate)
        {
            var conn = GetConnection();
            
            // 分表实体：查询所有分表后在内存中过滤
            if (IsSplitTableEntity())
            {
                var allData = new List<T>();
                var tables = GetSplitTables();
                
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var sql = $"SELECT * FROM {actualTableName}";
                    var data = conn.Query<T>(sql);
                    allData.AddRange(data);
                }
                
                return allData.AsEnumerable().Where(predicate.Compile());
            }
            
            return conn.Select(predicate);
        }

        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            var conn = GetConnection();
            
            // 分表实体：查询所有分表后在内存中过滤
            if (IsSplitTableEntity())
            {
                var allData = new List<T>();
                var tables = GetSplitTables();
                
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var sql = $"SELECT * FROM {actualTableName}";
                    var data = await conn.QueryAsync<T>(sql);
                    allData.AddRange(data);
                }
                
                return allData.AsEnumerable().Where(predicate.Compile());
            }
            
            return await conn.SelectAsync(predicate);
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        public void Update(T data)
        {
            var conn = GetConnection();
            
            // 分表实体需要特殊处理
            if (IsSplitTableEntity())
            {
                var date = GetSplitFieldValue(data);
                var tableName = GetSplitTableName(date);
                UpdateInSplitTable(conn, tableName, data);
            }
            else
            {
                conn.Update(data);
            }
        }

        public void Update(Expression<Func<T, bool>> predicate, T updatedata)
        {
            var conn = GetConnection();
            
            if (IsSplitTableEntity())
            {
                var tables = GetSplitTables();
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var sql = $"SELECT * FROM {actualTableName}";
                    var allData = conn.Query<T>(sql).ToList();
                    var entities = allData.AsEnumerable().Where(predicate.Compile()).ToList();
                    
                    foreach (var entity in entities)
                    {
                        foreach (var prop in typeof(T).GetProperties())
                        {
                            var newValue = prop.GetValue(updatedata);
                            if (newValue != null)
                            {
                                prop.SetValue(entity, newValue);
                            }
                        }
                        UpdateInSplitTable(conn, table, entity);
                    }
                }
            }
            else
            {
                var entities = conn.Select(predicate).ToList();
                foreach (var entity in entities)
                {
                    foreach (var prop in typeof(T).GetProperties())
                    {
                        var newValue = prop.GetValue(updatedata);
                        if (newValue != null)
                        {
                            prop.SetValue(entity, newValue);
                        }
                    }
                    conn.Update(entity);
                }
            }
        }

        public async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata)
        {
            var conn = GetConnection();
            
            if (IsSplitTableEntity())
            {
                var tables = GetSplitTables();
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var sql = $"SELECT * FROM {actualTableName}";
                    var allData = await conn.QueryAsync<T>(sql);
                    var entities = allData.AsEnumerable().Where(predicate.Compile()).ToList();
                    
                    foreach (var entity in entities)
                    {
                        foreach (var prop in typeof(T).GetProperties())
                        {
                            var newValue = prop.GetValue(updatedata);
                            if (newValue != null)
                            {
                                prop.SetValue(entity, newValue);
                            }
                        }
                        await UpdateInSplitTableAsync(conn, table, entity);
                    }
                }
            }
            else
            {
                var entities = (await conn.SelectAsync(predicate)).ToList();
                foreach (var entity in entities)
                {
                    foreach (var prop in typeof(T).GetProperties())
                    {
                        var newValue = prop.GetValue(updatedata);
                        if (newValue != null)
                        {
                            prop.SetValue(entity, newValue);
                        }
                    }
                    await conn.UpdateAsync(entity);
                }
            }
        }

        public void Delete(Expression<Func<T, bool>> predicate)
        {
            var conn = GetConnection();
            
            if (IsSplitTableEntity())
            {
                var tables = GetSplitTables();
                foreach (var table in tables)
                {
                    var actualTableName = SqlNamingHelper.ConvertTableName(table, SQLParam.DBType);
                    var sql = $"SELECT * FROM {actualTableName}";
                    var allData = conn.Query<T>(sql).ToList();
                    var entities = allData.AsEnumerable().Where(predicate.Compile()).ToList();
                    
                    foreach (var entity in entities)
                    {
                        DeleteFromSplitTable(conn, table, entity);
                    }
                }
            }
            else
            {
                var entities = conn.Select(predicate).ToList();
                foreach (var entity in entities)
                {
                    conn.Delete(entity);
                }
            }
        }

        public async Task DeleteAsync(Expression<Func<T, bool>> predicate)
        {
            var conn = GetConnection();
            var entities = (await conn.SelectAsync(predicate)).ToList();
            foreach (var entity in entities)
            {
                await conn.DeleteAsync(entity);
            }
        }

        public void DeleteAll()
        {
            Delete(x => true);
        }

        public async Task DeleteAllAsync()
        {
            var conn = GetConnection();
            var sql = $"DELETE FROM {typeof(T).Name}";
            await conn.ExecuteAsync(sql);
        }

        public void Dispose()
        {
            DisConnect();
        }

        #region 分表支持（类似SqlSugar，全自动路由）

        /// <summary>
        /// 检查是否为分表实体
        /// </summary>
        private bool IsSplitTableEntity()
        {
            return typeof(T).GetCustomAttributes(typeof(DapperSplitTableAttribute), false).Any();
        }

        /// <summary>
        /// 获取分表字段值
        /// </summary>
        private DateTime GetSplitFieldValue(T entity)
        {
            var properties = typeof(T).GetProperties();
            
            // 优先查找标记了[DapperSplitField]的字段
            foreach (var prop in properties)
            {
                if (prop.GetCustomAttributes(typeof(DapperSplitFieldAttribute), false).Any())
                {
                    var value = prop.GetValue(entity);
                    if (value is DateTime date)
                        return date;
                }
            }
            
            // 如果没有标记，自动查找第一个DateTime类型的字段
            foreach (var prop in properties)
            {
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (propType == typeof(DateTime) && prop.CanRead && prop.CanWrite)
                {
                    var value = prop.GetValue(entity);
                    if (value is DateTime date)
                        return date;
                }
            }
            
            throw new InvalidOperationException($"实体 {typeof(T).Name} 中未找到DateTime类型的字段");
        }

        /// <summary>
        /// 获取分表名（带后缀）
        /// 类似SqlSugar的自动表名生成
        /// </summary>
        private string GetSplitTableName(DateTime date)
        {
            // 使用类名作为基础表名
            var baseTableName = typeof(T).Name;
            var splitAttr = typeof(T).GetCustomAttribute<DapperSplitTableAttribute>();

            if (splitAttr == null)
                return baseTableName;

            var suffix = splitAttr.SplitType switch
            {
                SplitType.Day => date.ToString("yyyyMMdd"),
                SplitType.Month => date.ToString("yyyyMM"),
                SplitType.Year => date.ToString("yyyy"),
                _ => throw new NotSupportedException($"不支持的分表类型: {splitAttr.SplitType}")
            };

            return $"{baseTableName}_{suffix}";
        }

        /// <summary>
        /// 获取所有分表名
        /// </summary>
        public List<string> GetSplitTables()
        {
            var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
            var baseTableName = tableAttr?.Name ?? typeof(T).Name;
            var convertedName = SqlNamingHelper.ConvertTableName(baseTableName, SQLParam.DBType);
            
            var sql = SQLParam.DBType switch
            {
                ESQLType.PostgreSQL => $"SELECT table_name FROM information_schema.tables WHERE table_name LIKE '{convertedName}_%'",
                ESQLType.MySQL => $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{SQLParam.DBName}' AND table_name LIKE '{convertedName}_%'",
                _ => throw new NotSupportedException($"不支持的数据库类型: {SQLParam.DBType}")
            };
            
            var conn = GetConnection();
            return conn.Query<string>(sql).ToList();
        }

        /// <summary>
        /// 在分表中删除数据
        /// </summary>
        private void DeleteFromSplitTable(IDbConnection conn, string tableName, T data)
        {
            var keyProperty = typeof(T).GetProperties()
                .FirstOrDefault(p => p.GetCustomAttributes(typeof(KeyAttribute), false).Any());
            
            if (keyProperty == null)
            {
                throw new InvalidOperationException("实体必须包含主键字段");
            }
            
            var actualTableName = SqlNamingHelper.FormatTableName(tableName, SQLParam.DBType);
            var whereClause = $"{SqlNamingHelper.ConvertColumnName(keyProperty.Name, SQLParam.DBType)} = @{keyProperty.Name}";
            var sql = $"DELETE FROM {actualTableName} WHERE {whereClause}";
            
            conn.Execute(sql, data);
        }

        #endregion
    }
}
