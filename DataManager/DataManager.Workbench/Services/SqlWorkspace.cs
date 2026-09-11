using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.Workbench.Models;

namespace DataManager.Workbench.Services
{
    /// <summary>
    /// SQL 实际流程（工作台新建的承接层，ViewModel 只剩绑定与命令装配）。
    /// 职责：连接实例生命周期（指纹重建）、测试数据生成、全部数据操作；
    /// 不碰 UI：计时/日志/忙指示/结果集全部由 ViewModel 的 Run 系列包装完成。
    /// 输入即 DataManager 原生 SQLParam（可观察模型，表格直绑，无中转层）。
    /// </summary>
    public sealed class SqlWorkspace
    {
        private readonly SQLManager _sqlManager;
        private readonly Random _random = new();
        private ISQL<TestEntity>? _db;
        private string? _currentKey;
        private SQLParam? _currentParam;

        public SqlWorkspace(SQLManager sqlManager)
        {
            _sqlManager = sqlManager;
        }

        /// <summary>
        /// 连接指纹：库类型 + 工具类型 + 分字段全文参与，任一改动（含密码纠错）都重建实例。
        /// 曾用“工具+库类型”做键，导致密码改对后仍复用旧实例，一直报密码错误。
        /// P3-1：纳入学 FilePath/ConnectionString/TrustServerCertificate，
        /// 否则 SQLite 换文件、直通串变更不触发重建（同款旧 bug）。
        /// </summary>
        public static string BuildDbKey(SQLParam param) =>
            $"{param.ToolType}:{param.DBType}:{param.ServerIP}:{param.Port}:{param.UserName}:{param.Password}:{param.DBName}:{param.FilePath}:{param.ConnectionString}:{param.TrustServerCertificate}";

        /// <summary>按参数确保连接实例（指纹命中复用，否则断开旧实例重建）。</summary>
        public ISQL<TestEntity> EnsureDb(SQLParam param)
        {
            ArgumentNullException.ThrowIfNull(param);
            var key = BuildDbKey(param);
            if (_db != null && _currentKey == key)
                return _db;

            if (_db != null)
            {
                try { _db.DisConnect(); } catch { }
                _sqlManager.Remove(_db);
            }

            _db = _sqlManager.Create<TestEntity>(param);
            _currentKey = key;
            _currentParam = param;
            return _db;
        }

        public void Disconnect()
        {
            _db?.DisConnect();
        }

        public async Task TestConnectionAsync(SQLParam param)
        {
            var db = EnsureDb(param);
            await Task.Run(() => db.Connect());
            await Task.Run(() => db.DisConnect());
        }

        public TestEntity NewEntity() => new()
        {
            Name = $"测试{DateTime.Now:HHmmss}",
            Age = _random.Next(18, 60),
            CreateTime = DateTime.Now,
        };

        public void AddNew() => EnsureCurrent().Add(NewEntity());

        public void AddNewBatch(int count) =>
            EnsureCurrent().AddRange(Enumerable.Range(1, count).Select(_ => NewEntity()));

        public async Task<IReadOnlyList<TestEntity>> QueryAllAsync() =>
            (await EnsureCurrent().QueryAsync()).ToList();

        public async Task<IReadOnlyList<TestEntity>> QueryAdultsAsync() =>
            (await EnsureCurrent().QueryAsync(x => x.Age > 20)).ToList();

        public void UpdateSelected(TestEntity selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            EnsureCurrent().Update(x => x.Id == selected.Id, new TestEntity
            {
                Name = selected.Name + "_改",
                Age = selected.Age + 1,
                CreateTime = DateTime.Now,
            });
        }

        public void DeleteSelected(TestEntity selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            EnsureCurrent().Delete(x => x.Id == selected.Id);
        }

        public void DeleteAll() => EnsureCurrent().DeleteAll();

        /// <summary>分表名查询（Dapper / EFCore / SqlSugar；其他抛业务异常由 ViewModel 记日志）。</summary>
        public IReadOnlyList<string> GetSplitTables(ISQL<TestEntity> db)
        {
            if (db is SQL.Dapper.DapperBase<TestEntity> dapper)
                return dapper.GetSplitTables().ToList();
            if (db is SQL.EFCore.EFCoreBase<TestEntity> ef)
                return ef.GetSplitTables().ToList();
            if (db is SQL.SqlSugarSQL.SqlSugarBase<TestEntity> sugar)
                return sugar.GetSplitTables().ToList();
            throw new NotSupportedException("当前实例不支持分表名查询");
        }

        /// <summary>
        /// 当前实例（每次操作都过指纹：档案改了自动重建，与原来每个命令先 EnsureDb 一致）。
        /// </summary>
        private ISQL<TestEntity> EnsureCurrent() =>
            _currentParam is null
                ? throw new InvalidOperationException("请先连接数据库")
                : EnsureDb(_currentParam);
    }
}
