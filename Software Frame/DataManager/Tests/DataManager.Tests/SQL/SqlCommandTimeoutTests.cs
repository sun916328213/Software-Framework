// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlCommandTimeoutTests、TimeoutRow、TimeoutSplitRow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using DataManager.Runtime.SQL.SqlSugarSQL;
using Microsoft.EntityFrameworkCore;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DapperSplitType = DataManager.Runtime.SQL.Dapper.SplitType;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P9-4：统一命令超时（<see cref="SQLParam.CommandTimeoutSeconds"/>）。
    /// 约定：默认 0 = 保持 provider 默认行为（升级后不改现场超时）；单位统一为秒；
    /// 只控制命令执行，不改 ConnectionString 直通串里的 Connect Timeout。
    /// 三 provider 的映射各自取「可观测的生效值」做实证。
    /// </summary>
    public class SqlCommandTimeoutTests : IDisposable
    {
        /// <summary>
        /// TimeoutRow 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class TimeoutRow
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// TimeoutSplitRow 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Day)]
        public class TimeoutSplitRow
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_p9_timeout_{Guid.NewGuid():N}.db");

        /// <summary>
        /// 验证 ParamFor 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 NewGuid；具体条件和先后次序见方法体。
        /// </summary>
        private SQLParam ParamFor(ESQLToolType tool, int timeoutSeconds = 0) => new()
        {
            Name = $"p9-timeout-{tool}-{Guid.NewGuid():N}",
            FilePath = _file,
            DBName = _file,
            DBType = ESQLType.SQLite,
            ToolType = tool,
            CommandTimeoutSeconds = timeoutSeconds,
        };

        #region 配置层：默认值与校验

        [Fact]
        public void CommandTimeoutSeconds_DefaultsToZero_MeaningProviderDefault()
        {
            var p = new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" };
            Assert.Equal(0, p.CommandTimeoutSeconds);
            Assert.Null(p.CommandTimeoutOrNull);
        }

        /// <summary>
        /// 验证 CommandTimeoutOrNull_MapsZeroToNull_AndPositiveToSeconds 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CommandTimeoutOrNull_MapsZeroToNull_AndPositiveToSeconds()
        {
            var p = new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" };
            p.CommandTimeoutSeconds = 7;
            Assert.Equal(7, p.CommandTimeoutOrNull);
            p.CommandTimeoutSeconds = 0;
            Assert.Null(p.CommandTimeoutOrNull);
        }

        /// <summary>
        /// 验证 Validate_RejectsNegativeTimeout_AllowsZeroAndPositive 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Validate_RejectsNegativeTimeout_AllowsZeroAndPositive()
        {
            var p = new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" };
            p.CommandTimeoutSeconds = -1;
            Assert.Throws<ArgumentException>(() => p.Validate());

            p.CommandTimeoutSeconds = 0;
            p.Validate();

            p.CommandTimeoutSeconds = 30;
            p.Validate();
        }

        #endregion

        #region Dapper：CommandDefinition 映射（命令超时 + 取消标记）

        [Fact]
        public void DapperCommandFactory_MapsTimeoutAndTokenOntoCommandDefinition()
        {
            var p = new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" };
            using var cts = new CancellationTokenSource();

            var off = DapperCommandFactory.Create("SELECT 1", null, null, p, cts.Token);
            Assert.Null(off.CommandTimeout);                       // 0 → provider 默认
            Assert.Equal(cts.Token, off.CancellationToken);        // Token 真正交给 Dapper

            p.CommandTimeoutSeconds = 12;
            var on = DapperCommandFactory.Create("SELECT 1", null, null, p, cts.Token);
            Assert.Equal(12, on.CommandTimeout);
        }

        /// <summary>分表路径全部走 CommandDefinition：带上命令超时后仍能端到端跑通。</summary>
        [Fact]
        public async Task Dapper_SplitTable_WithCommandTimeout_WorksEndToEnd()
        {
            var param = ParamFor(ESQLToolType.Dapper, timeoutSeconds: 5);
            var db = (DapperBase<TimeoutSplitRow>)new SQLManager().Create<TimeoutSplitRow>(param);
            db.Connect();
            try
            {
                await db.AddAsync(new TimeoutSplitRow { Id = 1, Name = "T", CreateTime = DateTime.Today });
                Assert.Single(await db.QueryAsync());
                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally
            {
                db.DisConnect();
            }
        }

        #endregion

        #region EFCore：Database.GetCommandTimeout 生效值

        [Fact]
        public void EFCore_CommandTimeoutSeconds_MapsToContextCommandTimeout()
        {
            var withTimeout = new SQLManager().Create<TimeoutRow>(ParamFor(ESQLToolType.EFCore, 7));
            withTimeout.Connect();
            try
            {
                var ef = (EFCoreBase<TimeoutRow>)withTimeout;
                Assert.Equal(7, ef.Database.GetCommandTimeout());
            }
            finally
            {
                withTimeout.DisConnect();
            }

            var defaulted = new SQLManager().Create<TimeoutRow>(ParamFor(ESQLToolType.EFCore, 0));
            defaulted.Connect();
            try
            {
                var ef = (EFCoreBase<TimeoutRow>)defaulted;
                Assert.NotEqual(7, ef.Database.GetCommandTimeout()); // 0 → 不覆盖 provider 默认
            }
            finally
            {
                defaulted.DisConnect();
            }
        }

        #endregion

        #region SqlSugar：Ado.CommandTimeOut 生效值

        [Fact]
        public void SqlSugar_CommandTimeoutSeconds_MapsToAdoCommandTimeOut()
        {
            var withTimeout = new SQLManager().Create<TimeoutRow>(ParamFor(ESQLToolType.SqlSugar, 9));
            withTimeout.Connect();
            try
            {
                Assert.Equal(9, AdoCommandTimeOut((SqlSugarBase<TimeoutRow>)withTimeout));
            }
            finally
            {
                withTimeout.DisConnect();
            }

            var defaulted = new SQLManager().Create<TimeoutRow>(ParamFor(ESQLToolType.SqlSugar, 0));
            defaulted.Connect();
            try
            {
                Assert.NotEqual(9, AdoCommandTimeOut((SqlSugarBase<TimeoutRow>)defaulted)); // 0 → provider 默认
            }
            finally
            {
                defaulted.DisConnect();
            }
        }

        /// <summary>
        /// 读取 SqlSugar 客户端生效的 Ado.CommandTimeOut。
        /// 客户端是 SqlSugarBase 的私有惰性字段（未对外暴露），测试用反射读取真实生效值，
        /// 避免为测试在产品代码上新增公开 API。
        /// </summary>
        private static int AdoCommandTimeOut(SqlSugarBase<TimeoutRow> db)
        {
            var field = typeof(SqlSugarBase<TimeoutRow>)
                .GetField("_dbClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var client = (SqlSugarClient?)field.GetValue(db)
                ?? throw new InvalidOperationException("SqlSugarClient 尚未创建（需先 Connect）。");
            return client.Ado.CommandTimeOut;
        }

        #endregion

        #region 直通连接串不被命令超时改写

        [Fact]
        public void CommandTimeout_DoesNotRewriteConnectionStringPassthrough()
        {
            var cs = $"Data Source={_file};";
            var p = new SQLParam
            {
                Name = $"p9-timeout-cs-{Guid.NewGuid():N}",
                DBType = ESQLType.SQLite,
                ToolType = ESQLToolType.EFCore,
                ConnectionString = cs,
                CommandTimeoutSeconds = 3,
            };

            var db = new SQLManager().Create<TimeoutRow>(p);
            db.Connect();
            try
            {
                // 直通串原样保留（命令超时只作用于命令执行，不动 Connect Timeout）。
                Assert.Equal(cs, db.SQLParam.ConnectionString);
                Assert.Equal(3, ((EFCoreBase<TimeoutRow>)db).Database.GetCommandTimeout());
            }
            finally
            {
                db.DisConnect();
            }
        }

        #endregion

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
