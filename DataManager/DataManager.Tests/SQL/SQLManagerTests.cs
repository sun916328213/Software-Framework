using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// SQLManager 工厂逻辑测试（不连接真实数据库）
    /// </summary>
    public class SQLManagerTests
    {
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

        public class WorkbenchEntity
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        public void Create_Dapper_ReturnsDapperHelper()
        {
            var manager = new SQLManager();

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.IsType<DapperHelper<WorkbenchEntity>>(sql);
            Assert.Equal("db-Dapper", sql.SQLParam.Name);
        }

        [Fact]
        public void Create_EFCore_ReturnsEFCoreHelper()
        {
            var manager = new SQLManager();

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.EFCore));

            Assert.IsType<EFCoreHelper<WorkbenchEntity>>(sql);
        }

        [Fact]
        public void Create_SqlSugar_ReturnsSqlSugarHelper()
        {
            var manager = new SQLManager();

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.SqlSugar));

            Assert.IsType<SqlSugarHelper<WorkbenchEntity>>(sql);
        }

        [Fact]
        public void Create_InvokesOnCreatedCallback()
        {
            var manager = new SQLManager();
            object? notified = null;

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper), onCreated: x => notified = x);

            Assert.Same(sql, notified);
        }

        [Fact]
        public void Create_CtorCallback_Invoked()
        {
            object? notified = null;
            var manager = new SQLManager(onCreated: x => notified = x);

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.Same(sql, notified);
        }

        [Fact]
        public void Create_AddsToManagerAndSupportsNameIndexer()
        {
            var manager = new SQLManager();

            manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.Equal(1, manager.Count);
            Assert.NotNull(manager["db-Dapper"]);
        }

        [Fact]
        public void Create_BaseMethod_ThrowsNotSupported()
        {
            var manager = new SQLManager();

            Assert.Throws<NotSupportedException>(() => manager.Create(CreateParam(ESQLToolType.Dapper)));
        }

        [Fact]
        public void SQLParam_HasDefaults()
        {
            var param = new SQLParam();
            Assert.Equal(ESQLType.PostgreSQL, param.DBType);
            Assert.Equal(ESQLToolType.EFCore, param.ToolType);
        }

        [Fact]
        public void Create_MissingServerIP_ThrowsArgumentException()
        {
            var manager = new SQLManager();
            var param = CreateParam(ESQLToolType.Dapper);
            param.ServerIP = string.Empty;

            Assert.Throws<ArgumentException>(() => manager.Create<WorkbenchEntity>(param));
        }

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
