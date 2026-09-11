using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Services;
using DataManager.Workbench.ViewModels;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// SQL 连接档案：行模型通知、增删保底、新文件（Config/SqlConnections.json）往返、
    /// 老 workbench-config.json 单次导入。持久化走 ConfigManager 控件保存按钮，
    /// 此处用同格式的 <see cref="JsonConfigStore"/> 读写验证兼容性。
    /// 注意：含数据库密码明文，仅限本机调试工作台使用；测前后原样恢复配置文件。
    /// </summary>
    public class SqlConnectionStoreTests : IDisposable
    {
        private readonly string? _configBackup;
        private readonly string? _legacyBackup;

        public SqlConnectionStoreTests()
        {
            _configBackup = File.Exists(SqlViewModel.ProfilePath)
                ? File.ReadAllText(SqlViewModel.ProfilePath)
                : null;
            _legacyBackup = File.Exists(JsonConfigStore.FileFullPath)
                ? File.ReadAllText(JsonConfigStore.FileFullPath)
                : null;
            if (File.Exists(SqlViewModel.ProfilePath))
                File.Delete(SqlViewModel.ProfilePath);
            if (File.Exists(JsonConfigStore.FileFullPath))
                File.Delete(JsonConfigStore.FileFullPath);
        }

        public void Dispose()
        {
            Restore(SqlViewModel.ProfilePath, _configBackup);
            Restore(JsonConfigStore.FileFullPath, _legacyBackup);
            GC.SuppressFinalize(this);
        }

        private static void Restore(string path, string? backup)
        {
            if (backup is null)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, backup);
            }
        }

        [Fact]
        public void SqlParam_RaisesPropertyChanged()
        {
            var param = new SQLParam();
            var raised = new List<string>();
            ((INotifyPropertyChanged)param).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null)
                    raised.Add(e.PropertyName);
            };

            param.ServerIP = "db01";
            param.DBType = ESQLType.MySQL;

            Assert.Contains(nameof(SQLParam.ServerIP), raised);
            Assert.Contains(nameof(SQLParam.DBType), raised);
        }

        [Fact]
        public void Ctor_WithoutConfig_HasExactlyOneProfile()
        {
            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            Assert.Single(vm.Connections);
            Assert.NotNull(vm.SelectedConnection);
        }

        [Fact]
        public void AddRemove_ViaManagerCallbacks_UpdatesSelectionAndCount()
        {
            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            // 模拟控件增加按钮：空行已由控件加入，回调命名选中（保存由控件保存按钮完成）。
            vm.Connections.Add(new SQLParam());
            vm.OnConnectionAddedExternally();
            Assert.Equal(2, vm.Connections.Count);
            Assert.Equal("连接2", vm.SelectedConnection!.Name);

            vm.SelectedConnection.ServerIP = "db01";
            Assert.Equal("db01", vm.Connections[1].ServerIP);

            // 模拟控件删除按钮：行已由控件移除，回调保底。
            vm.Connections.RemoveAt(1);
            vm.OnConnectionDeletedExternally();
            Assert.Single(vm.Connections);
            Assert.NotNull(vm.SelectedConnection);
        }

        [Fact]
        public void DeleteLastRow_RestoresDefault()
        {
            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            vm.Connections.Clear();
            vm.OnConnectionDeletedExternally();
            Assert.Single(vm.Connections);
            Assert.Equal("默认连接", vm.SelectedConnection!.Name);
        }

        [Fact]
        public void SaveLoad_RoundTripsAllFields()
        {
            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            vm.SelectedConnection!.Name = "主库";
            vm.SelectedConnection.ServerIP = "10.0.0.1";
            vm.SelectedConnection.Port = "5433";
            vm.SelectedConnection.DBName = "main";
            vm.SelectedConnection.UserName = "u";
            vm.SelectedConnection.Password = "p";
            vm.Connections.Add(new SQLParam());
            vm.OnConnectionAddedExternally();
            vm.SelectedConnection!.Name = "文件库";
            vm.SelectedConnection.DBType = ESQLType.SQLite;
            vm.SelectedConnection.FilePath = "backup.db";
            // 与控件保存按钮同格式落盘，再新起 VM 读回。
            JsonConfigStore.WriteList(SqlViewModel.ProfilePath, vm.Connections);

            var reloaded = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            Assert.Equal(2, reloaded.Connections.Count);
            var primary = Assert.Single(reloaded.Connections, p => p.Name == "主库");
            Assert.Equal("10.0.0.1", primary.ServerIP);
            Assert.Equal("5433", primary.Port);
            Assert.Equal("main", primary.DBName);
            Assert.Equal("u", primary.UserName);
            Assert.Equal("p", primary.Password);
            var fileDb = Assert.Single(reloaded.Connections, p => p.Name == "文件库");
            Assert.Equal(ESQLType.SQLite, fileDb.DBType);
            Assert.Equal("backup.db", fileDb.FilePath);
        }

        [Fact]
        public void LegacyArraySection_ImportsAndCleansUp()
        {
            // 老格式现状节（全字段数组）手写一份，不依赖已删除的记录类型。
            var legacy = new JsonObject
            {
                ["SqlConnections"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["Name"] = "老PG",
                        ["DbType"] = "PostgreSQL",
                        ["ToolType"] = "Dapper",
                        ["ConnectionString"] = "",
                        ["ServerIP"] = "192.168.9.9",
                        ["Port"] = "5433",
                        ["DBName"] = "legacydb",
                        ["UserName"] = "admin",
                        ["Password"] = "pw",
                        ["FilePath"] = "",
                        ["TrustServerCertificate"] = true,
                    },
                    new JsonObject { ["Name"] = "", ["ServerIP"] = "空名应丢弃" },
                },
            };
            Directory.CreateDirectory(Path.GetDirectoryName(JsonConfigStore.FileFullPath)!);
            File.WriteAllText(JsonConfigStore.FileFullPath, legacy.ToJsonString());

            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            var migrated = Assert.Single(vm.Connections);
            Assert.Equal("老PG", migrated.Name);
            Assert.Equal("192.168.9.9", migrated.ServerIP);
            Assert.Equal("legacydb", migrated.DBName);
            Assert.Equal(ESQLToolType.Dapper, migrated.ToolType);

            // 导入后新文件落盘、老节清除。
            Assert.True(File.Exists(SqlViewModel.ProfilePath));
            var root = JsonNode.Parse(File.ReadAllText(JsonConfigStore.FileFullPath))?.AsObject();
            Assert.NotNull(root);
            Assert.False(root!.ContainsKey("SqlConnections"));
        }

        [Fact]
        public void LegacySingleSection_ImportsToOneProfile()
        {
            var legacy = new JsonObject
            {
                ["Sql"] = new JsonObject
                {
                    ["ServerIP"] = "192.168.9.9",
                    ["Port"] = "5433",
                    ["UserName"] = "admin",
                    ["Password"] = "pw",
                    ["DBName"] = "legacydb",
                    ["DbType"] = "MySQL",
                    ["ToolType"] = "Dapper",
                },
            };
            Directory.CreateDirectory(Path.GetDirectoryName(JsonConfigStore.FileFullPath)!);
            File.WriteAllText(JsonConfigStore.FileFullPath, legacy.ToJsonString());

            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            var migrated = Assert.Single(vm.Connections);
            Assert.Equal(ESQLType.MySQL, migrated.DBType);
            Assert.Equal(ESQLToolType.Dapper, migrated.ToolType);
            Assert.Equal("192.168.9.9", migrated.ServerIP);
        }
    }
}
