// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlViewModelCancellationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Workbench.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace DataManager.Workbench.Tests
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

        /// <summary>
        /// 测试相关入口 Profile；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static SQLParam Profile(string name, string file) => new()
        {
            Name = name,
            DBType = ESQLType.SQLite,
            ToolType = ESQLToolType.EFCore,
            ConnectionString = $"Data Source={file};",
        };

        /// <summary>
        /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Clear、Profile、Add；具体条件和先后次序见方法体。
        /// </summary>
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
            Assert.Empty(workspace.Model.Results);           // 界面未被回写
        }

        /// <summary>释放后走 VM 命令：不崩、不动结果集，只记一行失败。</summary>
        [Fact]
        public void AfterDispose_VmCommand_DoesNotTouchModelResults()
        {
            var (workspace, vm) = Create();
            workspace.Dispose();

            vm.QueryAllCommand.Execute(null);
            WaitUntil(() => vm.OutputText.Contains("✗ 查询全部"), vm, "释放后查询被拒");

            Assert.Empty(vm.Model.Results);
            Assert.False(vm.Model.IsConnected);
        }

        /// <summary>
        /// 新操作接管：签发新 Token 前必须取消上一个在途操作（避免同一 Workspace 上并发跑两套数据库任务）。
        /// </summary>
        [Fact]
        public void OverlappingOperation_CancelsPreviousInFlight()
        {
            var (workspace, vm) = Create();
            using (workspace)
            {
                vm.ConnectCommand.Execute(null);
                WaitUntil(() => vm.Model.IsConnected, vm, "连接");
                vm.AddRangeCommand.Execute(null);
                WaitUntil(() => vm.OutputText.Contains("✓ 批量添加 10 条"), vm, "批量添加");

                var previous = workspace.BeginOperation();      // 模拟“上一个操作仍在途”
                vm.QueryAllCommand.Execute(null);               // 新操作接管
                WaitUntil(() => vm.Model.Results.Count == 10, vm, "查询10条");

                Assert.True(previous.IsCancellationRequested);  // 旧操作被取消
            }
        }

        /// <summary>
        /// 已释放的 Workspace 签发的 Token 一定是已取消状态——
        /// 这是「关闭后不会再有后台任务回写界面」的第一道闸。
        /// </summary>
        [Fact]
        public void DisposedWorkspace_BeginOperation_ReturnsCancelledToken()
        {
            var (workspace, _) = Create();
            workspace.Dispose();

            Assert.True(workspace.BeginOperation().IsCancellationRequested);
        }

        /// <summary>
        /// EF Core + SQLite 的库级锁等待应上报 provider 的 SQLITE_BUSY 错误。
        /// 先等待查询命令完成，再取消；立即取消可能在后台操作入场前生效，不能用于断言锁等待结果。
        /// 此用例不证明运行中的 SQLite 语句可被取消，具体中断能力仍由 provider 决定。
        ///
        /// 三个环境细节（都必要，否则锁拿不到或读不被阻塞）：
        /// ① <c>Pooling=False</c>：Microsoft.Data.Sqlite 默认连接池会把物理连接（及其文件句柄）
        ///    留到进程结束，EF 的 Open/Close 只是借还池子 ⇒ blocker 的 BEGIN EXCLUSIVE 永远拿不到独占锁；
        /// ② <c>Default Timeout=1</c>：把锁等待压到 1 秒，避免拖慢测试；
        /// ③ <c>PRAGMA journal_mode=DELETE</c>：WAL 模式下读不被写阻塞，必须归位到回滚日志模式。
        /// </summary>
        [Fact]
        public async Task EFCore_Sqlite_LockWait_SurfacesProviderErrorAsync()
        {
            var workspace = new SqlWorkspace<TestEntity>(new SQLManager());
            var vm = new SqlViewModel<TestEntity>(workspace);
            vm.Model.DataCollection.Clear();
            var profile = Profile("A", _fileA);
            profile.ConnectionString = $"Data Source={_fileA};Default Timeout=1;Pooling=False;";
            vm.Model.DataCollection.Add(profile);
            vm.Model.SelectedConnection = profile;

            using (workspace)
            {
                // 连接会建库建表；Pooling=False 保证 Connect 结束后物理连接已释放。
                vm.ConnectCommand.Execute(null);
                WaitUntil(() => vm.Model.IsConnected, vm, "连接");

                // 归位 journal_mode（否则 WAL 下读不阻塞），再独占锁库。
                using var blocker = new SqliteConnection($"Data Source={_fileA};Pooling=False;");
                blocker.Open();
                using (var pragma = blocker.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA journal_mode=DELETE;";
                    pragma.ExecuteScalar();
                }
                using (var lockCmd = blocker.CreateCommand())
                {
                    lockCmd.CommandText = "BEGIN EXCLUSIVE;";
                    lockCmd.ExecuteNonQuery();
                }

                // Execute 不保证后台查询已入场。等待完整命令，验证真正进入 provider 的锁等待错误。
                await vm.QueryAllCommand.ExecuteAsync(null);
                vm.CancelInFlight();

                WaitUntil(() => vm.OutputText.Contains("✗ 查询全部"), vm, "锁等待按 provider 错误收场", 30000);
                Assert.Contains("database is locked", vm.OutputText);
                Assert.DoesNotContain("已取消", vm.OutputText);
            }
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { if (File.Exists(_fileA)) File.Delete(_fileA); } catch { }
            try { if (File.Exists(_fileB)) File.Delete(_fileB); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
