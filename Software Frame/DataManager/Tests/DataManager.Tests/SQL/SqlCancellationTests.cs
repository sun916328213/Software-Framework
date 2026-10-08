// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlCancellationTests、CancelRow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、System.ComponentModel.DataAnnotations。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P9-1 / P9-3：取消契约。
    /// 约定：取消一律向上抛 <see cref="OperationCanceledException"/>（不吞掉、不返回空集合/false）；
    /// 事务内取消与异常一致——整体回滚，不允许半提交；
    /// 取消后实例状态必须干净（不能残留未提交事务导致后续操作全失败）。
    /// </summary>
    public class SqlCancellationTests : IDisposable
    {
        /// <summary>
        /// CancelRow 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class CancelRow
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_p9_cancel_{Guid.NewGuid():N}.db");

        /// <summary>
        /// 测试相关入口 ParamFor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewGuid；具体条件和先后次序见方法体。
        /// </summary>
        private SQLParam ParamFor(ESQLToolType tool) => new()
        {
            Name = $"p9-cancel-{tool}-{Guid.NewGuid():N}",
            FilePath = _file,
            DBName = _file,
            DBType = ESQLType.SQLite,
            ToolType = tool,
        };

        /// <summary>
        /// 测试相关入口 Open；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect；具体条件和先后次序见方法体。
        /// </summary>
        private ISQL<CancelRow> Open(ESQLToolType tool)
        {
            var db = new SQLManager().Create<CancelRow>(ParamFor(tool));
            db.Connect();
            return db;
        }

        /// <summary>
        /// 预取消 Token：所有异步入口立即终止，且不产生任何写入。
        /// 同时验证取消没有污染实例状态——随后不取消的操作必须照常成功
        /// （若事务标记 / 环境事务残留，这里会立刻失败）。
        /// </summary>
        [Theory]
        [InlineData(ESQLToolType.Dapper)]
        [InlineData(ESQLToolType.EFCore)]
        [InlineData(ESQLToolType.SqlSugar)]
        public async Task PreCancelledToken_ThrowsImmediately_AndLeavesNoSideEffect(ESQLToolType tool)
        {
            var db = Open(tool);
            try
            {
                await db.AddAsync(new CancelRow { Id = 1, Name = "seed", Age = 1 });

                var cancelled = new CancellationToken(canceled: true);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.QueryAsync(cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.QueryAsync(x => x.Age > 0, cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.AddAsync(new CancelRow { Id = 2, Name = "x", Age = 2 }, cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.AddRangeAsync(new[] { new CancelRow { Id = 3, Name = "y", Age = 3 } }, cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.UpdateAsync(new CancelRow { Id = 1, Name = "z", Age = 9 }, cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.UpdateAsync(x => x.Id == 1, new CancelRow { Name = "z2", Age = 9 }, cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.DeleteAsync(x => x.Id == 1, cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.DeleteAllAsync(cancelled));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.ExecuteInTransactionAsync(async () => { }, cancelled));

                // 无写入副作用：仍只有 seed 一行且值未变。
                var rows = (await db.QueryAsync()).ToList();
                Assert.Single(rows);
                Assert.Equal("seed", rows[0].Name);
                Assert.Equal(1, rows[0].Age);

                // 实例状态未被取消污染：后续正常调用必须成功。
                await db.AddAsync(new CancelRow { Id = 5, Name = "after", Age = 5 });
                Assert.Equal(2, (await db.QueryAsync()).Count());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 事务体内写入成功、提交前取消：必须整体回滚，不允许半提交。
        /// </summary>
        [Theory]
        [InlineData(ESQLToolType.Dapper)]
        [InlineData(ESQLToolType.EFCore)]
        [InlineData(ESQLToolType.SqlSugar)]
        public async Task Transaction_CancelBeforeCommit_RollsBackEverything(ESQLToolType tool)
        {
            var db = Open(tool);
            try
            {
                using var cts = new CancellationTokenSource();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    db.ExecuteInTransactionAsync(async () =>
                    {
                        await db.AddAsync(new CancelRow { Id = 7, Name = "inside", Age = 7 }, cts.Token);
                        cts.Cancel(); // 写入已发生，提交前取消
                    }, cts.Token));

                Assert.Empty(await db.QueryAsync());

                // 回滚后实例仍可用。
                await db.AddAsync(new CancelRow { Id = 8, Name = "recovered", Age = 8 });
                Assert.Single(await db.QueryAsync());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 事务体内某一步操作因取消抛 OCE：先前已成功的写入必须一并回滚。
        /// </summary>
        [Theory]
        [InlineData(ESQLToolType.Dapper)]
        [InlineData(ESQLToolType.EFCore)]
        [InlineData(ESQLToolType.SqlSugar)]
        public async Task Transaction_InnerOperationCancelled_RollsBackEarlierWrites(ESQLToolType tool)
        {
            var db = Open(tool);
            try
            {
                using var cts = new CancellationTokenSource();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    db.ExecuteInTransactionAsync(async () =>
                    {
                        await db.AddAsync(new CancelRow { Id = 11, Name = "first", Age = 11 }, CancellationToken.None);
                        cts.Cancel();
                        await db.AddAsync(new CancelRow { Id = 12, Name = "second", Age = 12 }, cts.Token);
                    }, CancellationToken.None));

                Assert.Empty(await db.QueryAsync());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 闸门第 3 条：取消不得被吞掉后返回空集合/默认值。
        /// 用「已有数据 + 预取消 Token」证明抛的是异常而不是空结果。
        /// </summary>
        [Theory]
        [InlineData(ESQLToolType.Dapper)]
        [InlineData(ESQLToolType.EFCore)]
        [InlineData(ESQLToolType.SqlSugar)]
        public async Task Cancellation_IsNotSwallowedAsEmptyResult(ESQLToolType tool)
        {
            var db = Open(tool);
            try
            {
                await db.AddAsync(new CancelRow { Id = 1, Name = "keep", Age = 1 });
                var cancelled = new CancellationToken(canceled: true);

                // 若实现把取消吞成空集合，这里不会抛异常 —— 断言必须是异常。
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.QueryAsync(cancelled));

                // 数据仍在（未被误删/误清空）。
                Assert.Single(await db.QueryAsync());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
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
