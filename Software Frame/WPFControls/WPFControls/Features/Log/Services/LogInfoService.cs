// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogInfoService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Prism.Ioc、System、WPFCustomControls.Controls、WPFControls.Controls.Diagnostics、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Prism.Ioc;
using System;
using WPFCustomControls.Controls;
using WPFControls.Controls.Diagnostics;
using DataManager.Core.Log.Definition;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using System.Threading.Tasks;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// LogInfoService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：DataGridService&lt;LogInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class LogInfoService : DataGridService<LogInfo>
    {
        private readonly TryCatchHelper _errors;

        ILogManager logManager;
        ILog log =>  (logManager.TryGet("RunLog", out var log) ? log : null) ;
        LogAlarmInfoService logAlarmInfoService;

        /// <summary>
        /// 同线程重入闸门。历史上本方法由 TryExecute 包裹，
        /// 而 TryCatchHelper 的 catch 又会回调本方法；一旦「写日志」这件事本身会抛，
        /// 就是「抛 → 记 → 再抛」的无限递归，实测直接 StackOverflowException
        /// （托管栈溢出不可捕获、无 dump、进程即死，表现为 UI 线程被独占而卡死）。
        ///
        /// 闸门下在本方法这个**公共节点**上，一次切断所有环（含将来新加的边）。
        /// 按服务实例及线程隔离，避免其他容器或线程的正常日志被丢弃。
        /// </summary>
        private readonly ThreadLocal<bool> _reporting = new();

        /// <summary>本线程被闸门丢弃的重入条数（仅在最外层调用结束时上报一次，避免静默失败）。</summary>
        private readonly ThreadLocal<int> _suppressedReentrantCount = new();

        /// <summary>
        /// 初始化 LogInfoService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public LogInfoService(IContainerProvider container, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            logManager= container.Resolve<ILogManager>();
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

            if (_reporting.Value)
            {
                _suppressedReentrantCount.Value++;
                return;
            }

            _reporting.Value = true;
            try
            {
                // 用「不回报」的变体：本方法就是日志上报的终点，它的失败只能降级为 Trace，
                // 绝不能再经由 TryCatchHelper 的 catch 回到本方法（那是无限递归）。
                _errors.TryExecuteWithoutReport(() =>
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
                _reporting.Value = false;
                ReportSuppressedReentrant();
            }
        }

        /// <summary>把本线程被闸门丢弃的重入条数写进 Trace 诊断，避免静默失败。</summary>
        private void ReportSuppressedReentrant()
        {
            if (_suppressedReentrantCount.Value == 0) return;

            var suppressed = _suppressedReentrantCount.Value;
            _suppressedReentrantCount.Value = 0;
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
