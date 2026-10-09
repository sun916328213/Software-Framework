// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogConfigViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System、System.Windows.Input、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// 日志配置界面（仿 CamManagerViewModel）：经 ConfigManagerView 编辑并保存统一日志配置（Config\LogConfig.json）。
    ///
    /// <para>
    /// <b>T12a：保存动作从「控件按钮 + SaveCallback 回调」改成页面自己的命令。</b>
    /// <see cref="SaveCommand"/> 绑到 <c>ConfigManagerView.SaveCommand</c>（T10 的 seam），覆盖控件内置的
    /// 「自己写盘 + 调 SaveCallback」行为，把落盘与生效统一交给 <see cref="LogConfigService.Save"/>，
    /// 结果写进 <see cref="StatusText"/> —— 失败绝不上报成功。
    /// </para>
    /// </summary>
    public class LogConfigViewModel : ObservableObject
    {
        readonly LogConfigService Service;

        public LogConfigModel Datas => Service.Model;

        /// <summary>
        /// 保存命令。绑到 <c>ConfigManagerView.SaveCommand</c>；覆盖后控件的内置
        /// <c>SaveData</c>（含 <c>JsonEx.Save</c> 与 <c>SaveCallback</c>）都不再执行。
        /// </summary>
        public ICommand SaveCommand { get; }

        private string statusText = string.Empty;

        /// <summary>
        /// 最近一次保存的结果说明；失败时写清原因，成功时给出落盘位置。初始为空。
        /// </summary>
        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value);
        }

        /// <summary>
        /// 初始化 LogConfigViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public LogConfigViewModel(LogConfigService service)
        {
            Service = service;
            SaveCommand = new RelayCommand(Save);
        }

        /// <summary>
        /// 保存并立即生效。异常在命令边界就地消化并落到 <see cref="StatusText"/>：
        /// 命令里抛出的异常会直接冒到 Dispatcher 把整个界面带崩，而"保存失败"（路径不可写、
        /// 配置项缺字段）是预期内的用户场景，必须变成一条可见的提示而不是一次崩溃。
        /// 失败路径**不重载**旧数据、也不刷新界面 —— 用户正在编辑的内容原样保留。
        /// </summary>
        private void Save()
        {
            try
            {
                Service.Save();
                StatusText = $"已保存并生效：{Service.Model.FullPath}";
            }
            catch (Exception ex)
            {
                StatusText = $"保存失败，配置未生效：{ex.Message}";
            }
        }
    }
}
