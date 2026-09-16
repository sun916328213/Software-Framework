using System;
using System.IO;
using DataManager.Log;
using DataManager.Log.Definition;
using DryIoc;
using Extension.Struct;
using Prism.DryIoc;
using Prism.Ioc;
using WPFControl.ProgramControl.Services;
using Xunit;

namespace WPFControl.Tests
{
    /// <summary>
    /// 回归测试：<see cref="LogInfoService.AddRunInfo"/> 的**两条递归环**必须被重入闸门截断。
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
    /// 复现手法：让 RunLog **必然写失败** —— 先建一个**文件**，再把它当父目录用，
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

                    // ⚠️ 别在这里再 RegisterInstance<IContainerProvider>/<IContainerExtension>：
                    // DryIocContainerExtension 的构造函数**自己**就把 this 注册成了
                    // IContainerExtension / IContainerProvider / DryIocContainerExtension。
                    // 手动再注册一次会造成同一服务两个默认注册，DryIoc 解析
                    // LogAlarmInfoService(IContainerProvider) 时抛
                    // `Error.ExpectedSingleDefaultFactory: Expecting a single default
                    //  registration but found many`（本测试第一版就踩了，报错信息在
                    // Assert.Null(captured) 里，很容易被误读成产品缺陷）。
                    var provider = new DryIocContainerExtension(container);

                    var service = new LogInfoService(provider);

                    // 修复前：这一行会无限递归 → 栈溢出，测试进程直接死。
                    // 修复后：外层记一次、内层被闸门丢弃，正常返回。
                    service.AddRunInfo("回归测试：日志写入必失败", ELogLevel.SysError);
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
            thread.Start();

            var completed = thread.Join(TimeSpan.FromSeconds(20));

            try { File.Delete(blocker); } catch { /* 清理失败不影响断言 */ }

            Assert.True(
                finished && completed,
                "AddRunInfo 未在 20s 内返回 —— 重入闸门失效，递归/卡死复现。");
            Assert.Null(captured);
        }
    }
}
