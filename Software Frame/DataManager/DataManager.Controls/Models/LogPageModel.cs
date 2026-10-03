// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、CommunityToolkit.Mvvm.ComponentModel、DataManager.Core.Log.Definition、System.Collections.ObjectModel、System.IO。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Core.Log.Definition;
using System.Collections.ObjectModel;
using System.IO;
using WPFCustomControls.Controls;

namespace DataManager.Controls.Models
{
    /// <summary>
    /// 日志测试页状态 Model：继承通用配置管理模型（对标 WPFControls 的 LogConfigModel）。
    /// 行实体为 Extension 的 <see cref="LogInfo"/>；查询条件（时间范围/文件）与结果同住此处。
    /// </summary>
    public partial class LogPageModel : ConfigManagerModel<LogParam>
    {
        /// <summary>
        /// 初始化 LogPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine；具体条件和先后次序见方法体。
        /// </summary>
        public LogPageModel()
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            FileName = "LogConfig.json";
        }

        /// <summary>写入内容。</summary>
        [ObservableProperty]
        private string message = "这是一条测试日志";

        [ObservableProperty]
        private ELogLevel selectedLevel = ELogLevel.Information;

        [ObservableProperty]
        private string? selectedFile;

        [ObservableProperty]
        private ObservableCollection<string> files = new();

        [ObservableProperty]
        private DateTime startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime endDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<LogInfo> results = new();

        [ObservableProperty]
        private string multiText = string.Empty;

        [ObservableProperty]
        private LogParam? selectedLogParam;

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedLogParamChanged。
        /// 正文中的关键协作调用包括 Clear；具体条件和先后次序见方法体。
        /// </summary>
        partial void OnSelectedLogParamChanged(LogParam? value)
        {
            // 切换档案仅重置文件列表/结果，实例由 Workspace 指纹重建
            Files.Clear();
            SelectedFile = null;
        }

        /// <summary>当前档案（选中行；空即报“请先添加/选择”）。兼容旧调用：无选中时回退首行。</summary>
        public LogParam CurrentProfile() => SelectedProfile();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SelectedProfile。
        /// 正文中的关键协作调用包括 FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        public LogParam SelectedProfile() =>
            SelectedLogParam ?? DataCollection.FirstOrDefault() ?? throw new InvalidOperationException("请先添加日志配置档案");
    }
}
