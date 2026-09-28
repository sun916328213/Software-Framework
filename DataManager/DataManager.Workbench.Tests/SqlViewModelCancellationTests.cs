using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.Workbench.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// P9-5：Workbench 可取消操作与退出安全。
    /// 约定：重连 / 切换档案 / 断开 / 释放都会先取消在途操作；释放后不再接受新操作、
    /// 也不会再回写界面（结果集与状态）；取消属正常流程，按「已取消」记录而不是 ✗ 错误。
    /// </summary>
    public class SqlViewModelCancellationTests : IDisposable
    {
        private readonly string _fileA = Path.Combine(
            Path.GetTempPath(), $"dm_wb_cancel_a_{Guid.NewGuid():N}.db");

        private readonly string _fileB = Path.Combine(
            Path.GetTempPath(), $"dm_wb_cancel_b_{Guid.NewGuid():N}.db");

        private static SQLParam Profile(string name, string file) => new()
        {
            Name = name,
            DBType = ESQLType.SQLite,
            ToolType = ESQLToolType.EFCore,
            ConnectionString = $"Data Source={file};",
        };

        private (SqlWorkspace<TestEntity> Workspace, SqlViewModel<TestEntity> Vm) Create()
        {
            var workspace = new SqlWorkspace<TestEntity>(new SQLManager());
            var vm = new SqlViewModel<TestEntity>(workspace);
            vm.Model.DataCollection.Clear();
            var a = Profile("A", _fileA);
            vm.Model.DataCollection.Add(a);
            vm.Model.SelectedConnection = a;
            return (workspace, vm);
        }

        private static void WaitUntil(Func<bool> cond, SqlViewModel<TestEntity> vm, string what, int timeoutMs = 30000)
        {
            var sw = Stopwatch.StartNew();
            while (!cond())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException($"{what} 超时，输出：{vm.OutputText}");
                Thread.Sleep(50);
            }
        }

        /// <summary>对照组：正常路径必须把结果集回写（否则“不回写”类断言没有意义）。</summary>
        [Fact]
        public void HappyPath_QueryAll_WritesResultsBack()
        {
            var (workspace, vm) = Create();
            using (workspace)
            {
                vm.ConnectCommand.Execute(null);
                WaitUntil(() => vm.Model.IsConnected, vm, "连接");

                vm.AddRangeCommand.Execute(null);
                WaitUntil(() => vm.OutputText.Contains("✓ 批量添加 10 条"), vm, "批量添加");

                vm.QueryAllCommand.Execute(null);
                WaitUntil(() => vm.Model.Results.Count == 10, vm, "查询10条");
                Assert.True(vm.Model.IsConnected);
            }
        }

        /// <summary>切换数据库配置（指纹变化 → 重建实例）必须先取消在途操作。</summary>
        [Fact]
        public void ProfileSwitch_CancelsInFlightOperation()
        {
            var (workspace, vm) = Create();
            using (workspace)
            {
                var inFlight = workspace.BeginOperation();
                Assert.False(inFlight.IsCancellationRequested);

                var b = Profile("B", _fileB);
                vm.Model.DataCollection.Add(b);
                vm.Model.SelectedConnection = b;
                workspace.EnsureDb(b);

                Assert.True(inFlight.IsCancellationRequested);
            }
        }

        /// <summary>断开连接必须先取消在途操作（否则断开后仍有任务回写“已连接”）。</summary>
        [Fact]
        public void Disconnect_CancelsInFlightOperation()
        {
            var (workspace, vm) = Create();
            using (workspace)
            {
                var inFlight = workspace.BeginOperation();
                vm.DisconnectCommand.Execute(null);

                Assert.True(inFlight.IsCancellationRequested);
                Assert.False(vm.Model.IsConnected);
            }
        }

        /// <summary>VM 的取消入口必须直达 Workspace 的在途操作。</summary>
        [Fact]
        public void VmCancelInFlight_ReachesWorkspace()
        {
            var (workspace, vm) = Create();
            using (workspace)
            {
                var inFlight = workspace.BeginOperation();
                vm.CancelInFlight();
                Assert.True(inFlight.IsCancellationRequested);
            }
        }

        /// <summary>释放：取消在途操作 + 断开并从 SQLManager 摘除实例 + 拒绝后续操作。</summary>
        [Fact]
        public void Dispose_CancelsInFlight_ReleasesInstance_AndRejectsNewOperations()
        {
            var manager = new SQLManager();
            var workspace = new SqlWorkspace<TestEntity>(manager);
            var profile = Profile("A", _fileA);
            workspace.Model.DataCollection.Clear();
            workspace.Model.DataCollection.Add(profile);
            workspace.Model.SelectedConnection = profile;

            var db = workspace.EnsureDb(profile);
            db.Connect();
            Assert.Equal(1, manager.Count);

            var inFlight = workspace.BeginOperation();
            workspace.Dispose();

            Assert.True(inFlight.IsCancellationRequested);   // 在途操作被取消
            Assert.Equal(0, manager.Count);                  // 实例已摘除
            Assert.False(db.IsConnected);                    // 已断开

            // 释放后不接受新操作（避免后台任务在已释放实例上继续跑）。
            Assert.Throws<ObjectDisposedException>(() => workspace.EnsureDb(profile));
            Assert.Equal(0, workspace.Model.Results.Count);  // 界面未被回写
        }

        /// <summary>释放后走 VM 命令：不崩、不动结果集，只记一行失败。</summary>
        [Fact]
        public void AfterDispose_VmCommand_DoesNotTouchModelResults()
        {
            var (workspace, vm) = Create();
            workspace.Dispose();

            vm.QueryAllCommand.Execute(null);
            WaitUntil(() => vm.OutputText.Contains("✗ 查询全部"), vm, "释放后查询被拒");

            Assert.Equal(0, vm.Model.Results.Count);
            Assert.False(vm.Model.IsConnected);
        }

        /// <summary>
        /// 在途取消：用另一条连接独占锁住库文件，让查询可预期地卡在锁等待上，
        /// 再取消——运行中的查询必须被中断，且 VM 记「已取消」而不是 ✗ 错误。
        /// </summary>
        [Fact]
        public void MidFlightCancel_AbortsRunningQuery_AndVmLogsCancelled()
        {
            var (workspace, vm) = Create();
            using (workspace)
            {
                vm.ConnectCommand.Execute(null);
                WaitUntil(() => vm.Model.IsConnected, vm, "连接");
                vm.AddRangeCommand.Execute(null);
                WaitUntil(() => vm.OutputText.Contains("✓ 批量添加 10 条"), vm, "批量添加");

                // 注意：锁命令必须与方法同生命周期——若提前 Dispose 掉 SqliteCommand，
                // 连接会在归还/清理时结束事务，锁随即失效（实测查询会立刻通过）。
                using var blocker = new SqliteConnection($"Data Source={_fileA};");
                blocker.Open();
                using var pragma = blocker.CreateCommand();
                pragma.CommandText = "PRAGMA journal_mode=DELETE;";
                pragma.ExecuteScalar();
                using var lockCmd = blocker.CreateCommand();
                lockCmd.CommandText = "BEGIN EXCLUSIVE;";
                lockCmd.ExecuteNonQuery();

                vm.QueryAllCommand.Execute(null);   // 被独占锁挡住 → 停在途
                Thread.Sleep(300);                  // 等它确实进入锁等待
                vm.CancelInFlight();                // 取消在途操作

                WaitUntil(() => vm.OutputText.Contains("已取消"), vm, "取消日志");
                Assert.DoesNotContain("✗ 查询全部", vm.OutputText);
                Assert.Equal(0, vm.Model.Results.Count); // 取消后不回写结果集
            }
        }

        public void Dispose()
        {
            try { if (File.Exists(_fileA)) File.Delete(_fileA); } catch { }
            try { if (File.Exists(_fileB)) File.Delete(_fileB); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
