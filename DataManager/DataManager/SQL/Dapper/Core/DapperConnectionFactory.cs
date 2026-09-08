using DataManager.SQL.Definition;
using System.Data;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// Dapper 连接工厂（P0-4 拆分产物）。
    /// 所有连接均为短连接：每次 CRUD 新建 + Open + using 释放，避免单连接缓存的多线程冲突与泄漏。
    /// </summary>
    public sealed class DapperConnectionFactory
    {
        private readonly SQLParam _param;

        public DapperConnectionFactory(SQLParam param)
        {
            _param = param;
        }

        public IDbConnection CreateOpen()
        {
            var cs = SqlDialectHelper.BuildConnectionString(_param);
            var conn = SqlDialectHelper.CreateConnection(_param.DBType, cs);
            conn.Open();
            return conn;
        }

        public void EnsureDatabase()
        {
            if (!SqlDialectHelper.IsDatabaseExists(_param))
                SqlDialectHelper.CreateDatabase(_param);
        }
    }
}
