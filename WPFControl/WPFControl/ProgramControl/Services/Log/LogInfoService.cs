using Prism.Ioc;
using System;
using WPFCustomControls.Controls;
using WPFControl.Helper;
using Extension.Struct;
using DataManager.Log;
using System.Threading.Tasks;

namespace WPFControl.ProgramControl.Services
{
    public class LogInfoService : DataGridService<LogInfo>
    {
        LogManager logManager;
        ILog log =>  logManager["RunLog"] ;
        LogAlarmInfoService logAlarmInfoService;

        /// <summary>
        /// 重入闸门（每线程一份）。
        ///
        /// <see cref="AddRunInfo"/> 恰好是**两条递归环的公共节点**，所以闸门下在这里：
        ///   ① `TryCatchHelper` 的 catch 里会回调 `AddRunInfo`（`Helper\TryCatchHelper.cs` 7 处），
        ///      而本方法体又被 `TryExecute` 包着 ⇒ 只要「日志写不出去」这件事本身会抛，
        ///      就是「抛 → 记 → 再抛 → 再记」的无限递归：UI 线程被独占，表现为**界面卡死**，
        ///      最终 StackOverflowException（托管栈溢出进程直接死，没有可捕获点）。
        ///      触发条件不是偶发：`Packing Machine\Config\LogConfig.json` 的 RunLog 路径是
        ///      `D:\DeviceData\Log\RunLog`，开发机没有 D 盘 ⇒ `Directory.CreateDirectory` 每次必抛。
        ///   ② `LogAlarmInfoService.Add` → `AlarmInfoManagerService.TriggerAlarm(islog:true)`
        ///      也会回到本方法。这条环**不经过任何 catch**，只在 catch 里加保护拦不住它。
        ///
        /// ⇒ 结论：日志系统**绝不允许递归进自己**。这里是通用做法（重入即丢弃），
        ///   而不是针对某一条环打补丁 —— 两条环都从这里断掉。
        /// </summary>
        [ThreadStatic]
        private static bool _isWriting;

        public LogInfoService(IContainerProvider container)
        {
            logManager= container.Resolve<LogManager>();
            logAlarmInfoService= container.Resolve<LogAlarmInfoService>();
        }

        /// <summary>
        /// 添加运行信息到控件显示和日志文件
        /// </summary>
        /// <param name="info">日志信息</param>
        /// <param name="level">日志级别</param>
        /// <param name="retainNum">控件中保留的最大数据条数</param>
        public void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000)
        {
            // 已在本次写入流程中 ⇒ 丢弃。再走一遍就是无限递归；而且这条路走不通时，
            // UI 列表与日志文件本来也写不进去，唯一有意义的是把原文留给调试器。
            if (_isWriting)
            {
                System.Diagnostics.Debug.WriteLine($"[LogInfoService] 写入日志过程中再次失败，已丢弃：{info}");
                return;
            }

            _isWriting = true;
            try
            {
                TryCatchHelper.TryExecute(() =>
                {
                    if (string.IsNullOrEmpty(info)) return;

                    // 添加到控件显示（UI线程）
                    var logInfo = new LogInfo(DateTime.Now.ToString("HH:mm:ss"), info, level);

                    Add(logInfo, retainNum);

                    logAlarmInfoService?.Add(logInfo, retainNum);
                    // 记录到日志文件 
                    log?.Add(info, level);
                });
            }
            finally
            {
                _isWriting = false;
            }
        }
    }
}