// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DapperConnectionFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.Data;

namespace DataManager.Runtime.SQL.Dapper
{
    /// <summary>
    /// Dapper 连接工厂（P0-4 拆分产物）。
    /// 所有连接均为短连接：每次 CRUD 新建 + Open + using 释放，避免单连接缓存的多线程冲突与泄漏。
    /// </summary>
    public sealed class DapperConnectionFactory
    {
        private readonly SQLParam _param;

        /// <summary>
        /// 初始化 DapperConnectionFactory 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DapperConnectionFactory(SQLParam param)
        {
            _param = param;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateOpen。
        /// 正文中的关键协作调用包括 BuildConnectionString、CreateConnection、Open；具体条件和先后次序见方法体。
        /// </summary>
        public IDbConnection CreateOpen()
        {
            var cs = SqlDialectHelper.BuildConnectionString(_param);
            var conn = SqlDialectHelper.CreateConnection(_param.DBType, cs);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDatabase。
        /// 正文中的关键协作调用包括 EnsureDatabase、Get；具体条件和先后次序见方法体。
        /// </summary>
        public void EnsureDatabase()
        {
            // P3-2：下沉到方言。SQLite=建目录；PG/MySQL/SQLServer=建库。
            SqlDialectRegistry.Get(_param.DBType).EnsureDatabase(_param);
        }
    }
}
