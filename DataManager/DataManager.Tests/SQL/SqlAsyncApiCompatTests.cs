using DataManager.SQL;
using DataManager.SQL.Definition;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P9-1：冻结 ISQL&lt;T&gt; 现有异步 API 的源码兼容性。
    /// P9 只新增 CancellationToken 重载，不删改旧签名；本用例确保旧签名（无 Token）
    /// 在三 provider 上仍可编译并保持原有语义，避免 P9 迫使业务代码批量改动。
    /// 注意：断言一律按业务字段（Name）定位，不依赖显式主键——Dapper/Dommel 视 long 主键为自增，
    /// 插入时忽略显式值（EFCore/SqlSugar 则写入显式值），显式主键断言会引入 provider 差异。
    /// </summary>
    public class SqlAsyncApiCompatTests : IDisposable
    {
        public class CompatRow
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_p9_compat_{Guid.NewGuid():N}.db");

        private SQLParam ParamFor(ESQLToolType tool) => new()
        {
            Name = $"p9-compat-{tool}-{Guid.NewGuid():N}",
            FilePath = _file,
            DBName = _file,
            DBType = ESQLType.SQLite,
            ToolType = tool,
        };

        /// <summary>
        /// 旧签名全量走一遍：Add/AddRange/Query/Query(predicate)/Update/Update(predicate)/
        /// Delete/DeleteAll/ExecuteInTransaction 的异步形态。
        /// </summary>
        [Theory]
        [InlineData(ESQLToolType.Dapper)]
        [InlineData(ESQLToolType.EFCore)]
        [InlineData(ESQLToolType.SqlSugar)]
        public async Task LegacyAsyncSignatures_StillCallable_AndKeepSemantics(ESQLToolType tool)
        {
            var manager = new SQLManager();
            var db = manager.Create<CompatRow>(ParamFor(tool));
            db.Connect();
            try
            {
                // 显式唯一主键：SqlSugar 在 SQLite 下不自增（IsIdentity 不支持），默认值会撞主键；
                // Dapper/Dommel 视 long 主键为自增并忽略该值，EFCore 写入显式值——三路均可。
                // AddAsync
                await db.AddAsync(new CompatRow { Id = 1, Name = "A", Age = 10 });

                // AddRangeAsync
                await db.AddRangeAsync(new[]
                {
                    new CompatRow { Id = 2, Name = "B", Age = 20 },
                    new CompatRow { Id = 3, Name = "C", Age = 30 },
                });

                // QueryAsync()
                Assert.Equal(3, (await db.QueryAsync()).Count());

                // QueryAsync(predicate)
                Assert.Equal(2, (await db.QueryAsync(x => x.Age >= 20)).Count());

                // UpdateAsync(单实体，按主键整行更新)：主键取插入后读回的真实值
                var b = (await db.QueryAsync(x => x.Name == "B")).Single();
                b.Age = 21;
                await db.UpdateAsync(b);
                Assert.Equal(21, (await db.QueryAsync(x => x.Name == "B")).Single().Age);

                // UpdateAsync(predicate, updatedata)
                var c = (await db.QueryAsync(x => x.Name == "C")).Single();
                await db.UpdateAsync(x => x.Id == c.Id, new CompatRow { Name = "C2", Age = 31 });
                var row3 = (await db.QueryAsync(x => x.Id == c.Id)).Single();
                Assert.Equal("C2", row3.Name);
                Assert.Equal(31, row3.Age);

                // DeleteAsync(predicate)
                await db.DeleteAsync(x => x.Name == "A");
                Assert.Empty(await db.QueryAsync(x => x.Name == "A"));

                // ExecuteInTransactionAsync
                await db.ExecuteInTransactionAsync(async () =>
                {
                    await db.AddAsync(new CompatRow { Id = 99, Name = "T", Age = 9 });
                });
                Assert.Single(await db.QueryAsync(x => x.Name == "T"));

                // DeleteAllAsync
                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// P9-1：旧同步签名同样冻结（P9 不动同步路径）。
        /// </summary>
        [Fact]
        public void LegacySyncSignatures_StillCallable()
        {
            var manager = new SQLManager();
            var db = manager.Create<CompatRow>(ParamFor(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                db.Add(new CompatRow { Name = "S1", Age = 1 });
                db.AddRange(new[] { new CompatRow { Name = "S2", Age = 2 } });
                Assert.Equal(2, db.Query().Count());
                Assert.Single(db.Query(x => x.Name == "S2"));

                var s2 = db.Query(x => x.Name == "S2").Single();
                db.Update(new CompatRow { Id = s2.Id, Name = "S2b", Age = 3 });
                Assert.Equal(3, db.Query(x => x.Name == "S2b").Single().Age);

                db.Update(x => x.Name == "S2b", new CompatRow { Name = "S2c", Age = 4 });
                Assert.Equal(4, db.Query(x => x.Name == "S2c").Single().Age);

                db.ExecuteInTransaction(() => db.Add(new CompatRow { Name = "S5", Age = 5 }));
                Assert.Single(db.Query(x => x.Name == "S5"));

                db.Delete(x => x.Name == "S5");
                Assert.Empty(db.Query(x => x.Name == "S5"));

                db.DeleteAll();
                Assert.Empty(db.Query());
            }
            finally
            {
                db.DisConnect();
            }
        }

        public void Dispose()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
