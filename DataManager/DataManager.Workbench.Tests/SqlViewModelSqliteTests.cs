using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.Workbench.Services;
using DataManager.Workbench.ViewModels;
using System.Diagnostics;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// 工作台 SQLite 端到端（无头驱动 SqlViewModel，全命令经 DataManager，文件自清，CI 可跑）。
    /// 证明 DbConnectionProfile.ToSqlParam(SQLite) 经 SQLManager 到 ISQL 整链可用。
    /// </summary>
    public class SqlViewModelSqliteTests : IDisposable
    {
        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_wb_sqlite_{Guid.NewGuid():N}.db");

        private SqlViewModel CreateVm()
        {
            var vm = new SqlViewModel(new SqlWorkspace(new SQLManager()));
            vm.Connections.Clear();
            var profile = new SQLParam
            {
                Name = "lite-it",
                DBType = ESQLType.SQLite,
                ToolType = ESQLToolType.EFCore,
                ConnectionString = $"Data Source={_file};",
            };
            vm.Connections.Add(profile);
            vm.SelectedConnection = profile;
            return vm;
        }

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
        public void FullFlow_ConnectAddQueryDeleteAll()
        {
            var vm = CreateVm();
            vm.ConnectCommand.Execute(null);
            WaitUntil(() => vm.IsConnected, vm, "连接");
            try
            {
                vm.AddRangeCommand.Execute(null);
                WaitMarker(vm, "✓ 批量添加 10 条");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Results.Count == 10, vm, "查询10条");

                vm.DeleteAllCommand.Execute(null);
                WaitMarker(vm, "✓ 清空表");

                vm.QueryAllCommand.Execute(null);
                WaitMarker(vm, "✓ 查询全部");
                WaitUntil(() => vm.Results.Count == 0, vm, "清空后0条");
            }
            finally
            {
                vm.DisconnectCommand.Execute(null);
                Assert.False(vm.IsConnected);
            }
        }

        public void Dispose()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
