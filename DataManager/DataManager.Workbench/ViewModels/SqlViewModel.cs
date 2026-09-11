using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.SQL.Definition;
using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Models;
using DataManager.Workbench.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// SQL 测试页：配置直绑 <see cref="SQLParam"/> 行模型，持久化走 ConfigManager 控件保存按钮
    /// （<see cref="ProfilePath"/>，一页一文件）；实际流程在 <see cref="SqlWorkspace"/>。
    /// </summary>
    public partial class SqlViewModel : ObservableObject
    {
        private readonly SqlWorkspace _workspace;

        /// <summary>连接档案文件完整路径（XAML 控件落盘目标 + 测试备份用）。</summary>
        public static string ProfilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SqlConnections.json");

        /// <summary>档案目录（XAML 控件 FilePath 绑定用）。</summary>
        public string ConfigDir => Path.GetDirectoryName(ProfilePath)!;

        public SqlViewModel(SqlWorkspace workspace)
        {
            _workspace = workspace;

            // 新文件 → 老 workbench-config.json 导入 → 空档案，保证界面永不为空。
            if (!TryLoadProfiles(out _) || Connections.Count == 0)
            {
                var fallback = new SQLParam { Name = "默认连接" };
                Connections.Add(fallback);
                SelectedConnection = fallback;
            }
        }

        // ---------- 连接档案（直绑 SQLParam 可观察模型，无中转层） ----------
        public ObservableCollection<SQLParam> Connections { get; } = new();

        [ObservableProperty]
        private SQLParam? selectedConnection;

        partial void OnSelectedConnectionChanged(SQLParam? value)
        {
            // 档案切换 = 连接要素变更，直接复位状态；stale 实例由流程按指纹重建。
            IsConnected = false;
            StatusText = "未连接";
        }

        /// <summary>供 XAML 绑定的增加回调（命名 + 选中；保存由控件保存按钮完成）。</summary>
        public Action ConnectionAddedAction => OnConnectionAddedExternally;

        /// <summary>供 XAML 绑定的删除回调（保底至少留一个）。</summary>
        public Action ConnectionDeletedAction => OnConnectionDeletedExternally;

        /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
        public Action ProfilesSavedAction => OnProfilesSavedExternally;

        // ---------- 状态 ----------
        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private string statusText = "未连接";

        [ObservableProperty]
        private string outputText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastElapsed = "-";

        [ObservableProperty]
        private ObservableCollection<TestEntity> results = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
        private TestEntity? selectedEntity;

        private bool CanOperateOnSelection => SelectedEntity is not null;

        // ---------- 命令 ----------
        [RelayCommand]
        private async Task ConnectAsync() => await RunAsync("连接", async () =>
        {
            var db = _workspace.EnsureDb(SelectedProfile());
            await Task.Run(() => db.Connect());
            IsConnected = true;
            StatusText = "已连接";
        });

        [RelayCommand]
        private void Disconnect() => Run(() =>
        {
            _workspace.Disconnect();
            IsConnected = false;
            StatusText = "未连接";
        });

        [RelayCommand]
        private Task AddAsync() => RunWork("单条添加", () => _workspace.AddNew());

        [RelayCommand]
        private Task AddRangeAsync() => RunWork("批量添加 10 条", () => _workspace.AddNewBatch(10));

        [RelayCommand]
        private async Task QueryAllAsync() => await RunAsync("查询全部", async () =>
        {
            var data = await _workspace.QueryAllAsync();
            Results = new ObservableCollection<TestEntity>(data);
        });

        [RelayCommand]
        private async Task QueryConditionAsync() => await RunAsync("条件查询 (Age > 20)", async () =>
        {
            var data = await _workspace.QueryAdultsAsync();
            Results = new ObservableCollection<TestEntity>(data);
        });

        [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
        private Task UpdateSelectedAsync() => RunWork("更新选中行", () =>
        {
            var sel = SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.UpdateSelected(sel);
        });

        [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
        private Task DeleteSelectedAsync() => RunWork("删除选中行", () =>
        {
            var sel = SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.DeleteSelected(sel);
        });

        [RelayCommand]
        private Task DeleteAllAsync() => RunWork("清空表", () =>
        {
            _workspace.DeleteAll();
            UiThread.Invoke(() => Results.Clear());
        });

        [RelayCommand]
        private void SplitTables() => Run(() =>
        {
            try
            {
                var db = _workspace.EnsureDb(SelectedProfile());
                var tables = _workspace.GetSplitTables(db);
                Log($"✓ 共 {tables.Count} 张分表({SelectedProfile().ToolType}): {string.Join(", ", tables)}");
            }
            catch (NotSupportedException ex)
            {
                Log($"⚠ {ex.Message}");
            }
        });

        [RelayCommand]
        private async Task TestConnectionAsync() => await RunAsync("连接测试", async () =>
        {
            await _workspace.TestConnectionAsync(SelectedProfile());
        });

        [RelayCommand]
        private void ClearOutput() => OutputText = string.Empty;

        [RelayCommand]
        private void ExportOutput()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt",
                    FileName = $"sql-output-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                };
                if (dialog.ShowDialog() != true)
                    return;
                System.IO.File.WriteAllText(dialog.FileName, OutputText);
                Log($"✓ 输出已导出 {OutputText.Length} 字 → {dialog.FileName}");
            }
            catch (Exception ex)
            {
                Log($"✗ 导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ConfigManagerView 增加按钮回调：控件已用 Activator 加了空行，此处命名 + 选中。
        /// </summary>
        public void OnConnectionAddedExternally() => Run(() =>
        {
            var blank = Connections.LastOrDefault(p => string.IsNullOrWhiteSpace(p.Name));
            var profile = blank ?? new SQLParam { Name = $"连接{Connections.Count + 1}" };
            if (blank is null)
                Connections.Add(profile);
            else
                profile.Name = $"连接{Connections.Count}";
            SelectedConnection = profile;
            Log($"✓ 已添加连接档案：{profile.Name}（点保存落盘）");
        });

        /// <summary>
        /// ConfigManagerView 删除按钮回调：控件已移除选中行，此处保底至少留一个。
        /// </summary>
        public void OnConnectionDeletedExternally() => Run(() =>
        {
            if (Connections.Count == 0)
            {
                var fallback = new SQLParam { Name = "默认连接" };
                Connections.Add(fallback);
                SelectedConnection = fallback;
            }
            else if (SelectedConnection is null || !Connections.Contains(SelectedConnection))
            {
                SelectedConnection = Connections[^1];
            }
        });

        /// <summary>ConfigManagerView 保存按钮回调：控件已写文件，此处记日志。</summary>
        public void OnProfilesSavedExternally() => Run(() =>
            Log($"✓ 已保存 {Connections.Count} 个连接档案 → {ProfilePath}"));

        // ---------- 持久化（新文件优先，老文件单次导入） ----------
        private bool TryLoadProfiles(out int count)
        {
            count = 0;
            var profiles = JsonConfigStore.ReadList<SQLParam>(ProfilePath) ?? TryImportLegacy();
            if (profiles is not { Count: > 0 })
                return false;

            ReplaceConnections(profiles);
            count = Connections.Count;
            return true;
        }

        private static ESQLType ParseDbType(string? name) =>
            Enum.TryParse(name, out ESQLType v) ? v : ESQLType.PostgreSQL;

        private static ESQLToolType ParseToolType(string? name) =>
            Enum.TryParse(name, out ESQLToolType v) ? v : ESQLToolType.EFCore;

        /// <summary>
        /// 单次导入：老 workbench-config.json 的 SqlConnections 数组 / Sql 单节 → 新文件。
        /// 成功后写新文件并清掉老节；空名行丢弃。
        /// </summary>
        private static List<SQLParam>? TryImportLegacy()
        {
            try
            {
                var path = JsonConfigStore.FileFullPath;
                if (!File.Exists(path))
                    return null;
                var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
                if (root is null)
                    return null;

                List<SQLParam>? result = null;
                if (root["SqlConnections"]?.AsArray() is { Count: > 0 } arr)
                {
                    result = new List<SQLParam>();
                    foreach (var n in arr)
                    {
                        if (n is null || string.IsNullOrWhiteSpace((string?)n["Name"]))
                            continue;
                        result.Add(new SQLParam
                        {
                            Name = (string?)n["Name"] ?? string.Empty,
                            DBType = ParseDbType((string?)n["DbType"]),
                            ToolType = ParseToolType((string?)n["ToolType"]),
                            ConnectionString = (string?)n["ConnectionString"] ?? string.Empty,
                            ServerIP = (string?)n["ServerIP"] ?? string.Empty,
                            Port = (string?)n["Port"] ?? string.Empty,
                            DBName = (string?)n["DBName"] ?? string.Empty,
                            UserName = (string?)n["UserName"] ?? string.Empty,
                            Password = (string?)n["Password"] ?? string.Empty,
                            FilePath = (string?)n["FilePath"] ?? string.Empty,
                            TrustServerCertificate = (bool?)n["TrustServerCertificate"] ?? true,
                        });
                    }
                }

                if (result is not { Count: > 0 }
                    && root["Sql"] is JsonObject single
                    && !string.IsNullOrWhiteSpace((string?)single["ServerIP"]))
                {
                    result = [new SQLParam
                    {
                        Name = (string?)single["DBName"] ?? "默认连接",
                        ServerIP = (string?)single["ServerIP"] ?? string.Empty,
                        Port = (string?)single["Port"] ?? string.Empty,
                        UserName = (string?)single["UserName"] ?? string.Empty,
                        Password = (string?)single["Password"] ?? string.Empty,
                        DBName = (string?)single["DBName"] ?? string.Empty,
                        DBType = ParseDbType((string?)single["DbType"]),
                        ToolType = ParseToolType((string?)single["ToolType"]),
                    }];
                }

                if (result is not { Count: > 0 })
                    return null;

                JsonConfigStore.WriteList(ProfilePath, result);
                JsonConfigStore.RemoveLegacySections("SqlConnections", "Sql");
                return result;
            }
            catch
            {
                return null;
            }
        }

        private void ReplaceConnections(IEnumerable<SQLParam> profiles)
        {
            Connections.Clear();
            foreach (var p in profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                    p.Name = $"连接{Connections.Count + 1}";
                Connections.Add(p);
            }
            SelectedConnection = Connections.FirstOrDefault();
        }

        // ---------- 展示层：日志输出/计时/忙指示 ----------
        /// <summary>当前档案（空即报“请先添加”，替代各命令内的重复判空）。</summary>
        private SQLParam SelectedProfile() =>
            SelectedConnection ?? throw new InvalidOperationException("请先添加数据库连接档案");

        private void Log(string msg) => UiThread.Invoke(
            () => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText);

        private Task RunWork(string title, Action action)
        {
            IsBusy = true;
            LastElapsed = "执行中…";
            return Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    action();
                    sw.Stop();
                    LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                    Log($"✓ {title} ({LastElapsed})");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                    Log($"✗ {title}: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            });
        }

        private async Task RunAsync(string title, Func<Task> action)
        {
            IsBusy = true;
            LastElapsed = "执行中…";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await action();
                sw.Stop();
                LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                Log($"✓ {title} ({LastElapsed})");
            }
            catch (Exception ex)
            {
                sw.Stop();
                LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                Log($"✗ {title}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
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
