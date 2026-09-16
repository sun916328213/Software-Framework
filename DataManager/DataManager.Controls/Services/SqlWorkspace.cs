using DataManager.SQL;
using Extension.Core.ControlEx;
using Extension.Core.ConvertEx;
using DataManager.SQL.Definition;
using System.Collections.ObjectModel;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// SQL 实际流程（对标 WPFControl 的 DataQueryService：持有 <see cref="Models.SqlPageModel{T}"/> 并读写它，
    /// ViewModel 只暴露 Model + 装配命令）。
    /// 职责：Model 初始化（读 Config 文件 → 默认）、连接实例生命周期（指纹重建）、
    /// 测试数据生成、全部数据操作（含结果集回填）。
    /// 行实体由宿主定义（实现 <see cref="Models.ISqlEntity"/>），ORM 映射随实体走。
    /// </summary>
    public sealed class SqlWorkspace<T>
        where T : class, Models.ISqlEntity, new()
    {
        private readonly SQLManager _sqlManager;
        private ISQL<T>? _db;
        private string? _currentKey;
        private SQLParam? _currentParam;

        /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
        public Models.SqlPageModel<T> Model { get; } = new();

        public SqlWorkspace(SQLManager sqlManager)
        {
            _sqlManager = sqlManager;
            LoadProfiles();
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
        public ISQL<T> EnsureDb(SQLParam param)
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

            _db = _sqlManager.Create<T>(param);
            _currentKey = key;
            _currentParam = param;
            return _db;
        }

        public async Task ConnectAsync()
        {
            var db = EnsureDb(Model.SelectedProfile());
            await Task.Run(() => db.Connect());
            DispatcherEx.UIInvokeSafe(() =>
            {
                Model.IsConnected = true;
                Model.StatusText = "已连接";
            });
        }

        public void Disconnect()
        {
            _db?.DisConnect();
            Model.IsConnected = false;
            Model.StatusText = "未连接";
        }

        public async Task TestConnectionAsync()
        {
            var db = EnsureDb(Model.SelectedProfile());
            await Task.Run(() => db.Connect());
            await Task.Run(() => db.DisConnect());
        }

        private T NewEntity() => new()
        {
            Name = $"测试{DateTime.Now:HHmmss}",
            CreateTime = DateTime.Now,
        };

        public void AddNew() => EnsureCurrent().Add(NewEntity());

        public void AddNewBatch(int count) =>
            EnsureCurrent().AddRange(Enumerable.Range(1, count).Select(_ => NewEntity()));

        public async Task QueryAllAsync()
        {
            var data = (await EnsureCurrent().QueryAsync()).ToList();
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(data));
        }

        public void UpdateSelected(T selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            EnsureCurrent().Update(x => x.Id == selected.Id, new T
            {
                Name = selected.Name + "_改",
                CreateTime = DateTime.Now,
            });
        }

        public void DeleteSelected(T selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            EnsureCurrent().Delete(x => x.Id == selected.Id);
        }

        public void DeleteAll()
        {
            EnsureCurrent().DeleteAll();
            DispatcherEx.UIInvokeSafe(() => Model.Results.Clear());
        }

        /// <summary>分表名查询（Dapper / EFCore / SqlSugar；其他抛业务异常由 VM 记日志）。</summary>
        public IReadOnlyList<string> GetSplitTableNames()
        {
            var db = EnsureDb(Model.SelectedProfile());
            if (db is SQL.Dapper.DapperBase<T> dapper)
                return dapper.GetSplitTables().ToList();
            if (db is SQL.EFCore.EFCoreBase<T> ef)
                return ef.GetSplitTables().ToList();
            if (db is SQL.SqlSugarSQL.SqlSugarBase<T> sugar)
                return sugar.GetSplitTables().ToList();
            throw new NotSupportedException("当前实例不支持分表名查询");
        }

        /// <summary>
        /// 当前实例（每次操作都过指纹：档案改了自动重建，与原来每个命令先 EnsureDb 一致）。
        /// </summary>
        private ISQL<T> EnsureCurrent() =>
            _currentParam is null
                ? throw new InvalidOperationException("请先连接数据库")
                : EnsureDb(_currentParam);

        // ---------- Model 初始化（读 Config 文件 → 默认） ----------
        private void LoadProfiles()
        {
            var profiles = JsonEx.TryRead<List<SQLParam>>(Model.FullPath!);
            if (profiles is { Count: > 0 })
                ReplaceConnections(profiles.Where(p => !string.IsNullOrWhiteSpace(p.Name)));
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new SQLParam { Name = "默认连接" };
                Model.DataCollection.Add(fallback);
                Model.SelectedConnection = fallback;
            }
        }

        private void ReplaceConnections(IEnumerable<SQLParam> profiles)
        {
            Model.DataCollection.Clear();
            foreach (var p in profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                    p.Name = $"连接{Model.DataCollection.Count + 1}";
                Model.DataCollection.Add(p);
            }
            Model.SelectedConnection = Model.DataCollection.FirstOrDefault();
        }
    }
}
