// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlViewModelSmokeTests。成员说明可结合下方具体实现阅读。
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
    /// SqlViewModel 端到端冒烟测试（无头驱动全部命令，连本机 PG 真库）。
    /// 覆盖胶水层：EnsureDb 指纹重建、Connect/Add/Query/Update/Delete 全链路、UiThread 无 Dispatcher 直行。
    /// 连接信息复用集成测试约定：DM_PG_* 环境变量，默认 127.0.0.1:5432/postgres/dm_smoke。
    /// 需真库密码（DM_PG_PASSWORD），CI 无库时跳过（见 SqlViewModelSqliteTests 的自清文件库）。
    /// </summary>
    [Xunit.Trait("Category", "ExternalDatabase")]
    public class SqlViewModelSmokeTests
    {
        /// <summary>
        /// 测试相关入口 Env；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetEnvironmentVariable；具体条件和先后次序见方法体。
        /// </summary>
        private static string Env(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

        /// <summary>
        /// 测试相关入口 PgConnectionString；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Env；具体条件和先后次序见方法体。
        /// </summary>
        private static string PgConnectionString(string password, string db) =>
            $"Host={Env("DM_PG_HOST", "127.0.0.1")};Port={Env("DM_PG_PORT", "5432")};" +
            $"Database={Env("DM_PG_DB", db)};Username={Env("DM_PG_USER", "postgres")};Password={password};";

        /// <summary>
        /// 测试相关入口 CreateVm；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 PgConnectionString；具体条件和先后次序见方法体。
        /// </summary>
        private static SqlViewModel<TestEntity> CreateVm(string password, string db = "dm_smoke")
        {
            var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager()));
            vm.Model.SelectedConnection!.DBType = ESQLType.PostgreSQL;
            vm.Model.SelectedConnection.ToolType = ESQLToolType.EFCore;
            vm.Model.SelectedConnection.ConnectionString = PgConnectionString(password, db);
            return vm;
        }

        /// <summary>
        /// 测试相关入口 PgPassword；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Env；具体条件和先后次序见方法体。
        /// </summary>
        private static string PgPassword() => Env("DM_PG_PASSWORD", "postgres");

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
        /// 测试相关入口 FullFlow_ConnectAddQueryUpdateDelete；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateVm、PgPassword、Execute、WaitUntil、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FullFlow_ConnectAddQueryUpdateDelete()
        {
            var vm = CreateVm(PgPassword());
            vm.ConnectCommand.Execute(null);
            WaitUntil(() => vm.Model.IsConnected, vm, "连接");
            Assert.Equal("已连接", vm.Model.StatusText);

            try
            {
                vm.DeleteAllCommand.Execute(null);
                WaitMarker(vm, "✓ 清空表");

                vm.AddRangeCommand.Execute(null);
                WaitMarker(vm, "✓ 批量添加 10 条");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Model.Results.Count == 10, vm, "查询10条");

                vm.Model.SelectedEntity = vm.Model.Results.First();
                var oldName = vm.Model.SelectedEntity.Name;
                vm.UpdateSelectedCommand.Execute(null);
                WaitMarker(vm, "✓ 更新选中行");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Model.Results.Any(x => x.Name == oldName + "_改"), vm, "更新生效");

                vm.Model.SelectedEntity = vm.Model.Results.First(x => x.Name == oldName + "_改");
                vm.DeleteSelectedCommand.Execute(null);
                WaitMarker(vm, "✓ 删除选中行");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Model.Results.Count == 9, vm, "删除后9条");
            }
            finally
            {
                vm.DeleteAllCommand.Execute(null);
                WaitMarker(vm, "✓ 清空表");
                vm.DisconnectCommand.Execute(null);
                Assert.False(vm.Model.IsConnected);
            }
        }

        /// <summary>
        /// 测试相关入口 WrongPasswordThenFixed_Reconnects；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateVm、Execute、WaitUntil、Contains、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WrongPasswordThenFixed_Reconnects()
        {
            var vm = CreateVm("wrong-password");
            vm.ConnectCommand.Execute(null);
            WaitUntil(() => vm.OutputText.Contains("✗"), vm, "等待连接失败");
            Assert.False(vm.Model.IsConnected);

            // 纠正密码后重连：指纹变化必须重建实例，一次连上（第 11 节 bug 的端到端覆盖）
            vm.Model.SelectedConnection!.ConnectionString = PgConnectionString(PgPassword(), "dm_smoke");
            vm.ConnectCommand.Execute(null);
            WaitUntil(() => vm.Model.IsConnected, vm, "纠正密码后重连");
            vm.DisconnectCommand.Execute(null);
        }
    }
}
