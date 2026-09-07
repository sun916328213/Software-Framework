using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using Prism.DryIoc;
using Prism.Ioc;

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

        // 必须为 public：EFCoreHelper 通过 Reflection.Emit 生成派生类型，
        // 私有嵌套类型作为泛型参数会导致 CreateType 可见性校验失败
        public class WorkbenchEntity
        {
            public long Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void Create_Dapper_ReturnsDapperHelper()
        {
            var container = new DryIocContainerExtension();
            var manager = new SQLManager(container);

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.IsType<DapperHelper<WorkbenchEntity>>(sql);
            Assert.Equal("db-Dapper", sql.SQLParam.Name);
        }

        [Fact]
        public void Create_EFCore_ReturnsEFCoreHelper()
        {
            var container = new DryIocContainerExtension();
            var manager = new SQLManager(container);

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.EFCore));

            // EFCoreHelper 通过 Reflection.Emit 生成动态子类实例
            Assert.IsAssignableFrom<EFCoreHelper<WorkbenchEntity>>(sql);
        }

        [Fact]
        public void Create_SqlSugar_ReturnsSqlSugarHelper()
        {
            var container = new DryIocContainerExtension();
            var manager = new SQLManager(container);

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.SqlSugar));

            Assert.IsType<SqlSugarHelper<WorkbenchEntity>>(sql);
        }

        [Fact]
        public void Create_RegistersInstanceInContainer()
        {
            var container = new DryIocContainerExtension();
            var manager = new SQLManager(container);

            var sql = manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));
            var resolved = container.Resolve<ISQL<WorkbenchEntity>>();

            Assert.Same(sql, resolved);
        }

        [Fact]
        public void Create_AddsToManagerAndSupportsNameIndexer()
        {
            var container = new DryIocContainerExtension();
            var manager = new SQLManager(container);

            manager.Create<WorkbenchEntity>(CreateParam(ESQLToolType.Dapper));

            Assert.Equal(1, manager.Count);
            Assert.NotNull(manager["db-Dapper"]);
        }

        [Fact]
        public void Create_BaseMethod_ThrowsNotSupported()
        {
            var container = new DryIocContainerExtension();
            var manager = new SQLManager(container);

            Assert.Throws<NotSupportedException>(() => manager.Create(CreateParam(ESQLToolType.Dapper)));
        }

        [Fact]
        public void SQLParam_HasDefaults()
        {
            var param = new SQLParam();
            Assert.Equal(ESQLType.PostgreSQL, param.DBType);
            Assert.Equal(ESQLToolType.EFCore, param.ToolType);
        }
    }
}
