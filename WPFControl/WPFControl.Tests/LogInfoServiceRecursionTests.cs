using System;
using System.Diagnostics;
using System.IO;
using DataManager.Log;
using DataManager.Log.Definition;
using DryIoc;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Prism.DryIoc;
using Prism.Ioc;
using WPFControl.Helper;
using WPFControl.ProgramControl.Services;
using Xunit;

namespace WPFControl.Tests
{
    /// <summary>
    /// 回归测试：<see cref="LogInfoService.AddRunInfo"/> 的**两条递归环**必须被截断。
    ///
    /// 修复前：`AddRunInfo` 的方法体被 `TryCatchHelper.TryExecute` 包着，而 `TryCatchHelper`
    /// 的 7 处 catch 又回调 `AddRunInfo` ⇒ 只要「日志写不出去」这件事**本身会抛**，
    /// 就是「抛 → 记 → 再抛 → 再记」的无限递归：UI 线程被独占（表现为**界面卡死**），
    /// 最终 `StackOverflowException`（托管栈溢出进程直接死，没有可捕获点、没有 dump）。
    ///
    /// 第二条环（潜伏）：`AddRunInfo` → `LogAlarmInfoService.Add` →
    /// `AlarmInfoManagerService.TriggerAlarm(islog:true)` → 回到 `AddRunInfo`。
    /// 这条环**不经过任何 catch**，所以只在 catch 里加保护拦不住它 —— 闸门必须下在
    /// `AddRunInfo` 这个公共节点上。本测试覆盖的是第一条环（第二条需要真实报警配置，
    /// 在单测里构造不出，只能在代码注释里留档）。
    ///
    /// ⚠️ **必须构造 <see cref="TryCatchHelper"/>**：它的上报目标 `_logInfoService` 是
    /// **静态字段、只在构造函数里赋值**。不构造它 → 上报目标为 null →
    /// `_logInfoService?.AddRunInfo(...)` 退化成空操作 → 递归环被「意外」切断，
    /// 本测试会**假通过**（第一版就是这样：绿了很久，却什么都没验证）。
    ///
    /// ⚠️ **闸门本身在本测试里打不到**：`AddRunInfo` 的重入闸门只在真的重入时生效，而唯一
    /// 的结构性重入边（报警环）需要一整套报警栈（`SecsControl` / `IExcel` / 匹配的报警项）
    /// 才能触发，单测里构造不出。所以本测试断言的是「切点 1」（异常降级到 Trace、
    /// 不再回到 `AddRunInfo`），闸门属于兜底。
    ///
    /// ✅ **但闸门的兜底效果已被实测确认**（把 `AddRunInfo` 里的 `TryExecuteWithoutReport`
    /// 临时改回 `TryExecute` 复现）：环回来时**没有栈溢出**，而是闸门丢弃那一次重入、
    /// 并在 Trace 里留下 `[LogInfoService] 丢弃 1 条重入日志（日志上报路径成环，已由重入闸门截断）`，
    /// 测试以**普通断言失败**收场（而不是"测试主机进程崩溃"）。
    /// ⇒ 只有「切点 1」与「闸门」**同时**失效，才会退化成不可捕获的 StackOverflowException。
    ///
    /// ⚠️ 复现手法：让 RunLog **必然写失败** —— 先建一个**文件**，再把它当父目录用，
    /// `Directory.CreateDirectory` 必抛 IOException。这样不依赖「本机有没有 D 盘」
    /// （`Packing Machine\Config\LogConfig.json` 里配的 `D:\DeviceData\Log\*` 在开发机上
    /// 必然失败，正是线上卡死的触发条件，但那是环境相关的，不能拿来做断言）。
    /// </summary>
    public class LogInfoServiceRecursionTests
    {
        [Fact]
        public void AddRunInfo_日志持续写失败_应当返回而不是无限递归()
        {
            var blocker = Path.Combine(
                Path.GetTempPath(),
                "loginfo-guard-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, string.Empty);          // 建一个**文件**
            var badDir = Path.Combine(blocker, "RunLog");      // 以文件为父目录 ⇒ 建目录必抛

            Exception? captured = null;
            var finished = false;
            var output = new StringWriter();
            using var listener = new TextWriterTraceListener(output);

            // WPF 的 DispatcherTimer / DataGridService 要求 STA 线程。
            var thread = new Thread(() =>
            {
                try
                {
                    var manager = new LogManager();
                    manager.Create(new LogParam
                    {
                        Name = "RunLog",
                        FilePath = badDir,
                        Separator = "||",
                        SaveDays = 180,
                        LogType = ELogType.SeriLog,
                    });

                    var container = new Container();
                    container.RegisterInstance(manager);
                    container.Register<LogAlarmInfoService>(Reuse.Singleton);
                    container.Register<LogInfoService>(Reuse.Singleton);

                    // ⚠️ 别在这里再 RegisterInstance<IContainerProvider>/<IContainerExtension>：
                    // DryIocContainerExtension 的构造函数**自己**就把 this 注册成了
                    // IContainerExtension / IContainerProvider / DryIocContainerExtension。
                    // 手动再注册一次会造成同一服务两个默认注册，DryIoc 解析
                    // LogAlarmInfoService(IContainerProvider) 时抛
                    // `Error.ExpectedSingleDefaultFactory: Expecting a single default
                    //  registration but found many`（本测试第一版就踩了，报错信息在
                    // Assert.Null(captured) 里，很容易被误读成产品缺陷）。
                    var provider = new DryIocContainerExtension(container);

                    // ⚠️ 这一行是**本测试的关键**：不构造 TryCatchHelper，它的静态上报目标就是
                    // null，环会被"意外"切断，测试会假通过（详见类注释）。
                    _ = new TryCatchHelper(provider);

                    // 从容器取，确保与 TryCatchHelper 上报的是**同一个单例**。
                    provider.Resolve<LogInfoService>()
                        .AddRunInfo("回归测试：日志写入必失败", ELogLevel.SysError);
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    finished = true;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;

            Trace.Listeners.Add(listener);
            try
            {
                thread.Start();

                var completed = thread.Join(TimeSpan.FromSeconds(20));
                listener.Flush();

                Assert.True(
                    finished && completed,
                    "AddRunInfo 未在 20s 内返回 —— 重入闸门失效，递归/卡死复现。");
                Assert.Null(captured);

                // 切点 1 生效的证据：异常确实发生（RunLog 写失败），且走了 Trace 降级通道，
                // 而不是经由 TryCatchHelper 的 catch 回到 AddRunInfo。
                // 若这条断言失败，说明「不回报」的变体没接上，环随时可能复活。
                Assert.Contains(
                    "[TryCatch] 日志上报失败",
                    output.ToString(),
                    StringComparison.Ordinal);
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                try { File.Delete(blocker); } catch { /* 清理失败不影响断言 */ }
            }
        }
    }
}
