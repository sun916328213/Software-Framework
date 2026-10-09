// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoManagerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Prism.Ioc、System、System.Threading.Tasks、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using System;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels.Alarm
{
    /// <summary>
    /// 报警信息设置页：经 <c>ConfigManagerView</c> 编辑报警表（<c>Config\Alarm.csv</c>）。
    ///
    /// <para>
    /// <b>T12b：保存动作从「控件按钮 + SaveCallback 回调」改成页面自己的命令。</b>
    /// 与同目录的 <see cref="AlarmInfoQueryViewModel"/> 保持一致，用
    /// <see cref="AsyncRelayCommand"/> 包一个**具名 async 方法**：
    /// 改造前是 <c>Action</c> + <c>async () =&gt; { await ... }</c>（async-void lambda，VSTHRD101）——
    /// 报警 CSV 被 Excel 占住时写盘必失败，异常从 async-void 逃逸出来会直接崩掉进程。
    /// </para>
    ///
    /// <para>
    /// 改绑 <c>SaveCommand</c> 同时消除了控件内置的 <c>JsonEx.Save</c>：改造前点一次保存会先往
    /// <c>Alarm.csv</c> 写一份 JSON，再由回调的 CSV 覆盖回去，覆盖失败就永久停在 JSON 上。
    /// </para>
    /// </summary>
    internal class AlarmInfoManagerViewModel : ObservableObject
    {
        private readonly AlarmInfoManagerService Service;

        public AlarmInfoManagerModel Datas => Service.Model;

        /// <summary>
        /// 保存命令。绑到 <c>ConfigManagerView.SaveCommand</c>（T10 的 seam），覆盖控件内置的
        /// 「自己写盘 + 调 SaveCallback」行为，落盘交给 <see cref="AlarmInfoManagerService.Save"/>，
        /// 结果写进 <see cref="StatusText"/> —— 失败绝不上报成功。
        /// </summary>
        public IAsyncRelayCommand SaveCommand { get; }

        private string statusText = string.Empty;

        /// <summary>
        /// 最近一次加载 / 保存的结果说明；初始为空。加载期问题（缺文件、读失败）在构造时就摆出来，
        /// 保存失败时写清原因且不出现"已保存"字样。
        /// </summary>
        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value);
        }

        /// <summary>
        /// 初始化 AlarmInfoManagerViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、AsyncRelayCommand；具体条件和先后次序见方法体。
        /// </summary>
        public AlarmInfoManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<AlarmInfoManagerService>();
            SaveCommand = new AsyncRelayCommand(SaveAsync);

            // 报警表在构造期就 Load 过：空表可能是"没配"，也可能是"文件没找到"。后者必须说出来。
            StatusText = Service.LoadMessage;
        }

        /// <summary>
        /// 保存报警表。异常在命令边界就地消化并落到 <see cref="StatusText"/>：
        /// 命令里逃出去的异常会冒到 Dispatcher 把整个界面带崩，而"保存失败"（CSV 被 Excel 占住、
        /// 地址为空、ID 重复）是预期内的用户场景，必须变成一条可见的提示而不是一次崩溃。
        /// 失败路径**不重载**旧数据、也不刷新界面 —— 用户正在编辑的内容原样保留。
        /// </summary>
        private async Task SaveAsync()
        {
            try
            {
                await Service.Save();
                StatusText = $"已保存：{Service.Model.FullPath}";
            }
            catch (Exception ex)
            {
                StatusText = $"保存失败，配置未改变：{ex.Message}";
            }
        }
    }
}
