// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlViewModelSqliteTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Workbench.Models、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Workbench.Models;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using System.Diagnostics;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// 工作台 SQLite 端到端（无头驱动 SqlViewModel，全命令经 DataManager，文件自清，CI 可跑）。
    /// 证明 DbConnectionProfile.ToSqlParam(SQLite) 经 SQLManager 到 ISQL 整链可用。
    /// </summary>
    public class SqlViewModelSqliteTests : IDisposable
    {
        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_wb_sqlite_{Guid.NewGuid():N}.db");

        /// <summary>
        /// 测试相关入口 CreateVm；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Clear、Add；具体条件和先后次序见方法体。
        /// </summary>
        private SqlViewModel<TestEntity> CreateVm()
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            vm.Model.DataCollection.Clear();
            var profile = new SQLParam
            {
                Name = "lite-it",
                DBType = ESQLType.SQLite,
                ToolType = ESQLToolType.EFCore,
                ConnectionString = $"Data Source={_file};",
            };
            vm.Model.DataCollection.Add(profile);
            vm.Model.SelectedConnection = profile;
            return vm;
        }

        /// <summary>
        /// 测试相关入口 WaitUntil；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 StartNew、cond、Sleep；具体条件和先后次序见方法体。
        /// </summary>
        private static void WaitUntil(Func<bool> cond, SqlViewModel<TestEntity> vm, string what, int timeoutMs = 30000)
        {
            var sw = Stopwatch.StartNew();
            while (!cond())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException($"{what} 超时，输出：{vm.OutputText}");
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 测试相关入口 WaitMarker；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitUntil、Contains；具体条件和先后次序见方法体。
        /// </summary>
        private static void WaitMarker(SqlViewModel<TestEntity> vm, string marker) =>
            WaitUntil(() => vm.OutputText.Contains(marker), vm, marker);

        /// <summary>
        /// 测试相关入口 FullFlow_ConnectAddQueryDeleteAll；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateVm、Execute、WaitUntil、WaitMarker、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FullFlow_ConnectAddQueryDeleteAll()
        {
            var vm = CreateVm();
            vm.ConnectCommand.Execute(null);
            WaitUntil(() => vm.Model.IsConnected, vm, "连接");
            try
            {
                vm.AddRangeCommand.Execute(null);
                WaitMarker(vm, "✓ 批量添加 10 条");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Model.Results.Count == 10, vm, "查询10条");

                vm.DeleteAllCommand.Execute(null);
                WaitMarker(vm, "✓ 清空表");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Model.Results.Count == 0, vm, "清空后0条");
            }
            finally
            {
                vm.DisconnectCommand.Execute(null);
                Assert.False(vm.Model.IsConnected);
            }
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
