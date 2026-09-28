using DataManager.SQL;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using DataManager.SQL.Definition;
using System.Collections.ObjectModel;
using System.Threading;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// SQL 实际流程（对标 WPFControl 的 DataQueryService：持有 <see cref="Models.SqlPageModel{T}"/> 并读写它，
    /// ViewModel 只暴露 Model + 装配命令）。
    /// 职责：Model 初始化（读 Config 文件 → 默认）、连接实例生命周期（指纹重建）、
    /// 测试数据生成、全部数据操作（含结果集回填）。
    /// 行实体由宿主定义（实现 <see cref="Models.ISqlEntity"/>），ORM 映射随实体走。
    /// P9-5：长操作（连接/查询/清表）走可取消 Token；重连、切换档案与释放时先取消在途操作，
    /// 已释放或已取消的操作不再回写界面。
    /// </summary>
    public sealed class SqlWorkspace<T> : IDisposable
        where T : class, Models.ISqlEntity, new()
    {
        private readonly ISQLManager _sqlManager;
        private ISQL<T>? _db;
        private string? _currentKey;
        private SQLParam? _currentParam;

        /// <summary>
        /// P9-5：在途操作取消源。同一时刻至多一个在途操作——签发新 Token 前先取消旧的。
        /// 只取消不释放：取消后可能仍有在途调用在 Token 上注册回调（EF Core 会），
        /// 提前 Dispose 会让注册抛 ObjectDisposedException；无定时器的 CTS 无托管外资源，交给 GC 即可。
        /// </summary>
        private CancellationTokenSource? _operationCts;
        private bool _disposed;

        /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
        public Models.SqlPageModel<T> Model { get; } = new();

        public SqlWorkspace(ISQLManager sqlManager)
        {
            _sqlManager = sqlManager;
            LoadProfiles();
        }

        /// <summary>
        /// P9-5：签发本次操作的取消标记。若已有在途操作，先取消它（新操作即“接管”）。
        /// 已释放时返回已取消的 Token，调用方会立刻观察到取消，不会再触碰数据库或界面。
        /// </summary>
        public CancellationToken BeginOperation()
        {
            if (_disposed)
                return new CancellationToken(canceled: true);
            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();
            return _operationCts.Token;
        }

        /// <summary>
        /// P9-5：取消在途操作（重连、切换数据库配置、关闭工作台时调用）。
        /// 取消属正常流程——不抛异常、不弹错误，调用方按“操作已取消”处理。
        /// </summary>
        public void CancelInFlight() => _operationCts?.Cancel();

        /// <summary>
        /// P9-5：释放——先取消在途操作，再断开并释放 SQL 实例；此后一切界面回写都被丢弃。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            CancelInFlight();
            _operationCts = null;

            var db = _db;
            _db = null;
            _currentKey = null;
            _currentParam = null;
            if (db is null)
                return;
            try { db.DisConnect(); } catch { }
            try { _sqlManager.Remove(db); } catch { }
            try { db.Dispose(); } catch { }
        }

        /// <summary>
        /// P9-5：结果回填的统一出口。已释放、或本次操作已被取消时直接丢弃，
        /// 保证关闭工作台后不会再有后台任务继续改界面（排队期间被取消的也拦在第二道检查上）。
        /// </summary>
        private void PostToModel(CancellationToken operationToken, Action write)
        {
            if (_disposed || operationToken.IsCancellationRequested)
                return;
            DispatcherEx.UIInvokeSafe(() =>
            {
                if (_disposed || operationToken.IsCancellationRequested)
                    return;
                write();
            });
        }

        /// <summary>
        /// 连接指纹：库类型 + 工具类型 + 分字段全文参与，任一改动（含密码纠错）都重建实例。
        /// 曾用“工具+库类型”做键，导致密码改对后仍复用旧实例，一直报密码错误。
        /// P3-1：纳入学 FilePath/ConnectionString/TrustServerCertificate，
        /// 否则 SQLite 换文件、直通串变更不触发重建（同款旧 bug）。
        /// P9-4：纳入 CommandTimeoutSeconds——EF Core 的命令超时在构造 DbContext 时固化，
        /// 改了超时值必须重建实例才能生效（Dapper/SqlSugar 虽按命令读取，此处口径统一）。
        /// </summary>
        public static string BuildDbKey(SQLParam param) =>
            $"{param.ToolType}:{param.DBType}:{param.ServerIP}:{param.Port}:{param.UserName}:{param.Password}:{param.DBName}:{param.FilePath}:{param.ConnectionString}:{param.TrustServerCertificate}:{param.CommandTimeoutSeconds}";

        /// <summary>
        /// 按参数确保连接实例（指纹命中复用，否则断开旧实例重建）。
        /// P9-5：重建前先取消在途操作——否则旧操作会在被释放的旧实例上继续跑（旧实现只 DisConnect 不取消）。
        /// </summary>
        public ISQL<T> EnsureDb(SQLParam param)
        {
            ArgumentNullException.ThrowIfNull(param);
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlWorkspace<T>));
            var key = BuildDbKey(param);
            if (_db != null && _currentKey == key)
                return _db;

            CancelInFlight();

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
            var token = BeginOperation();
            await Task.Run(() => db.Connect(), token);
            PostToModel(token, () =>
            {
                Model.IsConnected = true;
                Model.StatusText = "已连接";
            });
        }

        public void Disconnect()
        {
            // P9-5：先取消在途操作再断开，避免断开后仍有任务回写“已连接”。
            CancelInFlight();
            _db?.DisConnect();
            Model.IsConnected = false;
            Model.StatusText = "未连接";
        }

        public async Task TestConnectionAsync()
        {
            var db = EnsureDb(Model.SelectedProfile());
            var token = BeginOperation();
            await Task.Run(() => db.Connect(), token);
            await Task.Run(() => db.DisConnect(), token);
        }

        private T NewEntity() => new()
        {
            Name = $"测试{DateTime.Now:HHmmss}",
            CreateTime = DateTime.Now,
        };

        public void AddNew() => EnsureCurrent().Add(NewEntity());

        public void AddNewBatch(int count) =>
            EnsureCurrent().AddRange(Enumerable.Range(1, count).Select(_ => NewEntity()));

        /// <summary>查询全部（可取消；取消/释放后不回写结果集）。</summary>
        public async Task QueryAllAsync()
        {
            var db = EnsureCurrent();
            var token = BeginOperation();
            var data = (await db.QueryAsync(token)).ToList();
            PostToModel(token, () => Model.Results = new ObservableCollection<T>(data));
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

        /// <summary>P9-5：清空表（可取消；取消/释放后不清空结果集）。</summary>
        public async Task DeleteAllAsync()
        {
            var db = EnsureCurrent();
            var token = BeginOperation();
            await db.DeleteAllAsync(token);
            PostToModel(token, () => Model.Results.Clear());
        }

        /// <summary>分表名查询（P12：经 Core 能力接口，不再对具体 Provider 基类做类型判断）。</summary>
        public IReadOnlyList<string> GetSplitTableNames()
        {
            var db = EnsureDb(Model.SelectedProfile());
            if (db is ISplitTableNameProvider split)
                return split.GetSplitTables().ToList();
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
