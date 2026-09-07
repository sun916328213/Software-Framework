using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.Workbench.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// SQL 测试页：通过 DataManager 的 SQLManager / ISQL 完成全部数据库操作
    /// </summary>
    public class SqlViewModel : BindableBase
    {
        private readonly SQLManager _sqlManager;
        private readonly Random _random = new();
        private ISQL<TestEntity> _db;
        private string _currentKey;

        public SqlViewModel(SQLManager sqlManager)
        {
            _sqlManager = sqlManager;

            ConnectCommand = new DelegateCommand(async () => await RunAsync("连接", async () =>
            {
                var db = EnsureDb();
                await Task.Run(() => db.Connect());
                IsConnected = true;
                StatusText = "已连接";
            }));
            DisconnectCommand = new DelegateCommand(() => Run(() =>
            {
                _db?.DisConnect();
                IsConnected = false;
                StatusText = "未连接";
            }));
            AddCommand = new DelegateCommand(() => RunWork("单条添加", () =>
            {
                var db = EnsureDb();
                db.Add(NewEntity());
            }));
            AddRangeCommand = new DelegateCommand(() => RunWork("批量添加 10 条", () =>
            {
                var db = EnsureDb();
                db.AddRange(Enumerable.Range(1, 10).Select(_ => NewEntity()));
            }));
            QueryAllCommand = new DelegateCommand(async () => await RunAsync("查询全部", async () =>
            {
                var db = EnsureDb();
                var data = await db.QueryAsync();
                Results = new ObservableCollection<TestEntity>(data);
            }));
            QueryConditionCommand = new DelegateCommand(async () => await RunAsync("条件查询 (Age > 20)", async () =>
            {
                var db = EnsureDb();
                var data = await db.QueryAsync(x => x.Age > 20);
                Results = new ObservableCollection<TestEntity>(data);
            }));
            UpdateSelectedCommand = new DelegateCommand(() => RunWork("更新选中行", () =>
            {
                var db = EnsureDb();
                var sel = SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
                db.Update(x => x.Id == sel.Id, new TestEntity
                {
                    Name = sel.Name + "_改",
                    Age = sel.Age + 1,
                    CreateTime = DateTime.Now,
                });
            }), () => SelectedEntity != null).ObservesProperty(() => SelectedEntity);
            DeleteSelectedCommand = new DelegateCommand(() => RunWork("删除选中行", () =>
            {
                var db = EnsureDb();
                var sel = SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
                db.Delete(x => x.Id == sel.Id);
            }), () => SelectedEntity != null).ObservesProperty(() => SelectedEntity);
            DeleteAllCommand = new DelegateCommand(() => RunWork("清空表", () =>
            {
                var db = EnsureDb();
                db.DeleteAll();
                Results.Clear();
            }));
            SplitTablesCommand = new DelegateCommand(() => Run(() =>
            {
                if (EnsureDb() is SQL.Dapper.DapperBase<TestEntity> dapper)
                {
                    var tables = dapper.GetSplitTables();
                    Log($"✓ 共 {tables.Count} 张分表: {string.Join(", ", tables)}");
                }
                else
                {
                    Log("⚠ 分表名查询仅 Dapper 支持");
                }
            }));
        }

        // ---------- 连接参数 ----------
        public string ServerIP { get; set; } = "localhost";
        public string Port { get; set; } = "5432";
        public string UserName { get; set; } = "postgres";
        public string Password { get; set; } = "postgres";
        public string DBName { get; set; } = "workbench";
        public Array DbTypes { get; } = Enum.GetValues(typeof(ESQLType));
        public Array ToolTypes { get; } = Enum.GetValues(typeof(ESQLToolType));

        private ESQLType _selectedDbType = ESQLType.PostgreSQL;
        public ESQLType SelectedDbType
        {
            get => _selectedDbType;
            set => SetProperty(ref _selectedDbType, value);
        }

        private ESQLToolType _selectedToolType = ESQLToolType.EFCore;
        public ESQLToolType SelectedToolType
        {
            get => _selectedToolType;
            set => SetProperty(ref _selectedToolType, value);
        }

        // ---------- 状态 ----------
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        private string _statusText = "未连接";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _outputText = string.Empty;
        public string OutputText
        {
            get => _outputText;
            set => SetProperty(ref _outputText, value);
        }

        private ObservableCollection<TestEntity> _results = new();
        public ObservableCollection<TestEntity> Results
        {
            get => _results;
            set => SetProperty(ref _results, value);
        }

        private TestEntity _selectedEntity;
        public TestEntity SelectedEntity
        {
            get => _selectedEntity;
            set => SetProperty(ref _selectedEntity, value);
        }

        // ---------- 命令 ----------
        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand AddRangeCommand { get; }
        public DelegateCommand QueryAllCommand { get; }
        public DelegateCommand QueryConditionCommand { get; }
        public DelegateCommand UpdateSelectedCommand { get; }
        public DelegateCommand DeleteSelectedCommand { get; }
        public DelegateCommand DeleteAllCommand { get; }
        public DelegateCommand SplitTablesCommand { get; }

        // ---------- 私有方法 ----------
        private ISQL<TestEntity> EnsureDb()
        {
            var key = $"{SelectedToolType}:{SelectedDbType}";
            if (_db != null && _currentKey == key)
                return _db;

            if (_db != null)
            {
                try { _db.DisConnect(); } catch { }
                _sqlManager.Remove(_db);
            }

            _db = _sqlManager.Create<TestEntity>(new SQLParam
            {
                Name = "workbench",
                ServerIP = ServerIP,
                Port = Port,
                UserName = UserName,
                Password = Password,
                DBName = DBName,
                DBType = SelectedDbType,
                ToolType = SelectedToolType,
            });
            _currentKey = key;
            IsConnected = false;
            StatusText = "未连接";
            Log($"已创建 {SelectedToolType} 实例 ({SelectedDbType})");
            return _db;
        }

        private TestEntity NewEntity() => new()
        {
            Name = $"测试{DateTime.Now:HHmmss}",
            Age = _random.Next(18, 60),
            CreateTime = DateTime.Now,
        };

        private void Log(string msg) => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText;

        private Task RunWork(string title, Action action) => Task.Run(() =>
        {
            try
            {
                action();
                Log("✓ " + title);
            }
            catch (Exception ex)
            {
                Log($"✗ {title}: {ex.Message}");
            }
        });

        private async Task RunAsync(string title, Func<Task> action)
        {
            try
            {
                await action();
                Log("✓ " + title);
            }
            catch (Exception ex)
            {
                Log($"✗ {title}: {ex.Message}");
            }
        }

        private void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log($"✗ {ex.Message}");
            }
        }
    }
}
