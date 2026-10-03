// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SQLManagerTests、WorkbenchEntity。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using DataManager.Runtime.SQL.SqlSugar;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// SQLManager 工厂逻辑测试（不连接真实数据库）
    /// </summary>
    public class SQLManagerTests
    {
        /// <summary>
        /// 验证 CreateParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private static SQLParam CreateParam(ESQLToolType toolType) => new()
        {
            Name = $"db-{toolType}",
            ServerIP = "127.0.0.1",
            Port = "5432",
            UserName = "postgres",
            Password = "postgres",
            DBName = "workbench_test",
            DBType = ESQLType.PostgreSQL,
            ToolType = toolType,
        };

        /// <summary>
        /// WorkbenchEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class WorkbenchEntity
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// 验证 Create_Dapper_ReturnsDapperHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、IsType、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_Dapper_ReturnsDapperHelper()
        {
            var manager = new SQLManager();

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.IsType<DapperHelper<WorkbenchEntity>>(sql);
            Assert.Equal("db-Dapper", sql.SQLParam.Name);
        }

        /// <summary>
        /// 验证 Create_EFCore_ReturnsEFCoreHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、IsType；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_EFCore_ReturnsEFCoreHelper()
        {
            var manager = new SQLManager();

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.EFCore));

            Assert.IsType<EFCoreHelper<WorkbenchEntity>>(sql);
        }

        /// <summary>
        /// 验证 Create_SqlSugar_ReturnsSqlSugarHelper 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、IsType；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_SqlSugar_ReturnsSqlSugarHelper()
        {
            var manager = new SQLManager();

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.SqlSugar));

            Assert.IsType<SqlSugarHelper<WorkbenchEntity>>(sql);
        }

        /// <summary>
        /// 验证 Create_InvokesOnCreatedCallback 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Same；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_InvokesOnCreatedCallback()
        {
            var manager = new SQLManager();
            object? notified = null;

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper), onCreated: x => notified = x);

            Assert.Same(sql, notified);
        }

        /// <summary>
        /// 验证 Create_CtorCallback_Invoked 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Same；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_CtorCallback_Invoked()
        {
            object? notified = null;
            var manager = new SQLManager(onCreated: x => notified = x);

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.Same(sql, notified);
        }

        /// <summary>
        /// 验证 Create_AddsToManagerAndSupportsNameIndexer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Equal、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_AddsToManagerAndSupportsNameIndexer()
        {
            var manager = new SQLManager();

            manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.Equal(1, manager.Count);
            Assert.NotNull(manager["db-Dapper"]);
        }

        /// <summary>
        /// 验证 Create_BaseMethod_ThrowsNotSupported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Create、CreateParam；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_BaseMethod_ThrowsNotSupported()
        {
            var manager = new SQLManager();

            Assert.Throws<NotSupportedException>(() => manager.Create(CreateParam(ESQLToolType.Dapper)));
        }

        /// <summary>
        /// 验证 SQLParam_HasDefaults 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SQLParam_HasDefaults()
        {
            var param = new SQLParam();
            Assert.Equal(ESQLType.PostgreSQL, param.DBType);
            Assert.Equal(ESQLToolType.EFCore, param.ToolType);
        }

        /// <summary>
        /// 验证 Create_MissingServerIP_ThrowsArgumentException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CreateParam、Throws、Create；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_MissingServerIP_ThrowsArgumentException()
        {
            var manager = new SQLManager();
            var param = CreateParam(ESQLToolType.Dapper);
            param.ServerIP = string.Empty;

            Assert.Throws<ArgumentException>(() => manager.Create<WorkbenchEntity>(param));
        }

        /// <summary>
        /// 验证 Create_DuplicateName_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、CreateParam、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_DuplicateName_Throws()
        {
            var manager = new SQLManager();
            manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            var dup = CreateParam(ESQLToolType.EFCore);
            dup.Name = "db-Dapper";
            Assert.Throws<InvalidOperationException>(() => manager.Create<WorkbenchEntity>(dup));
        }
    }
}
