// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SqlWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、Extension.UI.Controls、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using DataManager.Controls.Configuration;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.Collections.ObjectModel;
using System.Threading;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// SQL 实际流程（对标 WPFControls 的 DataQueryService：持有 <see cref="Models.SqlPageModel{T}"/> 并读写它，
    /// ViewModel 只暴露 Model + 装配命令）。
    /// 职责：Model 初始化（读 Config 文件 → 默认）、连接实例生命周期（指纹重建）、
    /// 测试数据生成、全部数据操作（含结果集回填）。
    /// 行实体由宿主定义（实现 <see cref="Models.ISqlEntity"/>），ORM 映射随实体走。
    /// P9-5：长操作（连接/查询/清表）走可取消 Token；重连、切换档案与释放时先取消在途操作，
    /// 已释放或已取消的操作不再回写界面。
    /// </summary>
    public sealed class SqlWorkspace<T> : IDisposable, IAsyncDisposable
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
        private volatile bool _disposed;
        private readonly SemaphoreSlim _operationGate = new(1, 1);

        /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
        public Models.SqlPageModel<T> Model { get; }

        /// <summary>生产默认：配置落在 <see cref="ConfigRoot.Default"/>（即 &lt;BaseDirectory&gt;\Config）。</summary>
        public SqlWorkspace(ISQLManager sqlManager) : this(sqlManager, ConfigRoot.Default)
        {
        }

        /// <summary>
        /// 初始化 SqlWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 LoadProfiles；具体条件和先后次序见方法体。
        /// 传入自己的 <see cref="ConfigRoot"/> 可把本工作区读写配置的目录隔离到临时位置。
        /// </summary>
        public SqlWorkspace(ISQLManager sqlManager, ConfigRoot configRoot)
        {
            ArgumentNullException.ThrowIfNull(configRoot);
            _sqlManager = sqlManager;
            Model = new Models.SqlPageModel<T>(configRoot);
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
        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        /// <summary>Cancel pending work, drain entered provider operations, then release the database.</summary>
        public async ValueTask DisposeAsync()
        {
            _disposed = true;
            CancelInFlight();
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _operationCts = null;
                var db = _db;
                _db = null;
                _currentKey = null;
                _currentParam = null;
                if (db is null) return;
                await Task.Run(() =>
                {
                    try { db.DisConnect(); } catch { }
                    try { _sqlManager.Remove(db); } catch { }
                    try { db.Dispose(); } catch { }
                }).ConfigureAwait(false);
            }
            finally { _operationGate.Release(); }
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
            _operationGate.Wait();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_db != null && _currentKey == key) return _db;
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
            finally { _operationGate.Release(); }
        }

        /// <summary>
        /// 发起连接操作；对应入口为 ConnectAsync。
        /// 正文中的关键协作调用包括 EnsureDb、SelectedProfile、BeginOperation、Run、Connect；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task ConnectAsync()
        {
            var db = EnsureDb(Model.SelectedProfile());
            var token = BeginOperation();
            await WithDbAsync(db, token, () =>
            {
                db.Connect();
                return Task.FromResult(true);
            }).ConfigureAwait(false);
            PostToModel(token, () =>
            {
                Model.IsConnected = true;
                Model.StatusText = "已连接";
            });
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// 正文中的关键协作调用包括 CancelInFlight；具体条件和先后次序见方法体。
        /// </summary>
        public void Disconnect()
        {
            // P9-5：先取消在途操作再断开，避免断开后仍有任务回写“已连接”。
            CancelInFlight();
            _operationGate.Wait();
            try { _db?.DisConnect(); }
            finally { _operationGate.Release(); }
            Model.IsConnected = false;
            Model.StatusText = "未连接";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 TestConnectionAsync。
        /// 正文中的关键协作调用包括 EnsureDb、SelectedProfile、BeginOperation、Run、Connect；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task TestConnectionAsync()
        {
            var db = EnsureDb(Model.SelectedProfile());
            var token = BeginOperation();
            await WithDbAsync(db, token, () =>
            {
                db.Connect();
                try { token.ThrowIfCancellationRequested(); }
                finally { db.DisConnect(); }
                return Task.FromResult(true);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 NewEntity。
        /// </summary>
        private T NewEntity() => new()
        {
            Name = $"测试{DateTime.Now:HHmmss}",
            CreateTime = DateTime.Now,
        };

        /// <summary>
        /// 执行添加操作；对应入口为 AddNew。
        /// 正文中的关键协作调用包括 Add、EnsureCurrent、NewEntity；具体条件和先后次序见方法体。
        /// </summary>
        public void AddNew() => WithDb(db => db.Add(NewEntity()));

        /// <summary>
        /// 执行添加操作；对应入口为 AddNewBatch。
        /// 正文中的关键协作调用包括 AddRange、EnsureCurrent、Select、Range、NewEntity；具体条件和先后次序见方法体。
        /// </summary>
        public void AddNewBatch(int count) =>
            WithDb(db => db.AddRange(Enumerable.Range(1, count).Select(_ => NewEntity())));

        /// <summary>查询全部（可取消；取消/释放后不回写结果集）。</summary>
        public async Task QueryAllAsync()
        {
            var db = EnsureCurrent();
            var token = BeginOperation();
            var data = await WithDbAsync(db, token, async () =>
                (await db.QueryAsync(token).ConfigureAwait(false)).ToList()).ConfigureAwait(false);
            PostToModel(token, () => Model.Results = new ObservableCollection<T>(data));
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateSelected。
        /// 正文中的关键协作调用包括 ThrowIfNull、Update、EnsureCurrent；具体条件和先后次序见方法体。
        /// </summary>
        public void UpdateSelected(T selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            WithDb(db => db.Update(x => x.Id == selected.Id, new T
            {
                Name = selected.Name + "_改",
                CreateTime = DateTime.Now,
            }));
        }

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteSelected。
        /// 正文中的关键协作调用包括 ThrowIfNull、Delete、EnsureCurrent；具体条件和先后次序见方法体。
        /// </summary>
        public void DeleteSelected(T selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            WithDb(db => db.Delete(x => x.Id == selected.Id));
        }

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteAll。
        /// 正文中的关键协作调用包括 DeleteAll、EnsureCurrent、UIInvokeSafe、Clear；具体条件和先后次序见方法体。
        /// </summary>
        public void DeleteAll()
        {
            WithDb(db => db.DeleteAll());
            DispatcherEx.UIInvokeSafe(() => Model.Results.Clear());
        }

        /// <summary>P9-5：清空表（可取消；取消/释放后不清空结果集）。</summary>
        public async Task DeleteAllAsync()
        {
            var db = EnsureCurrent();
            var token = BeginOperation();
            await WithDbAsync(db, token, async () =>
            {
                await db.DeleteAllAsync(token).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
            PostToModel(token, () => Model.Results.Clear());
        }

        /// <summary>分表名查询（P12：经 Core 能力接口，不再对具体 Provider 基类做类型判断）。</summary>
        public IReadOnlyList<string> GetSplitTableNames()
        {
            var db = EnsureDb(Model.SelectedProfile());
            _operationGate.Wait();
            try
            {
                CheckCurrent(db);
                if (db is ISplitTableNameProvider split) return split.GetSplitTables().ToList();
                throw new NotSupportedException("当前实例不支持分表名查询");
            }
            finally { _operationGate.Release(); }
        }

        private void CheckCurrent(ISQL<T> db)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!ReferenceEquals(db, _db)) throw new OperationCanceledException("Database profile changed.");
        }

        private void WithDb(Action<ISQL<T>> operation)
        {
            var db = EnsureCurrent();
            _operationGate.Wait();
            try { CheckCurrent(db); operation(db); }
            finally { _operationGate.Release(); }
        }

        private async Task<TResult> WithDbAsync<TResult>(ISQL<T> db, CancellationToken token, Func<Task<TResult>> operation)
        {
            await _operationGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                CheckCurrent(db);
                // Providers may capture their entry context; synchronous drain must not wait on the UI.
                return await Task.Run(operation, token).ConfigureAwait(false);
            }
            finally { _operationGate.Release(); }
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReplaceConnections。
        /// 正文中的关键协作调用包括 Clear、IsNullOrWhiteSpace、Add、FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
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
