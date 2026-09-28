using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugarSQL;
using Microsoft.EntityFrameworkCore;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DapperSplitType = DataManager.SQL.Dapper.SplitType;

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
        public class TimeoutRow
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

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

        [Fact]
        public void CommandTimeoutOrNull_MapsZeroToNull_AndPositiveToSeconds()
        {
            var p = new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" };
            p.CommandTimeoutSeconds = 7;
            Assert.Equal(7, p.CommandTimeoutOrNull);
            p.CommandTimeoutSeconds = 0;
            Assert.Null(p.CommandTimeoutOrNull);
        }

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

        public void Dispose()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
