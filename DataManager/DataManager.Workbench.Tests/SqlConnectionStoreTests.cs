using DataManager.SQL;
using DataManager.Workbench.Models;
using DataManager.SQL.Definition;
using Extension.Core.Conversion;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using System.ComponentModel;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// SQL 连接档案：行模型通知、增删保底、新文件（Config/SqlConnections.json）往返。
    /// 持久化走 ConfigManager 控件保存按钮，此处用同格式的 <see cref="JsonEx"/> 读写验证兼容性。
    /// 注意：含数据库密码明文，仅限本机调试工作台使用；测前后原样恢复配置文件。
    /// </summary>
    public class SqlConnectionStoreTests : IDisposable
    {
        private static string ProfilePath => new SqlPageModel<TestEntity>().FullPath!;

        private readonly string? _configBackup;

        public SqlConnectionStoreTests()
        {
            _configBackup = File.Exists(ProfilePath)
                ? File.ReadAllText(ProfilePath)
                : null;
            if (File.Exists(ProfilePath))
                File.Delete(ProfilePath);
        }

        public void Dispose()
        {
            Restore(ProfilePath, _configBackup);
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
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            Assert.Single(vm.Model.DataCollection);
            Assert.NotNull(vm.Model.SelectedConnection);
        }

        [Fact]
        public void AddRemove_ViaManagerCallbacks_UpdatesSelectionAndCount()
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            // 模拟控件增加按钮：空行已由控件加入，回调命名选中（保存由控件保存按钮完成）。
            vm.Model.DataCollection.Add(new SQLParam());
            vm.OnConnectionAddedExternally();
            Assert.Equal(2, vm.Model.DataCollection.Count);
            Assert.Equal("连接2", vm.Model.SelectedConnection!.Name);

            vm.Model.SelectedConnection.ServerIP = "db01";
            Assert.Equal("db01", vm.Model.DataCollection[1].ServerIP);

            // 模拟控件删除按钮：行已由控件移除，回调保底。
            vm.Model.DataCollection.RemoveAt(1);
            vm.OnConnectionDeletedExternally();
            Assert.Single(vm.Model.DataCollection);
            Assert.NotNull(vm.Model.SelectedConnection);
        }

        [Fact]
        public void DeleteLastRow_RestoresDefault()
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            vm.Model.DataCollection.Clear();
            vm.OnConnectionDeletedExternally();
            Assert.Single(vm.Model.DataCollection);
            Assert.Equal("默认连接", vm.Model.SelectedConnection!.Name);
        }

        [Fact]
        public void SaveLoad_RoundTripsAllFields()
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            vm.Model.SelectedConnection!.Name = "主库";
            vm.Model.SelectedConnection.ServerIP = "10.0.0.1";
            vm.Model.SelectedConnection.Port = "5433";
            vm.Model.SelectedConnection.DBName = "main";
            vm.Model.SelectedConnection.UserName = "u";
            vm.Model.SelectedConnection.Password = "p";
            vm.Model.DataCollection.Add(new SQLParam());
            vm.OnConnectionAddedExternally();
            vm.Model.SelectedConnection!.Name = "文件库";
            vm.Model.SelectedConnection.DBType = ESQLType.SQLite;
            vm.Model.SelectedConnection.FilePath = "backup.db";
            // 与控件保存按钮同格式落盘，再新起 VM 读回。
            JsonEx.Save(vm.Model.DataCollection.ToList(), ProfilePath);

            var reloaded = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            Assert.Equal(2, reloaded.Model.DataCollection.Count);
            var primary = Assert.Single(reloaded.Model.DataCollection, p => p.Name == "主库");
            Assert.Equal("10.0.0.1", primary.ServerIP);
            Assert.Equal("5433", primary.Port);
            Assert.Equal("main", primary.DBName);
            Assert.Equal("u", primary.UserName);
            Assert.Equal("p", primary.Password);
            var fileDb = Assert.Single(reloaded.Model.DataCollection, p => p.Name == "文件库");
            Assert.Equal(ESQLType.SQLite, fileDb.DBType);
            Assert.Equal("backup.db", fileDb.FilePath);
        }
    }
}
