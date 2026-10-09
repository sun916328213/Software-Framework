// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMIManagerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Extension.Core.Conversion、Prism.Ioc、System、System.Collections.ObjectModel、System.Windows.Input、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.Core.Conversion;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels.Log
{
    /// <summary>
    /// SEMI 配置页：SECS 的 ReportInfo / EventInfo 两张表（<c>Config\SecsJson\ReportID\ReportID.csv</c>
    /// 与 <c>Config\SecsJson\CEID\CEID.csv</c>）。
    ///
    /// <para>
    /// <b>T12c：两个保存按钮此前完全失效。</b>页面上的两个 <c>ConfigManagerView</c> **都没有**设置
    /// <c>FilePath</c> / <c>FileName</c>（它们绑的是内存里的表，不是磁盘文件），而控件的内置
    /// <c>SaveData</c> 开头就是：
    /// <code>
    /// if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(FileName))
    /// {
    ///     MessageBox.Show("请检查文件路径(FilePath)和文件名称(FileName)", "提示");
    ///     return;                       // ← 早退，下面的 SaveCallback?.Invoke() 永远到不了
    /// }
    /// </code>
    /// ⇒ 点"保存"只会弹一个"请检查文件路径"的框，<c>ReportSaveCommand</c> / <c>EventSaveCommand</c>
    /// **一次都不会被调用**，SECS 报表 / 事件表也就永远无法从本页落盘。
    /// 改绑 <c>SaveCommand</c>（T10 的 seam）后控件内置的 <c>SaveData</c> 整条路径都不再执行，
    /// 早退分支自然被绕开。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>本页的表格是只读快照。</b><c>ISecsGemConfiguration.SaveReports()</c> **不接受任何参数** ——
    /// 它保存的是 <c>SecsGemRuntime</c> 自己那张表，不是 <c>Datas.Reports</c>。
    /// 所以状态行只说"已保存 SECS ReportInfo 配置表"，**不**声称表格里的内容被保存了。
    /// 把表格编辑写回磁盘需要给契约加 <c>SaveReports(IEnumerable&lt;...&gt;)</c> 重载，属独立任务（见报告 §8）。
    /// </para>
    /// </summary>
    internal class SEMIManagerViewModel : ObservableObject
    {

        public SEMIManagerService Service { get; set; }
        public SEMIManagerModel Datas => Service.Model;

        /// <summary>页面 <c>Loaded</c> 时触发（<c>i:InvokeCommandAction</c>），读两张 SECS 表。</summary>
        public ICommand LoadCommand { get; }

        /// <summary>保存 SECS ReportInfo 表。绑到 ReportInfo 那个 <c>ConfigManagerView.SaveCommand</c>。</summary>
        public ICommand ReportSaveCommand { get; }

        /// <summary>保存 SECS EventInfo 表。绑到 EventInfo 那个 <c>ConfigManagerView.SaveCommand</c>。</summary>
        public ICommand EventSaveCommand { get; }

        private string statusText = string.Empty;

        /// <summary>
        /// 最近一次加载 / 保存的结果说明；初始为空。失败时写清原因，且不出现"已保存"字样。
        /// </summary>
        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value);
        }

        /// <summary>
        /// 初始化 SEMIManagerViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、RelayCommand；具体条件和先后次序见方法体。
        /// </summary>
        public SEMIManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<SEMIManagerService>();

            LoadCommand = new RelayCommand(Load);
            ReportSaveCommand = new RelayCommand(() => Save(Service.ReportSave, "SECS ReportInfo 配置表"));
            EventSaveCommand = new RelayCommand(() => Save(Service.EventSave, "SECS EventInfo 配置表"));
        }

        /// <summary>
        /// 读两张 SECS 表。异常在**命令边界**消化：本命令由 XAML 的 <c>Loaded</c> 触发器调用，
        /// 抛出去会直接冒到 Dispatcher 把整个界面带崩，而"配置文件缺失 / 被 Excel 占住"是
        /// 预期内的用户场景，必须变成一条可见提示而不是一次崩溃。
        /// 顺序与改造前一致（先 Report 后 Event）。
        /// </summary>
        private void Load()
        {
            try
            {
                Service.ReportLoad();
                Service.EventLoad();
                StatusText = string.Empty;
            }
            catch (Exception ex)
            {
                StatusText = $"加载 SECS 配置失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 保存一张 SECS 表。异常在命令边界就地消化并落到 <see cref="StatusText"/>：
        /// 失败时**不**出现"已保存"字样 —— 这一页此前连"保存成功"都报不出来（按钮根本没生效）。
        /// </summary>
        /// <param name="save">服务上的保存入口。</param>
        /// <param name="what">被保存的东西，用于状态行文案（说清保存的是哪张表）。</param>
        private void Save(Action save, string what)
        {
            try
            {
                save();
                StatusText = $"已保存 {what}";
            }
            catch (Exception ex)
            {
                StatusText = $"保存失败，{what}未改变：{ex.Message}";
            }
        }
    }
}
