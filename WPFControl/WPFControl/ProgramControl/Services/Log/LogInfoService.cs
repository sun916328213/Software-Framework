using Prism.Ioc;
using System;
using WPFCustomControls.Controls;
using WPFControl.Helper;
using DataManager.Log.Definition;
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
        /// 同线程重入闸门。本方法的方法体由 <see cref="TryCatchHelper.TryExecute"/> 包裹，
        /// 而 TryCatchHelper 的 catch 又会回调本方法；一旦「写日志」这件事本身会抛，
        /// 就是「抛 → 记 → 再抛」的无限递归，实测直接 StackOverflowException
        /// （托管栈溢出不可捕获、无 dump、进程即死，表现为 UI 线程被独占而卡死）。
        ///
        /// 闸门下在本方法这个**公共节点**上，一次切断所有环（含将来新加的边）。
        /// 必须按线程隔离：用实例字段会让 A 线程的递归把 B 线程的正常日志一起丢掉。
        /// </summary>
        [ThreadStatic]
        private static bool _reporting;

        /// <summary>本线程被闸门丢弃的重入条数（仅在最外层调用结束时上报一次，避免静默失败）。</summary>
        [ThreadStatic]
        private static int _suppressedReentrantCount;

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
            if (string.IsNullOrEmpty(info)) return;

            if (_reporting)
            {
                _suppressedReentrantCount++;
                return;
            }

            _reporting = true;
            try
            {
                // 用「不回报」的变体：本方法就是日志上报的终点，它的失败只能降级为 Trace，
                // 绝不能再经由 TryCatchHelper 的 catch 回到本方法（那是无限递归）。
                TryCatchHelper.TryExecuteWithoutReport(() =>
                {
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
                _reporting = false;
                ReportSuppressedReentrant();
            }
        }

        /// <summary>把本线程被闸门丢弃的重入条数写进 Trace 诊断，避免静默失败。</summary>
        private static void ReportSuppressedReentrant()
        {
            if (_suppressedReentrantCount == 0) return;

            var suppressed = _suppressedReentrantCount;
            _suppressedReentrantCount = 0;
            try
            {
                System.Diagnostics.Trace.TraceError(
                    $"[LogInfoService] 丢弃 {suppressed} 条重入日志（日志上报路径成环，已由重入闸门截断）");
            }
            catch
            {
                // 诊断通道自身的故障不得向上传播。
            }
        }
    }
}