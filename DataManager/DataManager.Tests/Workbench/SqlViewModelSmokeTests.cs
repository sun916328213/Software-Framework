using DataManager.SQL;
using DataManager.Workbench.ViewModels;
using System.Diagnostics;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// SqlViewModel 端到端冒烟测试（无头驱动全部命令，连本机 PG 真库）。
    /// 覆盖胶水层：EnsureDb 指纹重建、Connect/Add/Query/Update/Delete 全链路、UiThread 无 Dispatcher 直行。
    /// 连接信息复用集成测试约定：DM_PG_* 环境变量，默认 127.0.0.1:5432/postgres/dm_smoke。
    /// </summary>
    public class SqlViewModelSmokeTests
    {
        private static string Env(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

        private static SqlViewModel CreateVm(string password, string db = "dm_smoke") => new(new SQLManager())
        {
            ServerIP = Env("DM_PG_HOST", "127.0.0.1"),
            Port = Env("DM_PG_PORT", "5432"),
            UserName = Env("DM_PG_USER", "postgres"),
            Password = password,
            DBName = Env("DM_PG_DB", db),
        };

        private static string PgPassword() => Env("DM_PG_PASSWORD", "postgres");

        private static void WaitUntil(Func<bool> cond, SqlViewModel vm, string what, int timeoutMs = 30000)
        {
            var sw = Stopwatch.StartNew();
            while (!cond())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException($"{what} 超时，输出：{vm.OutputText}");
                Thread.Sleep(100);
            }
        }

        private static void WaitMarker(SqlViewModel vm, string marker) =>
            WaitUntil(() => vm.OutputText.Contains(marker), vm, marker);

        [Fact]
        public void FullFlow_ConnectAddQueryUpdateDelete()
        {
            var vm = CreateVm(PgPassword());
            vm.ConnectCommand.Execute();
            WaitUntil(() => vm.IsConnected, vm, "连接");
            Assert.Equal("已连接", vm.StatusText);

            try
            {
                vm.DeleteAllCommand.Execute();
                WaitMarker(vm, "✓ 清空表");

                vm.AddRangeCommand.Execute();
                WaitMarker(vm, "✓ 批量添加 10 条");

                vm.QueryAllCommand.Execute();
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Results.Count == 10, vm, "查询10条");

                vm.QueryConditionCommand.Execute();
                WaitMarker(vm, "✓ 条件查询");

                vm.QueryAllCommand.Execute();
                WaitMarker(vm, "✓ 查询全部");
                vm.SelectedEntity = vm.Results.First();
                var oldAge = vm.SelectedEntity.Age;
                var oldName = vm.SelectedEntity.Name;
                vm.UpdateSelectedCommand.Execute();
                WaitMarker(vm, "✓ 更新选中行");

                vm.QueryAllCommand.Execute();
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Results.Any(x => x.Name == oldName + "_改" && x.Age == oldAge + 1), vm, "更新生效");

                vm.SelectedEntity = vm.Results.First(x => x.Name == oldName + "_改");
                vm.DeleteSelectedCommand.Execute();
                WaitMarker(vm, "✓ 删除选中行");

                vm.QueryAllCommand.Execute();
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Results.Count == 9, vm, "删除后9条");
            }
            finally
            {
                vm.DeleteAllCommand.Execute();
                WaitMarker(vm, "✓ 清空表");
                vm.DisconnectCommand.Execute();
                Assert.False(vm.IsConnected);
            }
        }

        [Fact]
        public void WrongPasswordThenFixed_Reconnects()
        {
            var vm = CreateVm("wrong-password");
            vm.ConnectCommand.Execute();
            WaitUntil(() => vm.OutputText.Contains("✗"), vm, "等待连接失败");
            Assert.False(vm.IsConnected);

            // 纠正密码后重连：指纹变化必须重建实例，一次连上（第 11 节 bug 的端到端覆盖）
            vm.Password = PgPassword();
            vm.ConnectCommand.Execute();
            WaitUntil(() => vm.IsConnected, vm, "纠正密码后重连");
            vm.DisconnectCommand.Execute();
        }
    }
}
