// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlConnectionStoreTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Workbench.Models、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Workbench.Models;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using Extension.Core.Conversion;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using System.ComponentModel;

namespace DataManager.Workbench.Tests
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

        /// <summary>
        /// 初始化 SqlConnectionStoreTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Exists、ReadAllText、Delete；具体条件和先后次序见方法体。
        /// </summary>
        public SqlConnectionStoreTests()
        {
            _configBackup = File.Exists(ProfilePath)
                ? File.ReadAllText(ProfilePath)
                : null;
            if (File.Exists(ProfilePath))
                File.Delete(ProfilePath);
        }

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Restore、SuppressFinalize；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            Restore(ProfilePath, _configBackup);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 验证 Restore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete、WriteAllText；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 SqlParam_RaisesPropertyChanged 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Contains、nameof；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Ctor_WithoutConfig_HasExactlyOneProfile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Single、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Ctor_WithoutConfig_HasExactlyOneProfile()
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            Assert.Single(vm.Model.DataCollection);
            Assert.NotNull(vm.Model.SelectedConnection);
        }

        /// <summary>
        /// 验证 AddRemove_ViaManagerCallbacks_UpdatesSelectionAndCount 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、OnConnectionAddedExternally、Equal、RemoveAt、OnConnectionDeletedExternally；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 DeleteLastRow_RestoresDefault 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Clear、OnConnectionDeletedExternally、Single、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DeleteLastRow_RestoresDefault()
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            vm.Model.DataCollection.Clear();
            vm.OnConnectionDeletedExternally();
            Assert.Single(vm.Model.DataCollection);
            Assert.Equal("默认连接", vm.Model.SelectedConnection!.Name);
        }

        /// <summary>
        /// 验证 SaveLoad_RoundTripsAllFields 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、OnConnectionAddedExternally、Save、ToList、Equal；具体条件和先后次序见方法体。
        /// </summary>
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
