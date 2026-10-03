// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogInfoServiceRecursionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Diagnostics、System.IO、DataManager.Core.Log、DataManager.Runtime.Log。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Diagnostics;
using System.IO;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using DryIoc;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Prism.DryIoc;
using Prism.Ioc;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Services;
using Xunit;

namespace WPFControls.Tests
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
    /// 注册容器自己的错误执行器并绑定同一容器的 LogInfoService，
    /// 避免没有上报目标时的空操作使递归测试假通过。
    ///
    /// ⚠️ **闸门本身在本测试里打不到**：`AddRunInfo` 的重入闸门只在真的重入时生效，而唯一
    /// 的结构性重入边（报警环）需要一整套报警栈（`SecsGemRuntime` / `IExcel` / 匹配的报警项）
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
        /// <summary>
        /// 验证 AddRunInfo_日志持续写失败_应当返回而不是无限递归 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、WriteAllText；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
                    container.RegisterInstance<DataManager.Core.Log.ILogManager>(manager);
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

                    // 错误执行器与日志服务来自同一容器，并配置真实的上报回路。
                    provider.RegisterSingleton<TryCatchHelper>(() => new TryCatchHelper(error =>
                        provider.Resolve<LogInfoService>().AddRunInfo(error.ToString(), ELogLevel.SysError)));

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
